extends SceneTree

var failures: Array[String] = []
var checks: Node


func _initialize() -> void:
    call_deferred("run_test")


func run_test() -> void:
    if not OS.get_user_data_dir().replace("\\", "/").get_file().begins_with("dark-dungeon-save-menu-test-"):
        push_error("This test requires an isolated custom user directory named dark-dungeon-save-menu-test-<id>.")
        quit(1)
        return
    checks = load("res://tests/SaveSlotChecks.cs").new()
    root.add_child(checks)
    change_scene_to_file("res://scenes/StartScreen.tscn")
    await settle()
    press("NewGameButton")
    await settle()
    check(FileAccess.file_exists("user://dark_dungeon_tactics_recovery.json"),
        "The fixture must have a recovery checkpoint to verify it stays hidden.")
    var recovery_bytes = FileAccess.get_file_as_string("user://dark_dungeon_tactics_recovery.json")
    press("LoadButton")
    await settle()
    check_auto_save_card(0)
    press("CancelSaveMenu")
    await settle()
    check(FileAccess.get_file_as_string("user://dark_dungeon_tactics_recovery.json") == recovery_bytes,
        "Hiding the recovery card must not delete or alter the defeat-recovery checkpoint.")
    await return_to_main_menu()
    press("LoadGameButton")
    await settle()
    check_auto_save_card(0)
    press("SaveCard_0")
    press("ConfirmSaveMenu")
    await settle()
    checks.call("SetGameGold", 123)
    press("SaveButton")
    await settle()
    check(paused and find("SaveMenuTitle").text == "Save Game", "Utility Save must open the save picker and pause gameplay.")
    press("NewSaveCard")
    enter_name("First Adventure")
    press("ConfirmSaveMenu")
    await settle()
    check(not paused and read_save(1).party_gold == 123, "New Save must create the named slot and resume gameplay.")

    checks.call("SetGameGold", 321)
    press("SaveButton")
    await settle()
    press("SaveCard_1")
    press("ConfirmSaveMenu")
    await settle()
    var confirmation = root.find_children("*", "ConfirmationDialog", true, false)[0]
    confirmation.get_ok_button().emit_signal("pressed")
    await settle()
    check(read_save(1).party_gold == 321, "Confirmed overwrite must update the selected slot.")
    var first_bytes = FileAccess.get_file_as_string("user://saves/slot-01.json")

    checks.call("SetGameGold", 789)
    press("SaveButton")
    await settle()
    press("NewSaveCard")
    enter_name("Second Adventure")
    press("ConfirmSaveMenu")
    await settle()
    check(read_save(2).party_gold == 789, "A second new save must receive an independent slot.")
    var second_bytes = FileAccess.get_file_as_string("user://saves/slot-02.json")
    press("LoadButton")
    await settle()
    check(find("ConfirmSaveMenu").text == "Load", "Utility Load must show a Load action, not Save.")
    press("SaveCard_1")
    press("ConfirmSaveMenu")
    await settle()
    check(not paused and checks.call("GetGameGold") == 321, "Utility Load must restore the selected adventure in a fresh scene.")
    check(FileAccess.get_file_as_string("user://saves/slot-02.json") == second_bytes, "Loading must leave other manual saves unchanged.")

    var auto_file = FileAccess.open("user://dark_dungeon_tactics_save.json", FileAccess.WRITE)
    var older_autosave = JSON.parse_string(second_bytes)
    older_autosave.saved_at_utc = "2000-01-01T00:00:00Z"
    auto_file.store_string(JSON.stringify(older_autosave))
    auto_file.close()
    checks.call("SetGameGold", 999)
    press("LoadButton")
    await settle()
    check_auto_save_card(2)
    check(find("SaveCards").get_child(2).name == "SaveCard_0",
        "An older Auto-Save must sort below newer named saves, not stay pinned above them.")
    press("SaveCard_0")
    press("ConfirmSaveMenu")
    await settle()
    check(checks.call("GetGameGold") == 789, "Loading Autosave must not overwrite it with outgoing progress.")

    await return_to_main_menu()
    press("LoadGameButton")
    await settle()
    check(find("ConfirmSaveMenu").text == "Load", "Title-screen Load must open the shared load picker.")
    check_auto_save_card(2)
    check(find("SaveCards").get_child(0).name == "SaveCard_0",
        "A freshly updated Auto-Save must sort above older named saves.")
    press("SaveCard_1")
    press("ConfirmSaveMenu")
    await settle()
    check(checks.call("GetGameGold") == 321 and not paused, "Title-screen Load must restore the chosen slot and unpause.")

    await return_to_main_menu()
    press("NewGameButton")
    await settle()
    check(checks.call("GetGameGold") == 500, "New Game must not reload the previous autosave.")
    check(FileAccess.get_file_as_string("user://saves/slot-01.json") == first_bytes
        and FileAccess.get_file_as_string("user://saves/slot-02.json") == second_bytes,
        "New Game and automatic saves must never delete or modify named saves.")
    await return_to_main_menu()
    for path in ["user://saves/slot-01.json", "user://saves/slot-02.json",
        "user://dark_dungeon_tactics_save.json", "user://dark_dungeon_tactics_recovery.json"]:
        DirAccess.remove_absolute(ProjectSettings.globalize_path(path))
    DirAccess.remove_absolute(ProjectSettings.globalize_path("user://saves"))
    current_scene.queue_free()
    await process_frame
    checks.call("CollectManagedWrappers")
    checks.queue_free()
    await process_frame
    for failure in failures:
        push_error(failure)
    if failures.is_empty():
        print("PASS: one Auto-Save card in both Load menus, hidden recovery checkpoint, named save/load, and Settings Main Menu")
    quit(0 if failures.is_empty() else 1)


func settle() -> void:
    for frame in range(8):
        await process_frame


func check_auto_save_card(manual_count: int) -> void:
    var cards = find("SaveCards")
    check(cards.get_child_count() == manual_count + 1,
        "Load must show exactly one automatic card in addition to the named saves.")
    var automatic = find("SaveCard_0")
    check(automatic.find_child("CardTitle", true, false).text == "Auto-Save",
        "The automatic card must be labeled exactly Auto-Save, even with saved name metadata.")
    check(cards.find_children("SaveCard_-1", "", true, false).is_empty(),
        "The internal recovery checkpoint must not appear in the Load menu.")
    check(automatic.find_children("DeleteSave_*", "", true, false).is_empty(),
        "The Auto-Save card must remain protected from deletion.")
    var party_preview = automatic.find_child("PartyPreview", true, false)
    check(party_preview != null and party_preview.get_child_count() > 0 and party_preview.get_child_count() <= 5,
        "Auto-Save must show the current active party portraits.")
    if party_preview != null:
        for member in party_preview.get_children():
            var level = member.find_child("PartyMemberLevel", true, false)
            var portrait = member.find_child("PartyMemberPortrait", true, false)
            check(level != null and level.text.begins_with("Level ")
                and portrait != null and portrait.texture != null
                and portrait.mouse_filter == Control.MOUSE_FILTER_IGNORE
                and portrait.tooltip_text.is_empty(),
                "Each Auto-Save party portrait must display its level without hover behavior.")
        if manual_count > 0:
            var manual_preview = find("SaveCard_1").find_child("PartyPreview", true, false)
            check(manual_preview != null
                and abs(party_preview.get_global_rect().end.x - manual_preview.get_global_rect().end.x) < 1.0,
                "Auto-Save party portraits must line up with manual save portraits despite having no delete button.")


func return_to_main_menu() -> void:
    var gold = checks.call("GetGameGold")
    press("SettingsButton")
    await settle()
    var panel = find("SettingsPanel")
    var button = find("MainMenuButton")
    check(panel.visible and button.text == "Main Menu", "Settings must expose the Main Menu button.")
    check(panel.get_global_rect().encloses(button.get_global_rect()), "Main Menu must fit inside the Settings panel.")
    var screenshot_path := OS.get_environment("SETTINGS_MENU_SCREENSHOT")
    if not screenshot_path.is_empty() and DisplayServer.get_name() != "headless":
        await RenderingServer.frame_post_draw
        root.get_texture().get_image().save_png(screenshot_path)
    press("MainMenuButton")
    await settle()
    check(current_scene.scene_file_path == "res://scenes/StartScreen.tscn" and not paused,
        "Main Menu must return directly to the interactive opening screen.")
    var autosave = JSON.parse_string(FileAccess.get_file_as_string("user://dark_dungeon_tactics_save.json"))
    check(autosave.party_gold == gold, "Returning through Settings must preserve normal exit autosaving.")


func find(node_name: String) -> Node:
    return root.find_children(node_name, "", true, false)[0]


func press(node_name: String) -> void:
    find(node_name).emit_signal("pressed")


func enter_name(value: String) -> void:
    var field = find("SaveName")
    field.text = value
    field.emit_signal("text_changed", value)


func read_save(slot: int) -> Dictionary:
    return JSON.parse_string(FileAccess.get_file_as_string("user://saves/slot-%02d.json" % slot))


func check(condition: bool, message: String) -> void:
    if not condition:
        failures.append(message)
