using Godot;
using System;

public partial class AbilityBarButton : Button
{
    private const string AbilityDragPrefix = "ability-tray:";

    public int QuickSlotIndex { get; set; } = -1;
    public string AbilityId { get; set; } = "";
    public bool CanDragAbility { get; set; }

    public event Action<int, string> AbilityDropped;

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!CanDragAbility || string.IsNullOrEmpty(AbilityId))
        {
            return default;
        }

        var preview = new Button
        {
            Text = Text,
            Icon = Icon,
            CustomMinimumSize = new Vector2(184, 48),
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None
        };
        SetDragPreview(preview);
        return $"{AbilityDragPrefix}{AbilityId}";
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (QuickSlotIndex < 0 || data.VariantType != Variant.Type.String)
        {
            return false;
        }

        var dragData = data.AsString();
        return dragData.StartsWith(AbilityDragPrefix, StringComparison.Ordinal)
            && dragData.Length > AbilityDragPrefix.Length;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_CanDropData(atPosition, data))
        {
            AbilityDropped?.Invoke(QuickSlotIndex, data.AsString()[AbilityDragPrefix.Length..]);
        }
    }
}
