# DevLog — Core Rules compliance audit

## Mục tiêu và phạm vi

Quét một lượt toàn bộ repository theo `CORE-01…CORE-28`, sửa các vi phạm có thể
xử lý an toàn và ghi rõ blocker chưa thể hoàn tất trong một task.

Phân loại: Critical authentication, Authoritative gameplay và repository
infrastructure.

## Hoàn thành

- Sửa Git tracking cho Unity `.meta`, `.gitattributes`, .NET `bin/obj`.
- Bổ sung production guard cho insecure credential store và plaintext server.
- Bổ sung authentication validation, rate limit, timing mitigation.
- Bổ sung refresh-token family, reuse detection và migration.
- Bổ sung packet access enforcement tại dispatcher.
- Inject server clock vào authentication, persistence và gameplay.
- Observe background task và ghi log send failure.
- Thêm server test project và GitHub Actions CI.
- Tạo báo cáo compliance chi tiết.

## Files/Modules Changed

- Repository: `.gitignore`, `.gitattributes`, `.github/workflows/server-ci.yml`.
- Server: Accounts, Configuration, Networking, Persistence, Time, Program.
- Shared/Data/Network client authentication result contracts.
- Client development credential store/authentication flow.
- `KnightServer.Tests`.
- EF migrations và model snapshot.
- Unity `.meta` files dưới `KnightClient/Assets`.
- Documentation compliance và DevLog.

## Kiến trúc và quyết định kỹ thuật

- Production bị chặn cho đến khi có TLS và secure credential store thật.
- Packet handler mặc định yêu cầu authenticated; Anonymous phải khai báo rõ.
- Refresh-token rotation dùng family và conditional claim trong transaction.
- Token reuse thu hồi toàn family.
- Clock là dependency của server-authoritative logic.
- Rate limiter hiện là lớp Development single-server; multi-server cần
  distributed implementation.

## Test/Build

- `dotnet build KnightServer/KnightServer.csproj --configuration Release`:
  pass, 0 warning/error.
- `dotnet test KnightServer.Tests/KnightServer.Tests.csproj`: 13 pass, 0 fail.
- EF idempotent migration script: tạo thành công.
- 195 Unity meta: có GUID hợp lệ, 0 duplicate.
- Secret/boundary/static pattern scan: không phát hiện vi phạm đã liệt kê.
- Unity generated csproj CLI build: thất bại không có diagnostic; phải kiểm tra
  compile trong Unity Editor.

## Migration

- Thêm migration `RefreshTokenReuseDetection`.
- Legacy session được backfill `family_id = id`.
- Sau backfill mới áp dụng `NOT NULL` và index.
- Forward-fix: nếu migration thất bại, giữ server Development dừng, sửa
  migration và chạy lại trước khi mở listener.

## Rủi ro và technical debt

Xem:

- `Documentation/Compliance/2026-07-25-core-rules-audit.md`.

Blocker chính: TLS, secure platform store, heartbeat/lease, distributed session,
audit/outbox, DB integration tests và web registration callback.

## Bước tiếp theo

1. Mở Unity, xác nhận compile và chạy manual smoke test.
2. Commit/push toàn bộ `.meta` cùng thay đổi audit.
3. Triển khai heartbeat/session lease.
4. Triển khai TLS và secure credential store theo nền tảng.
5. Thêm PostgreSQL integration test cho authentication transaction.
