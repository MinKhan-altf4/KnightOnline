# KnightOnline — Project development checkpoint

Checkpoint date: 2026-08-04  
Repository baseline: `98c056c` — `feat: complete starter tutorial vertical slice`  
Purpose: periodic code and runtime review before the next development cycle.

> This file is a historical checkpoint, not a replacement for
> `KNIGHT_PROJECT_CORE_RULES.md` or the current architecture/design documents.
> Every conclusion must be rechecked against code, configuration, migrations
> and tests when a later audit is performed.

## 1. Project direction established so far

KnightOnline is being developed as an expandable server-authoritative MMORPG.
The immediate delivery strategy was deliberately narrowed from a broad MMO to
a verified vertical slice for one player first, while keeping contracts and
server boundaries suitable for later multiplayer expansion.

The long-term product direction currently includes:

- account registration/login and secure remembered sessions;
- one account with up to three characters per server;
- four initial classes: Warrior, Assassin, Mage and Archer;
- modular character appearance assembled from body, hair, legs and future
  cosmetic/equipment layers;
- server-authoritative movement, combat, quest, progression and economy;
- maps connected by validated portals;
- equipment, inventory, monster training and quest progression;
- structured operational/security logs and future Web Admin management;
- an initial public Alpha target of up to 500 active accounts;
- desktop and mobile presentation with responsive controls and HUD.

Core product principle:

```text
Client sends intent; server validates and decides the official result.
```

## 2. Chronological development summary

### Foundation and repository structure

- Established the Unity client, .NET server and shared packet source in one
  repository.
- Added dependency injection, EventBus, UniTask-based client operations and
  scene composition roots.
- Split runtime responsibilities instead of relying on one global manager.
- Introduced documentation structure, mandatory Core Rules and session DevLogs.

### Client/server networking

- Built packet-envelope transport and client packet-handler boundaries.
- Connected network responses to Unity events/presenters rather than allowing
  gameplay UI to own server rules.
- Added disconnect handling, receive-loop lifecycle and scene-safe EventBus
  dispatch.
- Fixed stale/destroyed Unity view callbacks during scene transitions.

### Authentication and account-session flow

- Added guest authentication foundation and guest-to-account conversion path.
- Added registered login and refresh-token rotation/reuse detection foundations
  backed by PostgreSQL integration tests.
- Device remembers only the latest successfully authenticated account session;
  plaintext password is not the intended persistent credential.
- Entry waits for player confirmation instead of automatically entering
  Character Select from a refresh token.
- Character Select is counted as an active account session.
- A second device cannot displace an account already active in Character Select
  or InGame; it receives an account-active rejection.
- Added heartbeat, TTL, disconnect grace and lease generation to prevent stale
  connections from renewing or releasing a newer owner.
- Added explicit logout and server-authoritative re-entry cooldown.
- Added active-account admission capacity of 500 and transport capacity of 750
  in the current single-process foundation.

### Character flow

- Added Character Select and creation flow with a maximum of three slots per
  account/server.
- Added configurable class, body type and appearance catalog foundations.
- Added name validation and server-backed character persistence.
- Added SelectCharacter to gameplay-session transition and EnterWorld snapshot.
- Removed the obsolete standalone Login scene; current scene flow is:

```text
App -> Bootstrap/Entry -> Character Select/Create -> InGame
```

### Authoritative movement and combat

- Added ordered movement input and server-owned player position.
- Server ignores duplicate/stale sequence values and validates movement speed.
- Client movement is presentation/prediction; snapshots correct the official
  position.
- Added map-aware combat validation, target life state, cooldown and range
  rejection reasons.
- Added server-decided respawn displacement to prevent a monster respawning
  through a player.
- Movement final position is clamped to configured map bounds.
- Monster/map mismatch is rejected before damage is applied.

### Monster selection and HUD

- Added MonsterView/Spawner, monster selection, shared target marker and fixed
  TargetPanel beside the minimap.
- Removed duplicate world-space monster health UI; target HP belongs in the HUD.
- Added authoritative player HP/MP, level and EXP snapshots.
- Added compact HP/MP HUD and enlarged minimap placeholder.
- Added a collapsible bottom menu with Logout implemented and future slots for
  equipment, mount and friends.

### Starter tutorial vertical slice

- Added three configured maps:
  - Làng tân thủ;
  - Bãi Sói;
  - Safe Zone 1.
- Player begins in Làng tân thủ and talks to NPC Mẹ.
- Accepting the quest does not teleport the player.
- Player manually enters Bãi Sói through a collision-triggered portal.
- Travel between Làng tân thủ and Bãi Sói is available while progressing the
  quest; Safe Zone 1 requires level 2.
- Bãi Sói contains eight configured Wolf instances.
- Kill credit is deduplicated by monster-life identity.
- Returning after 20 Wolf kills grants EXP and the starter iron weapon, armour
  and pants in one idempotent database transaction.
- Reward processing also writes gameplay audit and outbox records.
- Map bounds, NPCs, portals, monsters and transition snapshots are server-owned;
  Unity creates temporary presentation from those snapshots.

## 3. Current runtime flow

### Returning registered player

```text
Open App
  -> Entry displays Continue / Change Account / Server
  -> player presses Continue
  -> server validates refresh/authentication state and active-account lease
  -> Character Select becomes active and starts heartbeat
  -> player selects a character
  -> server creates gameplay session
  -> EnterWorld snapshot
  -> InGame
```

### New/guest player

```text
Open App
  -> Entry
  -> Play New sends a real guest-session request
  -> Character creation/select flow
  -> select character
  -> EnterWorld
```

Guest restrictions and the final level-10 registration conversion experience
remain product work; they must preserve the guest ownership transaction and
must not trust account identity supplied by Unity.

### Logout and connection loss

- Explicit InGame Logout releases gameplay ownership and starts configured
  disconnect grace/re-entry cooldown.
- Re-entry during cooldown displays the remaining server time.
- Re-entry after expiry is immediate; Unity does not impose a fixed delay.
- Crash, app close or network loss is detected through lease expiry rather than
  relying only on a clean close event.
- A stale connection/generation cannot release the replacement session.

### Starter gameplay loop

```text
Enter Làng tân thủ
  -> interact with Mẹ
  -> accept hunt quest
  -> walk into Bãi Sói portal
  -> kill 20 Wolf
  -> return through Làng tân thủ portal
  -> interact with Mẹ
  -> transaction grants equipment + EXP
  -> character reaches level 2
  -> Safe Zone 1 portal becomes available
```

## 4. Architecture boundaries currently in place

| Boundary | Current responsibility |
|---|---|
| Unity View | Render state, capture input and publish UI intent |
| Unity Presenter/Controller | Translate view events to application/network requests |
| Network handler | Parse/map packet and publish client-domain events |
| Server packet handler | Authenticate, authorize, validate and call application/domain service |
| Application/domain service | Enforce quest, movement, combat, reward and session rules |
| Repository/EF Core | Persist authoritative state through versioned schema |
| Configuration | Maps, spawns, portals, NPC dialogue, tutorial requirements and tunable values |
| Audit/outbox | Persist traceable effects and future reliable event delivery |

Important constraints:

- Unity Physics is presentation assistance, not the final authority.
- UI must not mutate inventory, reward, EXP, HP, map or quest truth.
- Admin Web must eventually use Admin API/application contracts, never direct
  writes to the game database.
- Configuration validation must reject invalid map, portal, spawn and content
  references at server startup.

## 5. Persistence and migration checkpoint

PostgreSQL persistence currently covers foundations for:

- accounts, refresh-token families and guest/registered identity;
- characters and character appearance;
- tutorial progress;
- character inventory reward rows;
- tutorial kill-credit deduplication;
- tutorial command idempotency;
- gameplay audit records;
- domain outbox records.

Tutorial-related migrations at this checkpoint:

- `20260804081708_StarterTutorialVerticalSlice`
- `20260804083518_TutorialQuestInventoryAndKillCredits`
- `20260804085111_TutorialRewardAuditOutbox`

Development includes a controlled account-data reset path for repeating the
character/tutorial loop. This path must remain disabled outside Development.

## 6. Verification baseline

Latest automated result at commit `98c056c`:

| Verification | Result |
|---|---|
| KnightServer Release build | Passed, 0 warnings and 0 errors |
| Server unit tests | 75/75 passed |
| PostgreSQL integration tests | 18/18 passed |
| Unity compile before final portal correction | Passed by user |
| Final portal Unity smoke test | Still required after clean restart/reimport |

Automated coverage includes session lease/generation, admission limits,
authentication persistence, character API boundaries, authoritative movement,
map-aware combat, tutorial state transitions, duplicate kill protection,
portal flow and exactly-once tutorial reward persistence.

Passing tests prove only their covered boundaries. They do not prove public
Alpha readiness, real-device rendering, network quality under load or safe
multi-node operation.

## 7. Known issues observed at this checkpoint

### Gameplay/presentation issues

- Player movement can visibly stutter when approaching colliders or world
  objects. Server correction, Unity collision presentation and interpolation
  must be traced together before changing movement rules.
- NPC Mẹ currently lacks final artwork/animation and a complete selectable
  target presentation.
- Map transitions still need smoother camera/loading/presentation sequencing.
- Current maps, portals, NPC labels and Wolf presentation are placeholders, not
  final art or level design.
- Inventory has persistence for tutorial reward rows but no complete player UI,
  list/query contract or general-purpose item-domain workflow yet.
- Reward completion does not yet show a polished item/EXP popup.

### Unity/editor issues

- TextMesh Pro may report an inconsistent import result for
  `LiberationSans SDF - Fallback.asset` and may regenerate its glyph cache.
- That fallback asset is intentionally not included in the vertical-slice
  commit. It requires a dedicated later investigation and must not be casually
  overwritten.
- Final desktop/mobile scale, safe area and representative aspect ratios are
  not yet accepted.

### Production/operations gaps

- Account lease/admission ownership remains a single-process foundation; a
  distributed atomic adapter is required before multi-node deployment.
- Public transport still requires production TLS and secure platform token
  storage verification.
- Outbox persistence exists, but dispatcher/retry/dead-letter monitoring is not
  complete.
- Interest management, bounded outbound queues and load tests are required for
  a real 500-player Alpha.
- Admin API, operational dashboards, alert thresholds, backup/restore drills
  and incident runbooks remain incomplete.
- Formal protocol-version negotiation for mixed client/server rollout is not
  complete.

## 8. Current readiness assessment

| Area | State | Meaning |
|---|---|---|
| Repository/core architecture | Foundation established | Boundaries exist but require continued audit |
| Authentication/session | Alpha foundation | Tested locally/PostgreSQL; not multi-node production-ready |
| Character flow | Functional foundation | Three-slot and EnterWorld flow exist; final UX/art incomplete |
| Movement/combat | Authoritative foundation | Core validation exists; collision smoothness remains unresolved |
| Tutorial quest | First vertical slice | Automated server flow passes; final Unity smoke/polish remains |
| Inventory/equipment | Partial foundation | Reward rows exist; full inventory domain/UI is next |
| Maps/content | Prototype | Three logical maps exist with placeholder presentation |
| HUD/mobile UI | Prototype-to-foundation | Functional HUD exists; responsive acceptance remains |
| Operations/Admin | Planned foundation | Audit/outbox/config seams exist; production tooling incomplete |

The project has a recognizable and testable shape, but it is not yet a public
Alpha build. The next work should strengthen the existing slice rather than add
many unrelated systems at once.

## 9. Mandatory periodic review procedure

Use this section whenever pausing feature development to audit the repository.

### Step 1 — Establish the exact baseline

- Record branch, commit SHA and working-tree status.
- Preserve user/Unity-generated changes; do not reset them blindly.
- Read Core Rules, this checkpoint, the current architecture documents and the
  latest relevant DevLog.
- Classify every proposed fix as Critical, Authoritative gameplay,
  Presentation or Tooling/prototype.

### Step 2 — Trace runtime flows from player input to persistence

For each important action, trace:

```text
Unity input/view
  -> presenter/application request
  -> packet contract
  -> server handler authorization/validation
  -> domain/application service
  -> transaction/repository
  -> response/snapshot
  -> client event
  -> Unity presentation
```

Review at minimum:

- authentication, Continue, Change Account and logout;
- Character Select/Create and EnterWorld;
- movement, correction, collision and map transition;
- target selection and combat;
- NPC interaction and quest progression;
- reward, inventory and equipment ownership.

### Step 3 — Check Core Rules compliance

- Search for gameplay truth decided by Unity.
- Search for duplicated business rules across handlers/services/UI.
- Check authorization, map/session state and input validation at every packet.
- Check transaction and idempotency for item, EXP and ownership changes.
- Check config instead of hardcoded operational/game-balance values.
- Check `.meta` files and assembly dependency direction.
- Check secrets, raw tokens, passwords and sensitive payloads are never logged
  or committed.

### Step 4 — Check failure and concurrency cases

- Duplicate, malformed, stale and reordered packets.
- Double click/retry and response lost after transaction commit.
- Disconnect, reconnect, old generation and server restart.
- Two devices using the same account.
- Target death/respawn during movement or interaction.
- Portal use at map boundary and while state changes concurrently.
- Reward request replay and partial database failure.

### Step 5 — Verify build, schema and runtime

- Release-build the server.
- Run unit tests.
- Run PostgreSQL integration tests.
- Validate migrations on a disposable Development database.
- Open Unity, allow full import/compile and confirm no red Console errors.
- Perform a clean full-flow smoke test from Entry through the tutorial reward.
- Test at least two client instances for account ownership/session behaviour.
- Record warnings separately; do not normalize recurring warnings without a
  root-cause decision.

### Step 6 — Produce an audit result

Record:

- verified behaviours and exact evidence;
- violations grouped by risk and owner;
- migrations/config/contract compatibility;
- security and operational gaps;
- rollback or forward-fix path;
- the smallest safe next milestone.

Do not modify this historical checkpoint to pretend an issue never existed.
Create a new dated audit file or update a current source-of-truth architecture
document when project state changes.

## 10. Next development milestone

The next planned session is intentionally limited to strengthening the current
vertical slice:

1. Design and implement Inventory opened by pressing the HP/MP HUD.
2. Define a reusable item stack/slot/read-model boundary instead of coupling
   Inventory to the three tutorial rewards.
3. Keep list/equip/unequip mutations server-authoritative with validation,
   idempotency, audit and transaction rules proportional to ownership risk.
4. Show a reward popup containing authoritative items and EXP after the tutorial
   transaction succeeds.
5. Give NPC Mẹ final placeholder artwork, collider/interaction anchor and target
   behaviour without embedding quest rules in the view.
6. Diagnose movement stutter near colliders with captured client prediction and
   server snapshot evidence.
7. Smooth map transitions while keeping the destination map/spawn controlled by
   the server.

Acceptance must cover the complete path:

```text
Quest reward commits
  -> authoritative reward response/snapshot
  -> popup displays once
  -> Inventory opened from HP/MP HUD shows the same items
  -> reconnect shows the same persisted inventory
```

## 11. Reading order for the next review

1. `Documentation/KNIGHT_PROJECT_CORE_RULES.md`
2. `Documentation/Audits/2026-08-04-project-development-checkpoint.md`
3. `Documentation/Architecture/Current_Unity_Runtime_And_Alpha_500_Online.md`
4. `Documentation/Design/System_And_HUD_Flow_v2.md`
5. `Documentation/Design/Character_Flow_Architecture_Plan.md`
6. `Documentation/Production/Account_And_Character_Flow_Cutover.md`
7. `Documentation/DevLogs/2026-08-04-starter-tutorial-vertical-slice.md`
8. Current code, configuration, migrations and tests for the module being
   reviewed.
