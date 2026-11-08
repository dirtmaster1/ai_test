using Godot;

public partial class PartyCard : Button
{
    [Signal]
    public delegate void UnitSelectedEventHandler(string unitId);

    [Signal]
    public delegate void ReorderRequestedEventHandler(string sourceUnitId, string targetUnitId, bool insertAfter);

    public string UnitId { get; set; } = "";
    public bool ReorderEnabled { get; set; }
    public bool TargetHighlighted { get; set; }

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

        var preview = new PanelContainer
        {
            CustomMinimumSize = Size,
            Size = Size,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.82f)
        };
        var cardStyle = GetThemeStylebox("normal", "Button");
        if (cardStyle != null)
        {
            preview.AddThemeStyleboxOverride("panel", (StyleBox)cardStyle.Duplicate());
        }

        if (GetChildCount() > 0 && GetChild(0).Duplicate() is Control cardContent)
        {
            cardContent.MouseFilter = MouseFilterEnum.Ignore;
            preview.AddChild(cardContent);
            cardContent.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        SetDragPreview(preview);
        return UnitId;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return ReorderEnabled
            && data.VariantType == Variant.Type.String
            && !string.IsNullOrEmpty(data.AsString());
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_CanDropData(atPosition, data))
        {
            var sourceUnitId = data.AsString();
            if (sourceUnitId == UnitId)
            {
                EmitReorderForCurrentPosition(sourceUnitId);
                return;
            }

            EmitSignal(SignalName.ReorderRequested, sourceUnitId, UnitId, atPosition.Y >= Size.Y * 0.5f);
        }
    }

    private void EmitReorderForCurrentPosition(string sourceUnitId)
    {
        var siblingIndex = GetIndex();
        var siblings = GetParent()?.GetChildren();
        if (siblings == null)
        {
            return;
        }

        for (var index = siblingIndex + 1; index < siblings.Count; index++)
        {
            if (siblings[index] is PartyCard nextCard)
            {
                EmitSignal(SignalName.ReorderRequested, sourceUnitId, nextCard.UnitId, false);
                return;
            }
        }

        for (var index = siblingIndex - 1; index >= 0; index--)
        {
            if (siblings[index] is PartyCard previousCard)
            {
                EmitSignal(SignalName.ReorderRequested, sourceUnitId, previousCard.UnitId, true);
                return;
            }
        }
    }
}