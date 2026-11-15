extends Node


func _ready() -> void:
    call_deferred("run_test")


func run_test() -> void:
    var checks = load("res://tests/QuestChecks.cs").new()
    add_child(checks)
    var failures = checks.call("Run")
    for failure in failures:
        push_error(failure)
    checks.queue_free()
    await get_tree().process_frame
    if failures.is_empty():
        print("PASS: necromancer quest acceptance, progress, reward, journal, and persistence")
    get_tree().quit(0 if failures.is_empty() else 1)
