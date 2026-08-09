using Godot;
using Godot.Collections;

public partial class GameData : Node
{
    private const string DataPath = "res://resources/game_data.json";

    public Dictionary RawData { get; private set; } = new();
    public Dictionary Abilities { get; private set; } = new();
    public Dictionary Spells { get; private set; } = new();
    public Dictionary Items { get; private set; } = new();
    public Dictionary Dialogues { get; private set; } = new();
    public Dictionary Vendors { get; private set; } = new();
    public Dictionary Characters { get; private set; } = new();

    public override void _Ready()
    {
        LoadData();
    }

    public void LoadData()
    {
        RawData = LoadJson(DataPath);
        Abilities = TryGetDictionary(RawData, "abilities");
        Spells = TryGetDictionary(RawData, "spells");
        Items = TryGetDictionary(RawData, "items");
        Dialogues = TryGetDictionary(RawData, "dialogues");
        Vendors = TryGetDictionary(RawData, "vendors");
        Characters = TryGetDictionary(RawData, "characters");
    }

    private static Dictionary LoadJson(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            return new Dictionary();
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return new Dictionary();
        }

        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            return new Dictionary();
        }

        return (Dictionary)parsed;
    }

    private static Dictionary TryGetDictionary(Dictionary dict, string key)
    {
        if (!dict.ContainsKey(key))
        {
            return new Dictionary();
        }

        var value = (Variant)dict[key];
        return value.VariantType == Variant.Type.Dictionary ? (Dictionary)value : new Dictionary();
    }

    public Dictionary GetAbility(string id)
    {
        return TryGetDictionary(Abilities, id);
    }

    public Dictionary GetSpell(string id)
    {
        return TryGetDictionary(Spells, id);
    }

    public Dictionary GetItem(string id)
    {
        return TryGetDictionary(Items, id);
    }

    public Dictionary GetDialogue(string id)
    {
        return TryGetDictionary(Dialogues, id);
    }

    public Dictionary GetVendorProfile(string id)
    {
        return TryGetDictionary(Vendors, id);
    }

    public int GetVendorStartingGold(string id, int fallback = 100)
    {
        return GetInt(GetVendorProfile(id), "starting_gold", fallback);
    }

    public Array<string> GetVendorStartingInventory(string id)
    {
        return TryGetStringArray(GetVendorProfile(id), "starting_inventory_item_ids");
    }

    public float GetVendorBuyMultiplier(string id, float fallback = 1.0f)
    {
        return Mathf.Max(0.1f, GetFloat(GetVendorProfile(id), "buy_price_multiplier", fallback));
    }

    public float GetVendorSellMultiplier(string id, float fallback = 1.0f)
    {
        return Mathf.Max(0.1f, GetFloat(GetVendorProfile(id), "sell_price_multiplier", fallback));
    }

    public Dictionary GetCharacterTemplate(string id)
    {
        var templates = TryGetDictionary(Characters, "templates");
        return TryGetDictionary(templates, id);
    }

    public Array<Dictionary> GetDefaultPartyTemplates()
    {
        var result = new Array<Dictionary>();
        var templates = TryGetDictionary(Characters, "templates");
        var partyIds = TryGetStringArray(Characters, "default_party");

        foreach (var id in partyIds)
        {
            var template = TryGetDictionary(templates, id);
            if (template.Count == 0)
            {
                continue;
            }

            result.Add(CopyDictionary(template));
        }

        return result;
    }

    private static Array<string> TryGetStringArray(Dictionary dict, string key)
    {
        if (!dict.ContainsKey(key))
        {
            return new Array<string>();
        }

        var value = (Variant)dict[key];
        if (value.VariantType != Variant.Type.Array)
        {
            return new Array<string>();
        }

        var result = new Array<string>();
        foreach (var entry in (Array)value)
        {
            var variant = (Variant)entry;
            if (variant.VariantType == Variant.Type.String)
            {
                result.Add(variant.AsString());
            }
        }

        return result;
    }

    private static int GetInt(Dictionary dict, string key, int fallback)
    {
        if (!dict.ContainsKey(key))
        {
            return fallback;
        }

        var value = (Variant)dict[key];
        if (value.VariantType == Variant.Type.Int)
        {
            return (int)value;
        }

        if (value.VariantType == Variant.Type.Float)
        {
            return Mathf.RoundToInt((float)value);
        }

        return int.TryParse(value.AsString(), out var parsed) ? parsed : fallback;
    }

    private static float GetFloat(Dictionary dict, string key, float fallback)
    {
        if (!dict.ContainsKey(key))
        {
            return fallback;
        }

        var value = (Variant)dict[key];
        if (value.VariantType == Variant.Type.Float)
        {
            return (float)value;
        }

        if (value.VariantType == Variant.Type.Int)
        {
            return (int)value;
        }

        return float.TryParse(value.AsString(), out var parsed) ? parsed : fallback;
    }

    private static Dictionary CopyDictionary(Dictionary source)
    {
        var copy = new Dictionary();
        foreach (var key in source.Keys)
        {
            copy[key] = source[key];
        }

        return copy;
    }
}
