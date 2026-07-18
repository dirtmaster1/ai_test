using System.Collections.Generic;

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
}

public static class MapTokenCatalog
{
    public static readonly Dictionary<string, TokenMapDef> Maps = BuildMaps();

    private static Dictionary<string, TokenMapDef> BuildMaps()
    {
        var forestTerrain = new MapTerrainDef
        {
            AtlasPath = "res://assets/tilesets/forest_terrain_64.png",
            FloorAtlasX = 1,
            FloorAtlasY = 0,
            WallAtlasX = 0,
            WallAtlasY = 1,
            DoorAtlasX = 0,
            DoorAtlasY = 2,
            OpenDoorAtlasX = 1,
            OpenDoorAtlasY = 0
        };

        return new Dictionary<string, TokenMapDef>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["forest-town"] = new TokenMapDef
            {
                Id = "forest-town",
                Name = "Forest Town",
                Terrain = forestTerrain
            },
            ["forest-path"] = new TokenMapDef
            {
                Id = "forest-path",
                Name = "Forest Path",
                Terrain = forestTerrain
            },
            ["goblin-cave"] = new TokenMapDef
            {
                Id = "goblin-cave",
                Name = "Goblin Cave"
            },
            ["graveyard"] = new TokenMapDef
            {
                Id = "graveyard",
                Name = "Graveyard",
                Terrain = forestTerrain
            },
            ["necromancers-crypt"] = new TokenMapDef
            {
                Id = "necromancers-crypt",
                Name = "Necromancers Crypt"
            }
        };
    }
}
