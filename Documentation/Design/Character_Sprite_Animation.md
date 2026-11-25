# Sprite và animation nhân vật (PixelDoll)

**Trạng thái:** áp dụng từ 25-11-2026. Phân loại: *Presentation*.

## Quy ước file

Mỗi nhân vật có một thư mục `KnightClient/Assets/_Project/Resources/Characters/<Tên>/`, ví dụ `Hero`.

- Mỗi file là **một dải frame nằm ngang**. Ô vuông, số frame = chiều rộng / chiều cao. Bản hiện tại dùng ô 128×128.
- Tên file theo một trong hai dạng:
  - `walking_south_strip6.png`: tên gốc khi PixelDoll export kiểu **generic** (thư mục `generic/base_main/`), copy thẳng vào được;
  - `hero_walking_south.png`: có tiền tố tên nhân vật.
- 8 hướng: `south`, `south-east`, `east`, `north-east`, `north`, `north-west`, `west`, `south-west`. PixelDoll đã lật sẵn các hướng đối xứng.
- `standing` **bắt buộc** đủ 8 hướng. Move khác (`walking`, sau này `attack`, `hurt`…) tuỳ chọn; hướng nào thiếu thì dùng `standing`.
- Pivot ở chân (64, 116) theo PixelDoll; 64 px = 1 đơn vị Unity (nhân vật cao khoảng 1,4 đơn vị).

Import do `Editor/PixelCharacterImporter.cs` tự cấu hình: Sprite, Texture 2D, lọc Point, không nén. Không cần chỉnh Inspector.

## Cách phát

- `DirectionalCharacterSprites` nạp toàn bộ file trong thư mục và tự cắt dải thành frame.
- `DirectionalSpriteView` chọn move và frame mỗi khung hình:
  - đang di chuyển thì `walking` lặp, mỗi frame 100 ms;
  - dừng thì `standing` theo hướng cuối cùng.
- Chưa có frame `walking` thì dùng **hiệu ứng nhún tạm**: khi di chuyển, sprite đứng nhảy nhẹ 3 pixel mỗi bước (0,3 giây/bước), làm tròn theo pixel. Có frame thật thì hiệu ứng tự tắt. Sprite vẽ trên object con `HeroVisual` để việc nhún không dịch chuyển Rigidbody/collider của Player.
- Không dùng Animator: 8 hướng × nhiều move sẽ cần hàng chục clip. Khi dùng Hero, Animator hiệp sĩ cũ của prefab Player bị tắt.
- Người chơi khác dùng cùng cơ chế, với hướng lấy từ dead reckoning.

## Frame bước đi hiện tại (sinh tự động)

Hero chưa có frame đi vẽ tay. Bộ `walk_*_strip8.png` hiện tại (8 frame, chu kỳ 0,8 giây) do `Tools/PixelDoll/make_walk_from_standing.py` **sinh từ ảnh đứng**, dựa trên keypoint hông/gối/chân trong `manifest.json`:

- Chuyển động theo hình sin: chân sải ra, khép vào rồi đổi bên dần dần; chân đang đưa về trước được nhấc dần lên và chạm đất ở bước sải lớn nhất.
- **Hướng ngang:** nhân đôi vùng chân, nghiêng ngược nhau (tối đa 9 px ở bàn chân), nhấc 3 px.
- **Hướng chéo:** tách từng chân, giữ độ choãi 3 px, đung đưa thêm 4 px; chân trước thấp hơn tối đa 2 px.
- **Hướng trước/sau:** co gối luân phiên (cẳng chân nhấc tối đa 5 px).
- Thân nhún 1 px quanh lúc chạm đất. Hướng đối xứng được lật ngang như PixelDoll.

Đây là **bản tạm**: chân chuyển động nhưng cứng hơn frame vẽ thật. Khi có frame từ PixelDoll hoặc vẽ tay, chép đè các file `walk_*` là xong.

## Thêm animation bước đi

1. Trong PixelDoll: mở `project.json`, thêm move `walking` (gợi ý 6–8 frame, loop, khoảng 100 ms/frame) cho 5 hướng gốc. Các hướng còn lại PixelDoll tự lật.
2. Export kiểu **generic**.
3. Copy các file `generic/base_main/walking_*_strip*.png` vào `Resources/Characters/Hero/`.
4. Quay lại Unity, chờ import xong rồi Play. Console sẽ in dòng `[Character] Loaded Hero: standing (1 frame(s)), walking (N frame(s))`.

## Kiểm tra

- Test EditMode trong `WorldSyncClientTests`: đọc tên file, chọn frame theo thời gian, chọn 8 hướng.
- Thủ công:
  - đi 8 hướng thấy bước chân đúng hướng;
  - dừng thì về tư thế đứng đúng hướng;
  - người chơi khác cũng có bước chân.

## Giới hạn

- Thời gian frame cố định 100 ms; chưa đọc `rig.animations[].frames[].ms` từ `manifest.json`.
- Chưa hỗ trợ ghép lớp trang bị (`layers/`) hay đổi màu theo bảng màu (`materials`). Sẽ làm cùng module Túi đồ / Trang bị.
