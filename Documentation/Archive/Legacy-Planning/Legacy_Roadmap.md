# KnightClient Roadmap

## Phase 1 - Foundation

- [x] Git
- [x] Unity Project
- [x] Folder Structure
- [x] Bootstrap/App Scene Flow
- [x] GameManager
- [x] Scene Loader
- [x] VContainer Parent–Child LifetimeScope
- [x] EventBus
- [x] PostgreSQL/EF Core persistence foundation
- [x] Persistent development account + character roster
- [x] Packet dispatcher + feature packet handlers
- [x] Server-side Monster domain foundation
- [ ] Account authentication

## Phase 2 - Player & World

- [x] Player movement
- [x] Camera follow
- [x] Idle/Walk animation
- [x] SpawnPoint
- [x] Collision
- [ ] Combat

## Phase 3 - Game Systems

- [ ] Inventory
- [ ] Equipment
- [ ] Item
- [ ] Monster
- [x] Monster runtime model: HP, death, snapshot, respawn
- [x] Monster snapshot request/response packets
- [x] Unity MonsterData mapping + sticky synchronization event
- [ ] Monster Client prefab/presentation

## Phase 4 - UI & NPC

- [x] InGame HUD
- [x] NPC click + distance validation
- [x] Dynamic NPC dialog/options layout
- [x] NPC interaction events qua IEventBus
- [x] Dialog lifecycle: movement/input lock, Escape, click ngoài, NPC/scene cleanup
- [ ] NPC Shop handler
- [ ] Quest

## Phase 5 - Multiplayer

- [ ] Multiplayer

## Next Decision Gate

Không mở thêm feature gameplay trước khi NPC interaction vertical slice được
Play Test đầy đủ trong Unity:

- [ ] Không thể di chuyển hoặc click NPC khác khi dialog đang mở
- [ ] Escape, click ngoài và nút Close đều đóng dialog và trả control
- [ ] Destroy NPC hoặc unload scene không để Player bị khóa
- [ ] Shop/Quest chỉ publish `NpcActionRequestedEvent`
- [ ] Restart Server và xác nhận CharacterId/roster vẫn tồn tại
