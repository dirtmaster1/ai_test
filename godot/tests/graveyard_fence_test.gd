extends SceneTree

var failure_count := 0


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    var image := Image.load_from_file("res://assets/tilesets/graveyard_terrain_64.png")
    check(image != null, "Graveyard atlas must load")
    if image == null:
        quit(1)
        return
    check(image.get_size() == Vector2i(320, 192), "Atlas must retain its 5 by 3 grid")
    check_floor_seams(image)
    var world := (load("res://scenes/Gameworld.tscn") as PackedScene).instantiate()
    var layer := world.get_node("Maps/graveyard-base") as TileMapLayer
    var walls := {}
    var bounds := layer.get_used_rect()
    var maximum := bounds.end - Vector2i.ONE
    check(layer.get_used_cells().size() == 660, "Map must retain all 660 cells")
    for cell in layer.get_used_cells():
        var data := layer.get_cell_tile_data(cell)
        if data == null or data.get_custom_data("terrain_type") != "wall":
            continue
        var horizontal: bool = cell.y == bounds.position.y or cell.y == maximum.y
        var vertical: bool = cell.x == bounds.position.x or cell.x == maximum.x
        var coordinate := layer.get_cell_atlas_coords(cell)
        check(horizontal or vertical, "Fence must stay on the perimeter at " + str(cell))
        if horizontal and vertical:
            check(coordinate == Vector2i(2, 1), "Corner tile at " + str(cell))
        elif horizontal:
            check(coordinate in [Vector2i(0, 1), Vector2i(3, 1)], "Horizontal fence at " + str(cell))
        else:
            check(coordinate in [Vector2i(1, 1), Vector2i(4, 1)], "Vertical fence at " + str(cell))
        var expected_flags := 0
        if cell.x == maximum.x:
            expected_flags |= TileSetAtlasSource.TRANSFORM_FLIP_H
        if cell.y == maximum.y:
            expected_flags |= TileSetAtlasSource.TRANSFORM_FLIP_V
        var flags := layer.get_cell_alternative_tile(cell)
        check(flags == expected_flags, "Fence orientation at " + str(cell))
        var tile := image.get_region(Rect2i(coordinate * 64, Vector2i(64, 64)))
        if flags & TileSetAtlasSource.TRANSFORM_FLIP_H:
            tile.flip_x()
        if flags & TileSetAtlasSource.TRANSFORM_FLIP_V:
            tile.flip_y()
        walls[cell] = tile
    check(walls.size() == 99, "All 99 original walls must remain blocked")
    for entrance in [Vector2i(13, -13), Vector2i(9, 19), Vector2i(10, 19)]:
        var data := layer.get_cell_tile_data(entrance)
        check(data != null and data.get_custom_data("terrain_type") == "floor", "Entrance stays walkable: " + str(entrance))
    var join_count := 0
    for cell: Vector2i in walls:
        for direction in [Vector2i.RIGHT, Vector2i.DOWN]:
            var neighbor: Vector2i = cell + direction
            if not walls.has(neighbor):
                continue
            var first: Image = walls[cell]
            var second: Image = walls[neighbor]
            var matches := true
            for offset in range(64):
                var first_pixel := Vector2i(63, offset) if direction == Vector2i.RIGHT else Vector2i(offset, 63)
                var second_pixel := Vector2i(0, offset) if direction == Vector2i.RIGHT else Vector2i(offset, 0)
                if first.get_pixelv(first_pixel) != second.get_pixelv(second_pixel):
                    matches = false
                    break
            check(matches, "Seamless fence join: " + str(cell) + " -> " + str(neighbor))
            join_count += 1
    check(join_count == 97, "All 97 connected perimeter joins must be checked")
    world.free()
    if failure_count == 0:
        print("PASS: 100 floor pairings in both directions, 99 oriented walls, 97 fence joins, and three walkable entrance cells")
    quit(0 if failure_count == 0 else 1)


func check_floor_seams(image: Image) -> void:
    var floors: Array[Image] = []
    for row in [0, 2]:
        for column in range(5):
            var tile := image.get_region(Rect2i(column * 64, row * 64, 64, 64))
            check(not tile.is_invisible(), "Floor tile must contain artwork")
            floors.append(tile)
    for first_index in range(floors.size()):
        for second_index in range(floors.size()):
            var horizontal_matches := true
            var vertical_matches := true
            for offset in range(64):
                if floors[first_index].get_pixel(63, offset) != floors[second_index].get_pixel(0, offset):
                    horizontal_matches = false
                if floors[first_index].get_pixel(offset, 63) != floors[second_index].get_pixel(offset, 0):
                    vertical_matches = false
            check(horizontal_matches, "Horizontal floor seam: %d -> %d" % [first_index, second_index])
            check(vertical_matches, "Vertical floor seam: %d -> %d" % [first_index, second_index])


func check(condition: bool, message: String) -> void:
    if not condition:
        failure_count += 1
        push_error(message)