extends SceneTree


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    var checks = load("res://tests/ActionFeedbackChecks.cs").new()
    root.add_child(checks)
    if OS.get_cmdline_user_args().has("--icon-only"):
        var icon_failures = checks.call("RunIconChecks")
        for failure in icon_failures:
            push_error(failure)
        checks.call("Cleanup")
        checks.queue_free()
        await process_frame
        if icon_failures.is_empty():
            print("PASS: Poison Strike shared atlas region and action-bar icon")
        quit(0 if icon_failures.is_empty() else 1)
        return
    checks.call("Run")
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
