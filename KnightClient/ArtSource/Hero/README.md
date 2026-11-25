# Hero — bộ vẽ frame bước đi hướng ngang

Thư mục này nằm **ngoài** `Assets`, nên Unity không import. Game đã có sẵn `walk_east` do script sinh (chân vẽ lại bằng code). Bộ mẫu này dùng khi muốn **thay bằng bản vẽ tay** đẹp hơn; hướng Tây được lật từ bản Đông.

## File

| File | Dùng để |
|---|---|
| `walk_east_template.png` | Vẽ đè lên. 8 ô 128×128 (1024×128), mỗi ô có sẵn ảnh Hero đứng hướng Đông. |
| `walk_east_guides.png` | Lớp đường gióng trong suốt, đặt làm layer phụ khi vẽ, **không** lưu chung vào ảnh cuối. Đỏ: mặt đất (y = 116, bàn chân chạm dòng này). Vàng: hông (y = 79). Xanh lá: gối (y = 95). Xanh dương: trục giữa (x = 64). Chấm đỏ góc trên: số thứ tự frame. |
| `walk_east_reference.png` | Người que minh hoạ tư thế chân từng frame. Đậm: chân gần. Nhạt: chân xa. |

## Tư thế từng frame

Cùng nhịp với 6 hướng đã có; 8 frame, mỗi frame 100 ms, lặp lại.

| Frame | Tư thế |
|---|---|
| 1 | **Đi qua (passing):** chân gần thẳng dưới hông; chân xa co gối, đang đưa ra trước |
| 2 | Chân xa ra trước, sắp chạm đất; chân gần lùi về sau |
| 3 | **Chạm đất (contact):** sải rộng nhất, chân xa trước, chân gần sau; thân thấp 1 px |
| 4 | Chân gần bắt đầu nhấc gót, co gối |
| 5 | **Đi qua:** chân xa thẳng dưới hông; chân gần co gối, đưa ra trước |
| 6 | Chân gần ra trước, sắp chạm đất |
| 7 | **Chạm đất:** chân gần trước, chân xa sau; thân thấp 1 px |
| 8 | Chân xa nhấc gót, co gối |

Mẹo: tay vung ngược chân (chân gần ra trước thì tay gần ra sau). Chỉ cần sửa từ hông trở xuống, cộng thêm tay nếu muốn sinh động hơn.

## Công cụ vẽ (miễn phí)

- **Piskel** (piskelapp.com, chạy trên web): Import `walk_east_template.png` dạng sprite sheet 128×128 → 8 frame; bật onion skin để so frame trước và sau.
- **LibreSprite** (bản miễn phí của Aseprite): mở file, thêm layer từ `walk_east_guides.png`, xoá layer đó trước khi xuất.

Giữ đúng: nền trong suốt, kích thước 1024×128, không khử răng cưa (anti-alias), bàn chân chạm dòng y = 116.

## Đưa vào game

1. Lưu thành `KnightClient/Assets/_Project/Resources/Characters/Hero/walk_east_strip8.png`.
2. Tạo hướng Tây (lật từng ô, giữ thứ tự frame), chạy từ thư mục gốc repo:
   ```bash
   python Tools/PixelDoll/make_walk_from_standing.py --mirror KnightClient/Assets/_Project/Resources/Characters/Hero/walk_east_strip8.png KnightClient/Assets/_Project/Resources/Characters/Hero/walk_west_strip8.png
   ```
3. Quay lại Unity rồi Play. Khi chưa có hai file này, hướng Đông/Tây dùng hiệu ứng nhún tạm.
