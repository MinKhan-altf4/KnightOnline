# The World of Knights & Demons — Nhật ký Phát triển
## Day 2: Core Architecture Foundation

---

## 1. Tóm tắt những gì đã hoàn thành hôm nay

### 1.1. Cấu trúc Repository (Monorepo Client-Server)

Chuyển từ cấu trúc Unity project đơn lẻ sang monorepo tách biệt rõ ràng:

```
KnightOnline/                  <- git repo root
├── KnightClient/              <- Unity project (2D, URP)
│   └── Assets/_Project/
├── KnightServer/               <- .NET Console App (server giả lập, học tập)
├── Documentation/               <- tài liệu chung, KHÔNG nằm trong Assets
└── .gitignore
```

**Quyết định kiến trúc quan trọng:**
- Server hiện tại là **giả lập** (simulated), viết bằng .NET Console App riêng biệt, KHÔNG dùng framework production (Mirror/FishNet) — mục đích là rèn tư duy "client không tự quyết định state" trước khi đủ kinh nghiệm dùng framework thật.
- Shared code (packet/DTO dùng chung Client-Server) đặt vật lý trong `KnightClient/Assets/_Project/Scripts/Shared/`, Server `.csproj` sẽ tham chiếu tới bằng glob include — tránh việc phải build DLL thủ công mỗi lần sửa.
- Không dùng `Resources.Load` — dự kiến dùng Addressables khi bắt đầu quản lý asset.

### 1.2. Assembly Definition — Dependency Graph 1 chiều

8 module tách biệt, enforce bằng asmdef, ngăn vi phạm kiến trúc bằng compile-time thay vì tự giác:

| Assembly | Phụ thuộc | Vai trò |
|---|---|---|
| `KnightOnline.Client.Core` | VContainer, UniTask | Hạ tầng lõi: DI, EventBus, cơ chế nền tảng |
| `KnightOnline.Client.Data` | *(không phụ thuộc)* | Data model thuần Client-side (ScriptableObject config...) |
| `KnightOnline.Client.Shared` | *(không phụ thuộc)* | Packet/DTO giao tiếp Client-Server |
| `KnightOnline.Client.Input` | Core, Unity.InputSystem | Xử lý input người chơi |
| `KnightOnline.Client.Network` | Core, Shared, UniTask | Giao tiếp với Server |
| `KnightOnline.Client.Audio` | Core | Điều khiển phát âm thanh |
| `KnightOnline.Client.Gameplay` | Core, Data, Input, Network, Audio | Logic gameplay — **tuyệt đối không biết UI** |
| `KnightOnline.Client.UI` | Core, Data, Gameplay, TextMeshPro | Giao diện người dùng |

Quy ước namespace: `KnightOnline.Client.*` cho Client, dự kiến `KnightOnline.Server.*` cho Server sau này — tách rõ để không nhầm lẫn khi cả 2 phát triển song song.

### 1.3. Bootstrap & Dependency Injection

- `Bootstrap.unity` là scene khởi động mặc định.
- `GameLifetimeScope` (kế thừa `LifetimeScope` của VContainer) — Composition Root, nơi đăng ký toàn bộ service của game.
- `GameBootstrap` (kế thừa `IAsyncStartable`) — Entry Point, khởi tạo game qua UniTask async flow.
- Không dùng Singleton tĩnh (`X.Instance`) ở bất kỳ đâu — mọi service đăng ký qua DI container, đảm bảo test được và không có global mutable state không kiểm soát.

### 1.4. EventBus — Hệ thống giao tiếp Decoupled

Xây dựng xong cơ chế truyền tin giữa các module mà không vi phạm dependency graph:

- `IGameEvent` — marker interface cho mọi event payload.
- `IEventBus` / `EventBus` — publish/subscribe strongly-typed (không dùng string event, tránh lỗi runtime).
- `EventBinding` — subscription trả về `IDisposable`, bắt buộc dispose để tránh memory leak (rủi ro nghiêm trọng với client chạy hàng giờ liền).
- Đăng ký làm Singleton qua `GameLifetimeScope`, không phải static instance.

**Nguyên tắc giao tiếp đã chốt:**
- UI → Gameplay: gọi trực tiếp qua interface (command/lệnh).
- Gameplay → UI: bắt buộc qua EventBus (notification), vì Gameplay không được reference UI.

Đã verify hoạt động đúng qua log thực tế: DI inject `IEventBus` vào `GameBootstrap`, publish/subscribe đồng bộ chính xác.

**Trạng thái hiện tại:** Đang xử lý lỗi compile `CS0234` — 1 trong 4 file EventBus có khả năng namespace chưa khớp `KnightOnline.Client.Core.Events` sau đợt đổi tên hàng loạt. Cần xác nhận trước khi đóng ngày.

---

## 2. Ước tính % hoàn thành toàn bộ game — nhìn thẳng vào thực tế

Đây là phần quan trọng để tránh ảo tưởng tiến độ — một MMORPG hoàn chỉnh bao gồm rất nhiều hệ thống lớn (server networking thật, database, combat, inventory, quest, crafting, trading, PvP, guild, event theo mùa, anti-cheat, matchmaking, content pipeline...).

**Ước tính thực tế: khoảng 1-2% tổng khối lượng công việc của một MMORPG hoàn chỉnh.**

Điều này **không có nghĩa là tiến độ chậm** — ngược lại, đây là giai đoạn quan trọng nhất và dễ bị người mới bỏ qua nhất:

- Những gì đã làm hôm nay không phải "feature" — đó là **nền móng kiến trúc** quyết định toàn bộ project có sống được 3-5 năm hay sụp đổ sau vài tháng vì nợ kỹ thuật.
- Sửa sai kiến trúc ở giai đoạn này (như đổi namespace) tốn vài giờ. Sửa sai kiến trúc khi đã có 500 file code sẽ tốn hàng tuần, thậm chí buộc phải viết lại từ đầu.
- Phần lớn dự án indie MMORPG thất bại không phải vì thiếu ý tưởng, mà vì bỏ qua bước này — code thẳng vào gameplay, để rồi 6 tháng sau không thể mở rộng được nữa.

**Đừng đánh giá tiến độ bằng số lượng feature nhìn thấy được — đánh giá bằng việc "nếu 1 năm nữa cần thêm 1 hệ thống lớn, kiến trúc hiện tại có chịu được không".** Với những gì đã dựng (dependency graph rõ ràng, DI, EventBus decoupled, tách Client-Server từ đầu), câu trả lời hiện tại là **có**.

---

## 3. Các bước đang dẫn đến đâu — bức tranh tổng thể

```
[Đã xong / đang xử lý nốt lỗi]
Bootstrap + DI + EventBus
        │
        ▼
[Tiếp theo gần nhất]
Networking Layer thật (giả lập Server)
        │
        ▼
Login Flow (UI + Network kết nối)
        │
        ▼
Character/Player Data Model (Data module)
        │
        ▼
Scene Flow chính thức (Boot → Login → CharSelect → InGame)
        │
        ▼
[Giai đoạn Gameplay đầu tiên]
Player Movement + Camera (Gameplay module)
        │
        ▼
Combat cơ bản (nếu đã tạo folder Combat — cần xác nhận vị trí trong dependency graph)
        │
        ▼
[Giai đoạn xa hơn — chưa bắt đầu]
Inventory / Equipment → Loot System → Crafting →
Boss/PvE → PvP → Trading → Guild → Seasonal Event → Anti-cheat validation
```

Mọi hệ thống ở "giai đoạn xa hơn" đều sẽ **giao tiếp qua EventBus** (Gameplay → UI) và **qua Network layer** (Client → Server) đã và đang được dựng hôm nay — đây là lý do vì sao không được phép làm ẩu ở bước nền tảng.

---

## 4. Việc cần làm ngay (trước khi đóng Day 2)

1. **Xác định và sửa lỗi `CS0234`** — kiểm tra namespace của cả 4 file trong `Core/Events/`, đảm bảo đồng nhất `KnightOnline.Client.Core.Events`.
2. Compile sạch, chạy lại test log EventBus (đã xóa code test tạm, chỉ còn `GameBootstrap` sạch).
3. Làm rõ vị trí folder `Combat` (phát hiện trong ảnh trước) — thuộc `Gameplay` hay là module mới cần thêm vào dependency graph.
4. Cập nhật `Architecture.md` với: bảng dependency graph 8 module, quy ước namespace `.Client`/`.Server`, quyết định EventBus (vị trí, nguyên tắc UI/Gameplay giao tiếp).
5. Commit toàn bộ thay đổi hôm nay theo từng nhóm logic riêng (không gộp 1 commit khổng lồ):
   - `refactor: restructure repo into Client/Server monorepo`
   - `chore: untrack IDE-generated files, ignore .qodo`
   - `feat: setup 8-module assembly definition architecture`
   - `feat: implement decoupled EventBus system in Core`
   - `refactor: standardize namespace to KnightOnline.Client.*`

## 5. Việc cần làm tiếp theo (Day 3 — đề xuất)

Chưa bắt đầu cho đến khi Day 2 đóng sạch (mục 4 hoàn tất):

- Thiết kế packet protocol tối thiểu (`ConnectRequest`, `ConnectResponse`) trong `Shared/`.
- Viết Server giả lập: TCP listener + tick loop rỗng (chưa có gameplay logic).
- Viết `NetworkClient` phía Client (trong module `Network`), kết nối thử tới Server giả lập.
- Login Scene: UI thuần túy, gọi qua `NetworkClient` — không tự xác thực phía Client.

---

*Tài liệu này nên lưu tại `Documentation/DevLog/Day2.md` để làm hồ sơ theo dõi tiến độ studio thật — không xóa sau khi đọc, đây là tài liệu tham chiếu cho các quyết định kiến trúc đã chốt.*
