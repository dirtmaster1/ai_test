extends SceneTree

var failure_count := 0


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    var tileset := load("res://assets/tilesets/map_markers_64_tileset.tres") as TileSet
    var atlas: TileSetAtlasSource
    for source_index in range(tileset.get_source_count()):
        var source := tileset.get_source(tileset.get_source_id(source_index)) as TileSetAtlasSource
        if source != null and source.texture.resource_path == "res://assets/tilesets/units_2_64.png":
            atlas = source
            break
    check(atlas != null, "Unit marker atlas must exist")
    if atlas == null:
        quit(1)
        return

    var expected_cells := {
        "wolf": Vector2i(0, 3), "dire-wolf": Vector2i(1, 4),
        "skeletal-archer": Vector2i(2, 3), "skeletal-knight": Vector2i(3, 3),
        "skeletal-elite-swordsman": Vector2i(4, 3),
        "orc-warrior": Vector2i(2, 4), "orc-archer": Vector2i(3, 4),
        "orc-chieftain": Vector2i(0, 5), "giant-rat": Vector2i(4, 4),
        "bandit-warrior": Vector2i(1, 5), "bandit-archer": Vector2i(2, 5),
        "bandit-thief": Vector2i(3, 5), "bandit-sorcerer": Vector2i(4, 5),
        "bandit-leader": Vector2i(5, 5), "cave-spider": Vector2i(0, 4),
        "spectre": Vector2i(5, 4),
    }
    var game_data := root.get_node("GameData")
    var unit_scene := load("res://scenes/Unit.tscn") as PackedScene
    var snapshots := {}
    for template_id in expected_cells:
        var cell: Vector2i = expected_cells[template_id]
        var marker := atlas.get_tile_data(cell, 0)
        check(marker.get_custom_data("marker_type") == "enemy_spawn", template_id + " marker type")
        check(marker.get_custom_data("template_id") == template_id, template_id + " marker template")
        var template: Dictionary = game_data.call("GetCharacterTemplate", template_id).duplicate(true)
        template["id"] = "forest-encounter-" + template_id + "-3-7"
        var unit := unit_scene.instantiate() as Node2D
        root.add_child(unit)
        unit.call("Setup", template)
        var expected_region := Rect2(Vector2(cell) * 64.0, Vector2(64, 64))
        var sprite := unit.get_node("Sprite2D") as Sprite2D
        var portrait := unit.call("GetTurnOrderIcon") as AtlasTexture
        check(sprite.region_rect == expected_region, template_id + " world sprite must match marker")
        check(portrait.region == expected_region, template_id + " portrait must match marker")
        check(unit.get("BaseUnarmedDamage") == template.get("base_unarmed_damage", 1), template_id + " damage must use its own template")
        check(unit.get("MaxHitPoints") == template["max_hit_points"], template_id + " HP must use its own template")
        snapshots[template_id] = unit.call("BuildRuntimeSnapshot")
        unit.call("ApplyRuntimeSnapshot", snapshots[template_id])
        check(sprite.region_rect == expected_region, template_id + " sprite must survive snapshot restoration")
        unit.free()
    check(snapshots["wolf"]["unit_id"] != snapshots["dire-wolf"]["unit_id"], "Wolf variants must have distinct unit IDs")
    var additional_cells := {
        "wizard": Vector2i(0, 0), "warrior": Vector2i(1, 0),
        "cleric": Vector2i(2, 0), "ranger": Vector2i(3, 0), "thief": Vector2i(4, 0),
        "goblin-warrior": Vector2i(3, 1), "goblin-archer": Vector2i(4, 1),
        "goblin-shaman": Vector2i(5, 1), "goblin-chieftain": Vector2i(0, 2),
        "goblin-brute": Vector2i(5, 2), "skeleton-warrior": Vector2i(1, 2),
        "skeleton-mage": Vector2i(2, 2), "giant-spider": Vector2i(1, 3),
        "zombie": Vector2i(3, 2), "ghoul": Vector2i(3, 2), "necromancer": Vector2i(4, 2),
    }
    for template_id in additional_cells:
        var unit := unit_scene.instantiate() as Node2D
        root.add_child(unit)
        unit.call("Setup", game_data.call("GetCharacterTemplate", template_id))
        var expected_region := Rect2(Vector2(additional_cells[template_id]) * 64.0, Vector2(64, 64))
        check((unit.get_node("Sprite2D") as Sprite2D).region_rect == expected_region, template_id + " sprite mapping")
        check((unit.call("GetTurnOrderIcon") as AtlasTexture).region == expected_region, template_id + " portrait mapping")
        unit.free()
    if failure_count == 0:
        print("PASS: all 32 atlas-based templates use expected sprites and portraits; new enemy markers, stats, and snapshots verified")
    quit(0 if failure_count == 0 else 1)


func check(condition: bool, message: String) -> void:
    if not condition:
        failure_count += 1
        push_error(message)