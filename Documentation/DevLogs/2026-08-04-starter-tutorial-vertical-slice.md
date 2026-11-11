# DevLog — Lát cắt nhiệm vụ tân thủ

Ngày: 04-08-2026
Phạm vi: nhiệm vụ authoritative, map/cổng, combat và trình bày Unity.

## Kết quả

- Xây dựng nhiệm vụ với NPC Mẹ: trò chuyện, hạ 20 Wolf, quay về nhận thưởng,
  rồi mở hướng đi tới Safe Zone 1.
- Chấp nhận nhiệm vụ không tự dịch chuyển người chơi. Người chơi tự di chuyển
  và dùng cổng; server kiểm tra session, map nguồn, khoảng cách, cấp độ, map đích
  và điểm xuất hiện.
- Cấu hình ba map: Làng Tân Thủ, Bãi Sói và Safe Zone 1. Bãi Sói có tám Wolf;
  cổng Safe Zone 1 yêu cầu cấp 2.
- Server quyết định vị trí di chuyển, kiểm tra map khi đánh quái và giới hạn vị
  trí trong map. Va chạm Unity chỉ hỗ trợ trình bày/dự đoán.
- Chống nhận kill credit trùng bằng định danh vòng đời quái. Thưởng vật phẩm,
  EXP/cấp, idempotency, audit và outbox được ghi trong cùng transaction.
- Thêm contract/event và trình bày runtime cho NPC, cổng, tiến độ nhiệm vụ,
  hội thoại, phần thưởng, ranh giới map và chuyển map. Tắt NPC legacy để tránh
  sinh đôi.

## Quyết định, dữ liệu và tương thích

- Server là nguồn sự thật cho quest, phần thưởng, map, vị trí và điều kiện cổng.
- Cấu hình `serverSettings.json` là nguồn dữ liệu cho map, spawn, NPC, cổng,
  yêu cầu quest, phần thưởng và vị trí Wolf.
- Thêm migration `StarterTutorialVerticalSlice`,
  `TutorialQuestInventoryAndKillCredits` và `TutorialRewardAuditOutbox`.
- Packet mới chỉ tương thích với cặp client/server hiện tại; chưa có thương
  lượng phiên bản protocol cho rollout hỗn hợp.
- Có công cụ reset dữ liệu tài khoản Development để chạy lại vòng quest; tuyệt
  đối không bật ngoài Development.

## Kiểm tra tại phiên

| Kiểm tra | Kết quả lúc ghi log |
|---|---|
| Build Release máy chủ | Đạt, 0 cảnh báo/lỗi |
| Unit test máy chủ | 75/75 đạt |
| Tích hợp PostgreSQL | 18/18 đạt |
| Unity compile | Người dùng xác nhận đạt trước chỉnh sửa cổng cuối |
| Chạy thử cổng trong Unity sau khởi động sạch | Còn chờ |

## Rủi ro và bước tiếp theo

- Cần khởi động lại server và Unity, đi bộ qua cổng Bãi Sói; `Training Wolf`
  có thể cho thấy server/client vẫn chạy nội dung cũ.
- Hình map, NPC, cổng và Wolf còn là placeholder; cần hoàn thiện art, va chạm,
  safe area và tỉ lệ màn hình.
- Outbox chưa có đầy đủ dispatcher, retry, metric và dead-letter.
- Mốc tiếp theo được đề xuất là thiết kế Inventory có read model/contract riêng;
  UI chỉ mở bảng, còn nội dung và thay đổi vẫn do server quyết định.

## File nên đọc khi điều tra lát cắt này

- `KnightServer/Tutorials/StarterTutorialService.cs`
- `KnightServer/Networking/Handlers/TutorialWorldPacketHandlers.cs`
- `KnightClient/Assets/_Project/Scripts/Gameplay/World/AuthoritativeWorldPresentation.cs`
- `KnightClient/Assets/_Project/Scripts/UI/InGameHUD.cs`
