# KnightOnline

> Một game nhập vai trực tuyến nhiều người chơi, xây dựng bằng Unity và .NET.

## Bắt đầu

- [Tình trạng dự án và việc nên làm tiếp](Documentation/Trang_Thai_Du_An.md)
- [Cổng tài liệu và thứ tự đọc](Documentation/README.md)
- [Quy tắc cốt lõi bắt buộc](Documentation/KNIGHT_PROJECT_CORE_RULES.md)

## Thành phần chính

- `KnightClient/`: client Unity.
- `KnightServer/`: máy chủ game .NET.
- `KnightServer.Tests/`: kiểm thử đơn vị máy chủ.
- `KnightServer.IntegrationTests/`: kiểm thử tích hợp với PostgreSQL và mạng.
- `Documentation/`: kiến trúc, thiết kế, vận hành và lịch sử thay đổi.

## Chạy luồng Unity

Mở dự án `KnightClient` trong Unity và chạy từ scene `App`. Scene này khởi tạo
các dịch vụ dùng chung rồi nạp `Bootstrap`; không chạy riêng `Bootstrap` nếu cần
kiểm tra đầy đủ luồng đăng nhập và chọn nhân vật.

> Tài liệu trạng thái có ngày đối chiếu. Trước khi xem một tính năng là đã hoàn
> tất, hãy kiểm tra source, cấu hình, migration, test và xác nhận lại trong Unity
> nếu tính năng có giao diện hoặc phụ thuộc runtime.
