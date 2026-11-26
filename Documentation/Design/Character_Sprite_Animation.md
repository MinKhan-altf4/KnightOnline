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
- **Nhún thân khi đi** (có frame `walk`): sprite nhún 2 px theo đúng frame đang hiện: cao nhất lúc hai chân đi qua nhau (frame 1 và 5), thấp nhất lúc chân chạm đất (frame 3 và 7), theo nguyên tắc chu kỳ đi. Đồng bộ với frame nên thân và chân luôn khớp nhịp; làm tròn theo pixel. Chỉnh bằng `DirectionalSpriteView.WalkBodyBobPixels`.
- Không dùng Animator: 8 hướng × nhiều move sẽ cần hàng chục clip. Khi dùng Hero, Animator hiệp sĩ cũ của prefab Player bị tắt.
- Người chơi khác dùng cùng cơ chế, với hướng lấy từ dead reckoning.

## Frame bước đi hiện tại

Cả 8 hướng dùng `walk_*_strip8.png` (8 frame, chuyển động hình sin) do `Tools/PixelDoll/make_walk_from_standing.py` sinh từ ảnh đứng theo keypoint hông/gối/chân:

- **Nam, Bắc:** giữ độ rộng chân gần như ảnh đứng (đùi chỉ nghiêng vào 1 px; khép nhiều hơn trông bị bóp, mất tự nhiên); từ gối xuống bàn chân thẳng đứng. Chân đang bước **rút ngắn phần đùi** 3 px, nên gối nhô lên luân phiên.
- **Đông Nam, Đông Bắc** (Tây Nam, Tây Bắc lật từ đó): giữ dáng đứng, hai chân đưa ngược pha nhau dọc hướng đi.
- **Đông** (Tây lật từ đó): **cùng cách với hướng chéo** (hướng chéo là mốc chuẩn). Ảnh đứng hướng ngang là chân gần cộng phần đùi chân xa nằm cạnh và bàn chân xa lòi ra sau mắt cá, nên script **cắt một chân đơn theo khung xương chân gần** (giữ pixel trong 3,5 px ở đùi, 3 px ở cẳng quanh xương, bỏ bàn chân xa, vẽ lại viền chỗ cắt). Chân đơn được **dịch về giữa xương chân gần và chân xa** theo từng dòng (khoảng +2 px ở đùi), vì đùi chân xa nằm ở phía trước nên cắt đi làm chân lệch ra sau so với thân. Mép trên chỗ nối (y = 88) không vẽ viền, để chân liền với thân. Hai chân dựng từ chân đơn này, **thẳng, đưa ngược pha kiểu cây kéo** từ y = 88 (lệch 0) tới bàn chân (±7 px). Chân đang đưa nhấc 3 px, thân nhún 1 px gần lúc chạm đất. Chân sau đi 85% biên độ. **Chân giữ nguyên chi tiết gốc** (vùng sáng, chuyển bóng, khối cơ), chỉ xoá các **nét tối đậm chạy dọc bên trong** (viền đen, đỏ sẫm): ở cỡ trong game chúng chia mỗi chân thành 2 vạch và 2 chân đọc thành 4. Pixel bị xoá lấy màu pixel sáng gần nhất cùng dòng. **Bàn chân (từ mắt cá xuống) giữ nguyên pixel gốc**, vì các nét tối nhỏ (kẽ ngón, cổ chân, lòng bàn chân) tạo hình bàn chân; xoá đi làm bàn chân phình to. Khi hai bàn chân lệch nhau dưới 5 px, bàn chân sau được ẩn sau bàn chân trước để không chồng lệch thành một khối to. Chân sau là cùng chi tiết đó, tối đi theo tông da ấm (đỏ giảm chậm hơn). Bản tô phẳng trước đó hết lỗi 4 chân nhưng mất độ sắc nét so với lúc đứng. Giữ nguyên ảnh gốc phía trên y = 88 (thân, quần lót, bàn tay che đùi). Các cách đã thử và bỏ: nhân đôi cả khối chân (ra 4 chân), vẽ chân bằng hình học (khác nét gốc), xoay cứng đùi quanh hông (lệch ở chỗ nối thân), rig gập gối (khó đọc).
- **Tay và vai** (mọi hướng): tay được tách theo khung xương vai → khuỷu → cổ tay (rộng đủ để mang theo viền ngoài) và vung **ngược pha với chân cùng bên**, đồng bộ 8 frame. Hướng ngang: bàn tay lệch ±2,5 px. Hướng chéo: **chỉ vung tay gần** (tay bên trái màn hình), ±2 px dọc hướng đi và ±1 px theo chiều sâu; tay xa gần như nằm sau mép thân, tách ra sẽ kéo theo pixel thân thành lỗ và vệt. Hướng trước/sau: bàn tay nâng 1–2 px (vung về phía hoặc ra xa người nhìn), **vai bên tay đang vung ra trước nhô 1 px** luân phiên. Tay là **một lớp riêng vẽ đè lên cuối cùng**: chân được dựng từ thân đã bỏ tay, rồi lớp tay đặt lên với cùng độ nhún thân của frame đó. Trước đó bàn tay nằm dưới y = 88 bị xử lý chung với chân (cắt, xoá nét tối) nên bị lem. Vùng tay ở hướng trước/sau/chéo được lấy theo từng dòng, đi từ xương ra hai bên và dừng ở viền của tay, để viền đùi và thân không bị kéo theo bàn tay. Hướng ngang lấy hết trong bán kính, vì bàn tay có nét ngón bên trong và nằm đè lên thân. Phía giáp thân, nếu pixel xuất phát đã là viền tối thì dừng ở viền kế tiếp, không lấn sang hông. Chỗ tay rời đi ở hướng trước/sau/chéo: nếu hai bên đều là thân (tay đè lên thân) hoặc ở 2 dòng đầu dưới vai thì lấp bằng pixel thân sáng gần nhất. Nếu không thì để trong suốt và vẽ viền cho mép thân lộ ra, chỉ nơi tay mới không che. Khe mỏng giữa bàn tay đã vung đi và hông được lấp bằng màu thân phía sau. Cách cũ lấp bằng màu pixel kế bên làm kéo dài viền thân thành mảng tối cạnh tay (lem ở hướng chéo). Ở hướng ngang, lấy pixel sáng gần nhất (không lấy viền tối, vì sẽ thành vệt đen). Chạy với `--no-arms` để tắt.
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
