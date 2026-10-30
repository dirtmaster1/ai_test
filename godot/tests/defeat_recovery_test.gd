extends SceneTree


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    root.get_node("AudioManager/BackgroundMusicPlayer").stop()
    var checks = load("res://tests/DefeatRecoveryChecks.cs").new()
    root.add_child(checks)
    var failures = checks.call("Run")
    for failure in failures:
        push_error(failure)
    checks.call("CollectManagedWrappers")
    checks.queue_free()
    await process_frame
    if failures.is_empty():
        print("PASS: defeat recovery modal, independent checkpoint, respawn, and defeat-save recovery")
    quit(0 if failures.is_empty() else 1)
