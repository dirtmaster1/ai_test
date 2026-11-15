using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Reflection;

public partial class QuestChecks : Node
{
    private const string QuestId = "defeat-necromancer";
    private const string MapId = "necromancers-crypt";
    private const string EnemyId = "necromancers-crypt-encounter-main-necromancer-9--3";
    private readonly Array<string> _failures = new();

    public Array<string> Run()
    {
        var gameData = GetNode<GameData>("/root/GameData");
        var manager = new QuestManager(gameData);
        var defeatedEnemiesByMap = new System.Collections.Generic.Dictionary<string, HashSet<string>>();
        var markerTileSet = GD.Load<TileSet>("res://assets/tilesets/map_markers_64_tileset.tres");
        var markerSource = (TileSetAtlasSource)markerTileSet.GetSource(0);
        var elderMarker = markerSource.GetTileData(new Vector2I(1, 1), 0);
        var elderNpcId = elderMarker.GetCustomData("npc_id").AsString();
        var elderDialogueId = elderMarker.GetCustomData("dialogue_id").AsString();
        var quest = gameData.GetQuest(QuestId);
        var questGiverId = ((Variant)quest["giver_npc_id"]).AsString();
        Check(elderNpcId == "forest-elder" && elderDialogueId == "forest-elder"
            && questGiverId == elderNpcId,
            "Elder Rowan's NPC ID, dialogue ID, and quest giver ID must match.");

        Check(manager.TryAcceptQuest(QuestId, defeatedEnemiesByMap), "The party must be able to accept the quest.");
        Check(manager.IsAccepted(QuestId) && !manager.IsReadyToTurnIn(QuestId),
            "An accepted quest must remain active until its objective is complete.");
        Check(!manager.NotifyEnemyDefeated("wrong-map", EnemyId),
            "Defeating an enemy with the right ID on the wrong map must not progress the quest.");
        Check(!manager.NotifyEnemyDefeated(MapId, "another-enemy"),
            "Defeating a different enemy on the correct map must not progress the quest.");
        Check(manager.NotifyEnemyDefeated(MapId, EnemyId),
            "Defeating the targeted necromancer must update quest progress.");
        Check(manager.IsReadyToTurnIn(QuestId),
            "The quest must become ready to turn in after defeating its target.");
        Check(!manager.NotifyEnemyDefeated(MapId, EnemyId),
            "Repeated defeat notifications must not change progress twice.");

        var currentEntries = manager.BuildJournalEntries(completed: false);
        Check(currentEntries.Count == 1
            && (string)((Dictionary)currentEntries[0])["status"] == "Ready to turn in",
            "The quest journal must list the ready quest under current quests.");
        if (currentEntries.Count == 1)
        {
            var journalEntry = currentEntries[0];
            var journalObjectives = (Array<Dictionary>)journalEntry["objectives"];
            Check((string)journalEntry["description"]
                == "Defeat the necromancer who is raising the dead in the graveyard."
                && journalObjectives.Count == 1
                && (string)journalObjectives[0]["description"] == "Defeat the necromancer in the crypt.",
                "The journal entry must include the quest description and objective description.");
            var rewardItemIds = (Array<string>)journalEntry["reward_item_ids"];
            var rewardItemNames = (Array<string>)journalEntry["reward_item_names"];
            Check((int)(Variant)journalEntry["reward_gold"] == 1000
                && rewardItemIds.Count == 1
                && rewardItemIds[0] == "full-plate-armor"
                && rewardItemNames.Count == 1
                && rewardItemNames[0] == "Full Plate Armor",
                "The journal must expose reward values and names for its reward icons.");
            CheckQuestJournalCards(currentEntries);
        }
        Check(manager.TryGetRewardData(QuestId, out var gold, out var itemIds)
            && gold == 1000
            && itemIds.Count == 1
            && itemIds[0] == "full-plate-armor",
            "The quest must award 1,000 gold and Full Plate Armor.");

        var restored = new QuestManager(gameData);
        restored.RestoreSnapshot(manager.BuildSnapshot());
        Check(restored.IsReadyToTurnIn(QuestId),
            "Objective progress must survive snapshot restoration.");
        Check(restored.BuildDialogueChoices(elderNpcId).Count == 1,
            "Elder Rowan must offer the reward once the quest is ready.");
        Check(restored.TryMarkCompleted(QuestId) && restored.IsCompleted(QuestId),
            "A ready quest must be markable as completed.");
        Check(!restored.TryMarkCompleted(QuestId),
            "A completed quest must not be claimable a second time.");
        Check(restored.BuildJournalEntries(completed: true).Count == 1
            && restored.BuildDialogueChoices(elderNpcId).Count == 0,
            "Completed quests must move to the completed journal and leave dialogue choices.");

        var completedRestore = new QuestManager(gameData);
        completedRestore.RestoreSnapshot(restored.BuildSnapshot());
        Check(completedRestore.IsCompleted(QuestId),
            "Completed quest state must survive snapshot restoration.");

        var previouslyDefeated = new System.Collections.Generic.Dictionary<string, HashSet<string>>
        {
            { MapId, new HashSet<string> { EnemyId } }
        };
        var retroactive = new QuestManager(gameData);
        Check(retroactive.TryAcceptQuest(QuestId, previouslyDefeated)
            && retroactive.IsReadyToTurnIn(QuestId),
            "An objective completed before quest acceptance must be recognized.");

        return _failures;
    }

    private void CheckQuestJournalCards(Array<Dictionary> currentEntries)
    {
        var hudScene = GD.Load<PackedScene>("res://ui/HUD.tscn");
        var hud = hudScene.Instantiate<HudController>();
        var currentCards = hud.GetNode<HBoxContainer>(
            "QuestPanel/QuestVBox/QuestTabs/Current/CurrentQuestCards");
        var completedCards = hud.GetNode<HBoxContainer>(
            "QuestPanel/QuestVBox/QuestTabs/Completed/CompletedQuestCards");
        typeof(HudController).GetField("_currentQuestCards", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(hud, currentCards);
        typeof(HudController).GetField("_completedQuestCards", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(hud, completedCards);
        hud.SetQuestJournalEntries(currentEntries, new Array<Dictionary>());

        Check(currentCards.GetChildCount() == 1, "Each quest must render as one quest card.");
        if (currentCards.GetChildCount() == 1 && currentCards.GetChild(0) is PanelContainer card)
        {
            Check(card.CustomMinimumSize.Y == 0
                && card.SizeFlagsVertical == Control.SizeFlags.ShrinkBegin,
                "Quest cards must use their content height instead of a fixed oversized height.");
            Check(card.SizeFlagsHorizontal == Control.SizeFlags.ExpandFill,
                "A quest card must fill the available journal width while retaining its minimum width.");
            Check(card.FindChild("QuestStatus", true, false) is Label questStatus
                && questStatus.Text == "Ready to turn in",
                "The card must retain the actionable ready-to-turn-in status.");
            Check(card.GetChild(0) is HBoxContainer layout
                && layout.GetChildCount() == 2
                && layout.GetChild(1).Name == "QuestRewards",
                "Quest cards must separate their details from the right-aligned rewards.");
            Check(card.FindChild("QuestRewardIcon_gold", true, false) is TextureRect goldIcon
                && goldIcon.Texture != null
                && card.FindChild("QuestRewardIcon_full-plate-armor", true, false) is TextureRect armorIcon
                && armorIcon.Texture != null
                && armorIcon.MouseFilter == Control.MouseFilterEnum.Stop
                && armorIcon.TooltipText == "Full Plate Armor",
                "Quest cards must use the game reward sprites and make the armor tooltip hoverable.");
            Check(card.FindChild("QuestRewardRow_gold", true, false) is HBoxContainer goldRow
                && card.FindChild("QuestRewardRow_full-plate-armor", true, false) is HBoxContainer itemRow
                && goldRow.Alignment == BoxContainer.AlignmentMode.Begin
                && itemRow.Alignment == BoxContainer.AlignmentMode.Begin,
                "Reward icons and labels must align consistently across reward rows.");
            Check(card.FindChild("QuestDescription", true, false) is Label description
                && description.Text == "Defeat the necromancer who is raising the dead in the graveyard."
                && description.CustomMinimumSize.Y >= 30,
                "Quest cards must display the quest description.");
            var objectiveDescriptionVisible = false;
            foreach (var node in card.FindChildren("*", "Label", true, false))
            {
                if (node is Label objective
                    && objective.Text.Contains("Defeat the necromancer in the crypt.")
                    && objective.CustomMinimumSize.Y >= 18)
                {
                    objectiveDescriptionVisible = true;
                    break;
                }
            }

            Check(objectiveDescriptionVisible,
                "Quest cards must display objective descriptions.");
        }

        var inProgressEntry = (Dictionary)currentEntries[0].Duplicate(true);
        inProgressEntry["status"] = "In progress";
        hud.SetQuestJournalEntries(new Array<Dictionary> { inProgressEntry }, new Array<Dictionary>());
        var inProgressCard = currentCards.GetChild(0) as PanelContainer;
        Check(inProgressCard != null && inProgressCard.FindChild("QuestStatus", true, false) == null,
            "The current tab must not repeat the redundant in-progress status on quest cards.");

        var scroll = hud.GetNode<ScrollContainer>("QuestPanel/QuestVBox/QuestTabs/Current");
        var configureQuestScroll = typeof(HudController).GetMethod(
            "ConfigureQuestScroll",
            BindingFlags.Static | BindingFlags.NonPublic);
        configureQuestScroll.Invoke(null, new object[] { scroll, currentCards });
        Check(scroll.HorizontalScrollMode == ScrollContainer.ScrollMode.Auto
            && scroll.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled
            && currentCards.SizeFlagsHorizontal == Control.SizeFlags.ExpandFill,
            "Quest cards must scroll horizontally when they exceed the available width.");
        hud.Free();
    }

    private void Check(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }
}
