# Nhật ký phiên — Ổn định P0 và mô phỏng thế giới P1

Ngày: 2026-11-14  
Người phụ trách: chủ dự án (MinKhang), thực hiện cùng Claude Code  
Phân loại: Gameplay do server quyết định (chính); Giao diện; Công cụ

## 1. Mục tiêu và phạm vi

- **Mục tiêu:**
  1. HUD dễ test.
  2. Sửa lỗi qua cổng.
  3. Hoàn tất gói ổn định P0 theo [audit 13-11](../Audits/2026-11-13-danh-gia-luong-va-huong-di.md).
  4. Xây nền P1 "thế giới nhiều người": tick, thấy người chơi khác, chống giật, load test.
- **Trong phạm vi:**
  - P0: B1–B5, B7, B8, B11, B15 (bản tối thiểu).
  - P1: protocol version (B10), hàng đợi gửi, world tick, AOI, dead reckoning, dự đoán phía client (B6), bot load test, giới hạn kết nối DB.
- **Ngoài phạm vi:** Generic Host + `ILogger`, tự resume sau mất kết nối (phần còn lại của B15), B9 (hash mật khẩu), B12–B14, PvP/party/chat, nhiều instance.

## 2. Kết quả hoàn thành

### HUD và luồng tân thủ

- `HudPresentation`: logic hiển thị thuần, dùng chung cho `InGameHUD` và `VillageUIToolkitHud`, có test EditMode.
- `HudDebugPanel` (F9, chỉ Editor/Development Build): xem trước mọi trạng thái nhiệm vụ, map, kết quả cổng và HP/MP/EXP mà không đổi dữ liệu server.
- Thông báo từ chối qua cổng hiện trên card UI Toolkit đang hiển thị. Trước đây thông báo này ghi vào card cũ đã bị ẩn.
- **Lỗi qua cổng:**
  - Server kiểm tra tầm bằng vị trí cũ, nay gọi `AdvancePosition` trước khi kiểm tra.
  - Trigger cổng phía client bị khoá vĩnh viễn sau lần bị từ chối đầu tiên, nay được kích hoạt lại.
  - Chủ dự án đã xác nhận qua cổng hoạt động.

### P0 — Ổn định nền

| Mục | Kết quả |
|---|---|
| B1 | `KnightServer.IntegrationTests` compile lại theo API hiện tại; sửa một kịch bản sai (spawn ngoài tầm NPC). CI chạy integration test với service PostgreSQL. |
| B2 | Vòng accept không còn thoát khi client gây lỗi; trả "server đầy" chạy nền, có hạn. |
| B4 | Kết nối chưa đăng nhập bị đóng sau `network.anonymousConnectionTimeoutSeconds` (120 s); client tự kết nối lại khi đăng nhập và không báo lỗi giả. |
| B11 | `maximumPacketBytes` 1 MB → 64 KB. |
| B3, B8 | Rate limiter dọn key hết hạn, trần 100.000 key; thêm limiter riêng theo tài khoản (`maximumLoginAttemptsPerAccount` = 10/60 s). |
| B5 | `AdvancePosition` trước kiểm tra tầm NPC/cổng và trước mọi lần lưu snapshot. |
| B7 | Client giữ nguyên `RequestId` cho cùng một ý định NPC/cổng chưa được trả lời. |
| B15 | Popup "MẤT KẾT NỐI" trong game, khoá di chuyển, quay về màn đăng nhập (bản tối thiểu). |

### P1 — Mô phỏng thế giới

- **Protocol version (B10):**
  - `PacketProtocol.Version`, hiện là **3**.
  - Server từ chối client ngoài khoảng cho phép bằng `VersionMismatch`.
  - Dispatcher bỏ qua mọi packet gửi trước khi handshake xong.
- **Hàng đợi gửi bounded** cho mỗi kết nối, một task ghi duy nhất. Broadcast không chặn; client không đọc kịp bị ngắt.
- **World tick 20 Hz:**
  - Tiến vị trí mọi người chơi.
  - Gửi `WorldEntitiesSnapshot` (spawn/move/despawn) theo lưới AOI 3×3 ô, giới hạn 100 người hiển thị có hysteresis.
  - Đo thời gian từng giai đoạn của tick.
- **Dead reckoning (protocol 3):**
  - Server chỉ gửi `Moved` khi đổi hướng/tốc độ, lệch quá 0,25 đơn vị, hoặc hiệu chỉnh mỗi 1 s.
  - Người bị chặn được báo là đứng yên.
  - Client ngoại suy rồi làm mượt hiển thị.
- **Client:**
  - `RemotePlayerDirectory` sống suốt vòng đời app; `RemotePlayersPresenter` vẽ người chơi khác.
  - `MovementPrediction` đối chiếu xác nhận của server với vị trí dự đoán lúc gửi input, nên hết bị kéo ngược (B6).
- **Biên map:** client áp đúng quy tắc của server (`WalkableBounds`: giới hạn tâm nhân vật) thay vì collider biên, nên hết rung khi đi sát tường.
- **`KnightBot`:** bot headless chạy toàn bộ luồng thật, đo thời gian vào game, RTT xác nhận di chuyển và băng thông.
- **Kết nối database:**
  - Trần pool Npgsql 40.
  - `SessionPersistenceGate` giới hạn 16 thao tác lưu/giải phóng phiên đồng thời và retry lỗi transient.
  - Hết lỗi 53300 khi nhiều người thoát cùng lúc.

## 3. File và module thay đổi

- **Server:**
  - `Networking/` (`ClientConnection`, `ConnectionRegistry`, `PacketDispatcher`, các handler Connect/Portal/NPC/Auth/EnterWorld/Attack).
  - `World/`: `WorldSimulation`, `AoiGrid`, `VisibilityTracker`, `WorldSnapshotJson` (mới).
  - `Players/PlayerSession`.
  - `Accounts/AuthenticationRateLimiter`.
  - `Persistence/`: `SessionPersistenceGate` (mới), `DatabaseConfiguration`.
  - `Configuration/ServerOptions`, `serverSettings.json`, `Program.cs`.
- **Shared/Packets:** `PacketProtocol` (mới), `ConnectRequestPacket`, `PacketType` (`WorldEntitiesSnapshot = 52`), `WorldEntityPackets` (mới).
- **Client:**
  - `UI/`: `HudPresentation`, `HudDebugPanel`, `InGameConnectionLostView` (mới), `InGameHUD`, `VillageUIToolkitHud`.
  - `Resources/UI/VillageHud.uxml|uss`.
  - `Network/`: `NetworkClient`, `PendingIntentRequests` và `WorldEntitiesSnapshotHandler` (mới).
  - `Data/Models/`: `RemotePlayerDirectory`, `DeadReckoningTrack`.
  - `Gameplay/Player/`: `PlayerController`, `MovementPrediction`, `WalkableBounds`.
  - `Gameplay/World/`: `AuthoritativeWorldPresentation`, `RemotePlayersPresenter`.
  - `Root/`: `AppLifetimeScope`, `InGameSceneRoot`.
- **Test:**
  - `KnightServer.Tests`: networking, world, dead reckoning, persistence gate, benchmark.
  - `KnightServer.IntegrationTests`: sửa theo API mới.
  - `KnightClient/Assets/_Project/Tests/EditMode`: assembly test mới.
- **Công cụ:** `KnightBot/` (mới), `.github/workflows/server-ci.yml`.
- **Tài liệu:** [World_Simulation_P1_Design.md](../Architecture/World_Simulation_P1_Design.md) (mới, có số đo mục 12).

## 4. Quyết định kỹ thuật

- **Tick 20 Hz, snapshot thực thể 10 Hz, AOI lưới 16 đơn vị.** Map hiện nhỏ nên thực tế cả map nhìn thấy nhau; lưới phục vụ map lớn sau này.
- **Dead reckoning thay cho nội suy:** nội suy gửi mọi vị trí 10 lần/giây, quá tốn băng thông (khoảng 130 KiB/s mỗi người). Dead reckoning giảm còn khoảng 45 KiB/s trong kịch bản thực tế.
- **Snapshot vị trí riêng theo tick cho chính người chơi được hoãn.** Snapshot sau mỗi input đã đủ cho dự đoán; chỉ cần khi có lực đẩy phía server (knockback).
- **Biên map:** server giới hạn *tâm* nhân vật; client sao chép đúng quy tắc. Không thu hẹp biên phía server để tránh đổi contract.
- **Thử lại ghi phiên** chỉ áp cho thao tác lặp lại an toàn: ghi snapshot có fencing generation/version, còn giải phóng nhân vật làm lại không sao. Không dùng execution strategy của EF vì xung đột với transaction do code tự mở.
- **Timeout kết nối ẩn danh 120 s** thay vì 10–15 s như audit đề xuất, vì client không gửi gì khi người chơi đang gõ tài khoản.
- **Tài liệu nguồn đã cập nhật:** `World_Simulation_P1_Design.md`.

## 5. Contract và tương thích

- **Breaking:** protocol 1 → 3.
  - `ConnectRequest` có `ProtocolVersion`.
  - Bắt buộc handshake trước mọi packet.
  - Packet mới `WorldEntitiesSnapshot = 52`, spawn/move có `Speed`.
  - `PortalUseResultEvent` (client) có thêm `RequestId`.
- Server nhận protocol 3..3 (`network.minimumProtocolVersion`/`maximumProtocolVersion`). Client và bot build từ cùng commit. Bản build cũ bị từ chối kèm thông báo cập nhật.
- **Rollout:** server và client phải lên cùng lúc. Không có cờ tính năng.

## 6. Database, migration và cấu hình

- **Migration:** không có.
- **Cấu hình mới (đều có default và validation):**
  - `network.anonymousConnectionTimeoutSeconds`, `minimumProtocolVersion`, `maximumProtocolVersion`; `maximumPacketBytes` = 65536.
  - `authentication.maximumLoginAttemptsPerAccount`.
  - `world.simulationTickMilliseconds`, `entitySnapshotEveryTicks`, `aoiCellSize`, `maximumVisiblePlayers`, `deadReckoningErrorThreshold`, `deadReckoningRefreshMilliseconds`.
  - `persistence.maximumPoolSize`, `maximumConcurrentSessionWrites`, `sessionWriteAttempts`.
- **Secret:** không thêm. Load test lấy chuỗi kết nối từ User Secrets, chỉ đổi tên database, truyền qua biến môi trường, không ghi ra log.
- **Database thử `knightonline_loadtest`** do chủ dự án tạo; còn dữ liệu bot (guest và nhân vật). Xoá bằng `DROP DATABASE knightonline_loadtest;` khi không cần.
- **Rollback:** revert commit theo bước. Không có thay đổi schema.

## 7. Bảo mật và khả năng quản trị

- **Kiểm tra đầu vào/phân quyền:** handshake bắt buộc; packet tối đa 64 KB; timeout kết nối ẩn danh; rate limiter có trần bộ nhớ và giới hạn theo tài khoản; client chậm bị ngắt thay vì làm phình bộ nhớ server.
- **Log:**
  - Mức Warning: tick quá ngân sách, slow consumer, retry transient.
  - Mức Info: protocol mismatch, đóng kết nối ẩn danh.
  - Lỗi cleanup persistence ghi đủ exception.
- **Admin:** read model đề xuất (người online theo map, thời gian tick, số slow consumer) đã ghi trong tài liệu thiết kế mục 10. Chưa có Admin API.
- **Phần còn hoãn:** Generic Host + `ILogger` có cấu trúc; metric/dashboard; B9 (hash mật khẩu).

## 8. Kiểm tra

| Kiểm tra | Kết quả | Ghi chú |
|---|---|---|
| Build server, bot | Đạt | Release |
| `KnightServer.Tests` | 134 đạt, 3 bỏ qua | 3 test cần `KNIGHTONLINE_TEST_POSTGRES_CONNECTION`; CI cấp sẵn |
| `KnightServer.IntegrationTests` | 18/18 đạt | Chạy trên DB dev qua User Secrets (test tự dọn tài khoản) |
| Compile client Unity | Đạt | Compile ngoài Editor bằng project Unity sinh ra, kể cả assembly test |
| Test EditMode Unity | **Chưa chạy** | Chạy trong Test Runner |
| Benchmark tick | Đạt ngân sách ở kịch bản thực tế | 500 người: tick có snapshot p95 39 ms, 45 KiB/s mỗi người |
| Bot load test 50/200/500 | Đạt | 100% vào game, 0 lỗi, RTT di chuyển p95 1,3/2,6/5,3 ms; 0 lỗi persistence sau bản sửa |
| Thủ công trong Unity (một người) | Đạt một phần | Chủ dự án xác nhận P0, qua cổng và hết rung sát tường |
| Hai client cùng map | **Chưa xác nhận đầy đủ** | Làm theo mục 11 |

## 9. Rủi ro và nợ kỹ thuật

- **Tick vượt 50 ms khi 500 người dồn một vùng** (kịch bản xấu nhất 74 ms; load test cùng máy: tick vượt từ khoảng 380 người).
  - Hướng xử lý: song song hoá phần việc theo từng người quan sát, chuyển sang định dạng nhị phân, đo với bot ở máy khác.
- **Băng thông** khoảng 45 KiB/s mỗi người khi đông vẫn cao cho Internet. Payload JSON đang bị escape lần hai trong envelope.
- **Có thể rung gần quái:** server dùng hình tròn (bán kính cộng 0,85), client dùng box collider. Chưa sửa.
- **B15** chưa tự resume.
- **CI** chưa compile Unity (cần license GameCI).
- `Packages/manifest.json` và `packages-lock.json` do Unity tự nâng `cloud-build` 2.0.8 → 2.1.1, chưa commit.
- **Người phụ trách:** chủ dự án quyết định thứ tự xử lý.

## 10. Bàn giao và bước tiếp theo

- **Trạng thái:** 10 commit code trên `main` (`d034cf7` → `744937d`) cộng commit DevLog này, chưa push.
- **Việc tiếp theo đề xuất:**
  1. Hoàn tất checklist hai client ở mục 11.
  2. Va chạm quái khớp với server.
  3. B15 tự resume.
  4. Generic Host + `ILogger`.
  5. Song song hoá tick.
  6. Đo load test với bot ở máy khác.
- **File nên đọc trước:** [World_Simulation_P1_Design.md](../Architecture/World_Simulation_P1_Design.md), [KnightBot/README.md](../../KnightBot/README.md).
- **Điểm chặn:** không có.
- **Commit:** `d034cf7`, `1dec330`, `eb064c5`, `f210638`, `8052970`, `69b7d9e`, `19cb93d`, `545fb30`, `bc2b5e4`, `744937d`.

## 11. Hướng dẫn test phiên này

### 11.1 Chuẩn bị

1. Mở `KnightClient` bằng Unity 6000.5.4f1, chờ compile xong và kiểm tra Console không có lỗi đỏ.
2. Chạy server bản mới với database dev:
   ```bash
   dotnet run --project KnightServer
   ```
   Server chỉ nhận client **protocol 3**. Bản build cũ phải bị từ chối kèm thông báo cập nhật.
3. Chạy từ scene `App`.

### 11.2 Test tự động

| Lệnh / thao tác | Mong đợi |
|---|---|
| `dotnet test KnightServer.Tests -c Release` | 134 đạt, 3 bỏ qua |
| `dotnet test KnightServer.IntegrationTests -c Release` | 18/18 đạt (cần DB qua User Secrets) |
| Unity: **Window → General → Test Runner → EditMode → Run All** | Tất cả đạt (HUD, RequestId, dự đoán, dead reckoning, biên map) |
| Benchmark (tuỳ chọn): đặt `KNIGHTONLINE_RUN_BENCHMARKS=1` rồi chạy `dotnet test KnightServer.Tests -c Release --filter Category=Benchmark --logger "console;verbosity=detailed"` | Dòng kết quả cho 50/200/500 người, 0 slow consumer |

### 11.3 Một người chơi (hồi quy)

| Kiểm tra | Mong đợi |
|---|---|
| Đăng nhập / Continue / Chơi mới | Vào được bình thường |
| Ngồi ở màn đăng nhập hơn 2 phút rồi đăng nhập | Tự kết nối lại, không báo lỗi |
| Di chuyển liên tục, đổi hướng nhanh | Không giật, không bị kéo ngược |
| Đi thẳng/chéo vào biên map, vào góc | Dừng hoặc trượt êm dọc mép, không rung |
| Đi sát quái Wolf | Ghi lại nếu rung (rủi ro đã biết) |
| Nói chuyện với Mẹ, nhận nhiệm vụ | Bình thường |
| Qua cổng Làng ↔ Bãi Sói; cổng Safe Zone 1 khi chưa đủ level | Qua được; khi bị từ chối có thông báo cam trên card |
| Đánh Wolf | Tiến độ tăng đúng |
| F9 (HUD Debug) | Xem trước được mọi trạng thái; "Khôi phục dữ liệu server" trả về dữ liệu thật |
| Tắt server khi đang trong game | Popup "MẤT KẾT NỐI", nút đưa về màn đăng nhập |

### 11.4 Hai người chơi

Chạy Unity Editor cộng một bản build (**File → Build And Run**), mỗi bên một tài khoản.

| Kiểm tra | Mong đợi |
|---|---|
| Cả hai vào cùng map | Thấy nhau (chấm xanh, tên, level) |
| A di chuyển thẳng | B thấy A đi mượt, đúng hướng |
| A đổi hướng hoặc dừng đột ngột | B thấy A đổi theo, trễ nhẹ, không giật mạnh |
| A đi vào tường | B không thấy A xuyên tường |
| A qua cổng sang map khác | A biến mất khỏi màn hình B |
| A thoát game rồi đăng nhập lại | A biến mất rồi xuất hiện lại trên màn hình B |

### 11.5 Load test (tuỳ chọn, cần database riêng)

1. Tạo database thử (một lần):
   ```bash
   "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h 127.0.0.1 -p 5432 -c "CREATE DATABASE knightonline_loadtest OWNER knightonline_app;"
   ```
2. Chạy server với `KNIGHTONLINE_ConnectionStrings__KnightOnline` trỏ vào `knightonline_loadtest`, **không** dùng DB dev.
3. Chạy bot theo [KnightBot/README.md](../../KnightBot/README.md):
   ```bash
   dotnet run --project KnightBot -c Release -- --bots 200 --ramp 25 --duration 60 --allow-db-writes
   ```
4. **Mong đợi:**
   - mọi bot vào được thế giới;
   - "Failures: none";
   - log server không có `[Persistence][Error]`.

Khi báo lỗi, gửi kèm bước tái hiện, ảnh chụp, log Console Unity và log server.
