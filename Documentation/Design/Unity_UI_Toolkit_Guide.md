# Hướng dẫn UI Toolkit trong InGame

Tài liệu này gộp hai hướng dẫn trước đây: **thẻ nhiệm vụ Làng Tân Thủ** và
**menu chức năng dưới màn hình**. Cả hai dùng chung một bộ UXML/USS và cùng
nguyên tắc: UI Toolkit chỉ trình bày; trạng thái map, nhiệm vụ và phiên vẫn
lấy từ event do luồng server phát ra.

## 1. File liên quan

| File | Vai trò |
|---|---|
| `KnightClient/Assets/_Project/Resources/UI/VillageHud.uxml` | Cấu trúc thẻ nhiệm vụ và menu (`function-menu-toggle`, `function-menu-panel`, `logout-button`); các `name` để C# truy vấn. |
| `KnightClient/Assets/_Project/Resources/UI/VillageHud.uss` | Màu sắc, khoảng cách, vị trí, trạng thái ẩn/hiện và bố cục màn hình hẹp. |
| `KnightClient/Assets/_Project/Resources/UI/VillageDefaultTheme.tss` | Nhập theme runtime mặc định của Unity để control và chữ có style nền. |
| `KnightClient/Assets/_Project/Scripts/UI/VillageUIToolkitHud.cs` | Tạo `UIDocument`, gắn Panel Settings, cập nhật thẻ nhiệm vụ theo event và nối callback menu. |
| `KnightClient/Assets/_Project/Scripts/UI/InGameMenuView.cs` | Bắt sự kiện nút, ẩn/hiện HUD và gọi `AuthenticationFlowService` để đăng xuất. |
| `KnightClient/Assets/_Project/Scripts/Root/InGame/InGameSceneRoot.cs` | Khởi tạo HUD Toolkit khi vào scene, truyền `InGameMenuView` đã được VContainer inject và ẩn thẻ nhiệm vụ uGUI cũ để tránh hai panel cùng hiện. |

## 2. Thẻ nhiệm vụ Làng Tân Thủ

Thẻ `NHẬT KÝ HÀNH TRÌNH` nằm ở góc trái dưới màn hình InGame.

- Nút `−` thu gọn phần chi tiết; `+` mở lại.
- Tiêu đề, mô tả và thanh tiến độ cập nhật theo `TutorialProgressChangedEvent`
  từ server.
- Tên khu vực cập nhật theo `MapTransitionedEvent` khi đi qua cổng.
- Để thêm trạng thái nhiệm vụ, sửa ánh xạ `StepId` trong `SetQuest`; không để
  USS hoặc UI tự quyết định tiến độ gameplay.

## 3. Menu chức năng

Thanh chức năng nằm sát giữa cạnh dưới màn hình. Khi mở, các chức năng xuất
hiện thành một hàng ngang phía trên nút mũi tên.

1. HUD tải UXML cùng Panel Settings có theme. `InGameMenuView` truy vấn control
   bằng đúng `name` trong UXML rồi đăng ký callback `clicked`.
2. Nút mũi tên luôn sát đáy. Khi đóng, hàng chức năng bị ẩn; HUD khác giữ nguyên
   trạng thái.
3. Khi nhấn **Đăng xuất**, presenter khóa nút và input di chuyển rồi gọi
   `LogoutToAuthenticationEntry()`. Thất bại thì mở khóa lại điều khiển; thành
   công thì chuyển về scene `Bootstrap`.
4. Các mục **Trang bị**, **Thú cưỡi** và **Bạn bè** hiện chỉ là giao diện minh
   họa bị vô hiệu hóa; chưa gọi nghiệp vụ nào.

## 4. Chỉnh giao diện

1. Sửa thứ tự, nhãn và cấu trúc element trong `VillageHud.uxml`.
2. Sửa màu, cỡ chữ, padding, margin, viền và vị trí trong `VillageHud.uss`
   (bằng UI Builder hoặc trình soạn thảo).
3. Giữ `name` trong UXML đồng bộ với truy vấn `root.Q<...>("...")` trong C#.
4. Nút mới có hành vi: đặt `name` ổn định trong UXML, rồi truy vấn và gắn
   callback trong `InGameMenuView.Initialize`. Chỉ gắn callback cho chức năng đã
   có application service tương ứng.
5. Xác thực, kết thúc phiên và mọi quy tắc nghiệp vụ nằm ở application service
   và server. UI chỉ gửi ý định và hiển thị kết quả.

## 5. Tạo một màn hình UI Toolkit mới

1. Tạo UXML bằng `Assets > Create > UI Toolkit > UI Document` hoặc mở UI Builder.
2. Tạo USS bằng `Assets > Create > UI Toolkit > Style Sheet` rồi gắn vào UXML.
3. Thêm `UIDocument` vào GameObject và gán UXML cùng Panel Settings.
4. Gán TSS theme cho Panel Settings. TSS mẫu nhập theme chuẩn bằng
   `@import url("unity-theme://default");`.
5. Lấy element theo `name` trong C#, đăng ký callback và cập nhật theo view
   model/event.

UI Toolkit có quy trình UXML/USS gần với làm web, nhưng USS chỉ là một tập con
thuộc tính của Unity, không phải toàn bộ CSS. Hiện UI Toolkit chưa thay thế các
HUD khác như HP/MP, bản đồ nhỏ hay bảng mục tiêu (vẫn dùng uGUI trong
`InGameHUD`).

## 6. Kiểm tra trong Unity

1. Mở `KnightClient` bằng phiên bản ghi trong
   `KnightClient/ProjectSettings/ProjectVersion.txt`, chờ import/compile xong.
2. Chạy từ scene `App` → đăng nhập → chọn nhân vật → vào Làng Tân Thủ.
3. Thẻ nhiệm vụ hiện góc trái dưới; nút mũi tên menu nằm giữa cạnh dưới và
   menu đang đóng; hai phần không chồng lên nhau.
4. Nói chuyện với Mẹ, đánh Wolf: tiến độ trên thẻ cập nhật đúng.
5. Mở/thu gọn menu: hàng chức năng hiện phía trên mũi tên, mũi tên vẫn sát đáy.
6. Đăng xuất thành công về `Bootstrap`; nếu server từ chối, nút và điều khiển
   di chuyển phải được mở lại.
7. Đi qua cổng: tên khu vực đổi theo map mới.
8. Kiểm tra Game view 16:9 và một tỉ lệ hẹp hơn. Panel Settings scale theo độ
   phân giải với reference 1920×1080; popup không được vượt khỏi màn hình.
