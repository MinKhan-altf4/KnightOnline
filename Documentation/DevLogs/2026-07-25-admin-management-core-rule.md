# DevLog — Bổ sung Admin Management Contract

## Mục tiêu và phạm vi

Mở rộng Core Rules để mọi tính năng server/domain mới đều sẵn sàng được quan
sát và quản trị bằng Web Admin trong tương lai.

Phân loại: Architecture/Governance.

## Hoàn thành

- Mở rộng `CORE-11` thành yêu cầu Admin-ready bắt buộc.
- Định nghĩa nội dung tối thiểu của một Admin Management Contract.
- Bổ sung quy tắc cho bulk/dangerous admin operation.
- Bổ sung Admin Management Contract vào Definition of Done và checklist trước
  triển khai.
- Giữ nguyên boundary: Admin Web không truy cập database trực tiếp.

## Files Changed

- `Documentation/KNIGHT_PROJECT_CORE_RULES.md`
- `Documentation/DevLogs/2026-07-25-admin-management-core-rule.md`

## Quyết định kiến trúc

- Yêu cầu áp dụng cho server, domain và dữ liệu vận hành.
- Presentation UI thuần không phải tự tạo Admin API.
- Admin-ready không bắt buộc hoàn thành giao diện Web Admin trong cùng task,
  nhưng boundary, read model, permission, audit và extension path phải rõ.
- Admin command phải tái sử dụng application/domain service, không tạo logic
  nghiệp vụ riêng trong Admin API.

## Kiểm tra

- Kiểm tra Markdown bằng `git diff --check`.
- Đối chiếu với CORE-02, CORE-07, CORE-09, CORE-10, CORE-12 và CORE-28.

## Rủi ro và tồn tại

- Admin API/Web chưa được triển khai.
- Các module hiện có cần audit dần để bổ sung Admin Management Contract.
- Permission catalog và approval workflow chưa được thiết kế.

## Bước tiếp theo

- Mỗi task server/domain mới phải ghi phần Admin Management Contract trong
  DevLog.
- Khi bắt đầu Admin API, tạo permission catalog và read-model conventions dùng
  chung.
