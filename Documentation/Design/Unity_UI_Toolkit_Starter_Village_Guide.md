# Hướng dẫn UI Toolkit qua HUD Làng Tân Thủ

Mẫu này đặt một thẻ nhiệm vụ ở góc trái dưới màn hình InGame. Nó dùng UI
Toolkit cho phần trình bày, còn trạng thái map và nhiệm vụ vẫn lấy từ event do
luồng server hiện tại phát ra.

## Cấu trúc mẫu

- `KnightClient/Assets/_Project/Resources/UI/VillageHud.uxml`: cấu trúc thẻ,
  các tên element để C# truy vấn.
- `KnightClient/Assets/_Project/Resources/UI/VillageHud.uss`: màu sắc, khoảng
  cách, bố cục và trạng thái responsive.
- `KnightClient/Assets/_Project/Resources/UI/VillageDefaultTheme.tss`: nhập
  theme runtime mặc định của Unity để các control và text Toolkit có style nền.
- `KnightClient/Assets/_Project/Scripts/UI/VillageUIToolkitHud.cs`: tạo
  `UIDocument`, gắn Panel Settings và cập nhật nội dung theo event.
- `InGameSceneRoot`: khởi tạo mẫu khi vào game và ẩn quest card uGUI cũ để
  tránh hai panel cùng hiển thị.

## Cách thử trong Unity

1. Mở `KnightClient` bằng phiên bản Unity ghi trong
   `ProjectSettings/ProjectVersion.txt` và chờ import/compile xong.
2. Chạy luồng bình thường qua Entry, chọn nhân vật rồi vào InGame ở Làng Tân
   Thủ.
3. Thẻ `NHẬT KÝ HÀNH TRÌNH` xuất hiện góc trái dưới. Nút `−` thu gọn phần chi
   tiết; `+` mở lại.
4. Nói chuyện với Mẹ và tiến hành nhiệm vụ. Tiêu đề, mô tả và thanh tiến độ cập
   nhật theo `TutorialProgressChangedEvent` từ server.
5. Đi qua cổng để đổi map. Tên khu vực cập nhật theo
   `MapTransitionedEvent`.

## Thử chỉnh như làm web

1. Mở `VillageHud.uxml` để sửa thứ tự và cấu trúc element.
2. Mở `VillageHud.uss` bằng UI Builder hoặc text editor để sửa màu, font-size,
   padding, margin, border và vị trí.
3. Trong `VillageUIToolkitHud.cs`, giữ các `name` trong UXML đồng bộ với các
   truy vấn `root.Q<...>("...")`.
4. Để thêm trạng thái nhiệm vụ, sửa ánh xạ `StepId` trong `SetQuest`; không để
   USS hoặc UI tự quyết định tiến độ gameplay.
5. Thử ít nhất Game view 16:9 và một tỉ lệ hẹp hơn. Panel Settings dùng scale
   theo độ phân giải với reference 1920×1080; USS giới hạn chiều rộng thẻ theo
   kích thước panel.

## Cách tạo UI Toolkit mới

1. Tạo UXML bằng `Assets > Create > UI Toolkit > UI Document` hoặc mở UI Builder.
2. Tạo USS bằng `Assets > Create > UI Toolkit > Style Sheet` rồi gắn vào UXML.
3. Thêm `UIDocument` vào GameObject và gán UXML cùng Panel Settings.
4. Gán một TSS theme cho Panel Settings. TSS của mẫu nhập runtime theme chuẩn
   bằng `@import url("unity-theme://default");`.
5. Để C# lấy element theo `name`, đăng ký callback và cập nhật view model/event.
6. Giữ domain rule và dữ liệu authoritative ở application/server boundary; UI
   chỉ hiển thị state và phát ý định người chơi.

UI Toolkit cung cấp quy trình UXML/USS gần với cách làm web, nhưng USS là một
tập thuộc tính theo Unity chứ không phải toàn bộ CSS. Mẫu này chưa thay thế các
HUD khác như HP/MP, minimap, target panel hoặc menu.
