using Godot;

public partial class BattleOverlay : Node2D
{
    private BattleController _battleController;
    private Node2D _fogLayer;
    private Node2D _foregroundLayer;

    public override void _Ready()
    {
        _battleController = GetParentOrNull<BattleController>();
        ZIndex = 1;
        TextureFilter = CanvasItem.TextureFilterEnum.Linear;
        _fogLayer = new Node2D
        {
            Name = "Fog",
            Material = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://assets/ui/fog_step_blend.gdshader")
            }
        };
        AddChild(_fogLayer);
        _fogLayer.Draw += () => _battleController?.DrawFogOfWarOverlay(_fogLayer);
        _foregroundLayer = new Node2D { Name = "Foreground" };
        AddChild(_foregroundLayer);
        _foregroundLayer.Draw += () => _battleController?.DrawWorldForegroundOverlays(_foregroundLayer);
    }

    public override void _Process(double delta)
    {
        // Keep overlay visuals responsive to hover and turn-state changes.
        QueueRedraw();
        _fogLayer.QueueRedraw();
        _foregroundLayer.QueueRedraw();
    }

    public override void _Draw()
    {
        _battleController?.DrawWorldOverlays(this);
    }
}
