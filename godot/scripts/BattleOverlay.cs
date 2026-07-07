using Godot;

public partial class BattleOverlay : Node2D
{
    private BattleController _battleController;

    public override void _Ready()
    {
        _battleController = GetParentOrNull<BattleController>();
        ZIndex = 1;
    }

    public override void _Process(double delta)
    {
        // Keep overlay visuals responsive to hover and turn-state changes.
        QueueRedraw();
    }

    public override void _Draw()
    {
        _battleController?.DrawWorldOverlays(this);
    }
}
