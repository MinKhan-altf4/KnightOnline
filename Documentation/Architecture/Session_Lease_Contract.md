# Hợp đồng phiên và lease tài khoản

**Trạng thái:** áp dụng cho giai đoạn server hiện tại, một instance.

## Phạm vi triển khai

- Chỉ chạy **một tiến trình KnightServer trên một máy**. `SingleInstanceGuard` giữ mutex hệ điều hành trong một thread chuyên dụng; tiến trình thứ hai cùng máy bị từ chối. Trên Windows tên mutex dùng namespace `Global\` để bao phủ các session đăng nhập khác nhau.
- Môi trường hiện tại chỉ cho bind loopback (`127.0.0.1`). Không mở cổng cho LAN/Internet và không chạy nhiều replica/container. Với môi trường điều phối, cấu hình replica phải là `1`; mutex không khóa được giữa các máy hoặc namespace container tách biệt.
- Lease tài khoản ở bộ nhớ tiến trình, có TTL 20 giây, heartbeat 5 giây và grace 10 giây. Lease không cần tồn tại qua lần khởi động server vì mọi TCP connection cũ đã mất.
- Khi server khởi động lại, các kết nối TCP bị đóng. Client kết nối lại, gửi yêu cầu Resume/Login mới và nhận session generation mới. Startup xóa generation nhân vật còn sót dưới singleton guard; vị trí/map được khôi phục từ snapshot DB gần nhất.
- Chỉ xem xét lease bền vững bằng DB/Redis khi cần nhiều instance hoặc cần giữ trạng thái lease xuyên khởi động lại. Khi đó phải thay adapter lease và kiểm chứng fencing đa node trước khi tăng replica.

## Quy tắc sở hữu và ghi nhân vật

- Account lease sở hữu `ConnectionId` và generation ngẫu nhiên; mọi renew, disconnect grace và release phải khớp cả hai.
- Khi chọn nhân vật, server ghi generation hiện tại vào hàng nhân vật. Snapshot chỉ cập nhật nếu account, nhân vật, generation và `Version` vẫn khớp; mỗi lần ghi tăng version.
- Snapshot được lưu định kỳ 5 giây, sau chuyển cổng và khi TCP kết thúc. Ghi snapshot của session cũ không thể đè session generation mới.
- Nếu server/process bị dừng đột ngột, có thể mất tối đa một chu kỳ snapshot vị trí; không tuyên bố checkpoint đồng bộ liên tục.

## Authentication retry

- Login và Resume bắt buộc có `RequestId`. Với Resume, client lưu ID đang chờ cùng refresh token trong local session store trước khi gửi; nếu ứng dụng bị tắt sau khi server rotate token nhưng trước khi nhận response, lần mở lại gửi lại cùng cặp token/ID. Login thủ công cần người chơi nhập lại thông tin nếu app tắt trước response.
- Server khóa theo RequestId trong transaction PostgreSQL, lưu fingerprint nội dung và kết quả auth được mã hóa cùng transaction với việc phát hành/rotate refresh token. Retry cùng ID/cùng nội dung nhận lại đúng kết quả; cùng ID nhưng nội dung hoặc operation khác bị từ chối. RequestId khác dùng lại refresh token cũ vẫn kích hoạt reuse detection.
- Receipt dùng key riêng trong .NET User Secrets hoặc biến môi trường `KNIGHTONLINE_Authentication__ReceiptKeys__<key-id>`, có `ActiveReceiptKeyId`; mỗi hàng ghi KeyId để giữ khóa cũ trong lúc xoay. Key active không lấy từ credential PostgreSQL.
- Receipt hết hạn sau 60 phút và có vòng dọn định kỳ 15 phút; auth request cũng xóa receipt quá hạn trong transaction. Khi hết hạn, retry cũ không còn được bảo đảm trả kết quả cũ.

## Kiểm thử và bot

- Test hiện có kiểm tra tranh lease đồng thời, TTL, grace, client suspend/mất mạng và cleanup generation cũ. PostgreSQL integration test dùng schema tạm trong DB được chỉ định bởi `KNIGHTONLINE_TEST_POSTGRES_CONNECTION`, kiểm tra version conflict xen thưởng tutorial, cùng RequestId chỉ rotate một lần và receipt hết hạn được xóa.
- Bot kiểm thử theo hai đợt: **đợt 1** login/lease/tranh chấp hai client; **đợt 2** restart/reconnect và xác nhận snapshot map/vị trí được nạp. Các test lease và persistence nền đã có; harness bot đầu-cuối restart/reconnect vẫn cần bổ sung.
- Đồng hồ lease đi qua `IServerClock` hoặc tham số UTC tại store; test TTL/grace dùng đồng hồ giả và không phải chờ thời gian thực.

## Cổng trước tester ngoài mạng

Trong cấu hình Development loopback, IP rate limit tắt để các bot cùng máy không dồn vào bucket `127.0.0.1`; giới hạn theo định danh vẫn bật và có thể điều chỉnh trong `serverSettings.json`. IP rate limit mặc định bật và không thể tắt ngoài Development.

Transport hiện là TCP packet framing, không phải HTTP, nên chưa đọc `X-Forwarded-For`. Khi đặt sau TLS proxy, cần lấy địa chỉ client từ proxy được allowlist (ví dụ PROXY protocol); bỏ qua metadata chuyển tiếp từ peer không tin cậy.

Chưa mở cổng ngoài mạng. Trước khi cho tester bên ngoài kết nối, cần hoàn tất và kiểm tra:

1. TLS cho transport game.
2. Token lưu trong Keychain/Keystore/Credential Manager trên client.
3. Rate limit theo IP, account và device. Hiện giới hạn identity/account ở bộ nhớ tiến trình; cần kiểm thử tải và chính sách phân tán nếu triển khai nhiều instance.
4. Broadcast theo vùng quan tâm và hàng đợi gửi có giới hạn cho từng connection. Broadcast hiện có lọc map nhưng gửi tuần tự và chưa có bounded queue.
5. Chạy bot đợt 1/2, migration PostgreSQL và kiểm thử khôi phục snapshot.

## Tiền và vật phẩm

Mọi thay đổi tiền hoặc vật phẩm phải cùng transaction với ledger có `RequestId`; không chờ checkpoint. Thưởng vật phẩm tutorial hiện nằm trong transaction cùng tiến độ/command/outbox. Hệ thống ví/tiền chưa được triển khai; không có số dư để cập nhật trong giai đoạn này.
