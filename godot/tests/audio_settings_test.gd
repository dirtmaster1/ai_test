extends SceneTree


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    var checks = load("res://tests/AudioSettingsChecks.cs").new()
    root.add_child(checks)
    var failures = checks.call("Run")
    for failure in failures:
        push_error(failure)
    checks.free()

    var audio_manager = root.get_node("AudioManager")
    var music_player = audio_manager.get_node("BackgroundMusicPlayer")
    var event_bus = root.get_node("EventBus")
    var transition_failures: Array[String] = []
    await create_timer(3.2).timeout
    audio_manager.call("SetBackgroundMusicVolume", 1.0)
    event_bus.emit_signal("CombatStarted")
    if music_player.stream.resource_path != "res://assets/audio/combat_1.wav" \
            or not is_equal_approx(music_player.volume_db, 4.0):
        transition_failures.append("Combat music must still switch immediately at the selected volume.")

    event_bus.emit_signal("CombatEnded")
    if music_player.stream.resource_path != "res://assets/audio/combat_1.wav":
        transition_failures.append("Combat music must remain selected while it fades out.")
    await create_timer(0.6).timeout
    if music_player.stream.resource_path != "res://assets/audio/combat_1.wav" \
            or music_player.volume_db >= 4.0 \
            or music_player.volume_db <= -20.0:
        transition_failures.append("Combat music must audibly fade down before the track changes.")

    await create_timer(0.8).timeout
    if music_player.stream.resource_path != "res://assets/audio/forest_town_theme.wav" \
            or music_player.volume_db >= 4.0 \
            or music_player.volume_db <= -80.0:
        transition_failures.append("Exploration music must fade in after combat music reaches silence.")
    await create_timer(2.0).timeout
    if music_player.stream.resource_path != "res://assets/audio/forest_town_theme.wav" \
            or not is_equal_approx(music_player.volume_db, 4.0):
        transition_failures.append("Exploration music must fade back to the selected volume.")

    event_bus.emit_signal("CombatStarted")
    if music_player.stream.resource_path != "res://assets/audio/combat_1.wav" \
            or not is_equal_approx(music_player.volume_db, 4.0):
        transition_failures.append("Combat re-entry must remain immediate and cancel the exploration fade.")

    for failure in transition_failures:
        push_error(failure)
    if failures.is_empty() and transition_failures.is_empty():
        print("PASS: combat-to-exploration music fades smoothly while combat entry stays immediate")
    quit(0 if failures.is_empty() and transition_failures.is_empty() else 1)
