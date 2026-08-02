# Luồng Unity hiện tại và mức sẵn sàng Alpha 500 người online

> Trạng thái: tài liệu hiện hành, đối chiếu trực tiếp với code ngày 2026-08-02.
> Phạm vi: Unity Client, kết nối game server, authentication, Character Select,
> vào game và rủi ro nhiều thiết bị.
> Không dùng tài liệu này để khẳng định Production-ready hoặc đã chịu tải 500
> người nếu chưa vượt qua các cổng kiểm thử ở phần cuối.

## 1. Mục đích và phân loại theo Core Rules

Tài liệu này giúp một thành viên mới trả lời được bốn câu hỏi:

1. Bấm Play trong Unity thì code chạy theo thứ tự nào?
2. Đăng nhập, chọn/tạo nhân vật và vào game đi qua những module nào?
3. Hai hoặc nhiều thiết bị dùng cùng tài khoản hiện được xử lý ra sao?
4. Còn thiếu gì trước Alpha giới hạn 500 tài khoản online đồng thời?

Phân loại rủi ro:

| Phần | Mức | Hệ quả bắt buộc |
|---|---|---|
| Account, token, active session | Critical | Authorization, lease đúng, idempotency, audit, metric, test và phương án khôi phục |
| Chọn nhân vật, movement, combat, monster | Authoritative gameplay | Server quyết định, validation, test concurrency/reconnect |
| Panel, presenter, scene Unity | Presentation | Không chứa luật nghiệp vụ, kiểm tra serialized reference và scene wiring |
| Store token và cổng đăng ký Development | Tooling/prototype | Phải bị chặn khỏi Production |

Nguồn sự thật của mọi yêu cầu vẫn là
`Documentation/KNIGHT_PROJECT_CORE_RULES.md`. File này mô tả hiện trạng và khoảng
trống, không tạo ngoại lệ cho Core Rules.

## 2. Scene Unity đang được build

`KnightClient/ProjectSettings/EditorBuildSettings.asset` hiện cấu hình:

1. `App.unity` — bật, scene khởi đầu.
2. `Bootstrap.unity` — bật, được App nạp additive.
3. `InGame.unity` — bật, được nạp sau khi server chấp nhận nhân vật.

Vì vậy phải chạy từ `App.unity`. UI đăng nhập hiện nằm trong Bootstrap. Scene
`Login.unity` legacy đã được loại bỏ để chỉ còn một nơi sở hữu luồng Entry và
tránh một lần chuyển scene không cần thiết.

## 3. Luồng khởi động Unity hiện tại

```text
App.unity
  -> AppLifetimeScope tạo dependency sống suốt ứng dụng
  -> tạo NetworkClient + GameSession dạng DontDestroyOnLoad
  -> nạp Bootstrap.unity theo Additive
  -> GameLifetimeScope nối dependency cho UI Bootstrap
  -> GameBootstrap gọi NetworkClient.ConnectAsync()
  -> client mở TCP và gửi ConnectRequest
  -> server trả kết quả kết nối
  -> Entry chờ người chơi chủ động thao tác
```

### 3.1 AppLifetimeScope

Đây là composition root cấp ứng dụng. Nó đăng ký `EventBus`, cấu hình network,
authentication/gameplay settings, session store Development, các packet response
handler, `NetworkClient` và `GameSession`.

`NetworkClient` và `GameSession` tồn tại khi chuyển scene. UI không được tự tạo
network client hoặc tự giữ trạng thái authoritative riêng.

### 3.2 GameLifetimeScope và Bootstrap

`GameLifetimeScope` nhận `AppLifetimeScope` làm parent, đăng ký các service của
luồng Authentication/Character và inject các view/presenter trong scene.
`GameBootstrap.StartAsync()` gọi `NetworkClient.ConnectAsync()`.

Khi không bật Development bypass, kết nối TCP thành công **chưa đồng nghĩa đã
đăng nhập**. Entry chỉ hiển thị lựa chọn và chờ người dùng bấm.

## 4. Luồng Entry và authentication

### 4.1 Mở app chưa có phiên lưu

```text
Kết nối server
  -> Entry
  -> Chơi mới
  -> CreateGuestRequest(deviceId)
  -> server tạo/xác thực guest và xin active-account lease
  -> thành công: AccountReadyEvent
  -> yêu cầu catalog tạo nhân vật + danh sách nhân vật
  -> Character Select
```

### 4.2 Mở app đã có refresh token

Refresh token lưu từ lần trước chỉ làm Entry hiển thị “Chơi tiếp”. Client **không
tự gửi token khi vừa mở app**.

```text
Entry
  -> người chơi bấm Chơi tiếp
  -> ResumeAccountRequest(refreshToken, deviceId)
  -> server xác minh/rotate token và xin active-account lease
  -> thành công: lưu phiên mới -> Character Select
  -> tài khoản đang active nơi khác: popup và ở lại Entry
```

### 4.3 Có tài khoản / Đổi tài khoản

Người chơi nhập username và password. Presenter chỉ stage thông tin; request thật
được gửi khi người chơi bấm Chơi tiếp:

```text
Nhập tài khoản/mật khẩu
  -> StageLogin (chưa xác thực)
  -> quay lại Entry
  -> Chơi tiếp
  -> LoginRequest(username, password, deviceId, guestToken nếu có)
  -> server xác thực và xin lease
```

Không được log password hoặc raw token. Store hiện tại là adapter Development;
Production phải thay bằng Keychain/Keystore/Credential Manager.

### 4.4 Đăng ký guest

Luồng hiện có transaction đăng ký và PKCE-like verifier/challenge, nhưng portal và
hoàn tất tự động vẫn là adapter Development. Nó chỉ là đường chuẩn bị kiến trúc,
không phải cổng đăng ký Production. Điều kiện Production nằm trong
`Documentation/Production/Registration_Flow.md`.

## 5. Luồng Character Select, tạo nhân vật và vào game

Sau `AccountReadyEvent`, `CharacterFlowController` yêu cầu độc lập:

- catalog class/body/appearance của server;
- tối đa ba slot/danh sách nhân vật của account trên server đang chọn.

Khi nhận danh sách, Character Select được mở. Theo quyết định sản phẩm hiện tại,
**Character Select đã tính là active** vì active-account lease được claim ngay sau
authentication thành công, trước khi chọn nhân vật.

```text
Character Select
  +-> slot có nhân vật -> SelectCharacterRequest
  |     -> server kiểm tra account sở hữu nhân vật, lease và active player
  |     -> CharacterSelectedEvent
  |     -> GameSession giữ SelectedCharacter
  |     -> nạp InGame.unity bằng LoadSceneMode.Single
  |
  +-> slot trống -> Character Creation
        -> lấy catalog do server cấp
        -> check tên
        -> CreateCharacterRequest
        -> server validate và lưu DB
        -> thành công thì tự gửi chọn nhân vật
```

Trong `InGame.unity`, `InGameSceneRoot` đọc nhân vật đã chọn và gửi request lấy
danh sách monster. Gameplay/HUD được lắp bởi `InGameLifetimeScope`.

Nút Quay lại từ Character Select gửi `LeaveAccountSessionRequest`. Server chỉ
release đúng lease của connection đó và detach account khi connection chưa có
player session, sau đó client trở về Entry.

## 6. Nhiều thiết bị cùng đăng nhập: hành vi hiện tại

### 6.1 Trường hợp đang hoạt động đúng

Giả sử thiết bị A đã đăng nhập và đang ở Character Select hoặc InGame:

1. A đang giữ lease theo `accountKey -> connectionId`.
2. B có thể mở app và đứng ở Entry vì socket ẩn danh chưa chiếm lease.
3. Khi B bấm Chơi tiếp hoặc đăng nhập, server xác thực rồi thử claim lease.
4. Store trả `ActiveElsewhere`.
5. A không bị đá ra.
6. B nhận popup “Tài khoản đang được đăng nhập ở nơi khác.” và ở lại Entry.

Nếu A đóng kết nối TCP sạch, server cleanup và chỉ release lease khi
`connectionId` đúng owner. Cơ chế này tránh connection cũ xóa nhầm lease của một
owner khác trong cùng tiến trình.

### 6.2 Lỗ hổng và giới hạn hiện tại

| Mức | Lỗ hổng | Tác động thực tế | Yêu cầu sửa trước Alpha/Production |
|---|---|---|---|
| Đã xử lý trong một process | Lease có heartbeat, TTL, disconnect grace và generation | App crash/mất mạng không còn giữ account vô hạn; connection cũ không thể renew/release generation mới | Tiếp tục kiểm thử Unity hai thiết bị và bổ sung adapter phân tán trước multi-node |
| Blocker khi chạy nhiều instance | Lease chỉ tồn tại trong một server process | Hai server instance có thể cùng cho một account online | Redis/DB distributed lease hoặc chỉ chạy một instance có rào chắn triển khai rõ ràng; không dùng in-memory lock làm bảo đảm Production |
| Cao | Không có idempotency cho authentication/resume | Retry/duplicate refresh token có thể va vào rotation/reuse detection và cho kết quả khó đoán | RequestId, lưu kết quả theo boundary tin cậy và semantics retry rõ ràng |
| Cao | TCP game transport chưa có TLS cho môi trường thật | Credential/token có nguy cơ bị nghe lén | TLS bắt buộc ngoài local; pinning/chính sách chứng thư theo kế hoạch phát hành |
| Cao | Refresh token lưu bằng store Development | Không đạt yêu cầu bảo vệ credential trên thiết bị | Adapter secure storage theo nền tảng; xóa khi uninstall theo hành vi nền tảng |
| Cao | Rate limiter in-memory, theo IP và không phân tán | Người dùng chung NAT ảnh hưởng nhau; restart xóa limit; nhiều instance không chia sẻ; key cũ có thể tích tụ | Distributed/sliding-window limiter, giới hạn theo IP + account/device đã băm, eviction và metric |
| Trung bình | Các thời gian kiểm tra/xung đột trong client settings hiện không điều khiển lease | Cấu hình gây hiểu nhầm, không giải quyết session chết | Loại bỏ hoặc nối vào policy server có contract rõ ràng; client không tự quyết active state |
| Trung bình | Entry socket ẩn danh vẫn tiêu thụ connection | Transport cap 750 đã chặn vượt tổng, nhưng bot vẫn có thể chiếm slot hợp lệ | Bổ sung idle timeout, per-IP cap và chống slow client |

Kết luận: quy tắc “thiết bị vào sau không đá thiết bị đang online” hiện được bảo
vệ bằng lease sống trong một process. Heartbeat gia hạn TTL; disconnect rút thời
hạn còn grace period; mọi packet authenticated bị kiểm tra generation tại network
boundary. Nó vẫn chưa bền vững qua server restart hoặc nhiều server instance vì
adapter hiện tại lưu trong RAM.

## 7. Đánh giá mục tiêu Alpha: tối đa 500 online

### 7.1 Phải định nghĩa “online” một cách authoritative

Đề xuất chốt cho Alpha:

- `ActiveAccountCount`: số lease account còn hiệu lực; bao gồm Character Select và
  InGame, đúng quyết định sản phẩm hiện tại.
- `TransportConnectionCount`: mọi socket, bao gồm Entry chưa đăng nhập.
- Giới hạn công bố `500 online` áp vào `ActiveAccountCount`.
- Phải có giới hạn riêng cho transport connection để 500 người thật không bị bot
  hoặc client treo ở Entry chiếm hết tài nguyên.

Server là nơi duy nhất đếm và quyết định nhận/từ chối. Không lấy số online từ Unity.

### 7.2 Admission hiện tại

Server hiện có hai cổng capacity atomic, lấy từ config:

- `maximumActiveAccounts = 500`: claim thứ 501 trả authentication `ServerFull`;
- `maximumTransportConnections = 750`: socket vượt giới hạn nhận
  `ConnectResult.ServerFull` rồi đóng có kiểm soát;
- response server đầy sau token rotation mang session mới về để client không tái
  sử dụng refresh token đã revoke;
- capacity snapshot cung cấp count/maximum làm boundary cho metric/Admin read model.

Concurrency test chứng minh đúng 500/501 active claim và 750/1000 transport gate.
Tuy nhiên đây chưa phải load test 500 client thật; chưa có reserved capacity,
queue, idle/per-IP limit, dashboard hoặc soak test. Do đó trạng thái hiện tại vẫn
là **chưa đạt cổng mở Alpha 500 online**, dù hard-cap foundation đã hoàn thành.

### 7.3 Điểm nghẽn lớn nhất ở gameplay broadcast

`ConnectionRegistry.BroadcastAsync` hiện duyệt toàn bộ connection và `await` gửi
từng client theo thứ tự. Monster health/death/respawn được broadcast toàn cục,
không lọc account đã xác thực, map, vùng quan sát hay khoảng cách.

Với 500 connection, mỗi event monster có thể tạo 500 lần gửi tuần tự. Một client
chậm có thể kéo dài cả lượt broadcast. Trước Alpha cần:

1. Chỉ gửi cho connection đã authenticated và đúng map/interest set.
2. Mỗi connection có outbound queue hữu hạn và chính sách slow-consumer.
3. Không để một send chậm chặn fan-out tới các client khác.
4. Có packet coalescing/rate policy cho state thay đổi nhanh.
5. Đo queue depth, dropped/coalesced packet, send latency và disconnect reason.

### 7.4 Các cổng bắt buộc trước khi mở Alpha

#### P0 — không mở Alpha nếu thiếu

- Nghiệm thu admission bằng client thật và load test: tối đa 500 active account,
  transport cap riêng và mã “server đầy” thay vì timeout im lặng.
- Hoàn tất manual/integration test Unity cho lease TTL + heartbeat + grace +
  generation với app close, crash, airplane mode và suspend; server restart cần
  adapter lease bền vững/phân tán.
- Interest-based broadcast và outbound queue hữu hạn.
- TLS cho endpoint Alpha public; secret lấy từ environment/secret store.
- Secure token store trong bản client phát hành.
- Structured log, metric, health/readiness cho connection/auth/session/game loop.
- Test concurrency cùng account và test idempotency/retry authentication.
- Movement/combat được server validate đầy đủ cho phần gameplay đưa vào Alpha.

#### P1 — nên hoàn thành trước đợt mời rộng

- Distributed rate limit và chống connection flood/slowloris.
- Dashboard và alert có owner/runbook.
- Admin read model xem số connection/lease theo server mà không lộ token/PII.
- Command quản trị disconnect/revoke thông qua Admin API có permission, lý do,
  RequestId và audit; Admin Web không truy cập thẳng game DB.
- Quy trình deploy, rollback/forward-fix, backup và restore drill.

### 7.5 Kịch bản kiểm thử tải tối thiểu

Không nhảy thẳng lên 500. Chạy cùng workload đại diện theo các nấc:

1. 50 người: login/resume, Character Select, vào map, đánh monster.
2. 100 người: thêm reconnect và nhiều thiết bị cùng account.
3. 250 người: login burst, monster event burst, một nhóm client mạng chậm.
4. 500 người: workload đầy đủ.
5. 600 người: safety margin 20%, server phải giữ ổn định hoặc từ chối có kiểm soát.
6. Soak 2–4 giờ ở 500: theo dõi RAM, CPU, GC, socket, DB pool và queue tăng dần.

Các failure test bắt buộc:

- 2–10 thiết bị cùng bấm Chơi tiếp cho một account;
- disconnect/reconnect đồng loạt;
- server restart trong Character Select và InGame;
- database chậm/mất kết nối;
- client không đọc packet hoặc gửi packet chậm;
- duplicate/reordered request;
- connection thứ 501 và transport vượt quota.

Ngưỡng latency/error cụ thể phải được chủ dự án chốt sau baseline đầu tiên. Không
tự tuyên bố “đạt 500” chỉ vì server không crash; phải có acceptance criteria đo
được, test report và rủi ro còn lại.

## 8. Observability và Admin Management Contract

### 8.1 Metric/read model tối thiểu

- transport connections: anonymous/authenticated/select/in-game;
- active leases, lease conflict, heartbeat expiry, reconnect outcome;
- login/resume success/failure/rate-limited và latency;
- packet receive/send rate, outbound queue depth, slow-consumer disconnect;
- active player theo map, game-loop duration, broadcast fan-out latency;
- DB query latency/pool saturation và unhandled exception.

Nhãn metric không chứa raw account key, username, IP đầy đủ hoặc token.

### 8.2 Admin Management Contract cho session/capacity

Admin API về sau được phép cung cấp read model: server, trạng thái capacity,
connection count, active lease count, trạng thái Select/InGame, lease expiry và
account identity đã che khi có quyền phù hợp.

Command nguy hiểm như disconnect session hoặc revoke token phải có:

- permission theo action/server/scope;
- lý do bắt buộc;
- RequestId/idempotency;
- actor, thời gian UTC, trước/sau và kết quả trong audit append-only;
- giới hạn phạm vi, preview/dry-run khi thao tác hàng loạt;
- metric/alert cho hành vi bất thường.

Admin Web không được truy cập trực tiếp database game hoặc dictionary trong process.

## 9. Boundary cần giữ khi củng cố code

- Unity View/Presenter chỉ hiển thị và phát ý định; không quyết định account active,
  nhân vật hợp lệ, damage, HP hoặc vị trí cuối.
- Network boundary ánh xạ packet sang event/model; Gameplay/UI không phụ thuộc sâu
  vào chi tiết protocol.
- Authentication handler parse/auth/validate rồi gọi service/store; không nhồi
  toàn bộ luật session vào packet handler.
- Lease, rate limiter, token store và registration portal là interface có adapter
  Development/Production tách biệt.
- Mọi giới hạn 500, timeout, TTL, queue size và rate phải là config đã validate,
  không hardcode rải rác.
- Contract thay đổi phải version hóa và xét client cũ/server mới.

## 10. Kết luận hiện trạng

Luồng Unity hiện đã có ranh giới App → Bootstrap → Entry → Authentication →
Character Select/Creation → InGame tương đối rõ, và server đang là nơi xác thực
account/chọn nhân vật. Chính sách thiết bị vào sau bị từ chối, không đá người đang
online, đã có nền tảng trong một server process.

Tuy nhiên, **chưa được mở Alpha 500 người chỉ dựa trên code hiện tại**. Ba blocker
lớn nhất là:

1. active lease đã có TTL/heartbeat/generation nhưng vẫn chưa phân tán;
2. broadcast gameplay đang fan-out tuần tự tới toàn bộ connection;
3. chưa có load/soak test, dashboard và cơ chế chống connection flood đầy đủ.

Thứ tự củng cố tiếp theo: distributed lease → interest-based network delivery →
observability/connection hardening → load/failure test → mới chốt ngày Alpha.

### 10.1 Policy session đang áp dụng

- Heartbeat mặc định: 5 giây.
- Lease TTL mặc định: 20 giây.
- Disconnect grace mặc định: 10 giây.
- Explicit Leave ở Character Select giải phóng ngay đúng generation.
- Disconnect ngoài ý muốn giữ lease tối đa grace period.
- Thiết bị khác bị từ chối khi lease còn sống; không đá owner hiện tại.
- Packet authenticated/character-selected chỉ được xử lý khi connection còn sở
  hữu đúng generation chưa hết hạn.

Ba giá trị thời gian nằm trong `serverSettings.json` và được validate khi server
khởi động; đây là default Development, không phải balance Production đã chốt.
