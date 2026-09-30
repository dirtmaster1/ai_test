extends SceneTree

var failures := 0
var blended_frames := 0
var changed_samples := 0


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    Engine.max_fps = 120
    var viewport := SubViewport.new()
    viewport.size = Vector2i(1792, 640)
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    viewport.world_2d = World2D.new()
    root.add_child(viewport)
    var checks = load("res://tests/FogOfWarChecks.cs").new()
    viewport.add_child(checks)
    await RenderingServer.frame_post_draw

    var directions := [Vector2i.RIGHT, Vector2i.UP, Vector2i.LEFT]
    for direction in directions:
        var before: Dictionary = checks.call("FogState")
        var previous_image: Image = before.texture.get_image()
        checks.call("StartStep", direction)
        var destination: Dictionary = checks.call("FogState")
        var destination_image: Image = destination.texture.get_image()
        var texture_id: int = destination.texture.get_instance_id()
        check(is_zero_approx(destination.blend), "Click step must start with the previous mask")
        check(destination.leader_position == before.leader_position, "Step must not teleport the rendered leader")
        while checks.call("IsStepRunning"):
            await RenderingServer.frame_post_draw
            var state: Dictionary = checks.call("FogState")
            check(state.texture.get_instance_id() == texture_id, "Fog must not rebuild between cell changes")
            check(state.revealed_count == destination.revealed_count, "Animation frames must not recalculate revealed cells")
            check(state.rect == before.rect, "Fog bounds must stay fixed in world space")
            check(is_equal_approx(state.blend, state.drawn_blend), "Shader progress must not lag the movement tween")
            var expected_position: Vector2 = before.leader_position.lerp(Vector2(state.leader_cell) * 64.0 + Vector2(32, 32), state.blend)
            check(state.leader_position.distance_to(expected_position) < 0.05, "Fog blend must track the leader's tween")
            if state.blend > 0.0 and state.blend < 1.0:
                blended_frames += 1
            check_pixels(viewport, checks, state, previous_image, destination_image)
        check(checks.call("StepSucceeded"), "Animated step must finish successfully")
        check(is_equal_approx(checks.call("FogState").blend, 1.0), "Finished step must show only its destination mask")

    var click_result: PackedByteArray = checks.call("FogState").texture.get_image().get_data()
    checks.call("ResetScenario")
    for direction in directions:
        check(checks.call("MoveWithKeyboard", direction), "Equivalent keyboard step must succeed")
    var keyboard_state: Dictionary = checks.call("FogState")
    check(click_result == keyboard_state.texture.get_image().get_data(), "Click and keyboard paths must produce identical final fog")
    check(is_equal_approx(keyboard_state.blend, 1.0), "Keyboard movement must not retain a click blend")
    checks.call("ResetScenario")
    await RenderingServer.frame_post_draw
    var path: Array[Vector2i] = [Vector2i.RIGHT, Vector2i.RIGHT, Vector2i.UP, Vector2i.LEFT, Vector2i.DOWN, Vector2i.LEFT]
    checks.call("StartPath", path)
    var handoffs := 0
    var last_cell: Vector2i = checks.call("FogState").leader_cell
    while checks.call("IsStepRunning"):
        await RenderingServer.frame_post_draw
        var state: Dictionary = checks.call("FogState")
        if state.leader_cell != last_cell:
            handoffs += 1
            last_cell = state.leader_cell
        check_pixels(viewport, checks, state, state.previous_texture.get_image(), state.texture.get_image())
    check(checks.call("StepSucceeded"), "Continuous click path must finish successfully")
    check(handoffs == path.size() - 1, "Continuous path must check every step handoff")
    check(blended_frames > 1, "Fog must be verified across multiple intermediate frames")
    check(changed_samples > 0, "Rendered checks must cover cells whose visibility changes")
    var benchmark: Dictionary = checks.call("BenchmarkFogRebuild")
    check(benchmark.sha256 == "DFB84A781BC06C6B3C57EBAA08CF3449EECA582224F280377A41B5A6E19A7BAE", "Optimized fog must remain byte-identical to the original mask")
    print("Fog rebuild, 96x96 cells: median %.2f ms, max %.2f ms; SHA256 %s" % [benchmark.median_ms, benchmark.max_ms, benchmark.sha256])
    checks.free()
    viewport.queue_free()
    if failures == 0:
        print("PASS: world-aligned fog, cached masks, wall occlusion, tween synchronization, keyboard equivalence, and %d continuous step handoffs across %d blended frames" % [handoffs, blended_frames])
    quit(0 if failures == 0 else 1)


func check_pixels(viewport: SubViewport, checks: Node2D, state: Dictionary, previous: Image, current: Image) -> void:
    var rendered := viewport.get_texture().get_image()
    for pixel_y in range(2, current.get_height() - 2, 3):
        for pixel_x in range(2, current.get_width() - 2, 3):
            var world_point: Vector2 = state.rect.position + Vector2(pixel_x + 0.5, pixel_y + 0.5) * 8.0
            var screen_point := Vector2i(checks.to_global(world_point).floor())
            if not Rect2i(Vector2i.ZERO, viewport.size).has_point(screen_point):
                continue
            var sample_point: Vector2 = (checks.to_local(Vector2(screen_point) + Vector2(0.5, 0.5)) - state.rect.position) / 8.0 - Vector2(0.5, 0.5)
            var old_alpha := sample_alpha(previous, sample_point)
            var new_alpha := sample_alpha(current, sample_point)
            var expected := 1.0 - lerpf(old_alpha, new_alpha, state.blend)
            if old_alpha != new_alpha:
                changed_samples += 1
            var actual := rendered.get_pixelv(screen_point).r
            if absf(actual - expected) > 0.04:
                check(false, "Fog pixel must blend in place: expected %.3f, got %.3f at %s; blend %.3f, submitted %.3f" % [expected, actual, screen_point, state.blend, state.drawn_blend])
                return


func sample_alpha(image: Image, point: Vector2) -> float:
    var left := floori(point.x)
    var top := floori(point.y)
    var horizontal := point.x - left
    var vertical := point.y - top
    return lerpf(
        lerpf(image.get_pixel(left, top).a, image.get_pixel(left + 1, top).a, horizontal),
        lerpf(image.get_pixel(left, top + 1).a, image.get_pixel(left + 1, top + 1).a, horizontal),
        vertical)


func check(condition: bool, message: String) -> void:
    if not condition:
        failures += 1
        push_error(message)