extends SceneTree


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    var checks = load("res://tests/ActionFeedbackChecks.cs").new()
    root.add_child(checks)
    if OS.get_cmdline_user_args().has("--level-up-only"):
        await check_level_up(checks, true)
        var level_up_failures = checks.call("GetFailures")
        for failure in level_up_failures:
            push_error(failure)
        checks.call("Cleanup")
        checks.queue_free()
        await process_frame
        if level_up_failures.is_empty():
            print("PASS: compact level-up layout, aligned stat controls, and multiple allocations in one open dialog")
        quit(0 if level_up_failures.is_empty() else 1)
        return
    if OS.get_cmdline_user_args().has("--equipment-only"):
        var equipment_failures = checks.call("RunEquipmentBonusChecks")
        for failure in equipment_failures:
            push_error(failure)
        checks.queue_free()
        await process_frame
        if equipment_failures.is_empty():
            print("PASS: equipment stat bonuses affect healing, maximum MP, and MP regeneration")
        quit(0 if equipment_failures.is_empty() else 1)
        return
    if OS.get_cmdline_user_args().has("--icon-only"):
        var icon_failures = checks.call("RunIconChecks")
        for failure in icon_failures:
            push_error(failure)
        checks.call("Cleanup")
        checks.queue_free()
        await process_frame
        if icon_failures.is_empty():
            print("PASS: compact action dock, movement counter, shared icon atlas, and action-bar icons")
        quit(0 if icon_failures.is_empty() else 1)
        return
    checks.call("Run")
    await check_level_up(checks, false)
    checks.call("StartQueueChecks")
    await create_timer(1.65).timeout
    checks.call("CheckPendingBanner")
    await create_timer(2.65).timeout
    checks.call("CheckPreservedBanner")
    checks.call("ShowLongFailure")
    await process_frame
    await process_frame
    checks.call("CheckBannerLayout")
    var screenshot_path := OS.get_environment("ACTION_FEEDBACK_SCREENSHOT")
    if not screenshot_path.is_empty() and DisplayServer.get_name() != "headless":
        await create_timer(0.25).timeout
        await RenderingServer.frame_post_draw
        root.get_texture().get_image().save_png(screenshot_path)
    var failures = checks.call("GetFailures")
    for failure in failures:
        push_error(failure)
    checks.call("Cleanup")
    checks.queue_free()
    await process_frame
    if failures.is_empty():
        print("PASS: red action feedback, resource/target checks, HUD/keyboard attempts, queue deduplication, and allowed actions")
    quit(0 if failures.is_empty() else 1)


func check_level_up(checks: Node, create_fixture: bool) -> void:
    var original_size := root.size
    checks.call("StartLevelUpChecks", create_fixture)
    for remaining in [3, 2, 1]:
        for frame in range(5):
            await process_frame
        checks.call("CheckLevelUpLayout")
        var screenshot_path := OS.get_environment("LEVEL_UP_SCREENSHOT")
        if remaining == 3 and not screenshot_path.is_empty() and DisplayServer.get_name() != "headless":
            await RenderingServer.frame_post_draw
            root.get_texture().get_image().save_png(screenshot_path)
        checks.call("ChooseLevelUpStat", remaining)
        await process_frame
        checks.call("CheckLevelUpClosed", remaining - 1)
        if remaining > 1:
            if remaining == 3:
                root.size = Vector2i(1280, 720)
            else:
                root.size = Vector2i(1920, 1080)
    root.size = original_size
