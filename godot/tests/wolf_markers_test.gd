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
    await check_marker_transforms(unit_scene)
    if failure_count == 0:
        print("PASS: atlas templates, marker transforms, rendered pixels, duplicate suppression, and snapshots verified")
    quit(0 if failure_count == 0 else 1)


func check_marker_transforms(unit_scene: PackedScene) -> void:
    var tileset := TileSet.new()
    tileset.tile_size = Vector2i(64, 64)
    for data_name in ["marker_type", "template_id", "uses_tile_visual"]:
        var layer_index := tileset.get_custom_data_layers_count()
        tileset.add_custom_data_layer()
        tileset.set_custom_data_layer_name(layer_index, data_name)
        tileset.set_custom_data_layer_type(layer_index, TYPE_BOOL if data_name == "uses_tile_visual" else TYPE_STRING)
    var atlas := TileSetAtlasSource.new()
    atlas.texture = load("res://assets/tilesets/units_2_64.png")
    atlas.texture_region_size = Vector2i(64, 64)
    tileset.add_source(atlas, 0)
    var atlas_cell := Vector2i(1, 5)
    atlas.create_tile(atlas_cell)
    var markers := TileMapLayer.new()
    markers.name = "transform-test-markers"
    markers.tile_set = tileset
    for alternative_index in range(8):
        if alternative_index > 0:
            atlas.create_alternative_tile(atlas_cell, alternative_index)
        var data := atlas.get_tile_data(atlas_cell, alternative_index)
        data.set_custom_data("marker_type", "enemy_spawn")
        data.set_custom_data("template_id", "bandit-warrior")
        data.set_custom_data("uses_tile_visual", true)
        data.flip_h = (alternative_index & 1) != 0
        data.flip_v = (alternative_index & 2) != 0
        data.transpose = (alternative_index & 4) != 0
        for transform_index in range(8):
            var flags := 0
            if (transform_index & 1) != 0:
                flags |= TileSetAtlasSource.TRANSFORM_FLIP_H
            if (transform_index & 2) != 0:
                flags |= TileSetAtlasSource.TRANSFORM_FLIP_V
            if (transform_index & 4) != 0:
                flags |= TileSetAtlasSource.TRANSFORM_TRANSPOSE
            markers.set_cell(Vector2i(transform_index, alternative_index), 0, atlas_cell, alternative_index | flags)

    var viewport := SubViewport.new()
    viewport.size = Vector2i(1024, 512)
    viewport.world_2d = World2D.new()
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    viewport.canvas_item_default_texture_filter = Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST
    root.add_child(viewport)
    var maps := Node2D.new()
    maps.name = "Maps"
    viewport.add_child(maps)
    var base_layer := TileMapLayer.new()
    base_layer.name = "transform-test-base"
    maps.add_child(base_layer)
    maps.add_child(markers)
    var loader := load("res://scripts/MapLoader.cs").new() as Node
    viewport.add_child(loader)
    var map_data: Dictionary = loader.call("LoadMapStub", "transform-test")
    var visuals := maps.get_node("transform-test-item-visuals") as TileMapLayer
    check(visuals.get_used_cells().is_empty(), "Enemy markers must not leave static duplicates when uses_tile_visual is true")
    var count := 0
    for encounter in map_data["encounters"]:
        for config in encounter["enemies"]:
            count += 1
            var unit := unit_scene.instantiate() as Node2D
            viewport.add_child(unit)
            unit.call("Setup", config)
            var sprite := unit.get_node("Sprite2D") as Sprite2D
            var expected_transform := sprite.transform
            var snapshot: Dictionary = unit.call("BuildRuntimeSnapshot")
            unit.call("Setup", {"id": "bandit-warrior"})
            check(sprite.transform.is_equal_approx(Transform2D.IDENTITY), "Setup must reset the previous orientation")
            unit.call("ApplyRuntimeSnapshot", snapshot)
            check(sprite.transform.is_equal_approx(expected_transform), "Snapshot must restore sprite orientation")
            var legacy_snapshot := snapshot.duplicate(true)
            for key in ["sprite_flip_h", "sprite_flip_v", "sprite_transpose"]:
                legacy_snapshot.erase(key)
            unit.call("ApplyRuntimeSnapshot", legacy_snapshot)
            check(sprite.transform.is_equal_approx(expected_transform), "Old snapshots must preserve configured orientation")
            unit.call("SetGridPos", config["grid_pos"] + Vector2i(8, 0))
            check(sprite.transform.is_equal_approx(expected_transform), "Movement must preserve sprite orientation")
            sprite.modulate = Color.WHITE
    check(count == 64, "All eight cell transforms combined with eight alternative transforms must spawn")
    markers.visible = true
    await RenderingServer.frame_post_draw
    var image := viewport.get_texture().get_image()
    for alternative_index in range(8):
        for transform_index in range(8):
            var mismatches := 0
            var opaque_pixels := 0
            for pixel_y in range(64):
                for pixel_x in range(64):
                    var position := Vector2i(transform_index * 64 + pixel_x, alternative_index * 64 + pixel_y)
                    var expected := image.get_pixelv(position)
                    var actual := image.get_pixelv(position + Vector2i(512, 0))
                    if expected.r + expected.g + expected.b > 0.2:
                        opaque_pixels += 1
                    if not expected.is_equal_approx(actual):
                        mismatches += 1
            check(opaque_pixels > 100, "Reference tile must render visible artwork")
            check(mismatches == 0, "Unit pixels must match tile: alternative=%d transform=%d mismatches=%d" % [alternative_index, transform_index, mismatches])
    viewport.queue_free()
    await process_frame


func check(condition: bool, message: String) -> void:
    if not condition:
        failure_count += 1
        push_error(message)