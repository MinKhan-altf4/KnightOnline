# Phase 1 — Architecture & Foundation
## The World of Knights & Demons

> **Status**: Awaiting Approval  
> **Role**: Technical Director Review  
> **Scope**: KnightClient Architecture Foundation

---

## 🎯 Mục tiêu Phase 1

Trước khi viết một dòng gameplay, chúng ta phải trả lời được 5 câu hỏi:

1. **Code nằm ở đâu?** → Folder Structure
2. **Ai phụ thuộc ai?** → Assembly Definitions & Dependency Graph
3. **Client biết gì, không biết gì?** → Client-Server Boundary
4. **Code viết theo chuẩn nào?** → Coding Convention
5. **Tài liệu lưu ở đâu?** → Documentation Standard

Nếu không trả lời được 5 câu này trước khi code → **dự án sẽ sụp đổ sau 6 tháng.**

---

## 1. Folder Structure

### Nguyên tắc thiết kế:
- **Feature-first**, không phải Type-first
- Mỗi folder = 1 domain rõ ràng
- Không có folder "Misc", "Common", "Utils" chứa mọi thứ
- `_Project` dùng underscore để luôn đứng đầu trong Unity

```
KnightClient/
└── Assets/
    ├── _Project/                        ← Toàn bộ code và asset của team
    │   │
    │   ├── Core/                        ← Framework layer (KHÔNG chứa game logic)
    │   │   ├── Bootstrap/               ← Khởi động game, DI setup
    │   │   ├── Services/                ← Service interfaces & base classes
    │   │   ├── Events/                  ← Event bus, message system
    │   │   ├── StateMachine/            ← Generic FSM dùng chung
    │   │   └── Extensions/              ← C# extension methods thuần
    │   │
    │   ├── Network/                     ← Client-side networking ONLY
    │   │   ├── Connection/              ← TCP connection management
    │   │   ├── Packets/                 ← Packet definitions (nhận từ KnightShared)
    │   │   ├── Handlers/                ← Xử lý packet nhận về từ Server
    │   │   └── Senders/                 ← Gửi packet lên Server
    │   │
    │   ├── Gameplay/                    ← Game systems (phụ thuộc Core)
    │   │   ├── Player/                  ← Player state, controller
    │   │   ├── Entity/                  ← Base entity (Monster, NPC, Player)
    │   │   ├── Combat/                  ← Combat visual & animation trigger
    │   │   ├── Inventory/               ← Local inventory display
    │   │   ├── Equipment/               ← Equipment visual
    │   │   ├── Quest/                   ← Quest tracking display
    │   │   ├── World/                   ← Map, zone, scene management
    │   │   ├── Chat/                    ← Chat system
    │   │   └── Guild/                   ← Guild UI logic
    │   │
    │   ├── UI/                          ← UI layer (phụ thuộc Core + Gameplay)
    │   │   ├── Framework/               ← UIManager, Screen base, transitions
    │   │   ├── Screens/                 ← Màn hình cụ thể (Login, Lobby, HUD...)
    │   │   ├── Components/              ← Reusable UI components
    │   │   └── HUD/                     ← In-game HUD elements
    │   │
    │   ├── Input/                       ← Input handling (phụ thuộc Core)
    │   │   ├── InputReader/             ← New Input System reader
    │   │   └── InputActions/            ← .inputactions files
    │   │
    │   ├── Audio/                       ← Audio management
    │   │   ├── AudioService/            ← Service interface & implementation
    │   │   └── AudioData/               ← Audio configs (ScriptableObjects)
    │   │
    │   ├── Data/                        ← ScriptableObjects & config data
    │   │   ├── Items/                   ← Item definitions
    │   │   ├── Skills/                  ← Skill definitions
    │   │   ├── Characters/              ← Character class definitions
    │   │   ├── Monsters/                ← Monster data (client-side display only)
    │   │   └── Configs/                 ← Game configs (không chứa gameplay logic)
    │   │
    │   ├── Art/                         ← Artist assets (KHÔNG có code)
    │   │   ├── Characters/
    │   │   ├── Environments/
    │   │   ├── VFX/
    │   │   ├── UI/
    │   │   └── Audio/
    │   │
    │   └── Scenes/                      ← Unity Scenes
    │       ├── Bootstrap.unity          ← Scene đầu tiên luôn load
    │       ├── Login.unity
    │       ├── CharacterSelect.unity
    │       └── World/
    │           └── Zone_01.unity
    │
    ├── Plugins/                         ← Third-party plugins
    └── StreamingAssets/                 ← Runtime assets (config files)
```

---

## 2. Assembly Definitions

### Tại sao cần Assembly Definitions?

Không có Assembly Definitions → Unity compile toàn bộ `Assets/` thành **một assembly duy nhất**. Điều này có nghĩa:
- ❌ Mọi thứ phụ thuộc mọi thứ
- ❌ Không kiểm soát được dependency
- ❌ Compile lại toàn bộ khi sửa 1 file
- ❌ Không phát hiện circular dependency

Với Assembly Definitions:
- ✅ Compile độc lập từng module
- ✅ Dependency được enforce bởi compiler
- ✅ Build nhanh hơn đáng kể
- ✅ Dễ viết Unit Test

### Danh sách Assembly Definitions:

| Assembly | Path | Mô tả |
|----------|------|--------|
| `KnightOnline.Core` | `_Project/Core/` | Framework core, không có game logic |
| `KnightOnline.Data` | `_Project/Data/` | ScriptableObjects, data definitions |
| `KnightOnline.Input` | `_Project/Input/` | Input handling |
| `KnightOnline.Network` | `_Project/Network/` | Client networking |
| `KnightOnline.Audio` | `_Project/Audio/` | Audio system |
| `KnightOnline.Gameplay` | `_Project/Gameplay/` | Game systems |
| `KnightOnline.UI` | `_Project/UI/` | UI layer |
| `KnightOnline.Core.Tests` | `_Project/Tests/Core/` | Core unit tests (Editor only) |
| `KnightOnline.Gameplay.Tests` | `_Project/Tests/Gameplay/` | Gameplay unit tests |

---

## 3. Dependency Graph (LUẬT BẤT BIẾN)

```
┌─────────────────────────────────────────────────────┐
│                   DEPENDENCY RULES                   │
│                                                      │
│   Mũi tên = "phụ thuộc vào"                         │
│   Không được tạo mũi tên ngược chiều                │
└─────────────────────────────────────────────────────┘

              [Data]
                │
                ▼
             [Core]
            /  │  \
           /   │   \
          ▼    ▼    ▼
      [Input] [Network] [Audio]
          \         \
           \         \
            ▼         ▼
          [Gameplay] ──────► [UI]


TUYỆT ĐỐI KHÔNG ĐƯỢC:
  ❌ Core → Gameplay
  ❌ Core → UI
  ❌ Gameplay → UI (UI nhận event từ Gameplay qua EventBus)
  ❌ Network → Gameplay trực tiếp (dùng Handler/Event)
  ❌ Data → Gameplay (Data là pure definitions)
```

### Giải thích từng rule:

**`Core` không phụ thuộc ai** — Core là nền móng. Nếu Core phụ thuộc Gameplay thì bạn đang xây nhà từ mái xuống.

**`Gameplay` không gọi thẳng vào `UI`** — UI là presentation layer. Gameplay không nên biết UI tồn tại. Thay vào đó Gameplay raise Event, UI subscribe Event. Nếu xóa toàn bộ UI, Gameplay vẫn phải hoạt động được.

**`Network` không inject thẳng vào `Gameplay`** — Network nhận packet → raise Event hoặc gọi Service interface → Gameplay react. Điều này cho phép test Gameplay mà không cần server thật.

---

## 4. Client-Server Boundary Specification

> [!IMPORTANT]
> Đây là tài liệu quan trọng nhất của dự án. Mọi developer phải đọc và ký xác nhận hiểu trước khi code.

### GOLDEN RULE:
**Client là màn hình hiển thị. Client không biết sự thật.**

### Client được phép làm:

| Category | Được phép | Lý do |
|----------|-----------|-------|
| **Rendering** | Render mọi thứ Server gửi về | Hiển thị trạng thái |
| **Input** | Capture input → gửi lên Server | Input collection only |
| **Animation** | Trigger animation theo state | Visual feedback |
| **Audio** | Play sound theo event | User experience |
| **UI** | Hiển thị data Server cung cấp | Presentation |
| **Prediction** | Client-side movement prediction | Giảm lag cảm giác |
| **VFX** | Spawn VFX theo event từ Server | Visual only |

### Client TUYỆT ĐỐI KHÔNG được làm:

| Category | Không được | Hậu quả nếu vi phạm |
|----------|-----------|---------------------|
| **Damage** | Tính damage | Hacker chỉnh packet → one shot mọi người |
| **HP/MP** | Tự cộng/trừ HP | Hacker bất tử |
| **Drop** | Quyết định item drop | Hacker farm infinite item |
| **Inventory** | Validate item hợp lệ | Hacker duplicate item |
| **Trade** | Xác nhận trade hợp lệ | Hacker dupe item |
| **Quest** | Đánh dấu quest hoàn thành | Hacker bỏ qua quest |
| **Position** | Authority về vị trí cuối | Speed hack |
| **Skill** | Quyết định skill có thể dùng | Skill hack |
| **Gold** | Tính toán gold | Economy exploit |

### Packet Flow:

```
[Client Input] ──────────────────► [Server]
                                      │
                    Validate          │
                    Calculate         │
                    Authorize         │
                                      │
[Client State Update] ◄──────────────┘
     │
     ├── Render
     ├── Update UI
     ├── Play Sound
     └── Trigger Animation
```

### Ví dụ cụ thể — Player Attack:

```
❌ WRONG (Tutorial style):
Client: if (Input.Attack) { 
    target.HP -= damage;     // Client tự tính damage
    PlayHitEffect();
}

✅ CORRECT (MMORPG style):
Client: if (Input.Attack) {
    SendPacket(new AttackRequestPacket { 
        TargetId = target.Id,
        SkillId = selectedSkill.Id
    });
    // Không làm gì thêm. Chờ Server phản hồi.
}

Server: 
    Nhận AttackRequestPacket
    → Validate: Player có thể tấn công không?
    → Calculate: Damage = stats + skill + random
    → Apply: target.HP -= damage
    → Broadcast: AttackResultPacket { damage, newHP, effects }

Client (nhận AttackResultPacket):
    → Update target HP display
    → Play hit animation
    → Spawn damage number VFX
    → Play hit sound
```

---

## 5. Coding Conventions

### Namespace:
```csharp
// Pattern: KnightOnline.Client.{Module}.{SubModule}
namespace KnightOnline.Client.Core.Services { }
namespace KnightOnline.Client.Gameplay.Combat { }
namespace KnightOnline.Client.Network.Handlers { }
namespace KnightOnline.Client.UI.Screens { }
```

### Class Rules:

```csharp
// ✅ CORRECT — Single Responsibility
public class PlayerMovementHandler : MonoBehaviour
{
    // Chỉ xử lý movement display
    // Không biết về combat, inventory, UI
}

// ❌ WRONG — God Object
public class PlayerManager : MonoBehaviour
{
    public void Attack() { }
    public void OpenInventory() { }
    public void SendChatMessage() { }
    public void UpdateUI() { }
    // ... 2000 dòng
}
```

### Field Rules:

```csharp
// ✅ CORRECT
public class CombatDisplay : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private ParticleSystem _hitEffect;
    
    private IEventBus _eventBus;  // Inject qua DI
    
    // Public API qua property hoặc method rõ ràng
    public void ShowHitEffect(Vector3 position) { }
}

// ❌ WRONG
public class CombatDisplay : MonoBehaviour
{
    public Animator animator;          // Public field
    public static CombatDisplay Instance; // Singleton bừa bãi
}
```

### Event Pattern:

```csharp
// ❌ WRONG — Direct coupling
public class InventorySystem
{
    private UIInventory _ui;  // Gameplay biết UI tồn tại
    
    public void AddItem(Item item)
    {
        _items.Add(item);
        _ui.Refresh();  // Tight coupling
    }
}

// ✅ CORRECT — Event-driven
public class InventorySystem
{
    private IEventBus _eventBus;  // Chỉ biết EventBus
    
    public void AddItem(Item item)
    {
        _items.Add(item);
        _eventBus.Publish(new InventoryChangedEvent(item));
        // UI tự subscribe và update
    }
}
```

### Async Rules:

```csharp
// ❌ WRONG — Coroutine cho business logic
IEnumerator LoadPlayerData()
{
    yield return new WaitForSeconds(0.1f);
    // Logic trộn lẫn với timing
}

// ✅ CORRECT — UniTask
private async UniTask LoadPlayerDataAsync(CancellationToken ct)
{
    var data = await _networkService.GetPlayerDataAsync(ct);
    ApplyPlayerData(data);
}
```

---

## 6. Naming Convention

| Type | Convention | Ví dụ |
|------|-----------|-------|
| Class | PascalCase | `PlayerMovementHandler` |
| Interface | I + PascalCase | `INetworkService` |
| Private field | _camelCase | `_playerController` |
| Public property | PascalCase | `CurrentHealth` |
| Method | PascalCase | `SendAttackPacket()` |
| Event | PascalCase + Event | `OnPlayerDeath` |
| Const | UPPER_SNAKE | `MAX_PLAYERS_PER_ZONE` |
| ScriptableObject | SO suffix | `ItemDefinitionSO` |
| MonoBehaviour | Tên chức năng rõ | `HealthBarDisplay` |
| Packet | Packet suffix | `AttackRequestPacket` |
| Event class | Event suffix | `PlayerDeathEvent` |
| Scene | PascalCase | `CharacterSelect.unity` |
| Assembly | KnightOnline.{Module} | `KnightOnline.Core` |

---

## 7. Documentation Standard

Mọi file tài liệu phải ở `Documentation/` trong monorepo root.

### Cấu trúc tài liệu:

```
Documentation/
├── Architecture/
│   ├── Overview.md               ← Tổng quan hệ thống
│   ├── ClientServerBoundary.md   ← Tài liệu này (xem Section 4)
│   ├── AssemblyDependencies.md   ← Dependency graph
│   └── NetworkProtocol.md        ← Packet protocol spec
│
├── Gameplay/
│   ├── CombatFormula.md          ← Công thức combat
│   ├── DropSystem.md             ← Hệ thống drop
│   ├── EconomyDesign.md          ← Economy design
│   └── SkillSystem.md            ← Skill system design
│
├── Operations/
│   ├── SetupGuide.md             ← Hướng dẫn setup dev environment
│   ├── CodingConventions.md      ← Coding rules
│   └── GitWorkflow.md            ← Git branching strategy
│
└── GameDesign/
    ├── GDD.md                    ← Game Design Document
    ├── Roadmap.md                ← Roadmap
    └── Todo.md                   ← Current tasks
```

---

## 8. Git Workflow (Branch Strategy)

```
main                ← Production only. Protected.
│
├── develop         ← Integration branch
│   │
│   ├── feature/player-movement
│   ├── feature/inventory-ui
│   ├── feature/network-connection
│   └── fix/login-crash
```

### Rules:
- **Không push thẳng vào `main`** bao giờ
- **Mọi feature phải qua `develop`** trước
- **Commit message** theo Conventional Commits:
  - `feat(client): Add player movement handler`
  - `fix(network): Fix packet deserialization crash`
  - `chore(arch): Setup assembly definitions`
  - `docs: Update client-server boundary spec`

---

## 9. Phase 1 Execution Plan

Sau khi approve plan này, thực hiện theo thứ tự:

- [ ] **Task 1.1** — Tạo folder structure đầy đủ trong `KnightClient/Assets/_Project/`
- [ ] **Task 1.2** — Tạo 7 file `.asmdef` (Assembly Definitions)
- [ ] **Task 1.3** — Verify dependency graph không có circular dependency
- [ ] **Task 1.4** — Tạo `Documentation/Architecture/` với 3 tài liệu cốt lõi
- [ ] **Task 1.5** — Tạo `Documentation/Operations/CodingConventions.md`
- [ ] **Task 1.6** — Tạo `Documentation/Operations/GitWorkflow.md`
- [ ] **Task 1.7** — Commit và push toàn bộ lên `KnightOnline`
- [ ] **Task 1.8** — **Review:** Kiểm tra toàn bộ trước khi sang Phase 2

---

## ⚠️ Open Questions — Cần xác nhận trước khi thực hiện

> [!IMPORTANT]
> **Câu hỏi 1**: Dependency Injection Framework
> - **VContainer** (nhẹ, nhanh, Unity-native) — Khuyến nghị
> - **Zenject/Extenject** (mature, phổ biến hơn, nặng hơn)
> - **Manual Service Locator** (tự viết, kiểm soát hoàn toàn)

> [!IMPORTANT]
> **Câu hỏi 2**: Async Framework
> - **UniTask** — Khuyến nghị (zero allocation, Unity-native)
> - **Task/async-await thuần** (ít dependency hơn)

> [!IMPORTANT]
> **Câu hỏi 3**: Unity Version
> - Unity version nào đang dùng? (ảnh hưởng đến Input System, Addressables API)

> [!NOTE]
> **Câu hỏi 4**: Git Workflow
> - Hiện tại làm một mình hay đã có team?
> - Cần thiết lập branch protection ngay chưa?

---

*Technical Director Sign-off Required — Do not proceed to Phase 2 without completing Phase 1 Review.*
