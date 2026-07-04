# TileMap Workflow (64x64)

This project now supports map authoring from Godot TileMapLayer nodes.

## Layer Naming Convention

For each map id (example: `map-a`), create layers under `Maps` in `TestEncounter2v2.tscn`:

- `map-a` or `map-a-visual`: Visual tiles only (floor/walls/decor)
- `map-a-collision`: Painted cells are blocked movement cells
- `map-a-markers`: Marker tiles with custom data for transitions, props, and spawns

Only the active map visual layer is shown at runtime.

## Tile Size Convention

Use `64x64` tiles in your TileSet atlases (`.png`).

Recommended folder layout:

- `assets/tilesets/terrain_64.png`
- `assets/tilesets/props_64.png`
- `assets/tilesets/units_64.png`

## Marker Tile Custom Data

On marker tiles in `*-markers`, define custom data keys in the TileSet.

### Transition Marker

- `marker_type`: `transition`
- `to_map`: target map id (example: `map-b`)
- `spawn_x`: target x cell in destination map
- `spawn_y`: target y cell in destination map

### Player Spawn Marker

- `marker_type`: `player_spawn`
- `party_slot` (optional): 0-based party index. If omitted, treated as party leader anchor.

### Enemy Spawn Marker

- `marker_type`: `enemy_spawn`
- `encounter_id`: encounter group id (example: `encounter-a`)
- `aggro_range` (optional): integer, default 4
- `id` (optional): enemy unit id
- `name` (optional): enemy display name
- `primary_ability_id` (optional): default `melee`
- `initiative` (optional): default 10
- `hit_points` (optional): default 8
- `max_hit_points` (optional): defaults to `hit_points`
- `starting_equipment` (optional): comma-separated item ids or array

### Interactable Marker (props/chest/sign/npc/trap)

- `marker_type`: one of `prop`, `chest`, `sign`, `npc`, `trap`
- `id` (optional): unique id
- `name` (optional): display name
- `interaction_text` (optional): inspect text for non-loot props
- `loot_item_ids` (optional): comma-separated item ids or array
- `loot_rolls_min` (optional): default 1
- `loot_rolls_max` (optional): default = min

Loot keys make it a lootable prop. Without loot keys, it becomes inspect-only.

## Authoring Order

1. Paint visuals in `map-id` (or `map-id-visual`).
2. Paint blocked cells in `map-id-collision`.
3. Paint marker tiles in `map-id-markers`.
4. Set tile custom data in the TileSet inspector.
5. Play and verify movement, transitions, and interactions.

## Current Compatibility

If no marker/collision layers are painted yet, the old hardcoded map stubs are still used as fallback.
