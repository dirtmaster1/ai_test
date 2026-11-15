extends SceneTree


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    var music := root.get_node("AudioManager/BackgroundMusicPlayer") as AudioStreamPlayer
    music.stop()
    music.stream = null
    var main := load("res://scenes/Main.tscn").instantiate() as Node
    var forest := main.get_node("Gameworld/Maps/forest-town-base") as TileMapLayer
    var missing := {}
    var sources := {}
    for cell in forest.get_used_cells():
        var source_id := forest.get_cell_source_id(cell)
        sources[source_id] = sources.get(source_id, 0) + 1
        if not forest.tile_set.has_source(source_id) or forest.get_cell_tile_data(cell) == null:
            var key := "%s:%s:%s" % [source_id, forest.get_cell_atlas_coords(cell), forest.get_cell_alternative_tile(cell)]
            missing[key] = missing.get(key, 0) + 1
    print("Forest terrain sources: ", sources)
    if not missing.is_empty():
        push_error("Unresolved forest terrain cells: %s" % missing)
        main.free()
        quit(1)
        return

    var screenshot := OS.get_environment("FOREST_TERRAIN_SCREENSHOT")
    if not screenshot.is_empty():
        if DisplayServer.get_name() == "headless":
            push_error("Forest terrain screenshot requires a renderer")
            main.free()
            quit(1)
            return
        forest.get_parent().remove_child(forest)
        forest.owner = null
        var viewport := SubViewport.new()
        viewport.size = Vector2i(1280, 960)
        viewport.world_2d = World2D.new()
        viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
        var world: Node2D = load("res://tests/WaterRippleChecks.cs").new()
        var maps := Node2D.new()
        maps.name = "Maps"
        world.add_child(maps)
        maps.add_child(forest)
        viewport.add_child(world)
        root.add_child(viewport)
        viewport.canvas_transform = Transform2D(Vector2(0.75, 0), Vector2(0, 0.75), Vector2(192, 0))
        await process_frame
        await RenderingServer.frame_post_draw
        var error := viewport.get_texture().get_image().save_png(screenshot)
        viewport.free()
        if error != OK:
            push_error("Could not save forest terrain screenshot: %s" % error)
            main.free()
            quit(1)
            return
    print("PASS: All authored forest terrain cells resolve in Main, including wall and floor atlas references")
    main.free()
    quit(0)
