# DevLog — Lease một instance, retry authentication và snapshot nhân vật

**Ngày:** 11-11-2026  
**Phạm vi:** hoàn thiện nền lease/phiên đăng nhập và điều kiện thử nghiệm nội bộ theo yêu cầu phiên làm việc.

## Đã triển khai

- Chốt hợp đồng hiện tại chỉ chạy một KnightServer trên một máy. `SingleInstanceGuard` giữ mutex qua thread riêng, dùng namespace Windows `Global\`, và từ chối tiến trình thứ hai kể cả ở session đăng nhập khác. Server chỉ bind `127.0.0.1`; lease tài khoản vẫn nằm trong bộ nhớ tiến trình.
- Ghi rõ khi tiến trình khởi động lại, mọi TCP connection cũ đóng, client kết nối lại và xin generation phiên mới. Startup xóa fence nhân vật còn sót trong khi singleton guard đang được giữ.
- Bổ sung `RequestId` cho Login/Resume. Client lưu ID Resume đang chờ cùng refresh token trước khi gửi, để retry còn nguyên ID sau khi đóng/mở lại ứng dụng. Server khóa theo RequestId trong transaction, lưu fingerprint và kết quả auth được AES-GCM bảo vệ cùng transaction phát hành/rotate refresh token. Cùng ID/cùng request trả lại cùng kết quả; cùng ID với operation/nội dung khác bị từ chối.
- Chuyển receipt sang key ring riêng trong User Secrets với key ID; key active `v2` không phụ thuộc connection string. Giữ `legacy-v1` trong cửa sổ chuyển tiếp để đọc receipt cũ. Receipt hết hạn sau 60 phút, được dọn định kỳ mỗi 15 phút và trong các transaction auth.
- Thêm migration `20261111074741_AuthenticationRequestReceipts` cho receipt, `20261111074313_CharacterSessionGenerationFence` cho generation nhân vật, và `20261111091318_AuthenticationReceiptKeyRotationAndExpiry` cho KeyId/expiry.
- Ghi snapshot map/spawn/vị trí mỗi 5 giây, sau teleport và khi TCP ngắt. Update có điều kiện account + generation + `Version`; sau version conflict, repository nạp lại version và thử tối đa 8 lần. Generation cũ bị từ chối và hết lượt retry được log; snapshot không đổi không tăng version.
- Khi client logout khỏi session nhân vật, lưu snapshot và nhả fence nhân vật trước khi detach.
- Bổ sung rate limit theo IP cùng account/device/token đã băm vào bộ nhớ. Cấu hình Development loopback tắt IP bucket chung và cho phép 500 attempt/phút để chạy bot; ngoài Development không cho tắt IP limiter. Ghi rõ chưa có forwarded-IP support; TCP hiện tại phải dùng proxy metadata được allowlist trước khi mở ra mạng ngoài.
- Rà test lease/concurrency sẵn có; thêm test hai kết nối TCP tranh lease, key rotation, và PostgreSQL integration test trên schema tạm cho version conflict xen thưởng tutorial, duplicate Resume RequestId, và dọn receipt hết hạn.
- Xác nhận server target `net10.0`; sửa tài liệu cấu trúc còn ghi .NET 8.
- Thêm [hợp đồng phiên và lease](../Architecture/Session_Lease_Contract.md), bao gồm điều kiện chạy một instance, retry semantics, thứ tự bot và các cổng bảo mật trước tester ngoài mạng.

## Kiểm tra

- `dotnet build KnightServer/KnightServer.csproj -c Release --no-restore` — thành công, 0 warning, 0 error.
- `dotnet test KnightServer.Tests/KnightServer.Tests.csproj -c Release --no-restore --verbosity quiet` với `KNIGHTONLINE_TEST_POSTGRES_CONNECTION` trỏ vào DB dev — 86/86 test qua, không skip.
- `git diff --check` — không còn lỗi whitespace; Git có cảnh báo quy tắc LF/CRLF của working tree.
- Ba migration đã được áp dụng lên DB dev bằng `dotnet-ef database update`; `migrations list` xác nhận không còn migration Pending.
- CLI phát cảnh báo `dotnet-ef 8.0.11` cũ hơn EF runtime `10.0.12`. Thử cập nhật tool thất bại vì môi trường không kết nối được NuGet (`127.0.0.1:9`); manifest tool chưa bị thay đổi.

## Rủi ro và phần còn lại

- PostgreSQL test dùng schema tạm trên database được chỉ định, chưa dùng Testcontainers. Các bot đầu-cuối restart/reconnect vẫn chưa được dựng/chạy; hai client test hiện xác nhận tranh lease, còn kiểm tra thao tác UI/gameplay thủ công vẫn cần thực hiện.
- Cần cập nhật tool `dotnet-ef` lên 10.0.x khi NuGet truy cập được để bỏ cảnh báo lệch phiên bản; migration hiện đã áp dụng thành công với tool 8.0.11.
- Receipt auth dùng key ring trong User Secrets. Giữ khóa cũ `legacy-v1` ít nhất hết cửa sổ receipt chuyển tiếp một giờ; có thể gỡ khóa sau khi hết hạn toàn bộ receipt legacy.
- Chưa mở tester ngoài mạng: TLS và kho token an toàn trên client chưa triển khai. Rate limiter theo identity hiện trong bộ nhớ; hỗ trợ IP sau TLS proxy chưa có. Broadcast lọc theo map nhưng chưa có hàng đợi gửi hữu hạn; chưa đạt điều kiện thử qua mạng công cộng.
- Ví/tiền chưa có trong source. Thưởng vật phẩm tutorial tiếp tục được ghi cùng transaction và command `RequestId`; mọi tính năng tiền/vật phẩm mới phải cập nhật ledger cùng transaction, không chờ checkpoint.
