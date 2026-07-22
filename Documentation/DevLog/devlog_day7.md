# The World of Knights & Demons — Nhật ký Phát triển
## Day 7: Nền tảng Gameplay & Physics Collision

---

## 1. Mục tiêu đầu ngày

Củng cố kiến trúc đã đặt nền ở Day 6 và xác nhận vòng lặp gameplay cơ bản hoạt động ổn định:

```text
CharacterData đủ field cho DB sau này
  → PlayerController đọc stats từ data, không hardcode
  → Physics collision đáng tin cậy với tường
  → Map test xác nhận luồng di chuyển
```

---

## 2. Những gì đã hoàn thành

### 2.1. Dọn Bootstrap

- Xóa `Debug.Log` test trong `GameLifetimeScope.Awake()` — override này chỉ
  tồn tại để kiểm tra Awake được gọi, không còn giá trị.
- Cập nhật comment class `CharacterFlowController` phản ánh đúng thực tế:
  controller vừa điều phối panel vừa gọi `LoadSceneAsync` chuyển sang InGame,
  không còn thuần panel-based.
- Xóa Player và Camera cũ đang disable trong scene Bootstrap — không còn rác legacy.

### 2.2. Mở rộng CharacterData

- `CharacterData` từ 1 field (`CharacterName`) lên đầy đủ:
  - **Identity**: `CharacterId` (int, default 0 đến khi có DB), `CharacterName`
  - **Stats**: `Level`, `MaxHp`, `CurrentHp`, `MoveSpeed` (default 4f)
  - **World**: `SpawnPosition` (Vector2, default `Vector2.zero`)
- Constructor giữ nguyên signature `(string characterName)` để không
  breaking change toàn bộ nơi tạo `CharacterData`.
- `CharacterData` vẫn là pure data — không có logic gameplay bên trong.

### 2.3. CharacterData vào DI scope của InGame

- `InGameLifetimeScope` đăng ký `CharacterData` từ `GameSession.Current`:
  - Nếu có session (chạy qua Bootstrap): dùng data thật.
  - Nếu không có session (chạy thẳng InGame để test): tạo
    `CharacterData("TestCharacter")` mặc định — container build thành công,
    không crash.
- `PlayerController` inject `CharacterData` qua VContainer, không còn gọi
  `GameSession.Current` trực tiếp.

### 2.4. PlayerController không hardcode stats

- Xóa `_moveSpeed = 4f` hardcode duy nhất.
- Thêm `_defaultMoveSpeed` là `[SerializeField]` fallback cho Editor khi không
  có CharacterData.
- `MoveSpeed` property: `_characterData?.MoveSpeed ?? _defaultMoveSpeed`.
- `Start()` đặt Player tại `_characterData.SpawnPosition` — sẵn sàng nhận
  tọa độ từ server sau này.

### 2.5. Chuyển từ Kinematic sang Dynamic Rigidbody2D

- Phát hiện Kinematic + `MovePosition` không đáng tin trong Unity 6 cho
  top-down collision — Player xuyên qua tường dù đã bật Continuous và
  Use Full Kinematic Contacts.
- Chuyển sang **Dynamic** + `Gravity Scale = 0` + `Linear Damping = 10`:
  - Dynamic body tự xử lý collision với static collider.
  - `Linear Damping = 10` đảm bảo Player dừng ngay khi thả phím, không trượt.
  - `FixedUpdate` dùng `linearVelocity` thay vì `MovePosition`.
- Fix warning `CS0168` trong `NetworkClient.cs`:
  `catch (IOException ex)` → `catch (IOException)`.

### 2.6. Map Test 2D với Wall Collider

- Tạo `Map_Test` trong scene InGame:
  - `Floor`: sprite xanh lá, scale phù hợp camera, không có Collider.
  - 4 tường (`Wall_Top/Bottom/Left/Right`): sprite nâu đất, `BoxCollider2D`,
    bố trí bao quanh Floor.
- Camera Orthographic Size = 8; map tính toán vừa khung camera.
- Kết quả: Player di chuyển WASD tự do bên trong, dừng hẳn khi chạm tường.

---

## 3. Kết quả kiểm thử

Luồng đã chạy thành công:

1. Chạy từ Bootstrap → kết nối server → chọn nhân vật.
2. Scene chuyển sang InGame; log hiển thị đầy đủ `CharacterName`,
   `CharacterId`, `MoveSpeed`, `SpawnPosition`.
3. Player spawn tại `SpawnPosition` (hiện tại `Vector2.zero`).
4. WASD di chuyển mượt, Camera bám theo.
5. Player dừng hẳn khi chạm 4 tường — không xuyên.

---

## 4. Bài học kiến trúc

**Kinematic + MovePosition không phải lựa chọn tốt cho top-down MMORPG
trong Unity 6.** Dynamic + velocity đơn giản hơn, đáng tin hơn và là pattern
phổ biến hơn. Collision hoàn toàn do physics engine xử lý, không cần custom
sweep logic.

**InGameLifetimeScope nên luôn đảm bảo container build được**, kể cả khi
thiếu session. Fallback data cho test mode giúp iterate nhanh mà không cần
chạy toàn bộ flow mỗi lần.

---

## 5. Nợ kỹ thuật còn treo

- Player vẫn là sprite đỏ tạm, chưa có Prefab/animation.
- HUD chưa có — không biết tên nhân vật, trạng thái kết nối hay vị trí debug
  trong gameplay.
- Map test cứng trong scene, chưa có spawn point rõ ràng — Player luôn spawn
  tại `(0,0)`.
- `CharacterData.CharacterId` luôn là 0 — server chưa trả về định danh thật.
- `GameSession` chỉ giữ `CharacterData`; khi có inventory, quest, buff cần
  tách thêm service riêng.

---

## 6. Việc cần làm tiếp theo — Còn lại Day 7

1. **Player Prefab**: đóng gói Player GameObject thành Prefab trong
   `Assets/_Project/Prefabs/`.
2. **HUD tối thiểu**: tên nhân vật, trạng thái kết nối, vị trí debug.

---

## 7. Kế hoạch Day 8

### Ưu tiên cao

1. **Animation Player**
   - Thêm `Animator` vào Player Prefab.
   - Tạo clip `idle` và `walk`.
   - Viết `PlayerAnimationController` chuyển state dựa theo `_currentDirection`:
     - `direction == Vector2.zero` → idle
     - `direction != Vector2.zero` → walk
   - Lật sprite theo hướng X (Flip SpriteRenderer khi đi trái).

2. **Spawn Point**
   - Tạo `SpawnPoint` marker GameObject trong Map_Test.
   - `InGameSceneRoot` đọc vị trí SpawnPoint và ghi vào
     `GameSession.Current.SelectedCharacter.SpawnPosition` trước khi
     Player được inject — tách rời vị trí spawn khỏi `Vector2.zero` hardcode.

3. **HUD tối thiểu**
   - Canvas trong InGame scene (Screen Space — Overlay).
   - Hiển thị: tên nhân vật, trạng thái kết nối, tọa độ Player (debug).
   - `InGameHUD` component inject `CharacterData` và `GameSession`.

### Ưu tiên trung bình

4. **Di chuyển folder InGame**
   - `InGameSceneRoot` và `InGameLifetimeScope` hiện nằm trong
     `Root/Bootstrap/` — sai vị trí về mặt ý nghĩa.
   - Di chuyển sang `Root/InGame/` để ranh giới Bootstrap / InGame rõ ràng.

5. **Làm giàu packet ListCharacters từ server**
   - Server hiện trả về `CharacterName` trong `ListCharactersResponse`.
   - Bổ sung `CharacterId`, `Level` vào packet để `CharacterData` được điền
     đúng từ đầu, không cần default 0.

### Để sau

6. NPC test đứng yên trong map để kiểm tra collision layer.
7. Thiết kế sơ bộ combat: hit detection, damage number, HP bar.
8. Cân nhắc authoritative server movement: server validate vị trí,
   client predict rồi reconcile.
