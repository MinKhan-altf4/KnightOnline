# DevLog — Baseline Unity/server và session lease

Ngày: 2026-08-02
Phân loại: Critical (authentication/session), Presentation (scene baseline)

## Mục tiêu

1. Audit và ổn định baseline Unity/server.
2. Triển khai active-account lease có heartbeat, TTL, disconnect grace và
   generation cho luồng nhiều thiết bị.

## Công việc hoàn thành

- Loại bỏ `Login.unity` legacy và `.meta`; Build Settings chỉ còn App, Bootstrap,
  InGame.
- Ghi kiến trúc runtime hiện hành và đánh giá Alpha 500 online.
- Thay lease `account → connection` bằng lease có generation/expiry.
- Bổ sung heartbeat request/response và forced-disconnect reason khi lease hết.
- Authentication response trả generation, expiry và heartbeat interval do server
  quyết định.
- Unity bắt đầu/dừng heartbeat theo vòng đời authentication.
- Disconnect server đưa lease vào grace; explicit Leave giải phóng ngay.
- Packet Dispatcher chặn mọi packet authenticated nếu lease hết hạn/stale.
- Config hóa và validate heartbeat/TTL/grace.
- Viết lại test lease, gồm concurrency và stale-generation protection.
- Thêm TCP loopback integration harness kiểm tra packet qua dispatcher/handler
  và forced disconnect thật trên socket.
- Thêm PostgreSQL integration project và kiểm thử authentication/token rotation
  trên database Development thật với cleanup dữ liệu test.
- Thêm active-account admission 500 và transport admission 750 từ config.
- Thêm capacity snapshot, concurrent gate và response `ServerFull` ở cả transport
  lẫn authentication boundary.
- Bảo toàn refresh token replacement khi authentication commit trước lúc capacity
  bị từ chối.

## Module/file chính thay đổi

- `KnightServer/Accounts/AccountSessionRegistry.cs`
- `KnightServer/Networking/PacketDispatcher.cs`
- `KnightServer/Networking/Handlers/AuthenticationPacketHandlers.cs`
- `KnightServer/Networking/ClientConnection.cs`
- `KnightServer/Configuration/ServerOptions.cs`
- `KnightClient/.../AuthenticationFlowService.cs`
- `KnightClient/.../AuthenticationPackets.cs`
- `KnightClient/.../NetworkClient.cs`
- `KnightServer.Tests/Accounts/AccountSessionRegistryTests.cs`
- `Documentation/Architecture/Current_Unity_Runtime_And_Alpha_500_Online.md`
- `Documentation/Audits/2026-08-02-unity-server-baseline-and-session-lease.md`

## Quyết định kỹ thuật

- Character Select tiếp tục được tính active.
- Thiết bị vào sau bị từ chối; không đá owner đang active.
- Server gửi heartbeat interval; client không tự suy luận session state.
- Generation là ownership token của lease; cleanup chỉ tác động đúng generation.
- Disconnect policy hiện là reject trong grace, cho claim mới sau expiry.
- In-memory store chỉ là adapter một process; multi-node phải thay adapter phân
  tán mà không đổi handler/domain boundary.

## Test/build

- Server Release build: thành công, 0 warning, 0 error.
- Server tests Release: 38/38 pass; chạy lặp 5 lượt đều pass.
- Sau admission: unit/network tests 42/42 pass; PostgreSQL integration 2/2 pass.
- `git diff --check`: pass tại checkpoint trước khi viết log.
- Static scan scene/prefab: không phát hiện missing script GUID/GUID zero.
- Unity compile/runtime A/B: chưa xác nhận trong phiên vì Unity-generated
  `project.assets.json` không tồn tại và Editor đang mở không refresh log.
- PostgreSQL authentication/token rotation end-to-end chưa nằm trong TCP harness.

## Config/migration

- Không có database migration.
- Thêm Development config:
  - heartbeat: 5 giây;
  - TTL: 20 giây;
  - disconnect grace: 10 giây.
- Shared packet thêm type 34/35; client và server phải rollout cùng phiên bản cho
  đến khi có compatibility negotiation hoàn chỉnh.

## Rủi ro và technical debt

- In-memory lease mất khi restart và không bảo vệ nhiều server instance.
- Chưa có TLS, secure token store hoặc distributed limiter/lease.
- Chưa có RequestId/idempotency cho login/resume.
- Chưa có metric/dashboard session.
- Broadcast gameplay toàn connection vẫn là blocker tải.
- Development bypass phải tiếp tục tắt.

## Rollback/forward-fix

- Không rollback về lease không TTL vì vi phạm CORE-06.
- Nếu Unity heartbeat có lỗi, forward-fix packet mapping/lifecycle; không bỏ kiểm
  tra generation ở server.
- Multi-node forward-fix bằng Redis/DB adapter thực thi
  `IActiveAccountLeaseStore` với thao tác atomic và TTL.

## Bước tiếp theo

1. Chạy manual checklist Unity A/B và ghi kết quả bổ sung.
2. Thêm structured session/capacity metrics, health/readiness và idle/per-IP cap.
3. Thiết kế interest-based broadcast và outbound queue.
4. Chạy load/soak test thật ở 50 → 100 → 250 → 500 → 600 client.
