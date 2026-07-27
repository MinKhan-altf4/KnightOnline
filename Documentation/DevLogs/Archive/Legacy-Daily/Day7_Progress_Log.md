# The World of Knights & Demons — Nhật ký Phát triển
## Day 7: Nền tảng Gameplay, Physics Collision & InGame HUD

---

## 1. Mục tiêu đầu ngày

Củng cố kiến trúc đã đặt nền ở Day 6 và xác nhận vòng lặp gameplay cơ bản hoạt động ổn định:

- CharacterData đủ field cho DB sau này
  → PlayerController đọc stats từ data, không hardcode
  → Physics collision đáng tin cậy với tường
  → Map test xác nhận luồng di chuyển

---

## 2. Những gì đã hoàn thành

### 2.1. Dọn Bootstrap
- Xóa `Debug.Log` test trong `GameLifetimeScope.Awake()` — override này chỉ tồn tại để kiểm tra Awake được gọi, không còn giá trị.
- Cập nhật comment class `CharacterFlowController` phản ánh đúng thực tế: controller vừa điều phối panel vừa gọi `LoadSceneAsync` chuyển sang InGame, không còn thuần panel-based.
- Xóa Player và Camera cũ đang disable trong scene Bootstrap — không còn rác legacy.

### 2.2. Mở rộng CharacterData
- `CharacterData` từ 1 field (`CharacterName`) lên đầy đủ:
  - **Identity**: `CharacterId` (int, default 0 đến khi có DB), `CharacterName`
  - **Stats**: `Level`, `MaxHp`, `CurrentHp`, `MoveSpeed` (default 4f)
  - **World**: `SpawnPosition` (Vector2, default `Vector2.zero`)
- Constructor giữ nguyên signature `(string characterName)` để không breaking change toàn bộ nơi tạo `CharacterData`.
- `CharacterData` vẫn là pure data — không có logic gameplay bên trong.

### 2.3. CharacterData vào DI scope của InGame
- `InGameLifetimeScope` đăng ký `CharacterData` từ `GameSession.Current`:
  - Nếu có session (chạy qua Bootstrap): dùng data thật.
  - Nếu không có session (chạy thẳng InGame để test): tạo `CharacterData("TestCharacter")` mặc định — container build thành công, không crash.
- `PlayerController` inject `CharacterData` qua VContainer, không còn gọi `GameSession.Current` trực tiếp.

### 2.4. PlayerController không hardcode stats
- Xóa `_moveSpeed = 4f` hardcode duy nhất.
- Thêm `_defaultMoveSpeed` là `[SerializeField]` fallback cho Editor khi không có CharacterData.
- `MoveSpeed` property: `_characterData?.MoveSpeed ?? _defaultMoveSpeed`.
- `Start()` đặt Player tại `_characterData.SpawnPosition` — sẵn sàng nhận tọa độ từ server sau này.

### 2.5. Chuyển từ Kinematic sang Dynamic Rigidbody2D
- Phát hiện Kinematic + `MovePosition` không đáng tin trong Unity 6 cho top-down collision — Player xuyên qua tường dù đã bật Continuous và Use Full Kinematic Contacts.
- Chuyển sang **Dynamic** + `Gravity Scale = 0` + `Linear Damping = 10`:
  - Dynamic body tự xử lý collision với static collider.
  - `Linear Damping = 10` đảm bảo Player dừng ngay khi thả phím, không trượt.
  - `FixedUpdate` dùng `linearVelocity` thay vì `MovePosition`.
- Fix warning `CS0168` trong `NetworkClient.cs`: `catch (IOException ex)` → `catch (IOException)`.

### 2.6. Map Test 2D với Wall Collider
- Tạo `Map_Test` trong scene InGame:
  - `Floor`: sprite xanh lá, scale phù hợp camera, không có Collider.
  - 4 tường (`Wall_Top/Bottom/Left/Right`): sprite nâu đất, `BoxCollider2D`, bố trí bao quanh Floor.
- Camera Orthographic Size = 8; map tính toán vừa khung camera.
- Kết quả: Player di chuyển WASD tự do bên trong, dừng hẳn khi chạm tường.

### 2.7. Đóng gói Player Prefab
- Đóng gói Player GameObject thành Prefab thành công và lưu trữ tại `Assets/_Project/Prefabs/` để chuẩn bị cho việc gắn Animator ở Day 8.

### 2.8. Hệ thống HUD Tối thiểu (InGameHUD)
- Khởi tạo `HUD_Canvas` với chế độ Screen Space - Overlay và Scale With Screen Size (1920x1080).
- Thiết lập 3 thành phần TextMeshPro hiển thị: Tên nhân vật, Trạng thái kết nối (xanh/đỏ) và Tọa độ X/Y Debug.
- Căn chỉnh UI phù hợp với tỷ lệ 16:9, đảm bảo font chữ hiển thị to, rõ ràng trong màn hình Game.
- Inject thành công `CharacterData` và `PlayerController` vào `InGameHUD` qua VContainer để cập nhật thông số realtime.

---

## 3. Kết quả kiểm thử
Luồng đã chạy thành công:
1. Chạy từ Bootstrap → kết nối server → chọn nhân vật.
2. Scene chuyển sang InGame; log hiển thị đầy đủ thông tin data.
3. Tên nhân vật, trạng thái mạng và tọa độ được cập nhật chuẩn xác lên HUD.
4. Player spawn tại `SpawnPosition` (hiện tại `Vector2.zero`).
5. WASD di chuyển mượt, Camera bám theo, tọa độ HUD nhảy liên tục.
6. Player dừng hẳn khi chạm 4 tường — không xuyên.

---

## 4. Bài học kiến trúc
- **Kinematic + MovePosition không phải lựa chọn tốt cho top-down MMORPG trong Unity 6.** Dynamic + velocity đơn giản hơn, đáng tin hơn và là pattern phổ biến hơn. 
- **InGameLifetimeScope nên luôn đảm bảo container build được**, kể cả khi thiếu session. Fallback data cho test mode giúp iterate nhanh mà không cần chạy toàn bộ flow mỗi lần.
- **UI Resolution**: Luôn thiết lập `Canvas Scaler` chuẩn tỷ lệ màn hình ngay từ đầu và kiểm tra UI dưới góc nhìn Aspect Ratio tĩnh (ví dụ 16:9) thay vì Free Aspect để tránh bóp méo hiển thị.

---

## 5. Nợ kỹ thuật còn treo
- Player vẫn là sprite đỏ, chưa có animation di chuyển.
- Map test cứng trong scene, chưa có spawn point rõ ràng — Player luôn spawn tại `(0,0)`.
- `CharacterData.CharacterId` luôn là 0 — server chưa trả về định danh thật.
- `GameSession` chỉ giữ `CharacterData`; khi có inventory, quest, buff cần tách thêm service riêng.

---

## 6. Kế hoạch Day 8

### Ưu tiên cao
1. **Animation Player**
   - Thêm `Animator` vào Player Prefab.
   - Tạo clip `idle` và `walk`.
   - Viết `PlayerAnimationController` chuyển state dựa theo `_currentDirection`.
   - Lật sprite theo hướng X (Flip SpriteRenderer khi đi trái).

2. **Spawn Point**
   - Tạo `SpawnPoint` marker GameObject trong Map_Test.
   - `InGameSceneRoot` đọc vị trí SpawnPoint và ghi vào `GameSession.Current.SelectedCharacter.SpawnPosition` trước khi Player được inject.

### Ưu tiên trung bình
3. **Di chuyển folder InGame**
   - Đưa `InGameSceneRoot` và `InGameLifetimeScope` sang thư mục `Root/InGame/` để ranh giới kiến trúc rõ ràng.

4. **Làm giàu packet ListCharacters từ server**
   - Bổ sung `CharacterId`, `Level` vào packet để `CharacterData` được điền đúng từ đầu.

### Để sau
5. NPC test đứng yên trong map để kiểm tra collision layer.
6. Thiết kế sơ bộ combat: hit detection, damage number, HP bar.
7. Cân nhắc authoritative server movement: server validate vị trí, client predict rồi reconcile.