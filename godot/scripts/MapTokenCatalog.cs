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
    public int? FogOfWarVisionRange { get; init; }
    public int? FogOfWarVisibleAlpha { get; init; }
    public int? FogOfWarRevealedAlpha { get; init; }
}

public static class MapTokenCatalog
{
    public const int DefaultFogOfWarVisionRange = 7;
    public const int DefaultFogOfWarVisibleAlpha = 0;
    public const int DefaultFogOfWarRevealedAlpha = 158;
    public static readonly Dictionary<string, TokenMapDef> Maps = BuildMaps();

    public static int GetFogOfWarVisionRange(string mapId)
    {
        if (!string.IsNullOrWhiteSpace(mapId)
            && Maps.TryGetValue(mapId, out var definition)
            && definition.FogOfWarVisionRange.HasValue)
        {
            return System.Math.Max(0, definition.FogOfWarVisionRange.Value);
        }

        return DefaultFogOfWarVisionRange;
    }

    public static int GetFogOfWarVisibleAlpha(string mapId) => GetFogOfWarAlpha(mapId, true);

    public static int GetFogOfWarRevealedAlpha(string mapId) => GetFogOfWarAlpha(mapId, false);

    private static int GetFogOfWarAlpha(string mapId, bool visibleCells)
    {
        if (!string.IsNullOrWhiteSpace(mapId) && Maps.TryGetValue(mapId, out var definition))
        {
            var configuredAlpha = visibleCells ? definition.FogOfWarVisibleAlpha : definition.FogOfWarRevealedAlpha;
            if (configuredAlpha.HasValue)
            {
                return System.Math.Max(0, System.Math.Min(255, configuredAlpha.Value));
            }
        }

        return visibleCells ? DefaultFogOfWarVisibleAlpha : DefaultFogOfWarRevealedAlpha;
    }

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

        var graveyardTerrain = new MapTerrainDef
        {
            AtlasPath = "res://assets/tilesets/graveyard_terrain_64.png",
            FloorAtlasX = 0,
            FloorAtlasY = 0,
            WallAtlasX = 0,
            WallAtlasY = 1,
            DoorAtlasX = 0,
            DoorAtlasY = 2,
            OpenDoorAtlasX = 1,
            OpenDoorAtlasY = 2
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
            ["spider-mines"] = new TokenMapDef
            {
                Id = "spider-mines",
                Name = "Spider Mines",
                FogOfWarVisionRange = 3,
                FogOfWarVisibleAlpha = 158,
                FogOfWarRevealedAlpha = 178
            },
            ["graveyard"] = new TokenMapDef
            {
                Id = "graveyard",
                Name = "Graveyard",
                Terrain = graveyardTerrain
            },
            ["necromancers-crypt"] = new TokenMapDef
            {
                Id = "necromancers-crypt",
                Name = "Necromancers Crypt"
            }
        };
    }
}
