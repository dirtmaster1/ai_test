using Godot;

public partial class PartyCard : Button
{
    [Signal]
    public delegate void UnitSelectedEventHandler(string unitId);

    [Signal]
    public delegate void ReorderRequestedEventHandler(string sourceUnitId, string targetUnitId);

    public string UnitId { get; set; } = "";
    public bool ReorderEnabled { get; set; }

    public override void _Pressed()
    {
        EmitSignal(SignalName.UnitSelected, UnitId);
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!ReorderEnabled || string.IsNullOrEmpty(UnitId))
        {
            return default;
        }

        var preview = new Label
        {
            Text = "Move party member",
            Modulate = new Color(0.94f, 0.9f, 0.78f, 0.9f)
        };
        SetDragPreview(preview);
        return UnitId;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return ReorderEnabled
            && data.VariantType == Variant.Type.String
            && data.AsString() != UnitId;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_CanDropData(atPosition, data))
        {
            EmitSignal(SignalName.ReorderRequested, data.AsString(), UnitId);
        }
    }
}