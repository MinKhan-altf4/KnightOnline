# KẾ HOẠCH CHUYỂN AUTHENTICATION, REGISTRATION VÀ CHARACTER FLOW LÊN PRODUCTION

## 1. Mục đích

Tài liệu này tổng hợp những phần phải thay đổi trước khi mở server thật cho ba
luồng liên quan:

1. Đăng nhập, refresh token và ghi nhớ tài khoản.
2. Active Account, Character Select và vào game.
3. Đăng ký tài khoản và chuyển đổi guest thành account chính thức.

Nguyên tắc xuyên suốt:

- Client chỉ gửi request; server quyết định kết quả.
- Không có hệ thống “bảo mật tuyệt đối”. Production phải dùng phòng thủ nhiều
  lớp, giới hạn thiệt hại, thu hồi được credential và truy vết được sự cố.
- Không tự động đăng nhập chỉ vì thiết bị có refresh token. Người chơi phải nhấn
  **Chơi tiếp**.
- Một account chỉ được giữ một Active Session trong một server game.
- Người đang Active không bị thiết bị đến sau đá khỏi game.
- Không dùng adapter Development trong Staging hoặc Production.

---

## 2. Luồng tổng thể khi mở ứng dụng

### 2.1 Thiết bị chưa từng đăng nhập

```text
Mở ứng dụng
  → kết nối anonymous
  → Entry hiển thị:
      - Chơi mới
      - Có tài khoản
      - Chọn server
  → không gửi request xác thực cho tới khi người chơi thao tác
```

### 2.2 Thiết bị có refresh token

```text
Mở ứng dụng
  → đọc refresh token từ secure storage
  → kết nối anonymous
  → Entry hiển thị:
      - Chơi tiếp: account đã che một phần
      - Đổi tài khoản
      - Chọn server
  → không tự động vào Character Select
```

Khi nhấn **Chơi tiếp**:

```text
Unity gửi ResumeAccountRequest
  → server kiểm tra token, device, expiry, revoke và reuse
  → rotate refresh token
  → thử claim Active Account Lease
      ├─ account chưa Active → cấp session → Character Select
      └─ account đang Active → từ chối thiết bị đến sau
                               → popup:
                                 “Tài khoản đang được đăng nhập ở nơi khác.”
```

### 2.3 Đăng nhập bằng username/password

```text
Người chơi chọn Có tài khoản/Đổi tài khoản
  → nhập username/password
  → Unity chỉ giữ password trong RAM
  → quay về Entry hiển thị Chơi tiếp
  → chỉ khi nhấn Chơi tiếp mới gửi LoginRequest
  → server xác thực và thử claim Active Account Lease
```

Khi đăng nhập thành công:

- Chỉ tài khoản đăng nhập gần nhất được ghi nhớ trên thiết bị.
- Refresh token cũ của thiết bị phải được thu hồi theo policy.
- Password phải bị xóa khỏi RAM ngay sau khi có kết quả.
- Không lưu password bằng `PlayerPrefs`, file JSON, registry hoặc database local.

---

## 3. Active Account và Character Select

### 3.1 Quy tắc chính thức

- Character Select được tính là **Active**.
- Một account chỉ có một Active Account Lease trên mỗi game server.
- Lease được claim sau khi xác thực thành công, trước khi hiện Character Select.
- Thiết bị đến sau không được thay thế hoặc disconnect người đang Active.
- Nếu account đã Active, thiết bị đến sau chỉ nhận popup rồi quay về Entry.
- Character Select không tự disconnect theo thời gian; phiên được quản lý bằng
  Active Account Lease và heartbeat.
- Khi timeout, server giải phóng đúng lease và disconnect về Entry.
- Một account được tạo tối đa ba nhân vật trên mỗi server.
- Chọn nhân vật vẫn phải kiểm tra:
  - connection sở hữu Active Account Lease;
  - character thuộc account;
  - character chưa online;
  - connection chưa chọn character khác.

### 3.2 Khi vào game

```text
Character Select
  → ListCharactersRequest
  → server trả tối đa 3 character thuộc account/server
  → SelectCharacterRequest(characterId)
  → server kiểm tra ownership + active lease + online state
  → server tạo PlayerSession chính thức
  → trả SelectCharacterResponse
  → Unity chuyển sang InGame
```

### 3.3 Disconnect và cleanup

Production không được chỉ dựa vào sự kiện đóng socket:

- Active lease phải có TTL.
- Client gửi heartbeat định kỳ.
- Lease chỉ được renew bởi đúng `ConnectionId/session generation`.
- Cleanup chỉ được release lease nếu connection vẫn là owner.
- Crash, mất mạng, kill app hoặc suspend sẽ thành inactive sau TTL/grace period.
- Connection cũ không được giải phóng lease mới.

---

## 4. Luồng Chơi mới và guest

```text
Nhấn Chơi mới
  → CreateGuestRequest(deviceId)
  → server tạo guest account + refresh-token family
  → claim Active Account Lease
  → Character Select
  → tạo/chọn nhân vật
  → được chơi giới hạn đến level/policy guest
```

Guest:

- Có account key và token riêng.
- Nhân vật thuộc guest account trên server.
- Bị giới hạn các chức năng quan trọng theo cấu hình/policy server.
- Không bị xóa chỉ vì đóng ứng dụng.
- Bị xóa khi người chơi đăng nhập account có sẵn trên đúng thiết bị/guest session
  theo policy đã chốt.
- Khi đăng ký thành công, guest được chuyển đổi nguyên tử thành account chính
  thức; không tạo lại nhân vật.

---

## 5. Luồng đăng ký account Production

```text
Guest chọn Đăng ký
  → Unity sinh RequestId + PKCE verifier/challenge
  → BeginRegistrationRequest
  → server tạo RegistrationTransaction có expiry
  → trả URL Web Account Service
  → Unity mở trình duyệt hệ thống
  → người chơi nhập username/email/password trên website HTTPS
  → xác minh email
  → Web Account Service hoàn tất transaction
  → callback bằng App Link/Universal Link
  → Unity gửi authorization code + PKCE verifier
  → server consume code một lần
  → chuyển guest thành registered account trong DB transaction
  → thu hồi guest credential
  → cấp refresh-token family mới
  → quay về Entry và hiển thị Chơi tiếp
```

### 5.1 Dữ liệu không được truyền qua URL

- Password.
- Guest refresh token.
- Access/refresh token.
- Password hash.
- Email verification secret.
- Authorization code tồn tại lâu dài.

### 5.2 Transaction đăng ký bắt buộc

Mỗi registration transaction cần:

- transaction ID;
- RequestId/idempotency key;
- account/guest identity dạng server-side reference;
- state/nonce hash;
- PKCE challenge;
- authorization-code hash;
- created, expiry và consumed UTC;
- version/concurrency token;
- correlation ID;
- kết quả cuối cùng.

Code, PKCE, expiry và trạng thái chưa dùng phải được kiểm tra và consume nguyên
tử. Replay hoặc hai callback đồng thời không được cấp token hai lần.

### 5.3 Guest conversion

Trong một database transaction:

1. Khóa registration transaction và guest account phù hợp.
2. Kiểm tra transaction chưa dùng, chưa hết hạn và đã xác minh email.
3. Kiểm tra unique username/email chuẩn hóa.
4. Chuyển `AccountKind` từ Guest sang Registered.
5. Giữ nguyên ownership của tối đa ba character.
6. Thu hồi guest refresh-token family.
7. Tạo refresh-token family mới.
8. Đánh dấu registration transaction consumed.
9. Ghi security event/outbox.
10. Commit.

Nếu có lỗi trước commit, toàn bộ thay đổi phải rollback.

---

## 6. Thành phần hiện tại có thể giữ nguyên

Các contract/luồng sau đã được thiết kế để tiếp tục sử dụng:

- `AccountAuthenticationService`.
- Refresh-token rotation và reuse detection hiện có.
- `AuthenticationFlowService` và Entry chờ người chơi xác nhận.
- `IActiveAccountLeaseStore`.
- `RegistrationFlowService`.
- `IRegistrationTransactionStore`.
- `IRegistrationPortal`.
- `IGuestRegistrationConverter`.
- Packet Begin Registration và các event/result phía Unity.
- Server-authoritative character ownership/selection.
- Giới hạn ba character/account/server từ server configuration/repository.

Các lớp này vẫn cần security review và integration test, nhưng không nên viết lại
toàn bộ khi thay adapter Production.

---

## 7. Thành phần local-only phải thay

| Hiện tại/local | Production phải dùng |
|---|---|
| Raw TCP game transport | TLS hoặc transport bảo mật đã được review |
| `DevelopmentAccountSessionStore` | Keychain/Keystore/Credential Manager |
| `InMemoryActiveAccountLeaseStore` | Redis/distributed lease có TTL + heartbeat |
| `InMemoryRegistrationTransactionStore` | PostgreSQL transaction store |
| `DevelopmentRegistrationPortal` | Web Account Service HTTPS |
| `CompleteDevelopmentRegistrationRequest` | Verified web callback + one-time authorization code |
| Development registration code trả về client | Code chỉ phát sau web verification |
| Console security logging | Structured security audit + metric + alert |
| Secret/config local | Vault/secret manager/managed identity |
| Một process server | Multi-instance deployment có readiness và graceful shutdown |

`CompleteDevelopmentRegistrationRequest` phải bị vô hiệu trong Staging và
Production. Production không nhận username/password qua game socket.

---

## 8. Database và migration cần bổ sung

### 8.1 Bảng chính

- `accounts`;
- `account_credentials`;
- `refresh_token_families`;
- `refresh_sessions`;
- `characters`;
- `registration_transactions`;
- `email_verifications`;
- `security_events`;
- `account_restrictions`;
- `outbox_messages`.

### 8.2 Constraint quan trọng

- Unique normalized username.
- Unique normalized email nếu email là định danh đăng nhập.
- Unique registration RequestId.
- Unique authorization-code hash.
- Foreign key character → account.
- Concurrency/version token cho transaction quan trọng.
- Index cho token hash, family ID, expiry và trạng thái.

### 8.3 Quy trình migration

- Migration phải được version hóa bằng EF Core.
- Chạy trên Development và Staging trước Production.
- Dùng expand/contract nếu có rolling deployment.
- Backup và kiểm tra restore trước migration phá vỡ dữ liệu.
- Không chỉnh schema Production bằng tay.

---

## 9. Password, token và thiết bị

### 9.1 Password

- Website nhận password qua HTTPS.
- Hash bằng Argon2id hoặc thuật toán được security review chấp thuận.
- Salt riêng cho từng password.
- Work factor nằm trong config/version của hash.
- Có thể dùng pepper trong secret manager.
- Không log password hoặc password hash.
- Hỗ trợ rehash khi policy thay đổi.

### 9.2 Refresh token

- Token ngẫu nhiên có entropy đủ mạnh.
- Database chỉ lưu token hash.
- Rotation sau mỗi lần resume.
- Reuse token cũ phải thu hồi cả token family và tạo security alert.
- Token có expiry tuyệt đối.
- Đổi/reset password phải thu hồi session theo policy.
- Thiết bị chỉ lưu account đăng nhập thành công gần nhất.

### 9.3 Device ID

- Device ID không phải bằng chứng danh tính và không thay thế token.
- Không tin device ID do client gửi để authorization.
- Chỉ dùng làm tín hiệu session/risk/rate limit đã được hash hoặc pseudonymize.
- Phải có policy khi hệ điều hành reset hoặc ứng dụng bị gỡ.

---

## 10. Rate limit và chống tấn công

Tách bucket/policy cho:

- Create Guest.
- Login.
- Resume token.
- Begin Registration.
- Email verification/resend.
- Complete Registration.
- Password recovery.
- Character selection.

Rate limit nên kết hợp account, IP, device, subnet và risk signal; không khóa chỉ
dựa vào IP. Thông báo đăng nhập thất bại không được giúp dò username/email tồn
tại. CAPTCHA chỉ dùng theo risk để tránh gây khó chịu cho người chơi bình thường.

---

## 11. Admin Web và vận hành

Admin Web không truy cập trực tiếp game database. Admin API cần read model cho:

- account status và loại guest/registered;
- email verification status;
- session/revocation history;
- registration transaction status;
- Active Session/server;
- security events và rate-limit incidents.

Phải che email, IP, device và identifier nhạy cảm. Không endpoint nào được trả:

- password/password hash;
- raw refresh token;
- raw authorization code;
- email verification secret.

Command khóa/mở account, thu hồi session hoặc hỗ trợ recovery phải có permission
chi tiết, lý do, RequestId, audit append-only và MFA/passkey cho admin.

---

## 12. Thứ tự triển khai Production

### Giai đoạn A — Persistence và secret

1. Tạo production schema/migrations.
2. Chuyển password hashing policy phù hợp Production.
3. Triển khai secret manager.
4. Triển khai secure token storage trên Unity cho từng nền tảng.

### Giai đoạn B — Web registration

1. Dựng Web Account Service HTTPS.
2. Dựng PostgreSQL registration transaction store.
3. Xác minh email và transactional outbox.
4. App Links/Universal Links + PKCE/state/one-time code.
5. Tắt development completion.

### Giai đoạn C — Distributed session

1. Redis Active Account Lease.
2. TTL, heartbeat, grace period và generation ownership.
3. Test crash, mất mạng, reconnect và nhiều server instance.

### Giai đoạn D — Hardening

1. TLS game transport.
2. Rate limit phân tán.
3. Structured log, metric, trace, alert.
4. Audit/security events.
5. Dependency, secret và vulnerability scan.
6. Backup/restore drill và incident runbook.

### Giai đoạn E — Staging và Alpha

1. Chạy migration trên Staging.
2. Test hai thiết bị/một account.
3. Test token replay/reuse/expiry.
4. Test đăng ký duplicate/race/rollback.
5. Test Character Select timeout và lease cleanup.
6. Load test login burst và reconnect storm.
7. Security review/pentest.
8. Chỉ mở Alpha khi toàn bộ release gate đạt.

---

## 13. Release gate bắt buộc

Không mở Production nếu còn một trong các điều sau:

- Development authentication bypass đang bật.
- Development registration completion đang bật.
- Game authentication truyền qua kết nối không mã hóa.
- Refresh token hoặc password lưu bằng PlayerPrefs/file rõ.
- Active Account chỉ dùng dictionary trong một process.
- Registration transaction chỉ nằm trong RAM.
- Chưa có email verification/recovery an toàn.
- Chưa có rate limit phân tán.
- Chưa test refresh-token reuse và revoke family.
- Chưa test database rollback/duplicate/race.
- Chưa có backup đã kiểm tra restore.
- Chưa có security audit/metric/alert/runbook.
- Admin dùng chung một quyền toàn năng hoặc truy cập DB trực tiếp.

---

## 14. Ma trận kiểm thử tối thiểu

| Trường hợp | Kết quả bắt buộc |
|---|---|
| Có refresh token nhưng chưa nhấn Chơi tiếp | Không gửi authentication request |
| Token hợp lệ, account inactive | Rotate token, claim lease, vào Character Select |
| Account đang Active nơi khác | Người cũ giữ phiên; người mới nhận popup |
| Hai thiết bị đăng nhập gần đồng thời | Chỉ một atomic lease claim thành công |
| Đứng lâu tại Character Select | Vẫn giữ phiên khi connection/lease còn hợp lệ |
| Chọn character không thuộc account | Server từ chối |
| Character đã online | Server từ chối |
| Kill app/mất mạng | Lease hết hạn sau TTL/grace |
| Connection cũ cleanup sau reconnect | Không xóa lease generation mới |
| Begin Registration lặp RequestId | Không tạo side effect thứ hai |
| Authorization code replay | Không cấp token lần hai |
| PKCE/state sai | Từ chối, ghi security signal phù hợp |
| Transaction hết hạn | Từ chối |
| Username/email race | Unique constraint bảo vệ invariant |
| Guest conversion lỗi giữa chừng | Rollback; guest/character còn nguyên |
| Refresh token cũ bị dùng lại | Revoke family và cảnh báo |
| Gỡ ứng dụng | Không còn credential tại thiết bị |

---

## 15. Trạng thái hiện tại và bước tiếp theo

Hiện tại dự án có scaffold local cho authentication, guest, refresh-token
rotation/reuse detection, Active Account Lease abstraction, Character Select và
registration Begin/Development Complete với PKCE.

Bước nên thực hiện tiếp theo:

1. Dựng `GuestRegistrationPanel` để test local từ Unity.
2. Viết integration test hoàn chỉnh cho guest conversion bằng PostgreSQL test DB.
3. Tạo PostgreSQL `registration_transactions` implementation và migration.
4. Thêm heartbeat/TTL contract cho Redis Active Account Lease.
5. Thay `DevelopmentAccountSessionStore` bằng secure storage abstraction theo
   nền tảng trước khi phát hành build cho người dùng thật.

Tài liệu chi tiết riêng về registration:
`Documentation/Production_Registration_Flow.md`.
