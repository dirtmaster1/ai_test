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

For level-up layout only, run
`godot --headless --path . --script res://tests/action_feedback_test.gd -- --level-up-only`.
This waits for container layout to settle, checks the centered dialog's narrative,
separate static bonus lines, section headings, portrait badge bounds, and stat
controls, then allocates three points in one open dialog. The panel uses a
`CenterContainer` to track content size; do not freeze a wrapped label's
pre-layout minimum into panel offsets.
Set `LEVEL_UP_SCREENSHOT` to a PNG output path and omit `--headless` to capture it.
On Windows, use Godot's `_console.exe` runner and wait for the explicit `PASS` and
exit code; starting the GUI executable alone does not verify test completion.

## Named save/load regression

The Utility Save button opens a card picker for up to ten named manual saves.
Select **New Save** to enable its inline name field, enter a name on the card,
and click **Save**, or select an existing card to edit its name inline and confirm
its overwrite. There is no separate footer name field or helper text. Cards
display the save name, map, and local timestamp in a 12-hour `hh:mm AM/PM`
format, without a slot-number or "Saved" prefix. To the right, saved party
members have their party-list portrait with a `Level N` label above it;
portraits are horizontally aligned, non-interactive, and limited to five.
The Delete action is vertically centered at the far-right edge, with the
portraits immediately to its left.
Both the title screen and Utility Load button open the same load-only picker
with a **Load** action. Cancel/Escape closes the picker without writing.
Cards form a single vertical list sorted newest first, including Auto-Save.
Legacy or damaged saves use their file modification time for ordering.
New Save stays at the top in Save mode. The centered, content-sized menu is
at most 700 pixels wide and shows up to five cards (fewer on short viewports),
then scrolls vertically without increasing its height. Empty/short lists shrink
to fit; the named-save counter and introductory instruction text are omitted.
**Settings > Main Menu** returns directly to the opening New Game/Load screen,
using the normal exit autosave without overwriting named saves.
Named-save cards have a **Delete** button in both menus. Deletion requires
confirmation, removes only that slot, refreshes the list and selection, and
immediately makes space for a new save. The **Auto-Save** card has no Delete
button. Damaged named saves can be deleted without loading them.
Confirmation prompts center their message and action buttons, use compact
content-based sizing, and keep the buttons together with a small gap.
The opening main menu also has no autosave deletion option.

Named saves live in `user://saves/slot-01.json` through `slot-10.json`.
Autosaving continues separately in `user://dark_dungeon_tactics_save.json`;
the pre-combat recovery checkpoint remains internal to defeat recovery and is
not listed in the Load picker. There is only one automatic card, **Auto-Save**,
which does not consume any of the ten manual slots. The old
single-file save remains available as **Auto-Save**, with its file modification
time used if it predates timestamp metadata. New Game never deletes named saves.
Loading switches adventures without autosaving the outgoing state over the
selected file. Corrupt saves are visibly disabled for loading, but can be
overwritten in Save mode. Writes use a temporary file before replacing a save.

After building, run:

```powershell
godot --headless --path . --script res://tests/save_slots_test.gd
```

The test uses uniquely named temporary saves, covers the ten-slot limit,
creation/overwrite, metadata, autosave isolation, disk round trips with unspent
level-up points, legacy/corrupt saves, inline editing, deletion/cancellation,
slot reuse, timestamp ordering, and both picker modes at 1080p and 720p.
Layout checks cover zero, one, five, six, and ten cards, enforce the five-card
height cap, and verify that scrolling reaches the last card.
Set `SAVE_MENU_SCREENSHOT` to an output path prefix and omit `--headless` to
capture the Save and Load menus. Also verify the actual title-screen and Utility
buttons, overwrite cancellation, and starting a New Game without losing slots.
`tests/save_menu_flow_test.gd` automates those complete scene transitions. Run it
only in an isolated project copy with `application/config/use_custom_user_dir`
enabled and `application/config/custom_user_dir_name` set to
`dark-dungeon-save-menu-test-<unique-id>`; its guard refuses the real save directory.

## Exit Gate

- 10 consecutive runs marked PASS.
- No unresolved blocker defects.
- Build remains clean after final fix set.
