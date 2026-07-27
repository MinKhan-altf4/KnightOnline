# The World of Knights & Demons — Nhật ký Phát triển
## Day 13: Packet Dispatcher & Monster Domain

## Mục tiêu

1. Loại bỏ packet switch, framing và serialization khỏi `Program.cs`.
2. Tạo Monster domain server-authoritative trước khi thêm combat packet.

## Packet architecture

Đã thêm:

- `ClientConnection`: quản lý TCP lifecycle, length-prefixed framing,
  serialize/deserialize và send lock.
- `IPacketHandler`: contract một handler cho một request `PacketType`.
- `PacketDispatcher`: registry và dispatch packet tới handler tương ứng.
- `ConnectPacketHandler`.
- `CreateCharacterPacketHandler`.
- `ListCharactersPacketHandler`.

`Program.cs` hiện chỉ:

- Cấu hình database và migration.
- Khởi tạo repository/services.
- Đăng ký packet handlers.
- Khởi tạo world state.
- Accept TCP client và tạo `ClientConnection`.

## Monster domain

Đã thêm:

- `MonsterDefinition`: cấu hình immutable.
- `Monster`: runtime HP, alive/dead và respawn state.
- `MonsterSnapshot`: dữ liệu immutable chuẩn bị cho network DTO mapping.
- `MonsterDamageResult`: kết quả damage có status rõ ràng.
- `MonsterService`: quản lý instance thread-safe.
- `WorldPosition`: tọa độ world không phụ thuộc Unity.

Development world hiện spawn một `Training Wolf`:

```text
Level: 1
Maximum HP: 50
Spawn: (2, 2)
Respawn delay: 10 seconds
```

Monster HP là transient world state và không ghi PostgreSQL.

## Xác nhận

- KnightServer build: 0 warnings, 0 errors.
- Chưa thêm monster packet hoặc Unity prefab trong Day 13.

## Bước tiếp theo

1. Tạo shared packet cho monster snapshot/list.
2. Gửi monster state khi Client vào InGame.
3. Tạo Monster presentation/prefab phía Unity.
4. Sau đó mới thêm AttackRequest/AttackResult.
