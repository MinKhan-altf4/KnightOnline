# KnightBot — bot load test headless

Bot giả lập người chơi bằng đúng giao thức thật (protocol 2):

1. Handshake.
2. Tạo guest.
3. Lấy catalog, tạo nhân vật.
4. Chọn nhân vật, vào thế giới.
5. Di chuyển ngẫu nhiên kèm heartbeat cho đến khi hết thời gian.

Thiết kế: [World_Simulation_P1_Design.md](../Documentation/Architecture/World_Simulation_P1_Design.md) §8.

## An toàn

- **Mỗi bot ghi dữ liệu thật**: một tài khoản guest và một nhân vật. Chỉ chạy với server Development trỏ tới **database riêng dùng để thử** (đặt `KNIGHTONLINE_ConnectionStrings__KnightOnline` khi khởi động server). Bot từ chối chạy nếu thiếu `--allow-db-writes`.
- Bot chỉ chấp nhận host loopback.
- Server có giới hạn `capacity.maximumActiveAccounts` (mặc định 500) và `capacity.maximumTransportConnections` (750). Muốn thử nhiều hơn thì phải nâng các giới hạn này trên server thử.

## Chạy

```bash
dotnet run --project KnightBot -c Release -- --bots 50 --ramp 10 --duration 60 --allow-db-writes
```

| Tham số | Mặc định | Ý nghĩa |
|---|---|---|
| `--bots` | 50 | Số bot |
| `--ramp` | 10 | Số bot khởi động mỗi giây |
| `--duration` | 60 | Số giây chạy sau khi khởi động xong bot cuối |
| `--move-ms` | 100 | Khoảng gửi input di chuyển |
| `--host` / `--port` | 127.0.0.1 / 7777 | Server |
| `--server-id` | server-1 | `characters.serverId` của server |
| `--seed` | 1 | Seed cho hướng di chuyển ngẫu nhiên |

Exit code `0` khi mọi bot vào được thế giới, `1` nếu có bot thất bại, `2` nếu tham số sai hoặc bị chặn vì lý do an toàn.

## Đọc kết quả

- **Time to world**: thời gian từ lúc mở TCP đến khi `EnterWorld` thành công (p50/p95/p99).
- **Move ack RTT**: thời gian từ khi gửi input đến khi nhận `PlayerPositionSnapshot` xác nhận input đó. Đây là chỉ số chính về độ phản hồi của server.
- **Entity snapshots**: số `WorldEntitiesSnapshot` nhận được và tổng spawn/move/despawn. Dùng để ước lượng băng thông mô phỏng thế giới.
- **Failures**: lỗi gom theo bước (ví dụ `CreateGuest: RateLimited`).

Mỗi lần đo cần ghi lại: cấu hình máy, commit, cấu hình `world.*` và kết quả theo bậc 50 → 200 → 500. Không công bố con số online khi chưa có số đo.
