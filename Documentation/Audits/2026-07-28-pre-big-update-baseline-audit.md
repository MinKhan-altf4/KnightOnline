# Pre-Big-Update Baseline Audit — 2026-07-28

## 1. Mục tiêu

Đối chiếu tài liệu hiện hành với code Client, Server, database và Unity scenes để
xác định dự án đã đủ ổn định làm baseline cho Big Update hay chưa.

Phạm vi kiểm tra:

- repository và trạng thái Git;
- composition root, assembly và scene flow;
- Authentication, Registration và Active Account;
- Character Select, Character Creation và vào InGame;
- movement, combat và monster synchronization đang liên quan tới luồng InGame;
- Unity scene/prefab references;
- build/test và technical debt có thể quan sát bằng kiểm tra tĩnh.

Audit này là snapshot tại ngày ghi trên tiêu đề, không thay thế tài liệu thiết
kế hiện hành.

## 2. Kết luận

**Chưa đủ điều kiện khóa baseline cho Big Update.**

Nền tảng local vertical slice đã hoạt động và có nhiều boundary đúng hướng,
nhưng còn một blocker authoritative gameplay, một số contract chưa sẵn sàng cho
client/server versioning và một khối thay đổi chưa được chốt trong Git.

Không cần viết lại toàn bộ dự án. Nên thực hiện một stabilization pass có thứ tự,
test lại từng luồng rồi mới tạo baseline commit/tag.

## 3. Trạng thái tổng quan

| Phạm vi | Đánh giá | Ghi chú |
|---|---|---|
| Core Rules và tài liệu nguồn | Đạt một phần | Đã có nguồn sự thật; thiếu Architecture Overview và Project Status hiện hành |
| Git baseline | Chưa đạt | Worktree đang chứa nhiều thay đổi code/UI/tài liệu chưa commit |
| App → Bootstrap → InGame | Đạt cho local | Build Settings đúng; phải chạy từ `App` |
| Authentication local | Đạt một phần | Luồng chính có boundary; còn adapter Development và thiếu duplicate-request protection |
| Registration local | Đạt cho scaffold | PKCE/transaction boundary có; completion hiện chỉ dành Development |
| Active Account local | Đạt cho một process | Character Select tính Active; chưa có TTL/heartbeat/distributed lease |
| Character Select/Create | Đạt một phần | Ownership, catalog, DB constraint và RequestId đã có; invariant 3 slot bị lặp |
| Unity serialized references | Đạt tại kiểm tra tĩnh | Reference chính đã nối; không thấy project script bị missing |
| Movement authoritative | Không đạt | Server không có collision/map validation hoặc correction snapshot |
| Combat monster local | Đạt một phần | Server quyết định range/cooldown/damage; phụ thuộc vị trí movement có thể sai lệch |
| Production readiness | Không đạt | Server chủ động chặn môi trường ngoài Development |

## 4. Kiến trúc đang chạy

```text
App scene
└── AppLifetimeScope (DontDestroyOnLoad)
    ├── EventBus
    ├── NetworkClient
    ├── packet response handlers
    ├── GameSession
    └── load Bootstrap additive

Bootstrap scene
└── GameLifetimeScope (child của AppLifetimeScope)
    ├── AuthenticationFlowService
    ├── CharacterService
    ├── CharacterSelectionService
    ├── CharacterFlowController
    └── Entry/Loading/Registration/Character UI

Select Character thành công
└── load InGame bằng Single
    └── InGameLifetimeScope (child của AppLifetimeScope)
        ├── Player
        ├── Monster/Target
        ├── HUD/NPC
        └── CharacterData lấy từ GameSession
```

Điểm đúng:

- `App` đứng đầu Build Settings.
- `Login` đã bị tắt; Authentication UI hiện thuộc `Bootstrap`.
- Server packet dispatcher mặc định yêu cầu authenticated; anonymous packet
  phải khai báo rõ.
- UI Authentication/Character không deserialize packet trực tiếp.

Điểm cần củng cố:

- `GameSession` vừa được DI vừa dùng static `Current`, tạo hai đường truy cập
  state.
- Gameplay services phụ thuộc trực tiếp `NetworkClient`; nên có command gateway
  hoặc interface boundary trước khi module tăng mạnh.
- Bốn file Core cũ (`GameManager.cs`, `SceneLoader.cs`, `ServiceLocator.cs`,
  `Singleton.cs`) đang rỗng nhưng vẫn tồn tại cùng `.meta`.

## 5. Authentication và Registration

### Đã đúng hướng

- Có guest account riêng và chuyển đổi giữ nguyên `AccountKey`.
- Username được tách thành display identity, không thay khóa ownership.
- Password được hash phía server.
- Refresh token có expiry, rotation, family và reuse detection.
- Token gắn với device hash.
- Entry không tự resume; chỉ gửi request khi người chơi chọn Chơi tiếp.
- Device chỉ giữ session gần nhất và không lưu password.
- Login/Create Guest/Resume có rate limit local.
- Active account từ chối connection đến sau, không đá owner hiện tại.
- Registration có RequestId, expiry, authorization code một lần và PKCE.
- Server chặn môi trường Staging/Production khi TLS và secure adapter chưa có.

### Sai lệch và rủi ro

1. `InitialSessionCheckSeconds` và `SessionConflictRetrySeconds` tồn tại trong
   Client settings/Inspector nhưng không được Authentication flow sử dụng.
   Tài liệu hoặc UI không được giả định đang có nhịp 5/10 giây.
2. `Guest.MaximumLevel` và `Guest.DisabledFeatures` chỉ được load/validate, chưa
   được gameplay handler thực thi.
3. Active lease là in-memory, không có TTL, heartbeat, grace period hoặc shared
   storage. Chỉ phù hợp một server process local.
4. Resume token không có RequestId/idempotency record. Hai request dùng cùng
   refresh token có thể làm request sau bị xem là token reuse và thu hồi family.
5. `ClientVersion` được gửi trong Connect request nhưng server không kiểm tra.
   Chưa có protocol compatibility gate cho Big Update.
6. Game transport là raw TCP chưa TLS.
7. PlayerPrefs session store chỉ được phép Editor/Development Build; chưa có
   secure platform adapter.
8. Registration transaction store và portal hiện là in-memory/Development.

## 6. Character Flow

### Đã đúng hướng

- Character List và Character Creation Catalog là hai contract độc lập.
- Catalog server định nghĩa class, body type và appearance option.
- Tạo nhân vật có RequestId và lưu success result để xử lý duplicate.
- Database bảo vệ unique character name và unique account/server/slot.
- Server kiểm tra account ownership khi chọn nhân vật.
- Character Select được tính Active ngay sau Authentication thành công.
- Tạo nhân vật thành công được chọn và đi vào InGame theo luồng hiện tại.
- Spawn map, spawn point và tutorial definition được lưu cùng character.

### Sai lệch và rủi ro

1. Invariant ba slot bị định nghĩa lại tại:
   - `ServerOptions`;
   - `CharacterRepository`;
   - database check constraint;
   - `CharacterSelectView`;
   - `CharacterCreationView`.

   Ba slot là quy tắc sản phẩm hợp lệ, nhưng cần một contract/catalog value cho
   Client và một domain constant/policy duy nhất phía Server; database constraint
   phải được quản lý bằng migration tương ứng.

2. Character HP/move speed khi select đang lấy từ config toàn cục, chưa phải
   progression/stat snapshot của character.
3. Asset address cho class/appearance mới chỉ là dữ liệu; Client chưa lắp ráp
   sprite/model thật.
4. Class promotion và inventory-owned cosmetic chưa được triển khai; catalog
   hiện mới là starter creation catalog.
5. Chưa có PostgreSQL integration test cho ownership, unique race và
   idempotency transaction.

## 7. Unity scenes và prefabs

### Kết quả kiểm tra

- Build order:
  - `App`: enabled;
  - `Bootstrap`: enabled;
  - `Login`: disabled;
  - `InGame`: enabled.
- `App.unity` serialize Authentication bypass bằng `false`, khớp server local.
- `GameLifetimeScope` có đủ reference tới Creation, Select, Registration,
  Authentication Entry, Loading và Connection Status.
- Registration confirm-password và Show Registration button đã được nối.
- MonsterSpawner có `_defaultPrefab`.
- Monster prefab có `MonsterView` và các reference UI; world-space legacy UI
  bị code tắt khi Awake.
- Không phát hiện `m_Script: {fileID: 0}` trong project scenes/prefabs.
- Hai NPC không gán marker anchor, nhưng code chủ động fallback về transform;
  đây không phải missing-reference crash.
- Recent Unity Editor log không có CS error, NullReference, VContainer exception
  hoặc missing-reference exception theo pattern đã kiểm tra.

### Technical debt

- `CharacterSelectView` tắt LayoutGroup cũ và tự đặt slot runtime để tương thích
  scene cũ.
- `CharacterCreationView` có thể tự sinh appearance root/dropdown runtime.
- Hai cơ chế trên giúp scaffold chạy nhưng khiến scene khó dự đoán và khó giao
  cho UI designer. Trước Big Update UI nên có prefab/layout contract rõ.
- `Login.unity` và `SampleScene.unity` là scene dư cần quyết định archive hoặc
  loại khỏi project sau khi xác nhận không còn tham chiếu.
- Không nên chạy test từ `Bootstrap` trực tiếp vì thiếu parent
  `AppLifetimeScope`; workflow chuẩn là chạy `App`.

## 8. Blocker authoritative movement

Client hiện:

- đọc input;
- tự đặt `Rigidbody2D.linearVelocity`;
- dùng Unity collision để chặn tường/NPC/quái;
- gửi hướng di chuyển định kỳ.

Server hiện:

- nhận hướng;
- cộng `direction × moveSpeed × elapsed`;
- không có collision world, nav/map bounds hoặc occupied-body validation;
- không broadcast authoritative position snapshot/correction;
- không lưu vị trí mới về database.

Hậu quả:

- client có thể bị tường chặn nhưng server vẫn đi xuyên tường;
- vị trí dùng kiểm tra attack range khác vị trí người chơi nhìn thấy;
- reconnect quay về vị trí cũ trong database;
- chưa thể mở multiplayer movement hoặc anti-cheat đáng tin cậy.

Đây là blocker cao nhất cần xử lý trước khi mở rộng farm/combat/world.

## 9. Test và build

Đã chạy:

```text
dotnet test KnightServer.Tests/KnightServer.Tests.csproj
  --no-restore --configuration Release
```

Kết quả:

```text
28 passed, 0 failed, 0 skipped
```

Giới hạn coverage hiện tại:

- test tập trung vào input policy, token/password helper, rate limit, local
  lease, registration flow, character name và catalog;
- chưa có Authentication service + PostgreSQL integration test;
- chưa có CharacterRepository transaction/race test;
- chưa có packet-handler authorization/integration test;
- chưa có movement/combat test;
- chưa có Unity EditMode/PlayMode automated test.

Unity-generated `.csproj` không build độc lập bằng `dotnet build` vì
`Temp/obj/.../project.assets.json` do Unity quản lý không tồn tại ở thời điểm
kiểm tra. Đây không phải diagnostic C#; compile phải xác nhận trong Unity.

## 10. Thứ tự stabilization đề xuất

### Gate 0 — Khóa baseline làm việc

1. Review toàn bộ diff hiện tại.
2. Test manual ba luồng Authentication/Character.
3. Commit riêng tài liệu, code/server và Unity assets khi trạng thái đạt.
4. Tạo tag baseline trước Big Update.

### Gate 1 — Contract và session

1. Thêm protocol/client compatibility policy.
2. Chốt duplicate Resume/Login semantics và RequestId.
3. Tách Development adapter khỏi Production composition rõ hơn.
4. Thêm integration test Authentication/Character với PostgreSQL.

### Gate 2 — Character/UI

1. Đưa slot count qua catalog/contract và policy duy nhất.
2. Chuẩn hóa Character Select/Create prefab layout, bỏ runtime compatibility
   hack khi scene mới đã ổn định.
3. Chốt Character runtime snapshot gồm stats/map/spawn cần thiết.

### Gate 3 — Authoritative world

1. Thiết kế server collision/map boundary.
2. Server simulation tick và authoritative position snapshot.
3. Client prediction/interpolation/correction.
4. Persist/checkpoint position theo policy.
5. Test wall, NPC/player blocking, teleport, reconnect và attack range.

### Gate 4 — Baseline approval

Chỉ mở Big Update khi:

- toàn bộ blocker Critical/High đã đóng hoặc được chủ dự án chấp nhận rõ;
- Server tests và Unity smoke tests pass;
- hai client test pass;
- Architecture Overview và Project Status phản ánh code thực;
- worktree sạch và baseline commit/tag đã được tạo.

