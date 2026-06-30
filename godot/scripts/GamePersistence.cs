using Godot;
using Godot.Collections;
using System.Collections.Generic;

public sealed class GamePersistence
{
    private readonly IGamePersistenceHost _host;
    private readonly string _saveFilePath;

    public GamePersistence(IGamePersistenceHost host, string saveFilePath)
    {
        _host = host;
        _saveFilePath = saveFilePath;
    }

    public void PersistSaveGame(bool notifyStatus)
    {
        var saveData = BuildSaveData();
        var encoded = EncodeDictionaryForSave(saveData);

        using var file = FileAccess.Open(_saveFilePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            if (notifyStatus)
            {
                _host.SetStatusText("Failed to save game state.");
            }

            return;
        }

        file.StoreString(Json.Stringify(encoded));
        if (notifyStatus)
        {
            _host.SetStatusText("Game saved.");
        }
    }

    public bool TryLoadSaveGame(bool notifyStatus)
    {
        if (!FileAccess.FileExists(_saveFilePath))
        {
            if (notifyStatus)
            {
                _host.SetStatusText("No save file found.");
            }

            return false;
        }

        using var file = FileAccess.Open(_saveFilePath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            if (notifyStatus)
            {
                _host.SetStatusText("Failed to open save file.");
            }

            return false;
        }

        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            if (notifyStatus)
            {
                _host.SetStatusText("Save file is invalid.");
            }

            return false;
        }

        var decoded = DecodeDictionaryFromSave((Dictionary)parsed);
        ApplySaveData(decoded);
        if (notifyStatus)
        {
            _host.SetStatusText("Game loaded.");
        }

        return true;
    }

    public bool DeleteSaveGame(bool notifyStatus)
    {
        if (!FileAccess.FileExists(_saveFilePath))
        {
            if (notifyStatus)
            {
                _host.SetStatusText("No save file found.");
            }

            return false;
        }

        var absolutePath = ProjectSettings.GlobalizePath(_saveFilePath);
        var result = DirAccess.RemoveAbsolute(absolutePath);
        if (result != Error.Ok)
        {
            if (notifyStatus)
            {
                _host.SetStatusText("Failed to delete save file.");
            }

            return false;
        }

        if (notifyStatus)
        {
            _host.SetStatusText("Save deleted.");
        }

        return true;
    }

    private Dictionary BuildSaveData()
    {
        _host.SaveClearedEncounterStateForCurrentMap();

        var selectedAbilities = new Dictionary();
        foreach (var entry in _host.SelectedAbilityIdByUnitId)
        {
            selectedAbilities[entry.Key] = entry.Value;
        }

        var equippedByUnit = new Dictionary();
        foreach (var unitEntry in _host.EquippedItemsByUnitId)
        {
            var bySlot = new Dictionary();
            foreach (var slotEntry in unitEntry.Value)
            {
                bySlot[slotEntry.Key] = slotEntry.Value;
            }

            equippedByUnit[unitEntry.Key] = bySlot;
        }

        var partyInventory = new Array<string>();
        foreach (var itemId in _host.PartyInventoryItemIds)
        {
            partyInventory.Add(itemId);
        }

        var unitSnapshots = _host.BuildUnitSnapshots();

        return new Dictionary
        {
            { "version", 1 },
            { "current_map_id", _host.CurrentMapId },
            { "flow_state", _host.GetFlowStateToken() },
            { "active_encounter_id", _host.ActiveEncounterId },
            { "explorer_unit_id", _host.GetExplorerUnitId() },
            { "selected_character_unit_id", _host.SelectedCharacterUnitId },
            { "selected_ability_id_by_unit_id", selectedAbilities },
            { "party_inventory_item_ids", partyInventory },
            { "equipped_items_by_unit_id", equippedByUnit },
            { "cleared_encounter_ids_by_map", BuildSetMapSnapshot(_host.ClearedEncounterIdsByMap) },
            { "opened_prop_ids_by_map", BuildSetMapSnapshot(_host.OpenedPropIdsByMap) },
            { "looted_bag_ids_by_map", BuildSetMapSnapshot(_host.LootedBagIdsByMap) },
            { "loot_bags_by_map", BuildLootBagsByMapSnapshot(_host.LootBagsByMap) },
            { "unit_snapshots", unitSnapshots },
        };
    }

    private void ApplySaveData(Dictionary saveData)
    {
        _host.SelectedAbilityIdByUnitId.Clear();
        _host.EquippedItemsByUnitId.Clear();
        _host.PartyInventoryItemIds.Clear();
        _host.ClearedEncounterIdsByMap.Clear();
        _host.OpenedPropIdsByMap.Clear();
        _host.LootedBagIdsByMap.Clear();
        _host.LootBagsByMap.Clear();

        _host.CurrentMapId = GetString(saveData, "current_map_id", "map-a");
        _host.ActiveEncounterId = GetString(saveData, "active_encounter_id", "");
        _host.SelectedCharacterUnitId = GetString(saveData, "selected_character_unit_id", "");

        RestoreSelectedAbilityMap(GetDictionary(saveData, "selected_ability_id_by_unit_id"));
        RestoreSetMapSnapshot(GetDictionary(saveData, "cleared_encounter_ids_by_map"), _host.ClearedEncounterIdsByMap);
        RestoreSetMapSnapshot(GetDictionary(saveData, "opened_prop_ids_by_map"), _host.OpenedPropIdsByMap);
        RestoreSetMapSnapshot(GetDictionary(saveData, "looted_bag_ids_by_map"), _host.LootedBagIdsByMap);
        RestoreLootBagsByMapSnapshot(GetDictionary(saveData, "loot_bags_by_map"), _host.LootBagsByMap);

        _host.SpawnMapEncounter(_host.CurrentMapId);

        // Spawn seeds default map/config equipment; clear it so save data is the single source of truth.
        _host.PartyInventoryItemIds.Clear();
        _host.EquippedItemsByUnitId.Clear();

        // Restore runtime inventory/equipment collections from save.
        RestorePartyInventory(GetStringArray(saveData, "party_inventory_item_ids"));
        RestoreEquippedItemsByUnit(GetDictionary(saveData, "equipped_items_by_unit_id"));

        _host.ApplyUnitSnapshots(GetDictionaryArray(saveData, "unit_snapshots"));

        var explorerId = GetString(saveData, "explorer_unit_id", "");
        _host.SetExplorerUnitById(explorerId);

        var flowState = GetString(saveData, "flow_state", "exploration");
        _host.RestoreFlowState(flowState);

        _host.SyncHudFromGameState();
        _host.RequestRedraw();
    }

    private static Dictionary BuildSetMapSnapshot(System.Collections.Generic.Dictionary<string, HashSet<string>> source)
    {
        var snapshot = new Dictionary();
        foreach (var entry in source)
        {
            var values = new Array<string>();
            foreach (var value in entry.Value)
            {
                values.Add(value);
            }

            snapshot[entry.Key] = values;
        }

        return snapshot;
    }

    private static Dictionary BuildLootBagsByMapSnapshot(System.Collections.Generic.Dictionary<string, Array<Dictionary>> source)
    {
        var snapshot = new Dictionary();
        foreach (var entry in source)
        {
            var bags = new Array<Dictionary>();
            foreach (var bag in entry.Value)
            {
                bags.Add(CopyDictionary(bag));
            }

            snapshot[entry.Key] = bags;
        }

        return snapshot;
    }

    private void RestoreSelectedAbilityMap(Dictionary snapshot)
    {
        foreach (var key in snapshot.Keys)
        {
            var unitId = ((Variant)key).AsString();
            if (string.IsNullOrEmpty(unitId))
            {
                continue;
            }

            _host.SelectedAbilityIdByUnitId[unitId] = ((Variant)snapshot[key]).AsString();
        }
    }

    private void RestorePartyInventory(Array<string> snapshot)
    {
        foreach (var itemId in snapshot)
        {
            if (!string.IsNullOrEmpty(itemId))
            {
                _host.PartyInventoryItemIds.Add(itemId);
            }
        }
    }

    private void RestoreEquippedItemsByUnit(Dictionary snapshot)
    {
        foreach (var key in snapshot.Keys)
        {
            var unitId = ((Variant)key).AsString();
            if (string.IsNullOrEmpty(unitId))
            {
                continue;
            }

            var slotDictionary = GetDictionary(snapshot, unitId);
            var bySlot = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var slotKey in slotDictionary.Keys)
            {
                var slot = ((Variant)slotKey).AsString();
                if (string.IsNullOrEmpty(slot))
                {
                    continue;
                }

                bySlot[slot] = ((Variant)slotDictionary[slotKey]).AsString();
            }

            _host.EquippedItemsByUnitId[unitId] = bySlot;
        }
    }

    private static void RestoreSetMapSnapshot(Dictionary snapshot, System.Collections.Generic.Dictionary<string, HashSet<string>> target)
    {
        foreach (var key in snapshot.Keys)
        {
            var mapId = ((Variant)key).AsString();
            if (string.IsNullOrEmpty(mapId))
            {
                continue;
            }

            var values = new HashSet<string>();
            var ids = (Array)((Variant)snapshot[key]);
            foreach (var id in ids)
            {
                var value = ((Variant)id).AsString();
                if (!string.IsNullOrEmpty(value))
                {
                    values.Add(value);
                }
            }

            target[mapId] = values;
        }
    }

    private static void RestoreLootBagsByMapSnapshot(Dictionary snapshot, System.Collections.Generic.Dictionary<string, Array<Dictionary>> target)
    {
        foreach (var key in snapshot.Keys)
        {
            var mapId = ((Variant)key).AsString();
            if (string.IsNullOrEmpty(mapId))
            {
                continue;
            }

            var bags = new Array<Dictionary>();
            var bagArray = (Array)((Variant)snapshot[key]);
            foreach (var bag in bagArray)
            {
                var variant = (Variant)bag;
                if (variant.VariantType == Variant.Type.Dictionary)
                {
                    bags.Add(CopyDictionary((Dictionary)variant));
                }
            }

            target[mapId] = bags;
        }
    }

    private static Dictionary EncodeDictionaryForSave(Dictionary source)
    {
        var encoded = new Dictionary();
        foreach (var key in source.Keys)
        {
            var keyString = ((Variant)key).AsString();
            encoded[keyString] = EncodeVariantForSave((Variant)source[key]);
        }

        return encoded;
    }

    private static Dictionary DecodeDictionaryFromSave(Dictionary source)
    {
        var decoded = new Dictionary();
        foreach (var key in source.Keys)
        {
            var keyString = ((Variant)key).AsString();
            decoded[keyString] = DecodeVariantFromSave((Variant)source[key]);
        }

        return decoded;
    }

    private static Variant EncodeVariantForSave(Variant value)
    {
        switch (value.VariantType)
        {
            case Variant.Type.Dictionary:
                return EncodeDictionaryForSave((Dictionary)value);
            case Variant.Type.Array:
                var encodedArray = new Array();
                foreach (var entry in (Array)value)
                {
                    encodedArray.Add(EncodeVariantForSave((Variant)entry));
                }

                return encodedArray;
            case Variant.Type.Vector2I:
                var vec = (Vector2I)value;
                return new Dictionary
                {
                    { "__type", "Vector2I" },
                    { "x", vec.X },
                    { "y", vec.Y },
                };
            default:
                return value;
        }
    }

    private static Variant DecodeVariantFromSave(Variant value)
    {
        if (value.VariantType == Variant.Type.Dictionary)
        {
            var dict = (Dictionary)value;
            if (dict.ContainsKey("__type") && ((Variant)dict["__type"]).AsString() == "Vector2I")
            {
                return new Vector2I(
                    dict.ContainsKey("x") ? (int)((Variant)dict["x"]) : 0,
                    dict.ContainsKey("y") ? (int)((Variant)dict["y"]) : 0
                );
            }

            return DecodeDictionaryFromSave(dict);
        }

        if (value.VariantType == Variant.Type.Array)
        {
            var decodedArray = new Array();
            foreach (var entry in (Array)value)
            {
                decodedArray.Add(DecodeVariantFromSave((Variant)entry));
            }

            return decodedArray;
        }

        return value;
    }

    private static Dictionary GetDictionary(Dictionary dict, string key)
    {
        if (!dict.ContainsKey(key))
        {
            return new Dictionary();
        }

        var value = (Variant)dict[key];
        return value.VariantType == Variant.Type.Dictionary ? (Dictionary)value : new Dictionary();
    }

    private static Array<string> GetStringArray(Dictionary dict, string key)
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
            var text = ((Variant)entry).AsString();
            if (!string.IsNullOrEmpty(text))
            {
                result.Add(text);
            }
        }

        return result;
    }

    private static Array<Dictionary> GetDictionaryArray(Dictionary dict, string key)
    {
        if (!dict.ContainsKey(key))
        {
            return new Array<Dictionary>();
        }

        var value = (Variant)dict[key];
        if (value.VariantType != Variant.Type.Array)
        {
            return new Array<Dictionary>();
        }

        var result = new Array<Dictionary>();
        foreach (var entry in (Array)value)
        {
            var variant = (Variant)entry;
            if (variant.VariantType == Variant.Type.Dictionary)
            {
                result.Add((Dictionary)variant);
            }
        }

        return result;
    }

    private static string GetString(Dictionary dict, string key, string fallback)
    {
        return dict.ContainsKey(key) ? ((Variant)dict[key]).AsString() : fallback;
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
