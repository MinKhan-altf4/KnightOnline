# The World of Knights & Demons — Nhật ký Phát triển
## Day 3: Networking Layer — Client ↔ Server ↔ EventBus ↔ UI

---

## 1. Mục tiêu đề ra đầu ngày

Xây xong đường ống giao tiếp Client ↔ Server tối thiểu: Client gửi request, Server nhận, xử lý, trả lời, Client nhận phản hồi, UI hiển thị được kết quả — toàn bộ qua đúng kiến trúc DI + EventBus đã dựng ở Day 2, không đi tắt.

**Kết quả: mục tiêu đã đạt được trọn vẹn**, kèm theo 1 vấn đề kiến trúc phát sinh giữa chừng (race condition) đã được giải quyết bằng 1 pattern mới (Sticky Event) — không có trong kế hoạch ban đầu nhưng là bổ sung cần thiết và đúng đắn.

---

## 2. Những gì đã hoàn thành

### 2.1. Packet Protocol (`Shared/Packets/`)

- `PacketType.cs` — enum định danh loại packet.
- `ConnectRequestPacket.cs`, `ConnectResponsePacket.cs` — packet đầu tiên, dùng `required` modifier thay vì nullable warning, đảm bảo an toàn dữ liệu ngay từ tầng packet.
- `PacketEnvelope.cs` — lớp bọc ngoài mang `PacketType` + `Payload` (JSON string), giúp bên nhận biết cách deserialize.
- Server (`KnightServer/Server.csproj`) tham chiếu trực tiếp source từ `Shared/` qua glob include — không build DLL riêng, đảm bảo 1 nguồn sự thật duy nhất giữa Client và Server.

### 2.2. Length-Prefixed Framing — kỹ thuật cốt lõi

Cả Server (`Program.cs`) và Client (`NetworkClient.cs`) dùng chung nguyên lý: gửi 4 byte độ dài trước, sau đó mới gửi payload JSON. Giải quyết đúng bản chất TCP là byte stream liên tục, không có khái niệm "message" tự nhiên.

### 2.3. Server (`KnightServer/Program.cs`)

- `TcpListener` lắng nghe port 7777, vòng lặp `Accept` liên tục, mỗi client xử lý trên 1 Task riêng (fire-and-forget có kiểm soát).
- Vòng lặp đọc packet liên tục cho mỗi client (không chỉ đọc 1 lần).
- Try-catch ở tầng ngoài cùng mỗi client handler — 1 client lỗi không làm sập toàn Server.
- Thuần .NET Console App, không có bất kỳ tham chiếu `UnityEngine` nào — đúng nguyên tắc tách biệt Server khỏi Unity.

### 2.4. Client (`NetworkClient.cs`)

- `MonoBehaviour`, kết nối TCP async qua UniTask, vòng lặp `ReceiveLoopAsync` chạy nền (fire-and-forget có `CancellationToken`).
- Inject `IEventBus` qua Method Injection (`[Inject] Construct()`) — vì `MonoBehaviour` không dùng được constructor injection.
- Đăng ký vào DI qua `builder.RegisterComponentOnNewGameObject<NetworkClient>(Lifetime.Singleton, ...).DontDestroyOnLoad()` — VContainer tự tạo GameObject, tự inject, không dùng `AddComponent` thủ công.
- Xử lý cleanup đầy đủ: `Disconnect()` dispose stream/socket/token đúng thứ tự, gọi trong `OnDestroy()`.

### 2.5. EventBus — nâng cấp quan trọng: Sticky Event

Phát hiện vấn đề thực tế: `NetworkClient` publish `ServerConnectionResultEvent` **trước khi** `ConnectionStatusView` kịp subscribe (do khác biệt thời điểm khởi tạo giữa `RegisterComponentOnNewGameObject` và `RegisterComponentInHierarchy`) — khiến UI bỏ lỡ event, đứng yên không cập nhật.

**Giải pháp:** thêm `IStickyGameEvent` marker interface. `EventBus` lưu lại giá trị gần nhất của mọi event thuộc loại này; khi có subscriber mới đăng ký, nếu đã có giá trị cached, gọi ngay handler với giá trị đó — subscriber tới muộn vẫn nhận được trạng thái hiện tại thay vì phải chờ lần publish tiếp theo (không bao giờ tới).

Nguyên tắc phân loại đã chốt:
- **Sticky** (`IStickyGameEvent`): đại diện trạng thái liên tục — trạng thái kết nối, HP hiện tại, số lượng item... Cần cho subscriber muộn.
- **Thường** (`IGameEvent`): đại diện hành động tức thời — mất kết nối, quái chết, rớt đồ... Không nên phát lại.

### 2.6. UI (`ConnectionStatusView.cs`)

- Lần đầu tiên module `UI` dùng `EventBus` theo đúng nguyên tắc thiết kế: subscribe `ServerConnectionResultEvent`/`ServerDisconnectedEvent`, không tham chiếu trực tiếp vào `NetworkClient`.
- Đăng ký qua `RegisterComponentInHierarchy` (khác với `NetworkClient` vì đây là GameObject đặt sẵn trong scene, không do VContainer tạo mới).
- Subscribe trong `Start()` (không phải `Awake()`) — đảm bảo `[Inject] Construct()` đã chạy xong.
- Dispose đúng cách trong `OnDestroy()`.

### 2.7. Kiến trúc Assembly — module thứ 9: `Root`

Phát hiện và sửa 1 vi phạm dependency graph tiềm ẩn: `GameLifetimeScope` (Composition Root) cần biết về **mọi** module để đăng ký DI — nếu đặt trong `Core`, sẽ buộc `Core` (hạ tầng thấp nhất) phải reference `UI` (tầng cao nhất), phá vỡ nguyên tắc 1 chiều.

**Quyết định:** tách riêng `KnightOnline.Client.Root` — module duy nhất được phép reference toàn bộ 8 module còn lại. Đây là ngoại lệ có chủ đích của luật dependency 1 chiều, đúng vai trò Composition Root.

**Bảng dependency graph cập nhật (9 module):**

| Assembly | Phụ thuộc |
|---|---|
| `Core` | VContainer, UniTask |
| `Data` | Core, Shared *(cập nhật — thêm 2 reference còn thiếu)* |
| `Shared` | *(không phụ thuộc)* |
| `Input` | Core, Unity.InputSystem |
| `Network` | Core, Shared, Data, VContainer, UniTask *(cập nhật — thêm Data, VContainer)* |
| `Audio` | Core |
| `Gameplay` | Core, Data, Input, Network, Audio, VContainer, UniTask |
| `UI` | Core, Data, Shared, Gameplay, VContainer, UniTask, TextMeshPro *(cập nhật — thêm Shared)* |
| `Root` | Tất cả 8 module + VContainer, UniTask — **Composition Root, ngoại lệ hợp lệ** |

---

## 3. Lỗi đã gặp và bài học kỹ thuật

| Lỗi | Nguyên nhân | Bài học |
|---|---|---|
| `CS8618` (3 warning) | Property không nullable thiếu giá trị khởi tạo | Dùng `required` modifier thay vì tắt warning — sửa tận gốc, không giấu vấn đề |
| Unity vào Safe Mode | Lỗi compile nghiêm trọng trước khi cập nhật asmdef | Sửa lỗi trước, Safe Mode tự thoát sau khi compile sạch |
| "Corrupted Library" | Cache Library bị hỏng sau nhiều lần đổi asmdef liên tục | Xóa `Library/`, `Temp/`, để Unity rebuild — an toàn vì đây chỉ là cache |
| `CS1503` type mismatch kỳ lạ | `Data.asmdef` có `references: []` rỗng — thiếu reference tới `Shared` dù dùng gián tiếp qua đường vòng | Khi lỗi asmdef khó hiểu, xem trực tiếp JSON thô của `.asmdef`, đừng chỉ tin Inspector UI |
| `CS0246` liên tục "giả" | VS Code Problems panel cache cũ, không đồng bộ kịp Unity | **Unity Console luôn là nguồn sự thật cuối cùng**, không phải VS Code Problems panel |
| `NullReferenceException` trong `Awake()` | `RegisterComponentInHierarchy` không đảm bảo `[Inject]` chạy trước `Awake()` | Dùng `Start()` cho logic phụ thuộc dependency đã inject, không dùng `Awake()` |
| UI không cập nhật dù không lỗi | Race condition: Network publish trước khi UI kịp subscribe | Sinh ra pattern `IStickyGameEvent` — giải pháp kiến trúc bền vững, không phải vá tạm |

---

## 4. Ước tính % hoàn thành tổng thể

**Cập nhật từ 1-2% (cuối Day 2) lên khoảng 3-4%.**

Đây là bước tiến quan trọng hơn con số thể hiện: Day 3 chứng minh được **nguyên lý cốt lõi nhất của kiến trúc MMORPG** — Client và Server là 2 tiến trình độc lập, giao tiếp qua giao thức tự định nghĩa, và toàn bộ luồng dữ liệu nội bộ Client tuân thủ decoupling qua EventBus. Đây là nền móng mọi hệ thống gameplay sau này (login thật, combat, inventory, trade...) sẽ xây dựng trên đó — không phải thêm 1 feature, mà là xác thực cả bộ khung chịu lực.

---

## 5. Việc cần làm tiếp theo (Day 4 — đề xuất)

1. **Dọn dẹp verbose log** trong `NetworkClient.cs` — bọc log chi tiết từng byte trong flag bật/tắt được, tránh nghẽn hiệu năng khi có traffic thật.
2. **Mở rộng `ConnectResult`** — thêm `NetworkError` để phân biệt "server từ chối" và "lỗi mạng thật sự" (hiện đang tạm dùng `ServerFull` cho cả 2 trường hợp).
3. **Login Scene thật** — UI nhập username/password (chưa cần xác thực thật, server giả lập vẫn chỉ echo lại).
4. **Character Data Model** — bắt đầu dùng `Data` module cho mục đích thật đầu tiên (không chỉ chứa `Events`).
5. Cân nhắc: có cần `Reconnect` logic cơ bản khi mất kết nối giữa chừng không, hay để dành cho giai đoạn sau.

## 6. Việc cần làm ngay (dọn dẹp trước khi đóng Day 3)

- [ ] Cập nhật `Architecture.md`: thêm mục Networking Layer, bảng dependency 9 module, pattern Sticky Event kèm lý do.
- [ ] Cập nhật `Todo.md`: các mục nợ kỹ thuật ở mục 5.
- [ ] Commit theo nhóm logic riêng biệt:
  - `feat: implement packet protocol with length-prefixed framing`
  - `fix: correct asmdef references (Data, Network, UI)`
  - `feat: add Root assembly as Composition Root exception`
  - `feat: implement sticky event pattern to fix EventBus race condition`
  - `feat: wire NetworkClient and ConnectionStatusView via VContainer`

---

*Tài liệu này nên lưu tại `Documentation/DevLog/Day3.md`, nối tiếp `Day2.md` — hồ sơ theo dõi tiến độ studio thật.*
