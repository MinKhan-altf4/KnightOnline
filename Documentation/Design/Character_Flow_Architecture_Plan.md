# LUỒNG NHÂN VẬT — KẾ HOẠCH KIẾN TRÚC VÀ TRIỂN KHAI

Trạng thái: **Tài liệu thiết kế; không dùng nhãn phase/checklist bên dưới làm tiến độ hiện hành**
Phạm vi: màn vào game → xác thực/đăng ký → chọn nhân vật → tạo nhân vật →
bản đồ tân thủ → nhiệm vụ hướng dẫn → thăng cấp nghề (Class Promotion)

Tiến độ mới hơn nằm trong `Documentation/Trang_Thai_Du_An.md` và DevLog theo
module. Một số mục trong phần "Bước nền tảng" phản ánh checkpoint cũ; hãy
đối chiếu code, cấu hình và test trước khi mở lại làm việc.

## 1. Mục tiêu

Xây dựng Character Flow có thể mở rộng mà không phải sửa lại toàn bộ client,
packet hoặc database khi:

- thêm/bớt class;
- thêm nhánh nâng cấp chức nghiệp;
- thêm giới tính/body type;
- thêm tóc, khuôn mặt, biểu cảm, trang phục và cosmetic bán trong shop;
- thêm server hoặc thay map khởi đầu;
- thay đổi tutorial, level requirement và reward;
- bổ sung Web Admin quản lý catalog;
- triển khai nhiều game server và các lớp chống gian lận.

Server là nguồn dữ liệu chính thức. Unity chỉ hiển thị catalog và gửi lựa chọn
của người chơi.

---

## 2. Luồng tổng thể

```text
Mở ứng dụng
  → Entry
      ├── Chơi mới / Chơi tiếp
      ├── Có tài khoản / Đổi tài khoản
      ├── Đăng ký tài khoản
      └── Chọn server
  → Authentication thành công
  → Claim Active Account Lease
  → Character Select
      ├── Slot 1
      ├── Slot 2
      └── Slot 3
          ├── Có nhân vật → Select → Vào game
          └── Trống → Character Creation
  → Chọn class
  → Chọn body type
  → Chọn appearance
  → Đặt tên
  → Server tạo nhân vật nguyên tử
  → Tự động select
  → Spawn tại map tân thủ
  → Tutorial bắt buộc tới khoảng level 2
  → Hỏi có tiếp tục hướng dẫn hay không
      ├── Có → tutorial mở rộng tới khoảng level 10
      └── Không → kết thúc tutorial mở rộng
```

---

## 3. Entry và Registration

Registration không nằm trong Character Select.

```text
AuthenticationEntryPanel
├── EntryContent
│   ├── Chơi mới / Chơi tiếp
│   ├── Có tài khoản / Đổi tài khoản
│   └── Chọn server
├── LoginContent
│   ├── Username
│   ├── Password
│   ├── Đăng nhập
│   ├── Đăng ký tài khoản
│   └── Back
└── RegistrationContent
    ├── Username
    ├── Email
    ├── Password
    ├── Confirm Password
    ├── Đăng ký
    └── Back
```

Development dùng form Unity để mô phỏng. Production mở Web Account Service:

```text
Begin Registration
→ PKCE/state/transaction
→ website HTTPS
→ email verification
→ verified callback
→ guest conversion hoặc tạo account mới
→ refresh-token family mới
→ quay về Entry
```

Nếu thiết bị có guest hợp lệ, đăng ký sẽ chuyển guest thành registered account và
giữ nguyên character. Nếu không có guest, tạo account mới với ba slot trống.

---

## 4. Character Select — ba slot cố định

UI luôn hiển thị đúng ba slot, không render số button bằng số character server
trả về.

```text
CharacterSelectPanel
├── CharacterSlot1
├── CharacterSlot2
├── CharacterSlot3
└── BackButton
```

### Slot có nhân vật

- preview được lắp từ appearance loadout;
- tên nhân vật;
- level;
- class hiện tại;
- nút Chọn.

### Slot trống

- dấu cộng hoặc silhouette;
- chữ `Tạo nhân vật`;
- nút mở Character Creation.

### Quy tắc

- `SlotIndex` hợp lệ từ 1 đến 3.
- Unique constraint: một account chỉ có một character tại một slot trên một
  server.
- Server trả slot index; client không tự suy ra theo thứ tự danh sách.
- Character Select được tính là Active.
- Back giải phóng đúng Active Account Lease.
- Character Select không tự disconnect theo thời gian. Active Account Lease
  chỉ được giải phóng khi Back, disconnect thật hoặc lease/heartbeat hết hạn.

---

## 5. Định nghĩa nghề (Class Definition)

Không dùng enum đóng hoặc hardcode điều kiện theo class trong UI/handler.

Bốn class ban đầu là dữ liệu:

| Mã định nghĩa (`DefinitionId`) | Tên hiển thị |
|---|---|
| `warrior` | Chiến binh |
| `assassin` | Sát thủ |
| `mage` | Pháp sư |
| `archer` | Xạ thủ |

Class definition dự kiến:

```json
{
  "definitionId": "warrior",
  "version": 1,
  "displayName": "Chiến binh",
  "description": "...",
  "isStarterClass": true,
  "isEnabled": true,
  "allowedBodyTypeIds": ["male", "female"],
  "baseStatsDefinitionId": "warrior_tier_1",
  "startingSkillSetId": "warrior_starter",
  "previewAssetAddress": "class/warrior/preview"
}
```

Client yêu cầu Creation Catalog và tự render danh sách. Thêm class mới không
được yêu cầu phát hành packet version mới nếu schema catalog vẫn tương thích.

---

## 6. Thăng cấp nghề (Class Promotion)

Promotion là đồ thị definition, không phải chuỗi `if/else`.

Ví dụ:

```text
warrior
└── berserker
```

```json
{
  "promotionDefinitionId": "warrior_to_berserker",
  "sourceClassDefinitionId": "warrior",
  "targetClassDefinitionId": "berserker",
  "requirements": {
    "minimumLevel": 150,
    "questDefinitionId": "berserker_awakening",
    "requiredItemDefinitionIds": []
  },
  "isEnabled": true,
  "version": 1
}
```

Mốc 150 là dữ liệu vận hành và có thể đổi thành mốc hợp lý khác. Server kiểm tra
level, quest, item, trạng thái và idempotency.

Character lưu class hiện tại; lịch sử chuyển class nằm ở bảng riêng:

```text
character_class_history
- id
- character_id
- source_class_definition_id
- target_class_definition_id
- promotion_definition_id
- promoted_at_utc
- request_id
- correlation_id
```

Thiết kế cho phép nhiều nhánh promotion, awakening, rebirth hoặc class theo
faction về sau.

---

## 7. Body Type và ngoại hình lắp ráp

Không dùng một `AppearanceId` duy nhất. Appearance là tập các slot:

```text
base_body
face
hair
expression
top
bottom
accessory
back
aura
```

Giai đoạn đầu tối thiểu:

```text
base_body
hair
bottom
expression
```

Không dùng `bool IsMale`. Body type dùng definition ID:

```text
male
female
```

Class có thể quy định body type được phép nhưng mặc định bốn starter class hỗ trợ
cả nam và nữ.

### Contract chọn ngoại hình

```json
{
  "slotDefinitionId": "hair",
  "optionDefinitionId": "hair_001"
}
```

Create request gửi danh sách selection, không gửi sprite/prefab path.

### Định nghĩa ngoại hình

```text
appearance_definitions
- definition_id
- slot_definition_id
- display_name
- allowed_body_type_ids
- allowed_class_definition_ids
- asset_address
- is_starter_option
- is_enabled
- version
```

`asset_address` là catalog key do server công bố; server không tin đường dẫn do
client tự gửi.

---

## 8. Cosmetic Ownership và trang bị

Tách ba nguồn dữ liệu:

1. Catalog định nghĩa cosmetic.
2. Account sở hữu cosmetic.
3. Character đang trang bị cosmetic.

### Quyền sở hữu

```text
account_cosmetic_entitlements
- id
- account_id
- appearance_definition_id
- granted_at_utc
- source_type
- source_reference_id
- revoked_at_utc
- request_id
```

Tóc và biểu cảm mua bằng shop mặc định thuộc account. Nếu cần cosmetic chỉ dành
cho một nhân vật, thêm ownership scope bằng policy thay vì tạo bảng tùy tiện.

### Ngoại hình đang trang bị

```text
character_appearances
- character_id
- slot_definition_id
- appearance_definition_id
- updated_at_utc
- version
```

Unique:

```text
character_id + slot_definition_id
```

Khi tạo character, chỉ được chọn:

- option starter miễn phí; hoặc
- option account đã sở hữu.

Server luôn kiểm tra compatibility và ownership.

---

## 9. Trình tạo nhân vật nhiều bước

```text
CharacterCreationPanel
├── StepClass
├── StepBodyType
├── StepAppearance
├── StepName
└── Confirmation
```

### Trình tự

1. Chọn slot trống.
2. Yêu cầu catalog tạo nhân vật (Creation Catalog).
3. Chọn class.
4. Chọn nam/nữ.
5. Chọn thân hình/tóc/quần/biểu cảm.
6. Nhập tên.
7. Xem preview và xác nhận.
8. Gửi Create Character.

### Request dự kiến

```text
RequestId
ServerId
SlotIndex
CharacterName
ClassDefinitionId
BodyTypeDefinitionId
AppearanceSelections[]
CatalogVersion
```

Client không gửi:

- level;
- HP/MP;
- sát thương/phòng thủ;
- skill;
- bản đồ/điểm xuất hiện;
- quyền sở hữu cosmetic.

---

## 10. Kiểm tra tên nhân vật

Có thể có API pre-check để cải thiện trải nghiệm, nhưng nó không bảo đảm tên sẽ
còn trống khi người chơi xác nhận.

Create Character là thao tác quyết định cuối:

1. Chuẩn hóa tên trên server.
2. Kiểm tra policy độ dài/ký tự/từ cấm.
3. Tạo trong database transaction.
4. Unique constraint bảo vệ race condition.
5. Map unique violation thành `NameAlreadyTaken`.

Không dùng kết quả check tên từ client làm authorization.

---

## 11. Transaction tạo nhân vật

Trong một database transaction:

1. Kiểm tra account/session/server.
2. Khóa hoặc bảo vệ slot bằng unique constraint.
3. Kiểm tra số character tối đa.
4. Validate catalog version và definitions.
5. Kiểm tra ngoại hình tương thích và đã sở hữu.
6. Tạo character.
7. Ghi bộ ngoại hình (appearance loadout).
8. Tạo tutorial progress.
9. Gán starter map/spawn point.
10. Ghi domain event/outbox và idempotency result.
11. Commit.

Sau khi commit:

```text
CreateCharacterResponse
→ client tự gửi SelectCharacterRequest
→ server tạo PlayerSession
→ vào Starter Map
```

Disconnect khi kết quả chưa rõ phải cho phép query bằng RequestId; không tuyên bố
“exactly once”.

---

## 12. Database dự kiến

### Bảng `characters`

```text
id
account_id
server_id
slot_index
name
normalized_name
level
current_class_definition_id
body_type_definition_id
current_map_definition_id
current_spawn_point_id
position_x
position_y
created_at_utc
version
```

Constraint:

```text
UNIQUE(server_id, normalized_name)
UNIQUE(account_id, server_id, slot_index)
CHECK(slot_index BETWEEN 1 AND 3)
```

### Bảng bổ sung

- `character_appearances`;
- `account_cosmetic_entitlements`;
- `character_class_history`;
- `character_tutorial_progress`;
- idempotency/outbox tables theo kiến trúc chung.

Migration phải dùng expand/contract và được kiểm thử trên Staging.

---

## 13. Bản đồ tân thủ

Chỉ có một map tân thủ ban đầu:

```text
tutorial_map_01
```

Không hardcode trong Unity:

```json
{
  "startingMapDefinitionId": "tutorial_map_01",
  "startingSpawnPointId": "tutorial_spawn_default"
}
```

Server ánh xạ SpawnPoint ID sang tọa độ thật. Client không quyết định map hoặc
tọa độ spawn.

---

## 14. Máy trạng thái nhiệm vụ hướng dẫn

Không dùng level làm nguồn trạng thái duy nhất.

```text
NotStarted
→ CoreTutorial
→ ContinueOffered
    ├── ExtendedTutorial
    └── Skipped
→ Completed
```

### Luồng

- Tutorial cơ bản bắt buộc trong khoảng hai level đầu.
- Khi hoàn thành core tutorial, server hỏi người chơi có tiếp tục không.
- Chọn Có: tiếp tục tutorial mở rộng tới khoảng level 10.
- Chọn Không: đánh dấu bỏ qua tutorial mở rộng.
- Mốc level và step nằm trong tutorial definition/config.

### Lưu trữ

```text
character_tutorial_progress
- character_id
- tutorial_definition_id
- current_step_definition_id
- state
- continue_choice
- started_at_utc
- updated_at_utc
- completed_at_utc
- version
```

Reward tutorial phải server-authoritative, idempotent và ghi audit/outbox phù
hợp. Mod client không thể nhảy step hoặc nhận reward hai lần.

---

## 15. Packet/API dự kiến

Các contract cần version hóa:

- `ListCharactersRequest/Response` mở rộng slot/class/body/appearance.
- `GetCharacterCreationCatalogRequest/Response` — lấy catalog tạo nhân vật.
- `CheckCharacterNameRequest/Response` — chỉ hỗ trợ UX.
- `CreateCharacterRequest/Response` — tạo nhân vật.
- `SelectCharacterRequest/Response` mở rộng map/spawn.
- `ChooseTutorialContinuationRequest/Response` — chọn tiếp tục/bỏ qua hướng dẫn.
- contract thăng cấp nghề ở giai đoạn sau.

Unknown definition/field phải được xử lý an toàn để hỗ trợ client cũ/server mới
và rolling deployment.

---

## 16. Server quyết định và chống sửa client

Không thể ngăn tuyệt đối việc sửa client. Mục tiêu là client bị sửa không thể
thay đổi trạng thái chính thức.

Server phải kiểm tra:

- account sở hữu character;
- bất biến tài khoản/máy chủ/ô nhân vật;
- class/body/appearance có trong catalog;
- quyền sở hữu cosmetic;
- điều kiện thăng cấp nghề;
- tên là duy nhất;
- bản đồ/điểm xuất hiện tân thủ;
- thứ tự và phần thưởng nhiệm vụ hướng dẫn;
- RequestId, replay, duplicate và rate limit;
- movement, combat, cooldown và trạng thái map.

Client mod có thể đổi hình hiển thị trên máy của họ nhưng không thể chính thức:

- trang bị cosmetic chưa sở hữu;
- đổi class hoặc promotion;
- tạo character thứ tư;
- tăng level/stat;
- teleport khỏi starter map;
- nhận tutorial reward nhiều lần;
- chọn character của account khác.

### Gia cố bản phát hành

- IL2CPP;
- không Development Build;
- tắt Script Debugging và khả năng gắn Profiler;
- TLS cho Production;
- bắt buộc kiểm tra phiên bản protocol/bản build;
- hash/chữ ký cho asset catalog khi phù hợp;
- Android Play Integrity;
- iOS App Attest/DeviceCheck;
- telemetry bảo mật có cấu trúc và điểm rủi ro;
- không ban chỉ dựa vào một tín hiệu client.

Làm rối mã (obfuscation) và kiểm tra toàn vẹn chỉ tăng chi phí tấn công, không
thay thế kiểm tra phía server.

---

## 17. Hợp đồng quản trị (Admin Management Contract)

Admin Web chỉ truy cập qua Admin API.

Dữ liệu đọc (read model) dự kiến:

- catalog nghề/thăng cấp và phiên bản;
- catalog ngoại hình;
- lịch sử cấp quyền sở hữu cosmetic;
- ô, nghề, thân hình, ngoại hình của nhân vật;
- tiến độ nhiệm vụ hướng dẫn;
- số liệu trùng tên/bị từ chối tên;
- yêu cầu tạo nhân vật/thăng cấp đáng ngờ.

Command dự kiến:

- bật/tắt một định nghĩa trong catalog;
- lên lịch phát hành phiên bản catalog;
- cấp/thu hồi quyền sở hữu cosmetic;
- sửa tutorial state trong quy trình hỗ trợ có kiểm soát;
- khóa character/account;
- chạy thử (dry-run) migration và kiểm tra catalog.

Command phải có permission theo action/resource/server, lý do, RequestId, audit
trước/sau và approval khi nguy hiểm. Admin không chỉnh trực tiếp database.

---

## 18. Phân chia module

```text
Shared Contracts
├── Character summaries/slots
├── Creation catalog
├── Create/select requests
└── Tutorial choices

Server Domain/Application
├── CharacterCatalogService
├── CharacterCreationService
├── CharacterSelectionService
├── AppearanceEntitlementPolicy
├── ClassPromotionService
└── TutorialProgressService

Server Infrastructure
├── EF repositories
├── catalog/config providers
├── migrations
├── idempotency
└── outbox/audit

Unity
├── CharacterSelectView/Presenter
├── CharacterCreationWizard
├── CharacterPreviewAssembler
├── catalog models
└── request services
```

UI không phụ thuộc trực tiếp packet/network. Network boundary ánh xạ packet thành
event/model trước khi Presentation sử dụng.

---

## 19. Thứ tự triển khai

### Giai đoạn 1 — Contract và catalog

1. Chuẩn hóa `ServerId`.
2. Tạo class/body/appearance definition contracts.
3. Tạo Creation Catalog packet.
4. Seed bốn starter class và hai body type.

### Giai đoạn 2 — Lưu trữ

1. Mở rộng `characters`.
2. Tạo `character_appearances`.
3. Tạo slot/name unique constraints.
4. Migration và integration test.

### Giai đoạn 3 — Chọn nhân vật

1. Ba slot cố định.
2. Trạng thái ô trống/tạo mới.
3. Xem trước, tên, cấp, nghề.
4. Quay lại (không tự hết giờ; xem mục 1).

### Giai đoạn 4 — Tạo nhân vật

1. Bước chọn nghề.
2. Chọn thân hình.
3. Ghép ngoại hình.
4. Đặt tên và xác nhận.
5. Tạo nguyên tử + idempotency.
6. Tự chọn nhân vật sau khi tạo.

### Giai đoạn 5 — Bản đồ tân thủ và nhiệm vụ hướng dẫn

1. Định nghĩa điểm xuất hiện tân thủ.
2. Lưu tiến độ hướng dẫn.
3. Hướng dẫn cốt lõi.
4. Đề nghị tiếp tục.
5. Mở rộng/bỏ qua/hoàn tất.

### Giai đoạn 6 — Kinh tế cosmetic

1. Quyền sở hữu cosmetic.
2. Chính sách trang bị/tháo.
3. Tích hợp cửa hàng.
4. Audit và Admin API.

### Giai đoạn 7 — Thăng cấp nghề

1. Catalog thăng cấp.
2. Bộ máy kiểm tra điều kiện.
3. Chiến binh → Cuồng chiến (Berserker).
4. Lịch sử, audit và giao diện.

### Giai đoạn 8 — Gia cố

1. Test lạm dụng, race condition, replay và tải.
2. Telemetry bảo mật.
3. Adapter kiểm tra toàn vẹn theo nền tảng.
4. Diễn tập migration/rollback trên Staging.

---

## 20. Ma trận kiểm thử tối thiểu

| Trường hợp | Kết quả |
|---|---|
| Account chưa có character | Hiện ba slot Tạo nhân vật |
| Account có một character | Một slot có dữ liệu, hai slot trống |
| Tạo vào slot đã dùng | Server từ chối |
| Tạo character thứ tư | Server từ chối |
| Hai request cùng slot | Chỉ một transaction thành công |
| Hai account tạo cùng tên | Unique constraint chỉ cho một |
| Class ID giả | Server từ chối |
| Appearance ID giả | Server từ chối |
| Cosmetic chưa sở hữu | Server từ chối |
| Body/appearance không tương thích | Server từ chối |
| Duplicate RequestId | Không tạo nhân vật lần hai |
| Disconnect lúc tạo | Query được kết quả bằng RequestId |
| Tạo thành công | Tự select và spawn starter map |
| Client gửi tọa độ spawn giả | Server bỏ qua |
| Nhảy tutorial step | Server từ chối |
| Nhận reward lặp | Không cấp lần hai |
| Promotion thiếu level/quest | Server từ chối |
| Promotion hợp lệ | Chuyển class một lần và ghi history |

---

## 21. Điều kiện phát hành

Không phát hành nếu:

- class/appearance vẫn hardcode trong View/handler;
- client quyết định stat/map/spawn;
- chưa có slot/name unique constraint;
- create character chưa idempotent;
- cosmetic chưa kiểm tra ownership;
- tutorial reward có thể nhận lặp;
- migration chưa test rollback/forward-fix;
- chưa test concurrency;
- Production vẫn dùng Development catalog/completion;
- thiếu audit/metric cho các command quan trọng.

---

## 22. Nội dung chưa cần chốt ngay

Các nội dung sau là dữ liệu, có thể quyết định sau mà không đổi kiến trúc:

- chỉ số cụ thể của bốn class;
- kỹ năng khởi đầu;
- promotion của Assassin/Mage/Archer;
- mốc chính xác thay cho level 150;
- danh sách hair/body/expression ban đầu;
- phần thưởng hướng dẫn;
- map chuyển tới sau tutorial;
- giá và loại tiền mua cosmetic;
- sprite/animation chính thức.

Khi bắt đầu thực thi, phải đi theo thứ tự contract → domain invariant →
persistence/migration → server handler → client model/service → UI. Không dựng UI
hardcode trước catalog rồi vá server về sau.

---

## 23. Tiến độ triển khai

### Bước nền tảng

- [x] Contract catalog class/body/appearance có version.
- [x] Bốn starter class và hai body type lấy từ server config.
- [x] Contract tạo nhân vật có `RequestId`, `ServerId`, `SlotIndex`, class,
  body type, appearance selections và catalog version.
- [x] Name policy và name availability pre-check phía server.
- [x] Persistence cho slot, class, body, map/spawn, appearance, tutorial state
  và idempotency request.
- [x] Unique constraint theo account/server/slot và server/name.
- [x] Migration backfill dữ liệu character cũ.
- [x] Character Select render cố định ba slot.
- [x] Character Select lấy tên class từ catalog, hiển thị trạng thái slot và
  khóa thao tác trong lúc chờ Select.
- [x] Creation form lấy class/body/starter appearance từ catalog.
- [x] UI placeholder tạo động lựa chọn appearance theo slot từ catalog; chưa
  phụ thuộc sprite hoặc prefab nhân vật chính thức.
- [x] Tạo thành công tự gửi request chọn character.
- [ ] Preview assembler bằng sprite/addressable thật.
- [x] Command, chuyển trạng thái và phần thưởng nhiệm vụ hướng dẫn tân thủ
  (`StarterTutorialService`, xem DevLog 04-08-2026).
- [ ] Command cấp quyền sở hữu/trang bị cosmetic.
- [ ] Bộ máy điều kiện thăng cấp nghề.
- [ ] Admin API/UI; foundation hiện chỉ chuẩn bị domain/read boundary.
- [ ] Adapter kiểm tra toàn vẹn theo nền tảng và test bảo mật tải/replay.
