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
    if failures.is_empty():
        print("PASS: exploration and combat music loop and the settings slider controls persistent volume")
    quit(0 if failures.is_empty() else 1)
