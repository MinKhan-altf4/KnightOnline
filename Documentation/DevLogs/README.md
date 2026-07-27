# KnightOnline DevLogs

Thư mục này lưu **bản tổng kết của các phiên làm việc đã kết thúc**. DevLog là
lịch sử thay đổi và bằng chứng bàn giao; DevLog không phải đặc tả hiện hành của
game.

## Người mới nên đọc theo thứ tự nào?

1. `Documentation/KNIGHT_PROJECT_CORE_RULES.md` — quy tắc bắt buộc.
2. Tài liệu luồng hoặc kiến trúc được chủ dự án giao cho task.
3. DevLog gần nhất có liên quan trực tiếp đến module được giao.
4. Code, test và cấu hình hiện tại — nguồn xác nhận trạng thái triển khai thực tế.

Nếu DevLog mâu thuẫn với tài liệu luồng hiện hành hoặc quyết định mới của chủ dự
án, không tự chọn một phía. Ghi lại điểm mâu thuẫn và xác nhận trước khi thay đổi
logic nghiệp vụ.

## Cấu trúc thư mục

```text
Documentation/DevLogs/
├── README.md
├── SESSION_DEVLOG_TEMPLATE.md
├── YYYY-MM-DD-task-name.md
└── Archive/
    └── YYYY-MM/
        └── YYYY-MM-DD-task-name.md
```

- Chỉ tạo **một DevLog tổng kết cho một phiên làm việc** khi chủ dự án yêu cầu
  viết log hoặc kết thúc phiên.
- DevLog phiên mới nằm trực tiếp tại `Documentation/DevLogs/` theo quy định của
  repository để dễ thấy trong quá trình bàn giao.
- `Archive/YYYY-MM/` chỉ chứa log lịch sử đã được chủ dự án cho phép dọn khỏi
  danh sách làm việc hiện tại. Không tự ý chuyển hoặc xóa DevLog đang được dùng.
- Không tạo log cho từng lỗi nhỏ, checkpoint hoặc thao tác Unity.
- Không sửa lịch sử để làm nó giống trạng thái hiện tại. Nếu quyết định thay đổi,
  ghi trong DevLog của phiên mới và đánh dấu tài liệu nguồn sự thật liên quan.

## Chỉ mục lịch sử

| Ngày | Phạm vi | File | Trạng thái khi đọc |
|---|---|---|---|
| 2026-07-25 | Chuẩn hóa Core Rules | [core-rules-standardization](Archive/2026-07/2026-07-25-core-rules-standardization.md) | Lịch sử; Core Rules hiện tại mới là nguồn sự thật |
| 2026-07-25 | Audit tuân thủ kiến trúc/bảo mật | [core-rules-compliance-audit](Archive/2026-07/2026-07-25-core-rules-compliance-audit.md) | Baseline cũ; số test và technical debt có thể đã thay đổi |
| 2026-07-25 | Admin Management Contract | [admin-management-core-rule](Archive/2026-07/2026-07-25-admin-management-core-rule.md) | Lịch sử quyết định; xem `CORE-11` hiện tại |
| 2026-07-25 | Unity Authentication Entry UI | [unity-authentication-entry-ui](Archive/2026-07/2026-07-25-unity-authentication-entry-ui.md) | Lịch sử triển khai; scene hiện tại có thể đã thay đổi |
| 2026-07-26 | Authentication, Registration, Character Session | [auth-registration-character-session](Archive/2026-07/2026-07-26-auth-registration-character-session.md) | Lịch sử; quy tắc timeout Character Select 15 giây đã bị loại bỏ sau phiên này |

## Nguồn sự thật theo phạm vi

| Phạm vi | Tài liệu ưu tiên |
|---|---|
| Quy tắc kỹ thuật bắt buộc | `Documentation/KNIGHT_PROJECT_CORE_RULES.md` |
| Luồng game và HUD | `Documentation/Design/System_And_HUD_Flow_v2.md` |
| Character Flow | `Documentation/Design/Character_Flow_Architecture_Plan.md` |
| Chuyển Authentication/Character sang Production | `Documentation/Production/Account_And_Character_Flow_Cutover.md` |
| Đăng ký Production | `Documentation/Production/Registration_Flow.md` |
| Kiến trúc tổng quan | Chưa có bản hiện hành; không dùng bản Day 11 trong `Archive/` làm nguồn sự thật |

## Brief giao việc tối thiểu

Chủ dự án nên giao mỗi người một phạm vi rõ ràng:

```text
Mục tiêu:
Module sở hữu:
Trong phạm vi:
Ngoài phạm vi:
Tài liệu nguồn sự thật:
Acceptance criteria:
Test bắt buộc:
Dependency với người khác:
Rủi ro/điểm cần xác nhận:
```

Mỗi file chỉ có một người chịu trách nhiệm chính tại một thời điểm. Nếu hai task
cùng cần sửa một file hoặc contract, hai người phải thống nhất boundary trước khi
code để tránh ghi đè và tạo hai nguồn logic.
