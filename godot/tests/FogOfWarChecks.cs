using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

public partial class FogOfWarChecks : BattleController
{
    private Unit _leader;
    private Task<bool> _movement;

    public override void _Ready()
    {
        Field("_gridWidth").SetValue(this, 24);
        Field("_gridHeight").SetValue(this, 5);
        _leader = GD.Load<PackedScene>("res://scenes/Unit.tscn").Instantiate<Unit>();
        AddChild(_leader);
        _leader.Setup(new Dictionary { { "id", "knight" }, { "team", "player" } });
        _leader.Visible = false;
        ((Array<Unit>)Field("_playerUnits").GetValue(this)).Add(_leader);
        ((Array<Unit>)Field("_allUnits").GetValue(this)).Add(_leader);
        var walls = (HashSet<Vector2I>)Field("_wallCellSet").GetValue(this);
        for (var row = 0; row < 5; row++) walls.Add(new Vector2I(12, row));
        AddChild(new BattleOverlay { Name = "Overlay" });
        ResetScenario();
    }

    public override void _ExitTree() { }

    public override void _Draw() => DrawRect(new Rect2(-4096, -4096, 8192, 8192), Colors.White);

    public void ResetScenario()
    {
        _leader.SetGridPos(new Vector2I(8, 2));
        var revealed = (System.Collections.Generic.Dictionary<string, HashSet<string>>)
            Field("_revealedFogCellIdsByMap").GetValue(this);
        revealed.Clear();
        revealed["forest-town"] = new HashSet<string> { "0,0" };
        Field("_fogVisibilityActor").SetValue(this, null);
        CallPrivate("UpdateFogOfWar");
        CallPrivate("CenterViewOnCurrentFocus");
    }

    public void StartStep(Vector2I delta)
    {
        _movement = (Task<bool>)CallPrivate("TryMoveExplorationPartyStepAnimated",
            _leader.GridPos + delta, new List<Unit> { _leader });
    }

    public void StartPath(Array<Vector2I> directions) => _movement = MovePath(directions);

    private async Task<bool> MovePath(Array<Vector2I> directions)
    {
        foreach (var direction in directions)
        {
            var step = (Task<bool>)CallPrivate("TryMoveExplorationPartyStepAnimated",
                _leader.GridPos + direction, new List<Unit> { _leader });
            if (!await step) return false;
        }
        return true;
    }

    public bool IsStepRunning() => _movement != null && !_movement.IsCompleted;
    public bool StepSucceeded() => _movement?.IsCompletedSuccessfully == true && _movement.Result;

    public bool MoveWithKeyboard(Vector2I delta)
    {
        var moved = (bool)CallPrivate("TryMoveExplorationParty", delta);
        CallPrivate("UpdateFogOfWar");
        return moved;
    }

    public Dictionary BenchmarkFogRebuild()
    {
        Field("_gridWidth").SetValue(this, 96);
        Field("_gridHeight").SetValue(this, 96);
        ((Array<Vector2I>)Field("_mapFogCells").GetValue(this)).Clear();
        ((HashSet<Vector2I>)Field("_mapFogCellSet").GetValue(this)).Clear();
        ResetScenario();
        var timings = new List<double>();
        for (var sample = 0; sample < 12; sample++)
        {
            _leader.SetGridPos(new Vector2I(48 + sample % 2, 48));
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            CallPrivate("UpdateFogOfWar");
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (sample >= 2) timings.Add(elapsed);
        }
        timings.Sort();
        var texture = (ImageTexture)Field("_fogOverlayTexture").GetValue(this);
        using var image = texture.GetImage();
        return new Dictionary
        {
            { "median_ms", timings[timings.Count / 2] },
            { "max_ms", timings[timings.Count - 1] },
            { "sha256", System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.GetData())) }
        };
    }

    public Dictionary FogState()
    {
        CallPrivate("UpdateFogOfWar");
        var revealed = (System.Collections.Generic.Dictionary<string, HashSet<string>>)
            Field("_revealedFogCellIdsByMap").GetValue(this);
        return new Dictionary
        {
            { "texture", (ImageTexture)Field("_fogOverlayTexture").GetValue(this) },
            { "previous_texture", (ImageTexture)Field("_fogPreviousOverlayTexture").GetValue(this)
                ?? (ImageTexture)Field("_fogOverlayTexture").GetValue(this) },
            { "rect", (Rect2)Field("_fogOverlayRect").GetValue(this) },
            { "blend", (float)Field("_fogStepBlend").GetValue(this) },
            { "drawn_blend", ((ShaderMaterial)GetNode<Node2D>("Overlay/Fog").Material).GetShaderParameter("step_blend") },
            { "revealed_count", revealed["forest-town"].Count },
            { "leader_position", _leader.Position },
            { "leader_cell", _leader.GridPos }
        };
    }

    private static FieldInfo Field(string name) =>
        typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    private object CallPrivate(string name, params object[] arguments) =>
        typeof(BattleController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this, arguments);
}