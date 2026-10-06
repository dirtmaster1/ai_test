using Godot;
using System;

public partial class EquipmentSlotButton : Button
{
    private const string EquippedSlotDragPrefix = "equipped-slot:";

    public string DropSlotKey { get; set; } = "";
    public string EquippedSlotKey { get; set; } = "";
    public Func<string, string, bool> CanAcceptInventoryItem { get; set; }
    public Func<string, string, bool> CanAcceptEquippedItem { get; set; }

    public event Action<string, string> InventoryItemDropped;
    public event Action<string, string> EquippedItemDropped;

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (string.IsNullOrEmpty(EquippedSlotKey))
        {
            return default;
        }

        var previewText = TooltipText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var preview = new Label
        {
            Text = previewText.Length > 0 ? previewText[0] : EquippedSlotKey,
            Modulate = new Color(0.94f, 0.9f, 0.78f, 0.9f)
        };
        SetDragPreview(preview);
        return $"{EquippedSlotDragPrefix}{EquippedSlotKey}";
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (string.IsNullOrEmpty(DropSlotKey))
        {
            return false;
        }

        if (SharedPartyInventoryList.TryGetItemId(data, out var itemId))
        {
            return CanAcceptInventoryItem?.Invoke(itemId, DropSlotKey) == true;
        }

        return SharedPartyInventoryList.TryGetEquippedSlot(data, out var sourceSlotKey)
            && CanAcceptEquippedItem?.Invoke(sourceSlotKey, DropSlotKey) == true;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!_CanDropData(atPosition, data))
        {
            return;
        }

        if (SharedPartyInventoryList.TryGetItemId(data, out var itemId))
        {
            InventoryItemDropped?.Invoke(itemId, DropSlotKey);
        }
        else if (SharedPartyInventoryList.TryGetEquippedSlot(data, out var sourceSlotKey))
        {
            EquippedItemDropped?.Invoke(sourceSlotKey, DropSlotKey);
        }
    }
}
