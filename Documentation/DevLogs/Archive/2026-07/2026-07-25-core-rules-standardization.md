# DevLog — Chuẩn hóa quy tắc cốt lõi dự án

## Mục tiêu

Đánh giá, chuẩn hóa và ghim bộ quy tắc bắt buộc của KnightOnline vào repository.

## Hoàn thành

- Đọc và đánh giá bản quy tắc gốc gồm 38 điều.
- Gom các nội dung trùng lặp thành 28 quy tắc có mã ổn định.
- Bổ sung phân loại rủi ro và Definition of Done theo phạm vi.
- Bổ sung session lease, TLS, token rotation, event Outbox, idempotency,
  compatibility, data governance và exception policy.
- Bổ sung quy tắc đặc thù Unity về `.meta`, asmdef và serialized reference.
- Quy định vị trí và nội dung DevLog bắt buộc.
- Thêm chỉ dẫn repository để các phiên agent sau luôn đọc Core Rules trước khi
  thay đổi dự án.

## Files Changed

- `Documentation/KNIGHT_PROJECT_CORE_RULES.md`
- `Documentation/DevLogs/2026-07-25-core-rules-standardization.md`
- `AGENTS.md`

## Quyết định kỹ thuật

- Không áp dụng audit/Admin API/domain event máy móc cho Presentation UI.
- Quy tắc được áp dụng theo mức Critical, Authoritative gameplay,
  Presentation hoặc Tooling/prototype.
- Ngoại lệ phải có owner, rủi ro và thời hạn; không cho ngoại lệ với secret,
  authorization, account/economy integrity hoặc data safety.

## Kiểm tra

- Kiểm tra cấu trúc Markdown và encoding UTF-8.
- Đối chiếu các quy tắc với kiến trúc Unity client, .NET server và PostgreSQL.

## Rủi ro và tồn tại

- `.gitignore` hiện vẫn bỏ qua phần lớn file Unity `.meta`, chưa tuân thủ
  `CORE-22`.
- Repository hiện còn một số `bin/obj` từng được Git theo dõi từ trước.
- Chưa có CI và test suite đủ để thực thi toàn bộ Definition of Done.
- Quy trình hiện tại vẫn push trực tiếp `main`; branch protection chưa được bật.

## Tiếp theo

- Thực hiện task riêng để sửa `.gitignore` và đưa toàn bộ Unity `.meta` cần
  thiết vào Git mà không làm mất GUID.
- Chuẩn hóa thư mục DevLogs cho các checkpoint trước.
- Thiết lập CI, test foundation và branch protection trước giai đoạn nhóm.
