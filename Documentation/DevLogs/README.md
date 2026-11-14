# Nhật ký phiên làm việc

DevLog ghi lại kết quả và bằng chứng tại thời điểm làm việc. Đây là lịch sử,
không phải mô tả tự cập nhật về trạng thái code hiện tại. Xem
[Tình trạng dự án](../Trang_Thai_Du_An.md) để bắt đầu.

## Quy tắc ghi nhật ký

- Chỉ tạo/cập nhật DevLog khi chủ dự án yêu cầu ghi log hoặc chốt phiên.
- Một DevLog tổng kết một phiên; không tạo file cho từng lỗi nhỏ hay checkpoint.
- Giữ lại quyết định, migration, rủi ro và kết quả test quan trọng.
- Không sửa lịch sử để khiến trạng thái cũ trông như hiện trạng mới.
- File mới đặt theo mẫu `YYYY-MM-DD-task-name.md` trong thư mục này.
- Không ghi secret, token hoặc dữ liệu nhạy cảm.

## Các mốc gần nhất

| Ngày | Mốc | Tình trạng ghi trong log |
|---|---|---|
| 14-11-2026 | [Ổn định P0 và mô phỏng thế giới P1](2026-11-14-p0-stabilization-and-p1-world-simulation.md) | 134 unit + 18 integration test đạt; bot 500 vào game 0 lỗi; protocol 3; test EditMode và hai client trong Unity còn chờ (có hướng dẫn test trong log). |
| 11-11-2026 | [Lease một instance và fencing phiên](2026-11-11-single-instance-session-fencing.md) | 86/86 test qua cùng PostgreSQL; migration đã áp dụng, bot restart/reconnect và cổng tester ngoài mạng còn mở. |
| 04-08-2026 | [Vertical slice nhiệm vụ tân thủ](2026-08-04-starter-tutorial-vertical-slice.md) | Test server tại thời điểm đó đạt; kiểm tra cổng trong Unity còn chờ. |
| 04-08-2026 | [Sinh lực, HUD và đăng xuất](2026-08-04-phase-1-vitals-hud-and-session-exit.md) | Test server tại thời điểm đó đạt; smoke test Unity cuối phiên còn chờ. |
| 03-08-2026 | [Gameplay authoritative và HUD](2026-08-03-authoritative-alpha-foundation-and-hud.md) | Test/build server đạt tại thời điểm ghi; HUD cần kiểm tra hình ảnh nhiều tỉ lệ màn hình. |
| 02-08-2026 | [Character và EnterWorld](2026-08-02-character-world-foundation.md) | Test server đạt; một số kiểm tra Unity/movement còn cần làm thủ công. |
| 02-08-2026 | [Baseline và session lease](2026-08-02-baseline-and-session-lease.md) | Test server đạt; Unity A/B và các yêu cầu Production còn mở. |

Nhật ký và tài liệu trước tháng 08-2026 đã gom tại [tóm tắt lịch sử](../Archive/Tom_Tat_Lich_Su.md).
Không dùng tài liệu Archive làm tiêu chí nghiệm thu nếu tài liệu hiện hành hoặc
quyết định mới hơn có nội dung khác.

## Tài liệu ưu tiên theo phạm vi

| Phạm vi | Tài liệu hiện hành |
|---|---|
| Quy tắc bắt buộc | `Documentation/KNIGHT_PROJECT_CORE_RULES.md` |
| Tình trạng và bước tiếp theo | `Documentation/Trang_Thai_Du_An.md` |
| Cấu trúc source | `Documentation/Architecture/Repository_Structure.md` |
| Luồng runtime và giới hạn Alpha | `Documentation/Architecture/Current_Unity_Runtime_And_Alpha_500_Online.md` |
| Hợp đồng lease/phiên và cổng mạng | `Documentation/Architecture/Session_Lease_Contract.md` |
| Luồng game/HUD | `Documentation/Design/System_And_HUD_Flow_v2.md` |
| Luồng nhân vật | `Documentation/Design/Character_Flow_Architecture_Plan.md` |
| Sai sót đã biết và hướng đi | `Documentation/Audits/2026-11-13-danh-gia-luong-va-huong-di.md` |

## Mẫu giao việc

```text
Mục tiêu:
Module sở hữu:
Trong phạm vi:
Ngoài phạm vi:
Tài liệu nguồn:
Điều kiện nghiệm thu:
Kiểm thử cần chạy:
Dependency:
Rủi ro/điểm cần xác nhận:
```
