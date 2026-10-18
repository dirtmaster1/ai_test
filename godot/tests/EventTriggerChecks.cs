using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Reflection;

public partial class EventTriggerChecks : BattleController
{
    private const string TestMapId = "event-trigger-test";
    private const string TestSavePath = "user://event-trigger-test-save.json";
    private const string EventId = "event-trigger-test-wolves";
    private const string FollowerEventId = "event-trigger-test-follower-wolf";
    private readonly Array<string> _failures = new();
    private GamePersistence _testPersistence;
    private Dictionary _testMapData;

    public override void _Ready() { }

    public override void _ExitTree()
    {
        _testPersistence?.DeleteSaveGame(false);
    }

    public Array<string> Run()
    {
        try
        {
            SetupFixture();
            CheckMarkerIsHiddenAndParsed();
            CheckPartyMovementSpawnsEnemiesOnce();
        }
        catch (System.Exception exception)
        {
            _failures.Add(exception.ToString());
        }

        return _failures;
    }

    private void SetupFixture()
    {
        var maps = new Node2D { Name = "Maps" };
        AddChild(maps);

        var tileSet = GD.Load<TileSet>("res://assets/tilesets/map_markers_64_tileset.tres").Duplicate(true) as TileSet;
        var markerSource = (TileSetAtlasSource)tileSet.GetSource(0);
        var eventTileData = markerSource.GetTileData(new Vector2I(2, 1), 0);
        eventTileData.SetCustomData("marker_type", "event_trigger");
        eventTileData.SetCustomData("event_id", EventId);
        eventTileData.SetCustomData("event_type", "spawn_enemies");
        eventTileData.SetCustomData("enemy_unit_id", "wolf");
        eventTileData.SetCustomData("enemy_count", 2);
        eventTileData.SetCustomData("uses_tile_visual", false);

        var followerEventTileData = markerSource.GetTileData(new Vector2I(0, 1), 0);
        followerEventTileData.SetCustomData("marker_type", "event_trigger");
        followerEventTileData.SetCustomData("event_id", FollowerEventId);
        followerEventTileData.SetCustomData("event_type", "spawn_enemies");
        followerEventTileData.SetCustomData("enemy_unit_id", "wolf");
        followerEventTileData.SetCustomData("enemy_count", 1);
        followerEventTileData.SetCustomData("uses_tile_visual", false);

        var markers = new TileMapLayer
        {
            Name = TestMapId + "-markers",
            TileSet = tileSet
        };
        markers.SetCell(new Vector2I(3, 3), 0, new Vector2I(2, 1));
        markers.SetCell(new Vector2I(6, 6), 0, new Vector2I(2, 1));
        markers.SetCell(new Vector2I(2, 3), 0, new Vector2I(0, 1));
        maps.AddChild(markers);

        var mapLoader = new MapLoader { MapsRootPath = "../Maps" };
        AddChild(mapLoader);
        _testMapData = mapLoader.LoadMapStub(TestMapId);
        var eventTriggers = (Array<Dictionary>)_testMapData["event_triggers"];
        Check(eventTriggers.Count == 3, "All trigger tiles must resolve to event definitions.");

        var eventTriggersByCell = Field<System.Collections.Generic.Dictionary<Vector2I, Dictionary>>("_mapEventTriggersByCell");
        foreach (var trigger in eventTriggers)
        {
            eventTriggersByCell[(Vector2I)trigger["grid_pos"]] = trigger;
        }

        Set("_currentMapId", TestMapId);
        Set("_gameData", GetNode<GameData>("/root/GameData"));
        Set("_gridWidth", 9);
        Set("_gridHeight", 9);

        var walkableCells = Field<HashSet<Vector2I>>("_walkableCells");
        for (var y = 0; y < 9; y++)
        {
            for (var x = 0; x < 9; x++)
            {
                walkableCells.Add(new Vector2I(x, y));
            }
        }

        var unitsRoot = new Node2D { Name = "Units" };
        AddChild(unitsRoot);
        Set("_unitsRoot", unitsRoot);

        var player = new Unit();
        unitsRoot.AddChild(player);
        player.Setup(new Dictionary
        {
            { "id", "event-trigger-test-player" },
            { "team", "player" },
            { "grid_pos", new Vector2I(2, 3) }
        });
        Field<Array<Unit>>("_allUnits").Add(player);
        Field<Array<Unit>>("_playerUnits").Add(player);
        Set("_explorerUnit", player);

        var follower = new Unit();
        unitsRoot.AddChild(follower);
        follower.Setup(new Dictionary
        {
            { "id", "event-trigger-test-follower" },
            { "team", "player" },
            { "grid_pos", new Vector2I(1, 3) }
        });
        Field<Array<Unit>>("_allUnits").Add(follower);
        Field<Array<Unit>>("_playerUnits").Add(follower);

        _testPersistence = new GamePersistence(this, TestSavePath);
        typeof(BattleController)
            .GetField("_persistence", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(this, _testPersistence);
    }

    private void CheckMarkerIsHiddenAndParsed()
    {
        var markerVisuals = GetNode<TileMapLayer>("Maps/" + TestMapId + "-item-visuals");
        Check(markerVisuals.GetCellSourceId(new Vector2I(3, 3)) == -1
            && markerVisuals.GetCellSourceId(new Vector2I(6, 6)) == -1
            && markerVisuals.GetCellSourceId(new Vector2I(2, 3)) == -1,
            "Event-trigger markers must not create party-visible tile visuals.");
        Check(((Array<Dictionary>)_testMapData["props"]).Count == 0,
            "Event-trigger markers must not appear as hoverable map interactables.");
    }

    private void CheckPartyMovementSpawnsEnemiesOnce()
    {
        var moved = (bool)Call("TryMoveExplorationParty", Vector2I.Right);
        var enemies = Field<Array<Unit>>("_enemyUnits");
        var triggeredEventIds = Field<HashSet<string>>("_triggeredEventIds");
        Check(moved && enemies.Count == 3, "Leader and follower movement onto event tiles must spawn their configured enemies.");
        Check(triggeredEventIds.Contains(EventId) && triggeredEventIds.Contains(FollowerEventId),
            "Events entered by any party member must be recorded as triggered.");

        Call("TriggerMapEventAtCell", new Vector2I(6, 6));
        Check(enemies.Count == 3, "Another tile with the same event_id must not trigger the event again.");

        var players = Field<Array<Unit>>("_playerUnits");
        var occupiedCells = new HashSet<Vector2I>();
        foreach (var enemy in enemies)
        {
            Check(enemy.Team == "enemy" && (enemy.EncounterId == EventId || enemy.EncounterId == FollowerEventId),
                "Spawned units must use the enemy team and event encounter ID.");
            foreach (var cell in enemy.GetOccupiedCellsAt(enemy.GridPos))
            {
                var overlapsParty = false;
                foreach (var player in players)
                {
                    overlapsParty |= player.OccupiesCell(cell);
                }

                Check(Field<HashSet<Vector2I>>("_walkableCells").Contains(cell)
                    && !overlapsParty && occupiedCells.Add(cell),
                    "Spawned enemies must occupy distinct valid cells without overlapping the party.");
            }
        }

        using var saveFile = FileAccess.Open(TestSavePath, FileAccess.ModeFlags.Read);
        Check(saveFile != null, "Triggering an event must persist its one-time state.");
        if (saveFile != null)
        {
            var saveText = saveFile.GetAsText();
            Check(saveText.Contains(EventId) && saveText.Contains(FollowerEventId)
                && saveText.Contains("event_spawned_unit_configs_by_map"),
                "The save must retain the event ID and spawned enemy definitions.");
        }
    }

    private T Field<T>(string fieldName)
    {
        return (T)typeof(BattleController)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(this);
    }

    private void Check(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }
}
