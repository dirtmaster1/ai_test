extends SceneTree


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    var failures: Array[String] = []
    var music := root.get_node("AudioManager/BackgroundMusicPlayer") as AudioStreamPlayer
    music.stop()
    music.stream = null
    var main := load("res://scenes/Main.tscn").instantiate() as Node
    var base := main.get_node("Gameworld/Maps/forest-town-base") as TileMapLayer
    var markers := main.get_node("Gameworld/Maps/forest-town-markers") as TileMapLayer

    check_tile(base, Vector2i(9, -10), 6, Vector2i(0, 7), "wall", failures)
    check_tile(base, Vector2i(10, -10), 6, Vector2i(1, 7), "wall", failures)
    check_tile(base, Vector2i(11, -10), 6, Vector2i(2, 7), "see_through", failures)
    check_tile(base, Vector2i(12, -10), 6, Vector2i(1, 7), "wall", failures)
    check_tile(base, Vector2i(13, -10), 6, Vector2i(3, 7), "wall", failures)
    check_tile(base, Vector2i(9, -9), 6, Vector2i(4, 8), "wall", failures)
    check_tile(base, Vector2i(13, -9), 6, Vector2i(5, 8), "wall", failures)
    check_tile(base, Vector2i(9, -6), 6, Vector2i(4, 8), "wall", failures)
    check_tile(base, Vector2i(13, -6), 6, Vector2i(5, 8), "wall", failures)
    check_tile(base, Vector2i(9, -5), 6, Vector2i(0, 10), "wall", failures)
    check_tile(base, Vector2i(10, -5), 6, Vector2i(0, 10), "wall", failures)
    check_tile(base, Vector2i(11, -5), 6, Vector2i(1, 8), "door", failures)
    check_tile(base, Vector2i(12, -5), 6, Vector2i(0, 10), "wall", failures)
    check_tile(base, Vector2i(13, -5), 6, Vector2i(0, 10), "wall", failures)
    check_tile(base, Vector2i(10, -9), 6, Vector2i(0, 9), "floor", failures)
    check_tile(base, Vector2i(11, -8), 6, Vector2i(0, 9), "floor", failures)
    check_tile(base, Vector2i(11, -4), 6, Vector2i(1, 10), "floor", failures)

    var door_data := base.get_cell_tile_data(Vector2i(11, -5))
    if door_data == null or door_data.get_custom_data("open_door_atlas") != "2,8":
        failures.append("Blacksmith door does not point to its open doorway tile.")
    var terrain_source := base.tile_set.get_source(6) as TileSetAtlasSource
    if terrain_source == null or not terrain_source.has_tile(Vector2i(2, 8)):
        failures.append("Blacksmith open doorway tile is missing from the terrain atlas.")
    if terrain_source == null \
    or not terrain_source.has_tile(Vector2i(4, 8)) \
    or not terrain_source.has_tile(Vector2i(5, 8)):
        failures.append("Blacksmith left/right side-wall variants are missing from the terrain atlas.")

    check_marker(markers, Vector2i(10, -9), 1, Vector2i(2, 5), "Blacksmith Forge", failures)
    check_marker(markers, Vector2i(12, -9), 1, Vector2i(3, 5), "Blacksmith Weapon Rack", failures)
    check_marker(markers, Vector2i(12, -8), 1, Vector2i(4, 5), "Blacksmith Armor Rack", failures)
    check_marker(markers, Vector2i(11, -7), 5, Vector2i(1, 0), "Angus the Blacksmith", failures)

    var angus_data := markers.get_cell_tile_data(Vector2i(11, -7))
    if angus_data == null \
    or angus_data.get_custom_data("marker_type") != "npc" \
    or angus_data.get_custom_data("uses_tile_visual") != true \
    or angus_data.get_custom_data("npc_role") != "vendor" \
    or angus_data.get_custom_data("npc_id") != "angus" \
    or angus_data.get_custom_data("vendor_id") != "angus" \
    or angus_data.get_custom_data("dialogue_id") != "angus":
        failures.append("Angus marker is missing vendor or dialogue metadata.")

    check_shop_art(failures)
    check_vendor_data(failures)
    main.free()
    if not failures.is_empty():
        for failure in failures:
            push_error(failure)
        quit(1)
        return
    print("PASS: Forest Town blacksmith layout, door, markers, Angus dialogue, vendor stock, and item icons.")
    quit()


func check_tile(
    layer: TileMapLayer,
    cell: Vector2i,
    source_id: int,
    atlas_coords: Vector2i,
    terrain_type: String,
    failures: Array[String]
) -> void:
    if layer.get_cell_source_id(cell) != source_id or layer.get_cell_atlas_coords(cell) != atlas_coords:
        failures.append("Unexpected terrain tile at %s." % cell)
        return
    var data := layer.get_cell_tile_data(cell)
    if data == null or data.get_custom_data("terrain_type") != terrain_type:
        failures.append("Unexpected terrain metadata at %s." % cell)


func check_marker(
    layer: TileMapLayer,
    cell: Vector2i,
    source_id: int,
    atlas_coords: Vector2i,
    expected_name: String,
    failures: Array[String]
) -> void:
    if layer.get_cell_source_id(cell) != source_id or layer.get_cell_atlas_coords(cell) != atlas_coords:
        failures.append("Unexpected marker tile at %s." % cell)
        return
    var data := layer.get_cell_tile_data(cell)
    if data == null or data.get_custom_data("name") != expected_name:
        failures.append("Unexpected marker metadata at %s." % cell)


func check_shop_art(failures: Array[String]) -> void:
    var terrain_image := Image.new()
    if terrain_image.load("res://assets/tilesets/terrain_grass_water_64.png") != OK:
        failures.append("Blacksmith terrain atlas image could not be loaded.")
        return
    var marker_image := Image.new()
    if marker_image.load("res://assets/tilesets/general_items_64.png") != OK:
        failures.append("Blacksmith marker atlas image could not be loaded.")
        return

    var shop_tiles := [
        Vector2i(0, 7), Vector2i(1, 7), Vector2i(2, 7), Vector2i(3, 7),
        Vector2i(1, 8), Vector2i(2, 8), Vector2i(3, 8), Vector2i(4, 8), Vector2i(5, 8),
        Vector2i(0, 9), Vector2i(0, 10), Vector2i(1, 10), Vector2i(3, 10),
    ]
    for atlas_coords in shop_tiles:
        if not tile_is_fully_opaque(terrain_image, atlas_coords):
            failures.append("Blacksmith terrain tile %s contains transparency." % atlas_coords)
    for atlas_coords in [Vector2i(3, 8), Vector2i(0, 9), Vector2i(3, 10)]:
        if not tiles_match(terrain_image, atlas_coords, Vector2i(5, 6)):
            failures.append("Blacksmith floor tile %s does not match the plank floor." % atlas_coords)

    for atlas_coords in [Vector2i(2, 5), Vector2i(3, 5), Vector2i(4, 5)]:
        if has_saturated_magenta(marker_image, atlas_coords):
            failures.append("Blacksmith marker tile %s still contains pink/purple pixels." % atlas_coords)


func tiles_match(image: Image, first: Vector2i, second: Vector2i) -> bool:
    for y in range(64):
        for x in range(64):
            if image.get_pixel(first.x * 64 + x, first.y * 64 + y) \
            != image.get_pixel(second.x * 64 + x, second.y * 64 + y):
                return false
    return true


func tile_is_fully_opaque(image: Image, atlas_coords: Vector2i) -> bool:
    for y in range(64):
        for x in range(64):
            if image.get_pixel(atlas_coords.x * 64 + x, atlas_coords.y * 64 + y).a < 0.999:
                return false
    return true


func has_saturated_magenta(image: Image, atlas_coords: Vector2i) -> bool:
    for y in range(64):
        for x in range(64):
            var color := image.get_pixel(atlas_coords.x * 64 + x, atlas_coords.y * 64 + y)
            var red_blue := minf(color.r, color.b)
            if color.a > 0.5 \
            and red_blue > 0.2 \
            and absf(color.r - color.b) < 0.15 \
            and color.g < red_blue * 0.35:
                return true
    return false


func check_vendor_data(failures: Array[String]) -> void:
    var game_data := JSON.parse_string(FileAccess.get_file_as_string("res://resources/game_data.json")) as Dictionary
    if game_data == null:
        failures.append("game_data.json could not be parsed.")
        return

    var dialogue: Dictionary = game_data.get("dialogues", {}).get("angus", {})
    var pages: Array = dialogue.get("pages", [])
    if pages.size() != 3:
        failures.append("Angus must have three dialogue pages.")
    elif not has_choice(pages[0].get("choices", []), "What do you make here?", 1) \
    or not has_choice(pages[0].get("choices", []), "What do you sell?", 2):
        failures.append("Angus informational dialogue choices are missing.")

    var vendor: Dictionary = game_data.get("vendors", {}).get("angus", {})
    var inventory: Array = vendor.get("starting_inventory_item_ids", [])
    var items: Dictionary = game_data.get("items", {})
    var icon_source := FileAccess.get_file_as_string("res://scripts/HudController.cs")
    if icon_source.find('ItemIconAtlasPath = "res://assets/ui/icon_misc_1.png"') < 0:
        failures.append("Item icons are not sourced from icon_misc_1.png.")
    if inventory.is_empty():
        failures.append("Angus vendor inventory is empty.")
    var weapon_count := 0
    var armor_count := 0
    for item_id in inventory:
        var item: Dictionary = items.get(item_id, {})
        if item.is_empty():
            failures.append("Angus inventory references unknown item '%s'." % item_id)
            continue
        if item.get("type", "") == "armor":
            armor_count += 1
            if item.get("armor_weight", "") not in ["light", "medium"]:
                failures.append("Angus stocks armor outside the light-to-medium range: %s." % item_id)
        elif item.get("type", "") == "weapon":
            weapon_count += 1
            if item.get("slot", "") != "1-handed":
                failures.append("Angus stocks a weapon outside the light/small/short range: %s." % item_id)
            if icon_source.find("[\"%s\"] = new(" % item_id) < 0:
                failures.append("Angus weapon has no icon_misc_1.png mapping: %s." % item_id)
        else:
            failures.append("Angus stocks an item that is neither armor nor a weapon: %s." % item_id)
    if weapon_count < 3 or armor_count < 1:
        failures.append("Angus inventory needs a few one-handed weapons and light/medium armor.")


func has_choice(choices: Array, label: String, next_page: int) -> bool:
    for choice in choices:
        if choice.get("label", "") == label and int(choice.get("next_page", -1)) == next_page:
            return true
    return false
