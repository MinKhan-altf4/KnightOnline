# DevLog — Nền gameplay do server quyết định và HUD người chơi

Ngày: 03-08-2026
Phạm vi: lease, di chuyển, combat, hồi sinh quái, tiến trình nhân vật và HUD.

## Kết quả

- Gia cố heartbeat/lease theo generation và xử lý lỗi lưu phiên để không mắc
  ở màn hình tải vô hạn.
- Thêm sequence cho input di chuyển; server loại request trùng/cũ/sai thứ tự,
  gửi snapshot vị trí và lý do hiệu chỉnh. Client dự đoán để phản hồi nhanh rồi
  hiệu chỉnh theo vị trí server.
- Lọc danh sách/trạng thái quái theo map; combat kiểm tra map, tầm đánh,
  cooldown và mục tiêu còn sống.
- Server xử lý dịch chuyển người chơi khi quái hồi sinh chồng lên vị trí.
- Thêm cấp độ tối đa cấu hình 40, EXP quái, lưu `total_experience`, migration
  `20260803122640_AlphaProgressionFoundation` và bảng lịch sử grant chống trùng.
- Tính chỉ số theo class, tăng trưởng mỗi cấp và modifier có nguồn; snapshot
  server trả cấp, EXP, HP/MP, Attack và Defense.
- Mở rộng `InGameHUD`; thêm công cụ Editor tạo PlayerStatusPanel. Công cụ chỉ
  chạy trong Editor và các thay đổi scene đi kèm `.meta`.

## Quyết định

- Server quyết định vị trí, map, combat, EXP, cấp và chỉ số.
- Giá trị cân bằng đặt trong cấu hình server đã kiểm tra, không rải trong packet
  handler/UI.
- Grant EXP chống lặp trong boundary PostgreSQL; không tuyên bố giao hàng
  exactly-once xuyên toàn hệ thống.
- Packet mới chưa có chính sách thương lượng phiên bản đầy đủ.
- Migration `Down()` xóa lịch sử tiến trình/EXP, có thể mất dữ liệu; ưu tiên
  forward-fix sau khi có dữ liệu người chơi thật.

## Kiểm tra tại phiên

- Unit test máy chủ: 58/58 đạt.
- Tích hợp PostgreSQL: 15/15 đạt; chạy tuần tự do dùng chung database.
- Build Release máy chủ: 0 cảnh báo, 0 lỗi.
- HUD chưa được kiểm tra hình ảnh cuối trong Play Mode trên các tỉ lệ màn hình.

## Rủi ro và bước tiếp theo

- Sự kiện thưởng EXP từ kill chưa đi trọn qua durable outbox.
- Session/world state còn trong bộ nhớ một tiến trình; chưa hỗ trợ nhiều server.
- TLS, kho token an toàn, metric/dashboard và load test vẫn là cổng phát hành.
- HUD cần event authoritative riêng cho sát thương, hồi phục, kỹ năng và hồi
  sinh; safe area/desktop/mobile còn cần nghiệm thu.
- Các mốc tiếp theo lúc đó: kiểm tra HUD trong Unity, hoàn thiện event sinh lực,
  outbox thưởng và read model lịch sử tiến trình cho Admin.
