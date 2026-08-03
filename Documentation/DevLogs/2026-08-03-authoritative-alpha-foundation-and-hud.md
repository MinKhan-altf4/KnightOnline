# DevLog — Authoritative Alpha foundation and Player HUD

Date: 2026-08-03  
Branch: `main`

## Objective and scope

Stabilize the authoritative gameplay baseline required before Alpha feature
development, then establish the first visible player progression HUD. The
session covered account lease reliability, ordered movement reconciliation,
map-aware combat, authoritative monster-respawn displacement, PostgreSQL-backed
progression levels 1–40, composable character stats, and Unity Player HUD
tooling.

Risk classification:

- Account lease/session work: Critical.
- Movement, combat, respawn and progression: Authoritative gameplay.
- InGame HUD and its scene builder: Presentation/tooling.

## Completed work

### Account session and connection reliability

- Kept account leases alive through authenticated gameplay activity and a
  realtime heartbeat independent of Unity time scale.
- Enabled background execution so a Development client can maintain its lease
  during local multi-client testing.
- Strengthened generation/ownership checks and stale-lease rejection.
- Added regression coverage around heartbeat, expiry and concurrent account
  ownership.
- Improved authentication flow failure handling so secure-store failures close
  loading UI and produce actionable feedback instead of an infinite spinner.

### Server-authoritative movement

- Added client movement sequence numbers and authoritative position snapshots.
- Server rejects duplicate, stale, out-of-order and invalid movement input.
- Client keeps prediction for responsiveness but reconciles to server position
  with soft correction and hard correction thresholds.
- Position snapshots include acknowledgement/server sequence and a correction
  reason suitable for future metrics and investigation.

### Respawn displacement and map-aware combat

- Monster spawns now belong to an explicit map definition.
- Monster list and state broadcasts are filtered by the player's current map.
- Combat validates player and monster map equality and returns `WrongMap`
  separately from `OutOfRange`, dead-target and cooldown outcomes.
- Added a deterministic server-side monster-respawn displacement resolver.
- When a monster respawns on a player, the server selects the official safe
  position and sends the corresponding authoritative position snapshot.
- Unity physics remains presentation/prediction only; it does not determine the
  final multiplayer position.

### Progression levels 1–40 and character stats

- Added a configurable experience curve with a server maximum level of 40 and
  a separate guest level cap.
- Added configurable monster EXP rewards.
- Added `total_experience` persistence and the
  `character_progression_grants` idempotency/audit table.
- Added EF Core migration `20260803122640_AlphaProgressionFoundation` and model
  snapshot updates.
- EXP grants execute in a PostgreSQL transaction with row locking and a unique
  request ID. A repeated reward request returns the stored outcome without
  adding EXP twice.
- Each monster life has a unique ID, used as the kill reward request ID.
- Added normalization for legacy characters whose saved level predates
  `total_experience`, preventing the first EXP grant from reducing their level.
- Avoided numeric overflow when a large EXP value reaches a capped character.
- Added a composable stats pipeline: class base stats, per-level growth and
  source-addressable additive/multiplicative modifiers. Equipment and buffs can
  extend this pipeline without rewriting progression.
- Selected-character snapshots and progression events now carry authoritative
  EXP, level, HP, MP, Attack and Defense values to Unity.

### Unity Player HUD

- Extended `InGameHUD` to display and refresh player name, level, HP, MP and
  level-relative EXP.
- Added null-safe presentation handling and validation that bar images use
  `Image.Type.Filled`.
- Added the Editor-only menu tool
  `KnightOnline > UI > Build Player Status HUD`.
- The builder creates `HUD_Canvas/PlayerStatusPanel`, constructs all bars and
  labels, anchors the panel at the upper-left, and connects serialized
  references automatically.
- Added Unity 6-compatible Editor APIs and direct TextMeshPro/UGUI assembly
  references.
- Replaced the removed Unity 6 Resources lookup with an Editor asset lookup and
  a project-sprite fallback.
- Generated scene/UI asset changes were saved in `InGame.unity`; associated
  Unity `.meta` files are included.

## Architecture decisions

- The server remains the only source of truth for position, map, combat result,
  EXP, level and stats.
- Client prediction and HUD formatting are presentation concerns and cannot
  commit gameplay state.
- Balance values live in validated server configuration rather than packet
  handlers or Unity views.
- Progression grants use effectively-once database semantics inside the current
  PostgreSQL boundary; the implementation does not claim end-to-end exactly
  once delivery.
- The stats pipeline accepts modifier sources so equipment, buffs, passives and
  future class advancement can compose without hardcoded item checks.
- The HUD builder is Editor-only tooling and is excluded from runtime builds.

## Verification performed

- `dotnet test KnightServer.Tests -c Release --no-restore`
  - Passed: 58/58.
- `dotnet test KnightServer.IntegrationTests -c Release --no-restore`
  - Passed: 15/15 against PostgreSQL.
  - Integration tests were made sequential because they currently migrate and
    clean the same configured database; this removes a schema-migration race in
    CI/local runs.
- `dotnet build KnightServer -c Release --no-restore`
  - Succeeded with 0 warnings and 0 errors.
- Unity compiled the Editor tool far enough to identify and remove Unity 6
  deprecated APIs and the removed built-in sprite Resources path. Final visual
  Play Mode verification remains a manual checkpoint after rebuilding/saving
  PlayerStatusPanel.

## Migration, rollout and recovery

- Migration adds a non-null `total_experience` column with a safe zero default,
  positive-level/nonnegative-EXP constraints, and the progression grant table.
- Application-level normalization preserves legacy character levels when old
  rows initially have zero total EXP.
- Before applying to staging/production, take a verified backup and run the
  migration against a staging copy.
- `Down()` removes progression history and total EXP and is therefore
  destructive. Prefer a forward-fix after real player progression exists.
- New packet fields were appended as optional/default-compatible fields where
  applicable, but a formal client/server protocol-version gate is still needed
  before rolling deployments.

## Admin-management readiness

- Progression grant rows provide a queryable history by character, timestamp,
  request ID and reward source without requiring Admin Web database writes.
- A future Admin API can expose a paginated progression history read model.
- Any future Admin EXP grant must use the same progression application service,
  require scoped permission and reason, use a unique request ID, and append
  audit information. Admin Web must not modify character EXP directly.
- Metrics/dashboard/API endpoints are not implemented in this session.

## Known risks and technical debt

- A monster kill and its EXP reward do not yet cross a durable outbox boundary.
  A database outage at the exact kill/reward boundary can leave an unresolved
  reward. Add a durable gameplay reward event/outbox before Production.
- Authoritative world state and active sessions are still in process memory;
  multi-server sharding/ownership transfer is not implemented.
- Transport TLS and production secure credential storage remain release gates;
  current server environment validation intentionally permits Development only.
- Player HP/MP HUD currently refreshes on initial snapshot and progression
  changes. General damage, healing, skill cost and respawn need a unified
  authoritative character-vitals event.
- Unity PlayerStatusPanel requires final visual validation across target desktop
  and mobile aspect ratios. Safe-area handling is still pending.
- The integration suite shares one PostgreSQL database and is intentionally
  sequential. Isolated per-test databases/schemas may be required when CI scale
  increases.

## Recommended next steps

1. Verify PlayerStatusPanel in Unity Play Mode and at representative desktop and
   mobile aspect ratios.
2. Add authoritative character-vitals events for damage, heal, MP cost and
   respawn so HUD resources update independently of leveling.
3. Add a durable reward/outbox pipeline and recovery/query path by request ID.
4. Add progression metrics and the Admin progression-history read contract.
5. Continue the Alpha vertical slice with map/HUD/character content only after
   the above HUD and reward checkpoints pass.
