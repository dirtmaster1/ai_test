extends SceneTree

var failure_count := 0


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    var viewport := SubViewport.new()
    viewport.size = Vector2i(320, 240)
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    viewport.world_2d = World2D.new()
    root.add_child(viewport)

    var world := Node2D.new()
    viewport.add_child(world)
    var unit := load("res://scenes/Unit.tscn").instantiate() as Node2D
    world.add_child(unit)
    unit.call("Setup", {"id": "knight", "team": "player"})
    unit.position = Vector2(96, 96)
    unit.call("SetSelectionHighlighted", true)
    await RenderingServer.frame_post_draw
    check_border(viewport, unit, false)

    var tween := unit.create_tween()
    tween.set_parallel(true)
    tween.set_trans(Tween.TRANS_SINE)
    tween.set_ease(Tween.EASE_OUT)
    tween.tween_property(unit, "position", Vector2(224, 160), 0.5)
    tween.tween_property(world, "position", Vector2(-48, -24), 0.5)
    var movement_frames := 0
    while tween.is_running():
        await RenderingServer.frame_post_draw
        check_border(viewport, unit, false)
        movement_frames += 1
    check(movement_frames > 1, "Movement must be checked across multiple rendered frames")

    unit.call("SetSelectionHighlighted", false)
    await RenderingServer.frame_post_draw
    var pixel := border_pixel(viewport, unit)
    check(not is_green(pixel), "Deselection must remove the green border")

    unit.call("Setup", {"id": "enemy", "team": "enemy"})
    unit.position = Vector2(160, 120)
    unit.call("SetSelectionHighlighted", true)
    await RenderingServer.frame_post_draw
    check_border(viewport, unit, true)

    if failure_count == 0:
        print("PASS: selection border tracks unit and parent tweens across %d frames; deselection and enemy color verified" % movement_frames)
    quit(0 if failure_count == 0 else 1)


func border_pixel(viewport: SubViewport, unit: Node2D) -> Color:
    var center := unit.get_global_transform_with_canvas().origin
    var image := viewport.get_texture().get_image()
    return image.get_pixel(roundi(center.x), roundi(center.y - 32))


func check_border(viewport: SubViewport, unit: Node2D, enemy: bool) -> void:
    var pixel := border_pixel(viewport, unit)
    if enemy:
        check(pixel.r > 0.7 and pixel.g < 0.4, "Enemy border must remain red")
    else:
        check(is_green(pixel), "Green border must stay at the unit's rendered position")


func is_green(pixel: Color) -> bool:
    return pixel.g > 0.6 and pixel.r < 0.5 and pixel.b < 0.6


func check(condition: bool, message: String) -> void:
    if not condition:
        failure_count += 1
        push_error(message)