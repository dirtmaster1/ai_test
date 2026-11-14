extends SceneTree


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    root.add_child(load("res://tests/PlayerWalkAnimationChecks.cs").new())
