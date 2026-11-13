# Thiết kế trò chơi: Thế giới Hiệp sĩ và Ác quỷ

## Tầm nhìn

Tạo một MMORPG công bằng, nơi người chơi mạnh lên nhờ nỗ lực, chiến lược và một
chút may mắn.

## Người chơi hướng tới

- Người yêu thích MMORPG và hành trình phát triển nhân vật lâu dài.
- Người thích săn quái, thu thập nguyên liệu và săn trùm.
- Người thích giao dịch, chế tạo và nâng cấp trang bị.

## Cảm giác cốt lõi

Mỗi lần đăng nhập, người chơi có một mục tiêu để mong đợi: có thể nhận được món
đồ hiếm, nâng cấp thành công, gom đủ nguyên liệu, tham gia sự kiện mới hoặc làm
nhân vật mạnh hơn.

## Vòng lặp chơi chính

```text
Đăng nhập → làm nhiệm vụ → săn quái/thu thập → đánh trùm → nhận vật phẩm
          → chế tạo/nâng cấp → phát triển nhân vật → tham gia sự kiện → tiếp tục
```

## Nguyên tắc

- Không bán lợi thế sức mạnh trực tiếp.
- Không dung túng công cụ gian lận hoặc sửa client để tạo lợi thế.
- Giữ môi trường chơi công bằng, cập nhật đều và chăm sóc cộng đồng.

## Doanh thu dự kiến

Trò chơi miễn phí. Doanh thu có thể đến từ trang phục, thẻ mùa, gói thành viên
hoặc tiện ích không làm mất cân bằng sức mạnh giữa người chơi.

## Định hướng PvP

> Phần này gộp từ `PvP_MMORPG_Plan.md` cũ. Đây là đề xuất; PvP chưa được triển
> khai và không thuộc milestone hiện tại.

### Triết lý

- Nhập vai (MMORPG) thưởng cho sự cày cuốc; đấu trường thưởng cho kỹ năng.
- Tỉ lệ cảm giác mong muốn: 70% nhập vai + 30% hành động.
- Trang bị tạo lợi thế và mở ra chiến thuật; kỹ năng quyết định chiến thắng.
- Tránh kiểu PvP "80% đồ + 20% may mắn"; giảm yếu tố ngẫu nhiên.

### Lộ trình

1. Đấu trường 1v1: nền tảng chiến đấu, hạng, điểm ELO, bảng xếp hạng; lướt, né,
   combo ngắn và đọc đối thủ.
2. Đấu trường 3v3: dành cho nhóm bạn, dùng chung hạng/ELO với 1v1 nhưng khác cơ
   chế chiến đấu và ghép trận; hạn chế tài khoản phụ (smurf).
3. Đấu trường bang hội 5v5: bang hội tuyển thành viên.
4. Giải đấu (tournament).

### Ghép trận

- Chia khung cấp độ: 65–74, 75–84, 85–94…
- Ưu tiên ELO gần nhau; với đội, so ELO trung bình và điểm trang bị (Gear
  Score) trung bình.
- Kiểm tra chênh lệch điểm trang bị trước khi ghép.

### Hành trình người chơi

```text
PvE → có trang bị → 1v1 → leo hạng → 3v3 → được bang hội tuyển → 5v5 → giải đấu
```

---

Đây là định hướng thiết kế, không phải danh sách hệ thống đã được triển khai.
