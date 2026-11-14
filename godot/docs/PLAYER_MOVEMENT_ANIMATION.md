# Player movement animation

Player animation sheets in `assets/animations/player/` use 64-by-64 cells.
Frames run across columns and direction rows are ordered north, east, south,
west. Diagonal travel uses the nearest cardinal cycle (horizontal wins ties).

## Five-class directional animation

Warrior, wizard, ranger, cleric, and thief use 512-by-256 walk sheets (eight frames per direction) and
64-by-256 idle sheets (one resting pose per direction). They start facing south
and retain their last travel direction when stopping. Custom sprite overrides
and enemy sprites still take precedence over player animation.

The source poses and cycles in `art/player_walk_prototype/` were generated with
RetroDiffusion's advanced rotation and walking models from the existing class
art, with transparent indexed-color reference preparation. These are generated
prototype frames, not hand-authored final artwork. Each source walk sheet is a
4-by-2 grid. Packing applies the same nearest-neighbor 62-by-62 fit to every
walk and resting pose, leaving a one-pixel transparent gutter within each
64-by-64 cell. No per-frame cropping or recentering is used. The packed resting
pose is the neutral first frame of each generated cycle, so its palette and
registration match the walking sprite exactly. The source `*-idle.png` images
are the reference inputs used to generate those cycles.
To repack the checked-in sources without an API call:

```powershell
.\tools\pack_player_walk_prototype.ps1
```

Pass `-Classes ranger,cleric,thief` to repack only selected classes. The original
`.\tools\generate_player_walk_sheets.ps1` entry point now delegates to this packer;
it no longer recreates the obsolete four-frame shifted-limb animations.

`Unit.SetWalkPosition` advances the cycle by actual travel distance, including
the easing used by combat/charge tweens. Cycle distances scale with multi-cell
footprints. At the unchanged cardinal exploration speed:

| Class | Full cycle distance | Cycle duration | Gait |
| --- | --- | --- | --- |
| Warrior | 256 pixels | 0.56 s | Heavy steps, controlled armor and axe |
| Wizard | 320 pixels | 0.70 s | Short measured steps, trailing robe |
| Ranger | 224 pixels | 0.49 s | Light purposeful steps, steady bow and quiver |
| Cleric | 288 pixels | 0.63 s | Upright measured steps, restrained cloak and staff |
| Thief | 208 pixels | 0.455 s | Compact agile steps, lowered stance and controlled dagger |

This deliberately keeps the existing fast traversal speed; it is not physically exact foot locking.
Time without displacement does not advance the feet.

An 80 ms visual-only stop grace period preserves the pose and gait phase across
consecutive tile tweens and turns. After a real stop the matching directional
idle is restored. This does not delay movement completion, input, grid updates,
traps, fog, or charge resolution. Zero-displacement party followers do not
start walking. Nearest filtering is applied only to the prototype sprites.

After building, run the targeted checks with Godot's .NET executable:

```powershell
godot --headless --path . --script tests/player_walk_animation_test.gd
```

The checks cover all direction rows, eight unique frames per row, transparent
frame borders, idle/facing retention, tile handoffs, distance phase at 30/60/120
FPS, actual combat/charge tweens, multi-tile exploration paths through corners,
and static-fallback/custom/enemy sprite behavior for all five classes.

`Unit` plays the matching cycle while a player moves. This is wired into player combat steps,
charge movement, and exploration party steps. Exploration movement-key repeats
use the same animated step duration as mouse click-to-move. Enemy units retain
their existing movement visuals.
