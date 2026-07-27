# DevLog — Authentication, Registration và Character Session

Ngày: 2026-07-26  
Phân loại: Critical Authentication + Authoritative Session + Unity Presentation

## 1. Mục tiêu phiên làm việc

Hoàn thiện nền tảng có thể mở rộng cho:

- Entry, Chơi mới, Có tài khoản/Đổi tài khoản và Chơi tiếp;
- guest account và refresh-token session;
- một account chỉ có một Active Session trên một server;
- Character Select được tính là Active;
- giới hạn ba nhân vật/account/server;
- timeout Character Select;
- đăng ký guest thành account chính thức;
- chuẩn bị boundary để chuyển từ local sang server Production;
- giao diện Unity phục vụ kiểm thử các luồng trên.

## 2. Quyết định nghiệp vụ đã chốt

- Có refresh token không đồng nghĩa tự động đăng nhập. Ứng dụng vẫn dừng tại
  Entry và chờ người chơi nhấn **Chơi tiếp**.
- Username/password nhập tại màn hình Đổi tài khoản chỉ được giữ tạm trong RAM.
  Request đăng nhập chỉ được gửi khi người chơi nhấn Chơi tiếp.
- Thiết bị chỉ ghi nhớ account đăng nhập thành công gần nhất bằng refresh token;
  không lưu password.
- Character Select được tính là Active.
- Nếu account đã Active, thiết bị đến sau bị từ chối và nhận thông báo
  “Tài khoản đang được đăng nhập ở nơi khác.” Người đang online không bị đá.
- Một account có tối đa ba nhân vật trên mỗi server.
- Không chọn nhân vật trong 15 giây sẽ giải phóng session, disconnect và trả về
  Entry với popup mất kết nối.
- Guest được chuyển đổi thành account chính thức trong transaction và giữ nguyên
  nhân vật.
- Adapter Development chỉ phục vụ local test và phải bị vô hiệu trước Production.

## 3. Authentication và refresh token

Đã hoàn thành:

- Tắt development authentication bypass trong cấu hình mặc định để `Chơi mới`
  thực sự tạo guest trên anonymous connection.
- Entry không tự resume khi phát hiện refresh token.
- Thêm trạng thái Chơi tiếp/Đổi tài khoản và account display hint đã che.
- Password pending chỉ tồn tại trong bộ nhớ và được xóa sau khi có kết quả.
- Xử lý an toàn việc Unity thoát Play Mode khi network send đang diễn ra.
- Refresh-token rotation, expiry, revoke và reuse detection tiếp tục là nguồn
  xác thực phía server.
- Thêm popup riêng cho account đang Active và forced disconnect.
- Sticky Entry state tránh mất event do thứ tự khởi tạo Presenter.

## 4. Active Account và Character Select

Đã hoàn thành:

- Tách quản lý Active Account qua `IActiveAccountLeaseStore`.
- Adapter local dùng `InMemoryActiveAccountLeaseStore`.
- Mỗi connection có `ConnectionId` riêng.
- Claim, ownership check và release đều dùng contract bất đồng bộ.
- Connection đến sau không thay thế owner hiện tại.
- Cleanup chỉ release lease nếu đúng `ConnectionId` sở hữu.
- `SelectCharacterPacketHandler` kiểm tra connection sở hữu account lease.
- Character Select timeout được cấu hình bằng
  `characters.selectionTimeoutSeconds`.
- Back từ Character Select giải phóng account session và trở về Entry.
- Giữ kiểm tra character ownership, character online và connection đã chọn nhân
  vật.

Boundary này cho phép thay adapter local bằng Redis/distributed lease khi
Production mà không sửa lại handler authentication hoặc character selection.
Production vẫn phải bổ sung TTL, heartbeat, grace period và lease renewal.

## 5. Registration flow

Đã bổ sung:

- `RegistrationFlowService`.
- `IRegistrationTransactionStore`.
- `IRegistrationPortal`.
- `IGuestRegistrationConverter`.
- `InMemoryRegistrationTransactionStore` cho local.
- `DevelopmentRegistrationPortal` cho local.
- `BeginRegistrationRequest/Response`.
- Development completion packet được cấu hình riêng.
- Request ID chống duplicate request.
- PKCE verifier/challenge.
- Registration transaction có expiry.
- Authorization code dạng ngẫu nhiên, dùng một lần.
- Kiểm tra authorization code + PKCE + expiry trước khi consume.
- PKCE sai không làm mất transaction hợp lệ.
- Guest conversion sử dụng application boundary hiện có.
- Development completion bị cấm ngoài môi trường Development bằng validation
  cấu hình.

Production không được gửi password qua game socket. Web Account Service sẽ thay
Development portal/completion, trong khi Unity giữ luồng Begin → browser →
verified callback → Complete.

## 6. Unity UI

Đã bổ sung hoặc cập nhật:

- Authentication Entry panel/presenter.
- Loading panel và popup xác thực.
- Character Select Back flow.
- `GuestRegistrationPanel`.
- `GuestRegistrationPresenter`.
- Nút đăng ký chỉ dành cho guest trong `CharacterSelectView`.
- Form đăng ký gồm username, password và xác nhận password.
- Validation Presentation:
  - username tối thiểu ba ký tự;
  - password tối thiểu tám ký tự cho local scaffold;
  - password confirmation phải khớp.
- Hai trường password dùng chế độ Password và được xóa khi đóng/gửi form.
- Busy state và feedback từ server.
- Panel tự đóng và nút đăng ký biến mất khi guest chuyển thành registered
  account.
- Network client, packet handlers, events và VContainer registration phục vụ
  Begin/Development Complete.

### Việc Unity Editor còn phải hoàn tất

Scene `Bootstrap` cần được nối các serialized reference cho:

- `RegisterGuestButton`;
- `GuestRegistrationPanel`;
- username/password/confirm inputs;
- Register/Cancel buttons;
- MessageText;
- `CharacterSelectView.Register Guest Button`;
- `CharacterSelectView.Registration Panel`.

Đồ họa chính thức chưa cần đưa vào. Sau khi luồng hoạt động ổn định có thể thay
background, sprite button, font, character slot và animation mà không sửa logic.

## 7. Tài liệu kiến trúc

Đã tạo:

- `Documentation/Production/Registration_Flow.md`;
- `Documentation/Production/Account_And_Character_Flow_Cutover.md`.

Tài liệu cutover tổng hợp:

- login/resume và secure token storage;
- Active Account/Character Select;
- guest và registration;
- thành phần local-only phải thay;
- database/migration Production;
- Redis lease/heartbeat;
- TLS, rate limit, audit và Admin API;
- rollout Development → Staging → Alpha;
- release gate và ma trận kiểm thử.

## 8. Admin Management Contract

Authentication/registration đã được chuẩn bị boundary để Admin API bổ sung read
model và command mà không cho Admin Web truy cập trực tiếp database.

Read model dự kiến:

- account kind/status;
- verification status;
- registration transaction status;
- session/revocation history;
- Active Session/server;
- security event và rate-limit incident.

Admin không được xem password hash, raw refresh token, authorization code hoặc
verification secret. Command khóa/mở account và thu hồi session phải có
permission chi tiết, lý do, RequestId và audit append-only.

## 9. Kiểm tra đã thực hiện

- `dotnet test KnightServer.Tests/KnightServer.Tests.csproj --configuration Release`
- Kết quả: **18 passed, 0 failed, 0 skipped**.
- Các test registration xác nhận:
  - transaction có expiry và PKCE;
  - duplicate RequestId bị từ chối;
  - PKCE sai không consume transaction.
- Các test Active Account xác nhận:
  - owner hiện tại được giữ khi connection khác đến;
  - connection khác không thể release lease của owner.
- `git diff --check` không phát hiện lỗi định dạng trong các file code/tài liệu
  mới được kiểm tra.

Unity-generated `.csproj` không restore được ngoài Unity Editor và không trả
diagnostic C#. Việc compile/serialized reference phía client phải được xác nhận
trong Unity sau khi Editor refresh.

## 10. Database và migration

Phiên này không tạo schema local mới cho registration transaction vì adapter
đang là in-memory. Trước Production bắt buộc:

- tạo `registration_transactions`;
- email verification;
- refresh-token family/read model phù hợp;
- security events;
- transactional outbox;
- migration EF Core version hóa và kiểm thử trên Staging.

Không được đưa in-memory registration store vào Production.

## 11. Rủi ro và giới hạn còn lại

- Game transport local hiện chưa có TLS.
- Active Account local chưa có TTL/heartbeat và chưa hỗ trợ nhiều server process.
- Local account session store chưa phải Keychain/Keystore/Credential Manager.
- Chưa có Web Account Service và email verification.
- Chưa có PostgreSQL registration transaction implementation.
- Development completion trả code cho client để mô phỏng callback; tuyệt đối
  không bật ở Production.
- Chưa có integration test PostgreSQL cho toàn bộ guest conversion.
- Chưa manual test hoàn chỉnh hai client sau khi nối Registration UI trong scene.

## 12. Bước tiếp theo

1. Nối và kiểm tra serialized references của Registration UI trong
   `Bootstrap.unity`.
2. Test ba luồng end-to-end:
   - Chơi mới/guest và Character Select;
   - đăng nhập/Chơi tiếp và account conflict bằng hai client;
   - guest registration local và đăng nhập lại.
3. Bổ sung PostgreSQL integration test cho guest conversion.
4. Thiết kế TTL/heartbeat contract và Redis Active Account Lease.
5. Triển khai secure credential storage theo nền tảng Unity.
6. Sau khi luồng ổn định mới đưa đồ họa chính thức vào UI.

## 13. Rollback/forward-fix

- Có thể vô hiệu local registration bằng
  `registration.developmentCompletionEnabled = false`.
- Có thể rollback Presentation bằng cách bỏ panel/nút mới mà không ảnh hưởng
  registration/server contract.
- Không bật lại development authentication bypass trong cấu hình dùng để test
  luồng thật.
- Khi chuyển sang Production, ưu tiên forward-fix bằng adapter PostgreSQL/Redis
  tại composition root thay vì sửa handler hoặc UI flow.
