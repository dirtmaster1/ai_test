extends SceneTree

var failure_count := 0


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    if DisplayServer.get_name() == "headless":
        push_error("Water ripple coverage requires a renderer; run without --headless.")
        quit(1)
        return

    var viewport := SubViewport.new()
    viewport.size = Vector2i(512, 384)
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    viewport.world_2d = World2D.new()
    root.add_child(viewport)

    var main: Node = load("res://scenes/Main.tscn").instantiate()
    var forest := main.get_node("Gameworld/Maps/forest-town-base") as TileMapLayer
    var layer := TileMapLayer.new()
    layer.tile_set = forest.tile_set
    layer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
    var material := forest.material.duplicate() as ShaderMaterial
    check_wave_frames(material.get_shader_parameter("water_frames") as Texture2D)
    var shader := Shader.new()
    shader.code = material.shader.code.replace("void fragment()", "uniform float water_test_time;\nvoid fragment()").replace("TIME", "water_test_time")
    material.shader = shader
    layer.material = material
    main.free()
    var world: Node2D = load("res://tests/WaterRippleChecks.cs").new()
    var maps := Node2D.new()
    maps.name = "Maps"
    layer.name = "forest-town-base"
    world.add_child(maps)
    maps.add_child(layer)
    viewport.add_child(world)

    for y in range(4):
        for x in range(6):
            layer.set_cell(Vector2i(x, y), 3, Vector2i(2, 1))
    # Include mixed shoreline/bridge tiles and grass to check the water mask.
    for x in range(6):
        layer.set_cell(Vector2i(x, 4), 3, Vector2i(x + 1, 2))
        layer.set_cell(Vector2i(x, 5), 6, Vector2i(2, 1))

    viewport.canvas_transform = Transform2D.IDENTITY
    var strength: float = material.get_shader_parameter("blend_strength")
    material.set_shader_parameter("blend_strength", 0.0)
    var original := await capture(viewport, material, 0.0)
    material.set_shader_parameter("blend_strength", strength)
    var frames: Array[Image] = []
    for frame in range(21):
        frames.append(await capture(viewport, material, frame * 0.2))

    var min_coverage := 1.0
    var min_short_coverage := 1.0
    for tile_y in range(4):
        for tile_x in range(6):
            for quadrant_y in range(2):
                for quadrant_x in range(2):
                    var changed := 0
                    var short_changed := 0
                    for y in range(24):
                        for x in range(24):
                            var pixel := Vector2i(tile_x * 64 + 8 + quadrant_x * 24 + x, tile_y * 64 + 8 + quadrant_y * 24 + y)
                            for frame in range(1, frames.size()):
                                if color_difference(frames[0].get_pixelv(pixel), frames[frame].get_pixelv(pixel)) > 1.0 / 255.0:
                                    changed += 1
                                    if frame <= 4:
                                        short_changed += 1
                                    break
                    min_coverage = minf(min_coverage, changed / 576.0)
                    min_short_coverage = minf(min_short_coverage, short_changed / 576.0)
    check(min_short_coverage >= 0.6, "Every water tile interior quadrant must animate at least 60%% of pixels within 0.8 seconds; minimum %.1f%%" % (min_short_coverage * 100.0))
    check(min_coverage >= 0.95, "Every water tile interior quadrant must animate at least 95%% of pixels within 4 seconds; minimum %.1f%%" % (min_coverage * 100.0))

    var non_water_changes := 0
    for y in range(256, 384):
        for x in range(384):
            var color := original.get_pixel(x, y)
            if color.b >= 0.025 and color.g >= color.r * 1.35 and color.b >= color.g * 1.2:
                continue
            for frame in frames:
                if color_difference(color, frame.get_pixel(x, y)) > 1.0 / 255.0:
                    non_water_changes += 1
                    break
    check(non_water_changes == 0, "Shore, bridge and grass pixels must remain unchanged")

    var between_frames := await capture(viewport, material, 0.1)
    var moving_pixels := 0
    for y in range(8, 248):
        for x in range(8, 376):
            if color_difference(frames[0].get_pixel(x, y), between_frames.get_pixel(x, y)) > 1.0 / 255.0:
                moving_pixels += 1
    check(moving_pixels > 5000, "Ripples must travel between atlas frame changes, not just flicker at 5 FPS")

    for zoom in [0.75, 1.5]:
        var origin: Vector2 = Vector2(13.5, 19.5) - Vector2.ONE * zoom * 0.5
        var transform := Transform2D(Vector2(zoom, 0), Vector2(0, zoom), origin)
        viewport.canvas_transform = transform
        var moved := await capture(viewport, material, 0.0)
        var mismatches := 0
        for y in range(16, 144, 4):
            for x in range(16, 240, 4):
                var screen_pixel := Vector2i(transform * Vector2(x + 0.5, y + 0.5))
                if color_difference(moved.get_pixelv(screen_pixel), frames[0].get_pixel(x, y)) > 2.0 / 255.0:
                    mismatches += 1
        check(mismatches == 0, "Ripples must stay world-anchored when panning and zooming at %s" % zoom)

    viewport.canvas_transform = Transform2D.IDENTITY
    for zoom in [0.75, 1.0, 1.5]:
        for frame in range(1, 6):
            var origin: Vector2 = Vector2(13.5 + frame * 10, 19.5 + frame * 4) - Vector2.ONE * zoom * 0.5
            world.transform = Transform2D(Vector2(zoom, 0), Vector2(0, zoom), origin)
            var moved := await capture(viewport, material, frame * 0.2)
            var mismatches := 0
            for y in range(16, 144, 4):
                for x in range(16, 240, 4):
                    var screen_pixel := Vector2i(world.transform * Vector2(x + 0.5, y + 0.5))
                    if color_difference(moved.get_pixelv(screen_pixel), frames[frame].get_pixel(x, y)) > 2.0 / 255.0:
                        mismatches += 1
            check(mismatches == 0, "Party-follow movement must not alter water phase/speed at zoom %s, frame %s (%s mismatches)" % [zoom, frame, mismatches])

    world.transform = Transform2D.IDENTITY
    var tween := world.create_tween()
    tween.tween_property(world, "position", Vector2(64, -32), 0.14)
    var tween_frames := 0
    while tween.is_running():
        await process_frame
        await RenderingServer.frame_post_draw
        var expected := layer.get_global_transform_with_canvas().affine_inverse()
        var actual := Transform2D(
            material.get_shader_parameter("canvas_to_world_x"),
            material.get_shader_parameter("canvas_to_world_y"),
            material.get_shader_parameter("canvas_to_world_origin"))
        check(actual.is_equal_approx(expected), "Water transform must match this rendered frame, after party-follow tweens")
        tween_frames += 1
    check(tween_frames > 1, "Party-follow tween must be checked across multiple rendered frames")

    var screenshot := OS.get_environment("WATER_RIPPLE_SCREENSHOT")
    if not screenshot.is_empty():
        check(frames[0].save_png(screenshot) == OK, "Could not save water screenshot")
    viewport.queue_free()
    if failure_count == 0:
        print("PASS: water interior coverage %.1f%% at 0.8s / %.1f%% at 4s; seamless frames, continuous motion, terrain mask, pan/zoom and party-follow tween (%d frames)" % [min_short_coverage * 100.0, min_coverage * 100.0, tween_frames])
    quit(0 if failure_count == 0 else 1)


func capture(viewport: SubViewport, material: ShaderMaterial, time: float) -> Image:
    material.set_shader_parameter("water_test_time", time)
    await process_frame
    await RenderingServer.frame_post_draw
    return viewport.get_texture().get_image()


func color_difference(first: Color, second: Color) -> float:
    return maxf(absf(first.r - second.r), maxf(absf(first.g - second.g), absf(first.b - second.b)))


func check_wave_frames(texture: Texture2D) -> void:
    var image := texture.get_image()
    check(image.get_size() == Vector2i(256, 64), "Water atlas must contain four 64x64 frames")
    if image.get_size() != Vector2i(256, 64):
        return
    var edge_variation := 0.0
    var interior_variation := 0.0
    var seam_errors := 0
    for y in range(64):
        for x in range(64):
            var variation := 0.0
            for frame in range(4):
                variation = maxf(variation, color_difference(image.get_pixel(x, y), image.get_pixel(frame * 64 + x, y)))
                if x == 0 and image.get_pixel(frame * 64, y) != image.get_pixel(frame * 64 + 63, y):
                    seam_errors += 1
                if y == 0 and image.get_pixel(frame * 64 + x, 0) != image.get_pixel(frame * 64 + x, 63):
                    seam_errors += 1
            if x < 8 or x >= 56 or y < 8 or y >= 56:
                edge_variation += variation
            else:
                interior_variation += variation
    check(seam_errors == 0, "All ripple frames must wrap seamlessly on both axes")
    check(edge_variation / 1792.0 <= 2.0 * interior_variation / 2304.0, "Ripple frames must not concentrate animation in a flickering border")


func check(condition: bool, message: String) -> void:
    if not condition:
        failure_count += 1
        push_error(message)
