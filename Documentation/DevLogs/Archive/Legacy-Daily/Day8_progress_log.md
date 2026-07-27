# The World of Knights & Demons — Nhật ký Phát triển
## Day 8: Refactor kiến trúc — AppLifetimeScope & Parent-Child Scope

---

## 1. Bối cảnh đầu ngày

Ngày này **không nằm trong kế hoạch gốc** (`devlog_day7.md` đề ra Animation +
SpawnPoint cho Day 8). Kế hoạch bị hoãn lại vì review kỹ Day 6/Day 7 (viết ở
phiên làm việc khác) phát hiện 1 lỗ hổng kiến trúc nghiêm trọng cần sửa
**trước khi** xây thêm bất kỳ tính năng gameplay nào lên trên — đúng nguyên
tắc xuyên suốt dự án: sửa nền móng luôn ưu tiên hơn tính năng mới, và sửa
càng sớm càng rẻ.

---

## 2. Vấn đề phát hiện — Service toàn cục "chết" khi chuyển scene

### 2.1. Hiện trạng trước khi sửa

`CharacterFlowController` (Day 6) chuyển từ Panel-based sang
`SceneManager.LoadSceneAsync("InGame", LoadSceneMode.Single)` khi người chơi
chọn xong nhân vật. Đây là thay đổi hợp lý (tách frontend/gameplay thành 2
scene riêng), nhưng kéo theo hệ quả không được xử lý:

- `IEventBus`, `NetworkClient` được đăng ký trong `GameLifetimeScope`
  (thuộc scene `Bootstrap`).
- `LoadSceneMode.Single` **unload hoàn toàn** scene `Bootstrap` — bao gồm
  cả container DI của `GameLifetimeScope`.
- `NetworkClient` sống sót nhờ `DontDestroyOnLoad`, nhưng vẫn giữ tham chiếu
  tới **instance `EventBus` cũ** — instance này không hề bị hủy (còn tham
  chiếu tới nó), nhưng **không còn subscriber nào** vì mọi UI ở Bootstrap đã
  bị destroy cùng scene.
- `InGameLifetimeScope` không đăng ký lại `IEventBus` — không có cách nào
  kết nối lại với instance cũ.

**Hậu quả:** mọi packet Server gửi trong lúc chơi (sync vị trí, chat, combat,
disconnect...) publish vào 1 EventBus không ai lắng nghe — im lặng, không
lỗi, không crash. Loại bug nguy hiểm nhất: chỉ lộ ra khi thực sự cần dùng
tính năng network trong gameplay, rất muộn để phát hiện qua test thông
thường.

### 2.2. Vấn đề phụ đã tự phát hiện và tự sửa đúng hướng (trước khi tôi review)

`GameSession` (giữ `CharacterData` đã chọn) đã dùng đúng
`DontDestroyOnLoad` + static `Current` — nhưng chỉ truy cập tĩnh ở **đúng 1
điểm** (`InGameLifetimeScope.Configure()`), còn lại đều qua constructor
injection đàng hoàng. Đây không phải vi phạm tùy tiện, nhưng cùng gốc vấn đề
với `IEventBus`/`NetworkClient` — thiếu 1 tầng kiến trúc chung để quản lý
lifecycle xuyên-scene, buộc phải vá bằng static property.

---

## 3. Giải pháp — Parent-Child LifetimeScope, 3 tầng

```text
AppLifetimeScope (scene "App", load đầu tiên, DontDestroyOnLoad)
  ├─ IEventBus
  ├─ NetworkClient
  └─ GameSession
       │
       ├─ GameLifetimeScope (Child, scene Bootstrap)
       │    └─ CharacterService, CharacterFlowController, UI panels...
       │
       └─ InGameLifetimeScope (Child, scene InGame)
            └─ PlayerController, InGameHUD...
```

### 3.1. Scene `App.unity` — scene khởi động mới, đứng đầu Build Profiles

Chỉ chứa 1 GameObject `AppLifetimeScope`, không UI, không Camera thật (chỉ
dùng để khởi tạo service gốc rồi tự load `Bootstrap` qua
`LoadSceneMode.Additive` — bắt buộc dùng Additive, không phải Single, để
không tự giết chính Parent Scope vừa tạo).

### 3.2. Lỗi phát sinh trong lúc refactor — bài học quan trọng nhất trong ngày

**Giả định sai ban đầu:** VContainer tự động nhận Parent theo "scope đang
active gần nhất" khi Child ở khác scene. **Thực tế: sai** — xác nhận bằng
lỗi thật `VContainerException: No such registration of type: IEventBus` khi
`CharacterFlowController` (trong `GameLifetimeScope`, Child) cố resolve
`IEventBus` (đăng ký ở `AppLifetimeScope`, Parent) mà không có liên kết
tường minh giữa 2 scope.

**Cách sửa đúng:** gán Parent thủ công qua `parentReference.Object` trong
`Awake()`, **trước khi** gọi `base.Awake()` (vì `base.Awake()` mới là nơi
VContainer thực sự `Build()` container):

```csharp
protected override void Awake()
{
    var appScope = FindAnyObjectByType<AppLifetimeScope>();
    if (appScope != null) parentReference.Object = appScope;
    base.Awake();
}
```

Áp dụng cho cả `GameLifetimeScope` và `InGameLifetimeScope`.

---

## 4. Trạng thái hiện tại — CHƯA XÁC NHẬN XONG

Đây là điểm khác biệt so với các Day trước: **Day 8 kết thúc phiên làm việc
ở giữa chừng bước verify**, chưa có bằng chứng cuối cùng rằng bản sửa
`parentReference` chạy đúng end-to-end. Ghi rõ để không nhầm là đã hoàn tất.

---

## 5. Việc cần làm để ĐÓNG Day 8 (bắt buộc trước khi coi là xong)

- [ ] Sửa `GameLifetimeScope.cs`: thêm `Awake()` override gán
      `parentReference.Object` trước `base.Awake()`.
- [ ] Sửa `InGameLifetimeScope.cs`: làm tương tự.
- [ ] Compile lại, xác nhận Unity Console sạch (0 lỗi đỏ) — kiểm tra đúng
      Unity Console, không tin Problems panel VS Code.
- [ ] Play từ scene `App` (không phải mở trực tiếp `Bootstrap` rồi Play).
- [ ] Xác nhận toàn bộ 4 lỗi `NullReferenceException` (`CharacterCreationView`,
      `ConnectionStatusView`, `CharacterSelectView` — hệ quả của `IEventBus`
      resolve fail) biến mất hoàn toàn.
- [ ] Xác nhận log chuỗi quen thuộc xuất hiện đúng: Awake → Bootstrap →
      Network → kết nối thành công.
- [ ] Đi hết luồng: kết nối → tạo/chọn nhân vật → chuyển sang `InGame`.
- [ ] Tại `InGame`, xác nhận `InGameLifetimeScope` resolve `GameSession`
      đúng (không còn dùng `GameSession.Current` tĩnh nữa), Player + HUD
      hoạt động như Day 7.
- [ ] Nếu mọi bước trên pass — cập nhật `Architecture.md` thêm mục
      "Multi-Scene DI: Parent-Child LifetimeScope", ghi lý do + cách làm +
      cạm bẫy `parentReference` cần gán trước `base.Awake()`.
- [ ] Commit riêng: `refactor: introduce AppLifetimeScope as root DI scope
      across scenes`.

---

## 6. Nợ kỹ thuật phát sinh thêm

- Cần xác nhận `NetworkClient` (đã `DontDestroyOnLoad` qua `AppLifetimeScope`
  giờ) không bị tạo trùng lặp nếu người chơi quay lại `App`/`Bootstrap`
  bằng cách nào đó (chưa có luồng thật cho việc này, nhưng cần lưu ý).
- `AppLifetimeScope` hiện dùng `FindAnyObjectByType` mỗi lần Child `Awake()`
  — chấp nhận được vì chỉ chạy 1 lần lúc chuyển scene, không phải mỗi frame,
  nhưng ghi nhận nếu sau này có nhiều Child Scope hơn, cân nhắc cache lại.

---

## 7. Kế hoạch Day 9 (sau khi Day 8 được xác nhận đóng hoàn toàn)

Đây là kế hoạch Animation + SpawnPoint vốn dự kiến cho Day 8 gốc (từ
`devlog_day7.md`), dời sang Day 9 vì Day 8 bị chiếm bởi refactor kiến trúc.

### Ưu tiên cao
1. **Animation Player**
   - Thêm `Animator` vào Player Prefab.
   - Tạo clip `idle` và `walk`.
   - `PlayerAnimationController` chuyển state dựa theo `_currentDirection`
     (đọc từ `PlayerController`, không tự ý input riêng).
   - Lật sprite theo hướng X khi đi trái (Flip `SpriteRenderer`).

2. **Spawn Point**
   - `SpawnPoint` marker GameObject trong `Map_Test`.
   - `InGameSceneRoot` đọc vị trí `SpawnPoint`, ghi vào
     `CharacterData.SpawnPosition` trước khi `PlayerController` được inject
     — cần kiểm tra thứ tự này hoạt động đúng với kiến trúc
     `AppLifetimeScope` mới (Parent-Child), không giả định lại
     `GameSession.Current` tĩnh.

### Ưu tiên trung bình
3. Di chuyển `InGameSceneRoot`/`InGameLifetimeScope` sang `Root/InGame/` để
   ranh giới thư mục khớp ranh giới kiến trúc (đã đề ra ở Day 7, chưa làm).
4. Làm giàu packet `ListCharacters`: thêm `CharacterId`, `Level` từ Server.

### Để sau
5. NPC test đứng yên, kiểm tra collision layer.
6. Sơ bộ combat: hit detection, damage number, HP bar.
7. Cân nhắc authoritative server movement (client predict + reconcile) —
   quan trọng hơn với kiến trúc `Dynamic Rigidbody2D` đã chọn ở Day 7, vì
   physics engine client tự do di chuyển có thể lệch với vị trí server xác
   nhận sau này.

---

*Tài liệu này nên lưu tại `Documentation/DevLog/Day8.md`, nối tiếp
`Day6_Progress_Log.md`, `devlog_day7.md`.*