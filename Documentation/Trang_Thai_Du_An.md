# Tình trạng dự án KnightOnline

**Đối chiếu tài liệu:** 11-11-2026  
**Cơ sở:** lịch sử Git đến commit `a232050` (10-11-2026) và tài liệu trong repository.  
**Giới hạn:** đây là bản hướng dẫn bắt đầu phiên làm việc, không phải xác nhận đã chạy Unity hay kiểm thử toàn bộ source.

## Dự án đang ở đâu?

KnightOnline có nền tảng client Unity và server .NET theo nguyên tắc server quyết
định trạng thái chính thức. Đến mốc tài liệu gần nhất, đã xây dựng được luồng
chơi dọc từ tài khoản, chọn/tạo nhân vật, vào thế giới, nhiệm vụ tân thủ, quái,
dịch chuyển và HUD.

Các thay đổi gần đây nhất trong Git tập trung vào giao diện:

- **13-10-2026:** bổ sung HUD Làng Tân Thủ bằng UI Toolkit, menu chức năng dưới
  màn hình và tài liệu hướng dẫn tương ứng.
- **10-11-2026:** chỉnh giao diện Bootstrap cho đăng nhập/chọn nhân vật/tạo nhân
  vật, cùng màu sắc và cách trình bày nút.

Đây là dấu mốc từ lịch sử commit; cần kiểm tra Play Mode để xác nhận giao diện
hiện tại, đặc biệt sau khi Unity biên dịch lại.

## Việc nên làm đầu tiên trong phiên tiếp theo

1. Mở `KnightClient` bằng đúng phiên bản Unity của dự án và chờ import/compile
   hoàn tất.
2. Mở scene `App` và chạy từ đó. `App` nạp `Bootstrap` và tạo các dịch vụ dùng
   xuyên scene; chạy riêng `Bootstrap` có thể thiếu composition root.
3. Kiểm tra lần lượt: màn hình vào game → đăng nhập/tiếp tục → danh sách nhân
   vật → tạo nhân vật → vào game.
4. Trong InGame, kiểm tra HUD Làng Tân Thủ, menu chức năng, tương tác NPC Mẹ,
   nhiệm vụ, quái và cổng dịch chuyển.
5. Ghi lại log Unity/server và bước tái hiện cho lỗi còn gặp; so sánh với source
   hiện tại trước khi sửa tài liệu hay kết luận trạng thái.

## Hợp đồng triển khai server hiện tại

Giai đoạn hiện tại chỉ hỗ trợ một tiến trình server trên một máy; cấu hình bind
loopback và `SingleInstanceGuard` chặn tiến trình thứ hai. Lease vẫn ở bộ nhớ,
vì khi server khởi động lại mọi TCP connection đóng và client xin phiên mới.
Snapshot nhân vật được lưu theo generation/version định kỳ; lease bền vững chỉ
cần khi chuyển sang nhiều instance hoặc cần giữ lease qua restart.

Xem [Hợp đồng phiên và lease](Architecture/Session_Lease_Contract.md) để biết
ngữ nghĩa RequestId, generation ghi nhân vật, thứ tự bot và các cổng còn thiếu
trước khi thử ngoài mạng. Server hiện vẫn chỉ bind loopback; TLS, kho token an
toàn, broadcast có hàng đợi hữu hạn và bot đầu-cuối chưa hoàn tất.

## Hạng mục cần xác nhận hoặc tiếp tục

- **Giao diện Bootstrap:** xác nhận nền khung không còn quá tối/viền sprite cũ
  và chữ, nút hiển thị đúng sau khi Unity nạp scene.
- **UI Toolkit InGame:** xác nhận PanelSettings và theme được gán, menu chức
  năng nằm sát giữa cạnh dưới, không chồng lên HUD nhiệm vụ.
- **Luồng chơi tân thủ:** xác nhận có thể nói chuyện với NPC Mẹ, nhận tiến độ
  nhiệm vụ, đánh Wolf, nhận thưởng đúng một lần và đi qua cổng hợp lệ.
- **Kiểm thử runtime:** các kết quả test trong DevLog là kết quả tại thời điểm
  ghi. Chạy lại test/build phù hợp trước khi dựa vào chúng cho quyết định mới.
- **Sẵn sàng Production/Alpha:** tài liệu kiến trúc ngày 02-08-2026 liệt kê các
  cổng còn thiếu như session lease phân tán, TLS, kho token an toàn, broadcast
  theo vùng quan tâm, quan sát vận hành và load/soak test. Chưa có tài liệu mới
  xác nhận các cổng này đã đóng; cần kiểm tra lại code và cấu hình trước khi
  triển khai.

## Quy tắc kiến trúc cần nhớ

- Đọc [Quy tắc cốt lõi](KNIGHT_PROJECT_CORE_RULES.md) trước khi sửa code.
- Client gửi ý định; server xác thực, kiểm tra và quyết định kết quả chính thức.
- UI chỉ trình bày và phát thao tác; không tự quyết định nhiệm vụ, vật phẩm,
  tiền, sát thương, vị trí hoặc quyền tài khoản.
- Các module giao tiếp qua contract rõ ràng; tránh tạo nguồn logic nghiệp vụ
  trùng lặp hoặc phụ thuộc vòng.
- Không đưa secret vào repository. Thay đổi dữ liệu quan trọng phải xem xét
  transaction, idempotency, migration, audit và cách khôi phục tương ứng.

## Lộ trình đọc ngắn

1. [Cổng tài liệu](README.md) — biết tài liệu nào đang có giá trị.
2. [Quy tắc cốt lõi](KNIGHT_PROJECT_CORE_RULES.md) — cách làm việc bắt buộc.
3. [Cấu trúc repository](Architecture/Repository_Structure.md) — tìm đúng module.
4. [Luồng Unity và đánh giá Alpha](Architecture/Current_Unity_Runtime_And_Alpha_500_Online.md)
   — luồng runtime và các giới hạn đã ghi nhận.
5. Đọc tài liệu thiết kế hoặc DevLog gần nhất thuộc module được giao.

## Nguồn và độ tin cậy

Code, cấu hình, migration và kiểm thử hiện hành là căn cứ xác nhận triển khai.
Tài liệu kiến trúc/thiết kế mô tả hợp đồng và ý định hiện tại theo ngày ghi.
Audit và DevLog là ảnh chụp lịch sử, không tự cập nhật theo code.
