# Máy chủ game KnightOnline

Máy chủ game viết bằng .NET 10, dùng PostgreSQL/EF Core và TCP. Cấu hình môi
trường hiện tại trong `serverSettings.json` là `Development`; server tự áp dụng
migration đang chờ khi khởi động.

## Yêu cầu

- .NET SDK 10.
- PostgreSQL đang chạy local.
- PowerShell hoặc terminal tương đương.

## Tạo database local

Từ thư mục gốc repository, tạo role và database bằng tài khoản PostgreSQL quản
trị:

```powershell
psql -U postgres -h localhost -f KnightServer/Database/bootstrap.sql
```

Script sẽ hỏi mật khẩu mới cho role `knightonline_app`. Hãy dùng mật khẩu riêng
cho ứng dụng; không đưa mật khẩu vào Git.

Lưu connection string bằng .NET User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:KnightOnline" "Host=localhost;Port=5432;Database=knightonline_dev;Username=knightonline_app;Password=MAT_KHAU_LOCAL" --project KnightServer
```

Trong CI hoặc môi trường triển khai, dùng biến môi trường
`KNIGHTONLINE_ConnectionStrings__KnightOnline` hoặc secret manager tương ứng.

## Khởi động

```powershell
dotnet restore KnightServer
dotnet run --project KnightServer
```

Server lắng nghe cổng `7777` theo `serverSettings.json`. Khi khởi động, migration
được áp dụng; lỗi cấu hình/database sẽ xuất hiện trong cửa sổ server.

## Kiểm thử

```powershell
dotnet test KnightServer.Tests/KnightServer.Tests.csproj -c Release
dotnet test KnightServer.IntegrationTests/KnightServer.IntegrationTests.csproj -c Release
```

Integration test dùng PostgreSQL thật và có thể tạo/xóa dữ liệu test. Chỉ chạy
với database Development dành riêng cho kiểm thử; không trỏ vào dữ liệu người
dùng thật.

## Cấu hình Development đáng chú ý

- Tối đa 3 nhân vật mỗi tài khoản/máy chủ.
- Tối đa 500 tài khoản active và 750 kết nối transport trong một tiến trình.
- Heartbeat 5 giây, lease TTL 20 giây, thời gian grace khi mất kết nối 10 giây.
- Development registration completion đang bật; server không vì vậy mà trở
  thành sẵn sàng Production.

Giá trị thực tế lấy từ `serverSettings.json` và phải được kiểm tra cùng
`KnightServer/Configuration/ServerOptions.cs`. Không coi các giới hạn trên là
cam kết tải Production; lease/capacity hiện không thay thế kiến trúc phân tán,
TLS, kho credential an toàn hoặc load test.
