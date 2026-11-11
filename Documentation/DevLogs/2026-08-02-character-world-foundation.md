# DevLog — Ranh giới nhân vật và nền tảng EnterWorld

Ngày: 02-08-2026
Phạm vi: lưu nhân vật, quyền sở hữu, session gameplay và vào thế giới.

## Kết quả

- Củng cố create/list character trên PostgreSQL; xử lý payload lỗi rõ ràng và
  trả Unauthorized thay vì exception khi account không còn.
- `ListCharactersRequest` có `ServerId`; có tương thích tạm với packet cũ thiếu
  trường, còn server ID sai bị từ chối.
- Lỗi tải roster đi qua `CharacterListFailedEvent`, không giả thành danh sách
  rỗng.
- Thêm kiểm thử TCP thực qua length-prefix, connection, dispatcher, handler và
  PostgreSQL.
- Chọn nhân vật tạo `GameplaySessionId`; `EnterWorld` mới trả snapshot chính
  thức và cho phép chuyển scene. Retry không tạo session/entity trùng.
- Snapshot gồm identity, class/body/appearance, HP, movement, map/spawn, vị trí,
  giờ UTC phía server và version.
- Đưa `AuthenticationFlowService` lên `AppLifetimeScope` để heartbeat không bị
  hủy khi nạp InGame bằng `LoadSceneMode.Single`.
- Thêm resolver va chạm quái phía server, dùng chung cho movement, combat và
  EnterWorld; quái đã chết không chặn đường thoát.

## Quyết định và tương thích

- Server kiểm tra account sở hữu nhân vật; Character Select vẫn giữ account
  active.
- Select chỉ tạo gameplay session; EnterWorld trả snapshot dùng để vào scene.
- Session ID do server sinh. Metadata session nằm ngoài packet DTO/domain.
- Client/server cần cùng phiên bản để dùng flow EnterWorld hai bước; chưa có
  thương lượng capability/protocol.
- Không có migration mới. Bổ sung bán kính va chạm quái/người chơi trong config.

## Kiểm tra tại phiên

- Build Release máy chủ: 0 cảnh báo, 0 lỗi.
- Unit/network test: 45/45 đạt.
- Tích hợp PostgreSQL/TCP: 13/13 đạt.
- Unity compile được người dùng xác nhận; Select/EnterWorld hoạt động một phần.
- Collision cuối phiên và heartbeat dài 5–10 phút còn cần kiểm tra thủ công.

## Rủi ro và bàn giao

Chưa có snapshot hiệu chỉnh movement định kỳ hoàn chỉnh, va chạm map/NPC/player,
lưu vị trí sau di chuyển, policy dời quái hồi sinh khỏi người chơi, protocol
negotiation, metric, TLS, lease phân tán hay load test Alpha. Ưu tiên nghiệm thu
heartbeat và collision, sau đó thiết kế sequence/snapshot/reconciliation cùng
map boundary và checkpoint vị trí.
