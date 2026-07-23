# The World of Knights & Demons
## Architecture Documentation

> Đây là bản Architecture tổng hợp được viết lại dựa trên tài liệu Architecture hiện tại,
> DevLog (đến Day 8) và tài liệu Network/Phase 1.

## 1. Project Overview

**The World of Knights & Demons** là dự án MMORPG 2D sử dụng Unity cho Client và .NET cho Server.

Mục tiêu của dự án là xây dựng một nền tảng có khả năng mở rộng dài hạn theo kiến trúc **Server Authoritative**, trong đó:
- Server quyết định mọi game state.
- Client chỉ gửi input, render và hiển thị UI.

---

## 2. Current Development Status (Day 8)

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

### Chưa triển khai

- Combat
- Inventory
- Equipment
- Quest
- NPC
- Monster AI
- Database thật
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

---

## 9. EventBus

Gameplay và Network không gọi trực tiếp UI.

Mọi notification đi qua EventBus.

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

---

## 11. Technical Debt

- Animation
- SpawnPoint
- CharacterId từ Server
- Database
- Combat

---

## 12. Roadmap

1. Animation
2. Spawn System
3. Monster
4. Combat
5. Inventory
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
- DevLog Day 1–Day 8
- Network Architecture
- Phase 1 Foundation

Đây là phiên bản khởi đầu để tiếp tục mở rộng thành tài liệu đầy đủ cho toàn bộ dự án.
