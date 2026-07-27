# The World of Knights & Demons — Nhật ký Phát triển
## Day 9: Animation, SpawnPoint, Tái cấu trúc thư mục & Làm giàu Packet

---

## 1. Mục tiêu đầu ngày

Tiếp tục kế hoạch Animation + SpawnPoint đã dời từ Day 8 gốc (do Day 8 thực
tế bị chiếm bởi refactor `AppLifetimeScope`), cộng thêm 2 việc "ưu tiên
trung bình" còn treo: di chuyển thư mục `InGame` và làm giàu packet
`ListCharacters`.

---

## 2. Những gì đã hoàn thành

### 2.1. Player Animation (Idle/Walk)

- `PlayerAnimationController.cs` — component riêng biệt (tách khỏi
  `PlayerController`, đúng nguyên tắc 1 script 1 nhiệm vụ), đọc
  `Rigidbody2D.linearVelocity` để quyết định trạng thái `IsWalking`.
- **Quyết định kỹ thuật tốt hơn kế hoạch ban đầu:** đọc velocity thực tế
  thay vì đọc input direction từ `PlayerController` — tự động xử lý đúng
  trường hợp Player bị chặn bởi tường (input vẫn "đi tới" nhưng velocity
  giảm về 0 do va chạm), animation phản ánh đúng chuyển động vật lý thay vì
  ý định input, tránh bug "dậm chân khi đụng tường" phổ biến ở nhiều game
  amateur.
- Flip sprite theo `velocity.x` cho hướng trái/phải.
- **Sửa Animator Controller:** tắt `Write Defaults` trên cả 2 transition
  (Idle↔Walk) — phòng ngừa bug reset property sai khi mở rộng thêm state
  (tấn công, hurt...) sau này. Tắt `Has Exit Time` để chuyển trạng thái tức
  thời theo input, không có độ trễ "trôi" khó chịu.
- Animation hiện tại là **placeholder có chủ đích** — chỉ Idle/Walk, không
  phân biệt 4 hướng, dùng sprite free tạm. Ghi nhận rõ đây không phải thiếu
  sót, để tránh nhầm lẫn khi review sau này.

### 2.2. SpawnPoint

- `Gameplay/World/SpawnPoint.cs` — marker Transform thuần, có `OnDrawGizmos`
  hiển thị vòng tròn xanh trong Scene view để dễ đặt vị trí khi thiết kế map.
- `InGameLifetimeScope.Configure()` đọc `SpawnPoint` qua
  `FindAnyObjectByType`, ghi đè `CharacterData.SpawnPosition` trước khi
  `PlayerController` được inject — đúng thứ tự thời gian cần thiết (`Start()`
  của `PlayerController` đọc `SpawnPosition` để đặt vị trí ban đầu).
- Có fallback an toàn: nếu không tìm thấy `SpawnPoint` trong scene, giữ
  nguyên `Vector2.zero` mặc định, không crash — cho phép test nhanh mà
  không bắt buộc luôn phải có marker.
- Kết quả: Player xuất hiện đúng vị trí đã đặt, không còn cố định tại gốc
  tọa độ.

### 2.3. Tái cấu trúc thư mục — `InGame` sang `Root/InGame/`

- Phát hiện lần đầu: di chuyển file `.cs` **ngoài Unity Editor** (File
  Explorer/VS Code) làm đứt liên kết GUID trong `.meta`, gây hàng loạt
  Missing Script trên component đã gắn sẵn trong scene.
- Khôi phục và làm lại đúng cách: kéo-thả trực tiếp trong Unity Project
  panel — Unity tự đồng bộ `.meta` theo file `.cs`, giữ nguyên GUID, không
  mất reference đã cấu hình.
- Xác nhận sau khi sửa: không còn Missing Script, Console sạch, luồng
  end-to-end (App → Bootstrap → InGame) vẫn hoạt động bình thường.
- **Bài học quan trọng ghi vào quy trình chuẩn:** mọi thao tác di chuyển
  file `.cs` trong dự án này **bắt buộc** thực hiện trong Unity Project
  panel, không dùng công cụ ngoài — đây là quy tắc cứng, không có ngoại lệ.

### 2.4. Làm giàu packet `ListCharacters` — `CharacterId` + `Level`

- `CharacterSummaryPacket` (Shared) mở rộng từ 1 field lên 3
  (`CharacterName`, `CharacterId`, `Level`) — breaking change có chủ đích,
  đã rà soát và sửa đủ mọi nơi gọi constructor cũ (Server lẫn Client).
- Server (`Program.cs`): gán `CharacterId` tăng dần qua closure
  `Func<int> generateId` truyền vào `HandlePacketAsync`, giữ đúng nguyên
  tắc "state theo từng kết nối" đã thiết lập từ Day 5, không dùng biến
  static toàn cục.
- Client (`NetworkClient.cs`): `HandlePacket` "phiên dịch"
  `CharacterSummaryPacket` (DTO mạng) → `CharacterData` (domain model),
  đúng pattern đã dùng nhất quán từ Day 4 cho mọi loại response.
- **Sự cố trong lúc sửa:** 1 lần chỉnh sửa vô tình xóa mất 2 case
  (`ConnectResponse`, `CreateCharacterResponse`) trong `switch` khi chỉ định
  thay đúng đoạn `ListCharactersResponse` — bài học: khi yêu cầu "sửa 1 đoạn
  trong method", luôn xác nhận lại toàn bộ method sau khi sửa, không giả
  định các case khác còn nguyên.

---

## 3. Trạng thái xác nhận cuối ngày

| Hạng mục | Trạng thái |
|---|---|
| Animation Idle/Walk | ✅ Xác nhận hoạt động, phản hồi tức thì |
| SpawnPoint | ✅ Xác nhận Player spawn đúng vị trí |
| Di chuyển thư mục InGame | ✅ Xác nhận không Missing Script, luồng end-to-end OK |
| Làm giàu packet ListCharacters | ⚠️ Code đã sửa đủ 3 file, **chưa có xác nhận build/test cuối cùng** trong phiên làm việc — cần verify đầu Day 10 trước khi tiếp tục |

---

## 4. Nợ kỹ thuật còn treo

- Sprite Player vẫn là bộ free tạm, chưa phân biệt 4 hướng di chuyển.
- `CharacterId` sinh theo kiểu tăng dần trong bộ nhớ mỗi kết nối — sẽ reset
  về 1 mỗi khi Server restart hoặc client reconnect (kế thừa nợ kỹ thuật
  roster in-memory từ Day 4-5, chưa có persistent storage).
- Verbose log trong `NetworkClient.cs` (nợ từ Day 3) vẫn chưa bọc flag.
- `GameSession` giữ single `CharacterData` — khi có inventory/quest/buff
  cần tách service riêng (đã ghi từ Day 6, vẫn còn treo).

---

## 5. Việc cần làm đầu Day 10 — bắt buộc trước khi làm gì khác

1. Build Server, compile Unity, Play test toàn luồng — xác nhận việc làm
   giàu packet `ListCharacters` hoạt động đúng (đặc biệt: `HandlePacket`
   còn đủ cả 3 case sau lần sửa gần nhất).
2. Nếu pass — cập nhật `Architecture.md`: thêm mục "Multi-Scene DI:
   Parent-Child LifetimeScope" (từ Day 8), cập nhật bảng packet đã triển
   khai với field mới của `CharacterSummaryPacket`.
3. Commit theo nhóm: `feat: player animation + spawn point`,
   `refactor: move InGame scripts to Root/InGame`, `feat: enrich
   ListCharacters packet with CharacterId and Level`.

---

## 6. Kế hoạch Day 10

### Ưu tiên cao
1. **Camera bám theo mượt hơn + giới hạn biên map**
   - `CameraFollow` hiện chỉ Lerp đơn giản — thêm giới hạn để Camera không
     lộ ra ngoài `Map_Test` (dùng `Camera.orthographicSize` + bounds của
     map để clamp vị trí Camera).

2. **NPC test đứng yên — bước đầu cho combat/tương tác**
   - 1 GameObject NPC tĩnh trong map, có `Collider2D` riêng layer, xác nhận
     Player không xuyên qua nó (kiểm chứng Physics Layer Matrix đã cấu hình
     đúng, vì hiện tại chỉ có Player + tường, chưa test va chạm giữa 2 thực
     thể động/tĩnh khác loại).
   - Không cần AI, không cần dialogue — chỉ cần đứng đó để xác nhận
     collision layer hoạt động đúng trước khi làm NPC thật.

3. **Quyết định hướng Interaction cơ bản**
   - Cần bàn trước khi code: nhấn phím tương tác (E) khi đứng gần NPC, hay
     click chuột vào NPC? Đây là quyết định ảnh hưởng tới
     `IMovementInputProvider` có cần mở rộng thành `IInteractionInputProvider`
     riêng hay gộp chung.

### Ưu tiên trung bình
4. Dọn verbose log `NetworkClient.cs` (nợ kỹ thuật từ Day 3, vẫn treo qua
   nhiều ngày — nên xử lý trước khi log tăng thêm do nhiều tính năng mới).
5. HUD: thêm hiển thị `Level` (đã có dữ liệu từ packet mới Day 9, chưa lên
   giao diện).

### Để sau
6. Sơ bộ combat: hit detection, damage number, HP bar.
7. Cân nhắc authoritative server movement (client predict + reconcile).
8. Bắt đầu suy nghĩ về persistent storage (SQLite) để `CharacterId`/roster
   không mất khi Server restart — đây sẽ là cột mốc quan trọng, đánh dấu
   Server chuyển từ "giả lập học tập" sang "gần với production" hơn.

---

*Tài liệu này nên lưu tại `Documentation/DevLog/Day9.md`, nối tiếp
`Day8_progress_log.md`.*
