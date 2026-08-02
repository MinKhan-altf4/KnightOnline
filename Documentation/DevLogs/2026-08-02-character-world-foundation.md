# DevLog — Character boundary và EnterWorld foundation

Ngày: 2026-08-02  
Người phụ trách: Chủ dự án + Codex  
Phân loại: Critical (Character/authorization), Authoritative gameplay

## 1. Mục tiêu và phạm vi

- Củng cố Character persistence và packet boundary trên PostgreSQL/TCP thật.
- Tách rõ luồng `SelectCharacter → GameplaySession → EnterWorld snapshot`.
- Sửa vòng đời account heartbeat khi chuyển từ Bootstrap sang InGame.
- Chặn sai lệch movement cơ bản khi Unity bị collider quái chặn nhưng server vẫn
  tiếp tục tính vị trí xuyên qua quái.
- Ngoài phạm vi: persistence vị trí, collision map/NPC/player, full movement
  reconciliation, inventory/loot/EXP và Alpha load test.

## 2. Kết quả hoàn thành

- Character repository trả `Unauthorized` thay vì ném exception khi account đã
  không còn tồn tại.
- Bổ sung PostgreSQL integration tests cho create/list, ownership, idempotency,
  tên trùng và giới hạn ba character/account/server.
- `CreateCharacter` và `ListCharacters` xử lý malformed payload rõ ràng.
- `ListCharactersRequest` mang `ServerId`; server từ chối server ID sai và vẫn có
  fallback tương thích packet cũ chưa có trường này.
- Unity không biến lỗi list thành roster rỗng; lỗi đi qua
  `CharacterListFailedEvent`.
- Bổ sung TCP integration harness đi qua length-prefix socket,
  `ClientConnection`, dispatcher, handler và PostgreSQL thật.
- `SelectCharacter` tạo `PlayerSession` có `GameplaySessionId` riêng.
- Bổ sung `EnterWorldRequest/Response`; Unity chỉ chuyển InGame sau snapshot
  authoritative thành công.
- Retry Select cùng character trả cùng session; retry EnterWorld không claim hoặc
  tạo entity/session lần hai.
- Snapshot trả identity, class/body/appearance, HP, movement, map, spawn, vị trí,
  server UTC và snapshot version.
- Chuyển `AuthenticationFlowService` từ Bootstrap scene scope lên
  `AppLifetimeScope` để heartbeat không bị dispose khi load InGame bằng Single.
- Receive loop dừng ngay sau `ForcedDisconnect`, không đọc socket đã đóng và
  không phát duplicate disconnect event; TCP close thông thường là Warning.
- Thêm `IWorldMovementResolver` và collision resolver quái phía server. Movement,
  combat và EnterWorld dùng chung vị trí/collision authoritative.
- Quái chết không chặn; player được phép thoát nếu quái respawn chồng vị trí.

## 3. File và module thay đổi

- `KnightServer/Persistence/CharacterRepository.cs`
- `KnightServer/Networking/Handlers/CreateCharacterPacketHandler.cs`
- `KnightServer/Networking/Handlers/ListCharactersPacketHandler.cs`
- `KnightServer/Networking/Handlers/SelectCharacterPacketHandler.cs`
- `KnightServer/Networking/Handlers/EnterWorldPacketHandler.cs`
- `KnightServer/Players/PlayerSession.cs`
- `KnightServer/Players/PlayerSessionProfile.cs`
- `KnightServer/World/IWorldMovementResolver.cs`
- `KnightServer/World/MonsterCollisionMovementResolver.cs`
- `KnightServer/Combat/MonsterCombatService.cs`
- `KnightServer.IntegrationTests/Characters/`
- `KnightServer.Tests/World/`
- `KnightClient/.../Shared/Packets/GameplaySessionPackets.cs`
- `KnightClient/.../Network/NetworkClient.cs`
- `KnightClient/.../Network/Handlers/ClientPacketHandlers.cs`
- `KnightClient/.../Root/Bootstrap/AppLifetimeScope.cs`
- `KnightClient/.../Root/Bootstrap/CharacterFlowController.cs`

## 4. Quyết định kỹ thuật

- Character/account ownership luôn được kiểm tra ở server repository/handler.
- Character Select tiếp tục tính là active và phụ thuộc account lease sống.
- Select chỉ thiết lập gameplay session; EnterWorld mới trả snapshot dùng để đổi
  scene.
- Session ID do server sinh, client không được tự quyết định hoặc thay thế.
- Metadata session nằm trong `PlayerSessionProfile`, không nhét packet DTO vào
  gameplay domain.
- Không tăng cứng attack range hoặc tin tọa độ client để che lỗi movement.
- Collision được mở rộng qua `IWorldMovementResolver`; map/NPC/player sẽ bổ sung
  resolver/composition sau mà không viết lại combat.

## 5. Contract và compatibility

- Thêm packet type 36/37: `EnterWorldRequest`, `EnterWorldResponse`.
- `SelectCharacterResponse` thêm `GameplaySessionId`.
- `ListCharactersRequest` thêm `ServerId`; packet cũ thiếu trường được ánh xạ về
  server hiện hành, server ID không rỗng nhưng sai bị từ chối.
- Thêm result `MalformedRequest`, `InvalidServer`, `SessionMismatch` và các event
  presentation tương ứng.
- Client/server mới phải rollout cùng nhau để dùng flow EnterWorld hai bước.
- Chưa có protocol capability negotiation; đây vẫn là technical debt trước khi
  hỗ trợ rolling deployment Production.

## 6. Database, migration và cấu hình

- Không có database migration mới.
- Thêm config Development:
  - `World.PlayerCollisionRadius = 0.35`;
  - `World.MonsterCollisionRadius = 0.5`.
- Config được validate dương khi server khởi động.
- Không thêm hoặc thay đổi secret.
- Forward-fix collision bằng resolver/config; không rollback sang tin vị trí
  client hoặc bỏ range validation.

## 7. Security và Admin Management Contract

- Malformed payload bị chặn trước repository/domain mutation.
- Anonymous/stale lease bị dispatcher từ chối; stale generation nhận forced
  disconnect.
- Account khác không thể list/select character không thuộc sở hữu.
- Retry Create/Select/EnterWorld không tạo duplicate state.
- Character read model hiện đi qua repository/application boundary, có thể dùng
  lại cho Admin API sau này; Admin Web không được truy cập database trực tiếp.
- Admin query, audit event và metrics cho character/world session chưa triển khai.

## 8. Kiểm tra

| Kiểm tra | Kết quả | Ghi chú |
|---|---|---|
| Server Release build | Pass | 0 warning, 0 error |
| Unit/network tests | Pass | 45/45 |
| PostgreSQL/TCP integration | Pass | 13/13, tự cleanup dữ liệu test |
| `git diff --check` | Pass | Không có whitespace error |
| Unity compile | Pass theo xác nhận người dùng | Không xuất hiện compile error |
| Unity Select/EnterWorld | Pass một phần | Đã vào InGame và chọn Monster được |
| Heartbeat xuyên scene | Đã forward-fix | Cần chạy lại soak thủ công 5–10 phút |
| Collision quái cuối phiên | Chưa nghiệm thu thủ công | Automated resolver tests đã pass |

`NU1900` xuất hiện ở integration test vì môi trường local chặn endpoint package
vulnerability (`127.0.0.1:9`); không phải test/build failure nhưng dependency scan
phải chạy lại trong CI có network.

## 9. Rủi ro và technical debt

- Client chưa nhận movement snapshot định kỳ và chưa có prediction
  reconciliation hoàn chỉnh; sai lệch nhỏ vẫn có thể tích lũy.
- Collision hiện mới có quái tĩnh, chưa có map boundary, wall, NPC hoặc
  player-player.
- Bán kính server là config kỹ thuật; cần hiệu chỉnh với collider prefab/map thật.
- Quái respawn chưa có policy dời spawn khi player đứng trong vùng spawn.
- Vị trí character chưa checkpoint/persist sau movement/logout.
- Snapshot version đang là foundation v1; chưa có entity registry/map snapshot đầy
  đủ hoặc interest management.
- Chưa có structured metric cho out-of-range, collision reject, session creation
  và EnterWorld failure.
- Owner: chủ dự án/nhóm gameplay-server. Xử lý trước khi mở Alpha ngoài nhóm.

## 10. Bàn giao và bước tiếp theo

- Trạng thái: Character foundation, TCP boundary và EnterWorld snapshot đã có test
  tự động; collision quái authoritative tối thiểu đã được cài đặt.
- Nghiệm thu đầu phiên sau:
  1. chạy Unity 5–10 phút để xác nhận heartbeat không hết lease;
  2. giết/respawn quái và kiểm tra không còn `OutOfRange` khi đứng cạnh;
  3. kiểm tra mất mạng/reconnect và stale gameplay session.
- Sau nghiệm thu: triển khai movement sequence + authoritative position snapshot +
  client reconciliation, rồi map collision/boundary và checkpoint persistence.
- File nên đọc trước:
  - `KnightServer/Players/PlayerSession.cs`;
  - `KnightServer/World/MonsterCollisionMovementResolver.cs`;
  - `KnightServer/Networking/Handlers/SelectCharacterPacketHandler.cs`;
  - `KnightServer/Networking/Handlers/EnterWorldPacketHandler.cs`;
  - `KnightClient/.../Root/Bootstrap/CharacterFlowController.cs`.
- Blocker Alpha: chưa có load/soak, distributed lease, TLS, secure token store,
  interest management và movement reconciliation hoàn chỉnh.
- Commit/PR: commit chốt phiên cùng DevLog này; không tạo PR trong phiên.
