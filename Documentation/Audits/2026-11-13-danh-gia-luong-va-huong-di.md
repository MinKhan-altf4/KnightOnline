# Đánh giá luồng và đề xuất hướng đi — 13-11-2026

> Ảnh chụp tại commit `c860744` (nhánh `main`) cộng các sửa lỗi nhỏ trong cùng
> phiên (mục 7). Không tự cập nhật theo code; đối chiếu lại trước khi dùng để ra
> quyết định.

## 1. Tóm tắt

KnightOnline có **nền móng server-authoritative tốt hơn mức thường thấy ở một
dự án giai đoạn này**: lease tài khoản theo generation, refresh token xoay vòng
có phát hiện dùng lại, idempotency theo `RequestId` cho tạo nhân vật/xác thực/
nhiệm vụ/EXP, transaction + outbox cho phần thưởng, fencing khi ghi snapshot,
chặn cờ Development ngoài môi trường Development. Boundary client cũng sạch: UI,
Gameplay và Data không dùng trực tiếp packet.

Điểm yếu tập trung ở ba chỗ:

1. **Vòng lặp mô phỏng thế giới chưa tồn tại.** Server chỉ tính vị trí khi nhận
   input; không có tick cố định, không đồng bộ người chơi khác. Đây là lý do
   sâu xa của hiện tượng giật/kéo ngược và là rào cản lớn nhất để thành MMO.
2. **Lớp transport/vận hành còn mỏng**: broadcast gửi tuần tự, không có hàng đợi
   gửi, không timeout kết nối ẩn danh, log bằng `Console.WriteLine`, `Program.cs`
   làm mọi việc.
3. **Lưới an toàn kiểm thử bị thủng**: CI chạy sai SDK (không thể pass), project
   integration test không biên dịch được từ lâu mà không ai biết.

| Mảng | Đánh giá | Ghi chú ngắn |
|---|---|---|
| Xác thực, lease, token | Tốt | Thiết kế đúng hướng; cần siết rate limit, nâng hash mật khẩu |
| Chọn/tạo nhân vật | Tốt | Idempotent, unique constraint, fencing generation |
| Nhiệm vụ, EXP, phần thưởng | Tốt | Transaction + idempotency; kiểm tra tầm dùng vị trí cũ |
| Di chuyển | Cần cải thiện | Không có dự đoán/hòa giải theo sequence → giật |
| Thế giới nhiều người | Chưa có | Không thấy người chơi khác; không có tick/AOI |
| Transport & vận hành | Cần cải thiện | Broadcast tuần tự, rủi ro DoS, chưa graceful shutdown |
| Kiến trúc client | Khá | Boundary tốt; còn code chết, HUD trùng, handler 815 dòng |
| CI / kiểm thử | Yếu | CI hỏng, integration test mục |
| Tài liệu | Khá → Tốt | Nhiều, có kỷ luật; trạng thái bị trễ so với code (đã chỉnh) |

## 2. Phạm vi và bằng chứng

| Kiểm tra | Kết quả (13-11-2026, máy local) |
|---|---|
| `dotnet build KnightServer.Tests -c Release` | Thành công, 0 cảnh báo, 0 lỗi |
| `dotnet test KnightServer.Tests` | 83 đạt, 3 bỏ qua (cần `KNIGHTONLINE_TEST_POSTGRES_CONNECTION`), 0 lỗi |
| `dotnet build KnightServer.IntegrationTests` | **Thất bại.** Trước khi sửa: xung đột EF Core 10.0.4/10.0.12 (CS1705). Sau khi sửa: 13 lỗi do test dùng API cũ |
| `dotnet tool restore` | Thành công với `dotnet-ef` 10.0.12 |
| Unity compile / Play Mode | **Chưa chạy** trong phiên này |
| Load test / bot đầu-cuối | Chưa có |

Phương pháp: đọc toàn bộ đường đi của packet từ `NetworkClient` → `PacketDispatcher`
→ handler → service → repository, các vòng nền trong `Program.cs`, tham chiếu
`.asmdef`, Build Settings, cấu hình và tài liệu.

## 3. Sai sót phát hiện

### 3.1 Đã sửa trong phiên

| # | Mức | Vấn đề | Sửa |
|---|---|---|---|
| F1 | Cao | CI cài .NET `8.0.x` trong khi mọi project target `net10.0` → workflow không thể build | `.github/workflows/server-ci.yml` dùng `10.0.x` |
| F2 | Cao | `KnightServer` lấy EF Core 10.0.12 qua gói `Design` (`PrivateAssets=all`) nên không truyền tiếp; project tham chiếu nhận EF 10.0.4 từ Npgsql → CS1705 ở integration test | Thêm `Microsoft.EntityFrameworkCore.Relational` 10.0.12 vào `KnightServer.csproj` |
| F3 | Trung bình | Hai thông báo gửi người chơi bị lỗi mã hóa (mojibake), ví dụ `"TÃ i khoáº£n Ä‘ang…"` | Khôi phục "Tài khoản đang được đăng nhập ở nơi khác." và "Máy chủ hiện đã đầy. Vui lòng thử lại sau." trong `AuthenticationPacketHandlers.cs` |
| F4 | Thấp | `dotnet-ef` ghim 8.0.11, lệch major với EF Core 10 | `dotnet-tools.json` → 10.0.12 |
| F5 | Thấp | 4 file `KnightServer/server*.log` vẫn bị Git theo dõi dù đã có trong `.gitignore` | `git rm --cached` (file local giữ nguyên) |
| F6 | Thấp | Tài liệu mâu thuẫn: kế hoạch cutover yêu cầu "test Character Select timeout" trong khi quyết định đã bỏ timeout | Sửa trong `Account_And_Character_Flow_Cutover.md` và `Character_Flow_Architecture_Plan.md` |

### 3.2 Chưa sửa — cần quyết định hoặc làm riêng

| # | Mức | Vấn đề | Vị trí |
|---|---|---|---|
| B1 | Cao | **Integration test mục**: gọi constructor `StarterTutorialService` 5 tham số, `PersistPortalTransitionAsync` (đã xóa), `AccountAuthenticationService` thiếu `clock`, `LoginAsync/ResumeAsync` thiếu `CancellationToken`. CI không build project này nên lỗi tồn tại lâu mà không ai thấy | `KnightServer.IntegrationTests/**` |
| B2 | Cao | **Một client gây lỗi có thể làm sập vòng accept.** Khi server đầy, `await connection.SendAsync(...)` nằm thẳng trong vòng `while(true)` của `Main`, không có `try/catch`; client reset kết nối ngay → exception thoát `Main` → server dừng | `Program.cs` vòng accept |
| B3 | Cao | **Bộ giới hạn tần suất tăng bộ nhớ vô hạn.** `AuthenticationRateLimiter` không bao giờ xóa key; key được băm từ `DeviceId`/refresh token do client tự gửi → kẻ tấn công gửi DeviceId ngẫu nhiên để phình bộ nhớ | `Accounts/AuthenticationRateLimiter.cs` |
| B4 | Cao | **Không có timeout cho kết nối chưa xác thực.** Lease chỉ áp dụng sau đăng nhập; 750 kết nối TCP rảnh (không gửi gì) chiếm hết sức chứa transport | `ClientConnection.RunAsync` |
| B5 | Trung bình | **Kiểm tra tầm dùng vị trí cũ.** `InteractNpc`, `UsePortal` và các vòng lưu snapshot đọc `session.Position` mà không gọi `AdvancePosition` (chỉ Combat và EnterWorld gọi). Người chơi giữ phím đi tới NPC có thể bị báo "ngoài tầm" hoặc ngược lại; vị trí lưu khi ngắt kết nối là vị trí lúc nhận input cuối | `TutorialWorldPacketHandlers.cs:63,194`, `Program.cs` |
| B6 | Trung bình | **Hòa giải vị trí phía client gây giật.** Mỗi 0,1 giây client gửi input và nhận lại vị trí server *tại thời điểm nhận input*; client kéo mình về vị trí đó dù đã đi tiếp một RTT. Với tốc độ 4 đơn vị/giây, RTT 100 ms đã lệch 0,4 > ngưỡng 0,15. `AcknowledgedClientSequence` có trong packet nhưng client không dùng để phát lại input | `PlayerController.ReconcileAuthoritativePosition` |
| B7 | Trung bình | **Client tạo `RequestId` mới cho mỗi lần gửi** `InteractNpc`/`UsePortal` → nếu gửi lại sau timeout, server coi là yêu cầu mới; idempotency phía server mất tác dụng với retry | `NetworkClient.SendInteractNpcRequestAsync`, `SendUsePortalRequestAsync` |
| B8 | Trung bình | **Rate limit quá lỏng**: `maximumAttemptsPerWindow = 500`/60 giây áp dụng cả cho từng username → dò được 500 mật khẩu/phút/tài khoản | `serverSettings.json` |
| B9 | Trung bình | **Băm mật khẩu yếu hơn khuyến nghị hiện hành**: PBKDF2-SHA256 210.000 vòng (OWASP hiện khuyến nghị ≥ 600.000 hoặc Argon2id) | `Accounts/PasswordHasher.cs` |
| B10 | Trung bình | **Không kiểm tra phiên bản protocol.** Client gửi `ConnectRequest("1.0.0")` cố định, server bỏ qua → client cũ/mới lệch contract sẽ lỗi khó hiểu (vi phạm CORE-17) | `ConnectPacketHandler.cs`, `NetworkClient.ConnectAsync` |
| B11 | Thấp | Gói tối đa 1 MB (`maximumPacketBytes`) — quá lớn cho game; 750 kết nối × 1 MB cấp phát là đường tấn công bộ nhớ | `serverSettings.json` |
| B12 | Thấp | Thông báo server lẫn tiếng Anh/tiếng Việt ("Invalid username or password." cạnh chuỗi tiếng Việt). Client nên hiển thị theo mã kết quả, không theo chuỗi server | `AuthenticationPacketHandlers.cs` |
| B13 | Thấp | Tên khu vực hiển thị bị hardcode và lặp ở hai HUD (`"wolf_field_01" => "BÃI SÓI"` trong cả `InGameHUD` và `VillageUIToolkitHud`) — trái CORE-03/CORE-14 | `UI/InGameHUD.cs:147`, `UI/VillageUIToolkitHud.cs:110` |
| B15 | Trung bình | **Mất kết nối khi đang trong game có thể không có phản hồi.** Trong InGame, `InGameHUD.OnDisconnected` chỉ ghi log; `AuthenticationFlowService` phát yêu cầu hiện màn Entry/popup nhưng panel đó nằm trong scene `Bootstrap`, đã bị unload khi nạp `InGame` kiểu `Single`. Theo code, người chơi có thể bị kẹt trong thế giới với kết nối đã chết — cần xác nhận bằng Play Mode (tắt server khi đang trong game) | `UI/InGameHUD.cs:177`, `AuthenticationFlowService.cs:511`, `CharacterFlowController.cs:182` |
| B14 | Thấp | Code chết và rác template: `GameManager`, `ServiceLocator`, `Singleton`, `SceneLoader` không được tham chiếu; `SampleScene`, `TutorialInfo/`, `Readme.asset`; 9 thư mục rỗng; hai script camera follow; `*.anim.anim`; sprite tên UUID | Xem `Architecture/Repository_Structure.md` §9 |

## 4. Review từng luồng

### 4.1 Khởi động và kết nối

```text
Unity: App → AppLifetimeScope → Bootstrap → NetworkClient.ConnectAsync → ConnectRequest("1.0.0")
Server: TcpListener.Accept → ConnectionRegistry.TryAdd (≤750) → ClientConnection.RunAsync
```

**Tốt:** composition root rõ (VContainer), `SingleInstanceGuard`, migration tự
áp dụng, server dọn fence nhân vật còn sót khi khởi động, cấu hình được kiểm tra
tập trung ở `ServerOptions`.

**Vấn đề:** B2, B4, B10, B11. `Main` chạy `while(true)` không có cơ chế dừng:
Ctrl+C giết tiến trình mà không lưu snapshot cuối. Mọi log là `Console.WriteLine`
không có mức độ, correlation ID hay cấu trúc (CORE-18/19).

**Đề xuất:** chuyển server sang **.NET Generic Host** (`Host.CreateApplicationBuilder`):
`IHostedService` cho listener và từng vòng nền, `ILogger<T>` có cấu trúc, DI thay
cho chuỗi `new` 665 dòng, `IHostApplicationLifetime` để tắt êm (ngừng accept →
lưu snapshot → đóng kết nối). Đây là thay đổi cơ học, ít rủi ro, mở đường cho
health check và metric.

### 4.2 Xác thực và lease tài khoản

```text
CreateGuest / Login / Resume (RequestId) → rate limit IP + định danh
  → AccountAuthenticationService (PostgreSQL, receipt idempotency, xoay token)
  → TryClaim lease: Acquired | AlreadyOwned | ActiveElsewhere | CoolingDown | CapacityReached
  → gắn AccountKey + generation vào connection → heartbeat 5s, TTL 20s, ân hạn 10s
```

**Tốt:** người đang chơi không bị thiết bị đến sau đá ra; generation + ConnectionId
bảo vệ release/renew; mọi packet đã xác thực đều gia hạn lease (tránh mất lease
khi khung hình bị giật); receipt mã hóa cho retry; kho token PlayerPrefs bị chặn
cứng ở bản release.

**Vấn đề:** B3, B8, B9. Lease nằm trong bộ nhớ — chấp nhận được với hợp đồng một
instance hiện tại. Mỗi lần đăng nhập thất bại vì `ActiveElsewhere` vẫn đã xoay
và phát refresh token mới — đúng về bảo mật nhưng tạo nhiều họ token rác.

**Đề xuất:**
- Thay `ConcurrentDictionary` bằng bộ đếm có hết hạn (dọn định kỳ theo cửa sổ,
  hoặc `System.Threading.RateLimiting.PartitionedRateLimiter` có giới hạn số
  partition). Đặt trần riêng: đăng nhập theo tài khoản 5–10/phút, theo IP cao hơn.
- Nâng băm mật khẩu và **rehash khi đăng nhập thành công** (định dạng hash đã lưu
  số vòng nên chuyển đổi dần được).
- Kiểm tra lease *trước* khi xoay token với Login (thông tin đăng nhập đã đúng)
  để không phát token khi biết chắc sẽ từ chối.

### 4.3 Chọn/tạo nhân vật và vào thế giới

```text
GetCatalog → CheckName (chỉ UX) → CreateCharacter(RequestId) [transaction, unique slot/name]
  → SelectCharacter: kiểm tra sở hữu → IsOwner lease → ActivePlayerRegistry.TryClaim
     → BeginSession (ghi generation vào hàng nhân vật) → IsOwner lần 2 → PlayerSession
  → EnterWorld(gameplaySessionId) → snapshot vị trí, nhiệm vụ, túi đồ
```

**Tốt:** đây là luồng chắc nhất. Catalog từ server, unique constraint chống race,
idempotency theo `RequestId`, kiểm tra lease hai lần quanh bước ghi fence, gỡ
claim đúng thứ tự khi thất bại.

**Đề xuất nhỏ:** `SelectCharacter` lặp lại cho cùng nhân vật trả thành công — tốt;
nên ghi rõ ngữ nghĩa này trong `Session_Lease_Contract.md`. Bỏ tham chiếu
`Shared` khỏi assembly `Data`/`UI` để khóa boundary.

### 4.4 Di chuyển

```text
Client (mỗi khi đổi hướng hoặc mỗi 0,1s): PlayerMoveInput(direction, sequence)
Server: TrySetMovement → AdvancePosition (va chạm quái, biên map) → đặt hướng mới
        → trả PlayerPositionSnapshot(serverSeq, ackClientSeq, vị trí)
Client: nếu lệch > 0,15 → kéo dần; > 1,0 → dịch chuyển tức thời
```

**Tốt:** client chỉ gửi *hướng*, server tự tích phân theo thời gian và tốc độ
của chính nó → miễn nhiễm hack tốc độ/dịch chuyển; giới hạn delta thời gian chống
dồn input.

**Vấn đề:** B5, B6. Server không có tick: vị trí chỉ "tiến" khi có packet, nên mọi
nơi đọc vị trí phải nhớ gọi `AdvancePosition` (và đã có chỗ quên). Client dùng
Rigidbody2D Dynamic tự va chạm với quái, trong khi server dùng resolver riêng →
hai mô phỏng khác nhau, khác biệt bị "kéo" về liên tục.

**Đề xuất (theo thứ tự):**
1. Ngắn hạn: client lưu bộ đệm input theo `sequence`; khi nhận snapshot, đặt lại
   về vị trí server rồi **phát lại các input chưa được xác nhận**
   (`sequence > AcknowledgedClientSequence`). Chỉ hòa giải trên phần sai lệch còn
   lại. Server chỉ gửi snapshot khi từ chối hoặc định kỳ (ví dụ 5 Hz), không phải
   mỗi input.
2. Đưa logic va chạm/biên map về một nguồn (thư viện C# thuần dùng chung như
   `Shared/Packets`) để client dự đoán giống server.
3. Trung hạn: xem 4.8 (tick thế giới).

### 4.5 Chiến đấu và EXP

```text
AttackMonster(monsterId) → MonsterCombatService: AdvancePosition, cùng map, tầm, cooldown, còn sống
  → AttackResult cho người đánh → broadcast HP quái theo map
  → nếu chết: broadcast MonsterDied → RecordKill (tiến độ nhiệm vụ) → GrantExperience(monsterLifeId)
```

**Tốt:** sát thương do server tính; EXP idempotent theo `MonsterLifeId` (một mạng
quái chỉ cho EXP một lần); khách bị giới hạn cấp.

**Vấn đề:** toàn bộ chuỗi (broadcast tới mọi người trong map, 2 transaction DB)
chạy *trong* vòng đọc packet của người đánh — một client chậm trong map làm chậm
người đánh (xem 4.7). Chỉ người ra đòn cuối nhận EXP/kill credit; với nhóm sẽ cần
bảng đóng góp sát thương. `AttackMonsterRequest` không có `RequestId` — chấp nhận
được vì cooldown + idempotency theo mạng quái.

**Đề xuất:** tách "kết quả gameplay" (đồng bộ, trong bộ nhớ) khỏi "ghi nhận bền
vững" (EXP, nhiệm vụ) bằng hàng đợi nội bộ theo nhân vật hoặc outbox; người đánh
nhận `AttackResult` ngay, EXP đến sau vài mili giây.

### 4.6 Nhiệm vụ tân thủ, NPC và cổng dịch chuyển

```text
InteractNpc(RequestId, npcId) → kiểm tra map + tầm → StarterTutorialService (khóa hàng nhân vật,
  transaction ReadCommitted, TutorialCommands theo RequestId) → thưởng + outbox + audit
UsePortal(RequestId, portalId) → điều kiện nhiệm vụ + tầm → Teleport → lưu snapshot ngay
```

**Tốt:** máy trạng thái thuần (`StarterTutorialStateMachine`) có unit test; thưởng
nằm cùng transaction với tiến độ, command log và outbox — đúng CORE-07/08.

**Vấn đề:** B5, B7. Logic nhiệm vụ đang gắn cứng "một nhiệm vụ tân thủ"
(`StarterTutorialService` được tiêm cả vào handler tấn công và cổng).

**Đề xuất:** khi thêm nhiệm vụ thứ hai, tổng quát thành `QuestService` + định nghĩa
nhiệm vụ dạng dữ liệu (mục tiêu: nói chuyện/giết/thu thập; phần thưởng; điều kiện).
Combat chỉ phát domain event `MonsterKilled`; Quest/Progression đăng ký xử lý —
bỏ phụ thuộc trực tiếp Combat → Tutorial.

### 4.7 Ngắt kết nối, lưu snapshot và broadcast

```text
TCP đóng → SaveSessionSnapshot(generation) → ReleaseSession → BeginDisconnectGrace (10s)
         → ActivePlayerRegistry.Release → ConnectionRegistry.Remove
Vòng nền 5s: với mỗi người chơi còn sở hữu lease → SaveSessionSnapshot (tuần tự)
```

**Tốt:** snapshot có fencing generation + version, không đè phiên mới; lỗi lưu
được log rồi tiếp tục dọn.

**Vấn đề:**
- `BroadcastAsync`/`BroadcastToMapAsync` `await` lần lượt từng kết nối → một
  client không đọc làm nghẽn vòng hồi sinh quái và người đang đánh.
- Vòng snapshot 5s chạy tuần tự, mỗi người một round-trip DB: 500 người ≈ 500
  truy vấn nối đuôi mỗi 5 giây.
- Nếu `BeginDisconnectGraceAsync` ném lỗi, `Remove` không chạy → rò sức chứa
  (hiện bản in-memory không ném, nhưng adapter Redis sau này sẽ có thể).

**Đề xuất:** mỗi `ClientConnection` có `Channel<T>` giới hạn (bounded) + một task
ghi riêng; broadcast chỉ `TryWrite`, đầy thì ngắt kết nối client chậm. Gom snapshot
thành một lệnh batch (`ExecuteUpdate`/`COPY`) mỗi chu kỳ. Bọc từng bước dọn dẹp
trong `finally` riêng.

### 4.8 Thế giới nhiều người chơi (thiếu hẳn)

Hiện không có packet nào thông báo người chơi khác (không có
`PlayerSpawned/PlayerMoved/PlayerDespawned`). Hai người cùng map không thấy nhau;
"multiplayer" mới dừng ở quái dùng chung.

**Đề xuất — đây nên là trọng tâm giai đoạn tới:**
1. Thêm **tick thế giới cố định** (ví dụ 20 Hz) cho mỗi map: tiến vị trí mọi
   `PlayerSession`, cập nhật AI/hồi sinh quái, rồi phát snapshot. Mọi kiểm tra
   tầm đọc trạng thái của tick → hết lớp lỗi B5.
2. **Vùng quan tâm (AOI)** dạng lưới ô: mỗi người chỉ nhận thực thể trong các ô lân
   cận; sự kiện vào/ra ô sinh spawn/despawn.
3. Snapshot delta có `serverTick`; client nội suy thực thể khác trễ ~100 ms, dự
   đoán chính mình (4.4).
4. Load test bằng bot headless (C# console dùng chung `Shared/Packets`) trước khi
   tuyên bố bất kỳ con số người online nào.

### 4.9 Kiến trúc client Unity

**Tốt:** assembly một chiều không vòng; UI/Gameplay/Data không dùng packet; dùng
VContainer + EventBus nhất quán; `NetworkClient` tách handler theo `PacketType`,
bắt lỗi handler riêng để không làm rớt socket.

**Vấn đề:** B13, B14. `ClientPacketHandlers.cs` 815 dòng, `AuthenticationFlowService`
844 dòng, `CharacterCreationView` 696 dòng. Hai hệ HUD (uGUI `InGameHUD` và UI
Toolkit `VillageUIToolkitHud`) chạy song song, `InGameSceneRoot` phải ẩn bớt một
phần của HUD cũ. Xem thêm B15 về mất kết nối khi đang trong game.

**Đề xuất:**
- Chốt **UI Toolkit là hướng chính**; lập danh sách phần còn ở uGUI và chuyển dần,
  xóa phần uGUI tương ứng ngay khi chuyển xong.
- Tách `ClientPacketHandlers` theo domain (Authentication, Characters, World,
  Combat, Tutorial, Inventory) — không đổi contract.
- Tên hiển thị map/khu vực lấy từ catalog server (thêm `displayName` vào
  `MapDefinitions`).
- Luồng mất kết nối trong game (B15): hiện popup ngay trong InGame; sau đó tự
  Resume bằng refresh token rồi `SelectCharacter` lại cùng nhân vật khi hết thời
  gian ân hạn 10 giây; chỉ quay về `Bootstrap` nếu thất bại.

### 4.10 CI, kiểm thử, công cụ

**Vấn đề:** F1, B1. CI chỉ build/test server; không có Unity compile, format,
phân tích tĩnh hay quét secret như CORE-23 yêu cầu.

**Đề xuất:**
1. Sửa `KnightServer.IntegrationTests` theo API hiện tại; thêm job CI có
   `services: postgres` và đặt `KNIGHTONLINE_TEST_POSTGRES_CONNECTION` để chạy
   luôn 3 test PostgreSQL đang bị bỏ qua.
2. Thêm `dotnet format --verify-no-changes` và `.editorconfig` (`charset = utf-8`)
   để chặn mojibake tái diễn.
3. Thêm job compile Unity (GameCI, cần license Unity) hoặc ít nhất một
   checklist Play Mode bắt buộc trong PR.
4. Thêm quét secret (ví dụ gitleaks) và `dotnet list package --vulnerable`.

### 4.11 Tài liệu

**Tốt:** có cổng đọc, quy tắc cốt lõi chặt, DevLog có kỷ luật, ghi rõ tài liệu nào
là ảnh chụp lịch sử.

**Vấn đề (đã xử lý trong phiên):** cấu trúc repository bị trễ 3 tháng so với
code; nhiều tiêu đề/mục tiếng Anh; file nhỏ trùng nội dung (2 hướng dẫn UI
Toolkit, 2 file Archive, kế hoạch PvP tách rời thiết kế game); mâu thuẫn timeout
màn chọn nhân vật; checklist Character Flow chưa đánh dấu phần nhiệm vụ đã xong.

**Đề xuất:** `System_And_HUD_Flow_v2.md` dài 1.653 dòng, trộn đặc tả sản phẩm với
tính năng chưa có — nên tách thành "đã triển khai" và "đặc tả tương lai". Cân nhắc
ADR ngắn (`Documentation/ADR/`) cho các quyết định khó đảo ngược: một instance,
JSON-trong-JSON làm định dạng packet, UI Toolkit làm hướng chính.

## 5. Hướng đi đề xuất

Nguyên tắc: **sửa lưới an toàn trước, rồi mới xây thế giới nhiều người**, vì mọi
tính năng MMO sau này đều dựa trên tick + AOI + transport có hàng đợi.

### P0 — Ổn định nền (1–2 tuần)

1. Sửa integration test (B1) và đưa vào CI cùng PostgreSQL service.
2. Bọc vòng accept (B2), timeout kết nối ẩn danh 10–15 giây (B4), giảm
   `maximumPacketBytes` xuống ~64 KB (B11).
3. Rate limiter có hết hạn + ngưỡng hợp lý (B3, B8).
4. Gọi `AdvancePosition` trước kiểm tra tầm NPC/cổng và trước khi lưu snapshot
   (B5) — vá tạm cho tới khi có tick.
5. Client giữ nguyên `RequestId` khi gửi lại cùng một ý định (B7).
6. Xác nhận và xử lý mất kết nối khi đang trong game (B15).
7. Chạy lại checklist Play Mode trong `Trang_Thai_Du_An.md`.

### P1 — Thế giới nhiều người (3–6 tuần)

1. Generic Host + `ILogger` có cấu trúc + tắt êm.
2. Hàng đợi gửi bounded cho từng kết nối; broadcast không chặn.
3. Tick thế giới theo map; dự đoán + phát lại input ở client (B6).
4. Đồng bộ người chơi khác với AOI lưới; nội suy phía client.
5. Kiểm tra phiên bản protocol khi `Connect` (B10).
6. Bot headless + load test 50 → 200 → 500 kết nối, đo tick time và độ trễ
   broadcast.

### P2 — Nội dung và Production (song song/sau P1)

1. Module Inventory theo boundary ở `Repository_Structure.md` §8.
2. Tổng quát hóa nhiệm vụ (`QuestService` dữ liệu hóa, domain event `MonsterKilled`).
3. Nâng băm mật khẩu + rehash khi đăng nhập (B9); kho token an toàn trên client;
   TLS cho transport game.
4. Dọn client (B13, B14); chốt UI Toolkit.
5. Cân nhắc định dạng packet nhị phân có version (MessagePack/protobuf) thay JSON
   lồng JSON; `PacketType` đang là `byte` (tối đa 255 loại) — nên chuyển `ushort`
   trước khi số loại packet tăng nhanh.

## 6. Rủi ro còn lại

- Chưa có bằng chứng Unity compile/Play Mode sau các thay đổi giao diện tháng 11.
- 3 test PostgreSQL và toàn bộ integration test chưa chạy trong phiên này.
- Mọi con số sức chứa (500 tài khoản/750 kết nối) chỉ là giới hạn cấu hình, chưa
  được load test.

## 7. Thay đổi đã thực hiện trong phiên

| File | Thay đổi |
|---|---|
| `.github/workflows/server-ci.yml` | .NET `8.0.x` → `10.0.x` |
| `KnightServer/KnightServer.csproj` | Thêm `Microsoft.EntityFrameworkCore.Relational` 10.0.12 |
| `KnightServer/Networking/Handlers/AuthenticationPacketHandlers.cs` | Sửa 2 chuỗi mojibake |
| `dotnet-tools.json` | `dotnet-ef` 8.0.11 → 10.0.12 |
| `KnightServer/server*.log` (4 file) | Ngừng theo dõi trong Git |
| `Documentation/**` | Gộp, dịch, cập nhật — xem `Documentation/README.md` |

Kiểm tra sau thay đổi: build server 0 lỗi/0 cảnh báo; unit test 83 đạt, 3 bỏ qua;
`dotnet tool restore` thành công.
