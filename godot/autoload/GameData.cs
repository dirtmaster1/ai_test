using Godot;
using Godot.Collections;

public partial class GameData : Node
{
    private const string DataPath = "res://resources/game_data.json";

    public Dictionary RawData { get; private set; } = new();
    public Dictionary Abilities { get; private set; } = new();
    public Dictionary Spells { get; private set; } = new();
    public Dictionary Items { get; private set; } = new();

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
}
