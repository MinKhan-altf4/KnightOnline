# The World of Knights & Demons — Nhật ký Phát triển
## Day 6: Tách InGame Scene & Player Movement đầu tiên

---

## 1. Mục tiêu đầu ngày

Hoàn tất ranh giới đầu tiên giữa **frontend** và **gameplay**:

```text
Bootstrap (kết nối, tạo/chọn nhân vật)
  → chọn nhân vật
InGame (Player, Camera, gameplay)
```

Mục tiêu không phải dựng map hay combat, mà là xác nhận luồng chuyển scene
và điều khiển Player có thể hoạt động độc lập trong scene gameplay.

---

## 2. Những gì đã hoàn thành

### 2.1. Tạo scene gameplay riêng

- Thêm `InGame.unity` và đăng ký `Bootstrap`, `Login`, `InGame` trong Build
  Settings.
- `CharacterFlowController` không còn bật `InGamePanel` trong Bootstrap. Sau
  khi nhận `CharacterSelectedEvent`, controller lưu nhân vật được chọn và gọi
  `LoadSceneAsync("InGame", LoadSceneMode.Single)`.
- `InGameSceneRoot` là entry object của scene gameplay, ghi log tên nhân vật
  đang được load để kiểm tra handoff.

### 2.2. Giữ state chọn nhân vật khi đổi scene

- Thêm `GameSession` dưới dạng component `DontDestroyOnLoad`.
- `GameSession` giữ `SelectedCharacter` trước khi Bootstrap bị unload; scene
  InGame có thể đọc state này mà không phụ thuộc vào UI frontend.
- Đây là bước tách lifecycle quan trọng: UI của Bootstrap bị hủy khi đổi scene,
  còn session cần thiết cho gameplay vẫn tồn tại.

### 2.3. DI scope riêng cho InGame

- Thêm `InGameLifetimeScope` trên `InGameSceneRoot`.
- Scope này chỉ đăng ký dependency gameplay:
  - `IMovementInputProvider → KeyboardMovementInput`
  - `PlayerController` trong hierarchy của InGame
- Gỡ hai đăng ký trên khỏi `GameLifetimeScope`. Bootstrap không còn sở hữu
  input hoặc Player.
- Không đăng ký lại network, UI hay `GameBootstrap` trong InGame, nên chuyển
  scene không tạo kết nối server lần hai.

### 2.4. Player và Camera test trong InGame

- Tạo Player sprite màu đỏ tạm thời tại `(0, 0, 0)`.
- Player có `PlayerController`, `Rigidbody2D` Kinematic, Gravity Scale `0` và
  `BoxCollider2D`.
- `KeyboardMovementInput` giữ nguyên hỗ trợ WASD và phím mũi tên; vector chéo
  được chuẩn hóa để tốc độ không nhanh hơn khi đi chéo.
- Tạo `Main Camera` Orthographic tại `(0, 0, -10)`, thêm `CameraFollow` và gán
  target là Transform của Player.
- Player và Camera cũ trong Bootstrap đã được disable để có thể rollback trong
  khi kiểm thử; sẽ xóa hẳn sau khi scene mới ổn định.

---

## 3. Kết quả kiểm thử

Luồng đã chạy thành công:

1. Chạy từ `Bootstrap` và kết nối server.
2. Chọn nhân vật.
3. Scene chuyển sang `InGame`.
4. `InGameSceneRoot` nhận được tên nhân vật đã chọn.
5. Player hiển thị, di chuyển bằng WASD/phím mũi tên và Camera bám theo.

Lưu ý: `dotnet build` trực tiếp chưa phải cách kiểm tra phù hợp cho Unity ở
thời điểm thêm script mới, vì `.csproj` do Unity sinh ra chưa được refresh và
thiếu file `Temp/.../project.assets.json`. Mở Unity để import/compile là bước
xác thực đúng; Play Mode đã xác nhận feature hoạt động.

---

## 4. Bài học kiến trúc

`GameLifetimeScope` không nên tiếp tục phình to thành nơi đăng ký mọi thứ.
Khi gameplay được load bằng scene riêng, dependency dành riêng cho gameplay
phải sống trong scope của InGame. Nếu giữ `KeyboardMovementInput` và
`PlayerController` ở Bootstrap, Player trong scene mới sẽ không được inject
khi Bootstrap đã bị unload.

Mẫu hiện tại:

```text
Bootstrap scope
  ├─ EventBus / NetworkClient / GameSession / frontend UI
  └─ chuyển scene sau khi chọn nhân vật

InGame scope
  ├─ KeyboardMovementInput
  └─ PlayerController
```

Mẫu này là nền tảng để bổ sung map, NPC, quái, combat và HUD trong InGame mà
không kéo dependency gameplay quay lại Bootstrap.

---

## 5. Nợ kỹ thuật còn treo

- `Login.unity` hiện vẫn trống; frontend tạo/chọn nhân vật còn nằm trong
  Bootstrap. Cần tách tiếp khi có UI login riêng.
- Player đang là sprite test, chưa có prefab/animation/character visual thật.
- Player và Camera cũ trong Bootstrap đang disable, chưa xóa hẳn.
- InGame chưa có map, va chạm môi trường, spawn point hay HUD.
- `GameSession` mới giữ `CharacterData` tối thiểu; khi server có định danh
  nhân vật thật cần bổ sung `CharacterId`, stats và dữ liệu spawn.

---

## 6. Việc cần làm tiếp theo — Day 7

1. Dọn Bootstrap: xóa Player, Camera và `InGamePanel` cũ sau khi chốt scene
   flow.
2. Tạo map test 2D cùng collider tường để xác nhận collision của Player.
3. Tạo Player prefab thay cho sprite test và chuẩn bị animation idle/walk.
4. Thiết kế HUD tối thiểu trong InGame: tên nhân vật, trạng thái kết nối và
   vị trí debug.

