using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Reflection;

public partial class ActionFeedbackChecks : BattleController
{
    private readonly Array<string> _failures = new();
    private Unit _actor;
    private Unit _ally;
    private Unit _enemy;
    private HudController _testHud;
    private MapLoader _testLoader;
    private Label _banner;
    private ItemList _log;
    private Dictionary _door;
    private readonly Vector2I _doorCell = new(3, 3);

    public override void _Ready() { }
    public override void _ExitTree() { }

    public Array<string> RunIconChecks()
    {
        SetupFixture();
        CheckPoisonStrikeIcon();
        return _failures;
    }

    public Array<string> Run()
    {
        SetupFixture();
        CheckPoisonStrikeIcon();
        CheckResources();
        CheckTargets();
        CheckUtilities();
        CheckMovement();
        CheckInteractions();
        CheckAllowedActions();
        return _failures;
    }

    private void CheckPoisonStrikeIcon()
    {
        var icon = (Texture2D)typeof(HudController)
            .GetMethod("GetGameIcon", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { "poison-strike" });
        Check(icon is AtlasTexture atlas && atlas.Region == new Rect2(128, 192, 32, 32),
            "Poison Strike must use the fifth column of the seventh atlas row");
        _testHud.SetAbilityButtons(new Array<Dictionary>
        {
            new() { { "id", "poison-strike" }, { "is_enabled", 1 } }
        }, true);
        Check(AbilityButton("poison-strike").Icon == icon,
            "Poison Strike action-bar button must use the shared icon mapping");
        Call("SyncHudFromGameState");
    }

    private void SetupFixture()
    {
        Set("_gameData", GetNode<GameData>("/root/GameData"));
        Set("_gridWidth", 16);
        Set("_gridHeight", 8);
        Set("_currentMapId", "feedback-test");
        var units = new Node2D { Name = "Units" };
        AddChild(units);
        Set("_unitsRoot", units);
        _actor = NewUnit(units, "caster", "player", new Vector2I(2, 2));
        _ally = NewUnit(units, "ally", "player", new Vector2I(2, 4));
        _enemy = NewUnit(units, "enemy", "enemy", new Vector2I(5, 2));
        Field<Array<Unit>>("_allUnits").AddRange(new Array<Unit> { _actor, _ally, _enemy });
        Field<Array<Unit>>("_playerUnits").AddRange(new Array<Unit> { _actor, _ally });
        Field<Array<Unit>>("_enemyUnits").Add(_enemy);
        Field<HashSet<string>>("_activeCombatEnemyUnitIds").Add(_enemy.UnitId);
        var turns = new TurnManager();
        AddChild(turns);
        Set("_turnManager", turns);
        turns.SetupTurnOrder(new Array<Unit> { _actor, _ally, _enemy });
        Set("_selectedCharacterUnitId", _actor.UnitId);
        Set("_explorerUnit", _actor);
        _door = new Dictionary
        {
            { "id", "door" }, { "cell", _doorCell }, { "locked", true }, { "is_open", false }
        };
        Field<System.Collections.Generic.Dictionary<Vector2I, Dictionary>>("_mapDoorByCell")[_doorCell] = _door;
        Field<Array<Dictionary>>("_mapDoors").Add(_door);
        Field<Array<Dictionary>>("_mapProps").Add(new Dictionary
        {
            { "id", "trap" }, { "type", "trap" }, { "grid_pos", new Vector2I(1, 2) }
        });
        _testLoader = new MapLoader();
        Set("_mapLoader", _testLoader);
        var layer = new CanvasLayer();
        AddChild(layer);
        _testHud = GD.Load<PackedScene>("res://ui/HUD.tscn").Instantiate<HudController>();
        layer.AddChild(_testHud);
        Set("_hud", _testHud);
        _testHud.AbilityPressed += id => Call("OnHudAbilityPressed", id);
        _banner = _testHud.GetNode<Label>("CombatBanner/CombatBannerLabel");
        _log = HudField<ItemList>("_combatLog");
        Reset();
    }

    private Unit NewUnit(Node parent, string id, string team, Vector2I cell)
    {
        var unit = GD.Load<PackedScene>("res://scenes/Unit.tscn").Instantiate<Unit>();
        parent.AddChild(unit);
        unit.Setup(Config(id, team, cell));
        return unit;
    }

    private static Dictionary Config(string id, string team, Vector2I cell, int mana = 30) => new()
    {
        { "id", id }, { "name", id }, { "team", team }, { "class_id", "wizard" },
        { "grid_pos", cell }, { "hit_points", 50 }, { "max_hit_points", 50 },
        { "magic_points", mana }, { "max_magic_points", 30 },
        { "initiative", id == "caster" ? 1000 : 1 },
        { "primary_ability_id", "magic-missile" },
        { "ability_ids", new Array<string>
            { "magic-missile", "lesser-heal", "fireball", "defend", "charge", "sleep",
                "pin", "poison-strike", "melee", "pick-lock", "disarm-trap" } }
    };

    private void Reset(int mana = 30, bool exploration = false)
    {
        ResetBanner();
        var flow = PrivateField("_flowState");
        flow.SetValue(this, Enum.Parse(flow.FieldType, exploration ? "Exploration" : "Combat"));
        _actor.ClearStatusEffectsByScope("combat_only");
        _actor.ClearStatusEffectsByScope("persistent");
        _actor.ClearAbilityCooldowns();
        _actor.Setup(Config("caster", "player", new Vector2I(2, 2), mana));
        _enemy.Setup(Config("enemy", "enemy", new Vector2I(5, 2)));
        _ally.Setup(Config("ally", "player", new Vector2I(2, 4)));
        _ally.ClearStatusEffectsByScope("combat_only");
        Field<TurnManager>("_turnManager").SetupTurnOrder(new Array<Unit> { _actor, _ally, _enemy });
        _actor.TrySpendMagicPoints(_actor.MagicPoints - mana);
        Field<HashSet<Vector2I>>("_walkableCells").Clear();
        Field<HashSet<Vector2I>>("_wallCellSet").Clear();
        Field<HashSet<string>>("_openedPropIds").Clear();
        Field<HashSet<string>>("_openedDoorIds").Clear();
        Field<System.Collections.Generic.Dictionary<string, HashSet<string>>>("_unlockedDoorIdsByMap").Clear();
        Field<System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>>("_equippedItemsByUnitId").Clear();
        _door["locked"] = true;
        _door["is_open"] = false;
        Set("_awaitingPlayerAttackDirection", false);
        Set("_mouseMoveInputLockedUntilMs", 0UL);
        RefreshFog();
        Call("SetSelectedAbilityId", _actor, "magic-missile");
        Call("SyncHudFromGameState");
    }

    private void CheckResources()
    {
        Reset(0);
        var button = AbilityButton("magic-missile");
        Check(!button.Disabled && button.SelfModulate.R < 0.8f,
            "Unavailable ability must be dimmed but accept attempted clicks");
        ExpectFailure(() => button.EmitSignal(Button.SignalName.Pressed), "Out of mana - cannot cast Magic Missile.");
        Reset(2);
        ExpectFailure(() => Call("OnHudAbilityPressed", "magic-missile"),
            "Not enough mana to cast Magic Missile - requires 3 MP, have 2.");
        Reset(0);
        ExpectFailure(() => _Input(new InputEventKey { Pressed = true, Keycode = Key.F }),
            "Out of mana - cannot cast Magic Missile.");
        Reset();
        _actor.MarkAbilityUsed("defend");
        ExpectFailure(() => Call("OnHudAbilityPressed", "defend"), "No action points left to use Defend.");
        Reset();
        _actor.MarkAbilityCooldownOnly("magic-missile", 2);
        ExpectFailure(() => AbilityButtonAfterSync("magic-missile").EmitSignal(Button.SignalName.Pressed),
            "Magic Missile is on cooldown - 2 turns remaining.");
        Reset();
        _actor.ApplyStatusEffect("sleep", "Asleep", false, 2, preventActions: true);
        ExpectFailure(() => Call("OnHudAbilityPressed", "charge"), "caster cannot act while asleep.");
        Reset();
        ExpectFailure(() => Call("OnHudAbilityPressed", "pin"), "Equip a ranged weapon to use Pin.");
        Reset();
        Field<System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>>("_equippedItemsByUnitId")[_actor.UnitId]
            = new System.Collections.Generic.Dictionary<string, string> { { "2-handed", "short-bow" } };
        ExpectFailure(() => Call("OnHudAbilityPressed", "melee"), "Melee Attack requires a melee weapon or empty hands.");
        Reset();
        _actor.Setup(new Dictionary
        {
            { "id", "caster" }, { "name", "caster" }, { "team", "player" },
            { "grid_pos", new Vector2I(2, 2) }, { "ability_ids", new Array<string> { "melee" } }
        });
        ExpectFailure(() => Call("OnHudAbilityPressed", "fireball"), "caster has not learned Fireball.");
    }

    private void CheckTargets()
    {
        Reset();
        ExpectTargetFailure("magic-missile", new Vector2I(8, 2), "Out of range.");
        Reset();
        _enemy.SetGridPos(new Vector2I(8, 2));
        RefreshFog();
        Call("SetSelectedAbilityId", _actor, "magic-missile");
        Set("_awaitingPlayerAttackDirection", true);
        ExpectFailure(() => Call("HandlePlayerAttackDirectionInput", new InputEventKey
            { Pressed = true, Keycode = Key.D }), "Out of range.");
        Reset();
        Field<HashSet<Vector2I>>("_wallCellSet").Add(new Vector2I(3, 2));
        RefreshFog();
        ExpectTargetFailure("magic-missile", _enemy.GridPos, "No line of sight.");
        Reset();
        _enemy.SetGridPos(new Vector2I(4, 2));
        Field<HashSet<Vector2I>>("_wallCellSet").Add(new Vector2I(3, 2));
        RefreshFog();
        Call("SetSelectedAbilityId", _actor, "magic-missile");
        Set("_awaitingPlayerAttackDirection", true);
        ExpectFailure(() => Call("HandlePlayerAttackDirectionInput", new InputEventKey
            { Pressed = true, Keycode = Key.D }), "No line of sight.");
        Reset();
        ExpectTargetFailure("magic-missile", _ally.GridPos, "Magic Missile must target an enemy.");
        Reset();
        ExpectTargetFailure("magic-missile", new Vector2I(4, 1), "No enemy at that location.");
        Reset();
        ExpectTargetFailure("lesser-heal", _enemy.GridPos, "Select a living ally to heal.");
        Reset();
        ExpectTargetFailure("lesser-heal", _ally.GridPos, "ally is already at full health.");
        Reset();
        ExpectTargetFailure("sleep", new Vector2I(0, 5), "No living targets in the Sleep area.");
        Reset();
        _enemy.Setup(new Dictionary
        {
            { "id", "enemy" }, { "team", "enemy" }, { "race", "undead" }, { "grid_pos", new Vector2I(5, 2) }
        });
        ExpectTargetFailure("sleep", _enemy.GridPos, "Undead are immune to Sleep.");
        Reset();
        _enemy.SetGridPos(new Vector2I(3, 2));
        ExpectTargetFailure("charge", _enemy.GridPos, "Cannot charge an adjacent target.");
        Reset();
        foreach (var cell in new[] { new Vector2I(4, 1), new Vector2I(4, 2), new Vector2I(4, 3),
            new Vector2I(5, 1), new Vector2I(5, 3), new Vector2I(6, 1), new Vector2I(6, 2), new Vector2I(6, 3) })
            Field<HashSet<Vector2I>>("_wallCellSet").Add(cell);
        RefreshFog();
        ExpectTargetFailure("charge", _enemy.GridPos, "Target area is not visible.");
        Reset();
        Field<HashSet<Vector2I>>("_walkableCells").Add(_actor.GridPos);
        Field<HashSet<Vector2I>>("_walkableCells").Add(_enemy.GridPos);
        RefreshFog();
        ExpectTargetFailure("charge", _enemy.GridPos, "Cannot charge - no reachable space beside the target.");
    }

    private void CheckUtilities()
    {
        Reset(exploration: true);
        ExpectFailure(() => Call("TryOpenDoorAtCell", _doorCell), "Door locked - use Pick Lock.");
        Reset(exploration: true);
        _actor.SetGridPos(new Vector2I(1, 2));
        RefreshFog();
        ExpectFailure(() => Call("TryOpenDoorAtCell", _doorCell), "Move adjacent to the door.");
        Reset(exploration: true);
        _door["locked"] = false;
        _door["is_open"] = true;
        _ally.SetGridPos(_doorCell);
        RefreshFog();
        ExpectFailure(() => Call("TryOpenDoorAtCell", _doorCell), "Cannot close the door - doorway occupied.");
        Reset(exploration: true);
        ExpectTargetFailure("pick-lock", new Vector2I(1, 3), "Select a locked door.");
        Reset(exploration: true);
        _door["locked"] = false;
        ExpectTargetFailure("pick-lock", _doorCell, "This door is already unlocked.");
        Reset(exploration: true);
        ExpectTargetFailure("disarm-trap", new Vector2I(1, 3), "No trap at that location.");
        Reset(exploration: true);
        Field<HashSet<string>>("_openedPropIds").Add("trap");
        ExpectTargetFailure("disarm-trap", new Vector2I(1, 2), "This trap is already disarmed.");
        Reset(exploration: true);
        ExpectTargetFailure("pick-lock", new Vector2I(5, 3), "Move adjacent to use Pick Lock.");
    }

    private void CheckMovement()
    {
        Reset();
        _actor.TrySpendMovement(_actor.RemainingMovement);
        ExpectFailure(() => _Input(new InputEventKey { Pressed = true, Keycode = Key.W }), "No movement points remaining.");
        Reset();
        _actor.ApplyStatusEffect("pinned", "Pinned", false, 2, preventMovement: true);
        ExpectFailure(() => MoveClick(new Vector2I(2, 1)), "caster cannot move while pinned.");
        Reset();
        Field<HashSet<Vector2I>>("_wallCellSet").Add(new Vector2I(2, 1));
        ExpectFailure(() => MoveClick(new Vector2I(2, 1)), "Path blocked.");
        Reset();
        Field<HashSet<Vector2I>>("_wallCellSet").Add(new Vector2I(2, 1));
        ExpectTargetFailure("melee", new Vector2I(2, 1), "Path blocked.");
        Reset();
        ExpectFailure(() => MoveClick(_ally.GridPos), "That space is occupied.");
        Reset();
        ExpectFailure(() => MoveClick(new Vector2I(7, 1)), "Not enough movement points to reach that location.");
        Reset();
        for (var row = 0; row < 8; row++) Field<HashSet<Vector2I>>("_wallCellSet").Add(new Vector2I(3, row));
        ExpectFailure(() => MoveClick(new Vector2I(4, 4)), "No path to that location.");
        Reset(exploration: true);
        Field<HashSet<Vector2I>>("_wallCellSet").Add(new Vector2I(2, 1));
        ExpectFailure(() => _Input(new InputEventKey { Pressed = true, Keycode = Key.W }), "Path blocked.");
    }

    private void CheckInteractions()
    {
        Reset(exploration: true);
        Field<Array<Dictionary>>("_mapProps").Add(new Dictionary
        {
            { "id", "chest" }, { "name", "Chest" }, { "type", "chest" },
            { "grid_pos", new Vector2I(6, 4) }, { "gold_amount", 1 }
        });
        ExpectFailure(() => Call("TryOpenExplorationInteractionAtCell", new Vector2I(6, 4)),
            "Move adjacent to interact with that object.");
        ExpectFailure(() => Call("TryExecuteExplorationInteractionById", _actor, "prop:chest"),
            "Move adjacent to interact with Chest.");
        Reset(exploration: true);
        Field<Array<Dictionary>>("_lootBags").Add(new Dictionary
        {
            { "id", "empty-bag" }, { "grid_pos", new Vector2I(1, 2) }, { "gold_amount", 0 }
        });
        ExpectFailure(() => Call("TryExecuteExplorationInteractionById", _actor, "bag-all:empty-bag"),
            "This loot bag is empty.");
        Reset(exploration: true);
        ExpectFailure(() => Call("TryExecuteExplorationInteractionById", _actor, "bag-gold:empty-bag"),
            "This loot bag contains no gold.");
        Reset(exploration: true);
        Field<List<string>>("_partyInventoryItemIds").Add("fireball-scroll");
        ExpectFailure(() => Call("OnHudUseItemRequested", "fireball-scroll"), "caster already knows Fireball.");
        Reset(exploration: true);
        _actor.Setup(new Dictionary
        {
            { "id", "caster" }, { "name", "caster" }, { "team", "player" }, { "class_id", "knight" }
        });
        ExpectFailure(() => Call("OnHudUseItemRequested", "fireball-scroll"), "caster cannot use Fireball Scroll - requires wizard.");
        Reset(exploration: true);
        ExpectFailure(() => Call("OnHudUseItemRequested", "dagger"), "That item is no longer available.");
        Reset(exploration: true);
        Field<System.Collections.Generic.Dictionary<string, List<string>>>("_vendorInventoryItemIdsById")["test-vendor"]
            = new List<string> { "dagger" };
        Set("_partyGold", 0);
        var expectedPrice = (int)Call("GetItemBuyPrice", "test-vendor", "dagger");
        ExpectFailure(() => Call("TryBuyVendorItem", "test-vendor", "dagger"),
            $"Not enough gold - requires {expectedPrice} gp, have 0.");
        Reset(exploration: true);
        ExpectFailure(() => Call("TryBuyVendorItem", "test-vendor", "missing"), "That item is out of stock.");
        Reset(exploration: true);
        ExpectFailure(() => Call("TrySellVendorItem", "test-vendor", "missing"), "That item is not available to sell.");
        Reset(exploration: true);
        Field<List<string>>("_partyInventoryItemIds").Add("dagger");
        Field<System.Collections.Generic.Dictionary<string, int>>("_vendorGoldById")["test-vendor"] = 0;
        var vendorName = (string)Call("GetVendorDisplayName", "test-vendor");
        ExpectFailure(() => Call("TrySellVendorItem", "test-vendor", "dagger"), $"{vendorName} does not have enough gold.");
        Reset(exploration: true);
        var itemName = (string)Call("GetItemName", "dagger");
        ExpectFailure(() => Call("OnHudUseItemRequested", "dagger"), $"{itemName} cannot be used.");
        Reset(exploration: true);
        Field<Array<Unit>>("_playerUnits").Remove(_ally);
        ExpectFailure(() => Call("OnHudReserveStoreRequested", _actor.UnitId), "At least one member must remain in the party.");
        Field<Array<Unit>>("_playerUnits").Add(_ally);
        Reset(exploration: true);
        ExpectFailure(() => Call("BeginReserveRecruitInteraction", _actor, "missing", ""),
            "That reserve member is no longer available.");
    }

    private void CheckAllowedActions()
    {
        Reset();
        _actor.MarkAbilityUsed("defend");
        Call("OnHudAbilityPressed", "charge");
        Check(Field<bool>("_awaitingPlayerAttackDirection"), "Charge must remain available after the normal action is spent");
        Reset();
        Call("SetSelectedAbilityId", _actor, "fireball");
        var mana = _actor.MagicPoints;
        Call("TryResolvePlayerActionAtCell", _actor, new Vector2I(2, 7));
        Check(_actor.MagicPoints == mana - 10 && _actor.HasUsedAbilityThisTurn,
            "Fireball must still be allowed on empty areas and spend its normal resources");
        Reset();
        var health = _enemy.HitPoints;
        Call("TryResolvePlayerActionAtCell", _actor, _enemy.GridPos);
        Check(_enemy.HitPoints < health && _actor.MagicPoints == 27 && _actor.HasUsedAbilityThisTurn,
            "Valid attacks must still apply damage and consume mana/action");
        Reset();
        Call("SetSelectedAbilityId", _actor, "sleep");
        Call("TryResolvePlayerActionAtCell", _actor, _ally.GridPos);
        Check(_ally.HasStatusEffect("sleep"), "Sleep must still allow affecting allies");
        Reset();
        var logCount = _log.ItemCount;
        Call("CanUseActionProfileNow", _actor, Call("ResolveActionProfile", _actor, "pin"));
        Check(_banner.Text == "" && _log.ItemCount == logCount, "Readiness/AI checks must never display failure banners");
        Call("SetSelectedAbilityId", _actor, "magic-missile");
        Set("_awaitingPlayerAttackDirection", true);
        Call("HandlePlayerAttackDirectionInput", new InputEventKey { Pressed = true, Keycode = Key.Escape });
        Check(_banner.Text == "", "Canceling targeting must not show a failure");
    }

    public void StartQueueChecks()
    {
        ResetBanner();
        _testHud.ShowActionFailureBanner("Out of range.");
        var tween = HudField<Tween>("_combatBannerTween");
        for (var i = 0; i < 10; i++) _testHud.ShowActionFailureBanner("Out of range.");
        Check(ReferenceEquals(tween, HudField<Tween>("_combatBannerTween"))
            && HudField<Queue<(string Text, Color Accent)>>("_combatBannerQueue").Count == 0,
            "Identical warnings must not restart the tween or build a queue");
        ResetBanner();
        _testHud.ShowCombatBanner("COMBAT START", Colors.Orange);
        _testHud.ShowCombatBanner("ZONE", Colors.Green);
        for (var i = 0; i < 10; i++) _testHud.ShowActionFailureBanner("Out of range.");
        Check(HudField<Queue<(string Text, Color Accent)>>("_combatBannerQueue").Count == 1
            && HudField<string>("_pendingActionFailureText") == "Out of range.",
            "Failures behind an existing banner must occupy only one pending slot");
    }

    public void CheckPendingBanner()
    {
        Check(_banner.Text == "Out of range." && HudField<bool>("_activeBannerIsFailure"),
            "Pending failure must display after the current normal banner");
        var color = _banner.GetThemeColor("font_color");
        Check(color.R > color.G && color.R > color.B, "Failure banner text must be red");
    }

    public void CheckPreservedBanner()
    {
        Check(_banner.Text == "ZONE", "Normal queued banners must survive failure feedback");
    }

    public Array<string> GetFailures() => _failures;

    public void ShowLongFailure()
    {
        ResetBanner();
        _testHud.ShowActionFailureBanner("Not enough mana to cast Magic Missile - requires 3 MP, have 2.");
    }

    public void CheckBannerLayout()
    {
        var panel = _testHud.GetNode<PanelContainer>("CombatBanner");
        Check(_banner.GetLineCount() > 1 && _banner.GetVisibleLineCount() == _banner.GetLineCount(),
            "Long failure messages must wrap without hiding any lines");
        Check(panel.Size.X <= _testHud.Size.X && _banner.Size.Y <= panel.Size.Y,
            "Failure banners must stay within the HUD and fit their text");
    }

    private void ExpectTargetFailure(string ability, Vector2I cell, string message)
    {
        Call("SetSelectedAbilityId", _actor, ability);
        Set("_awaitingPlayerAttackDirection", true);
        ExpectFailure(() => Call("HandleMouseAttackInput", new InputEventMouseButton
        {
            Pressed = true, ButtonIndex = MouseButton.Left,
            GlobalPosition = ToGlobal(new Vector2(cell.X * 64 + 32, cell.Y * 64 + 32))
        }), message);
        Check(Field<bool>("_awaitingPlayerAttackDirection"), "Rejected target must remain in targeting mode");
    }

    private void ExpectFailure(Action attempt, string message)
    {
        ResetBanner();
        var logCount = _log.ItemCount;
        var mana = _actor.MagicPoints;
        var position = _actor.GridPos;
        var movement = _actor.RemainingMovement;
        var actionSpent = _actor.HasUsedAbilityThisTurn;
        attempt();
        Check(_banner.Text == message, $"Expected '{message}', got '{_banner.Text}'");
        Check(_log.ItemCount == logCount, "Rejected attempts must not add combat-log entries");
        Check(_actor.MagicPoints == mana && _actor.GridPos == position
            && _actor.RemainingMovement == movement && _actor.HasUsedAbilityThisTurn == actionSpent,
            "Rejected attempts must not change unit resources or position");
        var color = _banner.GetThemeColor("font_color");
        Check(color.R > color.G && color.R > color.B, "Rejected attempts must use red banner text");
    }

    private void MoveClick(Vector2I cell) => Call("HandleMouseMoveInput", new InputEventMouseButton
    {
        ButtonIndex = MouseButton.Left, Pressed = false,
        GlobalPosition = ToGlobal(new Vector2(cell.X * 64 + 32, cell.Y * 64 + 32))
    });

    private Button AbilityButtonAfterSync(string id)
    {
        Call("SyncHudFromGameState");
        return AbilityButton(id);
    }

    private Button AbilityButton(string id)
    {
        foreach (var pair in HudField<System.Collections.Generic.Dictionary<Button, string>>("_abilityIdsByButton"))
            if (pair.Value == id) return pair.Key;
        throw new InvalidOperationException($"Missing {id} button");
    }

    private void RefreshFog()
    {
        Set("_fogVisibilityActor", null);
        Call("UpdateFogOfWar");
    }

    private void ResetBanner()
    {
        HudField<Tween>("_combatBannerTween")?.Kill();
        HudPrivateField("_combatBannerTween").SetValue(_testHud, null);
        HudPrivateField("_activeBannerIsFailure").SetValue(_testHud, false);
        HudPrivateField("_pendingActionFailureText").SetValue(_testHud, "");
        HudField<Queue<(string Text, Color Accent)>>("_combatBannerQueue").Clear();
        _banner.Text = "";
    }

    public void Cleanup() => _testLoader.Free();
    private void Check(bool condition, string message) { if (!condition) _failures.Add(message); }
    private static FieldInfo PrivateField(string name) => typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static FieldInfo HudPrivateField(string name) => typeof(HudController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private T HudField<T>(string name) => (T)HudPrivateField(name).GetValue(_testHud);
    private T Field<T>(string name) => (T)PrivateField(name).GetValue(this);
    private void Set(string name, object value) => PrivateField(name).SetValue(this, value);
    private object Call(string name, params object[] args) => typeof(BattleController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this, args);
}
