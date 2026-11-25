extends SceneTree

var failure_count := 0


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    var audio_manager := root.get_node_or_null("AudioManager")
    var music_player := root.get_node_or_null("AudioManager/BackgroundMusicPlayer") as AudioStreamPlayer
    if music_player != null:
        music_player.stop()
        music_player.stream = null
    if audio_manager != null:
        audio_manager.free()

    var tileset := load("res://assets/tilesets/map_markers_64_tileset.tres") as TileSet
    check(tileset != null, "Map marker tile set must load")
    if tileset == null:
        quit(1)
        return

    tileset = tileset.duplicate(true) as TileSet
    var fireSource := tileset.get_source(1) as TileSetAtlasSource
    var torchData := fireSource.get_tile_data(Vector2i(3, 1), 0) if fireSource != null else null
    var firePitData := fireSource.get_tile_data(Vector2i(4, 1), 0) if fireSource != null else null
    check(torchData != null and firePitData != null, "Torch and Fire Pit tile data must load")
    if torchData != null:
        torchData.set_custom_data("marker_type", "cosmetic")
        torchData.set_custom_data("percent_transparency", 50)
    if firePitData != null:
        firePitData.set_custom_data("marker_type", "light_source")
        firePitData.set_custom_data("percent_transparency", 25)
    var invisibleMarkerData := fireSource.get_tile_data(Vector2i(1, 2), 0) if fireSource != null else null
    if invisibleMarkerData != null:
        invisibleMarkerData.set_custom_data("percent_transparency", 100)

    var scene_root := Node2D.new()
    var maps := Node2D.new()
    maps.name = "Maps"
    scene_root.add_child(maps)

    var markers := TileMapLayer.new()
    markers.name = "campfire-markers"
    markers.tile_set = tileset
    markers.set_cell(Vector2i(1, 2), 1, Vector2i(2, 3))
    markers.set_cell(Vector2i(2, 2), 1, Vector2i(3, 1))
    markers.set_cell(Vector2i(3, 2), 1, Vector2i(4, 1))
    markers.set_cell(Vector2i(4, 2), 1, Vector2i(0, 2))
    markers.set_cell(Vector2i(5, 2), 1, Vector2i(1, 2))
    maps.add_child(markers)

    var loader = load("res://scripts/MapLoader.cs").new()
    loader.set("MapsRootPath", NodePath("../Maps"))
    scene_root.add_child(loader)
    root.add_child(scene_root)
    await process_frame

    var visuals := maps.get_node_or_null("campfire-item-visuals") as TileMapLayer
    check(visuals != null, "Map loader must build the Campfire visual layer")
    if visuals != null:
        visuals.visible = true
        var base := visuals.get_node_or_null("CampfireBase_1_2") as Sprite2D
        var flame := visuals.get_node_or_null("CampfireFlame_1_2") as AnimatedSprite2D
        check(base != null and base.texture != null, "Campfire must keep its static base")
        check(flame != null, "Campfire must have a flame sprite")
        if flame != null and flame.sprite_frames != null:
            check(flame.sprite_frames.get_frame_count("flicker") == 8, "Campfire flame must use all eight Retro Diffusion frames")
            check(flame.is_playing(), "Campfire flame animation must start automatically")
            var initial_frame := flame.frame
            await create_timer(0.35).timeout
            check(flame.frame != initial_frame, "Campfire flame frame must advance")

    check(markers.get_cell_source_id(Vector2i(1, 2)) == 1, "Campfire interaction marker must remain intact")
    check(markers.get_cell_source_id(Vector2i(2, 2)) == 1, "Torch marker must remain intact")
    check(markers.get_cell_source_id(Vector2i(3, 2)) == 1, "Fire Pit marker must remain intact")
    check(
        visuals != null
            and visuals.get_cell_source_id(Vector2i(4, 2)) == 1
            and visuals.get_cell_atlas_coords(Vector2i(4, 2)) == Vector2i(0, 2),
        "Unrelated cosmetic markers must keep their original tile visuals")
    if visuals != null:
        var defaultOpacityData := visuals.get_cell_tile_data(Vector2i(4, 2))
        var invisibleOpacityData := visuals.get_cell_tile_data(Vector2i(5, 2))
        check(defaultOpacityData != null and is_equal_approx(defaultOpacityData.modulate.a, 1.0),
            "Unset percent_transparency must leave regular marker visuals fully opaque")
        check(invisibleOpacityData != null and is_zero_approx(invisibleOpacityData.modulate.a),
            "percent_transparency 100 must make regular marker visuals fully transparent")

    if visuals != null:
        var torchBase := visuals.get_node_or_null("TorchBase_2_2") as Sprite2D
        var torchFlame := visuals.get_node_or_null("TorchFlame_2_2") as AnimatedSprite2D
        check(torchBase != null and torchBase.texture != null, "Torch must keep its static prop base")
        check(torchFlame != null, "Torch must have a flame sprite")
        check(torchBase != null and is_equal_approx(torchBase.modulate.a, 0.5),
            "Cosmetic Torch base must respect percent_transparency")
        check(torchFlame != null and is_equal_approx(torchFlame.modulate.a, 0.5),
            "Cosmetic Torch flame must respect percent_transparency")
        if torchFlame != null and torchFlame.sprite_frames != null:
            check(torchFlame.sprite_frames.get_frame_count("flicker") == 8, "Torch must use all eight Retro Diffusion frames")
            check(torchFlame.is_playing(), "Torch flame animation must start automatically")
            var initialTorchFrame := torchFlame.frame
            await create_timer(0.35).timeout
            check(torchFlame.frame != initialTorchFrame, "Torch flame frame must advance")

        var firePitBase := visuals.get_node_or_null("FirePitBase_3_2") as Sprite2D
        var firePitFlame := visuals.get_node_or_null("FirePitFlame_3_2") as AnimatedSprite2D
        check(firePitBase != null and firePitBase.texture != null, "Fire Pit must keep its static prop base")
        check(firePitFlame != null, "Fire Pit must have a flame sprite")
        check(firePitBase != null and is_equal_approx(firePitBase.modulate.a, 0.75),
            "Light-source Fire Pit base must respect percent_transparency")
        check(firePitFlame != null and is_equal_approx(firePitFlame.modulate.a, 0.75),
            "Light-source Fire Pit flame must respect percent_transparency")
        if firePitFlame != null and firePitFlame.sprite_frames != null:
            check(firePitFlame.sprite_frames.get_frame_count("flicker") == 8, "Fire Pit must use all eight Retro Diffusion frames")
            check(firePitFlame.is_playing(), "Fire Pit flame animation must start automatically")
            var initialFirePitFrame := firePitFlame.frame
            await create_timer(0.35).timeout
            check(firePitFlame.frame != initialFirePitFrame, "Fire Pit flame frame must advance")

    if failure_count == 0:
        print("PASS: Campfire and both cosmetic/light-source Torch and Fire Pit markers keep their map cells and display eight-frame flames")
    scene_root.free()
    await process_frame
    quit(0 if failure_count == 0 else 1)


func check(condition: bool, message: String) -> void:
    if not condition:
        failure_count += 1
        push_error(message)
