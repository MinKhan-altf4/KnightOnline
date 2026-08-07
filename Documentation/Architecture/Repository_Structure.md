# Cấu trúc repository KnightOnline

Trạng thái đối chiếu: 2026-08-07  
Phạm vi: cấu trúc source hiện hành của Unity Client, .NET Server, test và tài
liệu.

> Đây là tài liệu kiến trúc hiện hành. Khi thêm, xóa hoặc di chuyển module, phải
> cập nhật file này trong cùng thay đổi. Vai trò và dependency của module vẫn
> phải tuân thủ `Documentation/KNIGHT_PROJECT_CORE_RULES.md`.

## 1. Cấu trúc cấp repository

```text
KnightOnline/
├── .github/                       # Workflow và cấu hình GitHub/CI
├── Builds/                        # Build local; không commit output
├── Documentation/                 # Kiến trúc, thiết kế, audit và DevLog
├── KnightClient/                  # Unity Client
├── KnightServer/                  # .NET 8 game server
├── KnightServer.Tests/            # Unit test server
├── KnightServer.IntegrationTests/ # PostgreSQL/network integration test
├── .gitattributes
├── .gitignore
├── AGENTS.md                      # Quy tắc làm việc trong repository
├── dotnet-tools.json              # Công cụ .NET/EF Core được ghim phiên bản
└── README.md                      # Điểm vào repository
```

Các thư mục `.git`, `.agents`, `.qodo`, `.tmp` và cache/cấu hình công cụ không
phải module sản phẩm. Không đặt domain logic trong các thư mục này.

## 2. Unity Client

```text
KnightClient/
├── Assets/
│   ├── _Project/                  # Asset và source riêng của KnightOnline
│   ├── TextMesh Pro/              # Tài nguyên TextMesh Pro
│   └── ...                        # Package/plugin Unity khác
├── Packages/
├── ProjectSettings/
└── UserSettings/                  # Cấu hình local, không commit
```

### 2.1 Asset riêng của dự án

```text
KnightClient/Assets/_Project/
├── Animations/       # Animation clip/controller
├── Art/              # Sprite, background, icon và UI art
├── Audio/            # Nhạc và âm thanh
├── Documentation/    # Tài liệu Unity cũ; không phải cổng tài liệu chính
├── Editor/           # Builder/validator chỉ chạy trong Unity Editor
├── Materials/        # Material và shader-facing asset
├── Prefabs/          # Prefab gameplay/UI
├── Resources/        # Asset cần load qua Unity Resources
├── Scenes/           # Scene runtime
├── ScriptableObjects/# Data asset Unity
├── Scripts/          # Source C# của client
└── Settings/         # Cấu hình asset của client
```

Mọi Unity asset phải đi cùng file `.meta`. Không commit `Library`, `Temp`,
`Logs`, `UserSettings` hoặc build output.

### 2.2 Scene hiện hành

```text
Scenes/
├── App.unity
├── Bootstrap.unity
└── InGame.unity
```

Luồng scene:

```text
App
  -> load Bootstrap theo Additive
  -> Entry/Authentication/Character Flow
  -> load InGame theo Single sau EnterWorld thành công
```

`Login.unity` đã bị loại bỏ. Không tạo lại scene đăng nhập độc lập nếu chưa có
quyết định kiến trúc mới.

## 3. Source Unity Client

```text
KnightClient/Assets/_Project/Scripts/
├── Core/
├── Data/
├── Gameplay/
├── Input/
├── Network/
├── Root/
├── Shared/
├── UI/
├── Combat/       # Hiện trống/legacy candidate
├── Inventory/    # Hiện trống; boundary sắp thiết kế
├── Managers/     # Hiện trống/legacy candidate
├── NPC/          # Hiện trống/legacy candidate
└── Player/       # Hiện trống/legacy candidate
```

### 3.1 Core

```text
Core/
├── Events/
│   ├── EventBus.cs
│   └── IEventBus.cs
├── GameManager.cs
├── SceneLoader.cs
├── ServiceLocator.cs
└── Singleton.cs
```

`Core/Events` là hạ tầng event nội bộ đang dùng. Các lớp `GameManager`,
`ServiceLocator`, `Singleton` và `SceneLoader` có dấu hiệu thuộc kiến trúc cũ;
phải kiểm tra reference trước khi giữ, thay hoặc xóa. Không bổ sung business
logic mới vào các global manager này.

### 3.2 Data

```text
Data/
├── Events/    # Event ứng dụng/presentation của Unity
└── Models/    # Model client như CharacterData, MonsterData
```

Network handler phải ánh xạ packet sang model/event tại boundary. UI và Gameplay
không nên parse JSON hoặc phụ thuộc trực tiếp protocol nếu có thể tránh.

### 3.3 Gameplay

```text
Gameplay/
├── Camera/     # Camera gameplay cũ/hiện hành theo từng class
├── Monster/    # MonsterView, MonsterSpawner và presentation quái
├── NPC/        # Tương tác NPC và event gameplay-facing
├── Player/     # PlayerController, interaction và spawn
├── Services/   # Authentication/character/gameplay-facing application service
├── Targeting/  # Chọn mục tiêu và selection marker
└── World/      # Map, portal, boundary và world presentation
```

Gameplay client chỉ dự đoán/hiển thị. Damage, HP/MP, EXP, inventory, quest,
map và vị trí cuối cùng do server quyết định.

### 3.4 Input

```text
Input/
├── IMovementInputProvider.cs
└── KeyboardMovementInput.cs
```

Keyboard là adapter hiện tại. Joystick/mobile input phải triển khai cùng contract
thay vì ghi điều khiển nền tảng trực tiếp vào `PlayerController`.

### 3.5 Network

```text
Network/
├── Handlers/
│   ├── IClientPacketHandler.cs
│   └── ClientPacketHandlers.cs
├── NetworkClient.cs
├── NetworkSettings.cs
└── KnightOnline.Client.Network.asmdef
```

Trách nhiệm:

- mở/đóng TCP;
- encode/decode `PacketEnvelope`;
- giới hạn kích thước packet;
- chuyển response server sang client event/model;
- không chứa business rule của account hoặc gameplay.

`ClientPacketHandlers.cs` đang gom nhiều domain trong một file. Khi module tăng,
nên tách handler theo `Authentication`, `Characters`, `World`, `Combat`,
`Tutorial` và `Inventory` mà không đổi contract công khai.

### 3.6 Root — composition root

```text
Root/
├── Bootstrap/
│   ├── AppLifetimeScope.cs
│   ├── CharacterFlowController.cs
│   ├── GameBootstrap.cs
│   ├── GameLifetimeScope.cs
│   └── GameSession.cs
└── InGame/
    ├── AuthoritativeNpcRequestPresenter.cs
    ├── InGameLifetimeScope.cs
    └── InGameSceneRoot.cs
```

- `AppLifetimeScope`: đăng ký dependency sống xuyên scene.
- `GameLifetimeScope`: composition của Bootstrap/Character Flow.
- `CharacterFlowController`: điều phối Character Select/Create/EnterWorld.
- `GameSession`: giữ snapshot client cần mang qua scene, không thay server truth.
- `InGameLifetimeScope`: composition của gameplay scene.
- `InGameSceneRoot`: khởi tạo map/world presentation.

Root chỉ ghép module và điều phối lifecycle; không đặt domain logic phức tạp tại
đây.

### 3.7 Shared packets

```text
Shared/
└── Packets/
    ├── PacketEnvelope.cs
    ├── PacketType.cs
    ├── authentication packets
    ├── character packets
    ├── combat/progression packets
    └── tutorial/world packets
```

Đây là contract hiện dùng chung giữa client và server. Contract mới phải xem xét
version, client cũ/server mới, unknown field/message và rollout compatibility.
Về lâu dài có thể tách thành project/package protocol độc lập.

### 3.8 UI

```text
UI/
├── AuthenticationEntryPanel.cs
├── AuthenticationEntryPresenter.cs
├── AuthenticationLoadingPanel.cs
├── AuthenticationPopupPanel.cs
├── CharacterCreationView.cs
├── CharacterSelectView.cs
├── ConnectionStatusView.cs
├── GuestRegistrationPanel.cs
├── GuestRegistrationPresenter.cs
├── InGameHUD.cs
├── InGameMenuView.cs
├── KnightUiTheme.cs
├── NpcDialogUI.cs
└── TargetHUD.cs
```

UI chỉ hiển thị và phát intent. UI không được tự cấp vật phẩm, đổi EXP, cập nhật
quest, teleport hoặc quyết định combat.

## 4. Assembly Unity

```text
KnightOnline.Client.Core
KnightOnline.Client.Data
KnightOnline.Client.Gameplay
KnightOnline.Client.Input
KnightOnline.Client.Network
KnightOnline.Client.Root
KnightOnline.Client.Shared
KnightOnline.Client.UI
```

Hướng dependency mong muốn:

```text
Shared
  ↑
Core + Data
  ↑
Network + Input + Gameplay
  ↑
UI
  ↑
Root (composition)
```

Đây là sơ đồ ý định. Mỗi đợt audit phải đối chiếu `.asmdef` thật để phát hiện
dependency vòng hoặc module presentation truy cập protocol/domain sai boundary.

## 5. .NET Game Server

```text
KnightServer/
├── Accounts/
├── Characters/
├── Combat/
├── Configuration/
├── Database/
├── Migrations/
├── Monsters/
├── Networking/
├── Persistence/
├── Players/
├── Progression/
├── Time/
├── Tutorials/
├── World/
├── Program.cs
├── serverSettings.json
└── KnightServer.csproj
```

### 5.1 Accounts

Authentication, password hashing, guest/account conversion, refresh token,
registration transaction, rate limit và active-account lease. Đây là module
Critical.

### 5.2 Characters

Character creation catalog và character-name policy. Character persistence hiện
được thực hiện qua repository trong `Persistence`.

### 5.3 Combat

Damage calculator, combat stats, `MonsterCombatService` và các result/status.
Server kiểm tra map, range, cooldown, target life và quyết định damage.

### 5.4 Configuration

`ServerOptions.cs` load và validate authentication, capacity, character,
progression, world, map, NPC, portal, tutorial, item và monster configuration.

Khi tăng quy mô, có thể tách option theo module nhưng phải giữ validation tập
trung tại startup và không hardcode dữ liệu vận hành vào handler/UI.

### 5.5 Database và Migrations

- `Database/`: bootstrap SQL và công cụ tạo role/database local.
- `Migrations/`: migration EF Core được version hóa.

Không sửa Production schema bằng tay. Entity, mapping, migration và model
snapshot phải thay đổi đồng bộ.

### 5.6 Monsters

Monster entity/domain state, definition, snapshot, service và respawn lifecycle.

### 5.7 Networking

```text
Networking/
├── Handlers/                     # Packet boundary theo hành động
├── ClientConnection.cs          # Một TCP connection
├── ConnectionRegistry.cs        # Connection/capacity/broadcast
├── GameplaySessionPacketMapper.cs
├── IPacketHandler.cs
└── PacketDispatcher.cs           # Access + lease gate trước handler
```

Luồng chuẩn:

```text
Packet
  -> parse
  -> access/session lease
  -> validation
  -> application/domain service
  -> repository/transaction
  -> response/snapshot
```

### 5.8 Persistence

Chứa:

- `KnightDbContext`;
- database configuration/factory;
- repository;
- EF entity;
- Development-only data reset.

Các thay đổi account, character, progression, tutorial, inventory reward,
audit và outbox được lưu tại đây. Module khác không được truy cập SQL chi tiết
của nhau.

### 5.9 Players

`PlayerSession`, player state/snapshot và active-player ownership. Đây là trạng
thái authoritative của character đang online.

### 5.10 Progression

EXP curve, stat pipeline và service cấp progression có idempotency/persistence.

### 5.11 Tutorials

```text
Tutorials/
├── StarterTutorialService.cs
└── StarterTutorialStateMachine.cs
```

Chứa luồng Mẹ -> 20 Wolf -> reward -> level 2. Packet handler chỉ validate/map
response; state transition và transaction thuộc service/state machine.

### 5.12 World

Map catalog, authoritative map bounds, movement resolver, monster collision và
respawn displacement.

## 6. Test projects

### 6.1 Unit test

```text
KnightServer.Tests/
├── Accounts/
├── Characters/
├── Combat/
├── Networking/
├── Players/
├── Progression/
├── Tutorials/
└── World/
```

Unit test kiểm tra domain/application rule mà không cần PostgreSQL thật.

### 6.2 Integration test

```text
KnightServer.IntegrationTests/
├── Accounts/
└── Characters/
```

Integration test dùng PostgreSQL và/hoặc network boundary thật. Khi Inventory,
Tutorial và World tiếp tục tăng, nên tách test folder đúng domain thay vì dồn
vào `Characters`.

## 7. Documentation

```text
Documentation/
├── Architecture/ # Kiến trúc/runtime hiện hành
├── Design/       # Luồng người chơi và game design
├── Production/   # Điều kiện/cutover server thật
├── Audits/       # Snapshot kiểm tra theo ngày
├── DevLogs/      # Bàn giao theo phiên
├── Archive/      # Tài liệu không còn hiện hành
├── KNIGHT_PROJECT_CORE_RULES.md
└── README.md
```

`Documentation/README.md` là cổng đọc tài liệu. Tài liệu trong `Audits` và
`DevLogs` là lịch sử, không tự động trở thành nguồn sự thật hiện tại.

## 8. Boundary dự kiến cho Inventory

Inventory chưa có module hoàn chỉnh. Khi triển khai, ưu tiên cấu trúc:

```text
KnightClient/Assets/_Project/Scripts/
├── Data/Models/Inventory...
├── Data/Events/Inventory...
├── Gameplay/Inventory/
├── Network/Handlers/Inventory/
└── UI/Inventory/

KnightServer/
├── Inventory/
│   ├── InventoryService
│   ├── InventoryPolicy
│   ├── InventoryResult
│   └── InventoryReadModel
├── Networking/Handlers/Inventory...
├── Persistence/Entities/CharacterInventoryItemEntity
└── Migrations/
```

Inventory không được phụ thuộc cứng vào nhiệm vụ Mẹ. Cùng một boundary phải mở
rộng được cho quest reward, monster drop, shop, equipment, trade và Admin API.

## 9. Điểm cần rà soát định kỳ

1. Thư mục trống/legacy và class global cũ còn reference hay không.
2. `.asmdef` có dependency vòng hoặc presentation phụ thuộc protocol sai không.
3. File handler/config quá lớn có cần tách theo domain không.
4. Unity asset mới có `.meta` và scene/prefab reference hợp lệ không.
5. Server module mới có validation, test, metric/log và Admin Management
   Contract phù hợp chưa.
6. Database change có migration, integration test và forward-fix chưa.
7. Cây thư mục trong tài liệu này còn khớp repository thực tế không.
