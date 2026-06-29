using Godot;
using Godot.Collections;

public partial class MapLoader : Node
{
    private static readonly Dictionary WizardTemplate = new()
    {
        { "id", "wizard" },
        { "name", "Wizard" },
        { "team", "player" },
        { "primary_ability_id", "melee" },
        { "initiative", 15 },
        { "hit_points", 10 },
        { "max_hit_points", 10 }
    };

    private static readonly Dictionary WarriorTemplate = new()
    {
        { "id", "warrior" },
        { "name", "Warrior" },
        { "team", "player" },
        { "primary_ability_id", "melee" },
        { "initiative", 11 },
        { "hit_points", 14 },
        { "max_hit_points", 14 }
    };

    private static readonly Dictionary ClericTemplate = new()
    {
        { "id", "cleric" },
        { "name", "Cleric" },
        { "team", "player" },
        { "primary_ability_id", "lesser-heal" },
        { "initiative", 12 },
        { "hit_points", 12 },
        { "max_hit_points", 12 }
    };

    private Array<Dictionary> _defaultParty = new();

    public override void _Ready()
    {
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

    private static Array<Dictionary> BuildDefaultParty()
    {
        return new Array<Dictionary>
        {
            CopyDictionary(WizardTemplate),
            CopyDictionary(WarriorTemplate),
            CopyDictionary(ClericTemplate)
        };
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
