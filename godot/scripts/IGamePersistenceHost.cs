using Godot;
using Godot.Collections;
using System.Collections.Generic;

public interface IGamePersistenceHost
{
    string CurrentMapId { get; set; }
    string ActiveEncounterId { get; set; }
    string SelectedCharacterUnitId { get; set; }

    System.Collections.Generic.Dictionary<string, string> SelectedAbilityIdByUnitId { get; }
    System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>> EquippedItemsByUnitId { get; }
    List<string> PartyInventoryItemIds { get; }
    System.Collections.Generic.Dictionary<string, HashSet<string>> ClearedEncounterIdsByMap { get; }
    System.Collections.Generic.Dictionary<string, HashSet<string>> OpenedPropIdsByMap { get; }
    System.Collections.Generic.Dictionary<string, HashSet<string>> LootedBagIdsByMap { get; }
    System.Collections.Generic.Dictionary<string, Array<Dictionary>> LootBagsByMap { get; }

    string GetFlowStateToken();
    string GetExplorerUnitId();
    void SetExplorerUnitById(string unitId);
    void SaveClearedEncounterStateForCurrentMap();
    void SpawnMapEncounter(string mapId);
    Array<Dictionary> BuildUnitSnapshots();
    void ApplyUnitSnapshots(Array<Dictionary> snapshots);
    void RestoreFlowState(string flowStateToken);
    void SyncHudFromGameState();
    void RequestRedraw();
    void SetStatusText(string text);
}
