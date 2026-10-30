extends SceneTree


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    root.get_node("AudioManager/BackgroundMusicPlayer").stop()
    var checks = load("res://tests/CombatIntroChecks.cs").new()
    root.add_child(checks)
    checks.call("Run")
    var deadline := Time.get_ticks_msec() + 15000
    while not checks.get("Finished") and Time.get_ticks_msec() < deadline:
        await process_frame
    var failures = checks.call("GetFailures")
    if not checks.get("Finished"):
        failures.append("Combat intro checks timed out.")
    for failure in failures:
        push_error(failure)
    checks.call("CollectManagedWrappers")
    checks.queue_free()
    await process_frame
    if failures.is_empty():
        print("PASS: northeast bandit intro, turn blocking, dialogue completion, chaining, and save/load")
    quit(0 if failures.is_empty() else 1)
