# Player movement animation

The five current player classes have 4-frame walk cycles in
`assets/animations/player/`. Each 256-by-256 sheet uses 64-by-64 cells, with frames
in columns and direction rows ordered north, east, south, west. Diagonal travel
uses the nearest cardinal cycle (the horizontal cycle wins ties).

Regenerate the sheets from the player poses in `assets/tilesets/units_2_64.png`
with:

```powershell
.\tools\generate_player_walk_sheets.ps1
```

The generator preserves the original pose underneath each shifted limb region,
so movement cannot expose transparent gaps in the character. These are first-pass
derived frames; replacing them with hand-authored pixel-art frames will give the
limbs more natural joint movement without changing the runtime animation support.

`Unit` plays the matching cycle while a player moves and restores the original
static atlas pose when movement ends. This is wired into player combat steps,
charge movement, and exploration party steps. Exploration movement-key repeats
use the same animated step duration as mouse click-to-move. Enemy units retain
their existing movement visuals.
