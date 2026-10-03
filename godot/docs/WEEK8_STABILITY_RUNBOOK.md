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

## Rejected Action Feedback

Rejected player attempts show a red banner without adding combat-log entries or
spending resources. Unavailable abilities remain dimmed but clickable so their
mana, action, cooldown, weapon, or status requirement can be explained.

Check mouse and keyboard targeting, locked/occupied doors, healing a full-health
ally, movement limits and blocked routes, utility targets, distant/empty loot,
scroll requirements, shop funds, and party/reserve restrictions. A rejected
target should leave targeting active for another attempt. Free actions must
remain usable after the normal action is spent; Fireball can still target empty
areas and area spells can still affect allies.

Repeated identical failures must not restart or queue copies of the warning.
An existing combat/zone banner finishes first, then the latest pending failure
is shown. Other queued combat/zone banners are preserved.

After building, run the focused regression checks with a Godot .NET executable:

```powershell
godot --headless --path . --script res://tests/action_feedback_test.gd
godot --headless --path . --script res://tests/utility_abilities_test.gd
```

## Exit Gate

- 10 consecutive runs marked PASS.
- No unresolved blocker defects.
- Build remains clean after final fix set.
