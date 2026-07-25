# TileMap Workflow (64x64)

This project now uses one base TileMapLayer per map for floor/wall/door geometry.

## Layer Naming Convention

For each map id (example: `map-a`), create one layer under `Maps` in `Gameworld.tscn`:

- `map-a-base`
- `map-a-markers`

Only the active base layer and its authored item visuals are shown at runtime. Unit, spawn, and transition marker tiles remain hidden.

## Tile Size Convention

Use `64x64` tiles in your TileSet atlases (`.png`).

Recommended folder layout:

- `assets/tilesets/dungeon_terrain_64.png`
- `assets/tilesets/forest_terrain_64.png`
- `assets/tilesets/units_2_64.png`

Each `TokenMapDef` can configure its own terrain atlas and atlas cells with `Terrain = new MapTerrainDef { ... }`. If omitted, the map uses the dungeon terrain defaults.

## Base Layer Tile Custom Data

On tiles in each `*-base` layer, set custom data keys in the TileSet.

### `terrain_type` (required)

- `floor`: walkable, does not block line of sight
- `wall`: not walkable, blocks line of sight
- `door`: starts closed (not walkable, blocks line of sight)

### `door_id` (optional, door tiles only)

- Unique string id for persistence (example: `map-a-east-door`)
- If omitted, the game auto-generates an id from map and cell coordinates

### Door interaction behavior

- Closed doors are impassable and block line of sight
- Clicking an adjacent closed door opens it
- Open doors become walkable and no longer block line of sight
- Open/closed door state is saved per map

## Authoring Order

1. Create `map-id-base` under `Maps`.
2. Paint floor, wall, and door tiles in that layer.
3. Set `terrain_type` on those tiles in the TileSet inspector.
4. For door tiles, optionally set `door_id`.
5. Play and verify movement, line of sight, transitions, and door interaction.

## Item And Prop Markers

The `map_markers_64_tileset.tres` TileSet contains both the unit marker atlas and the `general_items_64.png` atlas. Godot assigns one TileSet resource to a TileMapLayer, so both atlases are sources in that shared TileSet. Paint item source tiles directly onto a map's `*-markers` layer, such as `forest-town-markers`.

General item tiles use these custom data keys:

- `marker_type`: `chest`, `sign`, `trap`, or `cosmetic`
- `id`: optional stable persistence ID; otherwise generated from map, type, and cell
- `name`: display name used by interaction UI
- `interaction_text`: text shown when inspecting a non-loot item
- `loot_item_ids`: comma-separated item IDs for lootable items
- `loot_rolls_min` and `loot_rolls_max`: number of unique entries selected from the loot pool
- `gold_amount`: fixed amount shown with the chest contents and added to the party when looted
- `uses_tile_visual`: keeps the atlas tile as the runtime visual instead of drawing the legacy prop overlay

The item atlas includes ready-to-paint chest, signpost, trap, and cosmetic defaults. To give two placements different IDs, loot, or text, create tile alternatives in the TileSet editor and override their custom data, matching the existing transition-marker workflow.

At runtime, `MapLoader` reads item cells from the marker layer into map props. It also creates an item-only visual TileMapLayer so item art is visible while player, enemy, and transition authoring markers stay hidden. `cosmetic` cells are visual-only and are not added to interaction logic.

## Current Compatibility

If base layers are not painted yet, old hardcoded map stubs are still used as fallback.
