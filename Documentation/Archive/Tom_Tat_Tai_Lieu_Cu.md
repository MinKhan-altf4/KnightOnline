# Tóm tắt tài liệu lịch sử đã gom

Các ghi chép Day 2–14 và bản thiết kế Phase 1/Day 11 là tài liệu cũ, được viết
trước khi kiến trúc và luồng hiện tại ổn định. Để giảm trùng lặp, các file riêng
đã được gom vào đây; chúng không còn là yêu cầu triển khai.

## Tiến trình lịch sử ở mức khái quát

- Dự án bắt đầu với Unity client, .NET server và trao đổi packet client/server.
- Các bản kế hoạch đầu tiên đề xuất chia module, assembly, scene, EventBus,
  persistence và quy ước code. Các quy tắc còn giá trị đã được thay thế bằng
  [Quy tắc cốt lõi hiện hành](../KNIGHT_PROJECT_CORE_RULES.md).
- Các mốc sau đó phát triển luồng đăng nhập, nhân vật, thế giới và NPC; những
  quyết định, thay đổi kỹ thuật và kết quả kiểm tra gần đây hơn nằm trong
  [DevLogs](../DevLogs/README.md) và tài liệu kiến trúc hiện hành.
- Roadmap cũ có các phase tổng quát nhưng không phản ánh trạng thái repository
  hiện nay. Dùng [Tình trạng dự án](../Trang_Thai_Du_An.md) để chọn việc tiếp theo.

## Cách dùng

Chỉ đọc phần này khi cần bối cảnh lịch sử. Không lấy danh sách task cũ, tên
module cũ, sơ đồ cũ hoặc giả định về scene/database làm chỉ dẫn cho code hiện
tại. Hãy kiểm tra source, cấu hình, migration và test.
