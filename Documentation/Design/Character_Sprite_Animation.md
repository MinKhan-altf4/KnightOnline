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

## Frame bước đi hiện tại

Cả 8 hướng dùng `walk_*_strip8.png` (8 frame, chuyển động hình sin) do `Tools/PixelDoll/make_walk_from_standing.py` sinh từ ảnh đứng theo keypoint hông/gối/chân:

- **Nam, Bắc:** giữ độ rộng chân gần như ảnh đứng (đùi chỉ nghiêng vào 1 px; khép nhiều hơn trông bị bóp, mất tự nhiên); từ gối xuống bàn chân thẳng đứng. Chân đang bước **rút ngắn phần đùi** 3 px, nên gối nhô lên luân phiên.
- **Đông Nam, Đông Bắc** (Tây Nam, Tây Bắc lật từ đó): giữ dáng đứng, hai chân đưa ngược pha nhau dọc hướng đi.
- **Đông** (Tây lật từ đó): ảnh đứng là chân gần cộng phần đùi của chân xa nằm cạnh và bàn chân xa lòi ra sau mắt cá. Script **cắt một chân đơn theo khung xương chân gần** (hông → gối → mắt cá): giữ pixel trong nửa bề rộng chân quanh xương (3,5 px ở đùi, 3 px ở cẳng), bỏ bàn chân xa, vẽ lại viền mọi chỗ cắt. Cả hai chân dựng từ cùng hình này nên cùng cỡ. Đùi **uốn dần** từ chỗ nối thân (lệch 0 px ở y = 88, tăng tới ±3 px ở gối), nên chân dính liền với thân. Cẳng chân xoay quanh gối ±14°, chân đang đưa gập gối tối đa 22°. Chân sau chỉ đi 75% biên độ và được tô thành **một mảng bóng đồng màu, chỉ giữ viền ngoài**, như cách vẽ chân xa trong chu kỳ đi pixel art. Nếu giữ nét cơ bên trong, các đường nét chồng lên chân trước và đọc thành nhiều chân. Nét vẽ và cơ bắp giữ đúng ảnh gốc. Thân hạ xuống để chân trụ luôn chạm đất. Giữ nguyên ảnh gốc phía trên y = 88 (thân, quần lót, bàn tay che đùi).
- Muốn vẽ tay đẹp hơn cho hướng ngang: dùng bộ mẫu `KnightClient/ArtSource/Hero/` (có README), rồi tạo Tây bằng `--mirror`.
- Đây là bản tạm: có frame thật từ PixelDoll hoặc vẽ tay thì chép đè file `walk_*`.

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
