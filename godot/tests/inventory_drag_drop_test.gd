extends SceneTree


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    var checks = load("res://tests/InventoryDragDropChecks.cs").new()
    root.add_child(checks)
    var failures = checks.call("Run")
    for failure in failures:
        push_error(failure)
    checks.queue_free()
    await process_frame
    if failures.is_empty():
        print("PASS: inventory drag/drop accepts only compatible equipment slots and returns equipped items to shared inventory")
    quit(0 if failures.is_empty() else 1)
