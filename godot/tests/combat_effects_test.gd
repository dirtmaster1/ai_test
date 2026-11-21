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
    attacker.call("Setup", {"id": "wizard", "class_id": "wizard", "team": "player"})
    target.call("Setup", {"id": "enemy", "team": "enemy"})
    attacker.call("SetGridPos", Vector2i(1, 1))
    target.call("SetGridPos", Vector2i(2, 1))
    var attacker_sprite := attacker.get_node("Sprite2D") as Sprite2D
    var event_bus := root.get_node("EventBus")
    event_bus.emit_signal("ActionUsed", attacker, "fireball", "target")
    check(attacker_sprite.texture.resource_path.ends_with("wizard_cast.png"),
        "Spell action-used signals must play the caster's class cast animation")
    event_bus.emit_signal("ActionUsed", attacker, "melee", "target")
    check(attacker_sprite.texture.resource_path.ends_with("wizard_attack.png"),
        "Physical ability action-used signals must play the class attack animation")
    attacker.hide()
    target.hide()
    await RenderingServer.frame_post_draw
    check(viewport.get_texture().get_image().is_invisible(), "Test viewport must start empty")

    var actions := [
        ["melee attack", "PlayAttack", [attacker, target, "melee", false, 5]],
        ["ranged attack", "PlayAttack", [attacker, target, "ranged", false, 5]],
        ["spell projectile", "PlayAttack", [attacker, target, "magic-missile", true, 5]],
        ["lesser heal", "PlayHealingSpell", [attacker, target, 4, false]],
        ["greater heal", "PlayHealingSpell", [attacker, target, 10, true]],
        ["status ability", "PlayStatus", [target, Color.CYAN, "STATUS"]],
        ["sleep field", "PlayField", ["sleep", PackedVector2Array([target.position])]],
        ["web field", "PlayField", ["web", PackedVector2Array([target.position])]],
        ["arrow volley", "PlayField", ["rain-of-arrows", PackedVector2Array([target.position, attacker.position])]],
        ["lightning line", "PlayLightning", [attacker.position, PackedVector2Array([target.position, target.position + Vector2(64,0)])]],
        ["teleport endpoints", "PlayTeleport", [attacker.position, target.position]],
        ["cleanse", "PlayCleansing", [target]],
        ["resurrection", "PlayResurrection", [target]],
        ["shadow departure", "PlayShadowDeparture", [attacker, attacker.position]],
        ["lock", "PlayUtility", [target.position, false]],
        ["trap", "PlayUtility", [target.position, true]],
        ["poison tick", "PlayPoisonTick", [target.position]],
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

    var hits: Dictionary[Vector2, int] = {}
    var center := Vector2(160, 120)
    var corner := center + Vector2(64, 64)
    hits[center] = 10
    hits[corner] = 7
    var footprint := PackedVector2Array()
    for y in range(-2, 3):
        for x in range(-2, 3):
            footprint.append(center + Vector2(x, y) * 32)
    effects.call("PlayFireball", Vector2(16, 120), center, 32.0, footprint, hits)
    var center_time := -1.0
    var corner_time := -1.0
    var flight_seen := false
    var corner_lit := false
    var elapsed := 0.0
    while elapsed < 2.2:
        await RenderingServer.frame_post_draw
        elapsed += root.get_process_delta_time()
        var image := viewport.get_texture().get_image()
        if center_time < 0 and image.get_pixel(72, 120).a > 0.1:
            flight_seen = true
        if image.get_pixel(230, 193).a > 0.1:
            corner_lit = true
        check(image.get_pixel(247, 193).a < 0.01, "Flames must stay inside the footprint")
        for node in effects.get_children():
            if node is Label and node.text == "-10" and center_time < 0:
                center_time = elapsed
            if node is Label and node.text == "-7" and corner_time < 0:
                corner_time = elapsed
    check(flight_seen and center_time > 0 and corner_time > center_time, "Flight precedes center and corner results")
    check(corner_lit and effects.get_child_count() == 0, "Blast reaches a corner and cleans up")
    if failure_count == 0:
        print("PASS: combat effects render and clean up; Fireball flight, footprint and hit order verified")
    quit(0 if failure_count == 0 else 1)


func check(condition: bool, message: String) -> void:
    if not condition:
        failure_count += 1
        push_error(message)