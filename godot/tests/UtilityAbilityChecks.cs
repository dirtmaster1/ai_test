using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Reflection;

public partial class UtilityAbilityChecks : BattleController
{
    private const string TestMap = "utility-test";
    private const string TestSave = "user://utility-ability-test-save.json";
    private readonly Array<string> _failures = new();
    private readonly Vector2I _trapCell = new(2, 1);
    private readonly Vector2I _doorCell = new(3, 2);
    private const string KeyId = "rusty-key";
    private Unit _thief;
    private GamePersistence _testPersistence;
    private MapLoader _testLoader;
    private TurnManager _testTurns;
    private HudController _testHud;
    private CanvasLayer _hudLayer;
    private bool _renderedDisarm;

    public override void _Ready() { }
    public override void _ExitTree() => _testPersistence?.DeleteSaveGame(false);

    public Vector2 StartRenderedCheck(bool combat, bool disarm)
    {
        _renderedDisarm = disarm;
        var flowField = PrivateField("_flowState");
        flowField.SetValue(this, System.Enum.Parse(flowField.FieldType, combat ? "Combat" : "Exploration"));
        _thief.ResetTurnResources();
        InactiveTraps().Clear();
        Field<HashSet<string>>("_openedDoorIds").Clear();
        Field<System.Collections.Generic.Dictionary<string, HashSet<string>>>("_unlockedDoorIdsByMap").Clear();
        Doors()[_doorCell]["is_open"] = false;
        _testLoader.SetDoorVisual(TestMap, _doorCell, false);
        FocusThief();
        Position = new Vector2(300, 80);
        if (GetNodeOrNull<BattleOverlay>("Overlay") == null)
            AddChild(new BattleOverlay { Name = "Overlay" });
        Set("_hud", null);
        _hudLayer?.Free();
        _hudLayer = new CanvasLayer();
        AddChild(_hudLayer);
        _testHud = GD.Load<PackedScene>("res://ui/HUD.tscn").Instantiate<HudController>();
        _hudLayer.AddChild(_testHud);
        Set("_hud", _testHud);
        _testHud.AbilityPressed += abilityId => Call("OnHudAbilityPressed", abilityId);
        Call("SyncHudFromGameState");
        var buttons = (System.Collections.Generic.Dictionary<Button, string>)typeof(HudController)
            .GetField("_abilityIdsByButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_testHud);
        var desiredAbility = disarm ? "disarm-trap" : "pick-lock";
        var pressed = false;
        foreach (var entry in buttons)
        {
            if (entry.Value != desiredAbility) continue;
            Check(entry.Key.Visible && !entry.Key.Disabled, "Utility must have a visible enabled action-bar button");
            Check(entry.Key.IsVisibleInTree() && entry.Key.Icon == null
                && entry.Key.Text == (disarm ? "Disarm Trap" : "Pick Lock")
                && !string.IsNullOrEmpty(entry.Key.TooltipText), "Utility action button must display its text fallback and descriptive tooltip");
            entry.Key.EmitSignal(Button.SignalName.Pressed);
            pressed = true;
            break;
        }
        Check(pressed && Field<bool>("_awaitingPlayerAttackDirection"), "Actual HUD button must enter utility target mode");
        var cell = disarm ? _trapCell : _doorCell;
        return ToGlobal(new Vector2(cell.X * 64 + 32, cell.Y * 64));
    }

    public void ShowRenderedConfirmation()
    {
        Click(_renderedDisarm ? _trapCell : _doorCell);
        var dialog = RequireDialog(_renderedDisarm ? "Disarm Trap" : "Pick Lock");
        var foundFrame = false;
        foreach (var child in dialog.GetChildren(true))
        {
            if (child is Panel panel && panel.GetNodeOrNull<NinePatchRect>("TacticalDialogFrame") is NinePatchRect frame)
                foundFrame = frame.Texture != null;
        }
        Check(foundFrame, "Utility dialog must display the shared decorative gold frame");
    }

    public void ConfirmRenderedAction()
    {
        RequireDialog(_renderedDisarm ? "Disarm Trap" : "Pick Lock").EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        var banner = _testHud.GetNode<Label>("CombatBanner/CombatBannerLabel");
        Check(banner.Text == (_renderedDisarm ? "Trap disarmed!" : "Door unlocked!"), "Success banner must state the completed action");
        var color = banner.GetThemeColor("font_color");
        Check(color.G > color.R && color.G > color.B, "Success banner text must be green");
    }

    public Array<string> GetFailures() => _failures;

    public Array<string> Run()
    {
        try
        {
            SetupFixture();
            CheckTerrainMetadata();
            CheckTrapLootExclusion();
            Check(_thief.HasAbility("disarm-trap") && _thief.HasAbility("pick-lock"), "Saved thief must gain both utility abilities");
            var entries = (Array<Dictionary>)Call("BuildAbilityEntriesForHud", _thief, true);
            Check(entries.Count == 2, "Exploration action bar must contain both utility actions, not attacks");
            entries = (Array<Dictionary>)Call("BuildAbilityEntriesForHud", _thief, false);
            Check(entries.Count == 5, "Combat action bar must retain the thief's existing three actions");

            var door = Doors()[_doorCell];
            Check((bool)Call("IsDoorLocked", door), "Authored locked flag must survive MapLoader");
            Check(door["key_id"].AsString() == KeyId, "Authored key_id must survive MapLoader");
            Check((bool)Call("IsDoorLocked", new Dictionary
            {
                { "id", "key-only-door" }, { "locked", false }, { "key_id", KeyId }
            }), "A key_id must require a key even when locked is false");
            Check(!(bool)Call("IsDoorLocked", new Dictionary()), "Absent locked flag must default to unlocked");
            Check(!(bool)Call("IsDoorLocked", new Dictionary { { "locked", false } }), "False locked flag must be unlocked");
            Call("TryOpenDoorAtCell", _doorCell);
            Check(!(bool)Call("IsDoorOpen", door), "Ordinary door click must not bypass a lock");
            Check(!(bool)Call("CanUnitStandAt", _thief, _doorCell), "Locked doors must block movement");
            Check(!(bool)Call("HasClearLineOfSight", _thief.GridPos, new Vector2I(4, 2)), "Locked doors must block sight");
            Check(!(bool)Call("TryUnlockDoorWithKey", _thief, door), "A locked door must reject an absent key");

            AttachHud();
            Choose("pick-lock");
            Click(_doorCell);
            Check(Dialog() == null, "A keyed door must not open a pick-lock confirmation");
            Check(_testHud.GetNode<Label>("CombatBanner/CombatBannerLabel").Text.Contains("requires a key and cannot be picked"),
                "Trying to pick a keyed door must show an alert banner");

            var partyInventory = Field<List<string>>("_partyInventoryItemIds");
            var gameData = GetNode<GameData>("/root/GameData");
            gameData.Items["incorrect-key"] = new Dictionary
            {
                { "id", "incorrect-key" }, { "name", "Wrong Key" }, { "type", "key" }, { "key_id", "other-lock" }
            };
            partyInventory.Add("incorrect-key");
            Check(!(bool)Call("TryUnlockDoorWithKey", _thief, door),
                "A different key_id in shared inventory must not unlock this door");
            partyInventory.Remove("incorrect-key");
            gameData.Items.Remove("incorrect-key");
            partyInventory.Add(KeyId);
            var keyItem = gameData.GetItem(KeyId);
            Check(keyItem.Count > 0 && keyItem["type"].AsString() == "key" && keyItem["key_id"].AsString() == KeyId,
                "Rusty Key must be a passive key item with the matching key_id");
            _testHud.SetInventoryItems((Array<Dictionary>)Call("BuildInventoryItemsForHud"), new Array<string>());
            var inventoryList = (ItemList)typeof(HudController)
                .GetField("_inventoryItemList", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_testHud);
            var keyIndex = -1;
            for (var i = 0; i < inventoryList.ItemCount; i++)
            {
                if (inventoryList.GetItemMetadata(i).AsString() == KeyId)
                {
                    keyIndex = i;
                    break;
                }
            }
            Check(keyIndex >= 0, "Rusty Key must appear in the shared inventory list");
            typeof(HudController).GetMethod("UpdateInventoryPrimaryAction", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_testHud, new object[] { keyItem });
            var equipButton = (Button)typeof(HudController)
                .GetField("_equipButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_testHud);
            Check(equipButton.Disabled && equipButton.Text == "Passive",
                "Key items must remain visible in shared inventory but cannot be equipped or used");

            Call("TryOpenDoorAtCell", _doorCell);
            var keyDialog = Dialog() ?? throw new System.InvalidOperationException("Missing key unlock confirmation");
            Check(keyDialog.Title == "Unlock Door"
                && keyDialog.DialogText.Contains("Rusty Key")
                && keyDialog.GetOkButton().Text == "Unlock",
                "Key unlock confirmation must name the required item");
            keyDialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
            Check((bool)Call("IsDoorLocked", door) && partyInventory.Contains(KeyId),
                "Cancel must preserve the lock and shared key");
            Call("TryOpenDoorAtCell", _doorCell);
            (Dialog() ?? throw new System.InvalidOperationException("Missing key unlock confirmation"))
                .EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            Check(!(bool)Call("IsDoorLocked", door) && !(bool)Call("IsDoorOpen", door),
                "Confirming the key must unlock without opening the door");
            Check(partyInventory.Contains(KeyId), "Unlocking a door must not consume its key");
            Call("TryOpenDoorAtCell", _doorCell);
            Check((bool)Call("IsDoorOpen", door), "A key-unlocked door must open normally");
            Call("TryOpenDoorAtCell", _doorCell);
            Check(!(bool)Call("IsDoorOpen", door), "A key-unlocked door must close normally");
            door.Remove("key_id");
            Field<System.Collections.Generic.Dictionary<string, HashSet<string>>>("_unlockedDoorIdsByMap").Clear();

            Choose("disarm-trap");
            Click(new Vector2I(2, 0));
            Check(Dialog() == null, "Distant trap must not open a confirmation");
            Click(new Vector2I(3, 1));
            RequireDialog("Disarm Trap").EmitSignal(ConfirmationDialog.SignalName.Canceled);
            Check(Dialog() == null, "Diagonally adjacent trap must open and cancel its confirmation");
            Choose("disarm-trap");
            Click(new Vector2I(2, 3));
            Check(Dialog() == null, "Empty floor must not open a disarm confirmation");
            Click(_trapCell);
            var dialog = RequireDialog("Disarm Trap");
            Check(dialog.GetOkButton().GetThemeStylebox("normal") is StyleBoxFlat style
                && style.BorderColor == TacticalTheme.Brass, "Utility confirmation must use the shared gold border");
            var position = _thief.GridPos;
            _Input(new InputEventKey { Pressed = true, Keycode = Key.W });
            Check(_thief.GridPos == position, "Pending confirmation must block world movement");
            dialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
            Check(!InactiveTraps().Contains(TrapId()), "Cancel must leave the trap armed");
            Check(!_thief.HasUsedAbilityThisTurn, "Cancel must not consume an action");

            Choose("disarm-trap");
            Click(_trapCell);
            RequireDialog("Disarm Trap").EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            Check(InactiveTraps().Contains(TrapId()), "Confirm must disable the trap");
            Check(!_thief.HasUsedAbilityThisTurn, "Exploration utility must not consume a combat action");
            Choose("disarm-trap");
            Click(_trapCell);
            Check(Dialog() == null, "Inactive trap must not be targetable again");

            Choose("pick-lock");
            Click(new Vector2I(1, 2));
            Check(Dialog() == null, "Unlocked door must not be a pick-lock target");
            _thief.SetGridPos(new Vector2I(2, 1));
            Click(_doorCell);
            RequireDialog("Pick Lock").EmitSignal(ConfirmationDialog.SignalName.Canceled);
            Check((bool)Call("IsDoorLocked", door), "Cancel must leave the door locked");
            Choose("pick-lock");
            Click(_doorCell);
            RequireDialog("Pick Lock").EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            Check(!(bool)Call("IsDoorLocked", door), "Confirm must unlock the door");
            Check(!(bool)Call("IsDoorOpen", door), "Picking must not automatically open the door");
            Call("TryOpenDoorAtCell", _doorCell);
            Check((bool)Call("IsDoorOpen", door), "Unlocked door must open normally");
            Check((bool)Call("CanUnitStandAt", _thief, _doorCell), "Opened unlocked door must allow movement");
            Check((bool)Call("HasClearLineOfSight", _thief.GridPos, new Vector2I(4, 2)), "Opened door must allow sight");
            Call("TryOpenDoorAtCell", _doorCell);
            Check(!(bool)Call("IsDoorLocked", door), "Closing must not relock a picked door");
            _thief.SetGridPos(new Vector2I(2, 2));

            CheckPersistence();
            CheckTrapDamage();
            CheckCombatActions();
        }
        catch (System.Exception exception)
        {
            _failures.Add(exception.ToString());
        }
        finally
        {
            _testPersistence?.DeleteSaveGame(false);
        }
        return _failures;
    }

    private void SetupFixture()
    {
        Set("_gameData", GetNode<GameData>("/root/GameData"));
        var unitsRoot = new Node2D { Name = "Units" };
        AddChild(unitsRoot);
        Set("_unitsRoot", unitsRoot);
        _testTurns = new TurnManager();
        AddChild(_testTurns);
        Set("_turnManager", _testTurns);
        var maps = new Node2D { Name = "Maps" };
        AddChild(maps);
        var tiles = (TileSet)GD.Load<TileSet>("res://assets/tilesets/dungeon_terrain_64_tileset.tres").Duplicate(true);
        var atlas = (TileSetAtlasSource)tiles.GetSource(0);
        atlas.GetTileData(new Vector2I(3, 1), 0).SetCustomData("locked", true);
        atlas.GetTileData(new Vector2I(3, 1), 0).SetCustomData("key_id", KeyId);
        var baseLayer = new TileMapLayer { Name = TestMap + "-base", TileSet = tiles };
        for (var row = 0; row < 5; row++)
            for (var column = 0; column < 5; column++)
                baseLayer.SetCell(new Vector2I(column, row), 0, Vector2I.Zero);
        baseLayer.SetCell(_doorCell, 0, new Vector2I(3, 1));
        baseLayer.SetCell(new Vector2I(1, 2), 0, new Vector2I(0, 2));
        maps.AddChild(baseLayer);
        var markers = new TileMapLayer
        {
            Name = TestMap + "-markers",
            TileSet = GD.Load<TileSet>("res://assets/tilesets/map_markers_64_tileset.tres")
        };
        foreach (var cell in new[] { _trapCell, new Vector2I(2, 0), new Vector2I(3, 1) })
            markers.SetCell(cell, 1, new Vector2I(4, 0));
        maps.AddChild(markers);
        _testLoader = new MapLoader { MapsRootPath = "../Maps" };
        AddChild(_testLoader);
        Set("_mapLoader", _testLoader);
        _testPersistence = new GamePersistence(this, TestSave);
        Set("_persistence", _testPersistence);
        Call("SpawnMapEncounter", TestMap, false, Vector2I.Zero, false);
        Call("ClearPlayerUnitsFromScene");
        var config = GetNode<GameData>("/root/GameData").GetCharacterTemplate("thief").Duplicate(true);
        config["ability_ids"] = new Array<string> { "melee", "poison-strike", "defend" };
        config["grid_pos"] = new Vector2I(2, 2);
        Call("SpawnUnit", config);
        _thief = Field<Array<Unit>>("_playerUnits")[0];
        FocusThief();
    }

    private void CheckTrapLootExclusion()
    {
        Check(!(bool)Call("TryOpenExplorationInteractionAtCell", _trapCell), "Clicking an authored trap must not open a loot interaction");
        var prop = new Dictionary
        {
            { "id", "trap-loot-check" }, { "type", "trap" }, { "grid_pos", _trapCell },
            { "gold_amount", 10 }, { "loot_item_ids", new Array<string> { "dagger" } }
        };
        var props = new Array<Dictionary> { prop };
        var bags = new Array<Dictionary>();
        var opened = new HashSet<string>();
        var inventory = new List<string>();
        var gold = 0;
        var gameData = GetNode<GameData>("/root/GameData");
        var rng = new RandomNumberGenerator();
        foreach (var inactive in new[] { false, true })
        {
            if (inactive) opened.Add("trap-loot-check");
            Check(!_testLoader.TryBuildExplorationClickLootEntries(_thief, _trapCell, props, bags, opened, gameData, out var entries, out _)
                && entries.Count == 0, "Armed and inactive traps must never offer loot entries, even with loot metadata");
            Check(!_testLoader.TryResolveExplorationInteractionById(_thief, "prop:trap-loot-check", props, bags, opened,
                new HashSet<string>(), inventory, ref gold, gameData, rng, out _, out _, out var changed) && !changed,
                "Direct trap prop interaction must be rejected without changing state");
            Check(opened.Count == (inactive ? 1 : 0) && bags.Count == 0 && inventory.Count == 0 && gold == 0,
                "Attempting to loot a trap must not disarm it or award loot");
        }
        opened.Clear();
        prop["type"] = "chest";
        prop["grid_pos"] = new Vector2I(3, 3);
        prop["loot_item_ids"] = new Array<string> { KeyId };
        Check(_testLoader.BuildNearbyLootEntries(_thief, props, bags, opened, gameData).Count > 0,
            "Diagonally adjacent containers must appear in nearby interactions");
        Check(_testLoader.TryBuildExplorationClickLootEntries(_thief, new Vector2I(3, 3), props, bags, opened, gameData, out var chestEntries, out _)
            && chestEntries.Count > 0, "Normal containers must still offer loot interactions");
        Check(_testLoader.TryResolveExplorationInteractionById(_thief, "prop:trap-loot-check", props, bags, opened,
            new HashSet<string>(), inventory, ref gold, gameData, rng, out _, out _, out var chestChanged)
            && chestChanged && bags.Count == 1, "Normal containers must still reveal loot");
        Check(_testLoader.GetBagItemIds(bags[0]).Contains(KeyId), "Chest loot configuration must support key item IDs");
        Check(_testLoader.TryResolveExplorationInteractionById(_thief, $"bag-item:{bags[0]["id"]}:0", props, bags, opened,
            new HashSet<string>(), inventory, ref gold, gameData, rng, out _, out _, out var lootedKey)
            && lootedKey && inventory.Contains(KeyId), "Looting a key must add it to shared party inventory");
    }

    private void CheckTerrainMetadata()
    {
        foreach (var name in new[] { "cave", "dungeon", "forest", "graveyard" })
        {
            var tiles = GD.Load<TileSet>($"res://assets/tilesets/{name}_terrain_64_tileset.tres");
            var layer = tiles.GetCustomDataLayerByName("locked");
            Check(layer >= 0 && tiles.GetCustomDataLayerType(layer) == Variant.Type.Bool,
                $"{name} terrain must expose a boolean locked layer");
            var keyLayer = tiles.GetCustomDataLayerByName("key_id");
            Check(keyLayer >= 0 && tiles.GetCustomDataLayerType(keyLayer) == Variant.Type.String,
                $"{name} terrain must expose a string key_id layer");
        }
    }

    private void CheckPersistence()
    {
        var trapId = TrapId();
        Set("_currentMapId", "another-map");
        Call("LoadMapInteractionStateForCurrentMap");
        Check((bool)Call("IsDoorLocked", Doors()[_doorCell]) && !InactiveTraps().Contains(trapId), "State must be scoped by map");
        Set("_currentMapId", TestMap);
        Call("LoadMapInteractionStateForCurrentMap");
        Check(!(bool)Call("IsDoorLocked", Doors()[_doorCell]) && InactiveTraps().Contains(trapId), "Returning to map must restore state");
        _testPersistence.PersistSaveGame(false);
        Field<System.Collections.Generic.Dictionary<string, HashSet<string>>>("_unlockedDoorIdsByMap").Clear();
        Field<System.Collections.Generic.Dictionary<string, HashSet<string>>>("_openedPropIdsByMap").Clear();
        InactiveTraps().Clear();
        Check(_testPersistence.TryLoadSaveGame(false), "Test save must load successfully");
        _thief = Field<Array<Unit>>("_playerUnits")[0];
        FocusThief();
        Check(!(bool)Call("IsDoorLocked", Doors()[_doorCell]), "Picked lock must survive save/load");
        Check(InactiveTraps().Contains(trapId), "Disarmed trap must survive save/load");
        Check(_thief.HasAbility("disarm-trap") && _thief.HasAbility("pick-lock"), "Abilities must survive save/load");
    }

    private void CheckTrapDamage()
    {
        var trap = Trap();
        var health = _thief.HitPoints;
        Check(!(bool)Call("TryTriggerTrap", trap, _thief) && _thief.HitPoints == health, "Disarmed trap must not hurt the thief");
        var enemy = GD.Load<PackedScene>("res://scenes/Unit.tscn").Instantiate<Unit>();
        AddChild(enemy);
        enemy.Setup(new Dictionary { { "id", "test-enemy" }, { "team", "enemy" }, { "hit_points", 20 }, { "max_hit_points", 20 } });
        Check(!(bool)Call("TryTriggerTrap", trap, enemy) && enemy.HitPoints == 20, "Disarmed trap must not hurt enemies");
        InactiveTraps().Remove(TrapId());
        Check((bool)Call("TryTriggerTrap", trap, enemy) && enemy.HitPoints < 20, "Armed trap must still hurt an enemy");
        InactiveTraps().Remove(TrapId());
        Call("CancelAttackMode", false);
        Check((bool)Call("TryMoveExplorationParty", Vector2I.Up), "Thief must be able to step onto an armed trap");
        Check(_thief.HitPoints < health, "Walking onto an armed trap must still damage the thief");
        _thief.SetGridPos(new Vector2I(2, 2));
        FocusThief();
        enemy.QueueFree();
    }

    private void CheckCombatActions()
    {
        var flowField = PrivateField("_flowState");
        flowField.SetValue(this, System.Enum.Parse(flowField.FieldType, "Combat"));
        Set("_mouseMoveInputLockedUntilMs", (ulong)0);
        Doors()[_doorCell].Remove("key_id");
        _testTurns.SetupTurnOrder(new Array<Unit> { _thief });
        _thief.ResetTurnResources();
        InactiveTraps().Remove(TrapId());
        Field<System.Collections.Generic.Dictionary<string, HashSet<string>>>("_unlockedDoorIdsByMap").Clear();
        Choose("disarm-trap");
        Click(_trapCell);
        RequireDialog("Disarm Trap").EmitSignal(ConfirmationDialog.SignalName.Canceled);
        Check(!_thief.HasUsedAbilityThisTurn && !InactiveTraps().Contains(TrapId()), "Combat cancellation must leave action and trap unchanged");
        Choose("disarm-trap");
        Click(_trapCell);
        RequireDialog("Disarm Trap").EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Check(_thief.HasUsedAbilityThisTurn && InactiveTraps().Contains(TrapId()), "Combat disarm must consume one action and disable trap");
        Call("OnHudAbilityPressed", "pick-lock");
        Check(!Field<bool>("_awaitingPlayerAttackDirection"), "Spent action must prevent starting Pick Lock");
        _thief.ResetTurnResources();
        Choose("pick-lock");
        Click(_doorCell);
        RequireDialog("Pick Lock").EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Check(_thief.HasUsedAbilityThisTurn && !(bool)Call("IsDoorLocked", Doors()[_doorCell]), "Combat lock-picking must consume one action and unlock");
        Call("TryOpenDoorAtCell", _doorCell);
        Check((bool)Call("IsDoorOpen", Doors()[_doorCell]), "Picked doors must be openable in combat");
    }

    private void FocusThief()
    {
        Set("_selectedCharacterUnitId", _thief.UnitId);
        Set("_explorerUnit", _thief);
        Set("_fogVisibilityActor", null);
        Call("UpdateFogOfWar");
    }

    private void AttachHud()
    {
        _hudLayer = new CanvasLayer();
        AddChild(_hudLayer);
        _testHud = GD.Load<PackedScene>("res://ui/HUD.tscn").Instantiate<HudController>();
        _hudLayer.AddChild(_testHud);
        Set("_hud", _testHud);
        Call("SyncHudFromGameState");
    }

    private void Choose(string abilityId)
    {
        Call("OnHudAbilityPressed", abilityId);
        Check(Field<bool>("_awaitingPlayerAttackDirection"), abilityId + " must enter target mode");
    }

    private void Click(Vector2I cell) => Call("HandleMouseAttackInput", new InputEventMouseButton
    {
        Pressed = true,
        ButtonIndex = MouseButton.Left,
        GlobalPosition = ToGlobal(new Vector2(cell.X * 64 + 32, cell.Y * 64 + 32))
    });

    private ConfirmationDialog Dialog()
    {
        foreach (var child in GetChildren())
            if (child is ConfirmationDialog dialog && !dialog.IsQueuedForDeletion()) return dialog;
        return null;
    }

    private ConfirmationDialog RequireDialog(string title)
    {
        var dialog = Dialog() ?? throw new System.InvalidOperationException("Missing " + title + " confirmation");
        Check(dialog.Title == title, "Confirmation title must match the utility");
        return dialog;
    }

    private Dictionary Trap()
    {
        foreach (var prop in Field<Array<Dictionary>>("_mapProps"))
            if (prop["grid_pos"].AsVector2I() == _trapCell) return prop;
        throw new System.InvalidOperationException("Missing fixture trap");
    }

    private string TrapId() => Trap()["id"].AsString();
    private HashSet<string> InactiveTraps() => Field<HashSet<string>>("_openedPropIds");
    private System.Collections.Generic.Dictionary<Vector2I, Dictionary> Doors() => Field<System.Collections.Generic.Dictionary<Vector2I, Dictionary>>("_mapDoorByCell");
    private void Check(bool condition, string message) { if (!condition) _failures.Add(message); }
    private static FieldInfo PrivateField(string name) => typeof(BattleController).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
    private T Field<T>(string name) => (T)PrivateField(name).GetValue(this);
    private void Set(string name, object value) => PrivateField(name).SetValue(this, value);
    private object Call(string name, params object[] args) => typeof(BattleController).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(this, args);
}