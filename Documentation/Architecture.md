# KnightOnline — Architecture

Tài liệu này ghi lại các quyết định kiến trúc đã chốt và LÝ DO đằng sau chúng.
Khi 1 quyết định thay đổi, sửa trực tiếp mục tương ứng — không để tài liệu
lệch khỏi thực tế code.

---

## 1. Solution Structure (Monorepo)

```
KnightOnline/                       <- git repo root
├── KnightClient/                    <- Unity project (2D, URP)
│   └── Assets/_Project/Scripts/
├── KnightServer/                     <- .NET 8 Console App (server giả lập)
├── Documentation/                     <- tài liệu chung, KHÔNG nằm trong Assets
└── .gitignore
```

**Không có project `Shared` riêng biệt.** Code dùng chung giữa Client và
Server đặt vật lý trong `KnightClient/Assets/_Project/Scripts/Shared/`,
Server tham chiếu trực tiếp qua glob include trong `.csproj`:

```xml
<ItemGroup>
  <Compile Include="../KnightClient/Assets/_Project/Scripts/Shared/**/*.cs" />
</ItemGroup>
```

Lý do: nếu Shared là project `.csproj` riêng, Unity không compile trực tiếp
được — phải build DLL rồi copy thủ công mỗi lần sửa, tạo ma sát lớn khi
đang lặp nhanh ở giai đoạn học tập. Đảm bảo 1 nguồn sự thật duy nhất, không
copy-paste.

**Server hiện tại là giả lập** (simulated) — mục đích rèn tư duy "client
không tự quyết định state" trước khi đủ kinh nghiệm dùng framework production
(Mirror/FishNet) hoặc viết server thật với database.

---

## 2. Assembly Definition — Dependency Graph 9 Module

Enforce bằng compile-time, không dựa vào tự giác. Luật: dependency 1 chiều,
ngoại trừ `Root` (Composition Root).

| Assembly | Phụ thuộc | Vai trò |
|---|---|---|
| `KnightOnline.Client.Core` | VContainer, UniTask | Hạ tầng lõi: DI, EventBus |
| `KnightOnline.Client.Data` | Core, Shared | Data model + domain events |
| `KnightOnline.Client.Shared` | *(không phụ thuộc)* | Packet/DTO giao tiếp Client-Server |
| `KnightOnline.Client.Input` | Core, Unity.InputSystem | Xử lý input người chơi |
| `KnightOnline.Client.Network` | Core, Shared, Data, VContainer, UniTask | Giao tiếp Server |
| `KnightOnline.Client.Audio` | Core | Điều khiển âm thanh |
| `KnightOnline.Client.Gameplay` | Core, Data, Input, Network, Audio, VContainer, UniTask | Logic gameplay — **không biết UI** |
| `KnightOnline.Client.UI` | Core, Data, Shared, Gameplay, VContainer, UniTask, TextMeshPro | Giao diện |
| `KnightOnline.Client.Root` | **Tất cả 8 module trên** + VContainer, UniTask | Composition Root — ngoại lệ hợp lệ |

**Vì sao `Root` là ngoại lệ hợp lệ, không phải vi phạm:** `GameLifetimeScope`
(Composition Root) phải biết về mọi module để đăng ký chúng vào DI container.
Nếu đặt vai trò này trong `Core` (hạ tầng thấp nhất), `Core` sẽ buộc phải
reference `UI` (tầng cao nhất) để dùng `ConnectionStatusView` — phá vỡ hoàn
toàn ý nghĩa tách asmdef. Tách riêng `Root` giữ nguyên tắc: **Composition
Root luôn đứng ở đỉnh graph, được phép biết tất cả, nhưng không module nào
được phép biết ngược lại nó.**

**Quy ước Root Namespace:** mỗi asmdef phải điền "Root Namespace" khớp chính
xác với "Name" — đảm bảo Unity tự động điền đúng namespace khi tạo file mới
trong folder đó, tránh lỗi thiếu `.Client` như đã từng gặp.

**Lưu ý vận hành:** mỗi khi `Root` cần dùng 1 type từ module mới (để đăng ký
DI), phải thêm reference tương ứng vào `Root.asmdef` — lỗi `CS0234`/`CS0246`
xuất hiện ở `GameLifetimeScope.cs` gần như luôn có nghĩa là quên bước này.
Kiểm tra JSON thô của `.asmdef` khi nghi ngờ, không chỉ tin Inspector UI —
đã có trường hợp `references: []` rỗng hoặc duplicate reference gây lỗi khó
hiểu (type mismatch dù type đúng, hoặc "Assembly has duplicate references").

---

## 3. Namespace Convention

- Client (Unity project): `KnightOnline.Client.*`
- Server (.NET project, khi triển khai thật): `KnightOnline.Server.*`
- Shared code: `KnightOnline.Client.Shared.*` (đặt vật lý trong Client,
  Server include qua glob path — xem mục 1)

Lý do tách `.Client`/`.Server` rõ ràng: tránh nhầm lẫn khi đọc code, nhất là
khi Server phát triển song song và namespace không tự nhiên phân biệt được
thuộc bên nào chỉ bằng cách đọc lướt.

---

## 4. Dependency Injection — VContainer

- `GameLifetimeScope` (kế thừa `LifetimeScope`, thuộc `Root`) — Composition
  Root, nơi duy nhất đăng ký service vào container.
- `GameBootstrap` (kế thừa `IAsyncStartable`, thuộc `Root`) — Entry Point,
  khởi tạo game qua UniTask async flow.
- **Không dùng Singleton tĩnh** (`X.Instance`) ở bất kỳ đâu.

### 2 cách đăng ký MonoBehaviour vào DI — dùng đúng ngữ cảnh

| Cách | Dùng khi | Ví dụ |
|---|---|---|
| `RegisterComponentOnNewGameObject<T>(...)` | VContainer tự tạo GameObject mới | `NetworkClient` |
| `RegisterComponentInHierarchy<T>()` | GameObject đã đặt sẵn trong scene (kéo-thả tay) | `ConnectionStatusView`, `CharacterCreationView` |

**Class C# thuần (không phải MonoBehaviour) đăng ký như service thường:**
```csharp
builder.Register<CharacterService>(Lifetime.Singleton);
```
VContainer tự resolve dependency của constructor (ví dụ `NetworkClient`)
miễn là dependency đó đã đăng ký sẵn trong container.

**Cảnh báo quan trọng:** `MonoBehaviour` không dùng được constructor
injection. Dùng Method Injection qua `[Inject] public void Construct(...)`.

**Race condition giữa 2 cách đăng ký:** `RegisterComponentOnNewGameObject`
có thể khởi tạo và chạy logic (publish event) sớm hơn nhiều so với
`RegisterComponentInHierarchy` (phụ thuộc vòng đời tự nhiên của Unity). Vì
vậy: subscribe EventBus ở `Start()`, không phải `Awake()` (VContainer chỉ
đảm bảo `[Inject]` chạy trước `Start()`, không đảm bảo trước `Awake()`) —
và với event đại diện trạng thái, dùng Sticky Event (xem mục 5) để không
phụ thuộc vào thứ tự thời gian.

---

## 5. EventBus — Giao tiếp Decoupled

Vị trí: `Core/Events/`. Đây là cơ chế hạ tầng thuần túy — chỉ biết "cách
truyền tin", không biết "tin gì". Domain event cụ thể (có payload, biết về
"chuyện gì xảy ra") đặt tại `Data/Events/`.

**Thành phần:**
- `IGameEvent` — marker interface cho mọi event thường.
- `IStickyGameEvent : IGameEvent` — marker cho event đại diện TRẠNG THÁI.
- `IEventBus` / `EventBus` — publish/subscribe strongly-typed (không dùng
  string event, tránh lỗi runtime không phát hiện được lúc compile).
- `EventBinding` — subscription trả về `IDisposable`, bắt buộc dispose
  trong `OnDestroy()` để tránh memory leak (rủi ro nghiêm trọng với client
  chạy hàng giờ liền, tích lũy qua nhiều lần chuyển scene).

**Nguyên tắc giao tiếp giữa module đã chốt:**
- UI → Gameplay/Network: gọi trực tiếp qua interface (command/lệnh).
- Gameplay/Network → UI: bắt buộc qua EventBus (notification), vì Gameplay
  và Network không được reference UI.

**Phân loại bắt buộc khi tạo event mới — Sticky hay thường:**
- **Sticky** (`IStickyGameEvent`): đại diện trạng thái LIÊN TỤC, tồn tại
  ngoài ý muốn kích hoạt của UI — trạng thái kết nối, HP hiện tại, số lượng
  item... EventBus lưu giá trị gần nhất, subscriber đăng ký muộn vẫn nhận
  được ngay thay vì chờ vô thời hạn.
  Ví dụ: `ServerConnectionResultEvent`.
- **Thường** (`IGameEvent`): đại diện HÀNH ĐỘNG TỨC THỜI, do chính UI chủ
  động kích hoạt và đã subscribe sẵn trước khi hành động xảy ra (ví dụ:
  người chơi bấm nút rồi mới có kết quả) — không cần phát lại.
  Ví dụ: `ServerDisconnectedEvent`, `CharacterCreationResultEvent`.

  Quy tắc nhanh: nếu event có thể xảy ra TRƯỚC KHI subscriber tồn tại/kịp
  lắng nghe (do khác biệt thời điểm khởi tạo giữa các thành phần) → Sticky.
  Nếu event luôn xảy ra SAU một hành động UI đã chủ động thực hiện (subscriber
  chắc chắn đã sẵn sàng) → thường.

---

## 6. Networking

**Mô hình:** Server Authoritative — server quyết định mọi state quan trọng
(damage, HP, inventory, position, monster AI, drop, quest). Client chỉ:
render, nhận input, hiển thị UI, phát âm thanh. Đây là yêu cầu bắt buộc
cho fairness (không Tool, không Mod) đã đặt ra từ Vision ban đầu.

**Giao thức hiện tại (giai đoạn học tập):**
- TCP, packet-based.
- Serialization: JSON (`System.Text.Json`) — ưu tiên dễ debug bằng mắt hơn
  hiệu năng, ở giai đoạn này. Sẽ cân nhắc binary protocol (MessagePack/
  Protobuf) khi chuyển sang production.
- **Length-Prefixed Framing:** mỗi message gửi 4 byte độ dài trước, sau đó
  đúng số byte đó là payload. Bắt buộc vì TCP là byte stream liên tục,
  không có khái niệm "message" tự nhiên.
- `PacketEnvelope(PacketType Type, string Payload)` — phong bì bọc mọi
  packet, cho bên nhận biết cách deserialize `Payload` (JSON string).

**Quy tắc mở rộng `PacketType` enum:** chỉ thêm giá trị mới ở cuối, không
đổi số thứ tự giá trị cũ khi đã có dữ liệu thật chạy qua mạng.

**Packet đã triển khai:**
| Packet | Chiều | Mục đích |
|---|---|---|
| `ConnectRequest`/`ConnectResponse` | C↔S | Bắt tay kết nối ban đầu |
| `CreateCharacterRequest`/`CreateCharacterResponse` | C↔S | Tạo nhân vật |

### ⚠️ Ràng buộc ngôn ngữ bắt buộc cho code trong `Shared/`

Unity Client: Scripting Backend = Mono, Api Compatibility Level = .NET
Standard 2.1 → tương ứng **C# 8.0**. Server (.NET 8) hỗ trợ tới C# 12.

Vì `Shared/` được compile bởi CẢ HAI, mọi code trong đó **chỉ được dùng cú
pháp C# 8.0 trở xuống**, bất kể Server hỗ trợ gì mới hơn. Cụ thể tránh:
- `required` modifier (C# 11) — **dùng constructor bắt buộc tham số thay
  thế** (property chỉ có getter, gán qua constructor). Đây là convention
  chính thức, bắt buộc cho MỌI class trong `Shared/Packets/`.
- Raw string literals (C# 11).

Trước khi dùng bất kỳ cú pháp C# mới nào trong `Shared/`, kiểm tra tương
thích cả 2 phía — không giả định Server hỗ trợ nghĩa là dùng được.

Ví dụ chuẩn (áp dụng cho mọi packet mới):
```csharp
public sealed class SomePacket
{
    public string SomeField { get; }

    public SomePacket(string someField)
    {
        SomeField = someField;
    }
}
```
Lưu ý: `Data/` (không phải `Shared/`) KHÔNG bị ràng buộc này, vì chỉ compile
trong Unity — có thể dùng `{ get; set; }` tự do (ví dụ `CharacterData`).

---

## 7. Gameplay Services — Trạm trung chuyển UI ↔ Network

**Vấn đề:** `UI` không được reference `Network` (theo dependency graph),
nhưng UI cần "ra lệnh" cho hành động liên quan mạng (VD: tạo nhân vật).

**Giải pháp:** đặt các class C# thuần (không phải `MonoBehaviour`) tại
`Gameplay/Services/`, đóng vai trò phiên dịch giữa ý định của UI và cách
Network thực hiện. UI chỉ biết gọi Service với ngôn ngữ nghiệp vụ ("tạo
nhân vật tên X"), không biết gì về packet, TCP, hay giao thức mạng.

Đăng ký như service thường qua `builder.Register<T>(Lifetime.Singleton)`,
không cần cách đăng ký đặc biệt như MonoBehaviour.

Ví dụ đã triển khai: `CharacterService` (nhận `NetworkClient` qua
constructor, expose `RequestCreateCharacter(string name)` cho UI gọi).

---

## 8. Database

- Prototype: SQLite.
- Production: MySQL/PostgreSQL (chưa triển khai, quyết định khi cần).

---

## 9. Lịch sử quyết định quan trọng (đổi hướng)

| Ngày | Thay đổi | Lý do |
|---|---|---|
| Day 1→2 | Bỏ `KnightShared` (project .NET riêng) → gộp vào `Client/Assets/Shared/` | Tránh ma sát build DLL thủ công mỗi lần sửa |
| Day 2 | Tách `Root` khỏi `Core` | `Core` không được phép biết `UI`; Composition Root cần đứng ở đỉnh graph |
| Day 3 | Thêm `IStickyGameEvent` | Race condition giữa 2 cách đăng ký DI khiến UI bỏ lỡ event trạng thái |
| Day 4 | Bỏ `required`, dùng constructor bắt buộc trong `Shared/` | Unity Mono + .NET Standard 2.1 không hỗ trợ C# 11 |
| Day 4 | Thêm tầng `Gameplay/Services/` | UI cần ra lệnh cho Network nhưng không được phép reference trực tiếp |

---

## 10. Nợ kỹ thuật đang treo (chưa xử lý, có chủ đích hoãn lại)

| Nợ | Vị trí | Lý do hoãn | Cần xử lý khi |
|---|---|---|---|
| Verbose log chưa bọc flag bật/tắt | `NetworkClient.cs` | Chưa ảnh hưởng hiệu năng ở quy mô test hiện tại | Trước khi có traffic thật (nhiều packet/giây) |
| `CreateCharacterResponsePacket` dùng `Message` mang tên nhân vật khi thành công | `Shared/Packets/CreateCharacterPacket.cs` | Giữ packet tối giản cho giai đoạn học tập | Khi có database thật — nên tách field `CharacterName` riêng khỏi `Message` |
| `NameAlreadyTaken` chưa xử lý được | Server (`Program.cs`) | Server giả lập chưa có database để kiểm tra trùng tên | Khi có persistent storage |

---

*Cập nhật lần cuối: Day 4. Xem `Documentation/DevLog/` để biết chi tiết quá
trình đi đến từng quyết định.*