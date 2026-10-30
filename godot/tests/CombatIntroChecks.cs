using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

public partial class CombatIntroChecks : BattleController
{
    private const string EncounterId = "forest-town-northeast-bandits";
    private const string DialogueId = "forest-town-bandit-intro";
    private const string SavePath = "user://combat-intro-test-save.json";
    private readonly Array<string> _failures = new();
    private GamePersistence _testPersistence;
    private Unit _player;
    private Unit _bandit;
    private TurnManager _turns;
    private HudController _testHud;
    private int _turnCount;
    private bool _processTurns = true;
    public bool Finished { get; private set; }

    public override void _Ready() { }
    public override void _ExitTree()
    {
        Call("CancelPendingCombatIntro");
        _testPersistence?.DeleteSaveGame(false);
    }

    public Array<string> GetFailures() => _failures;

    public void CollectManagedWrappers()
    {
        // Finalize temporary Godot arrays while the native engine is still alive.
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
    }

    public async void Run()
    {
        try
        {
            SetupFixture();
            await CheckIntroBlocksTurns();
            await CheckResumeAndPersistence();
            await CheckMultiplePagesAndInterruption();
            await CheckEnemyFirstTurn();
            await CheckLegacySave();
            await CheckUnconfiguredCombat();
        }
        catch (System.Exception exception)
        {
            _failures.Add(exception.ToString());
        }
        finally
        {
            Call("CancelPendingCombatIntro");
            Finished = true;
        }
    }

    private void SetupFixture()
    {
        SetField("_gameData", GetNode<GameData>("/root/GameData"));
        var main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
        var maps = main.GetNode<Node2D>("Gameworld/Maps");
        maps.GetParent().RemoveChild(maps);
        main.Free();
        AddChild(maps);
        var loader = new MapLoader { MapsRootPath = "../Maps" };
        AddChild(loader);
        SetField("_mapLoader", loader);
        var map = loader.LoadMapStub("forest-town");
        Dictionary banditEncounter = null;
        foreach (var encounter in (Array<Dictionary>)map["encounters"])
        {
            if (encounter["id"].AsString() == EncounterId)
            {
                banditEncounter = encounter;
            }
            else
            {
                Check(!encounter.ContainsKey("combat_intro_dialogue_id"),
                    "Other Forest Town encounters must not inherit the bandits' dialogue.");
            }
        }
        Check(banditEncounter != null, "The northeast bandits must have their own encounter.");
        if (banditEncounter == null)
        {
            throw new System.InvalidOperationException("Missing northeast bandit encounter.");
        }
        Check(banditEncounter["combat_intro_dialogue_id"].AsString() == DialogueId,
            "The authored bandit encounter must reference its intro.");
        var configs = (Array<Dictionary>)banditEncounter["enemies"];
        Check(configs.Count == 2, "Exactly two authored bandits must belong to the intro encounter.");
        var expectedCells = new HashSet<Vector2I> { new(69, -11), new(71, -11) };
        foreach (var config in configs)
        {
            var cell = (Vector2I)config["grid_pos"];
            Check(expectedCells.Remove(cell), "The intro must target the northeast bandits, not another group.");
            Check(config["id"].AsString() == $"forest-town-encounter-main-bandit-warrior-{cell.X}-{cell.Y}",
                "Bandit enemy IDs must remain compatible with previous saves.");
        }
        SetField("_currentMapId", "forest-town");
        SetField("_gridWidth", map["width"].AsInt32());
        SetField("_gridHeight", map["height"].AsInt32());
        foreach (var cell in (Array<Vector2I>)map["walkable_cells"])
        {
            Field<HashSet<Vector2I>>("_walkableCells").Add(cell);
        }
        var units = new Node2D { Name = "Units" };
        AddChild(units);
        SetField("_unitsRoot", units);
        _turns = new TurnManager();
        AddChild(_turns);
        SetField("_turnManager", _turns);
        _turns.TurnChanged += active =>
        {
            _turnCount++;
            if (_processTurns)
            {
                Call("OnTurnChanged", active);
            }
        };
        _player = AddUnit(new Dictionary
        {
            { "id", "combat-intro-test-player" }, { "name", "Warrior" }, { "team", "player" },
            { "grid_pos", new Vector2I(68, -11) }, { "initiative", 1000 },
            { "hit_points", 100 }, { "max_hit_points", 100 }, { "primary_ability_id", "melee" }
        });
        SetField("_explorerUnit", _player);
        SetField("_selectedCharacterUnitId", _player.UnitId);
        var encounterMap = new Dictionary { { "encounters", new Array<Dictionary> { banditEncounter } } };
        Call("SpawnEnemiesFromMap", encounterMap);
        _bandit = Field<Array<Unit>>("_enemyUnits")[0];
        _testPersistence = new GamePersistence(this, SavePath);
        SetField("_persistence", _testPersistence);
        _testPersistence.DeleteSaveGame(false);
        var canvas = new CanvasLayer();
        AddChild(canvas);
        _testHud = GD.Load<PackedScene>("res://ui/HUD.tscn").Instantiate<HudController>();
        canvas.AddChild(_testHud);
        SetField("_hud", _testHud);
    }

    private async Task CheckIntroBlocksTurns()
    {
        _player.ApplyStatusEffect("intro-poison", "Poisoned", false, 3,
            scope: "persistent", damagePerTurn: 1);
        Call("TryStartCombatFromAggro");
        Check(Field<object>("_flowState").ToString() == "CombatIntro", "Aggro must enter the intro phase.");
        var dialog = CurrentDialog();
        Check(dialog != null && dialog.Visible && dialog.Title == "Bandit",
            "Aggro must open the actual bandit dialogue window.");
        Check(dialog.GetOkButton().Text == "Close", "A single-page intro must offer Close.");
        Check(!dialog.GetCancelButton().Visible, "A single-page intro should not show a redundant cancel button.");
        Check(_testHud.GetNode<Button>("ActionPanel/ActionVBox/ActionRow/ActionButtons/AbilityButton1").Disabled,
            "The combat intro phase must disable HUD ability actions.");
        var text = "";
        foreach (var node in dialog.FindChildren("*", "RichTextLabel", true, false))
        {
            text += ((RichTextLabel)node).Text;
        }
        Check(text.Contains("None of the townsfolk") && text.Contains("kill them all")
            && text.Contains("Bandit King will have our heads"),
            "The visible dialogue must contain the requested bandit threat.");
        var startCell = _player.GridPos;
        var enemyCell = _bandit.GridPos;
        _Input(new InputEventKey { Keycode = Key.Right, Pressed = true });
        Call("StartCombat", Field<Array<Unit>>("_enemyUnits")[1]);
        Check(CurrentDialog() == dialog, "A second aggro request must not open a duplicate intro.");
        Check(!(bool)Call("TryRequestEndTurn", _bandit, true), "End turn must be blocked during the intro.");
        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        Check(_turnCount == 0 && _player.HitPoints == 100 && _player.GridPos == startCell
            && _bandit.GridPos == enemyCell && !Field<bool>("_isEnemyTurnProcessing"),
            "No turn, status tick, party movement, or enemy AI may run while dialogue is open.");
        _testPersistence.PersistSaveGame(false);
        var save = ReadSave();
        Check(save["flow_state"].AsString() == "combat"
            && save["active_encounter_id"].AsString() == EncounterId,
            "An unfinished intro must save enough combat state to reopen on load.");
        Check(((Dictionary)save["completed_combat_intro_encounter_ids_by_map"]).Count == 0,
            "An open intro must not be marked completed.");
    }

    private async Task CheckResumeAndPersistence()
    {
        CurrentDialog().EmitSignal(Window.SignalName.CloseRequested);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(_turnCount == 1 && _turns.GetActiveUnit() == _player && _player.HitPoints == 99,
            "Closing the dialogue must start exactly one normal turn, including start-of-turn effects.");
        Check(Field<object>("_flowState").ToString() == "Combat" && CurrentDialog() == null,
            "Closing must leave normal combat active with no intro window.");
        var completed = ((Dictionary)ReadSave()["completed_combat_intro_encounter_ids_by_map"])["forest-town"];
        Check(completed.AsGodotArray().Contains(EncounterId), "Closing must persist the completed encounter intro.");

        _processTurns = false;
        Check(_testPersistence.TryLoadSaveGame(false), "The test combat save must load.");
        Check(Field<object>("_flowState").ToString() == "Combat" && CurrentDialog() == null,
            "Reloading a completed intro must restore combat without repeating dialogue.");
        Check(((IGamePersistenceHost)this).ActiveEncounterId == EncounterId,
            "Restoring the map must not discard the saved active encounter ID.");
        _bandit = Field<Array<Unit>>("_enemyUnits")[0];
        foreach (var enemy in Field<Array<Unit>>("_enemyUnits"))
        {
            if (enemy.EncounterId == EncounterId)
            {
                _bandit = enemy;
                break;
            }
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task CheckMultiplePagesAndInterruption()
    {
        var gameData = GetNode<GameData>("/root/GameData");
        const string testDialogueId = "combat-intro-test-pages";
        gameData.Dialogues[testDialogueId] = new Dictionary
        {
            { "title", "Test Intro" },
            { "pages", new Array<Dictionary>
                {
                    new() { { "text", "First page." } },
                    new() { { "text", "Second page." } }
                }
            }
        };
        try
        {
            Field<System.Collections.Generic.Dictionary<string, string>>("_combatIntroDialogueIdsByEncounter")[EncounterId] = testDialogueId;
            ((IGamePersistenceHost)this).CompletedCombatIntroEncounterIdsByMap.Clear();
            Call("EnterExplorationMode", "");
            var before = _turnCount;
            Call("StartCombat", _bandit);
            Check(CurrentDialog().GetOkButton().Text == "Next", "Multi-page intros must offer Next.");
            Check(CurrentDialog().GetCancelButton().Visible && CurrentDialog().GetCancelButton().Text == "End",
                "Multi-page intros must offer End to skip the remaining pages.");
            CurrentDialog().EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            Check(_turnCount == before && CurrentDialog().GetOkButton().Text == "Close",
                "Next must show the next page without advancing combat.");
            CurrentDialog().EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            Check(_turnCount == before + 1, "Completing all pages must resume combat once.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            ((IGamePersistenceHost)this).CompletedCombatIntroEncounterIdsByMap.Clear();
            Call("EnterExplorationMode", "");
            Call("StartCombat", _bandit);
            CurrentDialog().EmitSignal(ConfirmationDialog.SignalName.Canceled);
            Check(_turnCount == before + 2, "End/Escape must skip remaining pages and resume combat.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            ((IGamePersistenceHost)this).CompletedCombatIntroEncounterIdsByMap.Clear();
            Call("EnterExplorationMode", "");
            Call("StartCombat", _bandit);
            _testPersistence.PersistSaveGame(false);
            before = _turnCount;
            Check(_testPersistence.TryLoadSaveGame(false), "An unfinished intro save must load.");
            Check(_turnCount == before && CurrentDialog() != null && CurrentDialog().Title == "Bandit",
                "Reload during an intro must cancel its old continuation and reopen the authored intro before any turn.");
            CurrentDialog().EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            Check(_turnCount == before + 1, "A restored intro must resume combat exactly once.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        finally
        {
            gameData.Dialogues.Remove(testDialogueId);
        }
    }

    private async Task CheckUnconfiguredCombat()
    {
        Call("EnterExplorationMode", "");
        var ordinary = AddUnit(new Dictionary
        {
            { "id", "combat-intro-ordinary-enemy" }, { "team", "enemy" },
            { "encounter_id", "ordinary-encounter" }, { "grid_pos", new Vector2I(70, -11) },
            { "initiative", 20000 }
        });
        var before = _turnCount;
        Call("StartCombat", ordinary);
        Check(_turnCount == before + 1 && CurrentDialog() == null && _turns.GetActiveUnit() == ordinary,
            "An unconfigured initiating encounter must start immediately, even when it chains the configured bandits.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task CheckEnemyFirstTurn()
    {
        Call("EnterExplorationMode", "");
        ((IGamePersistenceHost)this).CompletedCombatIntroEncounterIdsByMap.Clear();
        var enemy = AddUnit(new Dictionary
        {
            { "id", "combat-intro-fast-enemy" }, { "team", "enemy" },
            { "encounter_id", EncounterId }, { "grid_pos", new Vector2I(70, -11) },
            { "initiative", 10000 }
        });
        var before = _turnCount;
        Call("StartCombat", enemy);
        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        Check(_turnCount == before && CurrentDialog() != null,
            "An enemy winning initiative must not start a turn while the intro is open.");
        CurrentDialog().EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Check(_turnCount == before + 1 && _turns.GetActiveUnit() == enemy,
            "Closing the intro must preserve initiative and activate the winning enemy.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task CheckLegacySave()
    {
        var save = ReadSave();
        save.Remove("completed_combat_intro_encounter_ids_by_map");
        using (var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write))
        {
            file.StoreString(Json.Stringify(save));
        }
        var before = _turnCount;
        Check(_testPersistence.TryLoadSaveGame(false), "Saves predating combat intros must still load.");
        Check(_turnCount == before && CurrentDialog() != null,
            "A legacy save with no completed-intro field must treat the intro as unseen.");
        CurrentDialog().EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Check(_turnCount == before + 1, "Combat must continue normally after the legacy save's intro.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private Unit AddUnit(Dictionary config)
    {
        var unit = GD.Load<PackedScene>("res://scenes/Unit.tscn").Instantiate<Unit>();
        Field<Node2D>("_unitsRoot").AddChild(unit);
        unit.Setup(config);
        Field<Array<Unit>>("_allUnits").Add(unit);
        Field<Array<Unit>>(unit.Team == "player" ? "_playerUnits" : "_enemyUnits").Add(unit);
        return unit;
    }

    private ConfirmationDialog CurrentDialog() => Field<ConfirmationDialog>("_combatIntroDialog");

    private static Dictionary ReadSave()
    {
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
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
