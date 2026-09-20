using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Reflection;

public partial class ViewZoomChecks : BattleController
{
    public override void _Ready() { }
    public override void _ExitTree() { }
    public override void _Draw() { }

    public Array<string> Run()
    {
        var failures = new Array<string>();
        void Check(bool condition, string message)
        {
            if (!condition) failures.Add(message);
        }

        var cells = (HashSet<Vector2I>)Field("_walkableCells").GetValue(this);
        cells.Add(new Vector2I(-100, -100));
        cells.Add(new Vector2I(100, 100));
        var cursor = GetViewportRect().Size * 0.5f + new Vector2(80, 40);
        var anchor = ToLocal(cursor);
        Scroll(MouseButton.WheelUp, cursor);
        Check(Mathf.IsEqualApprox(Scale.X, 1.1f), "Wheel up must zoom in by 10 percent");
        Check(ToLocal(cursor).DistanceTo(anchor) < 0.01f, "Zoom must preserve the world point under the cursor");
        Scroll(MouseButton.WheelDown, cursor);
        Check(Scale.IsEqualApprox(Vector2.One), "Wheel down must reverse a zoom step");
        Scroll(MouseButton.WheelUp, cursor, pressed: false);
        Check(Scale.IsEqualApprox(Vector2.One), "Wheel release must not zoom");
        Scroll(MouseButton.WheelUp, cursor, factor: 0.5f);
        Check(Mathf.IsEqualApprox(Scale.X, Mathf.Sqrt(1.1f)), "Fractional wheel input must be respected");

        foreach (var zoomCase in new[] { (MouseButton.WheelUp, 2.0f), (MouseButton.WheelDown, 0.5f) })
        {
            for (var step = 0; step < 40; step++) Scroll(zoomCase.Item1, cursor);
            Check(Scale.IsEqualApprox(Vector2.One * zoomCase.Item2), "Zoom must stop at its configured limit");
            var stablePosition = Position;
            Scroll(zoomCase.Item1, cursor);
            Check(Position.DistanceTo(stablePosition) < 0.01f, "Scrolling at the limit must not move the view");
            var focus = new Vector2I(-5, 4);
            Call("CenterViewOnCell", focus);
            Check(ToGlobal(new Vector2(-288, 288)).DistanceTo(GetViewportRect().Size * 0.5f) < 0.01f,
                "Focus centering must account for zoom and negative cells");
            Check((bool)Call("IsPointInsideVisibleGrid", ToGlobal(new Vector2(-6390, -6390))),
                "Scaled hit testing must accept points inside negative map bounds");
            Check(!(bool)Call("IsPointInsideVisibleGrid", ToGlobal(new Vector2(-6410, -6410))),
                "Scaled hit testing must reject points outside map bounds");
            Call("SetViewPositionClamped", new Vector2(100000, 100000));
            Check(Mathf.IsEqualApprox(Position.Y, 6400 * Scale.Y + 96), "Top clamp must use scaled bounds");
            Call("SetViewPositionClamped", new Vector2(-100000, -100000));
            Check(Mathf.IsEqualApprox(Position.Y, GetViewportRect().Size.Y - 6464 * Scale.Y - 96),
                "Bottom clamp must use scaled bounds");
        }

        Scale = Vector2.One;
        Position = Vector2.Zero;
        Field("_isExplorationAutoMoving").SetValue(this, true);
        Scroll(MouseButton.WheelUp, cursor);
        Check(Scale.X > 1, "Zoom must remain available during exploration movement");
        Field("_isExplorationAutoMoving").SetValue(this, false);
        Field("_isPanningView").SetValue(this, true);
        Scroll(MouseButton.WheelUp, cursor);
        Check(((Vector2)Field("_viewPanStartPosition").GetValue(this)).IsEqualApprox(Position),
            "Zoom must rebase an active drag to avoid jumping");

        var hud = new HudController();
        try
        {
            typeof(HudController).GetField("_isDraggingPanel", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, true);
            Field("_hud").SetValue(this, hud);
            var previousScale = Scale;
            Scroll(MouseButton.WheelUp, cursor);
            Check(Scale.IsEqualApprox(previousScale), "HUD input blocking must prevent world zoom");
        }
        finally
        {
            Field("_hud").SetValue(this, null);
            hud.Free();
        }
        return failures;
    }

    private void Scroll(MouseButton button, Vector2 cursor, bool pressed = true, float factor = 1.0f)
    {
        using var input = new InputEventMouseButton
        {
            ButtonIndex = button, Pressed = pressed, Factor = factor,
            Position = cursor, GlobalPosition = cursor
        };
        _Input(input);
    }

    private static FieldInfo Field(string name) =>
        typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    private object Call(string name, params object[] arguments) =>
        typeof(BattleController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this, arguments);
}