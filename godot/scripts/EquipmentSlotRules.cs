using Godot;
using Godot.Collections;

public static class EquipmentSlotRules
{
    public static bool CanEquipItemToSlot(Dictionary item, string targetSlotKey)
    {
        if (item == null)
        {
            return false;
        }

        var type = GetString(item, "type");
        var slot = GetString(item, "slot");

        if (IsConsumableSlotKey(targetSlotKey))
        {
            return slot == "consumable" && (type is "potion" or "food" or "consumable");
        }

        if (targetSlotKey is "head" or "body" or "feet")
        {
            return type == "armor" && slot == targetSlotKey;
        }

        if (targetSlotKey == "1-handed-a")
        {
            return (type is "weapon" or "armor")
                && (slot == "1-handed" || type == "weapon" && slot == "2-handed");
        }

        return targetSlotKey == "1-handed-b"
            && (type is "weapon" or "armor")
            && slot == "1-handed";
    }

    public static bool IsConsumableSlotKey(string slotKey)
    {
        return slotKey is "consumable-1" or "consumable-2";
    }

    private static string GetString(Dictionary item, string key)
    {
        return item.ContainsKey(key) ? ((Variant)item[key]).AsString() : "";
    }
}
