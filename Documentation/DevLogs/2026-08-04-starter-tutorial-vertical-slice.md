# DevLog — Starter tutorial vertical slice

Date: 2026-08-04
Branch: `main`

## Objective and scope

Build the first server-authoritative tutorial loop as an extensible foundation:
start in Làng tân thủ, interact with Mẹ, travel manually to Bãi Sói, kill 20
Wolf, return for an exactly-once reward and unlock Safe Zone 1 at level 2.

Risk classification:

- Quest progress, kill credit, EXP and item reward: Authoritative gameplay and
  Critical economy/inventory mutation.
- Map transition, movement bounds and combat-map validation: Authoritative
  gameplay.
- NPC dialogue, quest HUD, map labels, portals and camera: Presentation.

## Completed work

### Authoritative tutorial and rewards

- Added configurable map, spawn, NPC, portal, item and tutorial definitions.
- Added the Mẹ tutorial state machine: talk, hunt 20 Wolf, return, reward and
  departure to Safe Zone 1.
- Quest acceptance no longer teleports the character; travel is initiated by
  the player through server-validated portals.
- Kill credit uses a monster-life identifier and persistent deduplication.
- Reward processing persists three starter iron items, EXP/level progression,
  command idempotency, gameplay audit and domain outbox records in one database
  transaction.
- Quest completion leaves the player in Làng tân thủ; Safe Zone 1 remains a
  separate player-initiated transition.

### World, portals and combat

- Added three configured maps: Làng tân thủ, Bãi Sói and Safe Zone 1.
- Added unrestricted travel between Làng tân thủ and Bãi Sói; Safe Zone 1
  requires level 2.
- Bãi Sói now contains eight configured Wolf instances; the old tutorial-map
  Training Wolf placement was removed.
- Portal requests validate the selected gameplay session, source map, range,
  destination spawn and minimum level on the server.
- Movement is clamped to authoritative four-direction map bounds.
- Combat rejects targets on another map and processes movement state before
  authoritative range validation.
- Legacy Unity scene walls are disabled at runtime. Unity creates collision
  presentation from bounds returned by the server, while the server remains
  the source of the final position.
- Portal activation now occurs by player collision and supports a player
  collider located on a child object.

### Unity presentation

- Added packet/event boundaries for NPCs, portals, tutorial progress, dialogue,
  inventory reward and map transition.
- Added runtime NPC and portal presentation, dynamic map boundary, map colour,
  camera follow and map-specific monster refresh.
- Added NPC dialogue and quest HUD feedback without placing tutorial rules in
  Unity UI.
- Legacy scene NPC presentation is disabled to prevent duplicate entities.

## Contract and compatibility

- Added tutorial/world packet types and snapshots for NPC lists, portal lists,
  interaction, portal use, quest progress and map transition.
- Portal list responses include authoritative map bounds.
- These are additive contracts for the current alpha client/server pair. Mixed
  old/new client rollout is not supported until formal protocol negotiation is
  implemented.

## Database, migration and configuration

- Added versioned migrations:
  - `StarterTutorialVerticalSlice`
  - `TutorialQuestInventoryAndKillCredits`
  - `TutorialRewardAuditOutbox`
- Added persistence for tutorial progress, kill credits, command idempotency,
  inventory items, gameplay audit and outbox messages.
- Added a Development-only account-data reset boundary for repeating the full
  character/tutorial loop. It must never be enabled in Production.
- `serverSettings.json` is the source for map bounds, portals, NPC dialogue,
  tutorial requirements, reward items and Wolf placement.
- Forward-fix/recovery: restore the prior server binary/config and database
  backup if migration deployment fails; schema rollback must not be assumed to
  be data-safe.

## Security and admin-management readiness

- Client requests are treated as intent only; session, character, map, range,
  level, quest state and duplicates are validated server-side.
- Reward mutation is transactional and idempotent, with append-only audit and
  an outbox record suitable for later reliable delivery/operations.
- Admin Web remains out of scope. Future Admin API can expose tutorial progress,
  reward audit and configured content through application read models; direct
  database access is not allowed.
- Any future quest reset or item grant command requires scoped authorization,
  reason, request ID, audit and idempotency.

## Verification

| Check | Result | Notes |
|---|---|---|
| Server Release build | Passed | 0 warnings, 0 errors |
| Server unit tests | Passed | 75/75 |
| PostgreSQL integration tests | Passed | 18/18 |
| Unity compilation | Passed previously | User confirmed compile without errors before final portal correction |
| Unity final portal smoke test | Pending | Restart server and Editor, then walk through Bãi Sói portal |

Integration coverage includes quest acceptance without automatic teleport,
deduplicated kills, exactly-once reward, inventory persistence, portal travel,
level-gated Safe Zone access and authoritative map boundaries.

## Known risks and technical debt

- Final Unity smoke test is still required after recompilation. The new server
  should report eight monsters and display `Wolf`; `Training Wolf` indicates a
  stale server/client process.
- Current world graphics and runtime labels are placeholders. Final map art,
  nav/collision geometry, safe-area work and mobile scaling remain later tasks.
- Outbox records are persisted, but a production dispatcher, retry policy,
  metrics and dead-letter handling remain future infrastructure work.
- Content definitions are configuration-backed but do not yet have Admin API or
  staged content-version rollout.

## Handover and next plan

Current state: the server-authoritative tutorial/map foundation is implemented
and automated tests pass.

Next planned feature: design Inventory opened by pressing the HP/MP HUD. Before
implementation, define the inventory domain/read model, packet boundary,
equipment ownership rules, responsive panel behaviour, authorization,
transaction/idempotency needs and Admin-management contract. The HUD click must
only open presentation; inventory contents and mutations remain authoritative
on the server.

Recommended first files:

- `KnightServer/Tutorials/StarterTutorialService.cs`
- `KnightServer/Networking/Handlers/TutorialWorldPacketHandlers.cs`
- `KnightServer/Persistence/KnightDbContext.cs`
- `KnightClient/Assets/_Project/Scripts/UI/InGameHUD.cs`
- `KnightClient/Assets/_Project/Scripts/Gameplay/World/AuthoritativeWorldPresentation.cs`
