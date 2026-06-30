using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class MapLoader : Node
{
    private GameData _gameData;
    private Array<Dictionary> _defaultParty = new();

    public override void _Ready()
    {
        _gameData = GetNodeOrNull<GameData>("/root/GameData");
        _defaultParty = BuildDefaultParty();
    }

    public Dictionary LoadMapStub(string mapId = "map-a")
    {
        if (_defaultParty.Count == 0)
        {
            _defaultParty = BuildDefaultParty();
        }

        return mapId switch
        {
            "map-b" => BuildMapB(_defaultParty),
            _ => BuildMapA(_defaultParty)
        };
    }

    private static Dictionary BuildMapA(Array<Dictionary> defaultParty)
    {
        var players = UpdatePartyGridPosition(defaultParty, new Vector2I(2, 7));

        var encounterA = new Dictionary
        {
            { "id", "encounter-a" },
            { "aggro_range", 4 },
            {
                "enemies",
                new Array<Dictionary>
                {
                    new Dictionary { { "id", "goblin-warrior-a" }, { "name", "Goblin" }, { "team", "enemy" }, { "grid_pos", new Vector2I(9, 6) }, { "primary_ability_id", "melee" }, { "initiative", 9 }, { "hit_points", 8 }, { "max_hit_points", 8 } },
                    new Dictionary { { "id", "goblin-archer-a" }, { "name", "Goblin Archer" }, { "team", "enemy" }, { "grid_pos", new Vector2I(11, 8) }, { "primary_ability_id", "ranged" }, { "initiative", 13 }, { "hit_points", 7 }, { "max_hit_points", 7 } }
                }
            }
        };

        var encounterB = new Dictionary
        {
            { "id", "encounter-b" },
            { "aggro_range", 4 },
            {
                "enemies",
                new Array<Dictionary>
                {
                    new Dictionary { { "id", "goblin-warrior-b" }, { "name", "Goblin" }, { "team", "enemy" }, { "grid_pos", new Vector2I(16, 5) }, { "primary_ability_id", "melee" }, { "initiative", 9 }, { "hit_points", 8 }, { "max_hit_points", 8 } },
                    new Dictionary { { "id", "goblin-archer-b" }, { "name", "Goblin Archer" }, { "team", "enemy" }, { "grid_pos", new Vector2I(17, 9) }, { "primary_ability_id", "ranged" }, { "initiative", 13 }, { "hit_points", 7 }, { "max_hit_points", 7 } }
                }
            }
        };

        return new Dictionary
        {
            { "id", "map-a" },
            { "width", 20 },
            { "height", 15 },
            { "blocked", new Array<Vector2I> { new Vector2I(7, 5), new Vector2I(7, 9), new Vector2I(13, 7), new Vector2I(15, 7) } },
            { "players", players },
            { "encounters", new Array<Dictionary> { encounterA, encounterB } },
            {
                "props",
                new Array<Dictionary>
                {
                    new Dictionary
                    {
                        { "id", "map-a-chest-1" },
                        { "name", "Old Chest" },
                        { "grid_pos", new Vector2I(5, 11) },
                        { "loot_item_ids", new Array<string> { "short-sword", "small-shield" } },
                        { "loot_rolls_min", 1 },
                        { "loot_rolls_max", 2 }
                    },
                    new Dictionary
                    {
                        { "id", "map-a-crate-1" },
                        { "name", "Supply Crate" },
                        { "grid_pos", new Vector2I(10, 3) },
                        { "loot_item_ids", new Array<string> { "small-shield" } },
                        { "loot_rolls_min", 1 },
                        { "loot_rolls_max", 1 }
                    }
                }
            },
            {
                "transitions",
                new Array<Dictionary>
                {
                    new Dictionary
                    {
                        { "from_cell", new Vector2I(19, 7) },
                        { "to_map", "map-b" },
                        { "spawn_cell", new Vector2I(1, 7) }
                    }
                }
            }
        };
    }

    private static Dictionary BuildMapB(Array<Dictionary> defaultParty)
    {
        var players = UpdatePartyGridPosition(defaultParty, new Vector2I(1, 7));

        var encounterC = new Dictionary
        {
            { "id", "encounter-c" },
            { "aggro_range", 4 },
            {
                "enemies",
                new Array<Dictionary>
                {
                    new Dictionary { { "id", "goblin-warrior-c" }, { "name", "Goblin" }, { "team", "enemy" }, { "grid_pos", new Vector2I(13, 6) }, { "primary_ability_id", "melee" }, { "initiative", 10 }, { "hit_points", 9 }, { "max_hit_points", 9 } },
                    new Dictionary { { "id", "goblin-archer-c" }, { "name", "Goblin Archer" }, { "team", "enemy" }, { "grid_pos", new Vector2I(15, 9) }, { "primary_ability_id", "ranged" }, { "initiative", 12 }, { "hit_points", 7 }, { "max_hit_points", 7 } }
                }
            }
        };

        return new Dictionary
        {
            { "id", "map-b" },
            { "width", 20 },
            { "height", 15 },
            { "blocked", new Array<Vector2I> { new Vector2I(8, 6), new Vector2I(8, 7), new Vector2I(8, 8), new Vector2I(12, 6), new Vector2I(12, 8) } },
            { "players", players },
            { "encounters", new Array<Dictionary> { encounterC } },
            {
                "props",
                new Array<Dictionary>
                {
                    new Dictionary
                    {
                        { "id", "map-b-chest-1" },
                        { "name", "Worn Chest" },
                        { "grid_pos", new Vector2I(14, 4) },
                        { "loot_item_ids", new Array<string> { "short-sword", "small-shield" } },
                        { "loot_rolls_min", 1 },
                        { "loot_rolls_max", 2 }
                    },
                    new Dictionary
                    {
                        { "id", "map-b-crate-1" },
                        { "name", "Broken Crate" },
                        { "grid_pos", new Vector2I(3, 10) },
                        { "loot_item_ids", new Array<string> { "short-sword" } },
                        { "loot_rolls_min", 1 },
                        { "loot_rolls_max", 1 }
                    }
                }
            },
            {
                "transitions",
                new Array<Dictionary>
                {
                    new Dictionary
                    {
                        { "from_cell", new Vector2I(0, 7) },
                        { "to_map", "map-a" },
                        { "spawn_cell", new Vector2I(18, 7) }
                    }
                }
            }
        };
    }

    public void DrawMapFeaturesOverlay(
        CanvasItem canvas,
        Array<Vector2I> blockedCells,
        Array<Dictionary> mapTransitions,
        int gridWidth,
        int gridHeight,
        int cellSize
    )
    {
        if (canvas == null)
        {
            return;
        }

        foreach (var blocked in blockedCells)
        {
            var rect = CellRect(blocked, cellSize);
            canvas.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 0.9f), true);
            canvas.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f, 0.9f), false, 2.0f);
            canvas.DrawLine(rect.Position, rect.Position + rect.Size, new Color(0.55f, 0.22f, 0.22f, 0.85f), 2.0f);
            canvas.DrawLine(
                new Vector2(rect.Position.X + rect.Size.X, rect.Position.Y),
                new Vector2(rect.Position.X, rect.Position.Y + rect.Size.Y),
                new Color(0.55f, 0.22f, 0.22f, 0.85f),
                2.0f
            );
        }

        foreach (var transition in mapTransitions)
        {
            var fromCell = GetVector2I(transition, "from_cell", new Vector2I(-9999, -9999));
            if (!IsInBounds(fromCell, gridWidth, gridHeight))
            {
                continue;
            }

            var rect = CellRect(fromCell, cellSize);
            canvas.DrawRect(rect, new Color(0.05f, 0.35f, 0.48f, 0.35f), true);
            canvas.DrawRect(rect, new Color(0.35f, 0.95f, 1.0f, 0.95f), false, 3.0f);

            var center = CellCenter(fromCell, cellSize);
            canvas.DrawCircle(center, 7.0f, new Color(0.45f, 1.0f, 1.0f, 0.95f));

            var markerLength = 10.0f;
            if (fromCell.X == 0)
            {
                canvas.DrawLine(center + new Vector2(-markerLength, 0), center + new Vector2(markerLength, 0), new Color(0.85f, 1.0f, 1.0f, 0.9f), 2.0f);
            }
            else if (fromCell.X == gridWidth - 1)
            {
                canvas.DrawLine(center + new Vector2(markerLength, 0), center + new Vector2(-markerLength, 0), new Color(0.85f, 1.0f, 1.0f, 0.9f), 2.0f);
            }
            else if (fromCell.Y == 0)
            {
                canvas.DrawLine(center + new Vector2(0, -markerLength), center + new Vector2(0, markerLength), new Color(0.85f, 1.0f, 1.0f, 0.9f), 2.0f);
            }
            else if (fromCell.Y == gridHeight - 1)
            {
                canvas.DrawLine(center + new Vector2(0, markerLength), center + new Vector2(0, -markerLength), new Color(0.85f, 1.0f, 1.0f, 0.9f), 2.0f);
            }
        }
    }

    public bool TryGetTransitionForCell(Array<Dictionary> mapTransitions, Vector2I cell, string fallbackMapId, out string toMap, out Vector2I spawnCell)
    {
        toMap = fallbackMapId;
        spawnCell = cell;

        if (mapTransitions == null)
        {
            return false;
        }

        foreach (var transition in mapTransitions)
        {
            var fromCell = GetVector2I(transition, "from_cell", new Vector2I(-9999, -9999));
            if (fromCell != cell)
            {
                continue;
            }

            toMap = GetString(transition, "to_map", fallbackMapId);
            spawnCell = GetVector2I(transition, "spawn_cell", cell);
            return true;
        }

        return false;
    }

    public void DrawMapInteractablesOverlay(CanvasItem canvas, Array<Dictionary> mapProps, Array<Dictionary> lootBags, HashSet<string> openedPropIds, int cellSize)
    {
        if (canvas == null)
        {
            return;
        }

        foreach (var prop in mapProps)
        {
            var cell = GetVector2I(prop, "grid_pos", new Vector2I(-9999, -9999));
            var propId = GetString(prop, "id", "prop");
            var isOpened = openedPropIds.Contains(propId);
            var rect = CellRect(cell, cellSize);
            if (isOpened)
            {
                canvas.DrawRect(rect, new Color(0.34f, 0.3f, 0.24f, 0.24f), true);
                canvas.DrawRect(rect, new Color(0.62f, 0.56f, 0.46f, 0.75f), false, 2.0f);
                canvas.DrawCircle(rect.GetCenter(), 5.0f, new Color(0.78f, 0.74f, 0.66f, 0.72f));
                canvas.DrawLine(rect.Position + new Vector2(12.0f, 12.0f), rect.End - new Vector2(12.0f, 12.0f), new Color(0.88f, 0.84f, 0.74f, 0.8f), 2.0f);
                canvas.DrawLine(new Vector2(rect.End.X - 12.0f, rect.Position.Y + 12.0f), new Vector2(rect.Position.X + 12.0f, rect.End.Y - 12.0f), new Color(0.88f, 0.84f, 0.74f, 0.8f), 2.0f);
            }
            else
            {
                canvas.DrawRect(rect, new Color(0.5f, 0.34f, 0.14f, 0.38f), true);
                canvas.DrawRect(rect, new Color(0.85f, 0.62f, 0.32f, 0.95f), false, 2.0f);
                canvas.DrawCircle(rect.GetCenter(), 6.0f, new Color(1.0f, 0.86f, 0.45f, 0.95f));
            }
        }

        foreach (var bag in lootBags)
        {
            var cell = GetVector2I(bag, "grid_pos", new Vector2I(-9999, -9999));
            var isEmpty = GetBagItemIds(bag).Count == 0;
            var rect = CellRect(cell, cellSize);
            if (isEmpty)
            {
                canvas.DrawRect(rect, new Color(0.32f, 0.29f, 0.24f, 0.2f), true);
                canvas.DrawCircle(rect.GetCenter(), 8.0f, new Color(0.72f, 0.68f, 0.6f, 0.72f));
                canvas.DrawArc(rect.GetCenter(), 10.0f, 0.0f, Mathf.Tau, 24, new Color(0.84f, 0.8f, 0.7f, 0.78f), 2.0f);
                canvas.DrawLine(rect.Position + new Vector2(14.0f, 14.0f), rect.End - new Vector2(14.0f, 14.0f), new Color(0.88f, 0.84f, 0.74f, 0.78f), 2.0f);
            }
            else
            {
                canvas.DrawRect(rect, new Color(0.6f, 0.46f, 0.2f, 0.28f), true);
                canvas.DrawCircle(rect.GetCenter(), 9.0f, new Color(0.97f, 0.78f, 0.25f, 0.95f));
                canvas.DrawArc(rect.GetCenter(), 11.0f, 0.0f, Mathf.Tau, 24, new Color(1.0f, 0.94f, 0.65f, 0.95f), 2.0f);
            }
        }
    }

    public Array<Dictionary> BuildNearbyLootEntries(Unit explorer, Array<Dictionary> mapProps, Array<Dictionary> lootBags, HashSet<string> openedPropIds, GameData gameData)
    {
        var entries = new Array<Dictionary>();
        if (explorer == null)
        {
            return entries;
        }

        foreach (var prop in mapProps)
        {
            var propCell = GetVector2I(prop, "grid_pos", new Vector2I(-9999, -9999));
            if (Manhattan(explorer.GridPos, propCell) > 1)
            {
                continue;
            }

            var propId = GetString(prop, "id", "prop");
            if (openedPropIds.Contains(propId))
            {
                continue;
            }

            var propName = GetString(prop, "name", "Prop");
            entries.Add(new Dictionary
            {
                { "id", $"prop:{propId}" },
                { "label", $"Open {propName}" },
                { "detail", $"Open {propName} at ({propCell.X}, {propCell.Y})." }
            });
        }

        foreach (var bag in lootBags)
        {
            var bagCell = GetVector2I(bag, "grid_pos", new Vector2I(-9999, -9999));
            if (Manhattan(explorer.GridPos, bagCell) > 1)
            {
                continue;
            }

            var bagId = GetString(bag, "id", "bag");
            var itemIds = GetBagItemIds(bag);
            if (itemIds.Count == 0)
            {
                continue;
            }

            entries.Add(new Dictionary
            {
                { "id", $"bag:{bagId}" },
                { "label", $"Pick up loot bag ({itemIds.Count} item{(itemIds.Count == 1 ? "" : "s")})" },
                { "detail", $"Pick up bag at ({bagCell.X}, {bagCell.Y}). Contains: {JoinItemNames(itemIds, gameData)}." }
            });
        }

        return entries;
    }

    public bool TryBuildExplorationClickLootEntries(Unit explorer, Vector2I clickedCell, Array<Dictionary> mapProps, Array<Dictionary> lootBags, HashSet<string> openedPropIds, GameData gameData, out Array<Dictionary> entries, out string statusText)
    {
        entries = new Array<Dictionary>();
        statusText = "";

        if (explorer == null)
        {
            return false;
        }

        var clickedInteractable = false;

        foreach (var prop in mapProps)
        {
            var propCell = GetVector2I(prop, "grid_pos", new Vector2I(-9999, -9999));
            if (propCell != clickedCell)
            {
                continue;
            }

            clickedInteractable = true;
            if (Manhattan(explorer.GridPos, propCell) > 1)
            {
                statusText = "Move adjacent to interact with that object.";
                return true;
            }

            var propId = GetString(prop, "id", "prop");
            var propName = GetString(prop, "name", "Prop");
            if (openedPropIds.Contains(propId))
            {
                statusText = $"{propName} is empty.";
                return true;
            }

            entries.Add(new Dictionary
            {
                { "id", $"prop:{propId}" },
                { "label", $"Open {propName}" },
                { "detail", $"Open {propName} at ({propCell.X}, {propCell.Y})." }
            });
            break;
        }

        if (entries.Count == 0)
        {
            foreach (var bag in lootBags)
            {
                var bagCell = GetVector2I(bag, "grid_pos", new Vector2I(-9999, -9999));
                if (bagCell != clickedCell)
                {
                    continue;
                }

                clickedInteractable = true;
                if (Manhattan(explorer.GridPos, bagCell) > 1)
                {
                    statusText = "Move adjacent to pick up that loot bag.";
                    return true;
                }

                var bagId = GetString(bag, "id", "bag");
                var itemIds = GetBagItemIds(bag);
                if (itemIds.Count == 0)
                {
                    statusText = "This loot bag is empty.";
                    return true;
                }

                entries.Add(new Dictionary
                {
                    { "id", $"bag:{bagId}" },
                    { "label", $"Pick up loot bag ({itemIds.Count} item{(itemIds.Count == 1 ? "" : "s")})" },
                    { "detail", $"Pick up bag at ({bagCell.X}, {bagCell.Y}). Contains: {JoinItemNames(itemIds, gameData)}." }
                });
                break;
            }
        }

        if (!clickedInteractable || entries.Count == 0)
        {
            return false;
        }

        statusText = "Loot interaction opened. Confirm to proceed.";
        return true;
    }

    public bool TryResolveExplorationInteractionById(Unit explorer, string interactionId, Array<Dictionary> mapProps, Array<Dictionary> lootBags, HashSet<string> openedPropIds, HashSet<string> lootedBagIds, List<string> partyInventoryItemIds, GameData gameData, RandomNumberGenerator lootRng, out string statusText, out string logText, out bool changedState)
    {
        statusText = "";
        logText = "";
        changedState = false;

        if (string.IsNullOrEmpty(interactionId) || explorer == null)
        {
            return false;
        }

        if (interactionId.StartsWith("prop:"))
        {
            return TryOpenPropById(explorer, interactionId.Substring(5), mapProps, lootBags, openedPropIds, gameData, lootRng, out statusText, out logText, out changedState);
        }

        if (interactionId.StartsWith("bag:"))
        {
            return TryPickupBagById(explorer, interactionId.Substring(4), lootBags, lootedBagIds, partyInventoryItemIds, gameData, out statusText, out logText, out changedState);
        }

        return false;
    }

    public Array<string> GetBagItemIds(Dictionary bag)
    {
        var itemIds = TryGetStringArray(bag, "item_ids");
        if (itemIds.Count > 0)
        {
            return itemIds;
        }

        var fallbackItem = GetString(bag, "item_id", "");
        if (!string.IsNullOrEmpty(fallbackItem))
        {
            itemIds.Add(fallbackItem);
        }

        return itemIds;
    }

    public string JoinItemNames(Array<string> itemIds, GameData gameData)
    {
        if (itemIds.Count == 0)
        {
            return "nothing";
        }

        var names = new List<string>();
        foreach (var itemId in itemIds)
        {
            var itemData = gameData?.GetItem(itemId) ?? new Dictionary();
            names.Add(GetString(itemData, "name", itemId));
        }

        return string.Join(", ", names);
    }

    private bool TryOpenPropById(Unit explorer, string propId, Array<Dictionary> mapProps, Array<Dictionary> lootBags, HashSet<string> openedPropIds, GameData gameData, RandomNumberGenerator lootRng, out string statusText, out string logText, out bool changedState)
    {
        statusText = "";
        logText = "";
        changedState = false;

        for (var i = mapProps.Count - 1; i >= 0; i--)
        {
            var prop = mapProps[i];
            if (GetString(prop, "id", "") != propId)
            {
                continue;
            }

            var propCell = GetVector2I(prop, "grid_pos", new Vector2I(-9999, -9999));
            if (Manhattan(explorer.GridPos, propCell) > 1)
            {
                return false;
            }

            if (openedPropIds.Contains(propId))
            {
                statusText = $"{GetString(prop, "name", "prop")} is empty.";
                return true;
            }

            openedPropIds.Add(propId);
            changedState = true;

            var drops = BuildPropLootDrops(prop, lootRng);
            if (drops.Count > 0)
            {
                var bag = new Dictionary
                {
                    { "id", $"bag-{propId}" },
                    { "grid_pos", propCell },
                    { "item_ids", drops },
                    { "source_prop_id", propId }
                };
                lootBags.Add(bag);
                statusText = $"{explorer.UnitName} opened {GetString(prop, "name", "prop")} and revealed loot.";
                logText = $"Opened {GetString(prop, "name", "prop")}: found {JoinItemNames(drops, gameData)}.";
            }
            else
            {
                statusText = $"{explorer.UnitName} opened {GetString(prop, "name", "prop")}. It was empty.";
            }

            return true;
        }

        return false;
    }

    private bool TryPickupBagById(Unit explorer, string bagId, Array<Dictionary> lootBags, HashSet<string> lootedBagIds, List<string> partyInventoryItemIds, GameData gameData, out string statusText, out string logText, out bool changedState)
    {
        statusText = "";
        logText = "";
        changedState = false;

        for (var i = lootBags.Count - 1; i >= 0; i--)
        {
            var bag = lootBags[i];
            if (GetString(bag, "id", "") != bagId)
            {
                continue;
            }

            var bagCell = GetVector2I(bag, "grid_pos", new Vector2I(-9999, -9999));
            if (Manhattan(explorer.GridPos, bagCell) > 1)
            {
                return false;
            }

            var itemIds = GetBagItemIds(bag);
            if (itemIds.Count == 0)
            {
                statusText = "This loot bag is empty.";
                return true;
            }

            lootedBagIds.Add(bagId);
            bag["item_ids"] = new Array<string>();
            bag["item_id"] = "";
            changedState = true;

            foreach (var itemId in itemIds)
            {
                partyInventoryItemIds.Add(itemId);
            }

            var pickupSummary = JoinItemNames(itemIds, gameData);
            statusText = $"{explorer.UnitName} picked up {pickupSummary}.";
            logText = $"Loot acquired: {pickupSummary}.";
            return true;
        }

        return false;
    }

    private static Array<string> BuildPropLootDrops(Dictionary prop, RandomNumberGenerator lootRng)
    {
        var pool = TryGetStringArray(prop, "loot_item_ids");
        var legacySingle = GetString(prop, "loot_item_id", "");
        if (pool.Count == 0 && !string.IsNullOrEmpty(legacySingle))
        {
            pool.Add(legacySingle);
        }

        if (pool.Count == 0)
        {
            return new Array<string>();
        }

        var minRolls = Mathf.Max(1, GetInt(prop, "loot_rolls_min", 1));
        var maxRolls = Mathf.Max(minRolls, GetInt(prop, "loot_rolls_max", minRolls));
        var desiredRolls = lootRng.RandiRange(minRolls, maxRolls);
        var rolls = Mathf.Clamp(desiredRolls, 1, pool.Count);

        var indices = new List<int>();
        for (var i = 0; i < pool.Count; i++)
        {
            indices.Add(i);
        }

        for (var i = indices.Count - 1; i > 0; i--)
        {
            var swap = lootRng.RandiRange(0, i);
            (indices[i], indices[swap]) = (indices[swap], indices[i]);
        }

        var drops = new Array<string>();
        for (var i = 0; i < rolls; i++)
        {
            drops.Add(pool[indices[i]]);
        }

        return drops;
    }

    private static int Manhattan(Vector2I a, Vector2I b)
    {
        return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
    }

    private static bool IsInBounds(Vector2I cell, int gridWidth, int gridHeight)
    {
        return cell.X >= 0 && cell.X < gridWidth && cell.Y >= 0 && cell.Y < gridHeight;
    }

    private static Rect2 CellRect(Vector2I cell, int cellSize)
    {
        return new Rect2(new Vector2(cell.X * cellSize, cell.Y * cellSize), new Vector2(cellSize, cellSize));
    }

    private static Vector2 CellCenter(Vector2I cell, int cellSize)
    {
        return new Vector2(cell.X * cellSize + cellSize / 2.0f, cell.Y * cellSize + cellSize / 2.0f);
    }

    private static Vector2I GetVector2I(Dictionary dict, string key, Vector2I fallback)
    {
        return dict.ContainsKey(key) ? (Vector2I)((Variant)dict[key]) : fallback;
    }

    private static string GetString(Dictionary dict, string key, string fallback)
    {
        return dict.ContainsKey(key) ? ((Variant)dict[key]).AsString() : fallback;
    }

    private static int GetInt(Dictionary dict, string key, int fallback)
    {
        return dict.ContainsKey(key) ? (int)((Variant)dict[key]) : fallback;
    }

    private static Array<string> TryGetStringArray(Dictionary dict, string key)
    {
        if (!dict.ContainsKey(key))
        {
            return new Array<string>();
        }

        var raw = (Variant)dict[key];
        if (raw.VariantType != Variant.Type.Array)
        {
            return new Array<string>();
        }

        var result = new Array<string>();
        foreach (var entry in (Array)raw)
        {
            var variant = (Variant)entry;
            if (variant.VariantType == Variant.Type.String)
            {
                result.Add(variant.AsString());
            }
        }

        return result;
    }

    private Array<Dictionary> BuildDefaultParty()
    {
        if (_gameData == null)
        {
            return new Array<Dictionary>();
        }

        if (_gameData.RawData.Count == 0)
        {
            _gameData.LoadData();
        }

        return _gameData.GetDefaultPartyTemplates();
    }

    private static Array<Dictionary> UpdatePartyGridPosition(Array<Dictionary> defaultParty, Vector2I leaderCell)
    {
        var players = new Array<Dictionary>();
        for (var i = 0; i < defaultParty.Count; i++)
        {
            var player = CopyDictionary(defaultParty[i]);
            player["grid_pos"] = leaderCell + GetPartyFormationOffset(i);
            players.Add(player);
        }

        return players;
    }

    private static Vector2I GetPartyFormationOffset(int index)
    {
        return index switch
        {
            0 => new Vector2I(0, -1),
            1 => new Vector2I(0, 1),
            2 => new Vector2I(0, 0),
            _ => new Vector2I(0, index - 2)
        };
    }

    private static Dictionary CopyDictionary(Dictionary source)
    {
        var player = new Dictionary();
        foreach (var key in source.Keys)
        {
            player[key] = source[key];
        }

        return player;
    }
}
