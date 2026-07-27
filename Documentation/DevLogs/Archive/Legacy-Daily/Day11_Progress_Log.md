# The World of Knights & Demons — Nhật ký Phát triển
## Day 11: Hoàn thiện nền móng NPC Interaction

## Mục tiêu

Ổn định NPC interaction vertical slice trước khi mở Shop hoặc Quest:

- Bỏ static event giữa Gameplay và UI.
- Dùng `IEventBus` với payload rõ ràng.
- Quản lý đầy đủ lifecycle của dialog.
- Bảo đảm UI chỉ phát ý định, không chứa nghiệp vụ gameplay.

## Đã hoàn thành

### 1. Event-driven interaction

- Loại bỏ `InteractableNPC.OnNpcClicked`.
- `PlayerInteraction` validate layer và khoảng cách, sau đó publish
  `NpcInteractionRequestedEvent`.
- Event mang snapshot gồm source, Unity `EntityId`, tên, lời thoại và danh sách
  `NpcOptionData`.
- `NpcDialogUI` subscribe qua `IEventBus` và dispose subscription khi destroy.

### 2. Command boundary cho lựa chọn NPC

- `Close` vẫn là hành vi presentation và đóng dialog trực tiếp.
- Câu chào là nội dung hội thoại; không dùng action hoặc nút `Talk`.
- `Shop`, `Quest` chỉ publish `NpcActionRequestedEvent`.
- Chưa có logic shop/quest trong UI; handler nghiệp vụ sẽ được xây ở
  feature tương ứng.
- UI luôn tạo đúng một nút Close. NPC không có chức năng, như lính canh,
  chỉ hiển thị câu chào và nút Close.

### 3. Dialog lifecycle

- Khóa movement và interaction mới khi dialog đang mở.
- Đưa velocity Player về zero ngay lúc khóa.
- Bỏ qua interaction lặp trong thời gian dialog đang hiển thị.
- Hỗ trợ đóng bằng nút Close, phím Escape hoặc click ngoài panel.
- Bỏ qua click mở dialog trong cùng frame để tránh dialog tự đóng ngay.
- Nếu NPC nguồn bị destroy, dialog tự đóng.
- Khi UI disable/destroy hoặc scene unload, Player controls được trả lại.

### 4. UI layout

- Các lựa chọn thường được xếp tối đa hai nút mỗi hàng.
- Nút Close luôn nằm ở hàng cuối và căn giữa.
- Khung thoại, vùng greeting và button container đã được căn lại trong
  `InGame.unity`.

## Kiến trúc sau thay đổi

```text
PlayerInteraction
  → NpcInteractionRequestedEvent
  → IEventBus
  → NpcDialogUI
  → NpcActionRequestedEvent
  → Feature handler tương ứng
```

Gameplay không reference UI. UI được phép reference Gameplay để render dữ liệu
và phát command, nhưng không được thực thi nghiệp vụ Shop/Quest.

## Việc cần xác nhận trong Unity

1. Click NPC gần để mở dialog.
2. Giữ phím movement trước khi mở: Player phải dừng ngay.
3. Click NPC khác khi dialog mở: dialog hiện tại không được thay đổi.
4. Đóng lần lượt bằng Close, Escape và click ngoài panel.
5. Sau mỗi cách đóng, Player phải di chuyển và tương tác lại được.
6. Destroy NPC đang trò chuyện hoặc unload scene: không được để Player bị khóa.
7. Shop/Quest publish event nhưng không thực thi nghiệp vụ.

## Bước tiếp theo

Sau khi checklist trên pass, ưu tiên camera bounds hoặc gameplay foundation
tiếp theo. Shop được để sau Inventory/Item/Economy foundation; Quest được triển
khai sau khi có quest domain và server contract rõ ràng.
