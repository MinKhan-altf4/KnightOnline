# The World of Knights & Demons — Nhật ký Phát triển
## Day 5: Character Select Flow, In-Memory Roster & Scene Rebuild

---

## 1. Bối cảnh đầu ngày

Ngày này không nằm trong roadmap gốc — phát sinh khi bạn tự triển khai
thêm 1 nhánh tính năng (Option 1: Character Select Scene) ở 1 phiên làm
việc riêng trước khi quay lại đây review. Đây là lần đầu dự án có 1 khối
lượng code lớn được viết ngoài phiên làm việc chính, cần review lại từ
đầu — đúng quy trình studio thật khi có nhiều người/nhiều phiên cùng đóng
góp vào 1 codebase.

---

## 2. Những gì đã hoàn thành

### 2.1. Packet Protocol mở rộng — Roster nhân vật

- `ListCharactersRequestPacket`/`ListCharactersResponsePacket` — danh sách
  nhân vật, dùng `CharacterSummaryPacket` làm DTO trung gian (Shared,
  tách biệt khỏi `CharacterData` của Client).
- `PacketType` mở rộng thêm 2 giá trị, đúng thứ tự "chỉ thêm cuối".
- Server giữ roster **in-memory theo từng kết nối** (`Dictionary` cục bộ
  trong `HandleClientAsync`), có giới hạn `1024*1024` byte chống tràn bộ
  nhớ khi đọc packet — điểm phòng thủ tốt, không có trong yêu cầu ban đầu.

### 2.2. Domain Events mới — áp dụng đúng quy tắc Sticky/Thường đã chốt

- `CharacterListReceivedEvent : IStickyGameEvent` — đúng, vì
  `CharacterFlowController` tự động request ngay khi kết nối, có thể xảy
  ra trước khi UI kịp subscribe.
- `CharacterSelectedEvent` — phát hiện lỗi phân loại sai (ban đầu là
  Sticky), đã sửa thành `IGameEvent` thường vì luôn xảy ra sau hành động
  chủ động của người chơi (subscriber chắc chắn đã sẵn sàng).

### 2.3. `CharacterFlowController` — điều phối trạng thái UI

Ban đầu thiết kế dùng `SceneManager.LoadScene` để chuyển giữa Creation/
Select/InGame — phát hiện đây là lỗi kiến trúc nghiêm trọng trước khi
triển khai: component ở scene mới sẽ không được VContainer inject, vì
`Configure()` chỉ quét component tồn tại tại thời điểm build container.
**Chuyển sang Panel-based switching** (`SetActive` qua `PanelRefs`), giữ
mọi thứ trong 1 scene `Bootstrap` duy nhất — quyết định kiến trúc quan
trọng nhất trong ngày, tránh được 1 lớp bug khó debug (NullReference hàng
loạt) nếu đã triển khai theo hướng cũ.

### 2.4. CharacterSelectView + CharacterSelectionService

Theo đúng pattern đã thiết lập ở Day 4 (UI → Service → Network), dựng
nút động cho từng nhân vật từ `CharacterButtonTemplate`, publish
`CharacterSelectedEvent` khi người chơi chọn.

### 2.5. Dựng lại toàn bộ Bootstrap.unity từ đầu

Sau nhiều vòng lỗi do thao tác Editor tích lũy (component trùng lặp do
quên xóa bản gốc khi Copy Component, Anchor bị stretch không chủ đích),
quyết định dựng lại scene sạch theo quy trình 10 Phase tuần tự, mỗi Phase
kiểm tra ngay trước khi qua Phase tiếp — không dồn nhiều thay đổi rồi mới
test.

**Phát hiện quan trọng trong lúc dựng lại:** nếu dựng scene từ Hierarchy
trống nhưng `GameLifetimeScope.Configure()` đã đăng ký sẵn toàn bộ
`RegisterComponentInHierarchy` cho các component chưa tồn tại, VContainer
sẽ throw `VContainerException` ngay từ Phase đầu tiên. Giải pháp: comment
tạm các dòng đăng ký chưa tới lượt, mở dần theo đúng Phase tương ứng —
đây là kỹ thuật hữu ích cho việc dựng lại scene phức tạp trong tương lai,
nên ghi nhận thành quy trình chuẩn.

---

## 3. Lỗi đã gặp và bài học — ngày nhiều lỗi UI nhất từ đầu dự án

| Lỗi | Nguyên nhân | Bài học |
|---|---|---|
| `NullReferenceException` ở `CharacterCreationView.Start()` | Component script bị gắn trùng lặp: 1 bản đúng (object riêng), 1 bản thừa còn sót lại trên chính Panel (do Copy Component quên xóa bản gốc) — VContainer inject vào bản đúng, nhưng Unity gọi `Start()` trên CẢ HAI, bản thừa crash trước | Sau "Copy Component As New", XÓA bản gốc ngay lập tức, không để cách quãng. Dùng `GetHashCode()`/log debug để so sánh instance khi nghi ngờ trùng lặp |
| Nút không bấm được, bị Text đè | Nhiều UI element chồng nhau ở vị trí mặc định (0,0); Text có Raycast Target chặn click xuyên qua | Luôn dời vị trí UI mới tạo; tắt Raycast Target cho Text hiển thị thuần |
| `NameInput` không gõ được | `Rect Transform` bị Anchor kiểu "stretch" thay vì điểm cố định, khiến kích thước thực tế gần bằng 0 dù số liệu nhìn có vẻ đúng | Dùng Anchor Presets (giữ Shift+Alt khi click) để reset về đúng kiểu; luôn kiểm tra nhãn hiển thị (Pos/Width vs Top/Bottom) để biết đang ở chế độ Anchor nào |
| Chữ "trắng trên trắng" tưởng là lỗi, hóa ra đúng | Nhầm giữa giá trị Inspector (nội bộ) và hiển thị Game view (thực tế người chơi thấy) — 2 thứ khác nhau | Luôn test bằng Play Mode + quan sát Game view, không chỉ tin giá trị debug trong Inspector |
| `VContainerException` khi dựng lại từ Hierarchy trống | `Configure()` cũ vẫn đăng ký `RegisterComponentInHierarchy` cho component chưa tồn tại ở Phase đầu | Khi dựng lại scene theo từng giai đoạn, cần đồng bộ hóa cả code đăng ký DI theo đúng tiến độ — comment tạm phần chưa tới lượt |

---

## 4. Ước tính % hoàn thành tổng thể

**Giữ nguyên khoảng 5-6% (không tính là +% mới)** — Day 5 chủ yếu là
**củng cố và sửa lỗi** cho tính năng đã có khung từ Day 4 (character flow),
không mở thêm phạm vi gameplay mới. Đây là điều bình thường và cần thiết:
không phải ngày nào cũng tăng % — có những ngày dành để đảm bảo nền móng
đã dựng thực sự vững, đặc biệt sau khi có code viết ngoài phiên làm việc
chính cần được kiểm chứng lại kỹ.

**Giá trị thực sự của Day 5** không nằm ở % tăng thêm, mà ở việc: quy
trình dựng UI phức tạp (nhiều panel, nhiều script, DI theo từng giai
đoạn) giờ đã có công thức lặp lại được — `Rebuild_Bootstrap_Scene.md` là
tài liệu tham chiếu cho mọi lần dựng UI phức tạp sau này.

---

## 5. Nợ kỹ thuật còn treo (bổ sung)

- Màu sắc/bố cục UI hiện đã có bảng màu chuẩn (`#1E1B2E`, `#2E2A45`,
  `#C9A24B`, `#F0EDE5`...) nhưng chưa áp dụng đồng bộ 100% — cần rà soát
  lại khi có thời gian.
- `CharacterResultText` chưa đổi màu động theo Success/Fail trong code
  (đã có sẵn đoạn code mẫu, chưa áp dụng).
- Roster Server vẫn reset khi client reconnect (nợ từ Day 4, chưa xử lý).

---

## 6. Việc cần làm tiếp theo — Day 6

1. **Player Movement cơ bản** — lần đầu tiên có Gameplay thật (di chuyển
   nhân vật trong InGamePanel/scene), đánh dấu ranh giới chính thức
   chuyển từ "hạ tầng" sang "gameplay".
2. Quyết định: InGame có cần tách sang scene riêng thật sự không (khác
   với Creation/Select vẫn hợp lý dùng chung 1 scene)? Cần bàn trước khi
   code, vì đây sẽ là lúc Child LifetimeScope (đã đề cập nhưng chưa dùng)
   có thể cần thiết thật sự.
3. Camera theo dõi nhân vật (top-down hoặc side-view, tùy quyết định
   gameplay 2D cụ thể).

---

*Tài liệu này nên lưu tại `Documentation/DevLog/Day5.md`, nối tiếp
`Day2.md`, `Day3.md`, `Day4.md`.*
