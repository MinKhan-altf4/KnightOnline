# Cổng tài liệu KnightOnline

Không cần đọc toàn bộ thư mục. Bắt đầu từ [tình trạng dự án](Trang_Thai_Du_An.md),
sau đó đọc quy tắc và tài liệu đúng module đang làm.

## Thứ tự dành cho người mới

1. [Tình trạng dự án](Trang_Thai_Du_An.md) — dự án đang ở đâu, nên làm gì trước.
2. [Quy tắc cốt lõi](KNIGHT_PROJECT_CORE_RULES.md) — yêu cầu bắt buộc cho mọi
   thay đổi.
3. [Cấu trúc repository](Architecture/Repository_Structure.md) — trách nhiệm
   client, server, test và boundary giữa module.
4. [Đánh giá luồng 13-11-2026](Audits/2026-11-13-danh-gia-luong-va-huong-di.md)
   — sai sót đã biết và hướng đi đề xuất.
5. Tài liệu theo phạm vi bên dưới, rồi DevLog gần nhất của module đó.

## Tài liệu theo phạm vi

| Bạn đang tìm hiểu | Tài liệu | Lưu ý |
|---|---|---|
| Luồng khởi động Unity, đăng nhập, chọn nhân vật, vào game | [Runtime Unity và đánh giá Alpha](Architecture/Current_Unity_Runtime_And_Alpha_500_Online.md) | Ảnh chụp 02-08-2026; đối chiếu code trước khi dùng. |
| Hợp đồng phiên, lease, retry xác thực | [Hợp đồng phiên và lease](Architecture/Session_Lease_Contract.md) | Áp dụng cho giai đoạn một instance hiện tại. |
| Tầm nhìn game và định hướng PvP | [Thiết kế trò chơi](Design/GameDesign.md) | Định hướng, chưa phải hệ thống đã làm. |
| Luồng nhân vật và tạo nhân vật | [Luồng nhân vật](Design/Character_Flow_Architecture_Plan.md) | Thiết kế; xem mục tiến độ cuối file. |
| HUD và luồng màn hình | [Luồng hệ thống/HUD](Design/System_And_HUD_Flow_v2.md) | Đặc tả sản phẩm dài; nhiều phần chưa triển khai. |
| UI Toolkit (thẻ nhiệm vụ, menu chức năng) | [Hướng dẫn UI Toolkit](Design/Unity_UI_Toolkit_Guide.md) | Có cách chỉnh và checklist kiểm tra trong Unity. |
| Đăng ký tài khoản khi mở server thật | [Luồng đăng ký Production](Production/Registration_Flow.md) | Kế hoạch và điều kiện phát hành. |
| Chuyển xác thực/nhân vật sang Production | [Kế hoạch cutover](Production/Account_And_Character_Flow_Cutover.md) | Đối chiếu adapter và migration trước khi làm. |
| Lịch sử thay đổi theo phiên | [DevLogs](DevLogs/README.md) | Kết quả test có ngày; không thay việc chạy lại. |

## Thư mục

| Thư mục | Nội dung |
|---|---|
| `Architecture/` | Cấu trúc source, luồng runtime, hợp đồng phiên. |
| `Design/` | Thiết kế game, luồng giao diện và hướng dẫn UI. |
| `Production/` | Kế hoạch/cổng chuẩn bị cho môi trường Production. |
| `Audits/` | Kết quả đánh giá tại một thời điểm; không tự cập nhật. |
| `DevLogs/` | Tổng kết phiên làm việc và mẫu DevLog. |
| `Archive/` | Tóm tắt lịch sử đã bị tài liệu mới thay thế; không dùng làm yêu cầu. |

## Quy tắc giữ tài liệu gọn

- Mỗi quyết định hoặc luồng chỉ có **một** tài liệu nguồn hiện hành. Nội dung
  nhỏ, cùng chủ đề thì gộp vào tài liệu sẵn có thay vì tạo file mới.
- Tài liệu viết bằng tiếng Việt. Giữ nguyên tên class, packet, file, cấu hình
  và thuật ngữ chuẩn (bảng dưới); thuật ngữ khác thì dịch, lần đầu có thể ghi
  kèm tiếng Anh trong ngoặc.
- Khi audit cũ bị thay thế, đánh dấu lịch sử hoặc chuyển vào `Archive/`.
- Không tạo DevLog cho từng lỗi nhỏ; chỉ tổng kết khi chủ dự án yêu cầu ghi log
  hoặc chốt phiên.
- Không suy ra tính năng đã hoàn tất chỉ từ tài liệu thiết kế hay DevLog; kiểm
  tra code, cấu hình, migration, test và Unity runtime.
- Tên file giữ ổn định để không gãy liên kết; đổi tên phải sửa mọi liên kết
  trong cùng thay đổi.

## Bảng thuật ngữ giữ nguyên

| Thuật ngữ | Nghĩa trong dự án |
|---|---|
| Server-authoritative | Server là nguồn sự thật; client chỉ gửi ý định. |
| Lease / generation | Quyền sở hữu phiên tài khoản có hạn (TTL) và mã thế hệ của phiên đó. |
| Heartbeat | Gói định kỳ client gửi để gia hạn lease. |
| `RequestId` / idempotency | Mã yêu cầu để gửi lại không gây tác dụng lặp. |
| Refresh token | Token dùng để tiếp tục phiên mà không nhập lại mật khẩu; được xoay vòng sau mỗi lần dùng. |
| Outbox | Bảng sự kiện ghi cùng transaction để phát tin cậy về sau. |
| Snapshot | Bản chụp trạng thái (vị trí, chỉ số…) server gửi hoặc lưu. |
| Fencing | Chặn phiên cũ ghi đè dữ liệu của phiên mới. |
| AOI (vùng quan tâm) | Phạm vi thực thể mà một người chơi cần nhận cập nhật. |
| Tick | Một bước mô phỏng thế giới với tần số cố định. |
| Admin Management Contract | Những gì Admin được xem/làm với một module, cùng quyền và audit. |
| Development / Staging / Production | Môi trường phát triển / thử nghiệm / chạy thật. |
