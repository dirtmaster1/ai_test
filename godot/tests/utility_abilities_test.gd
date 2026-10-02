extends SceneTree


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    var checks = load("res://tests/UtilityAbilityChecks.cs").new()
    root.add_child(checks)
    var failures = checks.call("Run")
    if failures.is_empty() and DisplayServer.get_name() != "headless":
        await process_frame
        for combat in [false, true]:
            for disarm in [true, false]:
                var pixel: Vector2 = checks.call("StartRenderedCheck", combat, disarm)
                await process_frame
                await RenderingServer.frame_post_draw
                var image := root.get_texture().get_image()
                image.save_png("user://utility-target-preview.png")
                var color := image.get_pixelv(Vector2i(pixel))
                if not (color.g > 0.65 and color.g > color.r * 1.3):
                    failures.append("Valid utility target must render a green range highlight")
                checks.call("ShowRenderedConfirmation")
                await process_frame
                await RenderingServer.frame_post_draw
                var mode := "combat" if combat else "exploration"
                var ability := "disarm" if disarm else "pick-lock"
                root.get_texture().get_image().save_png("user://utility-%s-%s-dialog.png" % [mode, ability])
                checks.call("ConfirmRenderedAction")
                await create_timer(0.25).timeout
                await RenderingServer.frame_post_draw
                root.get_texture().get_image().save_png("user://utility-%s-%s-banner.png" % [mode, ability])
        for failure in checks.call("GetFailures"):
            if not failures.has(failure):
                failures.append(failure)
    for failure in failures:
        push_error(failure)
    checks.queue_free()
    await process_frame
    if failures.is_empty():
        print("PASS: thief targeting, confirmations, trap damage, door locks, action costs, and persistence")
    quit(0 if failures.is_empty() else 1)