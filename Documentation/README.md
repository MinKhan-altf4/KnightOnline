# Cổng tài liệu KnightOnline

Không cần đọc toàn bộ thư mục. Bắt đầu từ [tình trạng dự án](Trang_Thai_Du_An.md),
sau đó đọc quy tắc và tài liệu đúng module đang làm.

## Thứ tự dành cho người mới

1. [Tình trạng dự án](Trang_Thai_Du_An.md) — dự án đang ở đâu và nên kiểm tra gì
   trước.
2. [Quy tắc cốt lõi](KNIGHT_PROJECT_CORE_RULES.md) — yêu cầu bắt buộc cho mọi
   thay đổi.
3. [Cấu trúc repository](Architecture/Repository_Structure.md) — trách nhiệm
   client, server, test và boundary giữa module.
4. Chọn tài liệu theo phạm vi bên dưới.
5. Đọc DevLog gần nhất liên quan để hiểu lịch sử, rồi đối chiếu source và test.

## Tài liệu theo phạm vi

| Bạn đang tìm hiểu | Tài liệu nên đọc | Lưu ý |
|---|---|---|
| Luồng khởi động Unity, đăng nhập, chọn nhân vật, vào game | [Runtime Unity và đánh giá Alpha](Architecture/Current_Unity_Runtime_And_Alpha_500_Online.md) | Audit ngày 02-08-2026; cần đối chiếu lại code trước khi coi là hiện trạng mới nhất. |
| Luồng nhân vật và tạo nhân vật | [Character Flow](Design/Character_Flow_Architecture_Plan.md) | Tài liệu thiết kế; xem phần tiến độ và đối chiếu server/client. |
| HUD và luồng màn hình | [System/HUD Flow](Design/System_And_HUD_Flow_v2.md) | Đặc tả sản phẩm dài; không phải toàn bộ chức năng đã được triển khai. |
| UI Toolkit trong Làng Tân Thủ | [Hướng dẫn HUD Làng Tân Thủ](Design/Unity_UI_Toolkit_Starter_Village_Guide.md) | Có ví dụ UXML/USS và cách kiểm tra trong Unity. |
| Menu chức năng InGame | [Hướng dẫn menu UI Toolkit](Design/Unity_UI_Toolkit_Function_Menu_Guide.md) | Phạm vi menu hiện có; các chức năng chưa xây dựng có thể chỉ là chỗ giữ chỗ. |
| Đăng ký tài khoản khi mở server thật | [Luồng đăng ký Production](Production/Registration_Flow.md) | Kế hoạch và điều kiện phát hành, không đồng nghĩa đã sẵn sàng Production. |
| Chuyển Authentication/Character sang Production | [Kế hoạch cutover](Production/Account_And_Character_Flow_Cutover.md) | Đối chiếu lại trạng thái adapter và migration trước khi triển khai. |
| Lịch sử thay đổi theo phiên | [DevLogs](DevLogs/README.md) | Tài liệu lịch sử; các kết quả test có ngày và không thay thế việc chạy lại. |

## Thư mục

- `Architecture/`: cấu trúc source và luồng runtime.
- `Design/`: thiết kế game, luồng giao diện và hướng dẫn UI.
- `Production/`: kế hoạch/cổng chuẩn bị cho môi trường Production.
- `Audits/`: kết quả kiểm tra tại một thời điểm; không tự cập nhật.
- `DevLogs/`: tổng kết phiên làm việc.
- `Archive/`: tài liệu lịch sử đã bị tài liệu mới thay thế; không dùng làm yêu
  cầu hiện hành.

## Quy tắc giữ tài liệu gọn

- Mỗi quyết định hoặc luồng chỉ có một tài liệu nguồn hiện hành.
- Khi audit cũ bị thay thế, đánh dấu lịch sử hoặc chuyển lưu trữ; không để nó
  cạnh tài liệu hiện hành mà không ghi rõ trạng thái.
- Không tạo DevLog cho từng lỗi nhỏ. Chỉ tổng kết/cập nhật log khi chủ dự án yêu
  cầu ghi log hoặc chốt phiên.
- Không suy ra tính năng đã hoàn tất chỉ từ tài liệu thiết kế hay DevLog; kiểm
  tra code, cấu hình, migration, test và Unity runtime.
