# DevLog — Unity Authentication Entry UI

## Mục tiêu và phạm vi

Triển khai lớp Presentation cho luồng chơi mới, đăng nhập tài khoản đã có và
loading kiểm tra session trong Unity.

Phân loại: Presentation.

## Công việc hoàn thành

- Thêm `AuthenticationEntryPanel` quản lý serialized references và trạng thái UI.
- Thêm `AuthenticationEntryPresenter` điều phối intent UI qua
  `AuthenticationFlowService`.
- Thêm `AuthenticationLoadingPanel` hiển thị loading, đếm ngược và spinner từ
  `AuthenticationLoadingEvent`.
- Đăng ký Presenter và Loading Panel trong `GameLifetimeScope`.
- Thêm validation cảnh báo reference còn thiếu trong Unity Editor.
- Thêm đầy đủ file `.meta` cho Unity assets mới.
- Dựng và nối Authentication Entry/Loading UI trong scene `Bootstrap`.

## Kiến trúc và quyết định kỹ thuật

- View không phụ thuộc Network hoặc packet protocol.
- Presenter không chứa logic xác thực; server và `AuthenticationFlowService` vẫn
  quyết định kết quả.
- Button listener được quản lý trong code để tránh phụ thuộc UnityEvent cấu hình
  thủ công.
- Password chỉ được chuyển thẳng tới authentication service, không được log hoặc
  lưu trong View.
- Nút Server hiện chỉ báo trạng thái chờ phát triển. Chưa tạo contract giả khi
  server-list protocol chưa được thiết kế.

## Files Changed

- `KnightClient/Assets/_Project/Scripts/UI/AuthenticationEntryPanel.cs`
- `KnightClient/Assets/_Project/Scripts/UI/AuthenticationEntryPresenter.cs`
- `KnightClient/Assets/_Project/Scripts/UI/AuthenticationLoadingPanel.cs`
- Các file `.meta` tương ứng.
- `KnightClient/Assets/_Project/Scripts/Root/Bootstrap/GameLifetimeScope.cs`
- `KnightClient/Assets/_Project/Scenes/Bootstrap.unity`
- TMP fallback asset do Unity cập nhật khi dựng giao diện.

## Kiểm tra

- Kiểm tra định dạng bằng `git diff --check`.
- Kiểm tra namespace, dependency direction và Unity serialized references.
- Unity Editor compile thành công, không có lỗi.
- Panel đã hiển thị trong Play Mode.
- Cần tiếp tục manual test các request authentication với server.

## Config, migration và compatibility

- Không có database migration hoặc protocol change.
- Tốc độ spinner được cấu hình bằng Inspector.
- Thời lượng 5/10 giây tiếp tục lấy từ `ClientAuthenticationSettings`, không
  hardcode trong UI.

## Admin Management Contract

Không áp dụng: thay đổi chỉ thuộc Presentation, không thêm server/domain state
hoặc command quản trị.

## Rủi ro và bước tiếp theo

- Chưa hoàn tất manual test end-to-end cho thiết bị mới, session hợp lệ,
  credential sai và session conflict.
- Khi phát triển chọn server, bổ sung versioned server-list contract rồi mới nối
  hành vi cho nút Server.
