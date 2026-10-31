using Godot;
using Godot.Collections;
using System.Collections.Generic;

public sealed class GamePersistence
{
    private readonly IGamePersistenceHost _host;
    private readonly string _saveFilePath;
    public string LastError { get; private set; } = "";

    public GamePersistence(IGamePersistenceHost host, string saveFilePath)
    {
        _host = host;
        _saveFilePath = saveFilePath;
    }

    public void PersistSaveGame(bool notifyStatus)
    {
        TryPersistSaveGame();
    }

    public bool TryPersistSaveGame(string saveName = null)
    {
        LastError = "";
        var saveData = BuildSaveData();
        saveData["saved_at_utc"] = System.DateTimeOffset.UtcNow.ToString("O");
        if (saveName != null)
            saveData["save_name"] = saveName;
        var encoded = EncodeDictionaryForSave(saveData);

        var temporaryPath = _saveFilePath + ".tmp";
        using (var file = FileAccess.Open(temporaryPath, FileAccess.ModeFlags.Write))
        {
            if (file == null)
                return Fail($"Could not open save file '{temporaryPath}' for writing: {FileAccess.GetOpenError()}.");
            file.StoreString(Json.Stringify(encoded));
            file.Flush();
            if (file.GetError() != Error.Ok)
                return Fail($"Could not write save file '{temporaryPath}': {file.GetError()}.");
        }

        try
        {
            System.IO.File.Move(ProjectSettings.GlobalizePath(temporaryPath),
                ProjectSettings.GlobalizePath(_saveFilePath), true);
        }
        catch (System.Exception exception) when (exception is System.IO.IOException or System.UnauthorizedAccessException)
        {
            return Fail($"Could not replace save file '{_saveFilePath}': {exception.Message}");
        }

        return true;
    }

    private bool Fail(string error)
    {
        LastError = error;
        GD.PushError(error);
        return false;
    }

    public bool HasSaveGame()
    {
        return FileAccess.FileExists(_saveFilePath);
    }

    public bool TryLoadSaveGame(bool notifyStatus)
    {
        LastError = "";
        if (!FileAccess.FileExists(_saveFilePath))
        {
            LastError = "The selected save no longer exists.";
            return false;
        }

        if (!SaveGameSlots.TryRead(_saveFilePath, out var parsed, out var error))
            return Fail(error);

        var decoded = DecodeDictionaryFromSave(parsed);
        ApplySaveData(decoded);

        return true;
    }

    public bool DeleteSaveGame(bool notifyStatus)
    {
        if (!FileAccess.FileExists(_saveFilePath))
        {
            return false;
        }

        var absolutePath = ProjectSettings.GlobalizePath(_saveFilePath);
        var result = DirAccess.RemoveAbsolute(absolutePath);
        if (result != Error.Ok)
        {
            return false;
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

        var quickAbilityIds = new Dictionary();
        foreach (var entry in _host.QuickAbilityIdsByUnitId)
        {
            var ids = new Array<string>();
            foreach (var abilityId in entry.Value)
            {
                ids.Add(abilityId);
            }

            quickAbilityIds[entry.Key] = ids;
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

        var identifiedItemIds = new Array<string>();
        foreach (var itemId in _host.IdentifiedItemIds)
        {
            identifiedItemIds.Add(itemId);
        }

        var vendorGoldById = new Dictionary();
        foreach (var entry in _host.VendorGoldById)
        {
            vendorGoldById[entry.Key] = entry.Value;
        }

        var vendorInventoryById = new Dictionary();
        foreach (var entry in _host.VendorInventoryItemIdsById)
        {
            var itemIds = new Array<string>();
            foreach (var itemId in entry.Value)
            {
                itemIds.Add(itemId);
            }

            vendorInventoryById[entry.Key] = itemIds;
        }

        var unitSnapshots = _host.BuildUnitSnapshots();
        var partyRoster = _host.BuildPartyRoster();
        var reserveRoster = _host.BuildReserveRoster();
        var recruitedNpcIds = _host.BuildRecruitedNpcIds();
        var pendingLevelUpNotices = _host.BuildPendingLevelUpNotices();
        var triggeredEventIds = new Array<string>();
        foreach (var eventId in _host.TriggeredEventIds)
        {
            triggeredEventIds.Add(eventId);
        }

        return new Dictionary
        {
            { "version", 1 },
            { "current_map_id", _host.CurrentMapId },
            { "flow_state", _host.GetFlowStateToken() },
            { "active_encounter_id", _host.ActiveEncounterId },
            { "explorer_unit_id", _host.GetExplorerUnitId() },
            { "selected_character_unit_id", _host.SelectedCharacterUnitId },
            { "selected_ability_id_by_unit_id", selectedAbilities },
            { "quick_ability_ids_by_unit_id", quickAbilityIds },
            { "party_gold", _host.PartyGold },
            { "party_inventory_item_ids", partyInventory },
            { "identified_item_ids", identifiedItemIds },
            { "vendor_gold_by_id", vendorGoldById },
            { "vendor_inventory_item_ids_by_id", vendorInventoryById },
            { "equipped_items_by_unit_id", equippedByUnit },
            { "cleared_encounter_ids_by_map", BuildSetMapSnapshot(_host.ClearedEncounterIdsByMap) },
            { "opened_door_ids_by_map", BuildSetMapSnapshot(_host.OpenedDoorIdsByMap) },
            { "unlocked_door_ids_by_map", BuildSetMapSnapshot(_host.UnlockedDoorIdsByMap) },
            { "opened_prop_ids_by_map", BuildSetMapSnapshot(_host.OpenedPropIdsByMap) },
            { "defeated_enemy_ids_by_map", BuildSetMapSnapshot(_host.DefeatedEnemyIdsByMap) },
            { "looted_bag_ids_by_map", BuildSetMapSnapshot(_host.LootedBagIdsByMap) },
            { "revealed_fog_cell_ids_by_map", BuildSetMapSnapshot(_host.RevealedFogCellIdsByMap) },
            { "loot_bags_by_map", BuildLootBagsByMapSnapshot(_host.LootBagsByMap) },
            { "triggered_event_ids", triggeredEventIds },
            { "completed_combat_intro_encounter_ids_by_map", BuildSetMapSnapshot(_host.CompletedCombatIntroEncounterIdsByMap) },
            { "event_spawned_unit_configs_by_map", BuildEventSpawnedUnitConfigsSnapshot(_host.EventSpawnedUnitConfigsByMap) },
            { "party_roster", partyRoster },
            { "reserve_roster", reserveRoster },
            { "recruited_npc_ids", recruitedNpcIds },
            { "pending_level_up_notices", pendingLevelUpNotices },
            { "unit_snapshots", unitSnapshots },
        };
    }

    private void ApplySaveData(Dictionary saveData)
    {
        _host.SelectedAbilityIdByUnitId.Clear();
        _host.QuickAbilityIdsByUnitId.Clear();
        _host.EquippedItemsByUnitId.Clear();
        _host.PartyInventoryItemIds.Clear();
        _host.IdentifiedItemIds.Clear();
        _host.VendorGoldById.Clear();
        _host.VendorInventoryItemIdsById.Clear();
        _host.ClearedEncounterIdsByMap.Clear();
        _host.OpenedDoorIdsByMap.Clear();
        _host.UnlockedDoorIdsByMap.Clear();
        _host.OpenedPropIdsByMap.Clear();
        _host.DefeatedEnemyIdsByMap.Clear();
        _host.LootedBagIdsByMap.Clear();
        _host.RevealedFogCellIdsByMap.Clear();
        _host.LootBagsByMap.Clear();
        _host.TriggeredEventIds.Clear();
        _host.CompletedCombatIntroEncounterIdsByMap.Clear();
        _host.EventSpawnedUnitConfigsByMap.Clear();

        _host.CurrentMapId = GetString(saveData, "current_map_id", "forest-town");
        _host.SelectedCharacterUnitId = GetString(saveData, "selected_character_unit_id", "");

        RestoreSelectedAbilityMap(GetDictionary(saveData, "selected_ability_id_by_unit_id"));
        RestoreQuickAbilityIdsByUnit(GetDictionary(saveData, "quick_ability_ids_by_unit_id"));
        RestoreSetMapSnapshot(GetDictionary(saveData, "cleared_encounter_ids_by_map"), _host.ClearedEncounterIdsByMap);
        RestoreSetMapSnapshot(GetDictionary(saveData, "completed_combat_intro_encounter_ids_by_map"), _host.CompletedCombatIntroEncounterIdsByMap);
        RestoreSetMapSnapshot(GetDictionary(saveData, "opened_door_ids_by_map"), _host.OpenedDoorIdsByMap);
        RestoreSetMapSnapshot(GetDictionary(saveData, "unlocked_door_ids_by_map"), _host.UnlockedDoorIdsByMap);
        RestoreSetMapSnapshot(GetDictionary(saveData, "opened_prop_ids_by_map"), _host.OpenedPropIdsByMap);
        RestoreSetMapSnapshot(GetDictionary(saveData, "defeated_enemy_ids_by_map"), _host.DefeatedEnemyIdsByMap);
        RestoreSetMapSnapshot(GetDictionary(saveData, "looted_bag_ids_by_map"), _host.LootedBagIdsByMap);
        RestoreSetMapSnapshot(GetDictionary(saveData, "revealed_fog_cell_ids_by_map"), _host.RevealedFogCellIdsByMap);
        RestoreLootBagsByMapSnapshot(GetDictionary(saveData, "loot_bags_by_map"), _host.LootBagsByMap);
        RestoreTriggeredEventIds(GetStringArray(saveData, "triggered_event_ids"));
        RestoreEventSpawnedUnitConfigs(GetDictionary(saveData, "event_spawned_unit_configs_by_map"));
        _host.RestoreRecruitedNpcIds(GetStringArray(saveData, "recruited_npc_ids"));

        _host.SpawnMapEncounter(_host.CurrentMapId);
        _host.RestorePartyRoster(GetDictionaryArray(saveData, "party_roster"));
        _host.RestoreReserveRoster(GetDictionaryArray(saveData, "reserve_roster"));

        // Spawn seeds default map/config equipment; clear it so save data is the single source of truth.
        _host.PartyInventoryItemIds.Clear();
        _host.EquippedItemsByUnitId.Clear();

        // Restore runtime inventory/equipment collections from save.
        _host.PartyGold = GetInt(saveData, "party_gold", 25);
        RestorePartyInventory(GetStringArray(saveData, "party_inventory_item_ids"));
        foreach (var itemId in GetStringArray(saveData, "identified_item_ids"))
        {
            _host.IdentifiedItemIds.Add(itemId);
        }
        RestoreVendorGold(GetDictionary(saveData, "vendor_gold_by_id"));
        RestoreVendorInventory(GetDictionary(saveData, "vendor_inventory_item_ids_by_id"));
        RestoreEquippedItemsByUnit(GetDictionary(saveData, "equipped_items_by_unit_id"));

        _host.ApplyUnitSnapshots(GetDictionaryArray(saveData, "unit_snapshots"));
        _host.RestorePendingLevelUpNotices(GetDictionaryArray(saveData, "pending_level_up_notices"));

        var explorerId = GetString(saveData, "explorer_unit_id", "");
        _host.SetExplorerUnitById(explorerId);

        var flowState = GetString(saveData, "flow_state", "exploration");
        _host.ActiveEncounterId = GetString(saveData, "active_encounter_id", "");
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

    private static Dictionary BuildEventSpawnedUnitConfigsSnapshot(System.Collections.Generic.Dictionary<string, Array<Dictionary>> source)
    {
        var snapshot = new Dictionary();
        foreach (var entry in source)
        {
            var configs = new Array<Dictionary>();
            foreach (var config in entry.Value)
            {
                configs.Add(CopyDictionary(config));
            }

            snapshot[entry.Key] = configs;
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

    private void RestoreQuickAbilityIdsByUnit(Dictionary snapshot)
    {
        foreach (var key in snapshot.Keys)
        {
            var unitId = ((Variant)key).AsString();
            if (string.IsNullOrEmpty(unitId))
            {
                continue;
            }

            var value = (Variant)snapshot[key];
            if (value.VariantType != Variant.Type.Array)
            {
                continue;
            }

            var abilityIds = new List<string>();
            foreach (var entry in (Array)value)
            {
                var abilityId = ((Variant)entry).AsString();
                if (!string.IsNullOrEmpty(abilityId)
                    && !abilityIds.Contains(abilityId)
                    && abilityIds.Count < 5)
                {
                    abilityIds.Add(abilityId);
                }
            }

            _host.QuickAbilityIdsByUnitId[unitId] = abilityIds;
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

    private void RestoreVendorGold(Dictionary snapshot)
    {
        foreach (var key in snapshot.Keys)
        {
            var vendorId = ((Variant)key).AsString();
            if (string.IsNullOrEmpty(vendorId))
            {
                continue;
            }

            _host.VendorGoldById[vendorId] = (int)((Variant)snapshot[key]);
        }
    }

    private void RestoreVendorInventory(Dictionary snapshot)
    {
        foreach (var key in snapshot.Keys)
        {
            var vendorId = ((Variant)key).AsString();
            if (string.IsNullOrEmpty(vendorId))
            {
                continue;
            }

            var itemIds = new List<string>();
            var snapshotItems = (Array)((Variant)snapshot[key]);
            foreach (var entry in snapshotItems)
            {
                var itemId = ((Variant)entry).AsString();
                if (!string.IsNullOrEmpty(itemId))
                {
                    itemIds.Add(itemId);
                }
            }

            _host.VendorInventoryItemIdsById[vendorId] = itemIds;
        }
    }

    private void RestoreTriggeredEventIds(Array<string> snapshot)
    {
        foreach (var eventId in snapshot)
        {
            if (!string.IsNullOrWhiteSpace(eventId))
            {
                _host.TriggeredEventIds.Add(eventId);
            }
        }
    }

    private void RestoreEventSpawnedUnitConfigs(Dictionary snapshot)
    {
        foreach (var key in snapshot.Keys)
        {
            var mapId = ((Variant)key).AsString();
            if (string.IsNullOrWhiteSpace(mapId))
            {
                continue;
            }

            var configs = GetDictionaryArray(snapshot, mapId);
            var restoredConfigs = new Array<Dictionary>();
            foreach (var config in configs)
            {
                restoredConfigs.Add(CopyDictionary(config));
            }

            _host.EventSpawnedUnitConfigsByMap[mapId] = restoredConfigs;
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

    private static int GetInt(Dictionary dict, string key, int fallback)
    {
        return dict.ContainsKey(key) ? (int)((Variant)dict[key]) : fallback;
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
