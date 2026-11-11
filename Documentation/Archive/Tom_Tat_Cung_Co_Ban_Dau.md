# Tóm tắt giai đoạn củng cố ban đầu — tháng 07-2026

Đây là bản gom các DevLog ngày 25–26-07-2026 và audit quy tắc cùng thời điểm.
Chi tiết từng thao tác nhỏ đã được lược bỏ; quy tắc còn hiệu lực nằm trong
[Quy tắc cốt lõi hiện hành](../KNIGHT_PROJECT_CORE_RULES.md).

## Những thay đổi nền tảng được ghi nhận

- Chuẩn hóa bộ quy tắc từ 38 mục thành 28 quy tắc có mã ổn định; bổ sung phân
  loại rủi ro, session lease, token rotation, idempotency, Outbox, tương thích,
  quản trị Admin và điều kiện hoàn tất.
- Củng cố bảo mật đăng nhập: validation input, rate limit, password timing
  mitigation, token family/reuse detection và chặn Production khi chưa có TLS,
  kho thông tin xác thực an toàn.
- Thêm quyền truy cập mặc định cho packet handler, tiêm clock phía server,
  theo dõi lỗi background task và kiểm thử máy chủ đầu tiên.
- Dọn Git tracking cho Unity `.meta`, loại `bin/obj` khỏi index và thêm CI cho
  server/shared contract.
- Dựng giao diện Entry/Loading/Registration trong Unity, với Presenter tách
  khỏi service; password chỉ giữ tạm trong bộ nhớ và không được ghi log.
- Đặt nền guest/đăng nhập/tiếp tục, refresh token và chọn nhân vật; giới hạn
  ba nhân vật mỗi tài khoản/máy chủ. Adapter đăng ký khi đó chỉ dành cho
  Development.
- Quy tắc timeout Character Select 15 giây từng xuất hiện trong đề xuất/luồng
  ban đầu nhưng đã bị bỏ sau đó; không khôi phục chỉ dựa trên log tháng 7.

## Kết quả kiểm tra tại audit 25-07-2026

- Build Release máy chủ: đạt, 0 cảnh báo/lỗi.
- Unit test máy chủ: 13/13 đạt.
- Tạo được script migration EF idempotent.
- Kiểm tra cơ bản không thấy secret phổ biến; 195 Unity `.meta` có GUID hợp lệ
  và không trùng.
- Unity-generated project chưa thể xác nhận bằng `dotnet build`; compile cần
  kiểm tra trong Unity Editor.

## Những giới hạn khi đó

Production bị chặn có chủ đích. Các mục còn thiếu gồm TLS, kho token an toàn,
lease có heartbeat/TTL và phân tán, integration test PostgreSQL, đăng ký Web
với state/nonce, audit/Outbox, structured logging, quan sát vận hành và chính
sách tương thích protocol.

Đây là snapshot cũ; các mục trên đã được phát triển thêm ở các phiên sau. Đọc
[DevLogs gần nhất](../DevLogs/README.md), [mốc rà soát 04-08-2026](../Audits/2026-08-04-project-development-checkpoint.md)
và kiểm tra code hiện tại trước khi kết luận.
