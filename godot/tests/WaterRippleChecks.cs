using Godot;
using System.Reflection;

public partial class WaterRippleChecks : BattleController
{
    public override void _Ready()
    {
        typeof(BattleController)
            .GetMethod("InitializeWaterRipples", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(this, null);
    }

    public override void _Draw() { }
}
