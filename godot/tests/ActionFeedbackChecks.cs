using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public partial class ActionFeedbackChecks : BattleController
{
    private readonly Array<string> _failures = new();
    private Unit _actor;
    private Unit _ally;
    private Unit _enemy;
    private HudController _testHud;
    private MapLoader _testLoader;
    private Label _banner;
    private ItemList _log;
    private Dictionary _door;
    private readonly Vector2I _doorCell = new(3, 3);

    public override void _Ready() { }
    public override void _ExitTree() { }

    public Array<string> RunIconChecks()
    {
        SetupFixture();
        CheckPoisonStrikeIcon();
        CheckExtendedIconAtlas();
        CheckConsumableActionBarIcons();
        CheckCompactActionBar();
        CheckAbilityTray();
        return _failures;
    }

    public Array<string> RunHealerServiceChecks()
    {
        SetupFixture();
        CheckExtendedIconAtlas();
        CheckHealerServices();
        CheckCombatHealingSpells();
        CheckDeadPartyDefeat();
        Cleanup();
        return _failures;
    }

    public Array<string> RunEquipmentBonusChecks()
    {
        SetupFixture();
        CheckEquipmentStatBonuses();
        Cleanup();
        return _failures;
    }

    public Array<string> RunKeyboardRepeatChecks()
    {
        SetupFixture();
        CheckExplorationKeyEchoQueued();
        Cleanup();
        return _failures;
    }

    public Array<string> RunDefendIndicatorChecks()
    {
        SetupFixture();
        CheckCombatEndClearsCombatStatuses();
        Cleanup();
        return _failures;
    }

    public Array<string> Run()
    {
        SetupFixture();
        CheckPoisonStrikeIcon();
        CheckExtendedIconAtlas();
        CheckConsumableActionBarIcons();
        CheckCompactActionBar();
        CheckAbilityTray();
        CheckResources();
        CheckEquipmentStatBonuses();
        CheckActionPointsAndLevelUp();
        CheckTargets();
        CheckUtilities();
        CheckMovement();
        CheckInteractions();
        CheckConsumables();
        CheckHealerServices();
        CheckAllowedActions();
        CheckNewHealerSpell();
        CheckAreaSpellEffects();
        CheckRainOfArrows();
        CheckShadowStrike();
        CheckTauntTargeting();
        CheckIdentifyInventoryFlow();
        CheckCombatEndClearsCombatStatuses();
        CheckCombatTargetClickReleaseDoesNotMoveInExploration();
        return _failures;
    }

    private void CheckCombatTargetClickReleaseDoesNotMoveInExploration()
    {
        Reset(exploration: true);
        var startCell = _actor.GridPos;
        Set("_ignoreNextCombatTargetClickRelease", true);

        Call("HandleMouseInput", new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = false
        });

        Check(_actor.GridPos == startCell
            && !Field<bool>("_isExplorationAutoMoving")
            && !Field<bool>("_ignoreNextCombatTargetClickRelease"),
            "The mouse release for the combat target click must not start exploration movement after victory.");
    }

    private void CheckCombatEndClearsCombatStatuses()
    {
        Reset();
        _actor.ApplyStatusEffect("combat-buff", "Protected", true, 3, scope: "combat_only", armorClassBonus: 2);
        _ally.ApplyStatusEffect("combat-debuff", "Poisoned", false, 3, scope: "combat_only", damagePerTurn: 1);
        _actor.ApplyStatusEffect("persistent-buff", "Blessed", true, 3, scope: "persistent");
        _enemy.ApplyStatusEffect("enemy-combat-buff", "Protected", true, 3, scope: "combat_only");
        _actor.MarkDefending();
        _ally.MarkDefending();

        Call("EnterExplorationMode", "Encounter cleared.");

        Check(!_actor.HasStatusEffect("combat-buff") && !_ally.HasStatusEffect("combat-debuff"),
            "Ending combat must clear combat-only buffs and debuffs from party members.");
        Check(!_actor.IsDefending && !_ally.IsDefending,
            "Ending combat must clear the Defend ring from every player unit.");
        Check(_actor.HasStatusEffect("persistent-buff"),
            "Ending combat must not clear persistent party status effects.");
        Check(_enemy.HasStatusEffect("enemy-combat-buff"),
            "Ending combat cleanup must not clear enemy status effects.");

        _actor.MarkDefending();
        _actor.ResetTurnResources();
        Check(!_actor.IsDefending,
            "The Defend ring must disappear when the effect wears off at the unit's next turn.");
    }

    private void CheckPoisonStrikeIcon()
    {
        var icon = (Texture2D)typeof(HudController)
            .GetMethod("GetGameIcon", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { "poison-strike" });
        Check(icon is AtlasTexture atlas && atlas.Region == new Rect2(128, 192, 32, 32),
            "Poison Strike must use the fifth column of the seventh atlas row");
        _testHud.SetAbilityButtons(new Array<Dictionary>
        {
            new()
            {
                { "id", "poison-strike" },
                { "label", "Poison Strike" },
                { "detail", "Damages and poisons one target." },
                { "is_enabled", 1 }
            }
        }, true);
        Check(AbilityButton("poison-strike").Icon == icon,
            "Poison Strike action-bar button must use the shared icon mapping");
        Check(AbilityButton("poison-strike").TooltipText == "Poison Strike",
            "Ability icon tooltip must show only the ability name");
        Call("SyncHudFromGameState");
    }

    private void CheckExtendedIconAtlas()
    {
        var getIcon = typeof(HudController).GetMethod("GetGameIcon", BindingFlags.Static | BindingFlags.NonPublic);
        var expectedCells = new System.Collections.Generic.Dictionary<string, Vector2I>
        {
            ["healers-circlet"] = new(0, 3),
            ["mages-amulet"] = new(1, 3),
            ["boots-of-speed"] = new(6, 3),
            ["healing-potion"] = new(0, 7),
            ["magic-potion"] = new(1, 7),
            ["gold"] = new(0, 8),
            ["rusty-key"] = new(1, 8),
            ["pick-lock"] = new(2, 8),
            ["disarm-trap"] = new(3, 8),
            ["small-shield"] = new(0, 2),
            ["poison-strike"] = new(4, 6),
            ["greater-heal"] = new(1, 4),
            ["web"] = new(6, 4),
            ["lightning-bolt"] = new(0, 5),
            ["identify"] = new(1, 5),
            ["rain-of-arrows"] = new(2, 6),
            ["shadow-strike"] = new(3, 6),
            ["taunt"] = new(6, 6),
            ["remove-malady"] = new(4, 8),
            ["resurrect"] = new(5, 8),
            ["blessing"] = new(6, 8)
        };
        foreach (var entry in expectedCells)
        {
            var icon = getIcon.Invoke(null, new object[] { entry.Key }) as AtlasTexture;
            var expectedRegion = new Rect2(entry.Value.X * 32, entry.Value.Y * 32, 32, 32);
            Check(icon != null && icon.Region == expectedRegion,
                $"{entry.Key} must use its assigned 32px atlas cell");
            if (icon == null) continue;
            Check(icon.Atlas.GetWidth() == 224 && icon.Atlas.GetHeight() == 288,
                "Extended icon atlas must retain seven columns and add two rows");
            using var image = icon.Atlas.GetImage();
            using var cell = image.GetRegion(new Rect2I(entry.Value * 32, new Vector2I(32, 32)));
            Check(!cell.IsInvisible(), $"{entry.Key} atlas cell must contain visible artwork");
        }

    }

    private void CheckEquipmentStatBonuses()
    {
        Reset();
        var equippedByUnit = Field<System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>>("_equippedItemsByUnitId");
        equippedByUnit[_actor.UnitId] = new System.Collections.Generic.Dictionary<string, string>
        {
            ["head"] = "healers-circlet"
        };
        Call("ApplyEquippedItemBonuses", _actor);
        Check(_actor.MaxMagicPoints == 32 && _actor.HealingSpellBonus == 3,
            "The Healer's Circlet must grant +2 maximum MP and +3 healing spell strength.");
        Call("ApplyEquippedItemBonuses", _actor);
        Check(_actor.MaxMagicPoints == 32,
            "Recalculating equipment bonuses must not stack the Healer's Circlet's maximum MP bonus.");

        _ally.ApplyDamage(20);
        var healed = (bool)Call("TryHealTarget", _actor, _ally, 4, 3, "lesser-heal", "Lesser Heal", 0, 2, true, false);
        Check(healed && _ally.HitPoints == 37,
            "The Healer's Circlet must add 3 HP to a magical healing spell.");

        Reset(mana: 20);
        equippedByUnit[_actor.UnitId] = new System.Collections.Generic.Dictionary<string, string>
        {
            ["head"] = "mages-amulet"
        };
        Call("ApplyEquippedItemBonuses", _actor);
        Check(_actor.MaxMagicPoints == 33 && _actor.ScaledMagicPointRegenPerTurn == 3,
            "The Mage's Amulet must grant +3 maximum MP and +2 MP regeneration per turn.");
        _actor.ResetTurnResources();
        Check(_actor.MagicPoints == 23,
            "The Mage's Amulet must restore its additional 2 MP when turn resources reset.");

        equippedByUnit.Remove(_actor.UnitId);
        Call("ApplyEquippedItemBonuses", _actor);
        Check(_actor.MaxMagicPoints == 30 && _actor.ScaledMagicPointRegenPerTurn == 1
            && _actor.HealingSpellBonus == 0,
            "Unequipping bonus items must remove their bonuses.");

        Reset();
        equippedByUnit[_actor.UnitId] = new System.Collections.Generic.Dictionary<string, string>
        {
            ["feet"] = "boots-of-speed"
        };
        Call("ApplyEquippedItemBonuses", _actor);
        Check(_actor.MovementPerTurn == 5 && _actor.RemainingMovement == 3,
            "Boots of Speed must add 2 movement per turn when equipped.");
        _actor.ResetTurnResources();
        Call("ApplyEquippedItemBonuses", _actor);
        Check(_actor.MovementPerTurn == 5 && _actor.RemainingMovement == 5,
            "Movement bonuses must not stack when equipment bonuses are recalculated.");
        equippedByUnit[_actor.UnitId].Remove("feet");
        Call("ApplyEquippedItemBonuses", _actor);
        Check(_actor.MovementPerTurn == 3 && _actor.RemainingMovement == 3,
            "Unequipping Boots of Speed must remove its movement bonus.");

        var miloStock = Field<GameData>("_gameData").GetVendorStartingInventory("milo");
        Check(miloStock.Contains("boots-of-speed")
            && (int)Call("GetItemBuyPrice", "milo", "boots-of-speed") == 150,
            "Milo's stock must include Boots of Speed for 150 gold.");
    }

    private void CheckConsumableActionBarIcons()
    {
        var getIcon = typeof(HudController).GetMethod("GetGameIcon", BindingFlags.Static | BindingFlags.NonPublic);
        _testHud.SetConsumableButtons(new Array<Dictionary>
        {
            new()
            {
                { "slot_key", "consumable-1" },
                { "id", "healing-potion" },
                { "label", "Healing Potion" },
                { "detail", "Restores health." },
                { "is_enabled", 1 }
            },
            new()
            {
                { "slot_key", "consumable-2" },
                { "id", "magic-potion" },
                { "label", "Magic Potion" },
                { "detail", "Restores magic." },
                { "is_enabled", 1 }
            }
        }, true);

        var healingIcon = (Texture2D)getIcon.Invoke(null, new object[] { "healing-potion" });
        var magicIcon = (Texture2D)getIcon.Invoke(null, new object[] { "magic-potion" });
        Check(_testHud.GetNode<Button>("ActionPanel/ActionVBox/ActionRow/ActionButtons/ConsumableButton1").Icon == healingIcon,
            "Healing potion action-bar button must use row 8, column 1");
        Check(_testHud.GetNode<Button>("ActionPanel/ActionVBox/ActionRow/ActionButtons/ConsumableButton2").Icon == magicIcon,
            "Magic potion action-bar button must use row 8, column 2");
        Check(_testHud.GetNode<Button>("ActionPanel/ActionVBox/ActionRow/ActionButtons/ConsumableButton1").TooltipText == "Healing Potion"
            && _testHud.GetNode<Button>("ActionPanel/ActionVBox/ActionRow/ActionButtons/ConsumableButton2").TooltipText == "Magic Potion",
            "Consumable icon tooltips must show only their names");
        Check(Unit.DefaultConsumableSlotCount == 2,
            "Every unit must have exactly two default consumable slots");
    }

    private void CheckAbilityTray()
    {
        var abilities = (Array<Dictionary>)Call("BuildAbilityEntriesForHud", _actor, false);
        _testHud.SetAbilityButtons(abilities, true);
        var moreButton = _testHud.GetNode<Button>("ActionPanel/ActionVBox/ActionRow/ActionButtons/MoreAbilitiesButton");
        var tray = _testHud.GetNode<PanelContainer>("AbilityTrayPanel");
        var grid = _testHud.GetNode<GridContainer>("AbilityTrayPanel/AbilityTrayVBox/AbilityTrayScroll/AbilityTrayGrid");
        var plusColor = moreButton.GetThemeColor("font_color");
        Check(moreButton.Visible && moreButton.Text == "+" && plusColor.G > plusColor.R,
            "The compact overflow button must display a green plus when more than five abilities are available");
        Check(moreButton.GetThemeFontSize("font_size") >= 32,
            "The compact overflow button must display a prominent plus");

        moreButton.EmitSignal(Button.SignalName.Pressed);
        Check(tray.Visible && grid.GetChildCount() == abilities.Count,
            "Opening the ability tray must show every available ability, including those beyond the five quick slots");
        Check(tray.Size.Y < 250.0f,
            "The ability tray must size to its rows instead of retaining a large empty scroll area");

        var draggedAbilityId = abilities[6]["id"].AsString();
        var trayAbility = grid.GetChild<AbilityBarButton>(6);
        var quickSlot = _testHud.GetNode<AbilityBarButton>("ActionPanel/ActionVBox/ActionRow/ActionButtons/AbilityButton1");
        var dragData = Variant.From($"ability-tray:{draggedAbilityId}");
        var droppedSlotIndex = -1;
        var droppedAbilityId = "";
        _testHud.QuickAbilitySlotDropRequested += (slotIndex, abilityId) =>
        {
            droppedSlotIndex = slotIndex;
            droppedAbilityId = abilityId;
        };
        Check(trayAbility.CanDragAbility && quickSlot._CanDropData(Vector2.Zero, dragData),
            "Abilities in the tray must be draggable onto quick slots");
        quickSlot._DropData(Vector2.Zero, dragData);
        var quickAbilityIds = Field<System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>>("_quickAbilityIdsByUnitId");
        Check(droppedSlotIndex == 0 && droppedAbilityId == draggedAbilityId
            && quickAbilityIds[_actor.UnitId][0] == draggedAbilityId,
            "Dropping an ability onto a quick slot must report the target slot and ability ID");

        var existingQuickAbilityId = quickAbilityIds[_actor.UnitId][4];
        var swapPayload = Variant.From($"ability-tray:{existingQuickAbilityId}");
        _testHud.GetNode<AbilityBarButton>("ActionPanel/ActionVBox/ActionRow/ActionButtons/AbilityButton2")
            ._DropData(Vector2.Zero, swapPayload);
        Check(quickAbilityIds[_actor.UnitId][1] == existingQuickAbilityId
            && quickAbilityIds[_actor.UnitId][4] == "lesser-heal",
            "Dragging an ability already in a quick slot must swap it with the destination slot");

        var buildSaveData = typeof(GamePersistence).GetMethod("BuildSaveData", BindingFlags.NonPublic | BindingFlags.Instance);
        var saveData = (Dictionary)buildSaveData.Invoke(Field<GamePersistence>("_persistence"), null);
        var savedQuickAbilityMap = (Dictionary)((Variant)saveData["quick_ability_ids_by_unit_id"]);
        var savedQuickAbilityIds = (Godot.Collections.Array)((Variant)savedQuickAbilityMap[_actor.UnitId]);
        Check(((Variant)savedQuickAbilityIds[0]).AsString() == draggedAbilityId,
            "Customized quick slots must be included in save data");

        var emittedAbilityId = "";
        _testHud.AbilityPressed += abilityId => emittedAbilityId = abilityId;
        grid.GetChild<AbilityBarButton>(6).EmitSignal(Button.SignalName.Pressed);
        Check(emittedAbilityId == draggedAbilityId && !tray.Visible,
            "Choosing an ability from the tray must emit its action and close the tray");

        var fiveAbilities = new Array<Dictionary>();
        for (var index = 0; index < 5; index++)
        {
            fiveAbilities.Add(abilities[index]);
        }

        _testHud.SetAbilityButtons(fiveAbilities, true);
        Check(!moreButton.Visible && !tray.Visible,
            "The overflow button and tray must stay hidden when all abilities fit in the five quick slots");

        var defaultQuickAbilityIds = new System.Collections.Generic.List<string>();
        for (var index = 0; index < 5; index++)
        {
            defaultQuickAbilityIds.Add(abilities[index]["id"].AsString());
        }

        quickAbilityIds[_actor.UnitId] = defaultQuickAbilityIds;
        Call("SyncHudFromGameState");
    }

    private void CheckCompactActionBar()
    {
        const string counterPath = "ActionPanel/ActionVBox/ActionRow/MovementCounterPanel";
        const string counterContentPath = counterPath + "/MovementCounter";
        var movementCounter = _testHud.GetNode<PanelContainer>(counterPath);
        var movementLabel = _testHud.GetNode<Label>(counterContentPath + "/MoveCounterRow/MoveCounterLabel");
        var actionPointLabel = _testHud.GetNode<Label>(counterContentPath + "/ActionPointCounterRow/ActionPointCounterLabel");
        var movementDiscs = _testHud.GetNode<HBoxContainer>(counterContentPath + "/MoveCounterRow/MoveCounterDiscs");
        var actionPointDiscs = _testHud.GetNode<HBoxContainer>(counterContentPath + "/ActionPointCounterRow/ActionPointCounterDiscs");
        var counterStyle = (StyleBoxFlat)movementCounter.GetThemeStylebox("panel");
        Check(movementCounter.MouseFilter == Control.MouseFilterEnum.Stop
            && movementCounter.TooltipText.Contains("Drag"),
            "Movement counter must retain the action bar's drag affordance");
        Check(counterStyle.BgColor == TacticalTheme.Iron && counterStyle.BorderColor == TacticalTheme.Brass,
            "Movement counter must use the dark background and gold trim");
        Check(movementLabel.Text == "Move:" && actionPointLabel.Text == "AP:",
            "Movement and action points must have separate stacked labels");
        var flowField = PrivateField("_flowState");
        var previousFlow = flowField.GetValue(this);
        foreach (var flowName in new[] { "Exploration", "Combat" })
        {
            flowField.SetValue(this, Enum.Parse(flowField.FieldType, flowName));
            Call("SyncHudFromGameState");
            Check(movementDiscs.GetChildCount() == _actor.RemainingMovement
                && actionPointDiscs.GetChildCount() == _actor.RemainingActionPoints,
                $"{flowName} action bar must show remaining movement and action points as discs");
        }
        flowField.SetValue(this, previousFlow);
        Call("SyncHudFromGameState");
        Check(_testHud.GetNodeOrNull<Label>("ActionPanel/ActionVBox/ActionHeader") == null
            && _testHud.GetNodeOrNull<Label>("ActionPanel/ActionVBox/ActiveUnitLabel") == null
            && _testHud.GetNodeOrNull<Label>("ActionPanel/ActionVBox/ConsumablesHeader") == null,
            "Action bar must not show redundant headers or party stats");
        Check(_testHud.GetNode<HBoxContainer>("ActionPanel/ActionVBox/ActionRow/ActionButtons").GetChildCount() == 9,
            "Quick abilities, overflow access, consumables, and End Turn must share one compact row");
        Check(_testHud.GetNode<PanelContainer>("ActionPanel").Size.Y <= 90.0f,
            "Action bar height must be reduced");
        Check(movementCounter.MouseDefaultCursorShape == Control.CursorShape.Move,
            "Movement counter must advertise the draggable action bar");

        var grip = _testHud.GetNode<Label>("ActionPanel/ActionVBox/ActionRow/ActionDragHandle");
        Check(grip.MouseFilter == Control.MouseFilterEnum.Stop
            && grip.MouseDefaultCursorShape == Control.CursorShape.Move
            && grip.TooltipText.Contains("Drag"),
            "Action bar must provide a visible draggable grip");

        var utilityPanel = _testHud.GetNode<PanelContainer>("UtilityPanel");
        var utilityMenu = _testHud.GetNode<VBoxContainer>("UtilityPanel/UtilityVBox");
        var utilityButtons = _testHud.GetNode<VBoxContainer>("UtilityPanel/UtilityVBox/UtilityButtons");
        var visibleRect = _testHud.GetViewport().GetVisibleRect();
        Check(Mathf.IsEqualApprox(utilityPanel.Position.Y, 12.0f)
            && Mathf.IsEqualApprox(utilityPanel.Position.X + utilityPanel.Size.X, visibleRect.Size.X - 24.0f),
            "Utility menu must be aligned to the top-right corner");
        Check(_testHud.GetNode<Label>("UtilityPanel/UtilityVBox/UtilityHeader").GetParent() == utilityMenu
            && utilityButtons.GetParent() == utilityMenu,
            "Utility heading and buttons must share one vertical menu");
        var expectedUtilityButtons = new[] { "InventoryButton", "ReserveButton", "SaveButton", "LoadButton", "SettingsButton" };
        Check(utilityButtons.GetChildCount() == expectedUtilityButtons.Length,
            "Utility menu must contain only the five requested controls");
        for (var buttonIndex = 0; buttonIndex < expectedUtilityButtons.Length; buttonIndex++)
        {
            Check(utilityButtons.GetChild(buttonIndex).Name == expectedUtilityButtons[buttonIndex],
                "Utility controls must be ordered Party, Reserves, Save, Load, Settings");
        }

        var widestUtilityButtonWidth = Mathf.Max(
            Mathf.Max(
                _testHud.GetNode<Button>("UtilityPanel/UtilityVBox/UtilityButtons/InventoryButton").GetCombinedMinimumSize().X,
                _testHud.GetNode<Button>("UtilityPanel/UtilityVBox/UtilityButtons/SettingsButton").GetCombinedMinimumSize().X),
            Mathf.Max(
                Mathf.Max(
                    _testHud.GetNode<Button>("UtilityPanel/UtilityVBox/UtilityButtons/SaveButton").GetCombinedMinimumSize().X,
                    _testHud.GetNode<Button>("UtilityPanel/UtilityVBox/UtilityButtons/LoadButton").GetCombinedMinimumSize().X),
                _testHud.GetNode<Button>("UtilityPanel/UtilityVBox/UtilityButtons/ReserveButton").GetCombinedMinimumSize().X));
        Check(utilityPanel.Size.X >= widestUtilityButtonWidth
            && utilityPanel.Size.X - widestUtilityButtonWidth <= 20.1f,
            "Utility menu width must fit the widest button with only modest padding");
        var utilityPanelStyle = utilityPanel.GetThemeStylebox("panel", "PanelContainer") as StyleBoxFlat;
        var utilityButtonStyle = _testHud.GetNode<Button>("UtilityPanel/UtilityVBox/UtilityButtons/SettingsButton")
            .GetThemeStylebox("normal", "Button") as StyleBoxFlat;
        Check(utilityPanelStyle != null
            && utilityPanelStyle.BgColor.A == 0.0f
            && utilityButtonStyle != null
            && utilityButtonStyle.BgColor.A > 0.0f,
            "Utility panel background must be transparent while buttons remain styled");
        var settingsPanel = _testHud.GetNode<PanelContainer>("SettingsPanel");
        var helpButton = _testHud.GetNode<Button>("SettingsPanel/SettingsVBox/SettingsButtons/HelpButton");
        var soundFxButton = _testHud.GetNode<Button>("SettingsPanel/SettingsVBox/SettingsButtons/SoundFxButton");
        _testHud.GetNode<Button>("UtilityPanel/UtilityVBox/UtilityButtons/SettingsButton").EmitSignal(Button.SignalName.Pressed);
        Check(settingsPanel.Visible && helpButton.Visible && soundFxButton.Visible,
            "Settings must open a submenu containing Help and Sound FX");
        soundFxButton.EmitSignal(Button.SignalName.Pressed);
        Check(!settingsPanel.Visible && _testHud.GetNode<PanelContainer>("SoundFxPanel").Visible,
            "Sound FX must open its submenu from Settings");
        _testHud.GetNode<Button>("SoundFxPanel/SoundFxVBox/SoundFxBackButton").EmitSignal(Button.SignalName.Pressed);
        var gameOptionsButton = _testHud.GetNode<Button>("SettingsPanel/SettingsVBox/SettingsButtons/GameOptionsButton");
        gameOptionsButton.EmitSignal(Button.SignalName.Pressed);
        var gameOptionsPanel = _testHud.GetNode<PanelContainer>("GameOptionsPanel");
        var healthBarsToggle = _testHud.GetNode<CheckButton>("GameOptionsPanel/GameOptionsVBox/HealthBarsToggle");
        var durationSlider = _testHud.GetNode<HSlider>("GameOptionsPanel/GameOptionsVBox/CombatMessageDurationSlider");
        var durationValue = _testHud.GetNode<Label>("GameOptionsPanel/GameOptionsVBox/CombatMessageDurationRow/CombatMessageDurationValue");
        var gameOptionsManager = GetNode<GameOptionsManager>("/root/GameOptionsManager");
        var originalHealthBarsEnabled = gameOptionsManager.HealthBarsEnabled;
        var originalDurationMultiplier = gameOptionsManager.CombatMessageDurationMultiplier;
        Check(!settingsPanel.Visible && gameOptionsPanel.Visible
            && healthBarsToggle.ButtonPressed == originalHealthBarsEnabled
            && durationSlider.Value == originalDurationMultiplier,
            "Game Options must open with the persisted values for both options");
        healthBarsToggle.EmitSignal(BaseButton.SignalName.Toggled, !originalHealthBarsEnabled);
        durationSlider.Value = 5.0;
        Check(gameOptionsManager.HealthBarsEnabled != originalHealthBarsEnabled
            && gameOptionsManager.CombatMessageDurationMultiplier == 5
            && durationValue.Text == "5x",
            "Game Options controls must update health bar visibility and the 5x message duration");
        gameOptionsManager.SetHealthBarsEnabled(originalHealthBarsEnabled);
        durationSlider.Value = originalDurationMultiplier;
        _testHud.GetNode<Button>("GameOptionsPanel/GameOptionsVBox/GameOptionsBackButton")
            .EmitSignal(Button.SignalName.Pressed);
        helpButton.EmitSignal(Button.SignalName.Pressed);
        Check(!settingsPanel.Visible && _testHud.GetNode<PanelContainer>("HelpPanel").Visible,
            "Help must open from Settings instead of the utility controls");
        _testHud.GetNode<Button>("HelpPanel/HelpVBox/HelpButtons/CloseHelpButton").EmitSignal(Button.SignalName.Pressed);
        var combatLogPanel = _testHud.GetNode<PanelContainer>("CombatLogPanel");
        var combatLogPanelStyle = combatLogPanel.GetThemeStylebox("panel", "PanelContainer") as StyleBoxFlat;
        var combatLogListStyle = _testHud.GetNode<ItemList>("CombatLogPanel/CombatLogVBox/CombatLog")
            .GetThemeStylebox("panel", "ItemList") as StyleBoxFlat;
        var combatLogButtonStyle = _testHud.GetNode<Button>("CombatLogPanel/CombatLogVBox/CombatLogHeaderRow/CombatLogMinimizeButton")
            .GetThemeStylebox("normal", "Button") as StyleBoxFlat;
        Check(combatLogPanelStyle != null
            && combatLogPanelStyle.BgColor.A == 0.0f
            && combatLogListStyle != null
            && combatLogListStyle.BgColor.A == 0.0f
            && combatLogButtonStyle != null
            && combatLogButtonStyle.BgColor.A > 0.0f,
            "Combat log backgrounds must be transparent while its button remains styled");

        var abilityStyle = _testHud.GetNode<Button>("ActionPanel/ActionVBox/ActionRow/ActionButtons/AbilityButton1")
            .GetThemeStylebox("normal", "Button") as StyleBoxFlat;
        var endTurnStyle = _testHud.GetNode<Button>("ActionPanel/ActionVBox/ActionRow/ActionButtons/EndTurnButton")
            .GetThemeStylebox("normal", "Button") as StyleBoxFlat;
        Check(abilityStyle != null && abilityStyle.BgColor == TacticalTheme.IronRaised
            && endTurnStyle != null && endTurnStyle.BgColor == TacticalTheme.Crimson,
            "Crimson emphasis must be reserved for End Turn, not routine ability buttons");
        var actionPanelStyle = _testHud.GetNode<PanelContainer>("ActionPanel")
            .GetThemeStylebox("panel", "PanelContainer") as StyleBoxFlat;
        Check(actionPanelStyle != null
            && actionPanelStyle.BgColor.A == 0.0f
            && abilityStyle != null
            && abilityStyle.BgColor.A > 0.0f,
            "Action bar background must be transparent while action buttons remain styled");
        var partyPanelStyle = _testHud.GetNode<PanelContainer>("PartyPanel")
            .GetThemeStylebox("panel", "PanelContainer") as StyleBoxFlat;
        Check(partyPanelStyle != null && partyPanelStyle.BgColor.A == 0.0f,
            "Party list panel background must be transparent");

        Check(utilityPanel.Position.X >= 0.0f
            && utilityPanel.Position.X + utilityPanel.Size.X <= visibleRect.Size.X
            && _testHud.GetNode<PanelContainer>("ActionPanel").Position.X >= 0.0f,
            "Top utility row and action dock must remain within the logical viewport");

        var explorationFlowField = PrivateField("_flowState");
        var flowBeforeTabCycle = explorationFlowField.GetValue(this);
        var selectedCharacterField = PrivateField("_selectedCharacterUnitId");
        var previousSelectedCharacter = selectedCharacterField.GetValue(this);
        explorationFlowField.SetValue(this, Enum.Parse(explorationFlowField.FieldType, "Exploration"));
        selectedCharacterField.SetValue(this, _actor.UnitId);
        _Input(new InputEventKey { Pressed = true, Keycode = Key.Tab });
        Check(Field<string>("_selectedCharacterUnitId") == _ally.UnitId,
            "Tab must select the next living party member during exploration");
        _Input(new InputEventKey { Pressed = true, Keycode = Key.Tab, ShiftPressed = true });
        Check(Field<string>("_selectedCharacterUnitId") == _actor.UnitId,
            "Shift+Tab must select the previous living party member during exploration");
        explorationFlowField.SetValue(this, flowBeforeTabCycle);
        selectedCharacterField.SetValue(this, previousSelectedCharacter);
        Call("SyncHudFromGameState");
        var partyCards = _testHud.GetNode<VBoxContainer>("PartyPanel/PartyVBox/PartyList");
        StyleBoxFlat actorCardStyle = null;
        StyleBoxFlat allyCardStyle = null;
        var partyPortraitTextCount = 0;
        var partyPortraitTextSizesMatch = true;
        var partyPortraitCardHeightFits = true;
        foreach (var child in partyCards.GetChildren())
        {
            if (child is not PartyCard card)
            {
                continue;
            }

            var cardStyle = card.GetThemeStylebox("normal", "Button") as StyleBoxFlat;
            if (card.UnitId == _actor.UnitId)
            {
                actorCardStyle = cardStyle;
                partyPortraitCardHeightFits &= card.CustomMinimumSize.Y >= 84.0f;
                foreach (var labelNode in card.FindChildren("*", "Label", true, false))
                {
                    var label = (Label)labelNode;
                    if (label.Text == _actor.UnitName)
                    {
                        partyPortraitTextCount++;
                        partyPortraitTextSizesMatch &= label.GetThemeFontSize("font_size") == 14;
                    }
                    else if (label.Text.StartsWith("HP ", StringComparison.Ordinal)
                        || label.Text.StartsWith("MP ", StringComparison.Ordinal)
                        || label.Text.StartsWith("AC ", StringComparison.Ordinal))
                    {
                        partyPortraitTextCount++;
                        partyPortraitTextSizesMatch &= label.GetThemeFontSize("font_size") == 11;
                    }
                }
            }
            else if (card.UnitId == _ally.UnitId)
            {
                allyCardStyle = cardStyle;
            }
        }
        Check(actorCardStyle != null
            && allyCardStyle != null
            && actorCardStyle.BgColor.R > allyCardStyle.BgColor.R
            && actorCardStyle.BgColor.G > allyCardStyle.BgColor.G
            && actorCardStyle.BgColor.B > allyCardStyle.BgColor.B,
            "Selected party member card must have a slightly lighter background");
        Check(partyPortraitTextCount >= 4 && partyPortraitTextSizesMatch && partyPortraitCardHeightFits,
            "Party portrait cards must fit 14px names and 11px HP, MP, and AC details.");

        var combatLogMinimizeButton = _testHud.GetNode<Button>("CombatLogPanel/CombatLogVBox/CombatLogHeaderRow/CombatLogMinimizeButton");
        combatLogMinimizeButton.EmitSignal(Button.SignalName.Pressed);
        Check(Mathf.IsEqualApprox(combatLogPanel.Position.X + combatLogPanel.Size.X, visibleRect.Size.X - 12.0f)
            && Mathf.IsEqualApprox(combatLogPanel.Position.Y + combatLogPanel.Size.Y, visibleRect.Size.Y - 12.0f),
            "Minimized combat log must sit in the bottom-right corner with a slight inset");
        combatLogMinimizeButton.EmitSignal(Button.SignalName.Pressed);
        Check(combatLogPanel.Size.X >= 260.0f && combatLogPanel.Size.Y >= 160.0f,
            "Restoring the combat log must restore its expanded minimum size");
    }

    private void SetupFixture()
    {
        Set("_gameData", GetNode<GameData>("/root/GameData"));
        Set("_gridWidth", 16);
        Set("_gridHeight", 8);
        Set("_currentMapId", "feedback-test");
        var units = new Node2D { Name = "Units" };
        AddChild(units);
        Set("_unitsRoot", units);
        _actor = NewUnit(units, "caster", "player", new Vector2I(2, 2));
        _ally = NewUnit(units, "ally", "player", new Vector2I(2, 4));
        _enemy = NewUnit(units, "enemy", "enemy", new Vector2I(5, 2));
        Field<Array<Unit>>("_allUnits").AddRange(new Array<Unit> { _actor, _ally, _enemy });
        Field<Array<Unit>>("_playerUnits").AddRange(new Array<Unit> { _actor, _ally });
        Field<Array<Unit>>("_enemyUnits").Add(_enemy);
        Field<HashSet<string>>("_activeCombatEnemyUnitIds").Add(_enemy.UnitId);
        var turns = new TurnManager();
        AddChild(turns);
        Set("_turnManager", turns);
        turns.SetupTurnOrder(new Array<Unit> { _actor, _ally, _enemy });
        Set("_selectedCharacterUnitId", _actor.UnitId);
        Set("_explorerUnit", _actor);
        _door = new Dictionary
        {
            { "id", "door" }, { "cell", _doorCell }, { "locked", true }, { "is_open", false }
        };
        Field<System.Collections.Generic.Dictionary<Vector2I, Dictionary>>("_mapDoorByCell")[_doorCell] = _door;
        Field<Array<Dictionary>>("_mapDoors").Add(_door);
        Field<Array<Dictionary>>("_mapProps").Add(new Dictionary
        {
            { "id", "trap" }, { "type", "trap" }, { "grid_pos", new Vector2I(1, 2) }
        });
        _testLoader = new MapLoader();
        Set("_mapLoader", _testLoader);
        var layer = new CanvasLayer();
        AddChild(layer);
        _testHud = GD.Load<PackedScene>("res://ui/HUD.tscn").Instantiate<HudController>();
        layer.AddChild(_testHud);
        Set("_hud", _testHud);
        Set("_persistence", new GamePersistence(this, "user://action-feedback-consumable-test.json"));
        _testHud.AbilityPressed += id => Call("OnHudAbilityPressed", id);
        _testHud.QuickAbilitySlotDropRequested += (slotIndex, abilityId) => Call("OnHudQuickAbilitySlotDropRequested", slotIndex, abilityId);
        _testHud.ConsumablePressed += slotKey => Call("OnHudConsumablePressed", slotKey);
        _testHud.EquipItemRequested += itemId => Call("OnHudEquipItemRequested", itemId);
        _testHud.UnequipItemRequested += slotKey => Call("OnHudUnequipItemRequested", slotKey);
        _testHud.LevelUpRequested += unitId => Call("OnHudLevelUpRequested", unitId);
        _testHud.LevelUpStatSelected += (unitId, statName) => Call("OnHudLevelUpStatSelected", unitId, statName);
        _banner = _testHud.GetNode<Label>("CombatBanner/CombatBannerLabel");
        _log = HudField<ItemList>("_combatLog");
        Reset();
    }

    private Unit NewUnit(Node parent, string id, string team, Vector2I cell)
    {
        var unit = GD.Load<PackedScene>("res://scenes/Unit.tscn").Instantiate<Unit>();
        parent.AddChild(unit);
        unit.Setup(Config(id, team, cell));
        return unit;
    }

    private static Dictionary Config(string id, string team, Vector2I cell, int mana = 30) => new()
    {
        { "id", id }, { "name", id }, { "team", team }, { "class_id", "wizard" },
        { "grid_pos", cell }, { "hit_points", 50 }, { "max_hit_points", 50 },
        { "magic_points", mana }, { "max_magic_points", 30 },
        { "initiative", id == "caster" ? 1000 : 1 },
        { "primary_ability_id", "magic-missile" },
        { "ability_ids", new Array<string>
            { "magic-missile", "lesser-heal", "fireball", "defend", "charge", "sleep",
                "pin", "poison-strike", "melee", "pick-lock", "disarm-trap" } }
    };

    private void Reset(int mana = 30, bool exploration = false)
    {
        ResetBanner();
        var flow = PrivateField("_flowState");
        flow.SetValue(this, Enum.Parse(flow.FieldType, exploration ? "Exploration" : "Combat"));
        _actor.ClearStatusEffectsByScope("combat_only");
        _actor.ClearStatusEffectsByScope("persistent");
        _actor.ClearAbilityCooldowns();
        _actor.Setup(Config("caster", "player", new Vector2I(2, 2), mana));
        _enemy.Setup(Config("enemy", "enemy", new Vector2I(5, 2)));
        _ally.Setup(Config("ally", "player", new Vector2I(2, 4)));
        _ally.ClearStatusEffectsByScope("combat_only");
        Field<TurnManager>("_turnManager").SetupTurnOrder(new Array<Unit> { _actor, _ally, _enemy });
        _actor.TrySpendMagicPoints(_actor.MagicPoints - mana);
        Field<HashSet<Vector2I>>("_walkableCells").Clear();
        Field<HashSet<Vector2I>>("_wallCellSet").Clear();
        Field<HashSet<string>>("_openedPropIds").Clear();
        Field<HashSet<string>>("_openedDoorIds").Clear();
        Field<System.Collections.Generic.Dictionary<string, HashSet<string>>>("_unlockedDoorIdsByMap").Clear();
        Field<System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>>("_quickAbilityIdsByUnitId").Clear();
        Field<System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>>("_equippedItemsByUnitId").Clear();
        _door["locked"] = true;
        _door["is_open"] = false;
        Set("_awaitingPlayerAttackDirection", false);
        Set("_awaitingShadowStrikeAttack", false);
        Set("_awaitingItemIdentifyTarget", false);
        Set("_shadowStrikeDestination", new Vector2I(-1, -1));
        Field<HashSet<string>>("_identifiedItemIds").Clear();
        Set("_mouseMoveInputLockedUntilMs", 0UL);
        RefreshFog();
        Call("SetSelectedAbilityId", _actor, "magic-missile");
        Call("SyncHudFromGameState");
    }

    private void CheckResources()
    {
        Reset(0);
        var button = AbilityButton("magic-missile");
        Check(!button.Disabled && button.SelfModulate.R < 0.8f,
            "Unavailable ability must be dimmed but accept attempted clicks");
        ExpectFailure(() => button.EmitSignal(Button.SignalName.Pressed), "Out of mana - cannot cast Magic Missile.");
        Reset(2);
        ExpectFailure(() => Call("OnHudAbilityPressed", "magic-missile"),
            "Not enough mana to cast Magic Missile - requires 3 MP, have 2.");
        Reset(0);
        ExpectFailure(() => _Input(new InputEventKey { Pressed = true, Keycode = Key.F }),
            "Out of mana - cannot cast Magic Missile.");
        Reset();
        _actor.MarkAbilityUsed("defend");
        ExpectFailure(() => Call("OnHudAbilityPressed", "defend"), "No action points left to use Defend.");
        Reset();
        _actor.MarkAbilityCooldownOnly("magic-missile", 2);
        ExpectFailure(() => AbilityButtonAfterSync("magic-missile").EmitSignal(Button.SignalName.Pressed),
            "Magic Missile is on cooldown - 2 turns remaining.");
        Reset();
        _actor.ApplyStatusEffect("sleep", "Asleep", false, 2, preventActions: true);
        ExpectFailure(() => Call("OnHudAbilityPressed", "charge"), "caster cannot act while asleep.");
        Reset();
        ExpectFailure(() => Call("OnHudAbilityPressed", "pin"), "Equip a ranged weapon to use Pin.");
        Reset();
        Field<System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>>("_equippedItemsByUnitId")[_actor.UnitId]
            = new System.Collections.Generic.Dictionary<string, string> { { "2-handed", "short-bow" } };
        ExpectFailure(() => Call("OnHudAbilityPressed", "melee"), "Melee Attack requires a melee weapon or empty hands.");
        Reset();
        _actor.Setup(new Dictionary
        {
            { "id", "caster" }, { "name", "caster" }, { "team", "player" },
            { "grid_pos", new Vector2I(2, 2) }, { "ability_ids", new Array<string> { "melee" } }
        });
        ExpectFailure(() => Call("OnHudAbilityPressed", "fireball"), "caster has not learned Fireball.");
    }

    private void CheckActionPointsAndLevelUp()
    {
        var defaultConfig = Config("caster", "player", new Vector2I(2, 2));
        _actor.Setup(defaultConfig);
        Check(_actor.ActionPoints == 1 && _actor.RemainingActionPoints == 1,
            "Units must default to one action point.");

        var enemyConfig = Config("enemy", "enemy", new Vector2I(5, 2));
        _enemy.Setup(enemyConfig);
        Check(_enemy.ActionPoints == 1 && _enemy.RemainingActionPoints == 1,
            "Enemy units must also default to one action point.");
        enemyConfig["action_points"] = 3;
        _enemy.Setup(enemyConfig);
        Check(_enemy.ActionPoints == 3 && _enemy.RemainingActionPoints == 3,
            "Configured action points must apply to enemy units.");
        _enemy.MarkAbilityUsed("melee");
        _enemy.MarkAbilityUsed("melee");
        Check(_enemy.CanUseAbilityThisTurn() && _enemy.RemainingActionPoints == 1,
            "Units must be able to spend multiple action points in a turn.");
        _enemy.ResetTurnResources();
        Check(_enemy.RemainingActionPoints == 3,
            "Turn reset must restore all configured action points.");

        var levelOneConfig = Config("caster", "player", new Vector2I(2, 2));
        _actor.Setup(levelOneConfig);
        Check(_actor.GrantExperience(700) == 3 && _actor.Level == 4,
            "Experience thresholds must grant multiple levels in one award.");
        Check(_actor.ActionPoints == 2 && _actor.RemainingActionPoints == 2,
            "Reaching level 4 must grant an additional action point.");
        Check(_actor.UnallocatedStatPoints == 3,
            "Each level gained must grant one unallocated stat point.");
        var initialStrength = _actor.Strength;
        var initialIntelligence = _actor.Intelligence;
        var initialWisdom = _actor.Wisdom;
        Check(_actor.TryAllocateStatPoint("str")
            && _actor.TryAllocateStatPoint("intelligence")
            && _actor.TryAllocateStatPoint("wisdom")
            && !_actor.TryAllocateStatPoint("luck"),
            "Stat points must allocate only to supported stats and reject invalid choices.");
        Check(_actor.Strength == initialStrength + 1
            && _actor.Intelligence == initialIntelligence + 1
            && _actor.Wisdom == initialWisdom + 1
            && _actor.UnallocatedStatPoints == 0,
            "Stat allocation must increase the chosen stat and consume one point.");

        var levelEightConfig = Config("caster", "player", new Vector2I(2, 2));
        levelEightConfig["level"] = 8;
        levelEightConfig["action_points"] = 2;
        _actor.Setup(levelEightConfig);
        _actor.MarkAbilityUsed("defend");
        _actor.GrantExperience(_actor.ExperienceToNextLevel);
        Check(_actor.Level == 9 && _actor.ActionPoints == 3 && _actor.RemainingActionPoints == 2,
            "Reaching level 9 must grant an additional action point without restoring spent points.");

        var snapshot = _actor.BuildRuntimeSnapshot();
        _actor.Setup(defaultConfig);
        _actor.ApplyRuntimeSnapshot(snapshot);
        Check(_actor.ActionPoints == 3 && _actor.RemainingActionPoints == 2
            && _actor.UnallocatedStatPoints == 1 && _actor.Level == 9,
            "Runtime snapshots must preserve action points and unallocated stat points.");

        var legacySnapshot = new Dictionary
        {
            { "action_points", 2 },
            { "has_used_ability_this_turn", true }
        };
        _actor.Setup(defaultConfig);
        _actor.ApplyRuntimeSnapshot(legacySnapshot);
        Check(_actor.RemainingActionPoints == 0,
            "Older snapshots must still restore the previously used-action state.");
        Reset();
    }

    public void StartLevelUpChecks(bool createFixture)
    {
        if (createFixture)
            SetupFixture();
        Reset(exploration: true);
        _actor.GrantExperience(700);
        Call("SyncHudFromGameState");
        OpenPendingLevelUp();
    }

    public void OpenPendingLevelUp() =>
        _testHud.EmitSignal(HudController.SignalName.LevelUpRequested, _actor.UnitId);

    public void CheckLevelUpLayout()
    {
        var overlay = _testHud.GetNode<Control>("LevelUpOverlay");
        var panels = overlay.FindChildren("*", "PanelContainer", true, false);
        Check(panels.Count == 1, "The level-up overlay must contain exactly one panel.");
        var panel = (PanelContainer)panels[0];
        var title = panel.GetNodeOrNull<Label>("VBoxContainer/LevelUpHeader/LevelUpCharacterTitle");
        Check(title != null && title.Text == _actor.UnitName,
            "Only the character name must appear in the dialog header.");
        var advancement = (Label)panel.FindChildren("LevelUpAdvancementText", "Label", true, false)[0];
        Check(advancement.Text.StartsWith("Thou hast advanced from level ", StringComparison.Ordinal)
            && advancement.Text.EndsWith($" to {_actor.Level}!", StringComparison.Ordinal)
            && advancement.HorizontalAlignment == HorizontalAlignment.Left,
            "The level transition must end with emphasis and align to the left.");
        var bonusesTitle = (Label)panel.FindChildren("LevelUpBonusesTitle", "Label", true, false)[0];
        var statSectionTitle = (Label)panel.FindChildren("LevelUpStatSectionTitle", "Label", true, false)[0];
        Check(bonusesTitle.Text == "Bonuses Granted:"
            && statSectionTitle.Text == "Increase Your Attributes"
            && title.GetThemeFontSize("font_size") > statSectionTitle.GetThemeFontSize("font_size")
            && statSectionTitle.GetThemeFontSize("font_size") > bonusesTitle.GetThemeFontSize("font_size"),
            "The character name must be the largest text, followed by distinct bonus and stat section headings.");
        foreach (var bonus in new[] { ("HP", "HP: +"), ("MP", "MP: +"), ("AP", "AP: +"), ("Regen", "Regen: +") })
        {
            var bonusLabels = panel.FindChildren($"LevelUpBonus_{bonus.Item1}", "Label", true, false);
            Check(bonusLabels.Count == 1 && ((Label)bonusLabels[0]).Text.StartsWith(bonus.Item2, StringComparison.Ordinal),
                $"The {bonus.Item1} level-up bonus must have its own line.");
        }

        var partyList = _testHud.GetNode<VBoxContainer>("PartyPanel/PartyVBox/PartyList");
        var partyCard = partyList.GetChildren().OfType<PartyCard>().FirstOrDefault(card => card.UnitId == _actor.UnitId);
        var badges = partyCard?.FindChildren("LevelUpBadge", "Button", true, false);
        Check(badges != null && badges.Count == 1,
            "A pending level-up must show its badge on the party portrait.");
        if (badges != null && badges.Count == 1)
        {
            var badge = (Button)badges[0];
            var portraitBounds = badge.GetParent<Control>().GetGlobalRect();
            var badgeBounds = badge.GetGlobalRect();
            var badgeStyle = badge.GetThemeStylebox("normal") as StyleBoxFlat;
            Check(portraitBounds.HasPoint(badgeBounds.Position)
                && portraitBounds.HasPoint(badgeBounds.End - Vector2.One)
                && badge.Size.X <= 20.0f && badge.Size.Y <= 20.0f
                && badgeStyle != null && badgeStyle.CornerRadiusTopLeft >= 8
                && badge.GetNodeOrNull<Label>("LevelUpBadgeGlyph")?.Text == "+",
                "The compact, rounded level-up badge must stay entirely within the portrait.");
        }
        var labels = panel.FindChildren("*", "Label", true, false);
        Control lastRow = null;
        var rowCount = 0;
        float? buttonX = null;
        foreach (var child in labels)
        {
            var label = (Label)child;
            if (!label.Name.ToString().StartsWith("LevelUpStat_", StringComparison.Ordinal))
                continue;
            rowCount++;
            Check(label.Text.Contains(": ", StringComparison.Ordinal)
                && !label.Text.Contains("(", StringComparison.Ordinal)
                && label.GetThemeColor("font_color") == new Color(0.76f, 0.73f, 0.66f),
                "Stat labels must omit redundant abbreviations and match the party HP/MP text color.");
            var row = (HBoxContainer)label.GetParent();
            var button = row.GetChild<Button>(1);
            Check(button.Text == "+" && button.GetThemeStylebox("normal") is StyleBoxFlat style
                && style.BorderColor == TacticalTheme.Brass,
                "Each stat row must have a shared-style plus button.");
            if (buttonX.HasValue)
                Check(Mathf.IsEqualApprox(button.GlobalPosition.X, buttonX.Value), "Stat plus buttons must align vertically.");
            buttonX = button.GlobalPosition.X;
            lastRow = row;
        }
        Check(rowCount == 5, "The level-up panel must list all five stats.");
        var closeButton = panel.GetNodeOrNull<Button>("VBoxContainer/LevelUpCloseButton");
        Check(closeButton != null && closeButton.Text == "Close"
            && closeButton.GetIndex() == closeButton.GetParent().GetChildCount() - 1,
            "The level-up menu must provide a bottom-aligned Close button.");
        var gap = panel.GetGlobalRect().End.Y - closeButton.GetGlobalRect().End.Y;
        var padding = panel.GetThemeStylebox("panel").GetContentMargin(Side.Bottom);
        var statToCloseGap = closeButton.GetGlobalRect().Position.Y - lastRow.GetGlobalRect().End.Y;
        GD.Print($"LEVEL_UP_LAYOUT height={panel.Size.Y} minimum={panel.GetCombinedMinimumSize().Y} stat_close_gap={statToCloseGap} close_bottom_gap={gap} padding={padding}");
        Check(Mathf.Abs(gap - padding) <= 1.0f,
            $"Panel must end at the Close button plus padding ({padding}px), not leave {gap}px below it.");
        Check(statToCloseGap >= 0 && statToCloseGap <= 12,
            $"The Close button must follow the final stat with a small gap; got {statToCloseGap}px.");
        Check(Mathf.Abs(panel.Size.Y - panel.GetCombinedMinimumSize().Y) <= 1.0f,
            "Panel height must track its settled content minimum.");
        Check(panel.GetGlobalRect().Position.Y >= 0 && panel.GetGlobalRect().End.Y <= overlay.Size.Y,
            "The level-up panel must fit vertically inside the viewport.");
        Check(panel.GetGlobalRect().GetCenter().DistanceTo(overlay.GetGlobalRect().GetCenter()) <= 2.0f,
            "The compact panel must remain centered after layout and viewport resizing.");
    }

    public void ChooseLevelUpStat(int remainingPoints)
    {
        var overlay = _testHud.GetNode<Control>("LevelUpOverlay");
        var strength = _actor.Strength;
        foreach (var child in overlay.FindChildren("*", "Label", true, false))
        {
            var label = (Label)child;
            if (!label.Text.StartsWith("Strength:", StringComparison.Ordinal))
                continue;
            Check(label.Text == $"Strength: {strength}", "Reopened level-up must show the updated stat value.");
            label.GetParent().GetChild<Button>(1).EmitSignal(Button.SignalName.Pressed);
            break;
        }
        Check(_actor.Strength == strength + 1 && _actor.UnallocatedStatPoints == remainingPoints - 1,
            "A stat click must permanently spend exactly one level-up point.");
        Check(_testHud.GetNodeOrNull("LevelUpOverlay") == overlay,
            "Choosing a stat must keep the level-up dialog open.");
        var updatedLabel = (Label)overlay.FindChildren("LevelUpStat_strength", "Label", true, false)[0];
        var prompt = (Label)overlay.FindChildren("LevelUpAllocationPrompt", "Label", true, false)[0];
        Check(updatedLabel.Text == $"Strength: {strength + 1}"
            && (remainingPoints > 1
                ? prompt.Text.Contains($"Points remaining: {remainingPoints - 1}")
                : prompt.Text == "All stat points have been allocated."),
            $"The open dialog must immediately show the increased stat and remaining point count: {updatedLabel.Text}; {prompt.Text}.");
        Check(_actor.BuildRuntimeSnapshot()["strength"].AsInt32() == strength + 1,
            "The allocated stat must be included in saved state.");
    }

    public void CheckLevelUpClosed(int remainingPoints)
    {
        var pending = (Array<string>)Call("BuildPendingLevelUpUnitIds");
        Check(pending.Contains(_actor.UnitId) == (remainingPoints > 0),
            "The portrait plus must remain available until the final point is spent.");
        if (remainingPoints > 0)
        {
            Check(_testHud.GetNodeOrNull("LevelUpOverlay") != null,
                "The level-up menu must remain open while there are points to spend.");
            return;
        }

        var closeButton = (Button)_testHud.GetNode<Control>("LevelUpOverlay")
            .FindChildren("LevelUpCloseButton", "Button", true, false)[0];
        closeButton.EmitSignal(Button.SignalName.Pressed);
        Check(_testHud.GetNode<Control>("LevelUpOverlay").IsQueuedForDeletion(),
            "The bottom Close button must dismiss the level-up dialog after all points are spent.");
    }

    private void CheckTargets()
    {
        Reset();
        ExpectTargetFailure("magic-missile", new Vector2I(8, 2), "Out of range.");
        Reset();
        _enemy.SetGridPos(new Vector2I(8, 2));
        RefreshFog();
        Call("SetSelectedAbilityId", _actor, "magic-missile");
        Set("_awaitingPlayerAttackDirection", true);
        ExpectFailure(() => Call("HandlePlayerAttackDirectionInput", new InputEventKey
            { Pressed = true, Keycode = Key.D }), "Out of range.");
        Reset();
        Field<HashSet<Vector2I>>("_wallCellSet").Add(new Vector2I(3, 2));
        RefreshFog();
        ExpectTargetFailure("magic-missile", _enemy.GridPos, "No line of sight.");
        Reset();
        _enemy.SetGridPos(new Vector2I(4, 2));
        Field<HashSet<Vector2I>>("_wallCellSet").Add(new Vector2I(3, 2));
        RefreshFog();
        Call("SetSelectedAbilityId", _actor, "magic-missile");
        Set("_awaitingPlayerAttackDirection", true);
        ExpectFailure(() => Call("HandlePlayerAttackDirectionInput", new InputEventKey
            { Pressed = true, Keycode = Key.D }), "No line of sight.");
        Reset();
        ExpectTargetFailure("magic-missile", _ally.GridPos, "Magic Missile must target an enemy.");
        Reset();
        ExpectTargetFailure("magic-missile", new Vector2I(4, 1), "No enemy at that location.");
        Reset();
        ExpectTargetFailure("lesser-heal", _enemy.GridPos, "Select a living ally to heal.");
        Reset();
        ExpectTargetFailure("lesser-heal", _ally.GridPos, "ally is already at full health.");
        Reset();
        ExpectTargetFailure("sleep", new Vector2I(0, 5), "No living targets in the Sleep area.");
        Reset();
        _enemy.Setup(new Dictionary
        {
            { "id", "enemy" }, { "team", "enemy" }, { "race", "undead" }, { "grid_pos", new Vector2I(5, 2) }
        });
        ExpectTargetFailure("sleep", _enemy.GridPos, "Undead are immune to Sleep.");
        Reset();
        _enemy.SetGridPos(new Vector2I(3, 2));
        ExpectTargetFailure("charge", _enemy.GridPos, "Cannot charge an adjacent target.");
        Reset();
        foreach (var cell in new[] { new Vector2I(4, 1), new Vector2I(4, 2), new Vector2I(4, 3),
            new Vector2I(5, 1), new Vector2I(5, 3), new Vector2I(6, 1), new Vector2I(6, 2), new Vector2I(6, 3) })
            Field<HashSet<Vector2I>>("_wallCellSet").Add(cell);
        RefreshFog();
        ExpectTargetFailure("charge", _enemy.GridPos, "Target area is not visible.");
        Reset();
        Field<HashSet<Vector2I>>("_walkableCells").Add(_actor.GridPos);
        Field<HashSet<Vector2I>>("_walkableCells").Add(_enemy.GridPos);
        RefreshFog();
        ExpectTargetFailure("charge", _enemy.GridPos, "Cannot charge - no reachable space beside the target.");
    }

    private void CheckUtilities()
    {
        Reset(exploration: true);
        ExpectFailure(() => Call("TryOpenDoorAtCell", _doorCell), "Door locked - use Pick Lock.");
        Reset(exploration: true);
        _actor.SetGridPos(new Vector2I(1, 2));
        RefreshFog();
        ExpectFailure(() => Call("TryOpenDoorAtCell", _doorCell), "Move adjacent to the door.");
        Reset(exploration: true);
        _door["locked"] = false;
        _door["is_open"] = true;
        _ally.SetGridPos(_doorCell);
        RefreshFog();
        ExpectFailure(() => Call("TryOpenDoorAtCell", _doorCell), "Cannot close the door - doorway occupied.");
        Reset(exploration: true);
        ExpectTargetFailure("pick-lock", new Vector2I(1, 3), "Select a locked door.");
        Reset(exploration: true);
        _door["locked"] = false;
        ExpectTargetFailure("pick-lock", _doorCell, "This door is already unlocked.");
        Reset(exploration: true);
        ExpectTargetFailure("disarm-trap", new Vector2I(1, 3), "No trap at that location.");
        Reset(exploration: true);
        Field<HashSet<string>>("_openedPropIds").Add("trap");
        ExpectTargetFailure("disarm-trap", new Vector2I(1, 2), "This trap is already disarmed.");
        Reset(exploration: true);
        ExpectTargetFailure("pick-lock", new Vector2I(5, 3), "Move adjacent to use Pick Lock.");
    }

    private void CheckMovement()
    {
        Reset();
        _actor.TrySpendMovement(_actor.RemainingMovement);
        ExpectFailure(() => _Input(new InputEventKey { Pressed = true, Keycode = Key.W }), "No movement points remaining.");
        Reset();
        _actor.ApplyStatusEffect("pinned", "Pinned", false, 2, preventMovement: true);
        ExpectFailure(() => MoveClick(new Vector2I(2, 1)), "caster cannot move while pinned.");
        Reset();
        Field<HashSet<Vector2I>>("_wallCellSet").Add(new Vector2I(2, 1));
        ExpectFailure(() => MoveClick(new Vector2I(2, 1)), "Path blocked.");
        Reset();
        Field<HashSet<Vector2I>>("_wallCellSet").Add(new Vector2I(2, 1));
        ExpectTargetFailure("melee", new Vector2I(2, 1), "Path blocked.");
        Reset();
        ExpectFailure(() => MoveClick(_ally.GridPos), "That space is occupied.");
        Reset();
        ExpectFailure(() => MoveClick(new Vector2I(7, 1)), "Not enough movement points to reach that location.");
        Reset();
        for (var row = 0; row < 8; row++) Field<HashSet<Vector2I>>("_wallCellSet").Add(new Vector2I(3, row));
        ExpectFailure(() => MoveClick(new Vector2I(4, 4)), "No path to that location.");
        Reset(exploration: true);
        Field<HashSet<Vector2I>>("_wallCellSet").Add(new Vector2I(2, 1));
        ExpectFailure(() => _Input(new InputEventKey { Pressed = true, Keycode = Key.W }), "Path blocked.");
        CheckExplorationKeyEchoQueued();
    }

    private void CheckExplorationKeyEchoQueued()
    {
        Reset();
        Set("_isExplorationAutoMoving", true);
        _Input(new InputEventKey { Keycode = Key.D, Pressed = true, Echo = true });
        Check(!Field<bool>("_hasPendingExplorationManualMove"),
            "Key repeat events must not move the party during combat.");

        Reset(exploration: true);
        Set("_isExplorationAutoMoving", true);
        _Input(new InputEventKey { Keycode = Key.D, Pressed = true, Echo = true });
        Check(Field<bool>("_hasPendingExplorationManualMove")
            && Field<Vector2I>("_pendingExplorationManualDelta") == new Vector2I(1, 0),
            "Holding a movement key must queue repeated exploration steps.");
        Set("_isExplorationAutoMoving", false);
        Set("_hasPendingExplorationManualMove", false);
    }

    private void CheckInteractions()
    {
        CheckNpcDialogueInteractionEntries();
        Reset(exploration: true);
        Field<Array<Dictionary>>("_mapProps").Add(new Dictionary
        {
            { "id", "chest" }, { "name", "Chest" }, { "type", "chest" },
            { "grid_pos", new Vector2I(6, 4) }, { "gold_amount", 1 }
        });
        ExpectFailure(() => Call("TryOpenExplorationInteractionAtCell", new Vector2I(6, 4)),
            "Move adjacent to interact with that object.");
        ExpectFailure(() => Call("TryExecuteExplorationInteractionById", _actor, "prop:chest"),
            "Move adjacent to interact with Chest.");
        Reset(exploration: true);
        Field<Array<Dictionary>>("_lootBags").Add(new Dictionary
        {
            { "id", "empty-bag" }, { "grid_pos", new Vector2I(1, 2) }, { "gold_amount", 0 }
        });
        ExpectFailure(() => Call("TryExecuteExplorationInteractionById", _actor, "bag-all:empty-bag"),
            "This loot bag is empty.");
        Reset(exploration: true);
        ExpectFailure(() => Call("TryExecuteExplorationInteractionById", _actor, "bag-gold:empty-bag"),
            "This loot bag contains no gold.");
        Reset(exploration: true);
        Field<List<string>>("_partyInventoryItemIds").Add("fireball-scroll");
        ExpectFailure(() => Call("OnHudUseItemRequested", "fireball-scroll"), "caster already knows Fireball.");
        Reset(exploration: true);
        _actor.Setup(new Dictionary
        {
            { "id", "caster" }, { "name", "caster" }, { "team", "player" }, { "class_id", "knight" }
        });
        ExpectFailure(() => Call("OnHudUseItemRequested", "fireball-scroll"), "caster cannot use Fireball Scroll - requires wizard.");
        Reset(exploration: true);
        ExpectFailure(() => Call("OnHudUseItemRequested", "dagger"), "That item is no longer available.");
        Reset(exploration: true);
        Field<System.Collections.Generic.Dictionary<string, List<string>>>("_vendorInventoryItemIdsById")["test-vendor"]
            = new List<string> { "dagger" };
        Set("_partyGold", 0);
        var expectedPrice = (int)Call("GetItemBuyPrice", "test-vendor", "dagger");
        ExpectFailure(() => Call("TryBuyVendorItem", "test-vendor", "dagger"),
            $"Not enough gold - requires {expectedPrice} gp, have 0.");
        Reset(exploration: true);
        ExpectFailure(() => Call("TryBuyVendorItem", "test-vendor", "missing"), "That item is out of stock.");
        Reset(exploration: true);
        ExpectFailure(() => Call("TrySellVendorItem", "test-vendor", "missing"), "That item is not available to sell.");
        Reset(exploration: true);
        Field<List<string>>("_partyInventoryItemIds").Add("dagger");
        Field<System.Collections.Generic.Dictionary<string, int>>("_vendorGoldById")["test-vendor"] = 0;
        var vendorName = (string)Call("GetVendorDisplayName", "test-vendor");
        ExpectFailure(() => Call("TrySellVendorItem", "test-vendor", "dagger"), $"{vendorName} does not have enough gold.");
        Reset(exploration: true);
        var itemName = (string)Call("GetItemName", "dagger");
        ExpectFailure(() => Call("OnHudUseItemRequested", "dagger"), $"{itemName} cannot be used.");
        Reset(exploration: true);
        Field<Array<Unit>>("_playerUnits").Remove(_ally);
        ExpectFailure(() => Call("OnHudReserveStoreRequested", _actor.UnitId), "At least one member must remain in the party.");
        Field<Array<Unit>>("_playerUnits").Add(_ally);
        Reset(exploration: true);
        ExpectFailure(() => Call("BeginReserveRecruitInteraction", _actor, "missing", ""),
            "That reserve member is no longer available.");
    }

    private void CheckNpcDialogueInteractionEntries()
    {
        var npc = new Dictionary
        {
            { "id", "gord" }, { "npc_id", "gord" }, { "name", "Gord the Rogue" },
            { "npc_role", "recruit" }, { "recruit_template_id", "thief" }
        };
        var entries = new Array<Dictionary>();
        typeof(MapLoader).GetMethod("AddNpcInteractionEntries", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { entries, npc, "Gord the Rogue" });
        Check(entries.Count == 1 && (string)entries[0]["id"] == "npc-dialogue:gord",
            "Recruitable NPCs must open the shared dialogue instead of a separate recruit menu.");

        var recruitChoices = new Array<Dictionary>();
        typeof(BattleController).GetMethod("AddNpcDialogueActionChoices", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { npc, recruitChoices });
        var gordPages = (Array<Dictionary>)Field<GameData>("_gameData").GetDialogue("gord")["pages"];
        Check(recruitChoices.Count == 1
            && (string)recruitChoices[0]["action"] == "recruit"
            && (string)recruitChoices[0]["label"] == "Recruit Gord the Rogue"
            && gordPages.Count == 1
            && !gordPages[0].ContainsKey("choices"),
            "Gord's welcome dialogue must have only the Recruit Gord the Rogue action.");

        var vendor = new Dictionary
        {
            { "id", "milo" }, { "npc_id", "milo" }, { "name", "Milo the Armorer" },
            { "npc_role", "vendor" }, { "vendor_id", "milo" }
        };
        var vendorChoices = new Array<Dictionary>();
        typeof(BattleController).GetMethod("AddNpcDialogueActionChoices", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { vendor, vendorChoices });
        var miloPages = (Array<Dictionary>)Field<GameData>("_gameData").GetDialogue("milo")["pages"];
        Check(vendorChoices.Count == 1
            && (string)vendorChoices[0]["action"] == "open_vendor"
            && (string)vendorChoices[0]["vendor_id"] == "milo"
            && (string)vendorChoices[0]["label"] == "Open Store"
            && miloPages.Count == 1
            && !string.IsNullOrWhiteSpace(miloPages[0]["text"].AsString())
            && !miloPages[0].ContainsKey("choices"),
            "Milo's welcome dialogue must be nonempty and offer only the Open Store action.");

        _testHud.OpenVendorPanel("Milo the Armorer");
        Check(_testHud.GetNode<TabContainer>("VendorPanel/VendorVBox/StoreTabs").Visible
            && _testHud.GetNodeOrNull<Button>("VendorPanel/VendorVBox/VendorChoiceButtons/TalkButton") == null
            && typeof(BattleController).GetMethod("ConfirmNpcRecruitAsync", BindingFlags.Instance | BindingFlags.NonPublic) == null,
            "Opening the store must show only the store interface without a talk button or recruit confirmation dialog.");
    }

    private void CheckHealerServices()
    {
        Reset(exploration: true);
        Set("_partyGold", 1000);
        var healerProp = new Dictionary
        {
            { "id", "eldius" },
            { "npc_id", "eldius" },
            { "type", "npc" },
            { "name", "Eldius the Healer" },
            { "npc_role", "healer" },
            { "grid_pos", new Vector2I(6, 2) }
        };
        Field<Array<Dictionary>>("_mapProps").Add(healerProp);
        var effectsDirector = new CombatEffectsDirector();
        AddChild(effectsDirector);
        Set("_combatEffectsDirector", effectsDirector);

        var dialogue = Field<GameData>("_gameData").GetDialogue("eldius");
        var pages = (Array<Dictionary>)dialogue["pages"];
        var openingChoices = (Array<Dictionary>)pages[0]["choices"];
        var displayedChoices = (Array<Dictionary>)Call("BuildNpcDialogueChoices", healerProp, openingChoices, true);
        var openingChoiceLabels = displayedChoices.Select(choice => choice["label"].AsString()).ToArray();
        Check(pages.Count == 3
            && pages[0]["text"].AsString().Contains("bandits")
            && pages[0]["text"].AsString().Contains("necromancer")
            && pages[0]["text"].AsString().Contains("goblins"),
            "Eldius must offer Forest Town dialogue about the necromancer, goblins, and bandits.");
        Check(openingChoiceLabels.SequenceEqual(new[]
                {
                    "Tell us about the danger to the north.",
                    "What should we expect to the south?",
                    "Healing Services",
                    "Farewell"
                })
            && displayedChoices[2]["action"].AsString() == "open_healing_services",
            "Eldius's opening dialogue must offer Healing Services third, immediately before Farewell.");

        var gameData = Field<GameData>("_gameData");
        var resurrectServiceReference = (Dictionary)gameData.HealingServices["resurrect"];
        var resurrectSpell = gameData.GetHealingService("resurrect");
        var blessingScroll = gameData.GetItem("blessing-scroll");
        var clericAbilityIds = (Array<string>)gameData.GetCharacterTemplate("cleric")["ability_ids"];
        var wizardAbilityIds = (Array<string>)gameData.GetCharacterTemplate("wizard")["ability_ids"];
        var necromancerAbilityIds = (Array<string>)gameData.GetCharacterTemplate("necromancer")["ability_ids"];
        Check(resurrectServiceReference.Count == 2
            && resurrectServiceReference["spell_id"].AsString() == "resurrect"
            && resurrectSpell["type"].AsString() == "resurrect"
            && (int)resurrectSpell["range"] == -1
            && (int)resurrectSpell["mp_cost"] == 8
            && (int)resurrectSpell["experience_loss_percent"] == 5
            && (int)resurrectSpell["price"] == 150
            && blessingScroll.Count > 0
            && blessingScroll["type"].AsString() == "scroll"
            && ((Dictionary)blessingScroll["use_effect"])["spell_id"].AsString() == "blessing"
            && !gameData.HealingServices.ContainsKey("blessing"),
            "Eldius must offer Blessing as a scroll instead of a healing service.");
        var healingSpellIds = new[] { "remove-malady", "resurrect", "blessing" };
        Check(healingSpellIds.All(spellId => !clericAbilityIds.Contains(spellId)
                && !wizardAbilityIds.Contains(spellId)
                && !necromancerAbilityIds.Contains(spellId)),
            "Healing spells must not be starting abilities for clerics, wizards, or enemy casters.");
        Check(healingSpellIds.All(spellId =>
                ((Array<string>)gameData.GetSpell(spellId)["allowed_classes"]).Contains("cleric")
                && ((Array<string>)gameData.GetSpell(spellId)["allowed_classes"]).Contains("healer")),
            "Healing spell definitions must restrict casting to cleric and healer classes.");
        Check(((Array<string>)blessingScroll["allowed_classes"]).SequenceEqual(new[] { "cleric", "healer" }),
            "The Blessing scroll must only be learnable by clerics and healers.");

        var serviceItems = (Array<Dictionary>)Call("BuildVendorBuyItemsForHud", "eldius");
        Check(serviceItems.Count == 3
            && serviceItems.All(item => item.ContainsKey("price") && item.ContainsKey("icon_id"))
            && serviceItems.Any(item => item["id"].AsString() == "blessing-scroll"
                && (int)item["price"] == 100
                && (int)item["quantity"] == 1)
            && (int)Field<GameData>("_gameData").GetHealingService("resurrect")["price"] == 150,
            "Eldius's shop must expose two icon-backed healing services and one 100 gp Blessing scroll.");
        Call("OpenVendor", "eldius");
        var storeTabs = _testHud.GetNode<TabContainer>("VendorPanel/VendorVBox/StoreTabs");
        Check(storeTabs.Visible
            && storeTabs.GetChild<Control>(1).Visible == false
            && storeTabs.GetTabTitle(0) == "Healer Shop"
            && _testHud.GetNode<Button>("VendorPanel/VendorVBox/StoreTabs/Buy/BuyButton").Text == "Buy / Select",
            "Eldius must reuse the vendor panel for purchases and healing services.");
        _actor.ApplyStatusEffect("poison", "Poisoned", false, 3, damagePerTurn: 1);
        _actor.ApplyStatusEffect("protection", "Protected", true, 2, armorClassBonus: 2);
        Call("BeginHealerServiceTargeting", "remove-malady");
        var effectsBeforeCleansing = effectsDirector.GetChildCount();
        Call("TryCastHealerServiceAtCell", _actor.GridPos);
        Check(!_actor.HasStatusEffect("poison")
            && _actor.HasStatusEffect("protection")
            && (int)Field<int>("_partyGold") == 950
            && effectsDirector.GetChildCount() > effectsBeforeCleansing
            && !Field<bool>("_awaitingHealerServiceTarget"),
            "Remove Malady must cleanse negative effects, play its effect, charge once, and end targeting.");
        var allyConfig = Config("ally", "player", _ally.GridPos);
        allyConfig["experience"] = 50;
        _ally.Setup(allyConfig);
        _ally.ApplyDamage(1000);
        Call("CleanupDefeatedUnits");
        var fallenRoster = (Array<Dictionary>)Call("BuildPartyRosterSnapshot");
        Check(Field<Array<Unit>>("_playerUnits").Contains(_ally)
            && fallenRoster.Any(entry => (string)entry["id"] == _ally.UnitId),
            "Fallen party members must remain visible in the active party and persist in the party roster.");
        Call("SyncHudFromGameState");
        var partyCards = _testHud.GetNode<VBoxContainer>("PartyPanel/PartyVBox/PartyList");
        Check(partyCards.GetChildren().OfType<PartyCard>().Any(card => card.UnitId == _ally.UnitId),
            "Fallen party members must remain visible as cards in the party HUD.");
        Call("OnHudReserveStoreRequested", _ally.UnitId);
        Check(Field<Array<Unit>>("_playerUnits").Contains(_ally)
            && Field<Array<Dictionary>>("_reservePartyRoster").Count == 0,
            "Fallen party members must not disappear into reserves before they are resurrected.");
        _ally.SetGridPos(new Vector2I(0, 10));
        Call("BeginHealerServiceTargeting", "resurrect");
        var resurrectionPartyCards = _testHud.GetNode<VBoxContainer>("PartyPanel/PartyVBox/PartyList")
            .GetChildren().OfType<PartyCard>().ToArray();
        var deadAllyCard = resurrectionPartyCards.Last(card => card.UnitId == _ally.UnitId);
        var livingActorCard = resurrectionPartyCards.Last(card => card.UnitId == _actor.UnitId);
        Check(deadAllyCard.TargetHighlighted
            && !livingActorCard.TargetHighlighted
            && _testHud.GetNode<Label>("CombatBanner/CombatBannerLabel").Text.Contains("Select a highlighted fallen party portrait")
            && _testHud.GetNode<Label>("HelpPanel/HelpVBox/HelpBody").Text.Contains("No range limit"),
            "Resurrection targeting must explain the party-list action in the standard banner and highlight fallen portraits.");
        var resurrectionSourceCell = Field<Vector2I>("_healerServiceSourceCell");
        Check(!Unit.IsWithinRange(resurrectionSourceCell, _ally.GridPos, 5),
            "The resurrection test target must be outside Eldius's normal service range.");
        var effectsBeforeResurrection = effectsDirector.GetChildCount();
        Call("OnHudPartyUnitSelected", _ally.UnitId);
        Check(!_ally.IsDead && _ally.HitPoints == _ally.MaxHitPoints
            && _ally.Experience == 45
            && Field<Array<Unit>>("_playerUnits").Contains(_ally)
            && effectsDirector.GetChildCount() > effectsBeforeResurrection
            && (int)Field<int>("_partyGold") == 800,
            "Selecting the fallen party portrait must resurrect at any distance, play its effect, and deduct XP and the service price.");
        var resurrectedAllyCard = _testHud.GetNode<VBoxContainer>("PartyPanel/PartyVBox/PartyList")
            .GetChildren().OfType<PartyCard>().Last(card => card.UnitId == _ally.UnitId);
        Check(!resurrectedAllyCard.TargetHighlighted,
            "Resurrection target highlighting must clear after a party member is selected.");

        Check((int)Field<int>("_partyGold") == 800
            && ((Array<Dictionary>)Call("BuildVendorBuyItemsForHud", "eldius")).Count == 3,
            "Both remaining healing services and the Blessing scroll must remain listed after service use.");
        Call("OnHudVendorBuyRequested", "blessing-scroll");
        var partyInventory = (List<string>)PrivateField("_partyInventoryItemIds").GetValue(this);
        Check((int)Field<int>("_partyGold") == 700
            && partyInventory.Contains("blessing-scroll")
            && ((Array<Dictionary>)Call("BuildVendorBuyItemsForHud", "eldius")).Count == 2,
            "The Blessing scroll must be purchasable for 100 gp and leave Eldius's finite stock.");
        ExpectFailure(() => Call("OnHudUseItemRequested", "blessing-scroll"),
            "caster cannot use Blessing Scroll - requires cleric, healer.");
        var clericConfig = Config("caster", "player", _actor.GridPos);
        clericConfig["class_id"] = "cleric";
        clericConfig["ability_ids"] = new Array<string> { "melee" };
        _actor.Setup(clericConfig);
        Call("OnHudUseItemRequested", "blessing-scroll");
        Check(_actor.HasAbility("blessing")
            && !partyInventory.Contains("blessing-scroll"),
            "A cleric must learn Blessing from the purchased scroll and consume it.");

        Call("BeginHealerServiceTargeting", "remove-malady");
        Set("_healerServiceSourceCell", new Vector2I(10, 2));
        Call("TryCastHealerServiceAtCell", _actor.GridPos);
        Check(Field<bool>("_awaitingHealerServiceTarget") && (int)Field<int>("_partyGold") == 700,
            "An out-of-range service target must not consume gold or end targeting.");
        Call("CancelHealerServiceTargeting");
    }

    private void CheckDeadPartyDefeat()
    {
        Reset();
        _actor.ApplyDamage(1000);
        _ally.ApplyDamage(1000);
        Call("CleanupDefeatedUnits");
        var combatResolved = (bool)Call("CheckCombatResolved");
        Check(combatResolved
            && Field<Array<Unit>>("_playerUnits").Count == 2
            && PrivateField("_flowState").GetValue(this).ToString() == "Defeat",
            "Keeping fallen members in the party roster must still end combat when no living members remain.");
    }

    private void CheckCombatHealingSpells()
    {
        var healingAbilityIds = new Array<string> { "remove-malady", "resurrect", "blessing" };

        Reset(mana: 20);
        Set("_partyGold", 1000);
        var actorConfig = Config("caster", "player", _actor.GridPos, 20);
        actorConfig["class_id"] = "cleric";
        actorConfig["ability_ids"] = healingAbilityIds;
        actorConfig["primary_ability_id"] = "remove-malady";
        _actor.Setup(actorConfig);
        _ally.ApplyStatusEffect("spell-test-poison", "Poisoned", false, 3, damagePerTurn: 1);
        Call("OnHudAbilityPressed", "remove-malady");
        Check(Field<bool>("_awaitingPlayerAttackDirection"),
            "Selecting a learned Remove Malady spell must enter target mode.");
        Call("OnHudPartyUnitSelected", _ally.UnitId);
        Check(!_ally.HasNegativeStatusEffects()
            && _actor.MagicPoints < 20
            && _actor.RemainingActionPoints == 0
            && (int)Field<int>("_partyGold") == 1000,
            "A learned Remove Malady spell must cleanse allies using MP and an action, without charging gold.");
        Reset(mana: 20);
        Set("_partyGold", 1000);
        actorConfig = Config("caster", "player", _actor.GridPos, 20);
        actorConfig["class_id"] = "cleric";
        actorConfig["ability_ids"] = healingAbilityIds;
        actorConfig["primary_ability_id"] = "resurrect";
        _actor.Setup(actorConfig);
        var allyConfig = Config("ally", "player", _ally.GridPos);
        allyConfig["experience"] = 50;
        _ally.Setup(allyConfig);
        _ally.ApplyDamage(1000);
        Call("CleanupDefeatedUnits");
        _ally.SetGridPos(new Vector2I(0, 10));
        Call("OnHudAbilityPressed", "resurrect");
        Check(Field<bool>("_awaitingPlayerAttackDirection"),
            "Selecting a learned Resurrection spell must enter target mode.");
        var deadAllyCard = _testHud.GetNode<VBoxContainer>("PartyPanel/PartyVBox/PartyList")
            .GetChildren().OfType<PartyCard>().Last(card => card.UnitId == _ally.UnitId);
        Check(deadAllyCard.TargetHighlighted
            && _testHud.GetNode<Label>("CombatBanner/CombatBannerLabel").Text.Contains("Select a highlighted fallen party portrait"),
            "Combat Resurrection must show the same target guidance and highlight fallen party portraits.");
        Call("OnHudPartyUnitSelected", _ally.UnitId);
        Check(!_ally.IsDead
            && _ally.HitPoints == _ally.MaxHitPoints
            && _ally.Experience == 45
            && _actor.MagicPoints < 20
            && _actor.RemainingActionPoints == 0
            && (int)Field<int>("_partyGold") == 1000,
            "A learned Resurrection spell must revive a fallen portrait target at any distance using MP and an action.");
        Reset(mana: 20);
        Set("_partyGold", 1000);
        actorConfig = Config("caster", "player", _actor.GridPos, 20);
        actorConfig["class_id"] = "cleric";
        actorConfig["ability_ids"] = healingAbilityIds;
        actorConfig["primary_ability_id"] = "blessing";
        _actor.Setup(actorConfig);
        _ally.SetGridPos(new Vector2I(2, 4));
        Call("OnHudAbilityPressed", "blessing");
        Check(Field<bool>("_awaitingPlayerAttackDirection"),
            "Selecting a learned Blessing spell must enter target mode.");
        Call("OnHudPartyUnitSelected", _ally.UnitId);
        Check(_actor.HasStatusEffect("blessing")
            && _ally.HasStatusEffect("blessing")
            && _actor.MagicPoints < 20
            && _actor.RemainingActionPoints == 0
            && (int)Field<int>("_partyGold") == 1000,
            "A learned Blessing spell must apply its configured area effect using MP and an action, without charging gold.");
        Reset(mana: 20);
        var ineligibleActorConfig = Config("caster", "player", _actor.GridPos, 20);
        ineligibleActorConfig["ability_ids"] = new Array<string> { "remove-malady" };
        ineligibleActorConfig["primary_ability_id"] = "remove-malady";
        _actor.Setup(ineligibleActorConfig);
        _ally.ApplyStatusEffect("wizard-test-poison", "Poisoned", false, 3, damagePerTurn: 1);
        Call("OnHudAbilityPressed", "remove-malady");
        Check(!Field<bool>("_awaitingPlayerAttackDirection")
            && _ally.HasNegativeStatusEffects()
            && _actor.MagicPoints == 20
            && _actor.RemainingActionPoints > 0,
            "A wizard must not cast a restricted healing spell even if the spell ID is manually assigned.");
        Reset();
        var enemyConfig = Config("enemy", "enemy", _enemy.GridPos, 10);
        enemyConfig["class_id"] = "healer";
        enemyConfig["ability_ids"] = new Array<string> { "remove-malady" };
        enemyConfig["primary_ability_id"] = "remove-malady";
        _enemy.Setup(enemyConfig);
        _enemy.ApplyStatusEffect("ai-test-poison", "Poisoned", false, 3, damagePerTurn: 1);
        var aiDirector = new AiDirector();
        AddChild(aiDirector);
        Set("_aiDirector", aiDirector);
        Call("TryExecuteAiAction", _enemy);
        Check(!_enemy.HasNegativeStatusEffects()
            && _enemy.MagicPoints < 10
            && _enemy.RemainingActionPoints == 0,
            "Enemy AI must cast learned healing spells on eligible allies using its normal MP and action resources.");
    }

    private void CheckConsumables()
    {
        var inventory = Field<List<string>>("_partyInventoryItemIds");
        var equippedByUnit = Field<System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>>("_equippedItemsByUnitId");

        ResetConsumableState(exploration: true);
        inventory.Add("healing-potion");
        _actor.ApplyDamage(10);
        Call("SyncHudFromGameState");
        var equipButton = HudField<Button>("_equipButton");
        Check(!equipButton.Disabled && equipButton.Text == "Equip",
            "A healing potion in shared inventory must be equipable, not directly usable");
        equipButton.EmitSignal(Button.SignalName.Pressed);
        Check(equippedByUnit[_actor.UnitId]["consumable-1"] == "healing-potion",
            "The first healing potion must equip to the first consumable slot");
        var inventorySlot = _testHud.GetNode<Button>("InventoryPanel/InventoryVBox/InventoryColumns/EquipmentColumn/EquipmentColumnVBox/ConsumableSlots/ConsumableSlot1Button");
        Check(inventorySlot.Icon != null, "The equipped potion must appear in the inventory's consumable slot");
        var healingButton = HudField<Button>("_consumableButton1");
        Check(!healingButton.Disabled && healingButton.Icon != null,
            "An equipped healing potion must be enabled on the action bar in exploration");
        healingButton.EmitSignal(Button.SignalName.Pressed);
        Check(_actor.HitPoints == 46 && !inventory.Contains("healing-potion")
            && !equippedByUnit.ContainsKey(_actor.UnitId) && !_actor.HasUsedAbilityThisTurn,
            "Exploration use must restore 6 HP, consume the potion, and remain free of combat action costs");

        ResetConsumableState(exploration: true);
        inventory.Add("healing-potion");
        Call("SyncHudFromGameState");
        HudField<Button>("_equipButton").EmitSignal(Button.SignalName.Pressed);
        _testHud.GetNode<Button>("InventoryPanel/InventoryVBox/InventoryColumns/EquipmentColumn/EquipmentColumnVBox/ConsumableSlots/ConsumableSlot1Button")
            .EmitSignal(Button.SignalName.Pressed);
        HudField<Button>("_unequipButton").EmitSignal(Button.SignalName.Pressed);
        Check(!equippedByUnit.ContainsKey(_actor.UnitId) && inventory.Contains("healing-potion"),
            "Unequipping a consumable must return it to shared inventory");

        ResetConsumableState(exploration: true);
        inventory.Add("healing-potion");
        Call("OnHudEquipItemRequested", "healing-potion");
        healingButton = HudField<Button>("_consumableButton1");
        Check(healingButton.Disabled, "A healing potion must be disabled at maximum HP");
        ExpectFailure(() => Call("OnHudConsumablePressed", "consumable-1"), "caster is already at full health.");
        Check(inventory.Contains("healing-potion")
            && equippedByUnit[_actor.UnitId]["consumable-1"] == "healing-potion",
            "A potion with no restorative effect must remain equipped and unconsumed");

        ResetConsumableState(exploration: true);
        inventory.Add("healing-potion");
        _actor.ApplyDamage(1);
        Call("OnHudEquipItemRequested", "healing-potion");
        HudField<Button>("_consumableButton1").EmitSignal(Button.SignalName.Pressed);
        Check(_actor.HitPoints == _actor.MaxHitPoints,
            "Healing must stop at maximum HP when less than 6 HP is missing");

        ResetConsumableState(exploration: false);
        inventory.Add("magic-potion");
        _actor.TrySpendMagicPoints(4);
        Call("OnHudEquipItemRequested", "magic-potion");
        HudField<Button>("_consumableButton1").EmitSignal(Button.SignalName.Pressed);
        Check(_actor.MagicPoints == _actor.MaxMagicPoints && _actor.HasUsedAbilityThisTurn
            && !inventory.Contains("magic-potion") && !equippedByUnit.ContainsKey(_actor.UnitId),
            "Combat use must restore 4 MP, consume the item, and spend the unit's action");

        ResetConsumableState(exploration: true);
        inventory.Add("magic-potion");
        _actor.TrySpendMagicPoints(2);
        Call("OnHudEquipItemRequested", "magic-potion");
        HudField<Button>("_consumableButton1").EmitSignal(Button.SignalName.Pressed);
        Check(_actor.MagicPoints == _actor.MaxMagicPoints,
            "Magic restoration must stop at maximum MP when less than 4 MP is missing");

        ResetConsumableState(exploration: true);
        inventory.AddRange(new Array<string> { "healing-potion", "magic-potion", "healing-potion" });
        Call("OnHudEquipItemRequested", "healing-potion");
        Call("OnHudEquipItemRequested", "magic-potion");
        ExpectFailure(() => Call("OnHudEquipItemRequested", "healing-potion"),
            "Both consumable slots are full. Unequip a consumable first.");
        Check(equippedByUnit[_actor.UnitId].Count == Unit.DefaultConsumableSlotCount
            && ((Array<Dictionary>)Call("BuildInventoryItemsForHud")).Count == 1,
            "A unit must not equip more than two consumables, and excess stock must remain shared");

        ResetConsumableState(exploration: false);
        inventory.Add("healing-potion");
        _actor.ApplyDamage(1);
        Call("OnHudEquipItemRequested", "healing-potion");
        _actor.MarkAbilityUsed("defend");
        Call("SyncHudFromGameState");
        Check(HudField<Button>("_consumableButton1").Disabled,
            "Combat consumables must be disabled after the unit has spent its action");
        ExpectFailure(() => Call("OnHudConsumablePressed", "consumable-1"),
            "caster cannot use a consumable right now.");
        Check(_actor.HitPoints == _actor.MaxHitPoints - 1 && inventory.Contains("healing-potion"),
            "A spent combat action must not restore resources or consume the equipped item");
    }

    private void ResetConsumableState(bool exploration)
    {
        Reset(exploration: exploration);
        Field<List<string>>("_partyInventoryItemIds").Clear();
        Set("_selectedCharacterUnitId", _actor.UnitId);
        Call("SyncHudFromGameState");
    }

    private void CheckAllowedActions()
    {
        Reset();
        _actor.MarkAbilityUsed("defend");
        Call("OnHudAbilityPressed", "charge");
        Check(Field<bool>("_awaitingPlayerAttackDirection"), "Charge must remain available after the normal action is spent");
        Reset();
        Call("SetSelectedAbilityId", _actor, "fireball");
        var mana = _actor.MagicPoints;
        Call("TryResolvePlayerActionAtCell", _actor, new Vector2I(2, 7));
        Check(_actor.MagicPoints == mana - 10 && _actor.HasUsedAbilityThisTurn,
            "Fireball must still be allowed on empty areas and spend its normal resources");
        Reset();
        var health = _enemy.HitPoints;
        Call("TryResolvePlayerActionAtCell", _actor, _enemy.GridPos);
        Check(_enemy.HitPoints < health && _actor.MagicPoints == 27 && _actor.HasUsedAbilityThisTurn,
            "Valid attacks must still apply damage and consume mana/action");
        Reset();
        Call("SetSelectedAbilityId", _actor, "sleep");
        Call("TryResolvePlayerActionAtCell", _actor, _ally.GridPos);
        Check(_ally.HasStatusEffect("sleep"), "Sleep must still allow affecting allies");
        Reset();
        var logCount = _log.ItemCount;
        Call("CanUseActionProfileNow", _actor, Call("ResolveActionProfile", _actor, "pin"));
        Check(_banner.Text == "" && _log.ItemCount == logCount, "Readiness/AI checks must never display failure banners");
        Call("SetSelectedAbilityId", _actor, "magic-missile");
        Set("_awaitingPlayerAttackDirection", true);
        Call("HandlePlayerAttackDirectionInput", new InputEventKey { Pressed = true, Keycode = Key.Escape });
        Check(_banner.Text == "", "Canceling targeting must not show a failure");
    }

    private void CheckNewHealerSpell()
    {
        Reset();
        var spells = GetNode<GameData>("/root/GameData");
        var greaterHeal = spells.GetSpell("greater-heal");
        Check(ReadInt(greaterHeal, "heal_amount", 0) == 10 && ReadInt(greaterHeal, "cooldown", 0) == 2,
            "Greater Heal must restore 10 HP and have a 2-turn cooldown");
        _actor.AbilityIds.Add("greater-heal");
        Check(((Variant)Call("GetActionFailureReason", _actor, Call("ResolveActionProfile", _actor, "greater-heal"))).AsString()
            .Contains("must be a cleric or healer"),
            "Greater Heal must be rejected for a wizard by its class restriction");

        ConfigureActor("cleric", "greater-heal", "melee");
        Call("SetSelectedAbilityId", _actor, "greater-heal");
        Check(string.IsNullOrEmpty(((Variant)Call("GetActionFailureReason", _actor, Call("ResolveActionProfile", _actor, "greater-heal"))).AsString()),
            "Greater Heal must be available to clerics");
        _ally.ApplyDamage(20);
        var previousHealth = _ally.HitPoints;
        Check((bool)Call("TryHealTarget", _actor, _ally, 10, 3, "greater-heal", "Greater Heal", 2, 4, true),
            "A cleric must be able to cast Greater Heal");
        Check(_ally.HitPoints == previousHealth + 10 && _actor.MagicPoints == 26,
            "Greater Heal must restore 10 HP and spend 4 MP");
    }

    private void CheckAreaSpellEffects()
    {
        Reset();
        _actor.AbilityIds.Add("web");
        _enemy.SetGridPos(new Vector2I(4, 3));
        var webProfile = Call("ResolveActionProfile", _actor, "web");
        Check((bool)Call("TryCastWebAtCell", _actor, new Vector2I(3, 3), webProfile),
            "Web must resolve on an area target cell");
        Check(_actor.HasStatusEffect("webbed") && _ally.HasStatusEffect("webbed") && _enemy.HasStatusEffect("webbed"),
            "Web must root every unit in its 3x3 area, including allies");
        Check(_actor.MagicPoints == 24 && _actor.HasUsedAbilityThisTurn,
            "Web must spend 6 MP and one action point");
        _enemy.ProcessStartOfTurnStatusEffects();
        Check(_enemy.HasStatusEffect("webbed"),
            "A two-turn Web root must remain active through both affected turns");

        Reset();
        _actor.AbilityIds.Add("lightning-bolt");
        _ally.SetGridPos(new Vector2I(4, 2));
        var actorHealth = _actor.HitPoints;
        var allyHealth = _ally.HitPoints;
        var enemyHealth = _enemy.HitPoints;
        Call("SetSelectedAbilityId", _actor, "lightning-bolt");
        Call("TryResolvePlayerActionAtCell", _actor, _enemy.GridPos);
        Check(enemyHealth - _enemy.HitPoints is >= 8 and <= 16
            && allyHealth - _ally.HitPoints is >= 8 and <= 16
            && _actor.HitPoints == actorHealth,
            "Player-targeted Lightning Bolt must roll 8-16 damage independently for every unit in its line, excluding the caster");
        Check(_actor.MagicPoints == 22 && _actor.HasUsedAbilityThisTurn,
            "Lightning Bolt must spend 8 MP and one action point");
        Check(!(bool)Call("IsEightDirectionLine", _actor.GridPos, new Vector2I(4, 3)),
            "Lightning Bolt must reject non-cardinal, non-diagonal target directions");
    }

    private void CheckRainOfArrows()
    {
        Reset();
        ConfigureActor("ranger", "rain-of-arrows", "melee");
        var equipment = new System.Collections.Generic.Dictionary<string, string>
        {
            ["main-hand"] = "short-bow"
        };
        Field<System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>>("_equippedItemsByUnitId")[_actor.UnitId] = equipment;
        Call("ApplyEquippedItemBonuses", _actor);
        _ally.SetGridPos(new Vector2I(6, 3));
        var enemyHealth = _enemy.HitPoints;
        var allyHealth = _ally.HitPoints;
        Call("TryResolvePlayerActionAtCell", _actor, _enemy.GridPos);
        Check(_actor.GetAbilityCooldownRemaining("rain-of-arrows") == 3 && _actor.HasUsedAbilityThisTurn,
            "Rain of Arrows must resolve through player targeting");
        Check(_enemy.HitPoints < enemyHealth && _ally.HitPoints < allyHealth,
            "Rain of Arrows must damage all units, including allies, within its 2-tile radius");
    }

    private void CheckShadowStrike()
    {
        Reset();
        ConfigureActor("thief", "shadow-strike", "melee");
        var destination = new Vector2I(4, 3);
        var enemyHealth = _enemy.HitPoints;
        var profile = Call("ResolveActionProfile", _actor, "shadow-strike");
        var firstPrompt = ((Variant)Call("BuildActionTargetingHelpText", profile, 1)).AsString();
        var secondPrompt = ((Variant)Call("BuildActionTargetingHelpText", profile, 2)).AsString();
        Check(firstPrompt.Contains("unoccupied reachable tile")
            && secondPrompt.Contains("adjacent enemy"),
            "Shadow Strike must present distinct data-driven instructions for both targeting steps");
        Set("_awaitingPlayerAttackDirection", true);
        Check((bool)Call("TryUseShadowStrikeAtCell", _actor, destination, profile)
            && Field<bool>("_awaitingShadowStrikeAttack")
            && _actor.GridPos == destination,
            "Shadow Strike must first teleport to the selected reachable tile");
        Check((bool)Call("TryUseShadowStrikeAtCell", _actor, _enemy.GridPos, profile),
            "Shadow Strike must then accept an adjacent enemy target");
        Check(enemyHealth - _enemy.HitPoints == _actor.AttackDamage * 4,
            "Shadow Strike must deal 4x weapon damage after teleporting");
        Check(_actor.HasUsedAbilityThisTurn && !Field<bool>("_awaitingShadowStrikeAttack"),
            "Completing Shadow Strike must spend the action and leave targeting mode");
    }

    private void CheckTauntTargeting()
    {
        Reset();
        ConfigureActor("warrior", "taunt", "melee");
        _enemy.SetGridPos(new Vector2I(4, 2));
        var profile = Call("ResolveActionProfile", _actor, "taunt");
        Check((bool)Call("TryUseTaunt", _actor, profile),
            "Taunt must resolve as an immediate area action");
        Check(_enemy.HasStatusEffect("taunted") && _enemy.TauntTargetUnitId == _actor.UnitId,
            "Taunt must mark nearby enemies with the warrior as their forced target");
        var option = new AiDirector.ActionOption("magic-missile", "attack", 5, true, false);
        Check((bool)Call("CanAiActionTarget", _enemy, _actor, option)
            && !(bool)Call("CanAiActionTarget", _enemy, _ally, option),
            "A taunted enemy may target the warrior but must reject every other party member");
        var targets = (Array<Unit>)Call("BuildAiTargetCandidates", _enemy);
        Check(targets.Count == 1 && targets[0] == _actor,
            "Taunted enemy pathfinding must only pursue the warrior");
        _enemy.ProcessStartOfTurnStatusEffects();
        Check(_enemy.HasStatusEffect("taunted"), "Taunt must still affect the enemy's next turn");
        _enemy.ProcessStartOfTurnStatusEffects();
        Check(!_enemy.HasStatusEffect("taunted") && string.IsNullOrEmpty(_enemy.TauntTargetUnitId),
            "Taunt must expire after the enemy's next turn");
    }

    private void CheckIdentifyInventoryFlow()
    {
        Reset();
        const string testItemId = "feedback-unidentified-test-item";
        var gameData = GetNode<GameData>("/root/GameData");
        Field<List<string>>("_partyInventoryItemIds").Clear();
        Field<HashSet<string>>("_identifiedItemIds").Clear();
        gameData.Items[testItemId] = new Dictionary
        {
            { "id", testItemId },
            { "name", "Test Relic" },
            { "type", "weapon" },
            { "range", 3 },
            { "requires_identification", true }
        };
        try
        {
            _actor.AbilityIds.Add("identify");
            Field<List<string>>("_partyInventoryItemIds").Add(testItemId);
            var inventory = (Array<Dictionary>)Call("BuildInventoryItemsForHud");
            var unknown = inventory[inventory.Count - 1];
            Check(!ReadBool(unknown, "is_identified", true)
                && ReadString(unknown, "name", "") == "Unidentified item"
                && ReadString(unknown, "type", "") == "unknown",
                "Unidentified items must hide their properties in the inventory");
            Call("OnHudEquipItemRequested", testItemId);
            Check(!Field<System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>>("_equippedItemsByUnitId")
                .ContainsKey(_actor.UnitId),
                "Unidentified items must not be usable by equipping them");

            Call("OnHudAbilityPressed", "identify");
            Check(Field<bool>("_awaitingItemIdentifyTarget") && Field<bool>("_awaitingPlayerAttackDirection"),
                "Identify must enter inventory-item targeting mode");
            Call("OnHudInventoryItemSelected", testItemId);
            Check(Field<HashSet<string>>("_identifiedItemIds").Contains(testItemId)
                && _actor.MagicPoints == 27
                && _actor.HasUsedAbilityThisTurn,
                "Identify must persist the item state and spend 3 MP and one combat action point");
            var identifiedItem = ((Array<Dictionary>)Call("BuildInventoryItemsForHud"))[0];
            Check(ReadBool(identifiedItem, "is_identified", false)
                && ReadString(identifiedItem, "name", "") == "Test Relic"
                && ReadString(identifiedItem, "type", "") == "weapon",
                "An identified item must reveal its properties and become usable");
            var buildSaveData = typeof(GamePersistence).GetMethod("BuildSaveData", BindingFlags.Instance | BindingFlags.NonPublic);
            var saveData = (Dictionary)buildSaveData.Invoke(Field<GamePersistence>("_persistence"), null);
            var savedItems = (Godot.Collections.Array)(Variant)saveData["identified_item_ids"];
            Check(savedItems.Count == 1 && ((Variant)savedItems[0]).AsString() == testItemId,
                "Identified item IDs must be included in save data");
            Call("OnHudEquipItemRequested", testItemId);
            var equipped = Field<System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>>("_equippedItemsByUnitId");
            Check(equipped.TryGetValue(_actor.UnitId, out var equippedSlots)
                && equippedSlots.ContainsValue(testItemId),
                "An identified weapon must be equipable");
        }
        finally
        {
            gameData.Items.Remove(testItemId);
            Field<List<string>>("_partyInventoryItemIds").Remove(testItemId);
            Field<HashSet<string>>("_identifiedItemIds").Clear();
        }
    }

    private void ConfigureActor(string classId, params string[] abilityIds)
    {
        var config = Config("caster", "player", new Vector2I(2, 2));
        config["class_id"] = classId;
        var abilities = new Array<string>();
        foreach (var abilityId in abilityIds)
        {
            abilities.Add(abilityId);
        }
        config["ability_ids"] = abilities;
        _actor.Setup(config);
        Call("SetSelectedAbilityId", _actor, abilityIds[0]);
    }

    private static int ReadInt(Dictionary data, string key, int fallback)
    {
        return data.ContainsKey(key) ? ((Variant)data[key]).AsInt32() : fallback;
    }

    private static bool ReadBool(Dictionary data, string key, bool fallback)
    {
        return data.ContainsKey(key) ? ((Variant)data[key]).AsBool() : fallback;
    }

    private static string ReadString(Dictionary data, string key, string fallback)
    {
        return data.ContainsKey(key) ? ((Variant)data[key]).AsString() : fallback;
    }

    public void StartQueueChecks()
    {
        ResetBanner();
        _testHud.ShowActionFailureBanner("Out of range.");
        var tween = HudField<Tween>("_combatBannerTween");
        for (var i = 0; i < 10; i++) _testHud.ShowActionFailureBanner("Out of range.");
        Check(ReferenceEquals(tween, HudField<Tween>("_combatBannerTween"))
            && HudField<Queue<(string Text, Color Accent)>>("_combatBannerQueue").Count == 0,
            "Identical warnings must not restart the tween or build a queue");
        ResetBanner();
        _testHud.ShowCombatBanner("COMBAT START", Colors.Orange);
        _testHud.ShowCombatBanner("ZONE", Colors.Green);
        for (var i = 0; i < 10; i++) _testHud.ShowActionFailureBanner("Out of range.");
        Check(HudField<Queue<(string Text, Color Accent)>>("_combatBannerQueue").Count == 1
            && HudField<string>("_pendingActionFailureText") == "Out of range.",
            "Failures behind an existing banner must occupy only one pending slot");
    }

    public void CheckPendingBanner()
    {
        Check(_banner.Text == "Out of range." && HudField<bool>("_activeBannerIsFailure"),
            "Pending failure must display after the current normal banner");
        var color = _banner.GetThemeColor("font_color");
        Check(color.R > color.G && color.R > color.B, "Failure banner text must be red");
    }

    public void CheckPreservedBanner()
    {
        Check(_banner.Text == "ZONE", "Normal queued banners must survive failure feedback");
    }

    public Array<string> GetFailures() => _failures;

    public void ShowLongFailure()
    {
        ResetBanner();
        _testHud.ShowActionFailureBanner("Not enough mana to cast Magic Missile - requires 3 MP, have 2.");
    }

    public void CheckBannerLayout()
    {
        var panel = _testHud.GetNode<PanelContainer>("CombatBanner");
        Check(_banner.GetLineCount() > 1 && _banner.GetVisibleLineCount() == _banner.GetLineCount(),
            "Long failure messages must wrap without hiding any lines");
        Check(panel.Size.X <= _testHud.Size.X && _banner.Size.Y <= panel.Size.Y,
            "Failure banners must stay within the HUD and fit their text");
    }

    private void ExpectTargetFailure(string ability, Vector2I cell, string message)
    {
        Call("SetSelectedAbilityId", _actor, ability);
        Set("_awaitingPlayerAttackDirection", true);
        ExpectFailure(() => Call("HandleMouseAttackInput", new InputEventMouseButton
        {
            Pressed = true, ButtonIndex = MouseButton.Left,
            GlobalPosition = ToGlobal(new Vector2(cell.X * 64 + 32, cell.Y * 64 + 32))
        }), message);
        Check(Field<bool>("_awaitingPlayerAttackDirection"), "Rejected target must remain in targeting mode");
    }

    private void ExpectFailure(Action attempt, string message)
    {
        ResetBanner();
        var logCount = _log.ItemCount;
        var mana = _actor.MagicPoints;
        var position = _actor.GridPos;
        var movement = _actor.RemainingMovement;
        var actionSpent = _actor.HasUsedAbilityThisTurn;
        attempt();
        Check(_banner.Text == message, $"Expected '{message}', got '{_banner.Text}'");
        Check(_log.ItemCount == logCount, "Rejected attempts must not add combat-log entries");
        Check(_actor.MagicPoints == mana && _actor.GridPos == position
            && _actor.RemainingMovement == movement && _actor.HasUsedAbilityThisTurn == actionSpent,
            "Rejected attempts must not change unit resources or position");
        var color = _banner.GetThemeColor("font_color");
        Check(color.R > color.G && color.R > color.B, "Rejected attempts must use red banner text");
    }

    private void MoveClick(Vector2I cell) => Call("HandleMouseMoveInput", new InputEventMouseButton
    {
        ButtonIndex = MouseButton.Left, Pressed = false,
        GlobalPosition = ToGlobal(new Vector2(cell.X * 64 + 32, cell.Y * 64 + 32))
    });

    private Button AbilityButtonAfterSync(string id)
    {
        Call("SyncHudFromGameState");
        return AbilityButton(id);
    }

    private Button AbilityButton(string id)
    {
        foreach (var pair in HudField<System.Collections.Generic.Dictionary<Button, string>>("_abilityIdsByButton"))
            if (pair.Value == id) return pair.Key;
        throw new InvalidOperationException($"Missing {id} button");
    }

    private void RefreshFog()
    {
        Set("_fogVisibilityActor", null);
        Call("UpdateFogOfWar");
    }

    private void ResetBanner()
    {
        HudField<Tween>("_combatBannerTween")?.Kill();
        HudPrivateField("_combatBannerTween").SetValue(_testHud, null);
        HudPrivateField("_activeBannerIsFailure").SetValue(_testHud, false);
        HudPrivateField("_pendingActionFailureText").SetValue(_testHud, "");
        HudField<Queue<(string Text, Color Accent)>>("_combatBannerQueue").Clear();
        _banner.Text = "";
    }

    public void Cleanup()
    {
        _testLoader.Free();
        var testSavePath = ProjectSettings.GlobalizePath("user://action-feedback-consumable-test.json");
        if (System.IO.File.Exists(testSavePath))
        {
            System.IO.File.Delete(testSavePath);
        }
    }
    private void Check(bool condition, string message) { if (!condition) _failures.Add(message); }
    private static FieldInfo PrivateField(string name) => typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static FieldInfo HudPrivateField(string name) => typeof(HudController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private T HudField<T>(string name) => (T)HudPrivateField(name).GetValue(_testHud);
    private T Field<T>(string name) => (T)PrivateField(name).GetValue(this);
    private void Set(string name, object value) => PrivateField(name).SetValue(this, value);
    private object Call(string name, params object[] args) => typeof(BattleController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this, args);
}
