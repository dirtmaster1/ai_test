using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Reflection;

public partial class InventoryDragDropChecks : Node
{
    private readonly Array<string> _failures = new();

    public Array<string> Run()
    {
        var hudScene = GD.Load<PackedScene>("res://ui/HUD.tscn");
        if (hudScene == null)
        {
            _failures.Add("HUD scene must load for inventory drag/drop checks.");
            return _failures;
        }

        var hud = hudScene.Instantiate<HudController>();
        AddChild(hud);

        var summaryUnit = new Unit();
        summaryUnit.Setup(new Dictionary { { "id", "summary-test-unit" }, { "team", "player" } });
        var characterSummary = hud.BuildCharacterSummary(summaryUnit, "", "", includeActionNames: false);
        var classIndex = characterSummary.IndexOf("Class:");
        var levelIndex = characterSummary.IndexOf("Level:");
        var experienceIndex = characterSummary.IndexOf("Experience:");
        var raceIndex = characterSummary.IndexOf("Race:");
        Check(classIndex >= 0 && classIndex < levelIndex && levelIndex < experienceIndex && experienceIndex < raceIndex,
            "Party info must list Level and Experience between Class and Race.");
        Check(!characterSummary.Contains("Team:")
            && !characterSummary.Contains("Status:")
            && !characterSummary.Contains("to next level"),
            "Party info must omit Team, Status, and the experience remaining suffix.");

        var characterTabs = hud.GetNode<TabContainer>("InventoryPanel/InventoryVBox/InventoryColumns/CharacterColumn/CharacterColumnVBox/CharacterTabs");
        Check(characterTabs.CurrentTab == 0, "The Info tab must be selected by default.");
        Check(characterTabs.GetTabTitle(0) == "Info" && characterTabs.GetTabTitle(1) == "Abilities & Spells",
            "The character record tabs must be Info and Abilities & Spells.");
        hud.SetInventoryAbilities(new Array<Dictionary>
        {
            new()
            {
                { "id", "fireball" },
                { "label", "Fireball" },
                { "detail", "Fireball\nType: area_attack\nRange: 6\nRequirement: none\nStatus: ready" }
            }
        });
        var abilityList = hud.GetNode<ItemList>("InventoryPanel/InventoryVBox/InventoryColumns/CharacterColumn/CharacterColumnVBox/CharacterTabs/Abilities/InventoryAbilityList");
        var abilityTooltip = abilityList.GetItemTooltip(0);
        Check(abilityTooltip.Contains("Range: 6")
            && !abilityTooltip.Contains("Type:")
            && !abilityTooltip.Contains("Requirement:")
            && !abilityTooltip.Contains("Status:"),
            "Abilities and spells tooltips must omit Type, Requirement, and Status.");

        var items = new Array<Dictionary>
        {
            new() { { "id", "test-helmet" }, { "name", "Test Helmet" }, { "type", "armor" }, { "slot", "head" } },
            new() { { "id", "test-armor" }, { "name", "Test Armor" }, { "type", "armor" }, { "slot", "body" } },
            new() { { "id", "test-boots" }, { "name", "Test Boots" }, { "type", "armor" }, { "slot", "feet" } },
            new() { { "id", "test-dagger" }, { "name", "Test Dagger" }, { "type", "weapon" }, { "slot", "1-handed" } },
            new() { { "id", "test-bow" }, { "name", "Test Bow" }, { "type", "weapon" }, { "slot", "2-handed" } },
            new() { { "id", "test-potion" }, { "name", "Test Potion" }, { "type", "potion" }, { "slot", "consumable" } },
            new() { { "id", "test-magic-potion" }, { "name", "Test Magic Potion" }, { "type", "potion" }, { "slot", "consumable" } },
            new() { { "id", "test-key" }, { "name", "Test Key" }, { "type", "key" }, { "slot", "none" } }
        };
        hud.SetInventoryItems(items, new Array<string>());

        var head = hud.GetNode<EquipmentSlotButton>("InventoryPanel/InventoryVBox/InventoryColumns/EquipmentColumn/EquipmentColumnVBox/DollStage/DollControl/HeadSlot");
        var body = hud.GetNode<EquipmentSlotButton>("InventoryPanel/InventoryVBox/InventoryColumns/EquipmentColumn/EquipmentColumnVBox/DollStage/DollControl/BodySlot");
        var feet = hud.GetNode<EquipmentSlotButton>("InventoryPanel/InventoryVBox/InventoryColumns/EquipmentColumn/EquipmentColumnVBox/DollStage/DollControl/FeetSlot");
        var mainHand = hud.GetNode<EquipmentSlotButton>("InventoryPanel/InventoryVBox/InventoryColumns/EquipmentColumn/EquipmentColumnVBox/DollStage/DollControl/MainHandSlot");
        var offHand = hud.GetNode<EquipmentSlotButton>("InventoryPanel/InventoryVBox/InventoryColumns/EquipmentColumn/EquipmentColumnVBox/DollStage/DollControl/OffHandSlot");
        var consumable = hud.GetNode<EquipmentSlotButton>("InventoryPanel/InventoryVBox/InventoryColumns/EquipmentColumn/EquipmentColumnVBox/ConsumableSlots/ConsumableSlot1Button");
        var consumable2 = hud.GetNode<EquipmentSlotButton>("InventoryPanel/InventoryVBox/InventoryColumns/EquipmentColumn/EquipmentColumnVBox/ConsumableSlots/ConsumableSlot2Button");
        var inventory = hud.GetNode<SharedPartyInventoryList>("InventoryPanel/InventoryVBox/InventoryColumns/InventoryColumn/InventoryColumnVBox/InventoryItemList");

        Variant helmet = "inventory-item:test-helmet";
        Variant dagger = "inventory-item:test-dagger";
        Variant bow = "inventory-item:test-bow";
        Variant potion = "inventory-item:test-potion";
        Variant key = "inventory-item:test-key";

        Check(head._CanDropData(Vector2.Zero, helmet), "Head armor must be accepted by the head slot.");
        Check(!body._CanDropData(Vector2.Zero, helmet), "Head armor must be rejected by the body slot.");
        Check(body._CanDropData(Vector2.Zero, (Variant)"inventory-item:test-armor"), "Body armor must be accepted by the body slot.");
        Check(feet._CanDropData(Vector2.Zero, (Variant)"inventory-item:test-boots"), "Footwear armor must be accepted by the feet slot.");
        Check(!feet._CanDropData(Vector2.Zero, (Variant)"inventory-item:test-armor"), "Body armor must be rejected by the feet slot.");
        Check(mainHand._CanDropData(Vector2.Zero, dagger) && offHand._CanDropData(Vector2.Zero, dagger), "One-handed weapons must be accepted by either hand.");
        Check(mainHand._CanDropData(Vector2.Zero, bow) && !offHand._CanDropData(Vector2.Zero, bow), "Two-handed weapons must only be accepted by the main-hand slot.");
        Check(consumable._CanDropData(Vector2.Zero, potion), "Consumables must be accepted by consumable slots.");
        Check(!head._CanDropData(Vector2.Zero, potion) && !consumable._CanDropData(Vector2.Zero, key), "Items incompatible with a slot must be rejected.");

        var equipRequested = false;
        hud.EquipItemToSlotRequested += (itemId, slotKey) =>
        {
            equipRequested = itemId == "test-dagger" && slotKey == "1-handed-b";
        };
        offHand._DropData(Vector2.Zero, dagger);
        Check(equipRequested, "Dropping a valid inventory item must request equipping it in the target slot.");
        var footwearEquipRequested = false;
        hud.EquipItemToSlotRequested += (itemId, slotKey) =>
        {
            footwearEquipRequested = itemId == "test-boots" && slotKey == "feet";
        };
        feet._DropData(Vector2.Zero, (Variant)"inventory-item:test-boots");
        Check(footwearEquipRequested, "Dropping footwear into the feet slot must request equipping it.");

        var unequipRequested = false;
        hud.UnequipItemRequested += slotKey => unequipRequested = slotKey == "head";
        hud.SetInventoryEquippedItems(new Array<Dictionary>
        {
            new() { { "id", "test-helmet" }, { "name", "Test Helmet" }, { "type", "armor" }, { "slot", "head" }, { "slot_key", "head" } }
        });
        Variant equippedHelmet = "equipped-slot:head";
        Check(inventory._CanDropData(Vector2.Zero, equippedHelmet), "Equipped items must be droppable into shared inventory.");
        Check(!inventory._CanDropData(Vector2.Zero, helmet), "Shared inventory must reject its own item payload.");
        inventory._DropData(Vector2.Zero, equippedHelmet);
        Check(unequipRequested, "Dropping an equipped item into shared inventory must request unequipping it.");

        var movedFrom = "";
        var movedTo = "";
        hud.MoveEquippedItemToSlotRequested += (sourceSlotKey, targetSlotKey) =>
        {
            movedFrom = sourceSlotKey;
            movedTo = targetSlotKey;
        };
        Variant equippedPotion = "equipped-slot:consumable-1";
        hud.SetInventoryEquippedItems(new Array<Dictionary>
        {
            new() { { "id", "test-potion" }, { "name", "Test Potion" }, { "type", "potion" }, { "slot", "consumable" }, { "slot_key", "consumable-1" } }
        });
        Check(consumable2._CanDropData(Vector2.Zero, equippedPotion), "An equipped potion must be droppable into the other empty consumable slot.");
        consumable2._DropData(Vector2.Zero, equippedPotion);
        Check(movedFrom == "consumable-1" && movedTo == "consumable-2", "Moving a potion between consumable slots must request a slot move.");

        movedFrom = "";
        movedTo = "";
        hud.SetInventoryEquippedItems(new Array<Dictionary>
        {
            new() { { "id", "test-potion" }, { "name", "Test Potion" }, { "type", "potion" }, { "slot", "consumable" }, { "slot_key", "consumable-1" } },
            new() { { "id", "test-magic-potion" }, { "name", "Test Magic Potion" }, { "type", "potion" }, { "slot", "consumable" }, { "slot_key", "consumable-2" } }
        });
        Check(consumable2._CanDropData(Vector2.Zero, equippedPotion), "An occupied consumable slot must accept a potion from the other consumable slot.");
        consumable2._DropData(Vector2.Zero, equippedPotion);
        Check(movedFrom == "consumable-1" && movedTo == "consumable-2", "Dropping onto an occupied consumable slot must request a swap.");

        CheckBodyArmorRoundTrip();
        CheckPartyInventoryIgnoresEnemyEquipment();
        CheckCombatStatusCleanup();

        hud.QueueFree();
        return _failures;
    }

    private void CheckBodyArmorRoundTrip()
    {
        var controller = new BattleController();
        var inventory = (List<string>)typeof(BattleController)
            .GetField("_partyInventoryItemIds", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(controller);
        var equippedByUnitId = (System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>)typeof(BattleController)
            .GetField("_equippedItemsByUnitId", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(controller);
        var unit = new Unit();
        unit.Setup(new Dictionary { { "id", "drag-drop-test-unit" }, { "team", "player" } });
        ((Array<Unit>)typeof(BattleController)
            .GetField("_playerUnits", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(controller)).Add(unit);
        var slots = new System.Collections.Generic.Dictionary<string, string>
        {
            { "2-handed", "test-bow" }
        };
        equippedByUnitId[unit.UnitId] = slots;
        var armor = new Dictionary
        {
            { "id", "test-armor" }, { "name", "Test Armor" }, { "type", "armor" }, { "slot", "body" }
        };
        var helmet = new Dictionary
        {
            { "id", "test-helmet" }, { "name", "Test Helmet" }, { "type", "armor" }, { "slot", "head" }
        };
        var equip = typeof(BattleController).GetMethod("EquipItemToSpecificSlot", BindingFlags.Instance | BindingFlags.NonPublic);
        var unequip = typeof(BattleController).GetMethod("UnequipSlotForUnit", BindingFlags.Instance | BindingFlags.NonPublic);
        var ensureInventory = typeof(BattleController).GetMethod("EnsureSharedInventoryHasUnequippedCount", BindingFlags.Instance | BindingFlags.NonPublic);
        var hasAvailable = typeof(BattleController).GetMethod("HasUnequippedSharedItem", BindingFlags.Instance | BindingFlags.NonPublic);

        Check((bool)equip.Invoke(controller, new object[] { unit, armor, "test-armor", "body" }), "Body armor must equip into its body slot.");
        Check(slots.TryGetValue("2-handed", out var bodyEquipTwoHanded) && bodyEquipTwoHanded == "test-bow",
            "Equipping body armor must preserve the unit's two-handed weapon.");
        Check((bool)equip.Invoke(controller, new object[] { unit, helmet, "test-helmet", "head" }), "Head armor must equip into its head slot.");
        Check(slots.TryGetValue("2-handed", out var headEquipTwoHanded) && headEquipTwoHanded == "test-bow",
            "Equipping a helmet must preserve the unit's two-handed weapon.");
        Check(!(bool)hasAvailable.Invoke(controller, new object[] { "test-armor" }), "Equipped armor must not be counted as unequipped inventory.");
        Check((bool)unequip.Invoke(controller, new object[] { unit, "body" }), "Equipped body armor must unequip from its body slot.");
        ensureInventory.Invoke(controller, new object[] { "test-armor", 1 });
        Check(inventory.Count == 1 && (bool)hasAvailable.Invoke(controller, new object[] { "test-armor" }), "Unequipped body armor must be available to equip again.");
        Check((bool)equip.Invoke(controller, new object[] { unit, armor, "test-armor", "body" }), "Body armor must re-equip after returning to shared inventory.");
        Check(!(bool)hasAvailable.Invoke(controller, new object[] { "test-armor" }), "Re-equipped body armor must no longer be counted as shared inventory.");

        slots["consumable-1"] = "test-potion";
        slots["consumable-2"] = "test-magic-potion";
        var moveSlots = typeof(BattleController).GetMethod("MoveEquippedItemBetweenSlots", BindingFlags.Static | BindingFlags.NonPublic);
        Check((bool)moveSlots.Invoke(null, new object[] { slots, "consumable-1", "consumable-2" }), "Equipped potions must be movable between consumable slots.");
        Check(slots["consumable-1"] == "test-magic-potion" && slots["consumable-2"] == "test-potion", "Moving a potion onto an occupied consumable slot must swap their contents.");

        unit.Free();
        controller.Free();
    }

    private void CheckPartyInventoryIgnoresEnemyEquipment()
    {
        var controller = new BattleController();
        var partyInventory = (List<string>)typeof(BattleController)
            .GetField("_partyInventoryItemIds", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(controller);
        var equippedByUnitId = (System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>)typeof(BattleController)
            .GetField("_equippedItemsByUnitId", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(controller);
        var playerUnits = (Array<Unit>)typeof(BattleController)
            .GetField("_playerUnits", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(controller);
        var player = new Unit();
        player.Setup(new Dictionary { { "id", "inventory-test-player" }, { "team", "player" } });
        playerUnits.Add(player);
        equippedByUnitId[player.UnitId] = new System.Collections.Generic.Dictionary<string, string>
        {
            { "1-handed-a", "short-sword" },
            { "1-handed-b", "small-shield" }
        };

        var enemy = new Unit();
        enemy.Setup(new Dictionary { { "id", "inventory-test-enemy" }, { "team", "enemy" } });
        equippedByUnitId[enemy.UnitId] = new System.Collections.Generic.Dictionary<string, string>
        {
            { "1-handed-a", "short-sword" },
            { "1-handed-b", "small-shield" }
        };

        partyInventory.Add("short-sword");
        partyInventory.Add("short-sword");
        partyInventory.Add("small-shield");
        partyInventory.Add("small-shield");

        var gameData = new GameData();
        gameData.LoadData();
        typeof(BattleController)
            .GetField("_gameData", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(controller, gameData);

        var buildInventoryItems = typeof(BattleController).GetMethod("BuildInventoryItemsForHud", BindingFlags.Instance | BindingFlags.NonPublic);
        var visibleItems = (Array<Dictionary>)buildInventoryItems.Invoke(controller, null);
        var shortSwordCount = 0;
        var smallShieldCount = 0;
        foreach (var item in visibleItems)
        {
            switch (((Variant)item["id"]).AsString())
            {
                case "short-sword":
                    shortSwordCount++;
                    break;
                case "small-shield":
                    smallShieldCount++;
                    break;
            }
        }

        var hasAvailable = typeof(BattleController).GetMethod("HasUnequippedSharedItem", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(shortSwordCount == 1 && smallShieldCount == 1,
            "Looted sword and shield copies must remain visible even when enemies equip matching items.");
        Check((bool)hasAvailable.Invoke(controller, new object[] { "short-sword" })
            && (bool)hasAvailable.Invoke(controller, new object[] { "small-shield" }),
            "Looted sword and shield copies must remain available to equip despite enemy equipment.");

        enemy.Free();
        player.Free();
        gameData.Free();
        controller.Free();
    }

    private void CheckCombatStatusCleanup()
    {
        var controller = new BattleController();
        var allUnits = (Array<Unit>)typeof(BattleController)
            .GetField("_allUnits", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(controller);
        var partyMember = new Unit();
        partyMember.Setup(new Dictionary { { "id", "status-test-party" }, { "team", "player" } });
        partyMember.ApplyStatusEffect("combat-buff", "Protected", true, 3, scope: "combat_only");
        partyMember.ApplyStatusEffect("persistent-buff", "Blessed", true, 3, scope: "persistent");
        var partyAlly = new Unit();
        partyAlly.Setup(new Dictionary { { "id", "status-test-ally" }, { "team", "player" } });
        partyAlly.ApplyStatusEffect("combat-debuff", "Poisoned", false, 3, scope: "combat_only");
        var enemy = new Unit();
        enemy.Setup(new Dictionary { { "id", "status-test-enemy" }, { "team", "enemy" } });
        enemy.ApplyStatusEffect("enemy-combat-buff", "Protected", true, 3, scope: "combat_only");
        allUnits.AddRange(new Array<Unit> { partyMember, partyAlly, enemy });

        typeof(BattleController)
            .GetMethod("ClearCombatOnlyStatusEffectsForParty", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(controller, null);

        Check(!partyMember.HasStatusEffect("combat-buff") && !partyAlly.HasStatusEffect("combat-debuff"),
            "Combat cleanup must clear combat-only buffs and debuffs from all party units.");
        Check(partyMember.HasStatusEffect("persistent-buff"),
            "Combat cleanup must preserve persistent party status effects.");
        Check(enemy.HasStatusEffect("enemy-combat-buff"),
            "Combat cleanup must leave enemy status effects untouched.");

        enemy.Free();
        partyAlly.Free();
        partyMember.Free();
        controller.Free();
    }

    private void Check(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }
}
