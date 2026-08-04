# KnightOnline Documentation

Đây là cổng vào duy nhất cho tài liệu dự án. Người mới không nên đọc tuần tự
toàn bộ thư mục; hãy đọc theo phạm vi công việc được chủ dự án giao.

## Bắt đầu tại đây

1. Đọc `KNIGHT_PROJECT_CORE_RULES.md`.
2. Đọc tài liệu nguồn sự thật của module được giao trong bảng bên dưới.
3. Đọc DevLog gần nhất có liên quan để biết bối cảnh triển khai.
4. Đối chiếu code, test và config hiện tại trước khi kết luận một chức năng đã
   hoàn thành.

## Cấu trúc

```text
Documentation/
├── README.md
├── KNIGHT_PROJECT_CORE_RULES.md
├── Architecture/
├── Design/
├── Production/
├── Audits/
├── DevLogs/
└── Archive/
```

| Thư mục | Mục đích | Có phải nguồn sự thật hiện tại? |
|---|---|---|
| `Architecture/` | Kiến trúc và luồng runtime đã đối chiếu trực tiếp với code hiện tại | Có, theo ngày/trạng thái ghi trong từng file |
| `Design/` | Luồng người chơi, game design, HUD và Character Flow | Có, theo đúng phạm vi của từng file |
| `Production/` | Điều kiện và kế hoạch thay adapter local khi mở server thật | Có cho kế hoạch cutover; phải đối chiếu code trước rollout |
| `Audits/` | Báo cáo kiểm tra tại một thời điểm | Không; là snapshot lịch sử |
| `DevLogs/` | Tổng kết và bàn giao theo phiên | Không; là lịch sử thay đổi |
| `Archive/` | Kế hoạch/kiến trúc cũ đã bị trạng thái dự án vượt qua | Không |

## Nguồn sự thật hiện hành

| Phạm vi | File |
|---|---|
| Quy tắc kỹ thuật bắt buộc | `KNIGHT_PROJECT_CORE_RULES.md` |
| Luồng Unity hiện tại, nhiều thiết bị và Alpha 500 online | `Architecture/Current_Unity_Runtime_And_Alpha_500_Online.md` |
| Luồng hệ thống và HUD | `Design/System_And_HUD_Flow_v2.md` |
| Character Flow | `Design/Character_Flow_Architecture_Plan.md` |
| Tầm nhìn và vòng lặp game | `Design/GameDesign.md` |
| Định hướng PvP | `Design/PvP_MMORPG_Plan.md` |
| Authentication/Character Production cutover | `Production/Account_And_Character_Flow_Cutover.md` |
| Đăng ký tài khoản Production | `Production/Registration_Flow.md` |
| Quy trình và chỉ mục DevLog | `DevLogs/README.md` |

Báo cáo baseline mới nhất:

- `Audits/2026-08-04-project-development-checkpoint.md`

Checkpoint này tổng hợp toàn bộ tiến trình từ nền móng repository đến Vertical
Slice nhiệm vụ tân thủ, đồng thời cung cấp checklist rà soát code định kỳ.

## Khoảng trống tài liệu đã xác nhận

- Đã có kiến trúc runtime cho Unity, authentication, Character Flow và đánh giá
  Alpha 500 online; vẫn chưa có sơ đồ toàn bộ module backend/gameplay ngoài phạm
  vi file này.
- Chưa có `Project Status/Roadmap` hiện hành để giao việc theo module.
- Các bản Day 11/Phase 1/Roadmap cũ đã được chuyển vào `Archive/` và không được
  dùng làm acceptance criteria.

Hai khoảng trống đầu tiên cần được xây dựng từ kết quả audit repository trong
đợt củng cố dự án, không sao chép trạng thái từ tài liệu legacy.

## Quy tắc cập nhật tài liệu

- Một quyết định nghiệp vụ chỉ có một tài liệu nguồn sự thật.
- Tài liệu lịch sử không được âm thầm sửa thành trạng thái mới.
- Khi thay đổi luồng, cập nhật đặc tả hiện hành trong cùng task.
- Khi kết thúc phiên và được chủ dự án yêu cầu, viết một DevLog tổng kết.
- File không còn đúng phải chuyển `Archive/` hoặc ghi trạng thái deprecated;
  không để lẫn với tài liệu hiện hành.
