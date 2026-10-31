extends SceneTree


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    root.get_node("AudioManager/BackgroundMusicPlayer").stop()
    var checks = load("res://tests/SaveSlotChecks.cs").new()
    root.add_child(checks)
    checks.call("RunStorageChecks")
    for dimensions in [Vector2i(1920, 1080), Vector2i(1280, 720)]:
        root.size = dimensions
        root.content_scale_size = dimensions
        for save_mode in [true, false]:
            for empty in [true, false]:
                checks.call("OpenMenu", save_mode, empty)
                for frame in range(5):
                    await process_frame
                var screenshot_path := OS.get_environment("SAVE_MENU_SCREENSHOT")
                if not screenshot_path.is_empty() and not empty and dimensions.x == 1920 and DisplayServer.get_name() != "headless":
                    await RenderingServer.frame_post_draw
                    root.get_texture().get_image().save_png(screenshot_path + ("-save.png" if save_mode else "-load.png"))
                checks.call("CheckMenu", save_mode, empty)
                for frame in range(5):
                    await process_frame
                checks.call("CheckInlineLayout")
                checks.call("CloseMenu")
                await process_frame
        for count in [0, 1, 5, 6, 10]:
                checks.call("OpenLayoutMenu", count)
                for frame in range(8):
                    await process_frame
                checks.call("CheckCompactLayout", count)
                if count > 5:
                    await process_frame
                    checks.call("CheckScrolledLastCard")
                var layout_screenshot := OS.get_environment("SAVE_MENU_SCREENSHOT")
                if not layout_screenshot.is_empty() and count in [1, 5, 10] and DisplayServer.get_name() != "headless":
                    await RenderingServer.frame_post_draw
                    root.get_texture().get_image().save_png(layout_screenshot + "-%d-cards-%d.png" % [count, dimensions.y])
                checks.call("CloseMenu")
                await process_frame
        for save_mode in [true, false]:
            checks.call("OpenMenu", save_mode, false)
            checks.call("SelectForDeletion")
            for frame in range(5):
                await process_frame
            var delete_button = root.find_children("DeleteSave_2", "Button", true, false)[0]
            var position = delete_button.get_global_rect().get_center()
            for pressed in [true, false]:
                var click := InputEventMouseButton.new()
                click.position = position
                click.global_position = position
                click.button_index = MOUSE_BUTTON_LEFT
                click.pressed = pressed
                root.push_input(click, true)
            checks.call("CheckDeletion", save_mode)
            checks.call("CloseMenu")
            await process_frame
    checks.call("OpenMenu", true, true)
    for frame in range(5):
        await process_frame
    var name_field = root.find_children("SaveName", "LineEdit", true, false)[0]
    var field_position = name_field.get_global_rect().get_center()
    for pressed in [true, false]:
        var click := InputEventMouseButton.new()
        click.position = field_position
        click.global_position = field_position
        click.button_index = MOUSE_BUTTON_LEFT
        click.pressed = pressed
        root.push_input(click, true)
    checks.call("CheckInlineDraft")
    for frame in range(5):
        await process_frame
    checks.call("CheckInlineLayout")
    var inline_screenshot := OS.get_environment("SAVE_MENU_SCREENSHOT")
    if not inline_screenshot.is_empty() and DisplayServer.get_name() != "headless":
        await RenderingServer.frame_post_draw
        root.get_texture().get_image().save_png(inline_screenshot + "-inline.png")
    checks.call("CloseMenu")
    await process_frame
    var failures = checks.call("GetFailures")
    for failure in failures:
        push_error(failure)
    checks.call("Cleanup")
    checks.queue_free()
    await process_frame
    if failures.is_empty():
        print("PASS: newest-first single-column saves, compact zero/one/five/six/ten-card layouts, capped scrolling, inline editing, deletion, and save/load round trips")
    quit(0 if failures.is_empty() else 1)
