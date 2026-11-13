# Cấu trúc repository KnightOnline

**Đối chiếu lần cuối:** 13-11-2026, commit `c860744`.
**Phạm vi:** source Unity client, server .NET, test và tài liệu.

> Đây là tài liệu kiến trúc hiện hành. Khi thêm, xóa hoặc di chuyển module, phải
> cập nhật file này trong cùng thay đổi. Vai trò và dependency của module phải
> tuân thủ [Quy tắc cốt lõi](../KNIGHT_PROJECT_CORE_RULES.md).

## 1. Cấp repository

```text
KnightOnline/
├── .github/workflows/             # CI server (build + unit test)
├── Documentation/                 # Kiến trúc, thiết kế, audit, DevLog
├── KnightClient/                  # Unity client (Unity 6000.5.4f1)
├── KnightServer/                  # Game server .NET 10
├── KnightServer.Tests/            # Unit test server (+3 test PostgreSQL tự bỏ qua nếu thiếu DB)
├── KnightServer.IntegrationTests/ # Test tích hợp PostgreSQL/mạng
├── .gitattributes / .gitignore
├── AGENTS.md                      # Chỉ dẫn làm việc trong repository
├── dotnet-tools.json              # Ghim công cụ dotnet-ef
└── README.md                      # Điểm vào repository
```

Không phải module sản phẩm, không commit nội dung: `Builds/`, `obj/` ở thư mục
gốc, `.tmp/`, `.agents/`, `.qodo/`, các file `KnightServer/server*.log`.

## 2. Unity client

```text
KnightClient/
├── Assets/
│   ├── _Project/          # Asset và source riêng của KnightOnline
│   ├── TextMesh Pro/      # Tài nguyên TextMesh Pro
│   ├── Settings/          # Render pipeline / build profile của Unity
│   ├── Scenes/SampleScene.unity, TutorialInfo/, Readme.asset
│   │                      # Phần còn lại của template Unity, không dùng (xem §9)
│   ├── InputSystem_Actions.inputactions
│   └── NoFriction.physicsMaterial2D
├── Packages/              # manifest.json + packages-lock.json (UniTask, VContainer ghim hash)
├── ProjectSettings/
└── UserSettings/          # Cấu hình máy cá nhân, không commit
```

### 2.1 Asset riêng của dự án (`Assets/_Project`)

```text
_Project/
├── Animations/Player/   # PlayerAnimatorController, Player_Idle, Player_Walk
├── Art/                 # Sprite nhân vật, khung/nút UI (giấy phép CC0 trong Art/UI/License)
├── Audio/               # Chỉ có asmdef, chưa có âm thanh
├── Editor/              # InGamePlayerHudBuilder (chỉ chạy trong Editor)
├── Prefabs/             # Player, Monster, OptionButton
├── Resources/UI/        # VillageHud.uxml/.uss, VillageDefaultTheme.tss, KnightUiTheme.asset
├── Scenes/              # App, Bootstrap, InGame
├── Scripts/             # Source C# của client (§3)
└── Documentation/, Materials/, ScriptableObjects/, Settings/
                         # Thư mục rỗng, chỉ còn .meta (xem §9)
```

### 2.2 Scene và luồng chạy

```text
App (composition root sống xuyên scene)
  -> nạp Bootstrap (Additive)
  -> màn vào game / xác thực / chọn-tạo nhân vật
  -> nạp InGame (Single) sau khi EnterWorld thành công
```

Build Settings chỉ chứa ba scene trên. `Login.unity` đã bị loại bỏ; không tạo lại
scene đăng nhập độc lập nếu chưa có quyết định kiến trúc mới.

## 3. Source client (`Assets/_Project/Scripts`)

| Thư mục | Nội dung | Ghi chú |
|---|---|---|
| `Core/` | `Events/` (EventBus, IEventBus, EventBinding, IGameEvent, IStickyGameEvent) và `GameManager`, `ServiceLocator`, `Singleton`, `SceneLoader` | Bốn lớp sau **không còn được tham chiếu** (đã kiểm tra 13-11-2026) — ứng viên xóa. Không thêm logic vào chúng. |
| `Data/` | `Events/` (sự kiện xác thực, nhân vật, quái, kết nối, nhiệm vụ/thế giới) và `Models/` (CharacterData, MonsterData, cấu hình client, StoredAccountSession) | Network handler ánh xạ packet sang model/event tại đây. |
| `Gameplay/` | `Camera/`, `Monster/`, `NPC/`, `Player/`, `Services/` (luồng xác thực, chọn/tạo nhân vật, kho phiên Development), `Targeting/`, `World/` | Chỉ dự đoán/hiển thị. Có hai script camera trùng vai trò: `Camera/CameraFollow` (gắn trong scene) và `World/CameraFollow2D` (tạo từ `InGameSceneRoot`). |
| `Input/` | `IMovementInputProvider`, `KeyboardMovementInput` | Joystick/mobile phải triển khai cùng contract. |
| `Network/` | `NetworkClient`, `NetworkSettings`, `Handlers/ClientPacketHandlers.cs` (815 dòng, gom mọi domain) | Mở/đóng TCP, đóng/mở gói `PacketEnvelope`, giới hạn kích thước gói; không chứa luật nghiệp vụ. |
| `Root/` | `Bootstrap/` (AppLifetimeScope, GameLifetimeScope, CharacterFlowController, GameBootstrap, GameSession) và `InGame/` (InGameLifetimeScope, InGameSceneRoot, AuthoritativeNpcRequestPresenter) | Chỉ ghép module và điều phối vòng đời. |
| `Shared/Packets/` | Contract packet dùng chung client/server | Server biên dịch trực tiếp các file này (`KnightServer.csproj`). |
| `UI/` | Panel/presenter xác thực, chọn/tạo nhân vật, `InGameHUD` (uGUI), `VillageUIToolkitHud` (UI Toolkit), `InGameMenuView`, `NpcDialogUI`, `TargetHUD`, `KnightUiTheme` | Chỉ hiển thị và phát ý định. |
| `Combat/`, `Inventory/`, `Managers/`, `NPC/`, `Player/` | Thư mục rỗng (chỉ còn `.meta`) | Ứng viên xóa; Inventory sẽ thiết kế lại theo §8. |

## 4. Assembly Unity và hướng phụ thuộc

Tham chiếu thực tế trong `.asmdef` (13-11-2026):

| Assembly | Tham chiếu |
|---|---|
| `Shared` | (không) |
| `Core` | VContainer, UniTask |
| `Data` | Core, Shared |
| `Input` | Core, InputSystem |
| `Network` | Core, Shared, Data, VContainer, UniTask |
| `Gameplay` | Core, Data, Network, Input, Audio, VContainer, UniTask, InputSystem, TextMeshPro |
| `UI` | Core, Data, Gameplay, Shared, VContainer, UniTask, InputSystem, TextMeshPro |
| `Root` | tất cả assembly dự án + VContainer, UniTask |
| `Editor` | UI, TextMeshPro, UnityEngine.UI |

```text
Shared ← Core ← Data ← Network ← Gameplay ← UI ← Root
                  ↖ Input ↗
```

Không có vòng phụ thuộc. Code trong `Data` và `UI` hiện **không dùng**
namespace `KnightOnline.Client.Shared.Packets`; tham chiếu `Shared` của hai
assembly này có thể gỡ để khóa boundary protocol ở tầng Network.

## 5. Server .NET (`KnightServer/`)

| Thư mục | Trách nhiệm | Mức rủi ro |
|---|---|---|
| `Accounts/` | Xác thực, băm mật khẩu (PBKDF2), khách/chuyển đổi tài khoản, refresh token, receipt idempotency, giới hạn tần suất, lease tài khoản trong bộ nhớ, luồng đăng ký | Critical |
| `Characters/` | Catalog tạo nhân vật và chính sách đặt tên | Authoritative |
| `Combat/` | Chỉ số chiến đấu, tính sát thương, `MonsterCombatService` (kiểm tra map, tầm, cooldown, mục tiêu còn sống) | Authoritative |
| `Configuration/` | `ServerOptions.cs` (781 dòng): nạp và kiểm tra toàn bộ cấu hình; chặn cờ Development khi không ở môi trường Development | — |
| `Database/` | `bootstrap.sql` tạo role/database local | — |
| `Hosting/` | `SingleInstanceGuard`: mutex hệ điều hành chặn tiến trình server thứ hai | — |
| `Migrations/` | Migration EF Core có version | Critical |
| `Monsters/` | Trạng thái quái, định nghĩa, snapshot, hồi sinh | Authoritative |
| `Networking/` | `ClientConnection`, `ConnectionRegistry` (sức chứa, broadcast), `PacketDispatcher` (cổng quyền + lease trước handler), `Handlers/` theo hành động | Critical |
| `Persistence/` | `KnightDbContext`, cấu hình/factory, `CharacterRepository`, entity EF, reset dữ liệu Development | Critical |
| `Players/` | `PlayerSession` (vị trí, chỉ số, tiến trình của nhân vật đang online), `ActivePlayerRegistry`, pipeline chỉ số | Authoritative |
| `Progression/` | Đường cong EXP và cấp EXP có idempotency | Authoritative |
| `Time/` | `IServerClock` | — |
| `Tutorials/` | `StarterTutorialService` + máy trạng thái: Mẹ → 20 Wolf → thưởng → cổng | Authoritative |
| `World/` | Catalog map, biên map, va chạm khi di chuyển, đẩy người chơi khi quái hồi sinh | Authoritative |
| `Program.cs` | Composition root (665 dòng) + ba vòng nền: hồi sinh quái, lưu snapshot 5 giây, dọn receipt 15 phút | — |
| `serverSettings.json` | Cấu hình Development (bind `127.0.0.1:7777`) | — |

Luồng chuẩn của một packet:

```text
Đọc khung (4 byte độ dài + JSON) → PacketDispatcher
  → kiểm tra mức truy cập (Anonymous / Authenticated / CharacterSelected)
  → gia hạn lease tài khoản (mọi packet đã xác thực)
  → handler: parse + validate → service/domain → repository/transaction
  → phản hồi / snapshot / broadcast
```

## 6. Test

```text
KnightServer.Tests/            # 86 test: Accounts, Characters, Combat, Hosting,
                               # Networking, Persistence*, Players, Progression,
                               # Tutorials, World   (*cần KNIGHTONLINE_TEST_POSTGRES_CONNECTION)
KnightServer.IntegrationTests/ # Accounts, Characters — ĐANG KHÔNG BIÊN DỊCH (13-11-2026)
```

`KnightServer.IntegrationTests` dùng API cũ (constructor `StarterTutorialService`
5 tham số, `PersistPortalTransitionAsync` đã bị xóa…). CI hiện chỉ build/chạy
`KnightServer.Tests`. Xem [đánh giá 13-11-2026](../Audits/2026-11-13-danh-gia-luong-va-huong-di.md).

## 7. Tài liệu

```text
Documentation/
├── README.md                     # Cổng tài liệu
├── Trang_Thai_Du_An.md           # Tình trạng và việc nên làm tiếp
├── KNIGHT_PROJECT_CORE_RULES.md  # Quy tắc bắt buộc
├── Architecture/                 # Kiến trúc/runtime hiện hành
├── Design/                       # Thiết kế game, luồng người chơi, hướng dẫn UI
├── Production/                   # Điều kiện/kế hoạch lên server thật
├── Audits/                       # Ảnh chụp đánh giá theo ngày
├── DevLogs/                      # Tổng kết theo phiên
└── Archive/                      # Lịch sử, không còn hiện hành
```

## 8. Boundary dự kiến cho Inventory

Server đã có `CharacterInventoryItemEntity` và packet `InventorySnapshot` (gửi
khi vào thế giới và khi nhận thưởng nhiệm vụ), nhưng chưa có module Inventory
riêng. Khi triển khai, ưu tiên:

```text
KnightServer/Inventory/        InventoryService, InventoryPolicy, InventoryResult, InventoryReadModel
KnightServer/Networking/Handlers/Inventory…
KnightClient/.../Data/Models/Inventory…, Data/Events/Inventory…
KnightClient/.../Network/Handlers/Inventory/
KnightClient/.../UI/Inventory/
```

Inventory không được phụ thuộc cứng vào nhiệm vụ Mẹ; cùng một boundary phải mở
rộng được cho thưởng nhiệm vụ, rơi đồ, cửa hàng, trang bị, giao dịch và Admin API.

## 9. Việc dọn dẹp còn mở

Cần làm trong Unity Editor (để Unity xử lý `.meta` đúng cách, theo CORE-22):

1. Xóa template Unity không dùng: `Assets/Scenes/SampleScene.unity`,
   `Assets/TutorialInfo/`, `Assets/Readme.asset`.
2. Xóa các thư mục rỗng ở §2.1 và §3.
3. Xóa `Core/GameManager`, `ServiceLocator`, `Singleton`, `SceneLoader` sau khi
   Unity compile xác nhận không còn tham chiếu.
4. Chọn một trong hai script camera follow.
5. Đổi tên `Player_Idle.anim.anim`, `Player_Walk.anim.anim` (đuôi lặp) và sprite
   nhân vật đang mang tên UUID trong `Art/Characters/`.

## 10. Điểm rà soát định kỳ

1. Thư mục rỗng/legacy và lớp global cũ còn tham chiếu hay không.
2. `.asmdef` có vòng phụ thuộc hoặc tầng trình bày phụ thuộc protocol không.
3. File handler/cấu hình quá lớn có cần tách theo domain không.
4. Asset mới có `.meta`; scene/prefab reference hợp lệ.
5. Module server mới có validation, test, log/metric và Admin Management
   Contract phù hợp.
6. Thay đổi database có migration, integration test và phương án forward-fix.
7. Cây thư mục trong tài liệu này còn khớp repository không.
