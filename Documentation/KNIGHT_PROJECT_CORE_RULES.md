# KNIGHT PROJECT CORE RULES

> Bộ quy tắc cốt lõi bắt buộc của toàn bộ dự án KnightOnline.
>
> Mục tiêu: xây dựng hệ thống multiplayer có thể mở rộng lâu dài, bảo mật,
> quan sát được, quản trị được và có thể bàn giao cho đội ngũ khác.

## 0. Phạm vi và mức độ bắt buộc

Các từ khóa trong tài liệu:

- **PHẢI / KHÔNG ĐƯỢC**: yêu cầu bắt buộc.
- **NÊN**: mặc định phải làm; trường hợp không làm phải giải thích trong DevLog.
- **CÓ THỂ**: tùy tình huống.

Mỗi thay đổi phải được phân loại trước khi triển khai:

| Mức | Phạm vi ví dụ | Yêu cầu tối thiểu |
|---|---|---|
| Critical | Account, authentication, economy, inventory, trade, payment, admin | Authorization, transaction/idempotency, audit, test, metric và rollback/forward-fix |
| Authoritative gameplay | Combat, movement, quest, drop, progression | Server-authoritative, validation, concurrency/reconnect test và log phù hợp |
| Presentation | Unity UI, animation, view/presenter | Tách khỏi domain/protocol, kiểm tra reference và test phù hợp với rủi ro |
| Tooling/prototype | Công cụ nội bộ, thử nghiệm | Không được đi vào Production nếu chưa đạt tiêu chuẩn tương ứng |

Không áp dụng audit, Admin API hoặc domain event một cách máy móc cho UI thuần
hiển thị. Mức kiểm soát phải tương xứng với rủi ro, nhưng các nguyên tắc về
secret, authorization và toàn vẹn dữ liệu không có ngoại lệ.

---

## CORE-01 — Thiết kế để mở rộng, thay thế và kiểm thử

Không viết code chỉ để “chạy được”.

Module phải có trách nhiệm rõ ràng, dependency một chiều và hợp đồng ổn định.
Ưu tiên composition, policy, strategy, interface và domain event khi chúng
thực sự giảm coupling.

Không tạo các `GameManager`, `GlobalManager`, `SystemManager` hoặc
`MainController` chứa nhiều domain không liên quan.

Không tạo abstraction chỉ để dự đoán tương lai. Abstraction phải phục vụ ít
nhất một boundary, test seam hoặc khả năng thay thế đã xác định.

Khi thêm tính năng, nếu phải sửa nhiều module không liên quan, phải xem lại
thiết kế trước khi tiếp tục.

## CORE-02 — Module giao tiếp qua contract

Module không được truy cập chi tiết triển khai hoặc database của module khác.
Giao tiếp qua:

- command/query;
- interface;
- API/packet contract;
- domain event;
- message;
- read model được công bố.

Không tạo dependency vòng. Không để Gameplay/UI phụ thuộc trực tiếp vào packet
protocol nếu có thể ánh xạ sang model hoặc event của domain.

## CORE-03 — Không lặp logic nghiệp vụ

Logic nghiệp vụ chỉ có một nguồn chính thức. Khi xuất hiện ở nhiều nơi, phải
đưa về domain rule, policy, validator hoặc application service.

Client có thể lặp lại phép tính để dự đoán/hiển thị, nhưng kết quả chính thức
vẫn do server quyết định và không được dùng bản client làm nguồn dữ liệu thật.

## CORE-04 — Server là nguồn sự thật duy nhất

Client chỉ gửi input hoặc request. Server xác thực, kiểm tra trạng thái, xử lý
và trả kết quả chính thức.

Client không được quyết định damage, HP/MP, tiền tệ, vật phẩm, kinh nghiệm,
cấp độ, vị trí cuối cùng, drop, nâng cấp, mở rương, giao dịch hoặc nhiệm vụ.

Mọi request từ client đều không đáng tin cậy. Server phải kiểm tra ít nhất:

- identity, session, quyền và scope;
- trạng thái nhân vật/map;
- range, cooldown, tài nguyên và thứ tự hành động;
- rate limit, replay, duplicate và dữ liệu bất thường.

## CORE-05 — Security First

Tính năng không đủ an toàn không được phát hành Production.

Bắt buộc:

- TLS cho authentication, API và game transport ngoài môi trường local;
- password hashing bằng thuật toán chuẩn có salt và work factor;
- access/refresh-token rotation, expiry, revoke và reuse detection;
- credential lưu trong Keychain/Keystore/Credential Manager phù hợp nền tảng;
- validation, authentication, authorization và least privilege;
- rate limit, replay protection và security audit;
- dependency/security scanning và quy trình cập nhật bản vá;
- không log password, raw token, secret hoặc payload nhạy cảm.

Web registration/deep link phải dùng state/nonce một lần, expiry ngắn và callback
đã đăng ký; không tin account identity do client tự khai.

## CORE-06 — Session, disconnect và reconnect

Không chỉ dựa vào sự kiện đóng ứng dụng. Session online phải dùng:

- heartbeat;
- server-side lease có TTL;
- grace period được cấu hình;
- session generation/connection ID;
- cleanup chỉ được xóa đúng generation mà nó sở hữu.

Close app/tab, crash, mất mạng và suspend phải dẫn tới `inactive` sau thời hạn
lease. Connection cũ không được phép giải phóng session mới.

Reconnect phải xác định rõ resume, replace, reject hoặc invalidate-all; không
được suy diễn ở client.

## CORE-07 — Idempotency và delivery semantics

Command quan trọng phải có `RequestId` hoặc `IdempotencyKey`, đặc biệt với
economy, reward, payment, mail, trade, shop và admin action.

Server phải lưu và kiểm tra kết quả idempotency trong cùng boundary tin cậy với
thay đổi dữ liệu.

Không tuyên bố “exactly once” nếu hệ thống không chứng minh được. Phải ghi rõ
semantics là at-most-once, at-least-once hoặc effectively-once.

Disconnect không đảm bảo transaction chưa commit sẽ tự hủy. Client phải có khả
năng truy vấn trạng thái command bằng RequestId khi kết quả không xác định.

## CORE-08 — Transaction và side effect

Mọi thay đổi economy hoặc quyền sở hữu phải nguyên tử.

Không gọi email, payment, Discord hoặc dịch vụ ngoài khi database transaction
đang mở. Sử dụng:

- transactional outbox cho message/event;
- idempotent consumer;
- saga/compensation cho transaction phân tán;
- retry có giới hạn và dead-letter queue.

Nếu không thể rollback side effect, phải có compensation và audit rõ ràng.

## CORE-09 — Domain event tin cậy

Sự kiện nghiệp vụ quan trọng phải có domain event, ví dụ đăng nhập, tạo nhân
vật, thay đổi vật phẩm/tiền tệ, hoàn thành quest, trade và admin grant.

Event contract phải có:

- EventId, occurred-at UTC;
- version;
- correlation ID và causation ID;
- aggregate/entity identity;
- ordering rule nếu cần;
- schema compatibility policy.

Event cần giao hàng tin cậy phải đi qua Outbox. Consumer phải idempotent.

## CORE-10 — Audit và khả năng truy vết

Thay đổi account, permission, economy, item, equipment, trade, mail, market,
upgrade và reward phải truy vết được:

- actor và actor type;
- thời gian UTC;
- server/device/session phù hợp;
- lý do;
- dữ liệu trước/sau đã lọc thông tin nhạy cảm;
- RequestId/correlation ID;
- kết quả cuối cùng.

Audit log phải append-only, có retention, backup, quyền đọc/export riêng và
cơ chế phát hiện sửa đổi. Không cho Admin API sửa hoặc xóa audit tùy ý.

PII phải được phân loại, che/redact và lưu đúng thời hạn pháp lý/vận hành.

## CORE-11 — Admin Web qua Admin API

Admin Web không truy cập trực tiếp game database. Thao tác quản trị phải qua
Admin API và application/domain service tương ứng.

Mọi tính năng mới thuộc server, domain hoặc dữ liệu vận hành **PHẢI** được thiết
kế để Web Admin có thể quản lý về sau. Đây là yêu cầu kiến trúc ngay từ đầu,
không phải công việc vá thêm sau khi gameplay đã hoàn thành.

Mỗi module phải xác định một **Admin Management Contract** phù hợp, bao gồm:

- trạng thái/read model nào Admin được phép xem;
- query, filter, sort, pagination và export nào cần hỗ trợ;
- command quản trị nào được phép thực hiện;
- permission, server, scope và thời hạn cần kiểm tra;
- audit trước/sau, actor, lý do, RequestId và correlation ID;
- domain event/metric/alert nào phục vụ theo dõi;
- dữ liệu nào là PII/secret và phải che hoặc cấm truy cập;
- concurrency, idempotency và rollback/compensation cho thao tác thay đổi;
- retention/history cần giữ để điều tra hoặc thống kê.

Nếu module chưa cần Admin command, vẫn phải có boundary/read model hoặc event
phù hợp để bổ sung Admin API mà không cho Admin Web truy cập database hay phá
domain invariant.

Tính năng chỉ được xem là **Admin-ready**, không có nghĩa phải dựng ngay giao
diện Web Admin trong cùng task. DevLog phải ghi rõ phần đã sẵn sàng và phần
Admin UI/API nào được hoãn.

Các thao tác nguy hiểm hoặc hàng loạt phải hỗ trợ khi phù hợp:

- dry-run/preview;
- xác nhận lại và nhập lý do;
- giới hạn số lượng/phạm vi;
- idempotency;
- approval nhiều bước;
- chạy background job có progress;
- khả năng dừng/compensate;
- audit và cảnh báo sau thực hiện.

Quyền quản trị phải chi tiết theo action/resource/server/scope, có thời hạn và
lý do. Không dùng một quyền `ADMIN` chung cho công việc hàng ngày.

Owner account chỉ dùng cho cấp/thu hồi quyền và khôi phục khẩn cấp; phải dùng
passkey/security key, session ngắn, thiết bị cho phép và cảnh báo đăng nhập.

Read-only analytics có thể dùng read replica/read model riêng nếu được phê
duyệt, không chứa command path và tuân thủ data governance.

## CORE-12 — Validation tại mọi boundary

Packet/API/controller chỉ:

1. Parse request.
2. Authentication/authorization.
3. Validate.
4. Gọi application service.
5. Ánh xạ response.

Không đặt logic nghiệp vụ phức tạp hoặc SQL trong controller/packet handler/UI.

Validation bao gồm type, length, range, state, permission, scope, RequestId,
duplicate, mass assignment và giới hạn tài nguyên.

## CORE-13 — Dependency Injection có chủ đích

Dependency nghiệp vụ, persistence, network, clock, token generator và external
service phải được inject qua composition root.

Value object hoặc helper thuần không bắt buộc tạo interface. Không dùng DI để
che giấu service locator hoặc global mutable state.

## CORE-14 — Config, constant và secret

Không hardcode dữ liệu vận hành: balance, cooldown có thể điều chỉnh, drop,
reward, URL môi trường, permission, event date, secret và API key.

Có thể dùng constant cho protocol enum, giới hạn bất biến của domain, thuật toán
và default an toàn. Default phải được tài liệu hóa và có validation.

Secret chỉ đến từ environment variable, secret manager, CI/CD secret, vault
hoặc managed identity; không commit vào repository.

## CORE-15 — Concurrency và multiplayer

Mọi tính năng phải xem xét race condition, duplicate, packet reorder, latency,
disconnect, reconnect, server restart và nhiều người thao tác cùng tài nguyên.

Phải chọn rõ:

- isolation/locking strategy;
- optimistic concurrency/version;
- timeout và cancellation;
- retry policy;
- ownership/lease;
- invariant được database bảo vệ.

Không dùng lock trong-memory làm bảo đảm duy nhất khi chạy nhiều server.

## CORE-16 — Database migration an toàn

Mọi schema change phải có migration được version hóa và kiểm thử trên staging.
Không sửa production schema bằng tay.

Ưu tiên expand/contract và backward compatibility để rolling deployment.
Không giả định `Down()` luôn an toàn. Với thay đổi phá hủy dữ liệu phải có:

- backup đã kiểm tra restore;
- kế hoạch chuyển đổi;
- forward-fix hoặc rollback đã diễn tập;
- thời gian và người chịu trách nhiệm;
- migration note trong DevLog.

## CORE-17 — API/packet/event compatibility

Mọi contract phải có versioning và chính sách tương thích.

Phải xem xét:

- client cũ với server mới;
- server cũ với client mới;
- rolling deployment;
- feature flag;
- thời gian deprecation;
- unknown field/message handling;
- migration dữ liệu liên quan.

Breaking change phải được phê duyệt và có kế hoạch rollout/rollback.

## CORE-18 — Error handling và logging

Không nuốt lỗi ngoài trường hợp expected/cancellation đã được nhận diện và chú
thích rõ.

Phân loại:

- cancellation/disconnect dự kiến: xử lý yên lặng hoặc log mức Debug;
- validation/business rejection: structured Info/Warning;
- unexpected exception: Error kèm stack trace và correlation context;
- security incident: security audit/alert riêng.

Không log secret/credential. Log phải có module, severity, UTC, request/session
identifier phù hợp và tránh PII không cần thiết.

## CORE-19 — Observability và vận hành

Module Critical và Authoritative không được release nếu thiếu mức quan sát phù
hợp:

- structured log;
- metric;
- health/readiness check;
- correlation/trace;
- alert có owner;
- dashboard/runbook;
- lịch sử lỗi.

Alert phải có ngưỡng và hành động xử lý, không tạo cảnh báo chỉ để “có”.

## CORE-20 — Thời gian

Server và database lưu UTC. Chỉ chuyển timezone khi hiển thị.

Client time không phải nguồn sự thật cho cooldown, event, reward, expiry,
auction, ban, access grant hoặc transaction.

Các phép kiểm tra thời gian quan trọng phải dùng clock có thể inject/test.

## CORE-21 — Môi trường và dữ liệu

Phải tách Development, Staging và Production về database, secret, admin,
storage, queue, log và config nhạy cảm.

Production data không được sao chép nguyên trạng sang Development. Dữ liệu dùng
test phải được ẩn danh hoặc sinh giả.

Phải có retention, backup, restore drill, disaster recovery và incident
response phù hợp với mức rủi ro.

## CORE-22 — Unity asset và assembly

Mọi Unity asset phải commit cùng file `.meta`. Không tự tạo lại, xóa hoặc bỏ
qua `.meta` của scene, prefab, script, sprite, material và asmdef.

Không commit `Library`, `Temp`, `Logs`, `UserSettings`, build output hoặc IDE
cache.

Assembly dependency phải một chiều. `Shared/Protocol` không được rò vào Gameplay
nếu có thể ánh xạ tại Network boundary. Scene/prefab reference phải được kiểm
tra sau rename/move.

Unity API chỉ được gọi trên main thread trừ API được tài liệu xác nhận an toàn.
Prefab/scene quan trọng phải có validation cho serialized reference bắt buộc.

## CORE-23 — Git, Pull Request và CI

Không push trực tiếp branch được bảo vệ khi dự án đã bật quy trình nhóm.

PR phải có:

- vấn đề và giải pháp;
- module ảnh hưởng;
- rủi ro;
- test/build đã chạy;
- migration và rollback/forward-fix;
- screenshot/demo nếu có UI;
- DevLog liên quan.

CI tối thiểu phải kiểm tra build, test, formatting/static analysis, migration
và secret/dependency scan phù hợp.

Không force-push Production branch. Không commit binary/cache/database/secret.

## CORE-24 — Testing theo rủi ro

Test phải tương xứng với phân loại thay đổi:

- domain unit test;
- database/integration test;
- authorization/security test;
- concurrency/idempotency/rollback test;
- migration compatibility test;
- Unity play/edit mode test hoặc manual checklist cho UI.

Không chỉ kiểm tra happy path. Nếu test tự động chưa khả thi, DevLog phải ghi
manual test, rủi ro còn lại và task bổ sung có owner.

## CORE-25 — DevLog tổng kết theo phiên làm việc

Không tạo DevLog cho từng thao tác, task nhỏ hoặc checkpoint riêng lẻ.

Trong suốt phiên làm việc, phải giữ lại đầy đủ thông tin quan trọng để tổng kết.
Chỉ tạo hoặc cập nhật DevLog khi:

- người dùng yêu cầu rõ ràng, ví dụ: `viết log cho tôi`;
- người dùng xác nhận kết thúc phiên làm việc;
- chuẩn bị bàn giao/PR mà người dùng đã yêu cầu có DevLog.

Một phiên làm việc có thể bao gồm nhiều task liên quan và được tổng kết trong một
DevLog:

```text
Documentation/DevLogs/YYYY-MM-DD-task-name.md
```

DevLog tổng kết tối thiểu:

- mục tiêu và phạm vi;
- công việc hoàn thành;
- file/module thay đổi;
- thay đổi kiến trúc và quyết định kỹ thuật;
- test/build đã chạy;
- migration/config;
- vấn đề, rủi ro và technical debt;
- rollback/forward-fix nếu cần;
- bước tiếp theo;
- commit/PR liên quan khi có.

DevLog không chứa secret, token hoặc dữ liệu nhạy cảm.

Không được dùng quy tắc theo phiên để làm mất dấu quyết định Critical, migration,
security change, test result, rủi ro hoặc technical debt. Các thông tin này phải
được giữ trong ngữ cảnh làm việc và đưa vào DevLog khi người dùng yêu cầu chốt
phiên.

## CORE-26 — Tài liệu và khả năng bàn giao

Module quan trọng phải có tài liệu phù hợp: README, kiến trúc, setup, contract,
migration note, troubleshooting, runbook và ADR cho quyết định khó đảo ngược.

Thiết kế như thể đội khác sẽ tiếp quản mà không cần hỏi tác giả ban đầu.

## CORE-27 — Ngoại lệ có kiểm soát

Ngoại lệ chỉ được phép với quy tắc không liên quan tới secret, authorization,
toàn vẹn account/economy hoặc an toàn dữ liệu.

Ngoại lệ phải được ghi trong ADR/DevLog với:

- quy tắc bị ngoại lệ;
- lý do và phạm vi;
- rủi ro;
- biện pháp giảm thiểu;
- owner;
- ngày hết hạn hoặc điều kiện xóa ngoại lệ.

Prototype phải bị chặn khỏi Production bằng config/build/deployment boundary,
không chỉ bằng ghi chú.

## CORE-28 — Definition of Done

Task chỉ hoàn thành khi:

- đáp ứng acceptance criteria;
- đúng boundary và kiến trúc;
- module server/domain đã xác định Admin Management Contract phù hợp;
- validation/authorization phù hợp;
- error handling và observability phù hợp mức rủi ro;
- test/build hoặc manual checklist đã chạy;
- migration/config/compatibility đã được xử lý;
- DevLog đã cập nhật nếu người dùng yêu cầu chốt phiên/bàn giao;
- không có TODO nghiêm trọng không được ghi nhận;
- có bước tiếp theo và rủi ro còn lại rõ ràng.

“Code chạy được” chưa phải “code hoàn thành”.

---

## Checklist bắt buộc trước khi triển khai

1. Tính năng thuộc mức rủi ro nào?
2. Server/domain nào là nguồn sự thật?
3. Boundary, contract và dependency direction là gì?
4. Validation và authorization nằm ở đâu?
5. Concurrency, duplicate, reconnect và restart xử lý thế nào?
6. Có transaction, idempotency, outbox hoặc compensation nào cần thiết?
7. Admin quan sát/quản trị bằng cách nào?
8. Admin Management Contract gồm read model, command, permission và audit nào?
9. Audit, log, metric và alert nào thực sự cần?
10. Contract/schema có tương thích khi rollout không?
11. Test, migration và rollback/forward-fix là gì?

Nếu câu hỏi Critical chưa có câu trả lời, chưa bắt đầu triển khai Production.

---

## Nguyên tắc tối cao

> Bảo vệ toàn vẹn account, tài sản và dữ liệu người chơi trước tiến độ.

> Client gửi ý định; server xác minh và quyết định kết quả.

> Thiết kế để một đội ngũ khác có thể quan sát, vận hành, mở rộng và bàn giao
> hệ thống mà không phụ thuộc vào tác giả ban đầu.
