# The World of Knights & Demons — Nhật ký Phát triển
## Day 12: PostgreSQL Character Persistence

## Mục tiêu

Chuyển Character roster khỏi memory sang PostgreSQL mà không thay đổi flow phía
Unity Client.

## Đã hoàn thành

- Thêm EF Core 8.0.11, Npgsql provider 8.0.11 và User Secrets.
- Thêm local tool manifest cho `dotnet-ef` 8.0.11.
- Tạo `KnightDbContext`, `AccountEntity` và `CharacterEntity`.
- Tạo initial migration cho bảng `accounts` và `characters`.
- Tên character có normalized unique index để chống trùng không phân biệt
  hoa/thường.
- `CharacterId` chuyển sang PostgreSQL identity, không còn reset theo connection.
- `CreateCharacter` và `ListCharacters` dùng `CharacterRepository`.
- Giới hạn bốn character trên một account.
- Bắt PostgreSQL unique violation để trả về `NameAlreadyTaken`.
- Connection string lấy từ User Secrets hoặc environment variable.
- Thêm bootstrap script tạo `knightonline_app` và `knightonline_dev`.

## Development account seam

Packet `ConnectRequest` hiện chưa có account credential. Để hoàn thành persistence
vertical slice mà không giả lập authentication, mọi local connection tạm dùng
account key `local-dev`.

Repository đã query theo account, nên bước authentication sau này chỉ cần truyền
account identity đã xác thực thay cho constant này.

## Xác nhận

- Build KnightServer qua output riêng: 0 warnings, 0 errors.
- Migration generation thành công.
- Chưa apply migration vào PostgreSQL thật vì connection string/mật khẩu chỉ
  được cấu hình cục bộ bởi developer.

## Acceptance test

1. Bootstrap database và lưu connection string bằng User Secrets.
2. Apply migration.
3. Tạo character từ Unity.
4. Restart KnightServer.
5. Reconnect và xác nhận roster, CharacterId, Level không đổi.
