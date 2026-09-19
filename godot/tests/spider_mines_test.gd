extends SceneTree

var failure_count := 0


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    var scene := load("res://scenes/Gameworld.tscn").instantiate() as Node
    var maps := scene.get_node("Maps")
    var loader := scene.get_node("MapLoader")
    scene.remove_child(maps)
    scene.remove_child(loader)
    scene.free()
    var host := Node2D.new()
    root.add_child(host)
    host.add_child(maps)
    host.add_child(loader)
    var map_data: Dictionary = loader.call("LoadMapStub", "spider-mines")
    var base_layer := maps.get_node("spider-mines-base") as TileMapLayer
    var marker_layer := maps.get_node("spider-mines-markers") as TileMapLayer
    for cell in base_layer.get_used_cells():
        check(base_layer.get_cell_tile_data(cell) != null, "Terrain tile must resolve at %s" % cell)
    for cell in marker_layer.get_used_cells():
        check(marker_layer.get_cell_tile_data(cell) != null, "Marker tile must resolve at %s" % cell)

    var reachable := {}
    var floor_cells := {}
    for cell in map_data["walkable_cells"]:
        if not map_data["walls"].has(cell):
            floor_cells[cell] = true
    var entrance := Vector2i(0, 13)
    check(floor_cells.has(entrance), "Entrance must be walkable")
    var frontier: Array[Vector2i] = [entrance]
    reachable[entrance] = true
    while not frontier.is_empty():
        var current := frontier.pop_front() as Vector2i
        for direction in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
            var neighbor: Vector2i = current + direction
            if floor_cells.has(neighbor) and not reachable.has(neighbor):
                reachable[neighbor] = true
                frontier.append(neighbor)

    var expected_groups := {
        "spider-mines-entrance": 2,
        "spider-mines-survey-room": 3,
        "spider-mines-supply-room": 3,
        "spider-mines-inner-tunnel": 3,
        "spider-mines-queen-nest": 3,
    }
    var expected_types := {"Cave Spider": 9, "Giant Spider": 4, "Spider Queen": 1}
    var actual_types := {}
    var enemies: Array = []
    check(map_data["encounters"].size() == expected_groups.size(), "Exactly five encounters must load")
    for encounter in map_data["encounters"]:
        var encounter_id: String = encounter["id"]
        check(expected_groups.has(encounter_id), "Unexpected encounter: " + encounter_id)
        check(encounter["enemies"].size() == expected_groups.get(encounter_id, -1), "Encounter size: " + encounter_id)
        for enemy in encounter["enemies"]:
            enemy["test_encounter_id"] = encounter_id
            enemies.append(enemy)
            actual_types[enemy["name"]] = actual_types.get(enemy["name"], 0) + 1
            check(reachable.has(enemy["grid_pos"]), "Enemy must be reachable: " + enemy["id"])
            check(enemy["hit_points"] > 0 and enemy["experience"] > 0, "Template stats must survive marker loading")
        var connected: Array = [encounter["enemies"][0]]
        for current in connected:
            for candidate in encounter["enemies"]:
                if not connected.has(candidate) and distance(current["grid_pos"], candidate["grid_pos"]) <= current["aggro_range"]:
                    connected.append(candidate)
        check(connected.size() == encounter["enemies"].size(), "Encounter must chain together: " + encounter_id)
    check(actual_types == expected_types, "Expected nine cave spiders, four giants, and one queen; got %s" % actual_types)
    for enemy in enemies:
        for candidate in enemies:
            if enemy["test_encounter_id"] != candidate["test_encounter_id"]:
                check(distance(enemy["grid_pos"], candidate["grid_pos"]) > enemy["aggro_range"], "Separate encounters must not chain immediately")

    var expected_rewards := {
        "spider-mines-surveyor-satchel": ["fireball-scroll", 12],
        "spider-mines-foreman-cache": ["chain-mail", 20],
        "spider-mines-queen-hoard": ["war-axe", 35],
    }
    var reward_count := 0
    var game_data := root.get_node("GameData")
    for prop in map_data["props"]:
        if not expected_rewards.has(prop["id"]):
            continue
        reward_count += 1
        var expected: Array = expected_rewards[prop["id"]]
        check(prop["type"] == "chest", "Rewards must use persistent one-time chest interactions")
        check(reachable.has(prop["grid_pos"]), "Reward must be reachable: " + prop["id"])
        check(prop["loot_item_ids"].size() == 1 and prop["loot_item_ids"][0] == expected[0], "Guaranteed reward: " + prop["id"])
        check(prop["loot_rolls_min"] == 1 and prop["loot_rolls_max"] == 1, "Exactly one item per reward")
        check(prop["gold_amount"] == expected[1], "Reward gold: " + prop["id"])
        var item: Dictionary = game_data.call("GetItem", expected[0])
        check(not item.is_empty(), "Reward item must exist: " + expected[0])
    check(reward_count == 3, "Both branches and the nest must have rewards")
    check(map_data["transitions"].size() == 2, "Both entrance transition cells must remain")
    check(map_data["doors"].is_empty(), "Cave floor must not become fallback doors")
    await check_queen_footprint(host, map_data)
    host.queue_free()
    await process_frame
    if failure_count == 0:
        print("PASS: Spider Mines encounters and rewards; queen 2x2 footprint, collision, pathfinding, AI, melee targeting, persistence, and sprite configuration.")
    quit(0 if failure_count == 0 else 1)


func check_queen_footprint(host: Node, map_data: Dictionary) -> void:
    var queen_config: Dictionary = {}
    for encounter in map_data["encounters"]:
        for enemy in encounter["enemies"]:
            if enemy["name"] == "Spider Queen":
                queen_config = enemy
    check(not queen_config.is_empty(), "Queen configuration must exist")
    if queen_config.is_empty():
        return
    var unit_scene := load("res://scenes/Unit.tscn") as PackedScene
    var queen := unit_scene.instantiate() as Node2D
    var hero := unit_scene.instantiate() as Node2D
    host.add_child(queen)
    host.add_child(hero)
    queen.call("Setup", queen_config)
    hero.call("Setup", {"id": "warrior", "team": "player"})
    var checks = load("res://tests/QueenFootprintChecks.cs").new()
    var failures: Array = checks.call("Run", queen, hero, map_data)
    for failure in failures:
        check(false, failure)
    checks = null
    hero.queue_free()
    if DisplayServer.get_name() != "headless":
        host.remove_child(queen)
        var viewport := SubViewport.new()
        viewport.size = Vector2i(320, 256)
        viewport.transparent_bg = true
        viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
        viewport.world_2d = World2D.new()
        root.add_child(viewport)
        viewport.add_child(queen)
        queen.call("SetGridPos", Vector2i(1, 1))
        await RenderingServer.frame_post_draw
        var image := viewport.get_texture().get_image()
        for quadrant in [Vector2i(64, 64), Vector2i(128, 64), Vector2i(64, 128), Vector2i(128, 128)]:
            var visible_pixels := 0
            for pixel_y in range(quadrant.y, quadrant.y + 64):
                for pixel_x in range(quadrant.x, quadrant.x + 64):
                    if image.get_pixel(pixel_x, pixel_y).a > 0.1:
                        visible_pixels += 1
            check(visible_pixels > 50, "Queen artwork must render in each occupied tile")
        queen.call("SetSelectionHighlighted", true)
        await RenderingServer.frame_post_draw
        image = viewport.get_texture().get_image()
        check(image.get_pixel(128, 64).r > 0.7 and image.get_pixel(192, 128).r > 0.7, "Selection must enclose all four cells")
        queen.call("SetGridPos", Vector2i(2, 1))
        await RenderingServer.frame_post_draw
        image = viewport.get_texture().get_image()
        check(image.get_pixel(64, 128).a < 0.1 and image.get_pixel(192, 64).r > 0.7, "Selection and sprite must follow footprint movement")
        image.save_png("user://queen-footprint-test.png")
        viewport.queue_free()
        if failure_count == 0:
            print("PASS: Queen sprite renders across four tiles; 128px selection follows movement.")
    else:
        queen.queue_free()


func distance(origin: Vector2i, target: Vector2i) -> int:
    return absi(origin.x - target.x) + absi(origin.y - target.y)


func check(condition: bool, message: String) -> void:
    if not condition:
        failure_count += 1
        push_error(message)