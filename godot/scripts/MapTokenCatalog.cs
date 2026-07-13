using System;
using System.Collections.Generic;

public enum MapBaseTileType
{
    Floor,
    Wall,
    Door,
    MapTransition,
    MapTransitionSpawn
}

public sealed class MapBaseTokenDef
{
    public MapBaseTileType Type { get; init; } = MapBaseTileType.Floor;
    public string TargetMapId { get; init; } = "";
}

public sealed class MapPropTokenDef
{
    public string Name { get; init; } = "Prop";
    public string Type { get; init; } = "prop";
    public string InteractionText { get; init; } = "";
    public string[] LootItemIds { get; init; } = Array.Empty<string>();
    public int LootRollsMin { get; init; } = 1;
    public int LootRollsMax { get; init; } = 1;
}

public sealed class MapEncounterTokenDef
{
    public string Kind { get; init; } = "enemy";
    public string ArchetypeId { get; init; } = "";
    public string CharacterId { get; init; } = "";
}

public sealed class MapTerrainDef
{
    public string AtlasPath { get; init; } = "res://assets/tilesets/dungeon_terrain_64.png";
    public int FloorAtlasX { get; init; } = 0;
    public int FloorAtlasY { get; init; } = 0;
    public int WallAtlasX { get; init; } = 0;
    public int WallAtlasY { get; init; } = 1;
    public int DoorAtlasX { get; init; } = 0;
    public int DoorAtlasY { get; init; } = 2;
    public int OpenDoorAtlasX { get; init; } = 3;
    public int OpenDoorAtlasY { get; init; } = 2;
}

public sealed class TokenMapDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public MapTerrainDef Terrain { get; init; } = new();
    public string[] LayoutRows { get; init; } = Array.Empty<string>();
    public string[] PropRows { get; init; } = Array.Empty<string>();
    public Dictionary<string, MapBaseTokenDef> BaseLegend { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, MapPropTokenDef> PropLegend { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, MapEncounterTokenDef> EncounterLegend { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class MapTokenCatalog
{
    public static readonly Dictionary<string, TokenMapDef> Maps = BuildMaps();

    private static Dictionary<string, TokenMapDef> BuildMaps()
    {
        var maps = new Dictionary<string, TokenMapDef>(StringComparer.OrdinalIgnoreCase);

        maps["goblin-cave"] = new TokenMapDef
        {
            Id = "goblin-cave",
            Name = "Goblin Cave",
            LayoutRows = new[]
            {
                "wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa",
                "wa __ __ __ __ __ wa __ __ __ __ __ wa __ __ GCB GB GW1 GA __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ dr __ __ GW GS GA __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ dr __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ __ __ wa",
                "wa wa __ __ wa wa wa wa wa __ __ wa wa wa wa dr dr wa wa wa wa",
                "wa GW GS GS __ __ __ __ wa __ __ __ __ __ wa __ __ GA GA __ wa",
                "wa GW1 SP SP __ __ __ __ wa __ __ __ __ __ __ __ __ GB GW __ wa",
                "wa __ __ __ __ __ __ __ wa __ __ __ __ __ __ __ __ __ GB __ wa",
                "wa __ __ __ __ __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ wa",
                "wa wa __ __ wa wa wa wa wa __ __ __ __ wa wa wa wa __ __ wa wa",
                "wa __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ wa __ __ __ wa",
                "wa __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ wa __ __ __ wa",
                "wa __ __ wa wa __ __ wa wa wa wa __ __ wa wa wa wa GA GW wa wa",
                "wa __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ wa __ GB __ wa",
                "wa __ SP SP wa __ __ __ __ __ __ __ __ __ __ __ wa __ __ __ wa",
                "wa wa dr dr wa __ __ wa wa __ __ __ wa wa __ __ wa __ __ wa wa",
                "wa __ __ __ __ __ __ __ wa __ __ __ wa __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ wa __ __ __ wa __ __ __ __ __ __ __ wa",
                "wa sfp __ __ __ __ __ __ wa __ __ __ wa __ __ __ __ __ __ __ wa",
                "wa mt mt wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa"
            },
            PropRows = new[]
            {
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ bd __ __ __ __ wr1 __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ chI __ __ chS __ __ __ __ __ __ __ __ __ __ __ __ wr2 __ ch2 __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ tb __ chI __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ st1 __ __ __ st2 __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ br1 __ cr __ __ __ __ __ __ __ __ __ __ __ __ __ br2 __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ chS __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __"
            },
            BaseLegend = new Dictionary<string, MapBaseTokenDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["wa"] = new MapBaseTokenDef { Type = MapBaseTileType.Wall },
                ["mt"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransition, TargetMapId = "forest-path" },
                ["sfp"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransitionSpawn, TargetMapId = "forest-path" },
                ["dr"] = new MapBaseTokenDef { Type = MapBaseTileType.Door },
                ["__"] = new MapBaseTokenDef { Type = MapBaseTileType.Floor }
            },
            PropLegend = new Dictionary<string, MapPropTokenDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["bd"] = new MapPropTokenDef { Name = "Bed", Type = "prop" },
                ["tb"] = new MapPropTokenDef { Name = "Table", Type = "prop" },
                ["wr1"] = new MapPropTokenDef { Name = "Weapon Rack", Type = "prop" },
                ["wr2"] = new MapPropTokenDef { Name = "Weapon Rack", Type = "prop" },
                ["chI"] = new MapPropTokenDef { Name = "Iron Chest", Type = "chest", LootItemIds = new[] { "short-sword", "small-shield" }, LootRollsMin = 1, LootRollsMax = 2 },
                ["chS"] = new MapPropTokenDef { Name = "Steel Chest", Type = "chest", LootItemIds = new[] { "short-sword" }, LootRollsMin = 1, LootRollsMax = 1 },
                ["ch2"] = new MapPropTokenDef { Name = "Gold Chest", Type = "chest", LootItemIds = new[] { "chain-mail" }, LootRollsMin = 1, LootRollsMax = 1 },
                ["br1"] = new MapPropTokenDef { Name = "Barrels", Type = "prop" },
                ["br2"] = new MapPropTokenDef { Name = "Barrels", Type = "prop" },
                ["cr"] = new MapPropTokenDef { Name = "Crate", Type = "crate" },
                ["st1"] = new MapPropTokenDef { Name = "Spike Trap", Type = "trap" },
                ["st2"] = new MapPropTokenDef { Name = "Spike Trap", Type = "trap" }
            },
            EncounterLegend = new Dictionary<string, MapEncounterTokenDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["GW"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "goblin-warrior" },
                ["GW1"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "goblin-warrior" },
                ["GA"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "goblin-archer" },
                ["GS"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "goblin-shaman" },
                ["GB"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "goblin-brute" },
                ["SP"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "giant-spider" },
                ["GCB"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "goblin-chieftain" }
            }
        };

        maps["forest-path"] = new TokenMapDef
        {
            Id = "forest-path",
            Name = "Forest Path",
            Terrain = new MapTerrainDef
            {
                AtlasPath = "res://assets/tilesets/forest_terrain_64.png",
                FloorAtlasX = 1,
                FloorAtlasY = 0,
                WallAtlasX = 0,
                WallAtlasY = 1
            },
            LayoutRows = new[]
            {
                "wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa m3 m3 wa wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ sft __ __ wa",
                "wa __ __ DW __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ wa DW __ __ __ __ wa __ __ __ __ __ __ wa __ __ __ wa",
                "wa __ wa __ __ wa __ __ wa __ wa wa __ wa __ __ __ wa __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ wa wa __ __ wa __ __ __ __ wa __ wa __ wa __ wa __ wa",
                "wa __ __ __ wa __ __ __ wa __ __ __ __ __ __ wa __ __ __ wa",
                "wa __ wa __ __ __ wa __ wa __ __ wa __ __ __ __ wa __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ wa __ __ __ __ __ wa",
                "wa __ wa DW __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ DW __ __ __ __ __ __ __ wa __ __ __ wa __ __ __ wa",
                "wa __ wa DW __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ wa __ __ __ __ __ wa __ __ __ __ wa __ __ __ __ __ wa",
                "wa __ wa __ __ __ wa __ wa GS GS wa __ wa __ __ __ wa __ wa",
                "wa __ __ __ wa __ __ __ __ GS GS __ __ __ __ __ __ wa __ wa",
                "wa __ wa wa wa __ wa __ __ wa __ wa __ wa __ wa __ wa __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa sgc __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa wa wa wa wa wa wa wa wa mt mt wa wa wa wa wa wa wa wa wa"
            },
            PropRows = new[]
            {
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __"
            },
            BaseLegend = new Dictionary<string, MapBaseTokenDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["wa"] = new MapBaseTokenDef { Type = MapBaseTileType.Wall },
                ["mt"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransition, TargetMapId = "goblin-cave" },
                ["m3"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransition, TargetMapId = "forest-town" },
                ["sgc"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransitionSpawn, TargetMapId = "goblin-cave" },
                ["sft"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransitionSpawn, TargetMapId = "forest-town" },
                ["__"] = new MapBaseTokenDef { Type = MapBaseTileType.Floor }
            },
            EncounterLegend = new Dictionary<string, MapEncounterTokenDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["DW"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "dire-wolf" },
                ["GS"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "giant-spider" }
            }
        };

        maps["forest-town"] = new TokenMapDef
        {
            Id = "forest-town",
            Name = "Forest Town",
            LayoutRows = new[]
            {
                "wa wa wa wa wa wa wa wa wa m4 m4 wa wa wa wa wa wa wa wa wa",
                "wa __ __ __ __ __ __ __ __ sgy __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ wa wa wa wa wa wa __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ wa __ PR __ __ wa __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ wa PW PZ PC __ wa __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ wa __ __ __ __ wa __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ wa __ __ __ __ wa __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ wa wa dr dr wa wa __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ sfp __ __ __ __ __ __ __ __ wa",
                "wa wa wa wa wa wa wa wa wa mt mt wa wa wa wa wa wa wa wa wa"
            },
            PropRows = new[]
            {
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ sg __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ bu __ __ __ __ __ __ __ __ __ __ __ tr __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ vn __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ tr __ __ __ __ __ __ __ __ __ __ __ __ __ bu __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __"
            },
            BaseLegend = new Dictionary<string, MapBaseTokenDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["wa"] = new MapBaseTokenDef { Type = MapBaseTileType.Wall },
                ["dr"] = new MapBaseTokenDef { Type = MapBaseTileType.Door },
                ["mt"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransition, TargetMapId = "forest-path" },
                ["m4"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransition, TargetMapId = "graveyard" },
                ["sfp"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransitionSpawn, TargetMapId = "forest-path" },
                ["sgy"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransitionSpawn, TargetMapId = "graveyard" },
                ["__"] = new MapBaseTokenDef { Type = MapBaseTileType.Floor }
            },
            PropLegend = new Dictionary<string, MapPropTokenDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["sg"] = new MapPropTokenDef { Name = "Town Graveyard Sign", Type = "sign", InteractionText = "Town Graveyard - watch your step!" },
                ["bu"] = new MapPropTokenDef { Name = "Bush", Type = "prop" },
                ["tr"] = new MapPropTokenDef { Name = "Tree", Type = "prop" },
                ["vn"] = new MapPropTokenDef { Name = "Mira the Vendor", Type = "npc", InteractionText = "Mira the Vendor is here." }
            },
            EncounterLegend = new Dictionary<string, MapEncounterTokenDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["PW"] = new MapEncounterTokenDef { Kind = "player", CharacterId = "warrior" },
                ["PZ"] = new MapEncounterTokenDef { Kind = "player", CharacterId = "wizard" },
                ["PC"] = new MapEncounterTokenDef { Kind = "player", CharacterId = "cleric" },
                ["PR"] = new MapEncounterTokenDef { Kind = "player", CharacterId = "ranger" }
            }
        };

        maps["graveyard"] = new TokenMapDef
        {
            Id = "graveyard",
            Name = "Graveyard",
            LayoutRows = new[]
            {
                "wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa",
                "wa __ snc __ __ __ __ __ wa m2 wa __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ wa m2 wa __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ SM SM SM __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ SW SW SW __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ GH GH GH __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ SM GH __ __ __ __ __ __ __ __ __ __ __ __ SM GH __ wa",
                "wa __ GH SW __ __ __ __ __ __ __ __ __ __ __ __ GH SW __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ sft __ __ __ __ __ __ __ __ wa",
                "wa wa wa wa wa wa wa wa wa mt mt wa wa wa wa wa wa wa wa wa"
            },
            PropRows = new[]
            {
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ gv gv gv gv gv gv gv gv gv gv __ __ __ __ __",
                "__ __ __ __ __ gv gv gv gv gv gv gv gv gv gv __ __ __ __ __",
                "__ __ __ __ __ gv gv gv gv gv gv gv gv gv gv __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __"
            },
            BaseLegend = new Dictionary<string, MapBaseTokenDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["wa"] = new MapBaseTokenDef { Type = MapBaseTileType.Wall },
                ["mt"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransition, TargetMapId = "forest-town" },
                ["m2"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransition, TargetMapId = "necromancers-crypt" },
                ["sft"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransitionSpawn, TargetMapId = "forest-town" },
                ["snc"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransitionSpawn, TargetMapId = "necromancers-crypt" },
                ["__"] = new MapBaseTokenDef { Type = MapBaseTileType.Floor }
            },
            PropLegend = new Dictionary<string, MapPropTokenDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["gv"] = new MapPropTokenDef { Name = "Haunted Grave", Type = "prop" }
            },
            EncounterLegend = new Dictionary<string, MapEncounterTokenDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["SW"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "skeleton-warrior" },
                ["SM"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "skeleton-mage" },
                ["GH"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "ghoul" }
            }
        };

        maps["necromancers-crypt"] = new TokenMapDef
        {
            Id = "necromancers-crypt",
            Name = "Necromancers Crypt",
            LayoutRows = new[]
            {
                "wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ SM SM SM __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ SW NC SW __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ GH SM GH __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ SW SP SW __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa",
                "wa __ __ __ __ __ __ __ __ sgy __ __ __ __ __ __ __ __ __ wa",
                "wa wa wa wa wa wa wa wa wa m2 m2 wa wa wa wa wa wa wa wa wa"
            },
            PropRows = new[]
            {
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ su __ __ __ __ __ __ __ __ su __ __ __ __ __ __",
                "__ __ __ __ __ __ __ sk sk sk sk sk __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ sk sk sk sk sk __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ sk sk sk sk sk __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ sk sk sk sk sk __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ sk sk sk sk sk __ __ __ __ __ __ __ __",
                "__ __ __ __ su __ __ __ __ __ __ __ __ su __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ tu __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ tu __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ tr __ __ tr __ __ __ __ __ __ __ __",
                "__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __"
            },
            BaseLegend = new Dictionary<string, MapBaseTokenDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["wa"] = new MapBaseTokenDef { Type = MapBaseTileType.Wall },
                ["m2"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransition, TargetMapId = "graveyard" },
                ["sgy"] = new MapBaseTokenDef { Type = MapBaseTileType.MapTransitionSpawn, TargetMapId = "graveyard" },
                ["__"] = new MapBaseTokenDef { Type = MapBaseTileType.Floor }
            },
            PropLegend = new Dictionary<string, MapPropTokenDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["su"] = new MapPropTokenDef { Name = "Ancient Sarcophagus", Type = "prop" },
                ["sk"] = new MapPropTokenDef { Name = "Bone Pile", Type = "prop" },
                ["tu"] = new MapPropTokenDef { Name = "Funerary Urn", Type = "prop" },
                ["tr"] = new MapPropTokenDef { Name = "Crypt Torch", Type = "prop" }
            },
            EncounterLegend = new Dictionary<string, MapEncounterTokenDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["SW"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "skeleton-warrior" },
                ["SM"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "skeleton-mage" },
                ["GH"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "ghoul" },
                ["SP"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "spectre" },
                ["NC"] = new MapEncounterTokenDef { Kind = "enemy", ArchetypeId = "necromancer" }
            }
        };

        return maps;
    }
}
