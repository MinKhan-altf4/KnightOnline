# KnightOnline Architecture

## Solution

KnightOnline

- KnightClient (Unity)
- KnightServer (.NET)
- KnightShared (.NET Class Library)

## Networking

Server Authoritative

Client chỉ:

- Render
- Input
- UI
- Audio

Server quyết định:

- Damage
- HP
- Inventory
- Position
- Monster AI
- Drop
- Quest

## Communication

TCP

Packet-based

Binary serialization (sẽ quyết định sau)

## Database

SQLite (Prototype)

MySQL/PostgreSQL (Production)