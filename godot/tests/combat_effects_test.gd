extends SceneTree

var failure_count := 0


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    var scene := load("res://scenes/Main.tscn").instantiate() as Node
    var effects := scene.get_node("Gameworld/CombatEffectsDirector") as Node2D
    check(effects.visible, "Main scene must not hide combat effects")
    effects.get_parent().remove_child(effects)
    scene.free()

    var viewport := SubViewport.new()
    viewport.size = Vector2i(320, 240)
    viewport.transparent_bg = true
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    viewport.world_2d = World2D.new()
    root.add_child(viewport)
    viewport.add_child(effects)

    var unit_scene := load("res://scenes/Unit.tscn") as PackedScene
    var attacker := unit_scene.instantiate() as Node2D
    var target := unit_scene.instantiate() as Node2D
    viewport.add_child(attacker)
    viewport.add_child(target)
    attacker.call("Setup", {"id": "knight", "team": "player"})
    target.call("Setup", {"id": "enemy", "team": "enemy"})
    attacker.call("SetGridPos", Vector2i(1, 1))
    target.call("SetGridPos", Vector2i(2, 1))
    attacker.hide()
    target.hide()
    await RenderingServer.frame_post_draw
    check(viewport.get_texture().get_image().is_invisible(), "Test viewport must start empty")

    var actions := [
        ["melee attack", "PlayAttack", [attacker, target, "melee", false, 5]],
        ["ranged attack", "PlayAttack", [attacker, target, "ranged", false, 5]],
        ["spell projectile", "PlayAttack", [attacker, target, "fireball", true, 5]],
        ["healing", "PlayHeal", [attacker, target, 5]],
        ["status ability", "PlayStatus", [target, Color.CYAN, "STATUS"]],
        ["area spell", "PlayArea", [Vector2(160, 120), 48.0, Color.ORANGE, "AREA"]],
        ["defend ability", "PlayDefend", [attacker]],
        ["damage effect", "PlayDamageResult", [target, 5, Color.RED]],
    ]
    for action in actions:
        effects.callv(action[1], action[2])
        var previous_pixels := PackedByteArray()
        var visible_frames := 0
        var changed_frames := 0
        var elapsed := 0.0
        while elapsed < 1.2:
            await RenderingServer.frame_post_draw
            elapsed += root.get_process_delta_time()
            var image := viewport.get_texture().get_image()
            if not image.is_invisible():
                visible_frames += 1
                var pixels := image.get_data()
                if not previous_pixels.is_empty() and pixels != previous_pixels:
                    changed_frames += 1
                previous_pixels = pixels
        check(visible_frames > 1, "%s must render across multiple frames" % action[0])
        check(changed_frames > 0, "%s must animate, not remain static" % action[0])
        check(effects.get_child_count() == 0, "%s must clean up completed visuals" % action[0])

    if failure_count == 0:
        print("PASS: main scene enables combat effects; all 8 attack, spell, and ability effects render, animate, and clean up")
    quit(0 if failure_count == 0 else 1)


func check(condition: bool, message: String) -> void:
    if not condition:
        failure_count += 1
        push_error(message)