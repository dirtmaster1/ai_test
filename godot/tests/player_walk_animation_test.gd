extends SceneTree


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    var checks = load("res://tests/PlayerWalkAnimationChecks.cs").new()
    root.add_child(checks)
    checks.call("Run")
    var frames = 0
    while not checks.call("IsFinished") and frames < 1200:
        await process_frame
        frames += 1
    if not checks.call("IsFinished"):
        push_error("Player walk animation checks did not finish within 1200 frames.")
        quit(1)
    else:
        quit(checks.call("ExitCode"))
