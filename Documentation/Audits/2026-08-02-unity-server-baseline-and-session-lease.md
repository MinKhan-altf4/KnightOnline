# Baseline audit Unity/server và session lease — 2026-08-02

## Phạm vi

- Build Settings và scene flow Unity hiện hành.
- Composition root App/Bootstrap/InGame.
- Authentication, active account, Character Select và disconnect cleanup.
- Shared packet compatibility trong cùng source tree.
- Server build/test baseline.

Phân loại: Authentication/session là **Critical**; scene/UI là Presentation.

## Baseline đã xác nhận

- Scene flow duy nhất: `App → Bootstrap → InGame`.
- `Login.unity` legacy đã bị loại bỏ cùng `.meta` và Build Settings entry.
- App scene đang serialized `DevelopmentAuthenticationBypass = false`.
- Server settings cũng tắt bypass.
- Authentication thành công mới mở Character Flow.
- Character Select được tính active theo lease account.
- Server là nguồn quyết định ownership account và character.
- Server Release build sạch, không warning/error.
- Toàn bộ 38 server test Release vượt qua.
- Không thấy missing script GUID hoặc GUID zero trong scene/prefab được quét tĩnh.

## Session lease đã củng cố

Contract hiện có:

```text
AccountKey + ConnectionId + Generation + ExpiresAtUtc
```

- Claim đồng thời chỉ có một owner.
- Heartbeat chỉ renew đúng connection và generation.
- TTL cho phép lease chết được thay thế.
- Disconnect chuyển lease sang grace expiry thay vì phụ thuộc sự kiện đóng sạch.
- Explicit Leave xóa ngay đúng generation.
- Cleanup/heartbeat cũ không xóa hoặc renew lease thay thế.
- Packet Dispatcher kiểm tra live lease trước mọi handler cần Authenticated hoặc
  CharacterSelected access.
- Client nhận generation/heartbeat interval từ response server, không tự quyết
  định TTL.

Config Development hiện tại:

```text
heartbeatIntervalSeconds = 5
sessionLeaseTtlSeconds = 20
disconnectGraceSeconds = 10
```

## Test đã chạy

```text
dotnet build KnightServer/KnightServer.csproj -c Release --no-restore
dotnet test KnightServer.Tests/KnightServer.Tests.csproj -c Release --no-restore
```

Kết quả: build 0 warning/0 error; 38/38 test pass.

Test lease bao phủ live conflict, expiry replacement, heartbeat generation,
disconnect grace, stale cleanup, 20 concurrent claims và JSON packet round-trip.

Integration test dùng cặp TCP loopback thật để chạy packet qua
`PacketDispatcher → AccountSessionHeartbeatPacketHandler → ClientConnection`:

- heartbeat trả renewal/expiry đúng;
- lease hết hạn bị chặn tại dispatcher và nhận `ForcedDisconnect`;
- connection generation cũ bị chặn sau khi generation mới claim;
- scenario A/B từ active conflict → disconnect grace → replacement → stale A.

Bộ 38 test Release được chạy lặp lại 5 lần bằng `--no-build --no-restore`; cả năm
lượt đều pass, chưa phát hiện race/flaky failure trong phạm vi test harness.

## Giới hạn xác minh

Unity Editor đang mở ngoài phiên kiểm tra nhưng không refresh project trong thời
gian audit. Các `.csproj` do Unity sinh không có `Temp/obj/project.assets.json`,
vì vậy `dotnet build` độc lập không thể dùng để xác nhận Unity compilation.

Việc bắt buộc còn lại trong Unity:

1. Mở project và chờ compile hoàn tất.
2. Xác nhận Console không có log đỏ.
3. Test client A/B với cùng account.
4. Stop A; xác nhận B bị từ chối trong grace và vào được sau khi lease hết hạn.
5. Tắt mạng A; xác nhận B vào được sau TTL/grace và A cũ bị disconnect nếu gửi
   packet với generation cũ.

Integration hiện chưa chạy toàn chuỗi PostgreSQL authentication/token rotation →
lease claim → Unity UI. Vì vậy kết quả này chứng minh server session/network
boundary, không thay thế kiểm thử database và thiết bị thật.

## Rủi ro còn lại

| Mức | Rủi ro | Trạng thái/forward-fix |
|---|---|---|
| P0 trước multi-node | Lease adapter in-memory | Thay Redis/DB atomic lease; giữ nguyên contract generation |
| P0 trước Alpha public | Chưa có TLS/secure client token store | Production adapter và transport security |
| Đã có foundation | Admission controller | Active cap 500, transport cap 750, atomic concurrency test; còn load/UI nghiệm thu |
| P0 trước 500 | Broadcast tuần tự toàn connection | Interest set + outbound queue hữu hạn |
| P1 | Authentication chưa có RequestId/idempotency store | Thiết kế retry/rotation transaction rõ ràng |
| P1 | Development bypass compatibility path không có lease | Giữ tắt; loại bỏ hoàn toàn hoặc triển khai contract riêng trước khi dùng |
| P1 | Chưa có session metric/dashboard | Thêm active/expired/conflict/renewal counters và health/readiness |

## PostgreSQL authentication integration và admission update

- Thêm project `KnightServer.IntegrationTests` tách khỏi unit test mặc định.
- Chạy trên PostgreSQL Development thật với dữ liệu account duy nhất từng test và
  cleanup theo account key.
- 2/2 integration test pass:
  - guest → registered → login → refresh rotation → reuse detection/family revoke;
  - hai thiết bị đăng nhập DB thành công nhưng chỉ một active lease được claim.
- Active admission: đúng 500 claim thành công, claim thứ 501 nhận
  `CapacityReached` trong concurrency test.
- Transport admission gate: đúng 750/1000 concurrent entry, capacity được trả lại
  sau disconnect.
- Client giữ response `ServerFull` thay vì để socket close ghi đè thành thông báo
  mất kết nối; authentication ServerFull lưu token replacement đã commit.

Lần restore đầu của integration project không truy cập được NuGet vulnerability
feed trong môi trường local (`NU1900`). Package restore/build và test vẫn thành
công; dependency vulnerability scan phải chạy lại trong CI có network.

## Kết luận audit

Baseline server và contract session đã ổn định hơn, nhưng task chưa chứng minh
Unity runtime A/B cho đến khi hoàn thành manual checklist. Không được xem adapter
in-memory là bảo đảm cho nhiều server hoặc Production.
