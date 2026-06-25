# Steam MVP Production Checklist (Godot)

This plan maps the current JavaScript game structure to a Godot production build and defines the shortest path to a sellable Steam release.

## 1) Product Target

- Platform: Windows (Steam) first.
- Engine: Godot 4.x.
- Launch type: Early Access recommended.
- Core promise: Tactical grid combat dungeon crawler with exploration, party progression, loot, and enemy AI.

## 2) Source-To-Target System Mapping

### Current source modules

- `grid_scene.cs`: Game loop, turn flow, map transitions, combat, progression, loot, spawning.
- `ai.cs`: Targeting logic, pathing decisions, movement and action selection.
- `game_data.cs`: Data dictionaries for abilities, spells, items, props.
- `character.cs`: Character creation, archetypes, stat derivation.
- `graphics.cs`: Sprites, effects, texture helpers, projectile visuals.
- `ui.cs`: HUD, tooltips, ability bar, combat log, inventory, overlays.
- `map_generator.cs`: Dungeon layouts, legends, encounter placement, prop placement.

### Godot target modules

- `godot/scenes/BattleScene.tscn` + `godot/scripts/BattleController.cs`
- `godot/scripts/TurnManager.cs`
- `godot/scripts/AiDirector.cs`
- `godot/autoload/GameData.cs`
- `godot/autoload/EventBus.cs`
- `godot/scripts/Unit.cs`
- `godot/scripts/MapLoader.cs`
- `godot/ui/HUD.tscn` + `godot/scripts/HudController.cs`

### Port difficulty estimate by subsystem

- Turn system and combat actions from `grid_scene.cs`: High (7-10 days)
- AI behavior parity from `ai.cs`: Medium-High (4-6 days)
- Data schema port from `game_data.cs` and `character.cs`: Medium (3-5 days)
- Map and encounter loader from `map_generator.cs`: Medium (3-5 days)
- UI rebuild from `ui.cs`: High (6-10 days)
- Effects and projectiles from `graphics.cs`: Medium (3-5 days)
- Save/load and progression from `grid_scene.cs`: Medium-High (4-7 days)
- Steam integration and packaging: Medium (3-5 days)

## 3) Steam MVP Cut List

## Keep for MVP

- Exploration mode + combat mode.
- Party of core classes.
- Initiative turn order and AP/MP economy.
- Core abilities/spells (attack, heal, 1-2 specials per class).
- Loot pickup and equip flow.
- Enemy groups with aggro and basic encounter progression.
- XP + level-ups.
- 2-3 handcrafted maps with transitions.
- Game over, restart, and save/load.

## Defer after launch

- Full summon set parity (all summon variants).
- Advanced vendor economy tuning.
- Full status effect breadth if unstable.
- Non-critical UI polish animations.
- Large prop interaction matrix if not core to loop.

## Cut for now if needed

- Rare edge-case systems tied to one-off map props.
- Non-essential overlays/help UX variants.
- Experimental encounters not in primary progression path.

## 4) 10-Week Execution Plan

## Week 1: Foundation

- Initialize Godot project in `godot/`.
- Add core folder structure for scripts/scenes/resources.
- Implement grid coordinate conversion and occupancy service.
- Add data import stubs for abilities/items/characters.

Exit criteria:
- Unit can be spawned on grid and moved cell-to-cell in test scene.

## Week 2: Combat Skeleton

- Implement turn manager (initiative, active unit, end turn).
- Add attack action with range + line-of-sight checks.
- Add HP, death, and remove-from-combat flow.

Exit criteria:
- Full turn rotates through players and enemies with basic attacks.

## Week 3: AI and Basic Exploration

- Port nearest target and offensive ability selection from `ai.cs`.
- Add simple enemy movement toward target with collision checks.
- Add exploration movement + aggro trigger into combat.

Exit criteria:
- Explore map, trigger enemy group, resolve combat, return flow stable.

## Week 4: Data-Driven Content

- Port ability/spell/item definitions from `game_data.cs`.
- Port character stat derivation and templates from `character.cs`.
- Replace hardcoded actions with data-driven action resolver.

Exit criteria:
- Add or modify an ability via data without code changes.

## Week 5: UI MVP

- Rebuild action bar, selected ability, turn queue, HP/MP panels.
- Add combat log and tooltip detail text.
- Add inventory modal with equip/unequip.

Exit criteria:
- Full combat loop playable using only Godot HUD controls and input.

## Week 6: Maps, Props, and Loot

- Port map legend token parsing from `map_generator.cs`.
- Spawn enemies/props from authored layout data.
- Implement searchable props and loot bags with shared inventory.

Exit criteria:
- Complete one dungeon run with loot and map transition.

## Week 7: Progression and Save

- Implement XP gain, level-up bonuses, and unlock notices.
- Add save/load for map state, doors, loot drops, and party state.
- Add fail-safe load validation and migration-safe fields.

Exit criteria:
- Quit and reload mid-run without state loss.

## Week 8: Stability and Content Completion

- Add missing must-have spells/statuses.
- Tune AI and action costs.
- Fix soft-lock risks in turn transitions and pending actions.

Exit criteria:
- 10 consecutive full runs without blocker bug.

## Week 9: Steam Integration

- Integrate Steamworks SDK (app init, overlay check).
- Add achievements (optional but recommended).
- Add cloud saves if save path is stable.
- Configure builds/depots and internal beta branch.

Exit criteria:
- Steam internal branch install and play succeeds on clean machine.

## Week 10: Release Prep

- Performance pass, crash logging, settings menu.
- Final QA checklist pass.
- Store page assets and launch copy finalized.
- Build and submit launch candidate.

Exit criteria:
- Release candidate passes smoke tests and store page is ready.

## 5) Steam Readiness Checklist

- Runs at stable frame rate on min target hardware.
- Proper windowed/fullscreen behavior and resolution scaling.
- Save/load robust across updates.
- Input rebinding or at least clear fixed key mapping.
- No blocking crashes in a 60-minute play session.
- Steam overlay works in release build.
- Build upload and branch promotion workflow documented.

## 6) QA Matrix (Minimum)

- OS: Windows 10, Windows 11.
- Resolution: 1920x1080 and 2560x1440.
- Session lengths: 10 min smoke, 30 min normal, 60 min endurance.
- Save cases: fresh save, mid-combat save, post-level-up save, map-transition save.
- Combat edge cases: all party dead, all enemies dead, summon deaths, poison/sleep interactions.

## 7) Commercial Risk Register

- Risk: UI rewrite takes longer than logic port.
  - Mitigation: HUD MVP first, polish later.
- Risk: Turn-state desync under projectile/action timing.
  - Mitigation: keep authoritative combat state in turn manager, visuals as observers only.
- Risk: Scope creep from content additions before stability.
  - Mitigation: lock MVP keep/defer/cut list and enforce weekly gate criteria.

## 8) First 5 Immediate Tasks

1. Create Godot project files under `godot/` and open in editor.
2. Implement `BattleController.cs` with grid, occupancy, and active unit handling.
3. Implement `TurnManager.cs` with initiative and end-turn progression.
4. Add data loader for abilities/spells/items from exported JSON.
5. Build a single test encounter scene with 2 players vs 2 enemies.

## 9) Definition of Done for First Steam-Sellable Build

- A new player can complete a full run loop (explore -> combat -> loot -> progression -> map transition) without external tools.
- No critical progression blockers after 10 full internal test runs.
- Save/load, settings, and exits are reliable.
- Steam branch can be installed and played on a separate machine.
- Store page, trailer, screenshots, and copy are ready for release.
