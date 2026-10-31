---
name: godot-gameplay
description: "Use when launching, navigating, or interacting with Dark Dungeon Tactics in Godot, including starting a new game, moving the party, talking to NPCs, and inspecting gameplay."
---

# Godot Gameplay

Use this workflow to play Dark Dungeon Tactics through its running Godot game, rather than inferring gameplay from code alone.

## Launch and Start

1. Use the Godot project at `godot/` in this workspace. Check whether its game window is already running before launching another instance.
2. By default, start a **New Game**, not a saved game, unless the user explicitly asks to continue or load one.
3. Before selecting New Game, check for the autosave at `user://dark_dungeon_tactics_save.json` and recovery checkpoint at `user://dark_dungeon_tactics_recovery.json`. Starting a new adventure replaces automatic progress. Preserve existing automatic saves with separate backups before proceeding; do not discard or overwrite those backups. Named saves in `user://saves/slot-01.json` through `slot-10.json` are not changed by New Game or autosaving.
4. Capture the game screen after launch and confirm the current screen before sending input. Use Godot debug output to detect errors and verify scene changes.

## Observe and Control

- Base all mouse coordinates on a fresh screenshot. After each meaningful input, wait for movement or scene transitions to finish, then capture the screen again.
- In this Windows workspace, bring the specific `Dark Dungeon Tactics (DEBUG)` window to the foreground immediately before sending input. Focus can return to VS Code between tool calls, so activate and send the input in the same operation.
- If the game cannot be brought into focus or its screen cannot be observed, do not claim to have played it; ask the user for a screenshot or a way to control the running instance.
- Press `H` to open the in-game help and confirm controls when needed.

## Exploration

- Move the party one grid step at a time with `W`, `A`, `S`, `D` or the arrow keys.
- Clicking a reachable floor tile starts party movement along a path. Wait for it to complete before issuing another move.
- To interact with an NPC or object, the explorer must be within one tile (Chebyshev distance) of its map cell. Click the target cell; if too far away, move to a reachable adjacent floor cell first, then click the target again.
- On a fresh `forest-town` game, the warrior starts at `(9, 9)`, Gord is at `(1, 8)`, and Milo the Vendor is at `(9, 10)`. Gord is west of the starting room: move west, click the west door when adjacent to open it, then follow the corridor and click Gord when adjacent. Verify the dialogue title because the nearby NPC is Milo.
- NPC dialogue can offer state-changing choices such as recruiting someone. Recruiting Gord with available party capacity adds him immediately and closes the dialogue; it does not open a confirmation menu. If the party is full, recruitment may instead ask whom to move to reserves. Read the dialogue and choices, but do not recruit, replace party members, spend items, or otherwise make consequential choices unless the user asked for that outcome.
- Click a party card to select a member. `P` toggles Party/Inventory, `R` toggles Reserves, `H` toggles Help, and `Tab` cycles inventory targets in the applicable UI.

## Persistence and Reporting

- Do not press Save unless the user asks to save progress.
- Utility Save opens the ten-slot card picker: select New Save and enter a name, or select an existing card to overwrite, then press Save. Overwriting requires confirmation. Both title-screen Load and Utility Load open the same load-only picker; select the requested card and press Load. Auto-Save is the only automatic card outside the ten named slots; the internal recovery checkpoint is available only through defeat recovery, not the Load picker. Cancel or Escape dismisses the picker without writing.
- Report which game state was used (new or saved), what actions completed, and any blockers or errors. Distinguish visible observations from conclusions inferred from source code.
- If an interaction is unavailable because the NPC is already recruited or absent, report that state and ask whether to continue elsewhere rather than treating a different character as the target.
