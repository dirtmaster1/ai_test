using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

public sealed class QuestManager
{
    private readonly GameData _gameData;
    private readonly HashSet<string> _validQuestIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _acceptedQuestIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _completedQuestIds = new(StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string, HashSet<string>> _completedObjectiveIdsByQuestId = new(StringComparer.Ordinal);

    public QuestManager(GameData gameData)
    {
        _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
        ValidateQuestDefinitions();
    }

    public bool IsAccepted(string questId)
    {
        return _acceptedQuestIds.Contains(questId);
    }

    public bool IsCompleted(string questId)
    {
        return _completedQuestIds.Contains(questId);
    }

    public bool IsReadyToTurnIn(string questId)
    {
        if (!_acceptedQuestIds.Contains(questId) || _completedQuestIds.Contains(questId)
            || !TryGetValidQuest(questId, out var quest))
        {
            return false;
        }

        return AreAllObjectivesComplete(questId, quest);
    }

    public bool TryAcceptQuest(
        string questId,
        System.Collections.Generic.Dictionary<string, HashSet<string>> defeatedEnemyIdsByMap)
    {
        if (!TryGetValidQuest(questId, out var quest) || _acceptedQuestIds.Contains(questId))
        {
            return false;
        }

        var completedObjectiveIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var objective in GetDictionaryArray(quest, "objectives"))
        {
            if (IsObjectiveAlreadyComplete(objective, defeatedEnemyIdsByMap))
            {
                completedObjectiveIds.Add(GetString(objective, "id"));
            }
        }

        _acceptedQuestIds.Add(questId);
        _completedObjectiveIdsByQuestId[questId] = completedObjectiveIds;
        return true;
    }

    public bool NotifyEnemyDefeated(string mapId, string enemyId)
    {
        if (string.IsNullOrWhiteSpace(mapId) || string.IsNullOrWhiteSpace(enemyId))
        {
            return false;
        }

        var progressChanged = false;
        foreach (var questId in _acceptedQuestIds)
        {
            if (_completedQuestIds.Contains(questId)
                || !_completedObjectiveIdsByQuestId.TryGetValue(questId, out var completedObjectiveIds)
                || !TryGetValidQuest(questId, out var quest))
            {
                continue;
            }

            foreach (var objective in GetDictionaryArray(quest, "objectives"))
            {
                if (MatchesEnemyObjective(objective, mapId, enemyId)
                    && completedObjectiveIds.Add(GetString(objective, "id")))
                {
                    progressChanged = true;
                }
            }
        }

        return progressChanged;
    }

    public bool TryMarkCompleted(string questId)
    {
        if (!IsReadyToTurnIn(questId))
        {
            return false;
        }

        _completedQuestIds.Add(questId);
        return true;
    }

    public bool TryGetRewardData(string questId, out int gold, out List<string> itemIds)
    {
        gold = 0;
        itemIds = new List<string>();
        var quest = _gameData.GetQuest(questId);
        if (quest.Count == 0 || !quest.ContainsKey("rewards"))
        {
            return false;
        }

        var rewardsValue = (Variant)quest["rewards"];
        if (rewardsValue.VariantType != Variant.Type.Dictionary)
        {
            GD.PushError($"Quest '{questId}' has invalid rewards data.");
            return false;
        }

        var rewards = (Godot.Collections.Dictionary)rewardsValue;
        if (!TryGetInt(rewards, "gold", out gold) || gold < 0
            || !rewards.ContainsKey("item_ids"))
        {
            GD.PushError($"Quest '{questId}' must define a non-negative gold reward and item_ids array.");
            return false;
        }

        var itemIdsValue = (Variant)rewards["item_ids"];
        if (itemIdsValue.VariantType != Variant.Type.Array)
        {
            GD.PushError($"Quest '{questId}' item_ids reward must be an array.");
            return false;
        }

        foreach (var itemIdValue in (Godot.Collections.Array)itemIdsValue)
        {
            var itemIdVariant = (Variant)itemIdValue;
            if (itemIdVariant.VariantType != Variant.Type.String)
            {
                GD.PushError($"Quest '{questId}' contains a non-string reward item ID.");
                return false;
            }

            var itemId = itemIdVariant.AsString();
            if (string.IsNullOrWhiteSpace(itemId) || _gameData.GetItem(itemId).Count == 0)
            {
                GD.PushError($"Quest '{questId}' references missing reward item '{itemId}'.");
                return false;
            }

            itemIds.Add(itemId);
        }

        return true;
    }

    public string GetRewardSummary(string questId)
    {
        if (!TryGetRewardData(questId, out var gold, out var itemIds))
        {
            return "Reward unavailable";
        }

        var parts = new List<string>();
        if (gold > 0)
        {
            parts.Add($"{gold:N0} gp");
        }

        foreach (var itemId in itemIds)
        {
            var item = _gameData.GetItem(itemId);
            parts.Add(GetString(item, "name", itemId));
        }

        return parts.Count == 0 ? "No reward" : string.Join(" + ", parts);
    }

    public Array<Dictionary> BuildDialogueChoices(string giverNpcId)
    {
        var choices = new Array<Dictionary>();
        if (string.IsNullOrWhiteSpace(giverNpcId))
        {
            return choices;
        }

        foreach (var questId in GetSortedQuestIds())
        {
            if (!TryGetValidQuest(questId, out var quest)
                || GetString(quest, "giver_npc_id") != giverNpcId
                || _completedQuestIds.Contains(questId))
            {
                continue;
            }

            if (!_acceptedQuestIds.Contains(questId))
            {
                choices.Add(new Dictionary
                {
                    { "label", $"Accept quest: {GetString(quest, "title", questId)}" },
                    { "action", "accept_quest" },
                    { "quest_id", questId },
                    { "end", true }
                });
            }
            else if (IsReadyToTurnIn(questId))
            {
                choices.Add(new Dictionary
                {
                    { "label", $"Claim reward: {GetRewardSummary(questId)}" },
                    { "action", "claim_quest" },
                    { "quest_id", questId },
                    { "end", true }
                });
            }
        }

        return choices;
    }

    public Array<Dictionary> BuildJournalEntries(bool completed)
    {
        var entries = new Array<Dictionary>();
        foreach (var questId in GetSortedQuestIds())
        {
            if (!_acceptedQuestIds.Contains(questId)
                || _completedQuestIds.Contains(questId) != completed
                || !TryGetValidQuest(questId, out var quest))
            {
                continue;
            }

            var objectiveEntries = new Array<Dictionary>();
            var completedObjectiveIds = _completedObjectiveIdsByQuestId[questId];
            foreach (var objective in GetDictionaryArray(quest, "objectives"))
            {
                var objectiveId = GetString(objective, "id");
                objectiveEntries.Add(new Dictionary
                {
                    { "description", GetString(objective, "description", objectiveId) },
                    { "completed", completedObjectiveIds.Contains(objectiveId) }
                });
            }

            var status = completed
                ? "Completed"
                : IsReadyToTurnIn(questId) ? "Ready to turn in" : "In progress";

            if (!TryGetRewardData(questId, out var rewardGold, out var rewardItemIds))
            {
                GD.PushError($"Quest '{questId}' reward data became unavailable while building the journal.");
                continue;
            }

            var rewardItemIdEntries = new Array<string>();
            var rewardItemNameEntries = new Array<string>();
            foreach (var itemId in rewardItemIds)
            {
                rewardItemIdEntries.Add(itemId);
                var item = _gameData.GetItem(itemId);
                rewardItemNameEntries.Add(GetString(item, "name", itemId));
            }

            entries.Add(new Dictionary
            {
                { "id", questId },
                { "title", GetString(quest, "title", questId) },
                { "description", GetString(quest, "description") },
                { "status", status },
                { "objectives", objectiveEntries },
                { "reward_gold", rewardGold },
                { "reward_item_ids", rewardItemIdEntries },
                { "reward_item_names", rewardItemNameEntries },
                { "reward_text", GetRewardSummary(questId) }
            });
        }

        return entries;
    }

    public Dictionary BuildSnapshot()
    {
        var snapshot = new Dictionary();
        foreach (var questId in GetSortedQuestIds())
        {
            if (!_acceptedQuestIds.Contains(questId)
                || !_completedObjectiveIdsByQuestId.TryGetValue(questId, out var completedObjectiveIds))
            {
                continue;
            }

            var completedObjectives = new Array<string>();
            foreach (var objective in GetDictionaryArray(_gameData.GetQuest(questId), "objectives"))
            {
                var objectiveId = GetString(objective, "id");
                if (completedObjectiveIds.Contains(objectiveId))
                {
                    completedObjectives.Add(objectiveId);
                }
            }

            snapshot[questId] = new Dictionary
            {
                { "status", _completedQuestIds.Contains(questId) ? "completed" : "active" },
                { "completed_objective_ids", completedObjectives }
            };
        }

        return snapshot;
    }

    public void RestoreSnapshot(Dictionary snapshot)
    {
        _acceptedQuestIds.Clear();
        _completedQuestIds.Clear();
        _completedObjectiveIdsByQuestId.Clear();

        foreach (var key in snapshot.Keys)
        {
            var questId = ((Variant)key).AsString();
            var stateValue = (Variant)snapshot[key];
            if (stateValue.VariantType != Variant.Type.Dictionary
                || !TryGetValidQuest(questId, out var quest))
            {
                GD.PushWarning($"Ignoring invalid saved quest state '{questId}'.");
                continue;
            }

            var state = (Godot.Collections.Dictionary)stateValue;
            var status = GetString(state, "status");
            if (status is not ("active" or "completed"))
            {
                GD.PushWarning($"Ignoring quest '{questId}' with invalid saved status '{status}'.");
                continue;
            }

            var knownObjectiveIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var objective in GetDictionaryArray(quest, "objectives"))
            {
                knownObjectiveIds.Add(GetString(objective, "id"));
            }

            var completedObjectiveIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var objectiveId in GetStringArray(state, "completed_objective_ids"))
            {
                if (knownObjectiveIds.Contains(objectiveId))
                {
                    completedObjectiveIds.Add(objectiveId);
                }
                else
                {
                    GD.PushWarning($"Ignoring unknown objective '{objectiveId}' in saved quest '{questId}'.");
                }
            }

            _acceptedQuestIds.Add(questId);
            _completedObjectiveIdsByQuestId[questId] = completedObjectiveIds;
            if (status == "completed")
            {
                if (AreAllObjectivesComplete(questId, quest))
                {
                    _completedQuestIds.Add(questId);
                }
                else
                {
                    GD.PushError($"Saved quest '{questId}' is completed but has unfinished objectives; keeping it active.");
                }
            }
        }
    }

    private bool IsObjectiveAlreadyComplete(
        Dictionary objective,
        System.Collections.Generic.Dictionary<string, HashSet<string>> defeatedEnemyIdsByMap)
    {
        var mapId = GetString(objective, "map_id");
        var targetEnemyId = GetString(objective, "target_enemy_id");
        return !string.IsNullOrEmpty(mapId)
            && !string.IsNullOrEmpty(targetEnemyId)
            && defeatedEnemyIdsByMap != null
            && defeatedEnemyIdsByMap.TryGetValue(mapId, out var defeatedEnemyIds)
            && defeatedEnemyIds.Contains(targetEnemyId);
    }

    private static bool MatchesEnemyObjective(Dictionary objective, string mapId, string enemyId)
    {
        return GetString(objective, "type") == "defeat_enemy"
            && GetString(objective, "map_id") == mapId
            && GetString(objective, "target_enemy_id") == enemyId;
    }

    private bool AreAllObjectivesComplete(string questId, Dictionary quest)
    {
        if (!_completedObjectiveIdsByQuestId.TryGetValue(questId, out var completedObjectiveIds))
        {
            return false;
        }

        var objectives = GetDictionaryArray(quest, "objectives");
        if (objectives.Count == 0)
        {
            return false;
        }

        foreach (var objective in objectives)
        {
            if (!completedObjectiveIds.Contains(GetString(objective, "id")))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryGetValidQuest(string questId, out Dictionary quest)
    {
        quest = _gameData.GetQuest(questId);
        return _validQuestIds.Contains(questId) && quest.Count > 0;
    }

    private List<string> GetSortedQuestIds()
    {
        var questIds = new List<string>(_validQuestIds);
        questIds.Sort(StringComparer.Ordinal);
        return questIds;
    }

    private void ValidateQuestDefinitions()
    {
        foreach (var key in _gameData.Quests.Keys)
        {
            var questId = ((Variant)key).AsString();
            var quest = _gameData.GetQuest(questId);
            var valid = GetString(quest, "id") == questId
                && !string.IsNullOrWhiteSpace(GetString(quest, "title"))
                && !string.IsNullOrWhiteSpace(GetString(quest, "giver_npc_id"));
            var objectives = GetDictionaryArray(quest, "objectives");
            var objectiveIds = new HashSet<string>(StringComparer.Ordinal);
            if (objectives.Count == 0)
            {
                valid = false;
            }

            foreach (var objective in objectives)
            {
                var objectiveId = GetString(objective, "id");
                if (string.IsNullOrWhiteSpace(objectiveId)
                    || !objectiveIds.Add(objectiveId)
                    || GetString(objective, "type") != "defeat_enemy"
                    || string.IsNullOrWhiteSpace(GetString(objective, "map_id"))
                    || string.IsNullOrWhiteSpace(GetString(objective, "target_enemy_id"))
                    || string.IsNullOrWhiteSpace(GetString(objective, "description")))
                {
                    valid = false;
                }
            }

            if (!TryGetRewardData(questId, out _, out _))
            {
                valid = false;
            }

            if (valid)
            {
                _validQuestIds.Add(questId);
            }
            else
            {
                GD.PushError($"Quest definition '{questId}' is invalid or uses an unsupported objective.");
            }
        }
    }

    private static Array<Dictionary> GetDictionaryArray(Dictionary source, string key)
    {
        var result = new Array<Dictionary>();
        if (!source.ContainsKey(key))
        {
            return result;
        }

        var value = (Variant)source[key];
        if (value.VariantType != Variant.Type.Array)
        {
            return result;
        }

        foreach (var entry in (Godot.Collections.Array)value)
        {
            var variant = (Variant)entry;
            if (variant.VariantType == Variant.Type.Dictionary)
            {
                result.Add((Dictionary)variant);
            }
        }

        return result;
    }

    private static Array<string> GetStringArray(Dictionary source, string key)
    {
        var result = new Array<string>();
        if (!source.ContainsKey(key))
        {
            return result;
        }

        var value = (Variant)source[key];
        if (value.VariantType != Variant.Type.Array)
        {
            return result;
        }

        foreach (var entry in (Godot.Collections.Array)value)
        {
            var variant = (Variant)entry;
            if (variant.VariantType == Variant.Type.String)
            {
                result.Add(variant.AsString());
            }
        }

        return result;
    }

    private static string GetString(Dictionary source, string key, string fallback = "")
    {
        if (!source.ContainsKey(key))
        {
            return fallback;
        }

        var value = (Variant)source[key];
        return value.VariantType == Variant.Type.String ? value.AsString() : fallback;
    }

    private static bool TryGetInt(Dictionary source, string key, out int result)
    {
        result = 0;
        if (!source.ContainsKey(key))
        {
            return false;
        }

        var value = (Variant)source[key];
        if (value.VariantType == Variant.Type.Int)
        {
            result = (int)value;
            return true;
        }

        if (value.VariantType != Variant.Type.Float)
        {
            return false;
        }

        var numericValue = (double)value;
        if (double.IsNaN(numericValue) || double.IsInfinity(numericValue)
            || numericValue < int.MinValue || numericValue > int.MaxValue
            || Math.Truncate(numericValue) != numericValue)
        {
            return false;
        }

        result = (int)numericValue;
        return true;
    }
}
