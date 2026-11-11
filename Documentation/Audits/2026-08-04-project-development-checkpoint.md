# Mốc rà soát phát triển dự án — 04-08-2026

> Đây là ảnh chụp lịch sử tại commit `98c056c`, không phải xác nhận hiện trạng
> ngày nay. Kết luận phải được đối chiếu lại với source, cấu hình, migration,
> kiểm thử và Unity trước khi dùng để ra quyết định.

## Hướng phát triển tại thời điểm đó

Dự án thu hẹp mục tiêu từ một MMORPG nhiều hệ thống sang một luồng chơi dọc có
thể kiểm chứng: tài khoản → nhân vật → vào thế giới → nhiệm vụ → quái → phần
thưởng. Kiến trúc vẫn giữ ranh giới để mở rộng multiplayer sau này.

Nguyên tắc sản phẩm/kỹ thuật:

```text
Client gửi ý định; server xác thực và quyết định kết quả chính thức.
```

## Các mốc đã hoàn thành đến ngày 04-08-2026

- Dựng Unity client, máy chủ .NET và contract packet dùng chung trong một
  repository; có composition root, EventBus và boundary xử lý packet.
- Hoàn thiện nền tảng guest/đăng nhập, refresh-token rotation, phát hiện token
  bị dùng lại, heartbeat và lease tài khoản có TTL/generation.
- Thiết lập giới hạn nền tảng tại thời điểm audit: 500 tài khoản đang hoạt động
  và 750 kết nối transport trong một tiến trình.
- Có luồng chọn/tạo tối đa ba nhân vật mỗi tài khoản/máy chủ, catalog từ server,
  kiểm tra tên và bước `SelectCharacter → EnterWorld`.
- Server sở hữu vị trí gameplay, kiểm tra di chuyển/chiến đấu, ranh giới map,
  quái và cổng dịch chuyển.
- Hoàn thành lát cắt nhiệm vụ tân thủ: nói chuyện với Mẹ, hạ 20 Wolf, quay về
  nhận phần thưởng một lần và tự chọn thời điểm đi tới Safe Zone 1.
- Lưu tiến độ nhiệm vụ, kill credit chống trùng, vật phẩm thưởng, idempotency,
  audit và outbox trong PostgreSQL.
- Có HUD sinh lực/năng lượng cơ bản và menu chức năng ở mức nền tảng.

## Scene và luồng chạy được ghi nhận

```text
App → Bootstrap → Entry/Authentication → Character Select/Create → InGame
```

Chạy game từ scene `App`; scene này tạo các dịch vụ sống xuyên scene rồi mới nạp
`Bootstrap`. `Login.unity` độc lập đã bị loại bỏ tại mốc đó.

## Kết quả kiểm tra tại commit ghi trên

| Kiểm tra | Kết quả thời điểm đó |
|---|---|
| Build Release máy chủ | Thành công, 0 cảnh báo, 0 lỗi |
| Unit test máy chủ | 75/75 đạt |
| Tích hợp PostgreSQL | 18/18 đạt |
| Unity compile trước chỉnh sửa cổng cuối | Người dùng xác nhận đạt |
| Unity smoke test cổng sau khi khởi động sạch | Còn chờ |

Các test chỉ chứng minh phạm vi đã kiểm tra; không chứng minh sẵn sàng mở Alpha,
chạy nhiều node, tải 500 người hoặc hiển thị đúng trên thiết bị thật.

## Hạn chế/rủi ro đã ghi nhận

- Lease/session và một số state runtime còn nằm trong một tiến trình; cần adapter
  phân tán trước triển khai nhiều node.
- Môi trường public cần TLS và nơi lưu token an toàn theo nền tảng.
- Outbox có dữ liệu nhưng chưa có đầy đủ dispatcher, retry, dead-letter và metric.
- Broadcast chưa lọc theo vùng quan tâm và chưa có hàng đợi gửi hữu hạn; chưa có
  load/soak test đủ để tuyên bố đạt 500 người online.
- Chưa hoàn thành Admin API/dashboard, runbook, backup/restore drill và thương
  lượng phiên bản protocol.
- Đồ họa, NPC, map, chuyển cảnh, safe area và tỉ lệ màn hình còn ở mức mẫu.
- Hệ thống inventory chưa có đầy đủ contract đọc, giao diện và luồng trang bị.

Các hạn chế này là trạng thái ở năm 2026-08-04. Một số có thể đã thay đổi; xem
[Tình trạng dự án](../Trang_Thai_Du_An.md) và kiểm tra code hiện tại trước khi
mở lại thành task.

## Checklist cho một đợt rà soát mới

1. Ghi branch, commit và trạng thái working tree; giữ nguyên thay đổi local.
2. Lần theo hành động từ UI/input qua packet, server authorization/validation,
   service/domain, persistence rồi ngược về event/presentation.
3. Kiểm tra server-authority, quyền truy cập, chống request trùng, transaction,
   cấu hình, secret, asset `.meta` và hướng phụ thuộc assembly.
4. Kiểm tra lỗi/mạng xấu: packet trùng hoặc sai thứ tự, retry, disconnect,
   reconnect, nhiều thiết bị, server restart và request lặp phần thưởng.
5. Chạy build/test/migration phù hợp; mở Unity và kiểm tra luồng đầy đủ nếu có
   thay đổi client/UI.
6. Ghi rõ bằng chứng, rủi ro, rollback/forward-fix và bước nhỏ tiếp theo.

## Bước tiếp theo được đề xuất ở mốc lịch sử

Thiết kế inventory đọc từ dữ liệu authoritative, thông báo thưởng chỉ hiện một
lần, hoàn thiện trình bày NPC Mẹ và điều tra giật khi va chạm. Các đề xuất này
không phải roadmap hiện hành; trước khi tiếp tục hãy kiểm tra tiến độ trong
[trạng thái dự án](../Trang_Thai_Du_An.md) và source mới nhất.
