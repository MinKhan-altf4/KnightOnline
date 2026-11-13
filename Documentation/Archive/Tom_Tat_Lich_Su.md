# Tóm tắt lịch sử dự án (trước tháng 08-2026)

File này gom các tài liệu lịch sử trước đây (ghi chép Day 2–14, bản thiết kế
Phase 1/Day 11, DevLog và audit ngày 25–26-07-2026). Nội dung chỉ để hiểu bối
cảnh, **không phải yêu cầu triển khai**. Quy tắc còn hiệu lực nằm trong
[Quy tắc cốt lõi](../KNIGHT_PROJECT_CORE_RULES.md).

## 1. Giai đoạn khởi đầu (Day 2–14)

- Dự án bắt đầu với client Unity, server .NET và trao đổi packet client/server.
- Các bản kế hoạch đầu đề xuất chia module, assembly, scene, EventBus,
  persistence và quy ước code. Phần còn giá trị đã chuyển vào quy tắc cốt lõi.
- Các mốc sau đó phát triển đăng nhập, nhân vật, thế giới và NPC.
- Roadmap cũ có các phase tổng quát nhưng không phản ánh repository hiện nay.

Không lấy danh sách task, tên module, sơ đồ hoặc giả định cũ về scene/database
làm chỉ dẫn cho code hiện tại.

## 2. Giai đoạn củng cố (25–26-07-2026)

### Thay đổi nền tảng

- Chuẩn hóa bộ quy tắc từ 38 mục thành 28 quy tắc có mã ổn định; bổ sung phân
  loại rủi ro, session lease, xoay vòng token, idempotency, Outbox, tương thích,
  quản trị Admin và điều kiện hoàn tất.
- Củng cố bảo mật đăng nhập: kiểm tra đầu vào, giới hạn tần suất, giảm rò rỉ
  thời gian so khớp mật khẩu, họ token và phát hiện dùng lại token; chặn
  Production khi chưa có TLS và kho thông tin xác thực an toàn.
- Thêm quyền truy cập mặc định cho packet handler, tiêm clock phía server, theo
  dõi lỗi tác vụ nền và các test server đầu tiên.
- Dọn Git cho file `.meta` của Unity, loại `bin/obj` khỏi index và thêm CI cho
  server và contract dùng chung.
- Dựng giao diện Entry/Loading/Registration, Presenter tách khỏi service; mật
  khẩu chỉ giữ tạm trong bộ nhớ và không ghi log.
- Đặt nền khách (guest)/đăng nhập/tiếp tục phiên, refresh token và chọn nhân
  vật; tối đa ba nhân vật mỗi tài khoản/máy chủ. Adapter đăng ký khi đó chỉ
  dùng cho Development.
- Quy tắc hết giờ 15 giây ở màn chọn nhân vật từng được đề xuất rồi bị bỏ;
  không khôi phục chỉ dựa trên log tháng 7.

### Kết quả kiểm tra ngày 25-07-2026

- Build Release server: đạt, 0 cảnh báo/lỗi.
- Unit test server: 13/13 đạt.
- Tạo được script migration EF idempotent.
- Không thấy secret phổ biến; 195 file `.meta` có GUID hợp lệ, không trùng.
- Project do Unity sinh chưa kiểm chứng được bằng `dotnet build`; cần compile
  trong Unity Editor.

### Giới hạn khi đó

Production bị chặn có chủ đích. Còn thiếu: TLS, kho token an toàn, lease có
heartbeat/TTL và phân tán, integration test PostgreSQL, đăng ký Web với
state/nonce, audit/Outbox, log có cấu trúc, quan sát vận hành và chính sách
tương thích protocol.

## 3. Đọc tiếp

Các mục trên đã được phát triển thêm ở các phiên sau. Xem
[DevLogs](../DevLogs/README.md), [mốc rà soát 04-08-2026](../Audits/2026-08-04-project-development-checkpoint.md),
[đánh giá 13-11-2026](../Audits/2026-11-13-danh-gia-luong-va-huong-di.md) và
kiểm tra code hiện tại trước khi kết luận.
