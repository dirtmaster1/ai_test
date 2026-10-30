using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Reflection;

public partial class DefeatRecoveryChecks : BattleController
{
    private const string MainSavePath = "user://defeat-recovery-main-test.json";
    private const string RecoverySavePath = "user://defeat-recovery-checkpoint-test.json";
    private readonly Array<string> _failures = new();
    private GamePersistence _mainSave;
    private GamePersistence _recoverySave;

    public override void _Ready() { }

    public override void _ExitTree()
    {
        Call("CloseDefeatRecoveryDialog");
        _mainSave?.DeleteSaveGame(false);
        _recoverySave?.DeleteSaveGame(false);
    }

    public Array<string> Run()
    {
        try
        {
            SetupFixture();
            CheckCombatDefeatShowsRecoveryChoices();
            CheckLoadRecovery();
            CheckRespawn();
            CheckTrapDefeatShowsRecoveryChoices();
            CheckRestoringDefeatSaveShowsChoices();
        }
        catch (System.Exception exception)
        {
            _failures.Add(exception.ToString());
        }
        return _failures;
    }

    public void CollectManagedWrappers()
    {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
    }

    private void SetupFixture()
    {
        SetField("_currentMapId", "forest-town");
        SetField("_gridWidth", 24);
        SetField("_gridHeight", 24);
        SetField("_gameData", GetNode<GameData>("/root/GameData"));

        var units = new Node2D { Name = "Units" };
        AddChild(units);
        SetField("_unitsRoot", units);
        var player = AddUnit(units, "recovery-test-warrior", "player", new Vector2I(9, 9));
        AddUnit(units, "recovery-test-cleric", "player", new Vector2I(9, 10));
        AddUnit(units, "recovery-test-bandit", "enemy", new Vector2I(12, 9));
        Field<Array<Unit>>("_playerUnits")[0].SetActive(false);
        SetField("_explorerUnit", player);
        var turns = new TurnManager();
        AddChild(turns);
        SetField("_turnManager", turns);

        var mainScene = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
        var maps = mainScene.GetNode<Node2D>("Gameworld/Maps");
        maps.GetParent().RemoveChild(maps);
        mainScene.Free();
        AddChild(maps);
        var mapLoader = new MapLoader { MapsRootPath = "../Maps" };
        AddChild(mapLoader);
        SetField("_mapLoader", mapLoader);

        _mainSave = new GamePersistence(this, MainSavePath);
        _recoverySave = new GamePersistence(this, RecoverySavePath);
        _mainSave.DeleteSaveGame(false);
        _recoverySave.DeleteSaveGame(false);
        SetField("_persistence", _mainSave);
        SetField("_recoveryPersistence", _recoverySave);
    }

    private void CheckCombatDefeatShowsRecoveryChoices()
    {
        var units = Field<Array<Unit>>("_playerUnits");
        SetField("_flowState", System.Enum.Parse(PrivateField("_flowState").FieldType, "Exploration"));
        Call("StartCombat", Field<Array<Unit>>("_enemyUnits")[0]);
        Check(Field<object>("_flowState").ToString() == "Combat",
            "The test encounter must enter combat after saving its checkpoint.");
        var recoveryJson = ReadSave(RecoverySavePath);
        Field<Array<Unit>>("_playerUnits")[0].ApplyDamage(3);
        var checkpointUnits = (Array<Dictionary>)recoveryJson["unit_snapshots"];
        var savedPlayer = (Dictionary)checkpointUnits[0];
        Check(recoveryJson["flow_state"].AsString() == "exploration"
            && savedPlayer["hit_points"].AsInt32() == 20,
            "Combat entry must snapshot the living exploration state before the fight begins.");
        foreach (var unit in units)
        {
            unit.ApplyDamage(unit.MaxHitPoints);
        }
        SetField("_flowState", System.Enum.Parse(PrivateField("_flowState").FieldType, "Combat"));
        Check((bool)Call("CheckCombatResolved"), "The last party defeat must resolve combat.");
        var dialog = Field<AcceptDialog>("_defeatRecoveryDialog");
        Check(dialog != null && dialog.Visible && dialog.Title == "The Party Has Fallen",
            "Combat defeat must show the recovery modal immediately.");
        Check(dialog.GetOkButton().Text == "Respawn in Forest Town",
            "The primary recovery action must offer a full town respawn.");
        Check(FindButton(dialog, "Load Recovery Save")?.Disabled == false,
            "The recovery-save action must be enabled when a checkpoint exists.");
        Check(FindButton(dialog, "Main Menu") != null,
            "The defeat modal must offer a return to the main menu.");
        Check(Json.Stringify(ReadSave(RecoverySavePath)) == Json.Stringify(recoveryJson),
            "Defeat autosaves must not overwrite the separate recovery checkpoint.");
    }

    private void CheckLoadRecovery()
    {
        Call("HandleDefeatRecoveryCustomAction", "load_recovery");
        Check(Field<object>("_flowState").ToString() == "Exploration",
            "Loading recovery must return to exploration.");
        Check(Field<Array<Unit>>("_playerUnits").Count == 2,
            "Loading recovery must restore the whole saved party.");
        foreach (var unit in Field<Array<Unit>>("_playerUnits"))
        {
            Check(!unit.IsDead && unit.HitPoints == unit.MaxHitPoints,
                "The pre-combat recovery checkpoint must restore the living party.");
        }
        Check(Field<AcceptDialog>("_defeatRecoveryDialog") == null,
            "The defeat modal must close after loading the checkpoint.");
    }

    private void CheckRespawn()
    {
        foreach (var unit in Field<Array<Unit>>("_playerUnits"))
        {
            unit.ApplyDamage(unit.MaxHitPoints);
        }
        SetField("_flowState", System.Enum.Parse(PrivateField("_flowState").FieldType, "Defeat"));
        Call("ShowDefeatRecoveryDialog");
        Field<AcceptDialog>("_defeatRecoveryDialog").EmitSignal(AcceptDialog.SignalName.Confirmed);

        Check(Field<object>("_flowState").ToString() == "Exploration"
            && Field<string>("_currentMapId") == "forest-town",
            "Respawn must return the party to Forest Town exploration.");
        Check(Field<Array<Unit>>("_playerUnits").Count == 2,
            "Respawn must keep the existing party roster.");
        foreach (var unit in Field<Array<Unit>>("_playerUnits"))
        {
            Check(!unit.IsDead && unit.HitPoints == unit.MaxHitPoints
                && unit.MagicPoints == unit.MaxMagicPoints,
                "Respawn must restore party HP and MP to full.");
            Check(unit.GridPos != new Vector2I(9, 9) && unit.GridPos != new Vector2I(9, 10),
                "Respawn must move the party to Forest Town's authored spawn formation.");
        }
        Check(_mainSave.HasSaveGame() && _recoverySave.HasSaveGame(),
            "Respawn must persist both the regular save and a new safe recovery point.");
    }

    private void CheckTrapDefeatShowsRecoveryChoices()
    {
        Call("CloseDefeatRecoveryDialog");
        var players = Field<Array<Unit>>("_playerUnits");
        foreach (var unit in players)
        {
            unit.SetGridPos(new Vector2I(15, 15));
            unit.RestoreFromRest();
        }
        SetField("_explorerUnit", players[0]);
        SetField("_flowState", System.Enum.Parse(PrivateField("_flowState").FieldType, "Exploration"));
        Field<Array<Dictionary>>("_mapProps").Add(new Dictionary
        {
            { "id", "recovery-test-lethal-trap" }, { "type", "trap" },
            { "name", "Lethal Trap" }, { "grid_pos", new Vector2I(15, 15) },
            { "damage", 999 }, { "target_scope", "party" }
        });
        Call("ResolveExplorationTraps");
        var dialog = Field<AcceptDialog>("_defeatRecoveryDialog");
        Check(dialog != null && dialog.Visible && Field<object>("_flowState").ToString() == "Defeat",
            "All-party defeat from an exploration trap must show the same recovery modal.");
    }

    private void CheckRestoringDefeatSaveShowsChoices()
    {
        Call("CloseDefeatRecoveryDialog");
        SetField("_flowState", System.Enum.Parse(PrivateField("_flowState").FieldType, "Exploration"));
        ((IGamePersistenceHost)this).RestoreFlowState("defeat");
        Check(Field<object>("_flowState").ToString() == "Defeat"
            && Field<AcceptDialog>("_defeatRecoveryDialog")?.Visible == true,
            "Loading an existing defeat save must show recovery choices instead of stalling.");
    }

    private Unit AddUnit(Node2D parent, string id, string team, Vector2I cell)
    {
        var unit = GD.Load<PackedScene>("res://scenes/Unit.tscn").Instantiate<Unit>();
        parent.AddChild(unit);
        unit.Setup(new Dictionary
        {
            { "id", id }, { "name", id }, { "team", team }, { "grid_pos", cell },
            { "hit_points", 20 }, { "max_hit_points", 20 }, { "magic_points", 10 },
            { "max_magic_points", 10 }, { "initiative", 10 }, { "primary_ability_id", "melee" }
        });
        Field<Array<Unit>>("_allUnits").Add(unit);
        Field<Array<Unit>>(team == "player" ? "_playerUnits" : "_enemyUnits").Add(unit);
        return unit;
    }

    private static Button FindButton(AcceptDialog dialog, string text)
    {
        foreach (var child in dialog.FindChildren("*", "Button", true, false))
        {
            if (child is Button button && button.Text == text)
            {
                return button;
            }
        }
        return null;
    }

    private static Dictionary ReadSave(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return Json.ParseString(file.GetAsText()).AsGodotDictionary();
    }

    private T Field<T>(string name) => (T)PrivateField(name).GetValue(this);
    private void SetField(string name, object value) => PrivateField(name).SetValue(this, value);
    private static FieldInfo PrivateField(string name) =>
        typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private void Check(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }
}
