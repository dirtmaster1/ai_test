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
    public event Action<string> EquippedItemDoubleClicked;

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton
            && mouseButton.ButtonIndex == MouseButton.Left
            && mouseButton.Pressed
            && mouseButton.DoubleClick
            && !string.IsNullOrEmpty(EquippedSlotKey))
        {
            EquippedItemDoubleClicked?.Invoke(EquippedSlotKey);
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (string.IsNullOrEmpty(EquippedSlotKey))
        {
            return default;
        }

        SetDragPreview(DragPreview.Create(Icon));
        return $"{EquippedSlotDragPrefix}{EquippedSlotKey}";
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragBegin)
        {
            DragPreview.BeginDrag();
        }
        else if (what == NotificationDragEnd)
        {
            DragPreview.EndDrag();
        }
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
