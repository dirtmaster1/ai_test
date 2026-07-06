# TileMap Workflow (64x64)

This project now uses one base TileMapLayer per map for floor/wall/door geometry.

## Layer Naming Convention

For each map id (example: `map-a`), create one layer under `Maps` in `TestEncounter2v2.tscn`:

- `map-a-base`
- `map-b-base`

Only the active base layer is shown at runtime.

## Tile Size Convention

Use `64x64` tiles in your TileSet atlases (`.png`).

Recommended folder layout:

- `assets/tilesets/terrain_64.png`
- `assets/tilesets/units_2_64.png`

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

## Current Compatibility

If base layers are not painted yet, old hardcoded map stubs are still used as fallback.
