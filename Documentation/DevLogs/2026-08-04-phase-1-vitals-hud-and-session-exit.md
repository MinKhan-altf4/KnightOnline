# DevLog — Sinh lực, HUD và đăng xuất

Ngày: 04-08-2026
Phạm vi: trạng thái nhân vật, HUD, menu chức năng, đăng xuất và vòng đời scene.

## Kết quả

- Bổ sung snapshot sinh lực có thứ tự và event Unity; `PlayerSession` sở hữu
  thao tác sát thương, hồi HP/MP và tiêu hao MP.
- Làm rõ HUD HP/MP, cấp độ/EXP và minimap; bỏ nhãn debug tên/kết nối/toạ độ.
- Thêm menu chức năng giữa cạnh dưới. Đăng xuất được bật; các mục thú cưỡi,
  trang bị, bạn bè và ô mở rộng vẫn là chỗ giữ chỗ.
- Trước khi gửi đăng xuất, client ngừng phát input di chuyển.
- Đăng xuất giải phóng player/session và đặt lease vào thời gian chờ do server
  cấu hình (mặc định 10 giây). Server trả thời gian còn lại; Unity chỉ hiển thị.
- Quay lại từ Character Select chỉ điều hướng về Entry, không đăng xuất hoặc
  tạo cooldown mới. Chỉ lệnh Đăng xuất trong InGame đóng phiên.
- Củng cố EventBus khi chuyển scene đồng bộ; callback của view đã bị hủy không
  còn được gọi. Lỗi callback packet không còn bị hiểu nhầm là lỗi đọc socket.

## Quyết định và tương thích

- Đồng hồ và lease phía server quyết định cooldown; giao diện chỉ trình bày.
- Điều hướng về Entry và đăng xuất là hai lệnh riêng vì khác ngữ nghĩa phiên.
- UI không phụ thuộc assembly Root hoặc truy cập trực tiếp `GameSession`.
- Response authentication thêm trường retry-after tương thích cộng thêm trong
  JSON hiện tại; vẫn cần thương lượng phiên bản trước rollout hỗn hợp.
- Không có migration database trong phiên.

## Kiểm tra tại phiên

- `dotnet test KnightServer.Tests -c Release --no-restore`: 63/63 đạt.
- `dotnet test KnightServer.IntegrationTests -c Release --no-restore`: 15/15 đạt.
- `dotnet build KnightServer -c Release --no-restore`: 0 cảnh báo, 0 lỗi.
- Unity đã compile và người dùng kiểm tra luồng trước đó; thay đổi cuối
  Character Select → Back → Continue vẫn cần smoke test.

## Việc còn lại

Chạy lại luồng đăng xuất → thử Continue trong cooldown → Character Select → Back
→ Continue; xác nhận không có callback view đã hủy hoặc packet di chuyển bị từ
chối. Menu placeholder, responsive/safe area và lease phân tán vẫn chưa hoàn
thiện. Các kết quả test trên chỉ đúng tại ngày ghi log.
