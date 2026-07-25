# Core Rules Compliance Audit — 2026-07-25

## Phạm vi

Audit source, configuration, Git tracking và build/test hiện có của:

- Unity client;
- .NET game server;
- PostgreSQL migrations;
- authentication/session foundation;
- repository workflow.

Đây là audit source-level một lượt, không phải chứng nhận Production.

## Đã sửa trong lượt audit

| Quy tắc | Phát hiện | Xử lý |
|---|---|---|
| CORE-22 | 146 Unity `.meta` bị ignore | Bỏ rule ignore, xác nhận 195/195 meta có GUID hợp lệ và không trùng |
| CORE-23 | 31 file `bin/obj` được track | Bỏ khỏi Git index, giữ nguyên file local và tiếp tục ignore |
| CORE-23 | Unity `.gitattributes` định nghĩa macro ngoài root | Chuyển `.gitattributes` lên repository root |
| CORE-05/27 | Raw refresh token lưu bằng PlayerPrefs có thể vào release | Thêm compile guard: chỉ Editor/Development Build được dùng adapter alpha |
| CORE-05/12 | Authentication input không giới hạn trước PBKDF2/query | Thêm `AuthenticationInputPolicy` và validation application-level |
| CORE-05 | Refresh token không có family/reuse detection | Thêm token family, rotation claim nguyên tử và revoke family khi reuse |
| CORE-05 | Login có timing khác nhau khi username không tồn tại | Thêm dummy password verification |
| CORE-05 | Authentication thiếu rate limit | Thêm rate limiter theo IP/operation và config |
| CORE-12 | Handler mới có thể quên authentication | Thêm `PacketAccessLevel` mặc định và enforcement tại dispatcher |
| CORE-18 | Broadcast/forced disconnect nuốt exception | Ghi warning không chứa credential |
| CORE-18/19 | Background task server không được observe | Thêm fault observer cho connection và respawn loop |
| CORE-20 | Critical/gameplay code gọi trực tiếp `DateTime.UtcNow` | Inject `IServerClock` từ composition root |
| CORE-16 | Legacy refresh session có thể chung `Guid.Empty` family | Migration backfill `family_id = id` trước khi đặt NOT NULL |
| CORE-24 | Không có server test project | Thêm xUnit foundation và 13 unit tests |
| CORE-23 | Không có CI | Thêm GitHub Actions build/test cho server và shared contracts |
| CORE-27 | Plaintext/dev auth có thể bị hiểu là Production-ready | Server chặn mọi environment ngoài Development cho đến khi có TLS |

## Kết quả kiểm tra

- Server Release build: pass, 0 warning, 0 error.
- Server unit tests: 13 pass, 0 fail.
- EF idempotent migration script: tạo thành công.
- Secret pattern scan cơ bản: không phát hiện private key, credential URI hoặc
  access key phổ biến.
- Unity meta: 195 file hợp lệ, không có GUID trùng.
- Gameplay/UI/Root protocol boundary scan: không phát hiện import
  `KnightOnline.Client.Shared`.
- Unity generated `.csproj` không build được bằng `dotnet build` và không trả
  diagnostic; cần xác nhận compile trong Unity Editor.

## Blocker trước Alpha internet/public

### Critical

1. Chưa có TLS cho game transport.
2. Chưa có secure credential store thật cho Windows/Android/iOS.
3. Chưa có heartbeat/session lease; disconnect hiện còn phụ thuộc socket close.
4. `AccountSessionRegistry`, `ActivePlayerRegistry` và rate limiter đang
   in-memory, chưa dùng distributed store cho multi-server.
5. Chưa có append-only audit store và transactional Outbox.
6. Chưa có integration test PostgreSQL cho Guest conversion, concurrent login,
   token rotation/reuse và rollback.
7. Web registration/deep-link state/nonce chưa được triển khai.

Server hiện cố ý không khởi động ở Staging/Production để ngăn phát hành nhầm
khi các blocker trên chưa hoàn thành.

### High

1. Các gameplay command chưa có RequestId/idempotency framework chung.
2. Logging còn dùng `Console.WriteLine`, chưa phải structured logging.
3. Packet/API compatibility chưa có version policy đầy đủ.
4. Chưa có health/readiness endpoint, metrics, alert và runbook.
5. Chưa có environment-specific client config/build profile.
6. Guest level cap và disabled-feature policy mới có config, chưa enforcement.

### Repository operations

1. Cần bật branch protection và required `Server CI` trên GitHub.
2. Cần bật GitHub secret scanning/dependency alerts.
3. Cần xác nhận Git LFS trên máy CI/team cho binary Unity.
4. Các Unity `.meta` mới phải được commit cùng checkpoint này.

## Kết luận

Codebase đã an toàn và nhất quán hơn cho Development, nhưng **chưa đạt điều kiện
Production**. Production được chặn có chủ đích. Bước ưu tiên tiếp theo là
heartbeat/session lease, sau đó TLS và secure platform credential store trước
khi hoàn thiện UI đăng nhập.
