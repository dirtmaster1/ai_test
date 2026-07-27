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

## Party, Enemy, And NPC Markers

Paint party, enemy, and NPC markers from the `units_2_64.png` source onto the map's `*-markers` layer. These tiles are authoring markers: party and enemy markers are hidden after they create runtime units, while NPCs are rendered as interactable map props.

### Party spawn markers

Set these custom data keys on a tile or tile alternative:

- `marker_type`: `player_spawn`
- `party_slot`: zero-based index in `characters.default_party` from `resources/game_data.json`

The current default party uses slot `0` for the warrior, `1` for the cleric, `2` for the ranger, and `3` for the wizard. Paint one marker for each member you want to position. The tile's `template_id` and artwork do not choose the party member; `party_slot` controls that assignment.

If `party_slot` is omitted or negative, the marker becomes the fallback party-leader spawn. Any party member without an explicit slot marker is placed near that leader using the default formation offsets.

### Enemy spawn markers

Set these custom data keys on a tile or tile alternative:

- `marker_type`: `enemy_spawn`
- `template_id`: character template ID from `characters.templates` in `resources/game_data.json`
- `encounter_id`: shared ID used to group enemies into one encounter
- `aggro_range`: optional detection range; defaults to `4`
- `id`: optional stable enemy ID; otherwise generated from the map, encounter, template, and cell
- `name`, `primary_ability_id`, `initiative`, `hit_points`, and `max_hit_points`: optional per-marker overrides
- `starting_equipment`: optional comma-separated item IDs

Enemies with the same `encounter_id` enter combat and persist as one encounter. Use a unique encounter ID for a separate fight. Some unit atlas cells infer `template_id` from their artwork, but setting it explicitly on a tile alternative is safer for custom enemies.

### NPC markers

Set these custom data keys on a tile or tile alternative:

- `marker_type`: `npc`
- `id`: optional stable interaction ID; otherwise generated from the map and cell
- `name`: NPC name shown by the interaction UI
- `interaction_text`: text shown when the NPC is inspected
- `uses_tile_visual`: set to `true` to keep the authored atlas tile as the runtime visual

NPC markers create static interactable props, not combat units. They do not use party slots, initiative, HP, or encounter grouping.

### Placement steps

1. Select the map's `map-id-markers` layer.
2. Select the unit atlas source in `map_markers_64_tileset.tres`.
3. Paint the appropriate marker tile or configured tile alternative on a walkable cell.
4. For custom values, create a tile alternative and override its custom data instead of changing a shared base tile.
5. Run the map and verify party positions, encounter grouping and aggro, and NPC interaction text.

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
- `damage`: HP damage dealt when a `trap` is triggered
- `target_scope`: `triggering_unit` for the unit on the trap, or `party` for every living member of the triggering unit's team

The item atlas includes ready-to-paint chest, signpost, trap, and cosmetic defaults. To give two placements different IDs, loot, or text, create tile alternatives in the TileSet editor and override their custom data, matching the existing transition-marker workflow.

At runtime, `MapLoader` reads item cells from the marker layer into map props. It also creates an item-only visual TileMapLayer so item art is visible while player, enemy, and transition authoring markers stay hidden. `cosmetic` cells are visual-only and are not added to interaction logic.

## Current Compatibility

If base layers are not painted yet, old hardcoded map stubs are still used as fallback.
