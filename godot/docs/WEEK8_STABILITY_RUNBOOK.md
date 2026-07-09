# Week 8 Stability Runbook

Goal: pass 10 consecutive full runs without a blocker bug.

## Scope

A run is complete when all steps below pass in sequence:

1. New game starts and party spawns.
2. Exploration movement works.
3. Aggro starts combat.
4. Combat resolves with no soft-lock.
5. Loot interaction works (open prop and pick up bag item).
6. Map transition works.
7. Save, quit, and load restore expected state.

## Blocker Definition

Mark a run as BLOCKER FAIL if any of these occur:

- Crash, freeze, or unresponsive input requiring restart.
- Turn cannot advance when valid actions exist.
- Unit gets stuck in invalid state (dead/alive mismatch, no control return).
- Map transition fails or spawns party into invalid location.
- Save/load loses core progression state (party, map, combat, loot, doors).

## Test Environment

- OS: Windows 10 and Windows 11.
- Resolution targets: 1920x1080 and 2560x1440.
- Session lengths: 10m smoke, 30m normal, 60m endurance.

## Run Procedure

1. Build before first run:

```powershell
dotnet build DarkDungeonTactics.csproj
```

2. Start run from game launch.
3. Complete the run scope flow.
4. Record result in `docs/WEEK8_RUN_LOG.md`.
5. If blocker found:
   - stop counting consecutive runs,
   - log exact repro steps,
   - fix bug,
   - restart count at Run 1.

## Exit Gate

- 10 consecutive runs marked PASS.
- No unresolved blocker defects.
- Build remains clean after final fix set.
