# DevLog — Phase 1 vitals, HUD and session exit

Date: 2026-08-04
Branch: `main`

## Objective and scope

Complete the first single-player-facing InGame foundation without weakening
the future multiplayer/server-authoritative boundaries. This session covered
authoritative character vitals, the first compact HP/MP HUD, a collapsible
bottom function menu, explicit logout, and server-controlled re-entry cooldown.

Risk classification:

- Account logout, lease cooldown and re-authentication: Critical.
- Character vitals and combat-facing state: Authoritative gameplay.
- HUD layout, menu and scene tooling: Presentation/tooling.

## Completed work

### Authoritative character vitals

- Added an ordered character-vitals snapshot contract and Unity event mapping.
- `PlayerSession` now owns reusable damage, healing, mana spending and mana
  restoration operations rather than embedding HP/MP mutations in UI or packet
  handlers.
- Progression changes publish the current authoritative vitals so the HUD stays
  consistent after a level/stat change.
- Added domain regression tests for damage, healing, mana and snapshot ordering.

### Player HUD and minimap presentation

- Reworked the generated PlayerStatusPanel into a larger high-contrast HP/MP
  panel with level and EXP percentage.
- Removed the temporary character-name, connection-status and coordinate debug
  labels from the production HUD.
- Enlarged and restyled the placeholder minimap and kept TargetPanel positioned
  to its left.
- Retained a responsive `1920x1080` CanvasScaler baseline; mobile safe-area and
  final art validation remain later presentation work.

### Collapsible bottom function menu

- Added `InGameMenuView` and builder support for a bottom-center arrow.
- Opening the menu hides PlayerStatusPanel, minimap and TargetPanel and exposes
  an expandable function bar.
- Implemented the Logout button. Mount, equipment, friends and expansion slots
  are visible extension points but intentionally disabled until their domains
  exist.
- Movement input is disabled before logout is sent, preventing gameplay packets
  from being emitted after the server detaches the selected character.

### Logout and re-entry cooldown

- Logout now releases active-player ownership, detaches the gameplay session,
  and places the account lease into a configurable disconnect-grace cooldown.
- The default cooldown remains server configuration (`10` seconds); Unity does
  not decide whether the previous session has expired.
- A re-entry attempt during cooldown receives the authoritative remaining time.
  Unity displays only that remaining countdown and retries after it expires.
- If the player waits until the old session has expired before pressing
  Continue, authentication succeeds immediately without a fixed ten-second
  delay.
- A different device that genuinely owns the active account still produces the
  existing account-active rejection rather than being displaced.

### Entry versus logout semantics

- Character Select remains an active authenticated account session with a live
  heartbeat.
- Character Select `Back` now only navigates to Entry and keeps that session;
  it no longer invokes logout or starts another cooldown.
- Pressing Continue while the current connection is already authenticated
  immediately requests Character Select data again.
- Only the explicit InGame Logout action closes the account/gameplay session.

### Scene-transition event safety

- Hardened EventBus dispatch for synchronous scene changes:
  - bindings removed during an earlier callback are skipped;
  - delegates whose Unity target has already been destroyed are removed and
    never invoked.
- Packet callback exceptions are now reported as packet-handler failures and do
  not masquerade as transport read failures or tear down a healthy socket.
- This fixes the stale `CharacterCreationView` callback observed while moving
  between Bootstrap and InGame.

## Architecture decisions

- The server clock and lease store are the sole authority for logout cooldown.
- Presentation may display a countdown but never creates or clears a lease.
- UI no longer depends on the Root assembly or reaches into `GameSession`,
  avoiding an assembly dependency cycle.
- Navigation back to Entry and explicit logout are separate application
  commands because they have different security/session semantics.
- A UI callback failure is isolated from transport lifecycle, while the full
  exception remains visible for diagnosis.

## Verification performed

- `dotnet test KnightServer.Tests -c Release --no-restore`
  - Passed: 63/63.
- `dotnet test KnightServer.IntegrationTests -c Release --no-restore`
  - Passed: 15/15 against the configured PostgreSQL integration database.
- `dotnet build KnightServer -c Release --no-restore`
  - Succeeded with 0 warnings and 0 errors.
- Unity had compiled and the user had exercised the preceding InGame/logout
  flow during the session. The final `Character Select -> Back -> Continue`
  adjustment still requires one Editor smoke test after Unity recompiles.

## Configuration, compatibility and recovery

- `Authentication.DisconnectGraceSeconds` remains the configurable source for
  the cooldown and defaults to 10 seconds.
- Authentication response packets gained an optional retry-after value. This is
  append-only for the current JSON contract, but formal protocol negotiation is
  still required before mixed client/server production rollout.
- Rollback is a code forward-fix/revert; this session adds no database schema
  migration.

## Admin-management readiness

- Cooldown and active-session decisions remain behind the account-session
  registry/application boundary; an Admin API can later expose a safe read
  model without giving Admin Web direct database access.
- No Admin command was added. Any future force-release command must require a
  scoped permission, reason, request ID, audit record and generation-safe lease
  handling.

## Known risks and follow-up

- Run one final Unity smoke test:
  1. logout from InGame;
  2. press Continue during cooldown and verify only remaining time is shown;
  3. enter Character Select;
  4. press Back, then Continue, and verify immediate Character Select without a
     second cooldown;
  5. confirm no destroyed-view callback or rejected movement packet appears.
- Bottom-menu placeholder actions are intentionally not implemented.
- Final HUD art, safe-area adaptation and representative desktop/mobile aspect
  ratio validation remain later Presentation work.
- Active leases are still process-local and need distributed ownership before
  a multi-node Production deployment.

## Phase result

Phase 1 implementation baseline is complete: authoritative vitals, usable
player HUD, extensible bottom menu, explicit logout, server-authoritative
cooldown, and correct Entry/Character Select navigation are in place. The
manual smoke test above is the acceptance checkpoint before beginning the next
feature phase.
