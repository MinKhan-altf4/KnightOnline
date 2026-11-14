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
  3. *(Hoãn)* Snapshot vị trí riêng theo tick cho chính người chơi. Client đã nhận snapshot sau mỗi input (tối thiểu 10 Hz khi di chuyển) kèm `AcknowledgedClientSequence`, đủ cho dự đoán ở mục 7. Chỉ thêm khi có lực đẩy phía server (knockback, bị đẩy khỏi quái) cần đồng bộ lúc không có input.
- Người chơi chỉ được tick mô phỏng và hiển thị cho người khác sau khi `EnterWorld` thành công (`PlayerSession.IsInWorld`). Nhân vật đã chọn nhưng đang tải scene thì vô hình.
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
| `WorldEntitiesSnapshot = 52` | S→C | `ServerTick`, `ServerTimeUtc`, `Spawned[]` (`CharacterId`, `Name`, `Level`, `ClassDefinitionId`, `X`, `Y`, `DirectionX`, `DirectionY`, `Speed`), `Moved[]` (`CharacterId`, `X`, `Y`, `DirectionX`, `DirectionY`, `Speed`), `Despawned[]` (`CharacterId`) |

- Chỉ gửi khi có ít nhất một thay đổi. Từ protocol 3, `Moved` chỉ gửi theo quy tắc dead reckoning (mục 12.4).
- Spawn được gửi khi B vào AOI của A, khi A đổi map hoặc vừa vào thế giới. Despawn khi B rời AOI, đổi map hoặc thoát game.
- Không gửi dữ liệu nhạy cảm (account, HP chính xác của người khác). Tên hiển thị là dữ liệu công khai trong game.

### 6.3 Thứ tự và mất gói

TCP giữ thứ tự. Client bỏ snapshot có `ServerTick` ≤ tick đã áp dụng. Sau reconnect, server gửi lại spawn đầy đủ vì tập visible bắt đầu rỗng.

## 7. Client

- **Remote player:** `RemotePlayersPresenter` tạo đối tượng hiển thị (sprite + tên) cho mỗi `Spawned`. Protocol 2 dùng nội suy trễ 100 ms; protocol 3 thay bằng ngoại suy (dead reckoning) cộng làm mượt (mục 12.4).
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

## 12. Số đo (14-11-2026)

### 12.1 Benchmark tick trong tiến trình

Lệnh: `KNIGHTONLINE_RUN_BENCHMARKS=1 dotnet test KnightServer.Tests -c Release --filter Category=Benchmark`.

Kịch bản xấu nhất: N người trên **một** map 20×12 (cả map nằm trong một vùng AOI), mỗi người đổi hướng ngẫu nhiên mỗi 100 ms, socket loopback thật, client đọc hết dữ liệu, 200 tick. Server và client chạy cùng một máy dev nên thời gian tick bị ảnh hưởng bởi tải I/O loopback.

| N | Tick p50 | Tick có snapshot p95 | Gửi / người | Slow consumer |
|---|---|---|---|---|
| 50 | 0,03 ms | 3,1 ms | 64 KiB/s | 0 |
| 200 | 0,09 ms | 30 ms | 132 KiB/s | 0 |
| 500 | 2,8 ms | **168 ms** | 136 KiB/s | 0 |

Trước khi ghép fragment JSON (`WorldSnapshotJson`), tick có snapshot p95 ở N=500 là 251 ms.

### 12.2 Kết luận và việc tiếp theo

- Đến khoảng 200 người cùng một vùng AOI, tick nằm trong ngân sách 50 ms. Với 500 người dồn một chỗ thì vượt.
- Nút thắt thật là **băng thông**: gửi tối đa 100 thực thể × 10 Hz, tức khoảng 130 KiB/s mỗi người. Không khả thi qua Internet. Đề xuất theo thứ tự lợi ích:
  1. **Dead reckoning:** chỉ gửi `Moved` khi hướng đổi hoặc sai lệch so với ngoại suy vượt ngưỡng, kèm một lần hiệu chỉnh mỗi giây. Client ngoại suy bằng hướng × tốc độ. Đây là thay đổi giao thức (protocol 3).
  2. Bỏ JSON lồng trong envelope (payload đang bị escape lần hai), chuyển sang định dạng nhị phân. Thay đổi giao thức lớn, cần ADR.
  3. Giảm `world.maximumVisiblePlayers` hoặc tần suất snapshot theo khoảng cách (gần 10 Hz, xa 2–5 Hz).
### 12.3 Bot load test qua luồng thật (14-11-2026, 21:18–21:22)

**Cấu hình:**
- Server Release (commit `69b7d9e`), database riêng `knightonline_loadtest`.
- Bot và server chạy **cùng một máy dev**, trên cùng map tutorial, mỗi bot đổi hướng ngẫu nhiên và gửi input 10 Hz.
- Mỗi bậc chạy 60 giây sau khi khởi động xong bot cuối.

| Bot | Vào thế giới | Thời gian vào game p95 | Move ack RTT p50 / p95 / max | Nhận tổng |
|---|---|---|---|---|
| 50 | 50/50 | 579 ms (khởi động lạnh) | 0,6 / 1,2 / 67 ms | 2,7 MiB/s |
| 200 | 200/200 | 25 ms | 0,7 / 2,4 / 29 ms | 21 MiB/s |
| 500 | 500/500 | 20 ms | 0,9 / 3,8 / 31 ms | 39 MiB/s |

- Không có bot nào lỗi. Các request ghi database (tạo guest, tạo nhân vật, chọn nhân vật) có p95 ≤ 13 ms ở mọi bậc.
- **Tick quá ngân sách:** 480 lần, bắt đầu khi có khoảng 290 người trong thế giới. Trong các tick vượt: p50 111 ms, p95 129 ms, tối đa 262 ms. Khớp với benchmark ở mục 12.1; bot dùng chung CPU nên con số bi quan hơn server thật.
- **Lỗi persistence khi ngắt kết nối hàng loạt:**
  - 3 lần "Session snapshot cleanup failed: transient failure" và 3 lần "snapshot rejected: generation no longer owns" trên 750 lần ngắt.
  - Xảy ra khi hàng trăm kết nối đóng cùng lúc ở cuối mỗi bậc.
  - Nghi do cạn pool kết nối PostgreSQL (Npgsql mặc định 100) khi vòng lưu snapshot 5 giây chạy cùng lúc với cleanup.
  - Log đã được sửa để in đủ exception cho lần đo sau.
  - **Rủi ro:** người chơi có thể mất vị trí lưu cuối cùng (tối đa một chu kỳ snapshot).

**Việc cần làm theo thứ tự:**
1. ~~Dead reckoning (mục 12.2)~~ đã làm, xem 12.4.
2. ~~Giới hạn số thao tác DB song song khi ngắt kết nối/lưu snapshot, retry cho lỗi transient~~ đã làm, xem 12.5. Gom lưu snapshot thành batch vẫn còn để ngỏ.
3. Đo lại với bot chạy trên máy khác server.

### 12.4 Dead reckoning (protocol 3)

**Hợp đồng:**
- `RemotePlayerSpawnPacket` và `RemotePlayerMovePacket` có thêm `Speed`.
- Client ngoại suy `vị trí + hướng × tốc độ × (thời gian từ lúc nhận)`, tối đa 2 giây, và làm mượt hiển thị về vị trí ngoại suy (exponential smoothing, snap nếu lệch ≥ 3 đơn vị).
- Server chỉ gửi `Moved` khi:
  1. hướng hoặc tốc độ đổi;
  2. vị trí thật lệch khỏi ngoại suy quá `world.deadReckoningErrorThreshold` (0,25);
  3. người đang di chuyển và đã quá `world.deadReckoningRefreshMilliseconds` (1000) kể từ lần gửi trước.
- Người đứng yên không phát sinh packet.
- Hướng gửi đi là **hướng hiệu dụng**: nếu tick vừa rồi người chơi gần như không đi được (bị tường/quái chặn), server báo hướng bằng 0 để client không ngoại suy xuyên tường.

**Tối ưu tick đi kèm:**
- Chia người chơi vào lưới ô AOI một lần mỗi tick; mỗi người chỉ duyệt 9 ô lân cận.
- Giới hạn số người hiển thị bằng cách sắp xếp chỉ số theo khoá float, có hysteresis 2 đơn vị cho người đang hiển thị để giảm spawn/despawn ở biên.
- `WorldTickResult.Phases` đo thời gian từng giai đoạn.

**Benchmark sau thay đổi:**

| Kịch bản | Tick có snapshot p95 | Gửi / người |
|---|---|---|
| 500 người, đổi hướng mỗi input (xấu nhất) | 74 ms (trước: 168) | 150 KiB/s |
| 500 người, đổi hướng khoảng mỗi 2 s, 30% đứng yên | **39 ms** | **45 KiB/s** |
| 200 người, xấu nhất | 25 ms | 149 KiB/s |

**Bot load test sau thay đổi** (21:31–21:35, cùng cấu hình như 12.3; bot đổi hướng trung bình mỗi 0,5 s nên tiết kiệm ít hơn kịch bản thực tế):

| Bot | Vào thế giới | Move ack RTT p95 | Nhận tổng (protocol 2 → 3) | Tick quá ngân sách |
|---|---|---|---|---|
| 50 | 50/50 | 1,3 ms | 2,7 → 1,3 MiB/s | 0 |
| 200 | 200/200 | 3,3 ms | 21 → 9,0 MiB/s | 0 |
| 500 | 500/500 | 6,0 ms | 39 → 23,6 MiB/s | 294 (trước: 480), bắt đầu từ 381 người (trước: ~290), p50 trong các tick vượt 53 ms (trước: 111) |

**Nguyên nhân lỗi persistence đã xác định:** `53300: remaining connection slots are reserved for roles with the SUPERUSER attribute`. PostgreSQL hết `max_connections` khi hàng trăm kết nối game đóng cùng lúc, vì mỗi lần cleanup mở một kết nối DB riêng đồng thời. Lần này 4/750 lần cleanup thất bại. Cách sửa: giới hạn số thao tác DB đồng thời khi ngắt kết nối (semaphore) và retry có backoff cho lỗi transient.

Ở N=500 thực tế, chi phí còn lại mỗi tick snapshot: chọn hiển thị 17 ms, đóng gói và đưa vào hàng đợi 9 ms, dựng JSON 4 ms, diff 5 ms. Bước tiếp theo nếu cần: chạy phần việc theo từng người quan sát song song trên nhiều nhân (tracker độc lập; cache fragment JSON cần thread-safe), và chuyển sang định dạng nhị phân.

### 12.5 Giới hạn kết nối database (14-11-2026)

- Nguyên nhân: SQLSTATE 53300. Pool Npgsql mặc định cho phép 100 kết nối, nhiều hơn số slot PostgreSQL dành cho role thường (`max_connections` 100 trừ slot superuser).
- `persistence.maximumPoolSize` (mặc định 40) được áp vào chuỗi kết nối nếu chuỗi chưa ghi rõ `Maximum Pool Size`. Khi dồn tải, Npgsql xếp hàng chờ thay vì PostgreSQL từ chối.
- `SessionPersistenceGate` bao các thao tác lưu snapshot/giải phóng nhân vật khi ngắt kết nối và vòng lưu định kỳ:
  - tối đa `persistence.maximumConcurrentSessionWrites` (16) thao tác đồng thời;
  - retry `persistence.sessionWriteAttempts` (4) lần có backoff cho lỗi transient (53300, 53400, 57P03, 40001, 40P01, Npgsql transient, timeout).
  - Chỉ bao các thao tác lặp lại an toàn.
- Vòng lưu định kỳ bỏ qua kết nối đã đóng; cleanup của chính kết nối đó lưu vị trí cuối.
- **Đo lại (21:51–21:55):** 50/200/500 bot vào thế giới đủ, **0 lỗi persistence, 0 lần phải retry, 0 cảnh báo "snapshot rejected"** (trước: 4/750 lỗi). Tick quá ngân sách giảm còn 215 lần, chỉ ở bậc 500 bot dùng chung CPU với server.

## 13. Rollout và rollback

- Client và server phải nâng cùng lúc lên protocol 2. Client 1 bị từ chối kèm thông báo cập nhật.
- Rollback: revert commit theo từng bước. Không có migration database trong P1.
- Cấu hình mới đều có default an toàn và validation khi khởi động.
