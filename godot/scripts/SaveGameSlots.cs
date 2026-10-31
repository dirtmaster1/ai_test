using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Globalization;

public sealed class SaveGameSlots
{
    public sealed record PartyMember(string UnitId, string Name, int Level, Texture2D Portrait);

    public const int Limit = 10;
    public const string DirectoryPath = "user://saves";
    public const string AutosavePath = "user://dark_dungeon_tactics_save.json";
    public const string RecoveryPath = "user://dark_dungeon_tactics_recovery.json";
    public const int NameLength = 60;

    public sealed record Entry(int Slot, string Path, string Name, string Map, string Timestamp, string Error)
    {
        public bool CanLoad => string.IsNullOrEmpty(Error);
        public DateTimeOffset SavedAt { get; init; }
        public IReadOnlyList<PartyMember> PartyMembers { get; init; } = System.Array.Empty<PartyMember>();
    }

    private readonly string _directory;

    public SaveGameSlots(string directory = DirectoryPath)
    {
        _directory = directory.TrimEnd('/');
    }

    public string GetPath(int slot)
    {
        if (slot < 1 || slot > Limit)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return $"{_directory}/slot-{slot:00}.json";
    }

    public List<Entry> ListManualSaves()
    {
        var entries = new List<Entry>();
        for (var slot = 1; slot <= Limit; slot++)
        {
            var path = GetPath(slot);
            if (Godot.FileAccess.FileExists(path))
                entries.Add(ReadEntry(slot, path, $"Save {slot}"));
        }
        return entries;
    }

    public int FindEmptySlot()
    {
        for (var slot = 1; slot <= Limit; slot++)
        {
            if (!Godot.FileAccess.FileExists(GetPath(slot)))
                return slot;
        }
        return 0;
    }

    public bool TrySave(IGamePersistenceHost host, int slot, string name, bool overwrite, out string error)
    {
        error = "";
        name = name?.Trim() ?? "";
        if (slot < 1 || slot > Limit || name.Length == 0 || name.Length > NameLength)
        {
            error = $"Choose a save slot and a name of 1-{NameLength} characters.";
            return false;
        }
        var path = GetPath(slot);
        if (!overwrite && Godot.FileAccess.FileExists(path))
        {
            error = "This slot is no longer empty. Select it again to overwrite it.";
            return false;
        }
        var mkdirError = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(_directory));
        if (mkdirError != Godot.Error.Ok)
        {
            error = $"Could not create the save directory: {mkdirError}.";
            GD.PushError(error);
            return false;
        }
        var persistence = new GamePersistence(host, path);
        if (!persistence.TryPersistSaveGame(name))
        {
            error = persistence.LastError;
            return false;
        }
        return true;
    }

    public static Entry ReadEntry(int slot, string path, string fallbackName)
    {
        var modifiedAt = DateTimeOffset.FromUnixTimeSeconds((long)Godot.FileAccess.GetModifiedTime(path));
        if (!TryRead(path, out var data, out var error))
            return new Entry(slot, path, fallbackName, "Unavailable", "Unknown time", error) { SavedAt = modifiedAt };

        var name = data.ContainsKey("save_name") ? data["save_name"].AsString() : fallbackName;
        var mapId = data["current_map_id"].AsString();
        var timestamp = data.ContainsKey("saved_at_utc") ? data["saved_at_utc"].AsString() : "";
        var savedAt = DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : modifiedAt;
        var partyMembers = ReadPartyMembers(data);
        return new Entry(slot, path, name, MapDisplayName(mapId),
            savedAt.ToLocalTime().ToString("MMM d, yyyy  hh:mm tt", CultureInfo.CurrentCulture), "")
        {
            SavedAt = savedAt,
            PartyMembers = partyMembers
        };
    }

    private static List<PartyMember> ReadPartyMembers(Dictionary data)
    {
        var snapshotsById = new System.Collections.Generic.Dictionary<string, Dictionary>(StringComparer.Ordinal);
        foreach (var snapshot in GetDictionaryEntries(data, "unit_snapshots"))
        {
            var unitId = GetString(snapshot, "unit_id", "");
            if (!string.IsNullOrEmpty(unitId))
                snapshotsById[unitId] = snapshot;
        }

        var partyMembers = new List<PartyMember>();
        foreach (var member in GetDictionaryEntries(data, "party_roster"))
        {
            if (partyMembers.Count == 5)
                break;

            var unitId = GetString(member, "id", "");
            var name = GetString(member, "name", "Party Member");
            snapshotsById.TryGetValue(unitId, out var snapshot);
            var level = GetInt(snapshot, "level", GetInt(member, "level", 1));
            var spriteTexturePath = GetString(snapshot, "sprite_texture", "");
            partyMembers.Add(new PartyMember(
                unitId,
                name,
                Math.Max(1, level),
                Unit.GetTurnOrderIcon(unitId, name, spriteTexturePath)));
        }

        return partyMembers;
    }

    private static List<Dictionary> GetDictionaryEntries(Dictionary data, string key)
    {
        var entries = new List<Dictionary>();
        if (!data.ContainsKey(key))
            return entries;

        var value = (Variant)data[key];
        if (value.VariantType != Variant.Type.Array)
            return entries;

        foreach (var item in (Godot.Collections.Array)value)
        {
            var variant = (Variant)item;
            if (variant.VariantType == Variant.Type.Dictionary)
                entries.Add((Dictionary)variant);
        }
        return entries;
    }

    private static string GetString(Dictionary data, string key, string fallback)
    {
        if (!data.ContainsKey(key))
            return fallback;

        var value = (Variant)data[key];
        return value.VariantType == Variant.Type.String ? value.AsString() : fallback;
    }

    private static int GetInt(Dictionary data, string key, int fallback)
    {
        if (data == null || !data.ContainsKey(key))
            return fallback;

        var value = (Variant)data[key];
        return value.VariantType is Variant.Type.Int or Variant.Type.Float ? value.AsInt32() : fallback;
    }

    public bool TryDelete(int slot, out string error)
    {
        error = "";
        if (slot < 1 || slot > Limit)
        {
            error = "Only named saves can be deleted from this menu.";
            return false;
        }
        var path = GetPath(slot);
        if (!Godot.FileAccess.FileExists(path))
        {
            error = "This save no longer exists.";
            return false;
        }
        var result = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        if (result != Godot.Error.Ok)
        {
            error = $"Could not delete the save: {result}.";
            GD.PushError(error);
            return false;
        }
        return true;
    }

    public static string MapDisplayName(string mapId) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(mapId.Replace('-', ' ').Replace('_', ' '));

    public static bool TryRead(string path, out Dictionary data, out string error)
    {
        data = new Dictionary();
        error = "";
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            error = $"Could not open save: {Godot.FileAccess.GetOpenError()}.";
            return false;
        }
        using var json = new Json();
        if (json.Parse(file.GetAsText()) != Godot.Error.Ok || json.Data.VariantType != Variant.Type.Dictionary)
        {
            error = "This save is damaged or is not a valid game save.";
            return false;
        }
        data = (Dictionary)json.Data;
        if (!data.ContainsKey("current_map_id") || data["current_map_id"].VariantType != Variant.Type.String
            || string.IsNullOrWhiteSpace(data["current_map_id"].AsString())
            || !data.ContainsKey("unit_snapshots") || data["unit_snapshots"].VariantType != Variant.Type.Array
            || !data.ContainsKey("party_roster") || data["party_roster"].VariantType != Variant.Type.Array)
        {
            error = "This save is missing required game data.";
            return false;
        }
        return true;
    }
}
