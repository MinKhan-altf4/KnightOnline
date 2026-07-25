# DevLog — Disable Development Authentication Bypass

## Mục tiêu và phạm vi

Đồng bộ cấu hình authentication của server với Unity client để luồng `Chơi mới`
có thể tạo guest trên một anonymous connection.

Phân loại: Critical configuration — authentication.

## Công việc hoàn thành

- Tắt `authentication.developmentBypassEnabled` trong cấu hình server mặc định.
- Giữ nguyên development bypass implementation cho môi trường test chuyên biệt,
  nhưng không bật mặc định trong luồng authentication đang được kiểm thử.

## Nguyên nhân

Client đã tắt bypass trong scene `App`, trong khi server vẫn bật bypass. Server vì
vậy tự gắn account `local-dev` vào connection khi handshake. Request tạo guest
sau đó bị từ chối với `AlreadyAuthenticated`.

## Files Changed

- `KnightServer/serverSettings.json`
- `Documentation/DevLogs/2026-07-26-disable-development-authentication-bypass.md`

## Kiểm tra

- Chạy test project `KnightServer.Tests`.
- Build `KnightServer` ở cấu hình Release.
- Cần restart tiến trình server và manual test `Chơi mới` từ Unity.

## Migration và compatibility

- Không có database migration hoặc thay đổi packet contract.
- Server cũ đang chạy phải được restart để nạp cấu hình mới.

## Admin Management Contract

Không thêm domain state hoặc Admin command. Cấu hình environment này về sau nên
được quan sát dưới dạng read-only deployment configuration; Web Admin không được
phép bật authentication bypass trên Production.

## Rủi ro, rollback và bước tiếp theo

- Rollback development-only: bật lại cờ trong một settings file riêng dành cho
  automated test, không dùng cấu hình mặc định.
- Về sau handshake nên công bố authentication mode từ server để client không phụ
  thuộc vào một cờ bypass cấu hình độc lập.
