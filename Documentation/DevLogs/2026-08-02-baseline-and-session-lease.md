# DevLog — Baseline Unity/server và lease phiên tài khoản

Ngày: 02-08-2026
Phạm vi: baseline scene, session lease và giới hạn kết nối.

## Kết quả

- Loại bỏ scene `Login` độc lập; Build Settings dùng `App`, `Bootstrap`,
  `InGame`.
- Thay lease account/connection bằng lease có generation và expiry; thêm
  heartbeat, forced-disconnect reason, grace khi disconnect và release tức thì
  khi người chơi chủ động rời phiên.
- Client bắt đầu/dừng heartbeat theo vòng đời authentication; dispatcher chặn
  packet authenticated từ lease hết hạn hoặc generation cũ.
- Thêm cấu hình/validation heartbeat, TTL và grace; test concurrency/stale
  generation cùng TCP loopback harness.
- Thêm integration project PostgreSQL cho authentication/token rotation.
- Thêm admission config: tối đa 500 account active và 750 kết nối transport;
  response `ServerFull` được giữ đúng kể cả sau token rotation.

## Quyết định và tương thích

- Character Select được tính là session active. Thiết bị vào sau bị từ chối,
  không đá thiết bị đang giữ lease.
- Chỉ generation hiện hành được renew/release lease.
- Store trong RAM chỉ dùng cho một tiến trình; cần adapter phân tán trước
  multi-node.
- Thêm packet heartbeat; client/server cần đồng bộ cho đến khi có protocol
  negotiation.
- Không có migration database. Giá trị mặc định Development: heartbeat 5 giây,
  TTL 20 giây, disconnect grace 10 giây.

## Kiểm tra tại phiên

- Unit/network test ban đầu: 38/38, chạy lặp 5 lần đạt; sau admission: 42/42.
- Tích hợp PostgreSQL sau admission: 2/2 đạt.
- `git diff --check` và scan serialized scene/prefab đạt tại checkpoint.
- Unity runtime A/B chưa xác nhận; authentication/token rotation end-to-end
  chưa chạy qua TCP harness.

## Rủi ro và bước tiếp theo

Lease mất khi restart và không bảo vệ nhiều node; chưa có TLS, kho token an toàn,
rate limit phân tán, idempotency login/resume, metric session hay broadcast theo
vùng quan tâm. Development bypass phải tiếp tục tắt. Ưu tiên kiểm tra Unity hai
thiết bị, bổ sung observability/idle cap, rồi đo broadcast và load theo các nấc.
