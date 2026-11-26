# Nhật ký phiên — Đồ họa thế giới, quái di chuyển và chuỗi map săn quái

Ngày: 2026-11-26  
Người phụ trách: chủ dự án (MinKhang), thực hiện cùng Claude Code  
Phân loại: Gameplay do server quyết định (quái di chuyển, NPC, cổng); Giao diện (map, HUD, NPC); Hiệu năng

## 1. Mục tiêu và phạm vi

- **Mục tiêu:**
  1. Thay sàn màu phẳng bằng map pixel art sống động.
  2. Áp khung UI fantasy cho HUD.
  3. Giảm giật lag.
  4. Cho quái di chuyển quanh điểm spawn.
  5. Mở thêm map săn quái từ Safe Zone 1, mỗi map một loại quái.
  6. Thêm NPC cho làng tân thủ và Safe Zone 1.
- **Trong phạm vi:**
  - Asset miễn phí có giấy phép rõ ràng.
  - Dựng map lúc chạy theo `mapId`.
  - Sửa dự đoán chuyển động và va chạm quái.
  - Protocol 4 (`MonsterMoved`).
  - Dữ liệu 10 map, 10 loại quái, 8 NPC.
  - Popup báo lý do không qua được cổng.
- **Ngoài phạm vi:**
  - Va chạm với cây, nước, vật thể trên map (chỉ là hình).
  - Quái đánh trả, đuổi theo hoặc né người chơi.
  - Chức năng NPC (shop, huấn luyện, nhiệm vụ mới).
  - Màn đăng nhập/đăng ký dùng khung Kenney.
  - Sprite cho Sói (definition 1).

## 2. Kết quả hoàn thành

### Map pixel art (client, chỉ hiển thị)

- Asset **Pixel Crawler – Free Pack** (Anokolisa): cho dùng thương mại, không bắt buộc ghi công, cấm bán lại chính bộ asset.
  - Cắt 36 tile và 64 prop vào `Resources/World/PixelCrawler/`.
  - PPU 20: tile 16 px = 0,8 đơn vị, nên vùng chơi 20×12 khớp đúng 25×15 ô.
- `MapEnvironmentView` dựng Grid và 2 Tilemap (nền và mép cỏ) cùng các prop cho từng `mapId`.
  - `GroundGrid` tự ghép mép cỏ lên đất, đá, gạch, nước, dùng bộ 5×5 dạng "flat" của `Floors_Tiles`.
  - Prop rải ngẫu nhiên có định danh theo hash FNV của `mapId`, nên mỗi lần vào map đều giống nhau.
  - Chỗ đứng của spawn, NPC, cổng được giữ trống.
- Có bố cục riêng cho làng, Bãi Sói, Safe Zone 1 và 10 map mới. Đống lửa có hoạt ảnh (`SpriteFrameAnimator`).
- Sắp lớp theo trục Y: camera dùng `TransparencySortMode.CustomAxis` (0,1,0). Hero, NPC, quái có art và prop đứng cùng `sortingOrder = 1`, `SpriteSortPoint.Pivot`.
- Ẩn object `Floor` cũ.

### HUD (Kenney Fantasy UI Borders, CC0)

- 5 khung trắng ở `Resources/UI/Kenney/`: 9-slice 12 px, PPU 100, tô màu bằng `-unity-background-image-tint-color`.
- Áp cho thẻ nhiệm vụ, menu chức năng, hộp thoại mất kết nối, nút có trạng thái hover/nhấn. Có class dùng lại `.kenney-frame` và `.kenney-primary-button`.
- **Popup cổng dịch chuyển:**
  - Hiện ở phía trên màn hình khi cổng từ chối, tự ẩn sau khoảng 2,6 s.
  - Server gửi kèm `PortalName` và `RequiredLevel`, nên thông báo đúng cho mọi cổng. Trước đây câu thông báo bị viết cứng là "Safe Zone 1 yêu cầu level 2".

### Hiệu năng và chống giật

| Vấn đề | Nguyên nhân | Sửa |
|---|---|---|
| Client chậm hơn server ~17 %, bị kéo vị trí liên tục | `Rigidbody2D.linearDamping = 10` hãm vận tốc trước khi tích phân | Damping = 0 trong `PlayerController.Awake` (vận tốc đặt tường minh mỗi bước) |
| Nhân vật và camera nhảy theo nhịp 50 Hz | Ghi `rigidbody.position` mỗi bước vật lý (clamp biên) làm mất nội suy | Chỉ ghi khi ra ngoài biên; hiệu chỉnh nhỏ cộng vào vận tốc |
| Trễ input, giật vị trí 40–200 ms | Nagle bật cả hai đầu; client ghi header và payload thành 2 lần | `NoDelay = true` ở client và `ClientConnection`; gộp thành 1 lần ghi |
| Khung hình không đều | vSync tắt, không giới hạn FPS | vSync = 1 trên desktop, 60 FPS trên mobile (`AppLifetimeScope`) |
| Chép texture thừa mỗi khung hình | URP PC bật depth/opaque texture dù không shader nào dùng | Tắt trong `PC_RPAsset.asset` |

Chủ dự án xác nhận chuyển động đã mượt.

### Va chạm quái (khựng một nhịp khi chạm quái)

- **Nguyên nhân:**
  - Client: collider vuông 1×1, Box2D chặn ở khoảng cách 1,0 và cho trượt.
  - Server: hình tròn bán kính 0,35 + 0,5 = 0,85, dừng hẳn tại điểm chạm.
  - Hai bên ra vị trí khác nhau nên server phải hiệu chỉnh.
- **Sửa:**
  - `CircleObstacleResolver` là bản sao phía client của `MonsterCollisionMovementResolver`, dùng để dự đoán.
  - `MonsterSpawner` cung cấp vị trí quái qua `IMovementObstacleSource`.
  - Collider của quái chuyển sang trigger, chỉ còn dùng để click.

### Quái di chuyển quanh điểm spawn (server quyết định)

- **Logic:**
  - `MonsterWanderPolicy`: bán kính, tốc độ, thời gian nghỉ min/max.
  - `Monster` giữ chặng đi hiện tại, vị trí là hàm của thời gian (`PositionAt`).
  - Lần gọi đầu chỉ lên lịch nghỉ ngẫu nhiên, để cả đàn không đồng loạt xuất phát.
  - Quái chết dừng tại chỗ; hồi sinh về điểm spawn.
- **Vị trí thực tế được dùng ở:** va chạm, đẩy người chơi khi quái hồi sinh, tầm đánh, `ListMonsters` và `MonsterRespawned`. Trước đây cả 5 chỗ dùng `SpawnPosition`.
- **Gói tin:** vòng lặp quái gọi `AdvanceWander` và broadcast `MonsterMoved` theo map, mỗi chặng một gói. `MonsterPacketMapper` gom phần chuyển snapshot sang packet, trước đây lặp ở 2 nơi.
- **Client:** `MonsterView` phát lại chặng đi (bắt đầu từ vị trí đang hiển thị nếu lệch ≤ 1 đơn vị). Đã sửa lỗi quái bị đặt lại về vị trí cũ mỗi lần mất máu (`Render` không còn đặt lại vị trí).

### 10 map săn quái từ Safe Zone 1

- Cổng phía đông Safe Zone 1 (9, 0) dẫn sang chuỗi map. Mỗi map có cổng tây (-9, 0) để về và cổng đông (9, 0) để đi tiếp. Thêm spawn point `safe_zone_01_east`.

| # | Map | Quái (id) | Cấp quái / cấp vào | Máu | EXP |
|---|---|---|---|---|---|
| 1 | `slime_meadow_01` Đồng Slime | Slime Xanh (2) | 2 | 80 | 14 |
| 2 | `spider_woods_01` Rừng Nhện | Nhện Hang (3) | 3 | 100 | 18 |
| 3 | `dragonfly_marsh_01` Đầm Chuồn Chuồn | Chuồn Chuồn Độc (4) | 4 | 120 | 23 |
| 4 | `serpent_lake_01` Hồ Rắn Nước | Rắn Nước (5) | 5 | 140 | 29 |
| 5 | `turtle_shore_01` Bờ Rùa Gai | Rùa Gai (6) | 6 | 160 | 36 |
| 6 | `goblin_camp_01` Trại Goblin | Goblin Chiến Binh (7) | 7 | 180 | 43 |
| 7 | `ghoul_swamp_01` Đầm Lầy Quỷ | Quỷ Đầm Lầy (8) | 8 | 200 | 52 |
| 8 | `eye_ruins_01` Phế Tích Mắt Quỷ | Mắt Quỷ (9) | 9 | 220 | 61 |
| 9 | `bone_graveyard_01` Nghĩa Địa Xương | Xương Thương Binh (10) | 10 | 240 | 71 |
| 10 | `mage_tower_01` Tháp Pháp Sư | Pháp Sư Bóng Tối (11) | 11 | 260 | 82 |

- Mỗi map 6 con, hồi sinh sau 12 s. EXP được tính để khoảng 12 con lên một cấp.
- Sprite quái do chủ dự án cung cấp (ảnh test), cắt vào `Resources/Monsters/monster_{2..11}.png` (PPU 32, nền xám 128 chuyển thành trong suốt).

### NPC (chỉ nói chuyện)

- **Làng:** Trưởng Làng, Bác Nông Dân, Lính Gác Làng; Mẹ có thêm hình.
- **Safe Zone 1:** Thương Nhân, Hiệp Sĩ Chỉ Huy, Pháp Sư Lang Thang, Chủ Quán, Trinh Sát.
- Hình idle 4 khung từ Pixel Crawler ở `Resources/Npcs/{look}/idle_0..3.png`. Trường mới `appearanceId` trong `npcDefinitions`.
- **Sửa lỗi nghiêm trọng có sẵn:** `InteractNpcPacketHandler` coi mọi NPC là NPC nhiệm vụ tân thủ. Giờ chỉ `tutorial.QuestNpcDefinitionId` gọi logic nhiệm vụ; NPC khác chỉ trả `InitialDialogue`.

### Dọn scene

- Xóa một bản Monster prefab đặt tay trong `InGame.unity` (thêm từ commit `e9267f7`). Object này không có dữ liệu server, không chọn được, hiện ở mọi map vì mọi map dùng chung scene `InGame`.

## 3. File và module thay đổi

- **Server:**
  - Mới: `Monsters/MonsterWanderPolicy.cs`, `Networking/MonsterPacketMapper.cs`.
  - Sửa: `Monsters/Monster.cs`, `MonsterService.cs`, `MonsterSnapshot.cs`, `Combat/MonsterCombatService.cs`, `World/MonsterCollisionMovementResolver.cs`, `World/MonsterRespawnDisplacementResolver.cs`, `Networking/ClientConnection.cs`, `Networking/Handlers/ListMonstersPacketHandler.cs`, `Networking/Handlers/TutorialWorldPacketHandlers.cs`, `Configuration/ServerOptions.cs`, `Program.cs`, `serverSettings.json`.
- **Shared/Packets:** `PacketProtocol` (4), `PacketType` (`MonsterMoved = 53`), `MonsterPackets`, `TutorialWorldPackets`.
- **Client – Gameplay:**
  - Mới: `World/Environment/*` (`MapEnvironmentLayout(s)`, `MapEnvironmentView`, `GroundGrid`, `SpriteFrameAnimator`), `Player/CircleObstacleResolver.cs`.
  - Sửa: `Player/PlayerController.cs`, `Monster/MonsterView.cs`, `Monster/MonsterSpawner.cs`, `World/AuthoritativeWorldPresentation.cs`.
- **Client – khác:**
  - Network: `NetworkClient.cs`, `ClientPacketHandlers.cs`.
  - Data: `MonsterData`, `MonsterEvents`, `TutorialWorldEvents`.
  - UI: `HudPresentation`, `InGameHUD`, `VillageUIToolkitHud`, `VillageHud.uxml`, `VillageHud.uss`.
  - Root: `AppLifetimeScope`, `InGameLifetimeScope`, `InGameSceneRoot`.
  - Scene `InGame.unity`, `Settings/PC_RPAsset.asset`.
- **Asset:** `Resources/World/PixelCrawler`, `Resources/UI/Kenney`, `Resources/Monsters`, `Resources/Npcs`.
- **Ghi nguồn:** `Art/Environment/License/PIXEL_CRAWLER_FREE_PACK.txt`, `Art/UI/License/KENNEY_FANTASY_UI_BORDERS_CC0.txt`.
- **Test:** mới `KnightServer.Tests/World/MonsterWanderTests.cs`; thêm `TalkOnlyNpc_AnswersWithoutTouchingTutorialState` trong `PostgresCharacterRepositoryIntegrationTests.cs`.

## 4. Quyết định kỹ thuật

- **Map dựng bằng code theo `mapId`, không dùng prefab Tilemap tự vẽ.** Cho kết quả ngay, cố định theo seed. Có thể thay bằng prefab vẽ tay sau mà không đổi phần nối với scene (`MapEnvironmentView.Show`).
- **Vật thể trên map không có collider.** Server chỉ biết biên hình chữ nhật và quái; chặn chỉ ở client sẽ gây hiệu chỉnh vị trí. Cây cao chỉ đặt ngoài vùng đi.
- **Quái di chuyển theo chặng (event), không gửi vị trí mỗi tick.** Mỗi chặng một gói; vị trí là hàm của thời gian nên không cần tick vị trí, server vẫn quyết định.
- **Quy tắc chặn quái giữ nguyên "dừng hẳn, không trượt"** của server; client sao chép đúng để dự đoán (CORE-03 cho phép lặp phép tính để dự đoán).
- **Dữ liệu nội dung (map, quái, NPC, hình NPC) nằm ở `serverSettings.json`**; client chỉ ánh xạ id sang resource.
- **Sửa JSON bằng cách chèn vào từng mảng,** không ghi lại toàn bộ file, để giữ định dạng gọn sẵn có (đã kiểm tra 0 dòng bị xóa).

## 5. Contract và tương thích

- **Protocol 3 → 4:**
  - `PacketType.MonsterMoved = 53`, `MonsterMovedPacket`.
  - `MonsterSnapshotPacket` thêm `TargetX/TargetY/MoveRemainingMilliseconds`; `PositionX/Y` giờ là vị trí thực tế.
  - `serverSettings.network` min/max protocol = 4: client bản cũ bị từ chối bằng `VersionMismatch`.
- **Trường thêm, có giá trị mặc định (không đổi version):** `UsePortalResponsePacket.PortalName/RequiredLevel`, `PortalSnapshotPacket.MinimumLevel`, `NpcSnapshotPacket.AppearanceId`.
- `KnightBot` compile cùng file Shared, đã build lại thành công.

## 6. Database, migration và cấu hình

- **Migration:** không có.
- **Cấu hình mới (`world`):** `monsterWanderRadius` 2.5 (0 = tắt), `monsterWanderSpeed` 1.2, `monsterWanderMinimumIdleMilliseconds` 2000, `monsterWanderMaximumIdleMilliseconds` 6000. Có kiểm tra hợp lệ khi khởi động.
- **Cấu hình mới (`npcDefinitions[].appearanceId`):** tùy chọn; chỉ chấp nhận `[a-z0-9_]`.
- **Dữ liệu thêm:** 10 map, 20 cổng, 10 loại quái, 60 spawn, 8 NPC, spawn point `safe_zone_01_east`.
- **Lưu ý vận hành:** server đọc `bin/Debug/net10.0/serverSettings.json` (bản sao chỉ cập nhật khi build). Sửa settings thì phải chạy lại bằng `dotnet run --project KnightServer`. Lần đầu thêm map, cổng không hiện chính vì lý do này.
- **Rollback:** đặt `monsterWanderRadius` = 0 để quái đứng yên; xóa các mục map/NPC đã thêm. Quay protocol về 3 phải revert cả client lẫn server.

## 7. Bảo mật và khả năng quản trị

- **Đầu vào và phân quyền:**
  - NPC không phải NPC nhiệm vụ không còn làm thay đổi trạng thái nhiệm vụ hay phát thưởng.
  - `appearanceId` được kiểm tra để làm đường dẫn resource an toàn.
  - Cấu hình wander có giới hạn trên/dưới.
- **Audit và metric:** không thêm (thay đổi thuộc gameplay hiển thị và di chuyển quái).
- **Giấy phép asset:**
  - Pixel Crawler: không được bán lại hay phân phối như một gói asset.
  - Kenney: CC0.
  - **Sprite quái do chủ dự án cung cấp chưa rõ nguồn**, cần xác nhận giấy phép trước khi phát hành.

## 8. Kiểm tra

| Kiểm tra | Kết quả | Ghi chú |
|---|---|---|
| Build server, client (csproj Unity), KnightBot | Đạt | Client build qua csproj tạm bổ sung file mới vì csproj Unity sinh ra đang cũ |
| `KnightServer.Tests` | 146 đạt, 3 bỏ qua | Gồm 12 test mới cho quái di chuyển, kiểm tra cấu hình và nạp `serverSettings.json` thật |
| `KnightServer.IntegrationTests` (PostgreSQL) | 19/19 đạt | Gồm test NPC chỉ nói chuyện mới |
| Server khởi động với settings mới | Đạt | Nghe cổng 7777, nạp 68 quái |
| Render thử map (harness gọi `GroundGrid` thật + ghép ảnh) | Đạt | Làng, Bãi Sói, Safe Zone 1, 4 map mới |
| HUD Kenney (trang HTML mô phỏng 9-slice + tô màu) | Đạt | Chưa xem trong Unity |
| Chuyển động mượt trong Unity | Chủ dự án xác nhận | |
| Map, quái di chuyển, NPC, popup cổng trong Unity | Chưa chạy | Cần smoke test |
| Nhiều client / quái di chuyển nhìn từ 2 client | Chưa chạy | |

## 9. Rủi ro và nợ kỹ thuật

- **Quái đi đè lên người chơi đứng yên.** Người chơi vẫn đi ra được, nhưng nhìn lạ. Cần thêm luật né ở server.
- **Vị trí quái phía client chậm hơn server ~RTT/2.** Chạm quái đang di chuyển có thể bị hiệu chỉnh nhẹ (dưới ngưỡng 0,15).
- **Bán kính chặn quái 0,85 ghi cố định ở client** cho khớp server. Nên để server gửi xuống.
- **Mật độ pixel khác nhau:** hero 64 px/đơn vị, cảnh 20, quái 32.
- **Có thể có đường kẻ giữa các tile khi camera di chuyển.** Cách xử lý: Sprite Atlas có padding.
- **Sói chưa có sprite;** màn đăng nhập/đăng ký chưa dùng khung Kenney.
- **Nhiệm vụ tân thủ cho 0 EXP từ sói,** nên người chơi mới có thể kẹt ở cấp thấp trước các cổng có giới hạn cấp.
- **csproj Unity sinh ra đang thiếu file mới.** Unity sẽ tự sinh lại khi mở editor; không ảnh hưởng build trong Unity.

## 10. Bàn giao và bước tiếp theo

- **Trạng thái:** mọi thay đổi **chưa commit**. Server đã tắt hết khi kết thúc phiên.
- **Việc tiếp theo:**
  1. Smoke test trong Unity: 13 map, quái di chuyển, NPC, popup cổng, HUD Kenney.
  2. Commit theo nhóm: art map, HUD, hiệu năng, quái di chuyển (protocol 4), chuỗi map, NPC.
  3. Xác nhận giấy phép sprite quái hoặc thay bằng asset có giấy phép.
  4. Quái né người chơi / đánh trả.
  5. Sprite cho Sói; áp khung Kenney cho màn đăng nhập/đăng ký.
- **File nên đọc trước:** `KnightServer/Monsters/Monster.cs`, `KnightClient/.../World/Environment/MapEnvironmentView.cs`, `MapEnvironmentLayouts.cs`, `KnightServer/serverSettings.json`.
- **Điểm chặn:** không có.
- **Commit/PR:** chưa có.

## 11. Hướng dẫn test phiên này

1. Đóng và mở lại scene `InGame` trong Unity (chọn **Reload** nếu được hỏi) để không lưu đè bản scene cũ còn chứa khối hồng.
2. Chạy server:

   ```powershell
   dotnet run --project D:\Project\KnightOnline\KnightServer
   ```

   Log cần thấy "Loaded 68 monster instance(s)".
3. Vào làng: kiểm tra nền, ruộng, đống lửa cháy, 4 NPC có hoạt ảnh; click NPC thường chỉ hiện câu thoại, nhiệm vụ của Mẹ không đổi.
4. Qua Safe Zone 1: 5 NPC, cổng phía đông. Đi tới cổng khi chưa đủ cấp → popup ghi đúng tên cổng và cấp.
5. Ở Đồng Slime: quái đi lang thang, chạm quái không khựng, đánh quái thì quái không bị đặt lại về vị trí cũ.
