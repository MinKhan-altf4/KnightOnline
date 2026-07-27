# Day 14 Progress Log

## Monster synchronization

- Added `ListMonstersRequest` and `ListMonstersResponse` packet types.
- Added shared monster snapshot DTOs without Unity or Server domain dependencies.
- Added a Server handler that maps authoritative `MonsterService` snapshots to
  network DTOs.
- Unity requests the initial monster list when the InGame scene starts.
- Unity maps packet DTOs to `MonsterData` and publishes the sticky
  `MonsterListReceivedEvent`.
- Server Release build completed with 0 errors.

## Current boundary

This step synchronizes monster data only. Spawning and updating Unity monster
prefabs belongs to the next presentation step.
