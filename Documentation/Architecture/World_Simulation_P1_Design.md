# Thiết kế P1 — Mô phỏng thế giới nhiều người chơi

**Trạng thái:** đề xuất triển khai, 14-11-2026. Phân loại: *Authoritative gameplay*.
**Nguồn:** [Đánh giá luồng 13-11-2026](../Audits/2026-11-13-danh-gia-luong-va-huong-di.md) mục 4.4, 4.7, 4.8, 5/P1.
**Giới hạn:** vẫn là một tiến trình server, bind loopback (xem [Hợp đồng phiên](Session_Lease_Contract.md)).

## 1. Mục tiêu

1. Server có **tick thế giới cố định**; mọi kiểm tra tầm/vị trí đọc trạng thái đã được tiến tới thời điểm hiện tại.
2. Người chơi **thấy người chơi khác** trong vùng quan tâm (AOI): xuất hiện, di chuyển mượt, biến mất.
3. Di chuyển của chính mình **không bị kéo ngược** khi RTT bình thường (B6).
4. Gửi dữ liệu không bao giờ chặn vòng mô phỏng; client chậm bị ngắt thay vì làm chậm người khác.
5. Client/server lệch contract bị từ chối rõ ràng khi kết nối (B10).
6. Có bot headless để đo trước khi công bố con số online.

Ngoài phạm vi: PvP, party, chat, đồng bộ hoạt ảnh tấn công của người khác, nhiều instance, UDP.

## 2. Thứ tự triển khai

| Bước | Nội dung | Phụ thuộc |
|---|---|---|
| 1 | Kiểm tra phiên bản protocol + bắt buộc handshake | — |
| 2 | Hàng đợi gửi bounded cho mỗi kết nối | — |
| 3 | World tick phía server | 2 |
| 4 | Đồng bộ người chơi khác với AOI | 2, 3 |
| 5 | Client dự đoán (own) + nội suy (remote) | 3, 4 |
| 6 | Bot headless + load test | 1–4 |

Mỗi bước build/test xanh và commit riêng.

## 3. Phiên bản protocol (B10)

- `ProtocolVersion.Current` (số nguyên) nằm trong `Shared/Packets`, dùng chung client, server và bot. P1 nâng lên **2** vì thêm packet mới và đổi ý nghĩa snapshot vị trí.
- `ConnectRequestPacket` thêm `ProtocolVersion` (int). Client cũ không gửi trường này nên giá trị là 0.
- Server cấu hình `network.minimumProtocolVersion` / `maximumProtocolVersion`. Nằm ngoài khoảng → `ConnectResult.VersionMismatch`, có thông báo, rồi đóng kết nối.
- Dispatcher chỉ nhận `ConnectRequest` khi kết nối chưa handshake; packet khác trước handshake bị bỏ qua và ghi log. Bật bằng cờ của dispatcher để test đơn vị cũ không phải gửi handshake.
- Chính sách tương thích: server hỗ trợ **một** khoảng phiên bản liên tục. Breaking change tăng `Current` và nâng `minimum` khi client cũ hết hạn hỗ trợ. Trường JSON lạ bị bỏ qua (System.Text.Json mặc định).

## 4. Hàng đợi gửi (transport)

- Mỗi `ClientConnection` có `Channel<byte[]>` bounded (mặc định 256 message) và **một** task ghi duy nhất. Thứ tự FIFO được giữ nguyên nên response → snapshot vẫn đúng thứ tự như hiện tại.
- `SendAsync` (phản hồi trực tiếp trong handler của chính kết nối đó): chờ chỗ trong hàng đợi, có backpressure. Một handler chậm chỉ làm chậm kết nối của nó.
- `TryEnqueue` (broadcast, tick): không chờ. Hàng đầy → kết nối bị đánh dấu *slow consumer*, ghi log Warning và đóng. Vòng tick không bao giờ `await` mạng.
- Ngắt kết nối có thông báo (`ForceDisconnect`) đưa packet vào hàng, chờ hàng xả tối đa 2 giây rồi mới đóng.
- `ConnectionRegistry.BroadcastToMapAsync` chuyển sang `TryEnqueue` (bỏ gửi tuần tự).

## 5. World tick

- `WorldSimulation` chạy `PeriodicTimer` với `world.simulationTickMilliseconds` (mặc định 50 ms = 20 Hz). Mỗi tick tăng `ServerTick` (long) và đọc clock inject được.
- Mỗi tick:
  1. Với mỗi `PlayerSession` đang trong thế giới: `AdvancePosition(now)`.
  2. Tính AOI và gửi cập nhật thực thể (mục 6) mỗi `world.entitySnapshotEveryTicks` tick (mặc định 2 → 10 Hz).
  3. Gửi snapshot vị trí của chính người chơi nếu vị trí đổi hoặc có input mới được xác nhận (reason `Tick`).
- Input di chuyển vẫn áp dụng ngay khi nhận (giữ hành vi hiện tại), tick chỉ tiến tiếp. Mọi nơi đọc vị trí (NPC, cổng, combat, lưu) đã gọi `AdvancePosition` từ P0. Tick giữ tính đúng đắn cả khi không có packet đến.
- Thời gian tick được đo (Stopwatch). Tick vượt ngân sách sẽ ghi log Warning có số thực thể; tick không dồn bù (`PeriodicTimer` bỏ nhịp lỡ).
- Lỗi trong một tick được bắt, ghi Error và vòng tiếp tục; một người chơi lỗi không làm dừng thế giới.

## 6. Người chơi khác và AOI

### 6.1 Lưới AOI

- `AoiGrid` thuần (không I/O): ô vuông `world.aoiCellSize` (mặc định 16 đơn vị). Người chơi A thấy B nếu cùng map và ô của B nằm trong 3×3 ô quanh A. Map hiện tại 20×12 nên trên thực tế cả map nhìn thấy nhau; lưới phục vụ map lớn sau này.
- Mỗi kết nối giữ tập `VisibleCharacterIds` trên server để sinh spawn/despawn theo hiệu tập.

### 6.2 Packet mới (protocol 2)

| PacketType | Hướng | Nội dung |
|---|---|---|
| `WorldEntitiesSnapshot = 52` | S→C | `ServerTick`, `ServerTimeUtc`, `Spawned[]` (`CharacterId`, `Name`, `Level`, `ClassDefinitionId`, `X`, `Y`), `Moved[]` (`CharacterId`, `X`, `Y`, `DirectionX`, `DirectionY`), `Despawned[]` (`CharacterId`) |

- Chỉ gửi khi có ít nhất một thay đổi. Người chơi đứng yên không tạo `Moved`.
- Spawn được gửi khi B vào AOI của A, khi A đổi map hoặc vừa vào thế giới. Despawn khi B rời AOI, đổi map hoặc thoát game.
- Không gửi dữ liệu nhạy cảm (account, HP chính xác của người khác). Tên hiển thị là dữ liệu công khai trong game.

### 6.3 Thứ tự và mất gói

TCP giữ thứ tự. Client bỏ snapshot có `ServerTick` ≤ tick đã áp dụng. Sau reconnect, server gửi lại spawn đầy đủ vì tập visible bắt đầu rỗng.

## 7. Client

- **Remote player:** `RemotePlayerPresenter` tạo đối tượng hiển thị (sprite + tên) cho mỗi `Spawned`. Vị trí nội suy trễ `100 ms` theo `ServerTimeUtc` giữa hai mẫu gần nhất; mất mẫu thì đứng ở mẫu cuối, không ngoại suy quá 200 ms.
- **Own prediction:** client lưu lịch sử vị trí dự đoán theo `ClientSequence` đã gửi. Khi snapshot báo `AcknowledgedClientSequence = n`, sai số = `vị trí server − vị trí dự đoán tại n`. Sai số > ngưỡng thì cộng dồn sai số vào vị trí hiện tại (mượt nếu nhỏ, snap nếu ≥ ngưỡng cứng). Như vậy không còn kéo về vị trí cũ một RTT (B6).
- Gameplay không phụ thuộc packet: handler map packet → event `RemotePlayersChangedEvent` trong `Data/Events`.

## 8. Bot headless

- Project console `KnightBot` (net10.0) dùng lại `Shared/Packets` bằng link file. Luồng: connect (protocol) → tạo guest → tạo nhân vật → chọn → vào thế giới → gửi input ngẫu nhiên 10 Hz → đếm `WorldEntitiesSnapshot`.
- Tham số: số bot, tốc độ ramp-up, thời gian chạy. Kết quả: số kết nối thành công, lỗi theo loại, độ trễ response, kích thước/tần suất snapshot.
- Mục tiêu đo theo bậc 50 → 200 → 500. Không công bố con số online nếu chưa có kết quả đo kèm cấu hình máy.
- Bot chỉ chạy với môi trường Development (guest + loopback). Database dùng cho load test phải là DB riêng.

## 9. Bảo mật và kiểm soát lạm dụng

- Input di chuyển giữ kiểm tra hiện có (hữu hạn, sequence tăng). Server không nhận vị trí từ client.
- Hàng đợi gửi bounded và ngắt slow consumer ngăn một client làm phình bộ nhớ server.
- AOI giới hạn số thực thể gửi cho mỗi người. `world.maximumVisiblePlayers` (mặc định 100) chặn trên kích thước packet.

## 10. Quan sát và quản trị (CORE-11, CORE-19)

- Log có cấu trúc tối thiểu (mức hiện tại vẫn là `Console`; chuyển `ILogger`/Generic Host là hạng mục P1 riêng, chưa làm ở đây):
  - tick quá ngân sách;
  - slow consumer bị ngắt;
  - protocol mismatch.
- **Admin Management Contract (read model, chưa có API):** danh sách người chơi online theo map (`CharacterId`, map, vị trí, ConnectionId, thời điểm vào), số người theo map, thời gian tick p50/p95, số slow consumer bị ngắt. Không có command quản trị trong P1; sau này thêm "kick" qua Admin API dùng `ForceDisconnectAsync` có audit.

## 11. Kiểm thử

| Mức | Nội dung |
|---|---|
| Unit | `AoiGrid` (biên ô, đổi map), diff visible set (spawn/move/despawn), hàng đợi gửi (FIFO, đầy → slow consumer), protocol version, lịch sử dự đoán client (EditMode) |
| Tích hợp TCP | hai kết nối cùng map thấy nhau, một bên đổi map → despawn, thoát → despawn; client cũ (protocol 0) bị từ chối |
| Bot | 50/200/500 kết nối, đo tick time và kích thước snapshot |
| Play Mode | hai client Unity cùng map thấy nhau, di chuyển không giật |

## 12. Rollout và rollback

- Client và server phải nâng cùng lúc lên protocol 2. Client 1 bị từ chối kèm thông báo cập nhật.
- Rollback: revert commit theo từng bước. Không có migration database trong P1.
- Cấu hình mới đều có default an toàn và validation khi khởi động.
