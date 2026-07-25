# LUỒNG ĐĂNG KÝ TÀI KHOẢN KHI MỞ SERVER THẬT

## 1. Mục tiêu và phạm vi

Tài liệu này là hợp đồng kiến trúc cho việc chuyển tài khoản guest thành tài
khoản đăng ký. Không có hệ thống “bảo mật tuyệt đối”; mục tiêu bắt buộc là phòng
thủ nhiều lớp, giới hạn thiệt hại, thu hồi được credential và truy vết được sự
cố.

Luồng phải giữ nguyên khi chuyển từ local sang Production. Chỉ adapter cho Web
Account Service, persistence, email, secret store và callback được thay thế.

## 2. Luồng chuẩn

1. Người chơi đang ở tài khoản guest chọn **Đăng ký**.
2. Unity sinh `RequestId` và PKCE `verifier/challenge`.
3. Unity gửi `BeginRegistrationRequest` gồm guest refresh token, device ID,
   request ID và PKCE challenge.
4. Game Server kiểm tra guest, rate limit và tạo `RegistrationTransaction` có
   hạn dùng 10–15 phút.
5. Server trả URL HTTPS của Web Account Service. URL chỉ chứa transaction ID;
   không chứa password, refresh token hoặc authorization code.
6. Unity mở URL bằng trình duyệt hệ thống.
7. Web Account Service nhận username, email, password, CAPTCHA/risk challenge
   nếu cần và xác minh email.
8. Password được hash bằng thuật toán chuẩn có salt/work factor. Password và raw
   token không được ghi log.
9. Web Account Service hoàn tất transaction và phát authorization code ngẫu
   nhiên, dùng một lần, hạn ngắn.
10. Callback về ứng dụng dùng Android App Links/iOS Universal Links đã xác minh.
11. Unity kiểm tra `state`, gửi authorization code cùng PKCE verifier.
12. Server tiêu thụ code nguyên tử, chuyển guest thành account trong một database
    transaction, thu hồi guest token và cấp refresh-token family mới.
13. Unity lưu refresh token bằng Keychain/Keystore/Credential Manager, xóa
    password/verifier/code khỏi RAM và quay về Entry với nút **Chơi tiếp**.

## 3. Invariant bắt buộc

- Mỗi transaction có `Id`, `RequestId`, expiry UTC và trạng thái consumed.
- Authorization code và email verification token chỉ lưu dạng hash.
- Code chỉ dùng một lần; kiểm tra code + PKCE + expiry và consume phải nguyên tử.
- Duplicate/replay không được chuyển đổi account hoặc cấp token lần hai.
- Unique constraint của database bảo vệ username/email chuẩn hóa.
- Guest conversion giữ nguyên ownership của character; lỗi giữa chừng phải
  rollback toàn bộ.
- Không gọi email/dịch vụ ngoài trong database transaction; dùng outbox.
- Client không quyết định account ID, trạng thái xác minh hoặc kết quả conversion.
- Production không nhận password qua game socket.
- Login, registration và recovery có rate limit riêng theo account/IP/device.

## 4. Ranh giới thay thế

| Boundary | Local test | Production |
|---|---|---|
| `IRegistrationTransactionStore` | In-memory | PostgreSQL, unique RequestId, atomic consume |
| `IRegistrationPortal` | URL localhost | Web Account Service HTTPS |
| Completion | Development-only packet | Verified web callback + one-time code |
| Token storage Unity | Development store | OS secure credential storage |
| Delivery | Không gửi email | Email provider qua outbox |
| Secrets | Development config | Vault/managed identity |

Packet `CompleteDevelopmentRegistrationRequest` chỉ là bộ mô phỏng local và
**PHẢI bị vô hiệu ngoài Development**. Production dùng callback của Web Account
Service; Unity không gửi username/password bằng packet này.

## 5. Dữ liệu Production

Production cần các bảng versioned migration:

- `registration_transactions`;
- `email_verifications`;
- `refresh_sessions`/`refresh_token_families`;
- `security_events`;
- transactional `outbox_messages`.

`registration_transactions` tối thiểu có transaction ID, hashed guest identity,
hashed state/code, PKCE challenge, RequestId unique, created/expiry/consumed UTC,
version và correlation ID. Raw guest token, raw authorization code và password
không được lưu.

## 6. Admin-ready

Admin Web chỉ truy cập qua Admin API. Read model được phép hiển thị trạng thái,
thời gian, kết quả, server và correlation ID; phải che email/device/IP. Admin
không được xem password hash, raw token hoặc tự đánh dấu verification thành công.
Command thu hồi session/khóa account phải có permission chi tiết, lý do,
RequestId và audit append-only.

## 7. Điều kiện trước Production

- Thay in-memory transaction store bằng PostgreSQL implementation.
- Tắt development completion bằng cấu hình và kiểm tra fail-fast.
- Bật TLS cho game transport và HTTPS cho Web Account Service.
- Tích hợp email verification, outbox worker và retry/dead-letter.
- Dùng secure storage thật trên từng nền tảng Unity.
- Thêm refresh-token rotation/reuse alert, MFA/passkey cho Admin.
- Chạy integration test: duplicate, replay, expiry, callback giả, race condition,
  disconnect, rollback, token theft và restore database.
- Security review, dependency/secret scan, staging load test và runbook sự cố.

## 8. Cách test local hiện tại

Local dùng `InMemoryRegistrationTransactionStore`,
`DevelopmentRegistrationPortal` và development completion packet. Test tự động
xác nhận PKCE transaction, expiry, duplicate RequestId và việc PKCE sai không làm
mất transaction. Đây là scaffold để kiểm tra contract; UI đăng ký và website
localhost sẽ được nối ở bước tiếp theo.
