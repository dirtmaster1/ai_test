using Godot;
using System;

public partial class SharedPartyInventoryList : ItemList
{
    private const string ItemDragPrefix = "inventory-item:";
    private const string EquippedSlotDragPrefix = "equipped-slot:";

    public event Action<string> EquippedItemDroppedIntoInventory;

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var itemIndex = GetItemAtPosition(atPosition);
        if (itemIndex < 0)
        {
            return default;
        }

        var metadata = GetItemMetadata(itemIndex);
        var itemId = metadata.VariantType == Variant.Type.String ? metadata.AsString() : "";
        if (string.IsNullOrEmpty(itemId))
        {
            return default;
        }

        var preview = new Label
        {
            Text = GetItemText(itemIndex),
            Modulate = new Color(0.94f, 0.9f, 0.78f, 0.9f)
        };
        SetDragPreview(preview);
        return $"{ItemDragPrefix}{itemId}";
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return TryGetEquippedSlot(data, out _);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (TryGetEquippedSlot(data, out var slotKey))
        {
            EquippedItemDroppedIntoInventory?.Invoke(slotKey);
        }
    }

    public static bool TryGetItemId(Variant data, out string itemId)
    {
        itemId = "";
        if (data.VariantType != Variant.Type.String)
        {
            return false;
        }

        var payload = data.AsString();
        if (!payload.StartsWith(ItemDragPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        itemId = payload.Substring(ItemDragPrefix.Length);
        return !string.IsNullOrEmpty(itemId);
    }

    public static bool TryGetEquippedSlot(Variant data, out string slotKey)
    {
        slotKey = "";
        if (data.VariantType != Variant.Type.String)
        {
            return false;
        }

        var payload = data.AsString();
        if (!payload.StartsWith(EquippedSlotDragPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        slotKey = payload.Substring(EquippedSlotDragPrefix.Length);
        return !string.IsNullOrEmpty(slotKey);
    }
}
