# Hướng dẫn menu chức năng bằng Unity UI Toolkit

Tài liệu này mô tả thanh chức năng nằm sát cạnh dưới màn hình InGame. Mũi tên
hiện có được thay bằng nút UI Toolkit; khi mở, các chức năng xuất hiện thành một
hàng ngang phía trên nút. Mục Đăng xuất dùng lại luồng kết thúc phiên hiện có.

## File liên quan

- `KnightClient/Assets/_Project/Resources/UI/VillageHud.uxml`: cấu trúc menu,
  gồm `function-menu-toggle`, `function-menu-panel` và `logout-button`.
- `KnightClient/Assets/_Project/Resources/UI/VillageHud.uss`: vị trí, kích thước,
  màu sắc, trạng thái ẩn/hiện và bố cục màn hình hẹp.
- `KnightClient/Assets/_Project/Scripts/UI/VillageUIToolkitHud.cs`: tạo
  `UIDocument`, truy vấn quest card và nối callback thay đổi trạng thái menu.
- `KnightClient/Assets/_Project/Scripts/UI/InGameMenuView.cs`: bắt sự kiện nút,
  ẩn/hiện HUD và gọi `AuthenticationFlowService` để đăng xuất.
- `KnightClient/Assets/_Project/Scripts/Root/InGame/InGameSceneRoot.cs`: nối
  HUD Toolkit với presenter menu đã được VContainer inject.

## Cách menu hoạt động

1. `InGameSceneRoot` tạo `VillageUIToolkitHud` khi vào scene và truyền
   `InGameMenuView` vào hàm khởi tạo.
2. HUD tải UXML cùng Panel Settings có theme. `InGameMenuView` truy vấn các
   control bằng đúng `name` trong UXML rồi đăng ký callback `clicked`.
3. Thanh menu luôn giữ nút mũi tên sát đáy. Khi đóng, hàng chức năng được ẩn;
   khi mở, hàng chức năng bung ngang phía trên nút. HUD khác giữ nguyên trạng thái.
4. Khi nhấn Đăng xuất, presenter khóa nút và input di chuyển, sau đó gọi
   `LogoutToAuthenticationEntry()`. Kết quả thất bại mở khóa lại điều khiển;
   thành công chuyển về scene `Bootstrap`.

## Chỉnh giao diện

- Sửa thứ tự, nhãn và thêm Button trong `VillageHud.uxml`.
- Sửa màu sắc, khoảng cách, kích thước và trạng thái responsive trong
  `VillageHud.uss`.
- Nếu thêm nút có hành vi, đặt `name` ổn định trong UXML rồi truy vấn và gắn
  callback trong `InGameMenuView.Initialize`. Chỉ thêm callback cho chức năng
  đã có application/service tương ứng.
- Các mục Trang bị, Thú cưỡi và Bạn bè hiện là giao diện minh họa bị vô hiệu hóa;
  chúng chưa gọi nghiệp vụ nào.
- Giữ việc xác thực/kết thúc phiên trong application/service và server boundary.
  UI chỉ gửi ý định và hiển thị kết quả.

## Kiểm tra trong Unity

1. Mở project bằng phiên bản ghi trong `KnightClient/ProjectSettings/ProjectVersion.txt`
   và chờ Unity import, compile hoàn tất.
2. Vào game tại Làng Tân Thủ. Xác nhận mũi tên hiện ở cạnh dưới chính giữa và
   popup đang đóng.
3. Mở thanh menu: xác nhận các mục chức năng nằm thành một hàng ngang sát đáy,
   phía trên nút mũi tên.
4. Thu gọn thanh menu và xác nhận nút mũi tên vẫn nằm sát viền dưới.
5. Kiểm tra đăng xuất thành công về `Bootstrap`; nếu server từ chối, nút và
   điều khiển di chuyển phải được mở lại.
6. Kiểm tra Game view ở 16:9 và cửa sổ hẹp để xác nhận popup không vượt khỏi màn hình.
