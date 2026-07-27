# The World of Knights & Demons
## Architecture Documentation

> Đây là bản Architecture tổng hợp được viết lại dựa trên tài liệu Architecture hiện tại,
> DevLog (đến Day 11) và tài liệu Network/Phase 1.

## 1. Project Overview

**The World of Knights & Demons** là dự án MMORPG 2D sử dụng Unity cho Client và .NET cho Server.

Mục tiêu của dự án là xây dựng một nền tảng có khả năng mở rộng dài hạn theo kiến trúc **Server Authoritative**, trong đó:
- Server quyết định mọi game state.
- Client chỉ gửi input, render và hiển thị UI.

---

## 2. Current Development Status (Day 11)

### Đã hoàn thành

- Bootstrap
- TCP Connection
- Packet System
- Character Creation
- Character Select
- Scene Transition
- GameSession
- CharacterData
- Parent–Child LifetimeScope
- Player Controller
- Camera Follow
- Collision
- InGame HUD
- Player Prefab
- Player Idle/Walk Animation
- SpawnPoint
- NPC click + distance validation
- Dynamic NPC Dialog UI
- Event-driven NPC interaction lifecycle
- PostgreSQL persistence foundation
- Persistent Account/Character schema
- Packet dispatcher và feature handlers
- Server-side Monster domain foundation

### Chưa triển khai

- Combat
- Inventory
- Equipment
- Quest
- Monster AI
- Multiplayer Synchronization

---

## 3. Repository Structure

```
KnightOnline
├── KnightClient
├── KnightServer
└── Documentation
```

Monorepo được sử dụng để Client và Server chia sẻ mã nguồn dùng chung.

---

## 4. High-Level Architecture

```
Player
    │
Input
    │
PlayerController
    │
CharacterData
    │
Network
    │
TCP
    │
KnightServer
```

---

## 5. Scene Flow

```
App
 ↓
Bootstrap
 ↓
Character Select
 ↓
InGame
```

AppLifetimeScope tồn tại xuyên suốt vòng đời game.

---

## 6. Gameplay Flow

```
Start Game
 ↓
Connect Server
 ↓
Receive ConnectResponse
 ↓
Character Select
 ↓
Save CharacterData
 ↓
Load InGame
 ↓
Inject CharacterData
 ↓
Spawn Player
 ↓
Camera Follow
 ↓
HUD Update
```

---

## 7. Dependency Injection

- AppLifetimeScope
- GameLifetimeScope
- InGameLifetimeScope

App là Root Scope.

Game và InGame là Child Scope.

---

## 8. Networking

- TCP
- JSON
- Length-Prefixed Framing
- Packet Envelope

Server Authoritative.

### Packet handling

`Program.cs` chỉ còn là composition root và TCP accept loop.

```text
TcpClient
 ↓
ClientConnection
 ↓ read/write length-prefixed PacketEnvelope
PacketDispatcher
 ↓ lookup theo PacketType
IPacketHandler
 ├── ConnectPacketHandler
 ├── CreateCharacterPacketHandler
 └── ListCharactersPacketHandler
```

- `ClientConnection` sở hữu TCP stream, framing, serialization và send lock.
- `PacketDispatcher` từ chối đăng ký trùng một `PacketType`.
- Mỗi request packet có đúng một feature handler.
- Handler gọi repository/domain service và gửi response qua connection.
- Thêm packet mới không yêu cầu sửa networking loop hoặc `Program.cs` switch.

## 8.1 Persistence

KnightServer sử dụng PostgreSQL qua EF Core 8 và Npgsql.

```text
Packet Handler
 ↓
CharacterRepository
 ↓
KnightDbContext (mỗi operation một instance)
 ↓
PostgreSQL
```

Schema đầu tiên:

- `accounts`: application account identity.
- `characters`: character thuộc account, level và tên normalized.
- Tên nhân vật unique toàn server, không phân biệt hoa/thường.
- `CharacterId` do PostgreSQL identity sinh và tồn tại qua restart.

Trong development, mọi connection dùng account key `local-dev` vì authentication
chưa được triển khai. Đây là seam tạm thời; repository đã scope query theo
account để có thể thay bằng account thật sau này.

Connection string được đọc từ .NET User Secrets hoặc biến môi trường
`KNIGHTONLINE_ConnectionStrings__KnightOnline`, không lưu trong repository.

---

## 9. EventBus

Gameplay và Network không gọi trực tiếp UI.

Mọi notification đi qua EventBus.

### NPC Interaction Flow

```text
Mouse click
 ↓
PlayerInteraction
 ↓ validate Layer + distance
NpcInteractionRequestedEvent
 ↓ IEventBus
NpcDialogUI
 ↓ render snapshot + lock Player controls
NpcActionRequestedEvent
 ↓
Shop / Quest handler (chưa triển khai)
```

Quy tắc:

- `InteractableNPC` chỉ sở hữu cấu hình và tạo snapshot interaction.
- `PlayerInteraction` chịu trách nhiệm validate input/khoảng cách và publish
  `NpcInteractionRequestedEvent`.
- `NpcDialogUI` chỉ render và phát ý định người dùng.
- `Close` là hành vi presentation và được xử lý ngay tại dialog.
- Câu chào của NPC chính là nội dung hội thoại; không có action `Talk`.
- `Shop`, `Quest` không chứa nghiệp vụ trong UI; chúng publish
  `NpcActionRequestedEvent` để handler tương ứng xử lý.
- Mọi NPC luôn có đúng một nút `Close`; UI tự sinh nút này nếu data NPC
  không cấu hình.
- Event interaction không phải sticky event.
- Subscriber phải giữ `IDisposable` và dispose khi bị destroy.

### NPC Dialog Lifecycle

- Khi mở dialog: khóa movement và interaction lặp.
- Dialog đóng bằng Close, Escape hoặc click ngoài khung.
- Nếu NPC nguồn bị destroy, dialog tự đóng.
- Khi UI bị disable/destroy hoặc scene unload, dialog trả lại Player controls.
- Snapshot dữ liệu giúp UI không đọc trực tiếp danh sách mutable của NPC sau
  thời điểm interaction.

---

## 10. Character Lifecycle

```
Server
 ↓
Packet
 ↓
NetworkClient
 ↓
GameSession
 ↓
CharacterData
 ↓
PlayerController
 ↓
HUD
```

## 10.1 Monster Domain

Monster runtime state nằm trong memory phía Server, không ghi PostgreSQL theo
từng đòn đánh.

```text
MonsterDefinition
 ↓ immutable static configuration
Monster
 ↓ runtime HP / death / respawn time
MonsterService
 ↓ synchronized access
MonsterSnapshot / MonsterDamageResult
```

Domain hiện hỗ trợ:

- Validate definition, level, maximum HP và respawn delay.
- Sinh runtime `MonsterId`.
- Snapshot immutable cho networking sau này.
- Damage được clamp theo HP còn lại.
- Death và thời điểm respawn.
- Respawn khi server clock đạt thời điểm chỉ định.
- Kết quả rõ ràng cho not found, dead và invalid damage.

`Training Wolf` là instance development đầu tiên. Unity yêu cầu snapshot bằng
`ListMonstersRequest`; Server map domain snapshot sang shared DTO và trả
`ListMonstersResponse`. Client map DTO sang `MonsterData`, sau đó publish
`MonsterListReceivedEvent` dạng sticky. Chưa có combat handler hoặc Unity
prefab/presentation.

---

## 11. Technical Debt

- Animation mới chỉ có Idle/Walk placeholder, chưa hỗ trợ 4 hướng
- Chưa có authentication; mọi connection dùng `local-dev`
- Giới hạn bốn character hiện được enforce ở application layer
- Combat
- Chưa có consumer cho `NpcActionRequestedEvent`
- Chưa có automated tests cho EventBus và NPC dialog lifecycle

---

## 12. Roadmap

1. Play Test và ổn định NPC interaction vertical slice
2. Camera bounds
3. Monster
4. Combat
5. Inventory/Shop
6. Database
7. Multiplayer

---

## 13. Architecture Principles

- Feature-first
- Composition Root
- Dependency Injection
- Server Authoritative
- Event Driven
- Single Source of Truth
- Monorepo

---

## 14. References

Tài liệu này được tổng hợp từ:
- Architecture.md
- DevLog Day 1–Day 11
- Network Architecture
- Phase 1 Foundation

Đây là phiên bản khởi đầu để tiếp tục mở rộng thành tài liệu đầy đủ cho toàn bộ dự án.
