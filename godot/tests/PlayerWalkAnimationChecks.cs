using Godot;
using Godot.Collections;
using System;
using System.Threading.Tasks;

public partial class PlayerWalkAnimationChecks : Node
{
    private int _failures;
    private int _checks;

    public override async void _Ready()
    {
        try
        {
            GetNode<AudioStreamPlayer>("/root/AudioManager/BackgroundMusicPlayer").Stop();
            foreach (var classId in new[] { "warrior", "wizard", "ranger", "cleric", "thief" })
            {
                CheckDirectionalPlayback(classId);
                CheckFrameRateIndependence(classId);
                await CheckTween(classId);
                await CheckExploration(classId);
            }
            CheckStaticFallback();
            CheckOverrides();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            _failures++;
        }

        GD.Print($"{(_failures == 0 ? "PASS" : "FAIL")}: {_checks} player walk animation checks, {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private Unit CreateUnit(string classId, string team = "player", string customTexture = "")
    {
        var unit = new Unit();
        AddChild(unit);
        unit.Setup(new Dictionary
        {
            ["id"] = classId,
            ["class_id"] = classId,
            ["team"] = team,
            ["sprite_texture"] = customTexture,
        });
        unit.SetProcess(false);
        return unit;
    }

    private static float ExpectedCycleDistance(string classId) => classId switch
    {
        "warrior" => 256.0f,
        "wizard" => 320.0f,
        "ranger" => 224.0f,
        "cleric" => 288.0f,
        "thief" => 208.0f,
        _ => throw new ArgumentException($"Unexpected test class '{classId}'."),
    };

    private void CheckDirectionalPlayback(string classId)
    {
        var unit = CreateUnit(classId);
        var sprite = unit.GetNode<Sprite2D>("Sprite2D");
        var cycleDistance = ExpectedCycleDistance(classId);
        Check(sprite.Texture.ResourcePath.EndsWith($"{classId}_idle.png"), $"{classId}: starts at directional idle");
        Check(sprite.RegionRect == new Rect2(0, 128, 64, 64), $"{classId}: default facing is south");
        Check(sprite.TextureFilter == CanvasItem.TextureFilterEnum.Nearest, $"{classId}: crisp pixel filtering");
        unit.BeginWalking(Vector2.Zero);
        Check(sprite.Texture.ResourcePath.EndsWith("_idle.png"), $"{classId}: stationary followers do not walk");

        var directions = new[] { Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left };
        for (var row = 0; row < directions.Length; row++)
        {
            var direction = directions[row];
            unit.BeginWalking(direction);
            Check(sprite.RegionRect.Position == new Vector2(0, row * 64), $"{classId}: direction row {row}");
            unit._Process(1.0);
            Check(sprite.RegionRect.Position.X == 0, $"{classId}: time alone does not advance feet");
            unit.SetWalkPosition(unit.Position + direction * (cycleDistance / 8));
            Check(sprite.RegionRect.Position.X == 64, $"{classId}: one eighth stride advances exactly one frame");
            unit.EndWalking();
            unit._Process(0.03);
            Check(sprite.Texture.ResourcePath.EndsWith("_walk.png"), $"{classId}: no idle flash at tile handoff");
            unit.BeginWalking(directions[(row + 1) % 4]);
            Check(sprite.RegionRect.Position.X == 64, $"{classId}: turns preserve phase");
            unit.EndWalking();
            unit._Process(0.081);
            Check(sprite.Texture.ResourcePath.EndsWith("_idle.png"), $"{classId}: real stop returns to idle");
            Check(sprite.RegionRect.Position.Y == ((row + 1) % 4) * 64, $"{classId}: idle retains facing");
        }

        unit.BeginWalking(new Vector2(-1, -1));
        Check(sprite.RegionRect.Position.Y == 192, $"{classId}: diagonal ties use horizontal facing");
        unit.EndWalking();
        unit._Process(0.081);
        unit.BeginWalking(Vector2.Right);
        var start = unit.Position;
        for (var frame = 1; frame <= 8; frame++)
        {
            unit.SetWalkPosition(start + Vector2.Right * (cycleDistance * frame / 8));
            Check(sprite.RegionRect.Position.X == frame % 8 * 64, $"{classId}: loop frame {frame}");
            unit.EndWalking();
            unit.BeginWalking(Vector2.Right);
        }
        CheckAssets(classId);
        unit.Free();
    }

    private void CheckFrameRateIndependence(string classId)
    {
        foreach (var fps in new[] { 30, 60, 120 })
        {
            var unit = CreateUnit(classId);
            var start = unit.Position;
            unit.BeginWalking(Vector2.Right);
            for (var frame = 1; frame <= fps; frame++)
            {
                unit.SetWalkPosition(start + new Vector2(192.0f * frame / fps, 0));
                unit._Process(1.0 / fps);
            }
            var expectedFrame = (int)(192.0f * 8 / ExpectedCycleDistance(classId));
            Check(unit.GetNode<Sprite2D>("Sprite2D").RegionRect.Position.X == expectedFrame * 64,
                $"{classId}: distance phase is correct at {fps} FPS");
            unit.Free();
        }
    }

    private async Task CheckTween(string classId)
    {
        var unit = CreateUnit(classId);
        var destination = new Vector2I(2, 0);
        await unit.AnimateWalkToAsync(destination, 0.14f);
        Check(unit.Position.IsEqualApprox(unit.GetWorldCenterAt(destination)), $"{classId}: tween reaches exact destination");
        Check(unit.GridPos == Vector2I.Zero, $"{classId}: animation does not change logical grid state");
        Check(unit.GetNode<Sprite2D>("Sprite2D").RegionRect.Position.X > 0, $"{classId}: combat/charge tween advances phase");
        unit._Process(0.081);
        Check(unit.GetNode<Sprite2D>("Sprite2D").Texture.ResourcePath.EndsWith("_idle.png"),
            $"{classId}: completed tween settles without blocking gameplay");
        unit.Free();
    }

    private void CheckStaticFallback()
    {
        var unit = CreateUnit("knight");
        var sprite = unit.GetNode<Sprite2D>("Sprite2D");
        unit.BeginWalking(Vector2.Left);
        unit.SetWalkPosition(unit.Position + Vector2.Left * 64);
        unit._Process(0.1);
        Check(sprite.Texture.ResourcePath.EndsWith("units_2_64.png"), "Classes without animation assets retain static atlas sprites");
        unit.Free();
    }

    private async Task CheckExploration(string classId)
    {
        var controller = new FogOfWarChecks();
        AddChild(controller);
        var leader = System.Linq.Enumerable.First(System.Linq.Enumerable.OfType<Unit>(controller.GetChildren()));
        leader.Setup(new Dictionary { ["id"] = classId, ["class_id"] = classId, ["team"] = "player" });
        controller.ResetScenario();
        var start = leader.Position;
        var directions = new Array<Vector2I> { Vector2I.Right, Vector2I.Up, Vector2I.Left, Vector2I.Down, Vector2I.Right };
        controller.StartPath(directions);
        var frames = 0;
        var distance = 0.0f;
        var previous = leader.Position;
        var cycleDistance = ExpectedCycleDistance(classId);
        var sprite = leader.GetNode<Sprite2D>("Sprite2D");
        while (controller.IsStepRunning())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            distance += previous.DistanceTo(leader.Position);
            previous = leader.Position;
            var expectedFrame = Mathf.Min(7, (int)((distance % cycleDistance) * 8 / cycleDistance));
            Check(sprite.Texture.ResourcePath.EndsWith("_walk.png"), $"{classId}: exploration handoff retains walk texture");
            Check(sprite.RegionRect.Position.X == expectedFrame * 64, $"{classId}: exploration phase follows distance through corners");
            frames++;
            if (frames > 600)
            {
                throw new InvalidOperationException("Exploration animation did not finish within 600 frames.");
            }
        }
        Check(controller.StepSucceeded(), $"{classId}: multi-tile exploration path succeeds");
        Check(frames > 5, $"{classId}: exploration inspected over multiple rendered updates");
        Check(leader.Position.IsEqualApprox(start + Vector2.Right * 64), $"{classId}: exploration reaches its logical destination");
        Check(leader.Position.IsEqualApprox(leader.GetWorldCenterAt(leader.GridPos)), $"{classId}: exploration grid and visuals agree");
        leader._Process(0.081);
        Check(sprite.Texture.ResourcePath.EndsWith("_idle.png") && sprite.RegionRect.Position.Y == 64,
            $"{classId}: exploration stops facing its final travel direction");
        controller.Free();
    }

    private void CheckOverrides()
    {
        var enemy = CreateUnit("warrior", "enemy");
        enemy.BeginWalking(Vector2.Right);
        Check(enemy.GetNode<Sprite2D>("Sprite2D").Texture.ResourcePath.EndsWith("units_2_64.png"), "Enemy visuals remain unchanged");
        enemy.Free();
        var custom = CreateUnit("wizard", customTexture: "res://assets/tilesets/units_2_64.png");
        custom.BeginWalking(Vector2.Right);
        custom.SetWalkPosition(custom.Position + Vector2.Right * 64);
        Check(!custom.GetNode<Sprite2D>("Sprite2D").RegionEnabled, "Custom sprites still override walk sheets");
        custom.Setup(new Dictionary { ["id"] = "ranger", ["class_id"] = "ranger" });
        Check(custom.GetNode<Sprite2D>("Sprite2D").Texture.ResourcePath.EndsWith("ranger_idle.png"), "Setup replaces previous class animation assets");
        custom.Setup(new Dictionary { ["id"] = "knight", ["class_id"] = "knight" });
        Check(custom.GetNode<Sprite2D>("Sprite2D").Texture.ResourcePath.EndsWith("units_2_64.png"), "Setup clears previous directional assets");
        Check(custom.GetNode<Sprite2D>("Sprite2D").TextureFilter == CanvasItem.TextureFilterEnum.ParentNode, "Setup restores inherited filtering");
        custom.Free();
    }

    private void CheckAssets(string classId)
    {
        var walk = GD.Load<Texture2D>($"res://assets/animations/player/{classId}_walk.png").GetImage();
        var idle = GD.Load<Texture2D>($"res://assets/animations/player/{classId}_idle.png").GetImage();
        Check(walk.GetSize() == new Vector2I(512, 256), $"{classId}: eight frames by four directions");
        Check(idle.GetSize() == new Vector2I(64, 256), $"{classId}: four resting poses");
        for (var row = 0; row < 4; row++)
        {
            var rest = idle.GetRegion(new Rect2I(0, row * 64, 64, 64));
            var first = walk.GetRegion(new Rect2I(0, row * 64, 64, 64));
            Check(System.Linq.Enumerable.SequenceEqual(rest.GetData(), first.GetData()),
                $"{classId}: resting pose matches neutral walk pose exactly in row {row}");
            var hashes = new System.Collections.Generic.HashSet<string>();
            for (var frame = 0; frame < 8; frame++)
            {
                var cell = walk.GetRegion(new Rect2I(frame * 64, row * 64, 64, 64));
                hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(cell.GetData())));
                Check(cell.GetUsedRect().HasArea(), $"{classId}: row {row} frame {frame} is not empty");
                var clearBorder = true;
                for (var pixel = 0; pixel < 64; pixel++)
                {
                    clearBorder &= cell.GetPixel(pixel, 0).A == 0 && cell.GetPixel(pixel, 63).A == 0
                        && cell.GetPixel(0, pixel).A == 0 && cell.GetPixel(63, pixel).A == 0;
                }
                Check(clearBorder, $"{classId}: row {row} frame {frame} has no edge clipping");
            }
            Check(hashes.Count == 8, $"{classId}: row {row} contains eight distinct poses");
        }
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition)
        {
            _failures++;
            GD.PushError(message);
        }
    }
}
