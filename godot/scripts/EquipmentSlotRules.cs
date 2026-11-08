using Godot;
using Godot.Collections;

public static class EquipmentSlotRules
{
    public static bool CanClassEquipItem(Dictionary item, string classId, Dictionary classRules)
    {
        if (item == null)
        {
            return false;
        }

        var itemType = GetString(item, "type");
        if (itemType is not ("weapon" or "armor"))
        {
            return true;
        }

        if (item.ContainsKey("allowed_classes")
            && !IsAllowedByStringList(item, "allowed_classes", classId))
        {
            return false;
        }

        if (classRules == null || classRules.Count == 0)
        {
            return true;
        }

        if (itemType == "weapon" && classRules.ContainsKey("allowed_weapon_types"))
        {
            return IsAllowedByStringList(item, "weapon_type", classRules, "allowed_weapon_types");
        }

        if (itemType == "armor" && classRules.ContainsKey("allowed_armor_weights"))
        {
            return IsAllowedByStringList(item, "armor_weight", classRules, "allowed_armor_weights");
        }

        return true;
    }

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

        if (targetSlotKey is "head" or "neck" or "body" or "legs" or "back" or "feet")
        {
            return type == "armor" && slot == targetSlotKey;
        }

        if (IsRingSlotKey(targetSlotKey))
        {
            return type == "armor" && slot == "ring";
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

    public static bool IsRingSlotKey(string slotKey)
    {
        return slotKey is "ring-1" or "ring-2";
    }

    public static bool CanMoveBetweenSlots(string sourceSlotKey, string targetSlotKey)
    {
        return IsConsumableSlotKey(sourceSlotKey) && IsConsumableSlotKey(targetSlotKey)
            || IsRingSlotKey(sourceSlotKey) && IsRingSlotKey(targetSlotKey);
    }

    private static string GetString(Dictionary item, string key)
    {
        return item.ContainsKey(key) ? ((Variant)item[key]).AsString() : "";
    }

    private static bool IsAllowedByStringList(Dictionary source, string sourceKey, string target)
    {
        if (!source.ContainsKey(sourceKey))
        {
            return false;
        }

        return IsAllowedByStringArray((Variant)source[sourceKey], target);
    }

    private static bool IsAllowedByStringList(Dictionary item, string itemKey, Dictionary rules, string ruleKey)
    {
        return !rules.ContainsKey(ruleKey)
            || IsAllowedByStringArray((Variant)rules[ruleKey], GetString(item, itemKey));
    }

    private static bool IsAllowedByStringArray(Variant value, string target)
    {
        if (value.VariantType != Variant.Type.Array)
        {
            return false;
        }

        var normalizedTarget = target?.Trim().ToLowerInvariant() ?? "";
        foreach (var entry in (Array)value)
        {
            var candidate = ((Variant)entry).AsString().Trim().ToLowerInvariant();
            if (candidate == "*" || candidate == normalizedTarget)
            {
                return true;
            }
        }

        return false;
    }
}
