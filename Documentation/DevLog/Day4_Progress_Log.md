# The World of Knights & Demons — Nhật ký Phát triển
## Day 4: Character Creation Flow & Gameplay Services

---

## 1. Mục tiêu đề ra đầu ngày

Biến "kết nối thành công" (Day 3) thành luồng chơi có ý nghĩa đầu tiên:
người chơi nhập tên nhân vật, gửi lên Server, Server validate và phản hồi,
Client hiển thị kết quả — đi trọn vẹn qua đúng kiến trúc DI + EventBus,
không đi tắt.

**Kết quả: đạt được trọn vẹn**, cả 3 kịch bản test (tên rỗng, tên hợp lệ,
tên quá dài) đều hoạt động đúng.

---

## 2. Những gì đã hoàn thành

### 2.1. Phát hiện và sửa vấn đề tương thích ngôn ngữ

`required` modifier (C# 11) không hoạt động trên Unity Client (Mono +
.NET Standard 2.1 → C# 8.0), dù Server (.NET 8) hỗ trợ đầy đủ. Đây là bài
học kiến trúc quan trọng: **code trong `Shared/` phải viết bằng tập con
ngôn ngữ mà CẢ HAI compiler đều hiểu**, không phải ngôn ngữ mới nhất mà 1
trong 2 bên hỗ trợ.

**Giải pháp:** toàn bộ packet trong `Shared/Packets/` chuyển sang pattern
constructor bắt buộc tham số (property chỉ có `get`), thay cho `required`.
Đã sửa lại cả 3 packet cũ (`ConnectRequestPacket`, `ConnectResponsePacket`,
`PacketEnvelope`) và áp dụng ngay từ đầu cho packet mới.

### 2.2. Packet Protocol mới — `CreateCharacterRequest`/`Response`

- `CreateCharacterResult` enum: `Success`, `NameEmpty`, `NameTooLong`,
  `NameAlreadyTaken` (giá trị cuối chưa xử lý được — Server giả lập chưa
  có database).
- `PacketType` mở rộng thêm 2 giá trị mới ở cuối, không đổi giá trị cũ.

### 2.3. `Data/Models/CharacterData.cs` — lần đầu `Data` chứa nội dung thật

Class thuần chứa `CharacterName`, dùng `{ get; set; }` bình thường (không
bị ràng buộc C# 8.0 vì chỉ compile trong Unity, không đi qua Server). Đây
là lần đầu tiên `Data` module có nội dung ngoài `Events/`.

### 2.4. Tầng mới: `Gameplay/Services/` — trạm trung chuyển UI ↔ Network

Phát hiện đúng vấn đề: `UI` cần "ra lệnh" cho hành động liên quan mạng,
nhưng `UI` không được reference `Network` theo dependency graph. Giải quyết
bằng `CharacterService` — class C# thuần (không phải `MonoBehaviour`),
nhận `NetworkClient` qua constructor, expose method mang ngôn ngữ nghiệp vụ
(`RequestCreateCharacter(name)`) cho UI gọi, không để UI biết gì về packet
hay TCP.

Đây là pattern sẽ tái sử dụng cho mọi tính năng gameplay sau này (farm,
đánh boss, chế tạo, giao dịch...).

### 2.5. `CharacterCreationView.cs` — UI hoàn chỉnh đầu tiên có tương tác 2 chiều

Khác với `ConnectionStatusView` (chỉ lắng nghe, không có input), đây là UI
đầu tiên vừa gửi lệnh (qua `CharacterService`) vừa lắng nghe kết quả (qua
`CharacterCreationResultEvent`, phân loại đúng là `IGameEvent` thường —
không cần Sticky vì UI luôn đã subscribe trước khi người chơi bấm nút).

### 2.6. Server — validate tối thiểu

`Program.cs` thêm case xử lý `CreateCharacterRequest`: kiểm tra tên rỗng,
tên quá dài (>20 ký tự), trả về response tương ứng. Chưa có persistent
storage — mọi lần chạy lại Server đều "quên" nhân vật đã tạo trước đó
(chấp nhận được ở giai đoạn này).

---

## 3. Lỗi đã gặp và bài học

| Lỗi | Nguyên nhân | Bài học |
|---|---|---|
| `CS8618` khi dùng `required` | Unity Mono không hỗ trợ C# 11 | Luôn kiểm tra Api Compatibility Level trước khi dùng cú pháp C# mới trong `Shared/` |
| `CS7036`/`CS0200` hàng loạt | Object initializer không còn hoạt động sau khi đổi property sang read-only | Khi đổi pattern 1 class dùng chung nhiều nơi, dùng Search in Files để tìm hết chỗ gọi, đừng sửa từng lỗi compiler báo rời rạc |
| `CS0234`/`CS0246` ở `GameLifetimeScope` | `Root.asmdef` thiếu reference `Gameplay` | Mỗi type mới dùng trong Composition Root cần asmdef reference tương ứng — đã thành pattern lặp lại nhiều lần, cần nhớ theo phản xạ |
| "Assembly has duplicate references" | Thêm nhầm 2 lần cùng 1 reference trong Inspector | Xem JSON thô của `.asmdef` để xác nhận, không chỉ tin Inspector UI |
| Lỗi cú pháp `switch` khi paste code Server | Paste đè/nhầm vị trí khi thêm case mới | Khi thêm case vào switch dài, thay nguyên khối method thay vì chỉnh sửa từng dòng |
| Nút UI không bấm được, bị Text che | Nhiều UI element cùng vị trí (0,0) mặc định, Text chặn Raycast | Luôn dời vị trí UI element mới tạo; tắt Raycast Target cho Text hiển thị thuần không cần tương tác |

---

## 4. Ước tính % hoàn thành tổng thể

**Cập nhật từ 3-4% (cuối Day 3) lên khoảng 5-6%.**

Ý nghĩa quan trọng hơn con số: Day 4 xác lập được **pattern chuẩn cho mọi
tính năng gameplay tương lai** — UI gọi Service, Service gọi Network,
Server xử lý và phản hồi, EventBus đưa kết quả về UI. Từ giờ, thêm 1 tính
năng mới (farm, chế tạo, PvP...) sẽ đi theo đúng khuôn này, không phải
phát minh lại kiến trúc mỗi lần.

---

## 5. Nợ kỹ thuật còn treo (xem chi tiết trong `Architecture.md` mục 10)

- Verbose log trong `NetworkClient.cs` chưa bọc flag.
- `CreateCharacterResponsePacket` tạm dùng `Message` mang tên nhân vật khi
  thành công — cần tách field riêng khi có database thật.
- `NameAlreadyTaken` chưa xử lý được (chưa có persistent storage).

---

## 6. Việc cần làm tiếp theo (Day 5 — đề xuất)

1. Character Select Scene — hiển thị danh sách nhân vật đã tạo (hiện tại
   chưa lưu trữ gì, cần quyết định: lưu tạm trong RAM của Server giả lập,
   hay bắt đầu tích hợp SQLite ngay).
2. Scene Flow chính thức: `Bootstrap → Login → CharacterSelect → InGame`
   — hiện tại mọi thứ đang gộp chung 1 scene `Bootstrap`, cần tách ra khi
   luồng phức tạp hơn.
3. Cân nhắc thời điểm bắt đầu Player Movement — đánh dấu ranh giới chuyển
   từ "hạ tầng" sang "gameplay thật" đầu tiên.

---

*Tài liệu này nên lưu tại `Documentation/DevLog/Day4.md`, nối tiếp
`Day2.md`, `Day3.md`.*
