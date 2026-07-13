using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class MapLoader : Node
{
    [Export] public NodePath MapsRootPath = "../Maps";
    private const string UnitsAtlasPath = "res://assets/tilesets/units_2_64.png";
    private const int TerrainTileSize = 64;
    private const int UnitTileSize = 64;
    private const string BaseSuffix = "-base";
    private const string MarkerSuffix = "-markers";
    private const string TerrainTypeKey = "terrain_type";
    private const string DoorIdKey = "door_id";

    private GameData _gameData;
    private Array<Dictionary> _defaultParty = new();
    private readonly System.Collections.Generic.Dictionary<string, TileMapLayer> _mapLayersById = new();
    private readonly System.Collections.Generic.Dictionary<string, TileSet> _terrainTileSetsByKey = new(System.StringComparer.OrdinalIgnoreCase);
    private Texture2D _unitsTexture;

    public override void _Ready()
    {
        _gameData = GetNodeOrNull<GameData>("/root/GameData");
        _defaultParty = BuildDefaultParty();
        CacheMapLayers();
        EnsureBaseLayersPopulated();
    }

    public Dictionary LoadMapStub(string mapId = "forest-town")
    {
        if (_defaultParty.Count == 0)
        {
            _defaultParty = BuildDefaultParty();
        }

        EnsureBaseLayerPopulated(mapId);

        if (!TryBuildMapFromTokenDefinition(mapId, out var tokenMapData))
        {
            tokenMapData = new Dictionary
            {
                { "id", mapId },
                { "width", 1 },
                { "height", 1 },
                { "walls", new Array<Vector2I>() },
                { "doors", new Array<Dictionary>() },
                { "players", BuildPlayersFromTokenSpawns(new System.Collections.Generic.Dictionary<string, Vector2I>(System.StringComparer.OrdinalIgnoreCase)) },
                { "encounters", new Array<Dictionary>() },
                { "props", new Array<Dictionary>() },
                { "transitions", new Array<Dictionary>() }
            };
        }

        ApplyMarkerOverrides(mapId, tokenMapData);
        SetActiveMapVisual(mapId);
        return tokenMapData;
    }

    private bool TryBuildMapFromTokenDefinition(string mapId, out Dictionary mapData)
    {
        mapData = null;

        if (!MapTokenCatalog.Maps.TryGetValue(mapId, out var definition))
        {
            return false;
        }

        var layoutRows = SplitTokenRows(definition.LayoutRows);
        if (layoutRows.Count == 0)
        {
            return false;
        }

        var height = layoutRows.Count;
        var width = GetRowWidth(layoutRows);
        var walls = new Array<Vector2I>();
        var doors = new Array<Dictionary>();
        var transitions = new Array<Dictionary>();
        var enemyConfigs = new Array<Dictionary>();
        var playerSpawnByCharacterId = new System.Collections.Generic.Dictionary<string, Vector2I>(System.StringComparer.OrdinalIgnoreCase);

        for (var y = 0; y < height; y++)
        {
            var row = layoutRows[y];
            for (var x = 0; x < width; x++)
            {
                var token = GetRowToken(row, x);
                var cell = new Vector2I(x, y);

                if (definition.BaseLegend.TryGetValue(token, out var baseDef))
                {
                    switch (baseDef.Type)
                    {
                        case MapBaseTileType.Wall:
                            walls.Add(cell);
                            break;
                        case MapBaseTileType.Door:
                            doors.Add(new Dictionary
                            {
                                { "id", $"{mapId}-door-{x}-{y}" },
                                { "cell", cell },
                                { "is_open", false }
                            });
                            break;
                        case MapBaseTileType.MapTransition:
                            var spawnCell = TryFindExplicitTransitionSpawnCell(definition.Id, baseDef.TargetMapId, out var explicitSpawn)
                                ? explicitSpawn
                                : BuildTransitionSpawnCell(cell, width, height);
                            transitions.Add(new Dictionary
                            {
                                { "from_cell", cell },
                                { "to_map", baseDef.TargetMapId },
                                { "spawn_cell", spawnCell }
                            });
                            break;
                    }
                }

                if (!definition.EncounterLegend.TryGetValue(token, out var encounterDef))
                {
                    continue;
                }

                if (string.Equals(encounterDef.Kind, "player", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(encounterDef.CharacterId))
                    {
                        playerSpawnByCharacterId[encounterDef.CharacterId] = cell;
                    }

                    continue;
                }

                if (string.Equals(encounterDef.Kind, "enemy", System.StringComparison.OrdinalIgnoreCase))
                {
                    enemyConfigs.Add(BuildEnemyFromToken(mapId, token, encounterDef, cell, enemyConfigs.Count));
                }
            }
        }

        var players = BuildPlayersFromTokenSpawns(playerSpawnByCharacterId);
        var encounters = new Array<Dictionary>();
        if (enemyConfigs.Count > 0)
        {
            encounters.Add(new Dictionary
            {
                { "id", $"{mapId}-encounter-main" },
                { "aggro_range", 4 },
                { "enemies", enemyConfigs }
            });
        }

        var props = BuildPropsFromTokenDefinition(mapId, definition, width, height);

        mapData = new Dictionary
        {
            { "id", definition.Id },
            { "name", definition.Name },
            { "width", width },
            { "height", height },
            { "walls", walls },
            { "doors", doors },
            { "players", players },
            { "encounters", encounters },
            { "props", props },
            { "transitions", transitions }
        };

        return true;
    }

    private static List<string[]> SplitTokenRows(string[] rows)
    {
        var parsedRows = new List<string[]>();
        if (rows == null)
        {
            return parsedRows;
        }

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row))
            {
                continue;
            }

            parsedRows.Add(row.Split(' ', System.StringSplitOptions.RemoveEmptyEntries));
        }

        return parsedRows;
    }

    private static int GetRowWidth(List<string[]> rows)
    {
        var width = 0;
        foreach (var row in rows)
        {
            width = Mathf.Max(width, row.Length);
        }

        return Mathf.Max(1, width);
    }

    private static string GetRowToken(string[] row, int x)
    {
        if (row == null || x < 0 || x >= row.Length)
        {
            return "__";
        }

        return row[x];
    }

    private static Vector2I BuildTransitionSpawnCell(Vector2I sourceCell, int width, int height)
    {
        if (sourceCell.X <= 0)
        {
            return new Vector2I(Mathf.Max(1, width - 2), sourceCell.Y);
        }

        if (sourceCell.X >= width - 1)
        {
            return new Vector2I(1, sourceCell.Y);
        }

        if (sourceCell.Y <= 0)
        {
            return new Vector2I(sourceCell.X, Mathf.Max(1, height - 2));
        }

        if (sourceCell.Y >= height - 1)
        {
            return new Vector2I(sourceCell.X, 1);
        }

        return sourceCell;
    }

    private static bool TryFindExplicitTransitionSpawnCell(string sourceMapId, string targetMapId, out Vector2I spawnCell)
    {
        spawnCell = default;
        if (string.IsNullOrEmpty(sourceMapId) || string.IsNullOrEmpty(targetMapId))
        {
            return false;
        }

        if (!MapTokenCatalog.Maps.TryGetValue(targetMapId, out var targetDefinition))
        {
            return false;
        }

        var rows = SplitTokenRows(targetDefinition.LayoutRows);
        for (var y = 0; y < rows.Count; y++)
        {
            var row = rows[y];
            for (var x = 0; x < row.Length; x++)
            {
                var token = row[x];
                if (!targetDefinition.BaseLegend.TryGetValue(token, out var baseDef))
                {
                    continue;
                }

                if (baseDef.Type != MapBaseTileType.MapTransitionSpawn)
                {
                    continue;
                }

                if (!string.Equals(baseDef.TargetMapId, sourceMapId, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                spawnCell = new Vector2I(x, y);
                return true;
            }
        }

        return false;
    }

    private Dictionary BuildEnemyFromToken(string mapId, string token, MapEncounterTokenDef encounterDef, Vector2I cell, int index)
    {
        var archetypeId = encounterDef.ArchetypeId;
        var enemy = new Dictionary();

        if (!string.IsNullOrEmpty(archetypeId) && _gameData != null)
        {
            var template = _gameData.GetCharacterTemplate(archetypeId);
            if (template.Count > 0)
            {
                enemy = CopyDictionary(template);
            }
        }

        if (enemy.Count == 0)
        {
            enemy["name"] = BuildFallbackName(archetypeId, "Enemy");
            enemy["primary_ability_id"] = "melee";
            enemy["initiative"] = 10;
            enemy["hit_points"] = 10;
            enemy["max_hit_points"] = 10;
        }

        enemy["id"] = $"{mapId}-{token.ToLowerInvariant()}-{index}";
        enemy["team"] = "enemy";
        enemy["grid_pos"] = cell;

        if (string.IsNullOrEmpty(GetString(enemy, "name", "")))
        {
            enemy["name"] = BuildFallbackName(archetypeId, "Enemy");
        }

        if (string.IsNullOrEmpty(GetString(enemy, "primary_ability_id", "")))
        {
            enemy["primary_ability_id"] = "melee";
        }

        return enemy;
    }

    private Array<Dictionary> BuildPlayersFromTokenSpawns(System.Collections.Generic.Dictionary<string, Vector2I> spawnByCharacterId)
    {
        var players = new Array<Dictionary>();
        if (_defaultParty.Count == 0)
        {
            return players;
        }

        var anchor = new Vector2I(2, 7);
        foreach (var pair in spawnByCharacterId)
        {
            anchor = pair.Value;
            break;
        }

        for (var i = 0; i < _defaultParty.Count; i++)
        {
            var player = CopyDictionary(_defaultParty[i]);
            var playerId = GetString(player, "id", "");
            if (!string.IsNullOrEmpty(playerId) && spawnByCharacterId.TryGetValue(playerId, out var specificCell))
            {
                player["grid_pos"] = specificCell;
            }
            else
            {
                player["grid_pos"] = anchor + GetPartyFormationOffset(i);
            }

            players.Add(player);
        }

        return players;
    }

    private static string BuildFallbackName(string archetypeId, string fallback)
    {
        if (string.IsNullOrEmpty(archetypeId))
        {
            return fallback;
        }

        return archetypeId.Replace('-', ' ');
    }

    private static Array<Dictionary> BuildPropsFromTokenDefinition(string mapId, TokenMapDef definition, int width, int height)
    {
        var props = new Array<Dictionary>();
        var propRows = SplitTokenRows(definition.PropRows);
        if (propRows.Count == 0 || definition.PropLegend.Count == 0)
        {
            return props;
        }

        var rowCount = Mathf.Min(height, propRows.Count);
        for (var y = 0; y < rowCount; y++)
        {
            var row = propRows[y];
            var colCount = Mathf.Min(width, row.Length);
            for (var x = 0; x < colCount; x++)
            {
                var token = row[x];
                if (token == "__" || !definition.PropLegend.TryGetValue(token, out var propDef))
                {
                    continue;
                }

                var prop = new Dictionary
                {
                    { "id", $"{mapId}-{token.ToLowerInvariant()}-{x}-{y}" },
                    { "type", propDef.Type },
                    { "name", propDef.Name },
                    { "grid_pos", new Vector2I(x, y) }
                };

                if (!string.IsNullOrEmpty(propDef.InteractionText))
                {
                    prop["interaction_text"] = propDef.InteractionText;
                }

                if (propDef.LootItemIds != null && propDef.LootItemIds.Length > 0)
                {
                    var lootIds = new Array<string>();
                    foreach (var itemId in propDef.LootItemIds)
                    {
                        if (!string.IsNullOrEmpty(itemId))
                        {
                            lootIds.Add(itemId.Trim());
                        }
                    }

                    if (lootIds.Count > 0)
                    {
                        prop["loot_item_ids"] = lootIds;
                        prop["loot_rolls_min"] = Mathf.Max(1, propDef.LootRollsMin);
                        prop["loot_rolls_max"] = Mathf.Max((int)prop["loot_rolls_min"], propDef.LootRollsMax);
                    }
                }

                props.Add(prop);
            }
        }

        return props;
    }

    public void SetActiveMapVisual(string mapId)
    {
        if (_mapLayersById.Count == 0)
        {
            CacheMapLayers();
        }

        var hasDirectLayer = _mapLayersById.ContainsKey(mapId) || _mapLayersById.ContainsKey(mapId + BaseSuffix);
        if (!hasDirectLayer)
        {
            var fallbackLayer = ResolveFallbackBaseLayer();
            foreach (var pair in _mapLayersById)
            {
                pair.Value.Visible = pair.Value == fallbackLayer;
            }

            return;
        }

        foreach (var pair in _mapLayersById)
        {
            var isBase = pair.Key == mapId || pair.Key == mapId + BaseSuffix;
            pair.Value.Visible = isBase;
        }
    }

    private void CacheMapLayers()
    {
        _mapLayersById.Clear();

        var mapsRoot = GetNodeOrNull<Node>(MapsRootPath);
        if (mapsRoot == null)
        {
            return;
        }

        foreach (var child in mapsRoot.GetChildren())
        {
            if (child is not TileMapLayer layer)
            {
                continue;
            }

            var mapId = layer.Name.ToString();
            _mapLayersById[mapId] = layer;
        }
    }

    private void EnsureBaseLayersPopulated()
    {
        EnsureBaseLayerPopulated("forest-town");
    }

    private void EnsureBaseLayerPopulated(string mapId)
    {
        if (!TryGetBaseLayer(mapId, out var baseLayer))
        {
            return;
        }

        var terrain = ResolveTerrainDefinition(mapId);
        if (!TryAssignTerrainTileSet(baseLayer, terrain))
        {
            return;
        }

        if (!MapTokenCatalog.Maps.TryGetValue(mapId, out var definition))
        {
            return;
        }

        baseLayer.Clear();

        var rows = SplitTokenRows(definition.LayoutRows);
        var width = GetRowWidth(rows);
        var height = rows.Count;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var token = GetRowToken(rows[y], x);
                var atlas = GetFloorAtlasCell(terrain);
                if (definition.BaseLegend.TryGetValue(token, out var baseDef))
                {
                    if (baseDef.Type == MapBaseTileType.Wall)
                    {
                        atlas = GetWallAtlasCell(terrain);
                    }
                    else if (baseDef.Type == MapBaseTileType.Door)
                    {
                        atlas = GetDoorAtlasCell(terrain);
                    }
                }

                baseLayer.SetCell(new Vector2I(x, y), 0, atlas, 0);
            }
        }
    }

    private bool TryAssignTerrainTileSet(TileMapLayer layer, MapTerrainDef terrain)
    {
        if (layer == null)
        {
            return false;
        }

        terrain ??= new MapTerrainDef();
        var terrainKey = BuildTerrainKey(terrain);
        if (!_terrainTileSetsByKey.TryGetValue(terrainKey, out var terrainTileSet))
        {
            var atlasTexture = GD.Load<Texture2D>(terrain.AtlasPath);
            if (atlasTexture == null)
            {
                return false;
            }

            var floorAtlasCell = GetFloorAtlasCell(terrain);
            var wallAtlasCell = GetWallAtlasCell(terrain);
            var doorAtlasCell = GetDoorAtlasCell(terrain);
            var openDoorAtlasCell = GetOpenDoorAtlasCell(terrain);
            var atlasSource = new TileSetAtlasSource
            {
                Texture = atlasTexture,
                TextureRegionSize = new Vector2I(TerrainTileSize, TerrainTileSize)
            };
            atlasSource.CreateTile(floorAtlasCell);
            atlasSource.CreateTile(wallAtlasCell);
            atlasSource.CreateTile(doorAtlasCell);
            atlasSource.CreateTile(openDoorAtlasCell);

            terrainTileSet = new TileSet();
            terrainTileSet.TileSize = new Vector2I(TerrainTileSize, TerrainTileSize);
            terrainTileSet.AddSource(atlasSource, 0);
            _terrainTileSetsByKey[terrainKey] = terrainTileSet;
        }

        if (layer.TileSet != terrainTileSet)
        {
            layer.TileSet = terrainTileSet;
        }

        return true;
    }

    public bool SetDoorVisual(string mapId, Vector2I cell, bool isOpen)
    {
        if (!TryGetBaseLayer(mapId, out var baseLayer))
        {
            return false;
        }

        var terrain = ResolveTerrainDefinition(mapId);
        if (!TryAssignTerrainTileSet(baseLayer, terrain))
        {
            return false;
        }

        var atlas = isOpen ? GetOpenDoorAtlasCell(terrain) : GetDoorAtlasCell(terrain);
        baseLayer.SetCell(cell, 0, atlas, 0);
        return true;
    }

    private bool TryBuildGeometryFromBaseLayer(string mapId, out Array<Vector2I> wallCells, out Array<Dictionary> doors, out int width, out int height)
    {
        wallCells = new Array<Vector2I>();
        doors = new Array<Dictionary>();
        width = 0;
        height = 0;

        if (_mapLayersById.Count == 0)
        {
            CacheMapLayers();
        }

        if (!TryGetBaseLayer(mapId, out var baseLayer))
        {
            return false;
        }

        var usedRect = baseLayer.GetUsedRect();
        if (usedRect.Size.X <= 0 || usedRect.Size.Y <= 0)
        {
            return false;
        }

        for (var y = usedRect.Position.Y; y < usedRect.End.Y; y++)
        {
            for (var x = usedRect.Position.X; x < usedRect.End.X; x++)
            {
                var cell = new Vector2I(x, y);
                if (baseLayer.GetCellSourceId(cell) == -1)
                {
                    continue;
                }

                var terrainType = ResolveTerrainType(mapId, baseLayer, cell);

                if (terrainType == "wall")
                {
                    wallCells.Add(cell);
                    continue;
                }

                if (terrainType == "door")
                {
                    var doorId = $"{mapId}-door-{cell.X}-{cell.Y}";

                    doors.Add(new Dictionary
                    {
                        { "id", doorId },
                        { "cell", cell },
                        { "is_open", false }
                    });
                }
            }
        }

        width = Mathf.Max(1, usedRect.End.X);
        height = Mathf.Max(1, usedRect.End.Y);
        return true;
    }

    private string ResolveTerrainType(string mapId, TileMapLayer layer, Vector2I cell)
    {
        var atlasCoords = layer.GetCellAtlasCoords(cell);
        var terrain = ResolveTerrainDefinition(mapId);
        if (atlasCoords == GetWallAtlasCell(terrain))
        {
            return "wall";
        }

        if (atlasCoords == GetDoorAtlasCell(terrain) || atlasCoords == GetOpenDoorAtlasCell(terrain))
        {
            return "door";
        }

        return "floor";
    }

    private static string BuildTerrainKey(MapTerrainDef terrain)
    {
        return $"{terrain.AtlasPath}|{terrain.FloorAtlasX},{terrain.FloorAtlasY}|{terrain.WallAtlasX},{terrain.WallAtlasY}|{terrain.DoorAtlasX},{terrain.DoorAtlasY}|{terrain.OpenDoorAtlasX},{terrain.OpenDoorAtlasY}";
    }

    private static Vector2I GetFloorAtlasCell(MapTerrainDef terrain) => new(terrain.FloorAtlasX, terrain.FloorAtlasY);

    private static Vector2I GetWallAtlasCell(MapTerrainDef terrain) => new(terrain.WallAtlasX, terrain.WallAtlasY);

    private static Vector2I GetDoorAtlasCell(MapTerrainDef terrain) => new(terrain.DoorAtlasX, terrain.DoorAtlasY);

    private static Vector2I GetOpenDoorAtlasCell(MapTerrainDef terrain) => new(terrain.OpenDoorAtlasX, terrain.OpenDoorAtlasY);

    private static MapTerrainDef ResolveTerrainDefinition(string mapId)
    {
        return MapTokenCatalog.Maps.TryGetValue(mapId, out var definition) ? definition.Terrain ?? new MapTerrainDef() : new MapTerrainDef();
    }

    private bool TryGetBaseLayer(string mapId, out TileMapLayer layer)
    {
        layer = null;
        if (_mapLayersById.Count == 0)
        {
            CacheMapLayers();
        }

        if (_mapLayersById.TryGetValue(mapId + BaseSuffix, out layer) && layer != null)
        {
            return true;
        }

        if (_mapLayersById.TryGetValue(mapId, out layer) && layer != null)
        {
            return true;
        }

        layer = ResolveFallbackBaseLayer();
        return layer != null;
    }

    private TileMapLayer ResolveFallbackBaseLayer()
    {
        foreach (var pair in _mapLayersById)
        {
            if (pair.Value != null)
            {
                return pair.Value;
            }
        }

        return null;
    }

    private bool TryGetMarkerLayer(string mapId, out TileMapLayer layer)
    {
        layer = null;
        if (_mapLayersById.Count == 0)
        {
            CacheMapLayers();
        }

        return _mapLayersById.TryGetValue(mapId + MarkerSuffix, out layer) && layer != null;
    }

    private void ApplyMarkerOverrides(string mapId, Dictionary mapData)
    {
        if (!TryGetMarkerLayer(mapId, out var markerLayer))
        {
            return;
        }

        var usedRect = markerLayer.GetUsedRect();
        if (usedRect.Size.X <= 0 || usedRect.Size.Y <= 0)
        {
            return;
        }

        var transitions = new Array<Dictionary>();
        var props = new Array<Dictionary>();
        var playerSpawnBySlot = new System.Collections.Generic.Dictionary<int, Vector2I>();
        Vector2I? playerLeaderSpawn = null;

        var encountersById = new System.Collections.Generic.Dictionary<string, Dictionary>();

        for (var y = usedRect.Position.Y; y < usedRect.End.Y; y++)
        {
            for (var x = usedRect.Position.X; x < usedRect.End.X; x++)
            {
                var cell = new Vector2I(x, y);
                if (markerLayer.GetCellSourceId(cell) == -1)
                {
                    continue;
                }

                var tileData = markerLayer.GetCellTileData(cell);
                if (tileData == null)
                {
                    continue;
                }

                var markerType = GetTileString(tileData, "marker_type", "").ToLowerInvariant();
                if (string.IsNullOrEmpty(markerType))
                {
                    continue;
                }

                switch (markerType)
                {
                    case "transition":
                    {
                        var toMap = GetTileString(tileData, "to_map", mapId);
                        var spawnX = GetTileInt(tileData, "spawn_x", cell.X);
                        var spawnY = GetTileInt(tileData, "spawn_y", cell.Y);
                        transitions.Add(new Dictionary
                        {
                            { "from_cell", cell },
                            { "to_map", toMap },
                            { "spawn_cell", new Vector2I(spawnX, spawnY) }
                        });
                        break;
                    }
                    case "player_spawn":
                    {
                        var slot = GetTileInt(tileData, "party_slot", -1);
                        if (slot >= 0)
                        {
                            playerSpawnBySlot[slot] = cell;
                        }
                        else
                        {
                            playerLeaderSpawn = cell;
                        }
                        break;
                    }
                    case "enemy_spawn":
                    {
                        var encounterId = GetTileString(tileData, "encounter_id", "encounter-a");
                        if (!encountersById.TryGetValue(encounterId, out var encounter))
                        {
                            encounter = new Dictionary
                            {
                                { "id", encounterId },
                                { "aggro_range", GetTileInt(tileData, "aggro_range", 4) },
                                { "enemies", new Array<Dictionary>() }
                            };
                            encountersById[encounterId] = encounter;
                        }

                        var templateId = GetTileString(tileData, "template_id", "");
                        var enemy = new Dictionary();
                        if (!string.IsNullOrEmpty(templateId) && _gameData != null)
                        {
                            var template = _gameData.GetCharacterTemplate(templateId);
                            if (template.Count > 0)
                            {
                                enemy = CopyDictionary(template);
                            }
                        }

                        var fallbackEnemyId = GetString(enemy, "id", $"{encounterId}-enemy-{cell.X}-{cell.Y}");
                        var fallbackEnemyName = GetString(enemy, "name", "Enemy");
                        var fallbackPrimaryAbility = GetString(enemy, "primary_ability_id", "melee");
                        var fallbackInitiative = GetInt(enemy, "initiative", 10);
                        var fallbackHp = GetInt(enemy, "hit_points", 8);
                        var fallbackMaxHp = GetInt(enemy, "max_hit_points", fallbackHp);

                        enemy["id"] = GetTileString(tileData, "id", fallbackEnemyId);
                        enemy["name"] = GetTileString(tileData, "name", fallbackEnemyName);
                        enemy["team"] = "enemy";
                        enemy["grid_pos"] = cell;
                        enemy["primary_ability_id"] = GetTileString(tileData, "primary_ability_id", fallbackPrimaryAbility);
                        enemy["initiative"] = GetTileInt(tileData, "initiative", fallbackInitiative);
                        enemy["hit_points"] = GetTileInt(tileData, "hit_points", fallbackHp);
                        enemy["max_hit_points"] = GetTileInt(tileData, "max_hit_points", fallbackMaxHp);

                        var startingEquipment = GetTileStringArray(tileData, "starting_equipment");
                        if (startingEquipment.Count > 0)
                        {
                            enemy["starting_equipment"] = startingEquipment;
                        }

                        var enemies = (Array<Dictionary>)encounter["enemies"];
                        enemies.Add(enemy);
                        break;
                    }
                    case "prop":
                    case "chest":
                    case "sign":
                    case "npc":
                    case "trap":
                    {
                        var propType = markerType;
                        var propId = GetTileString(tileData, "id", $"{mapId}-{propType}-{cell.X}-{cell.Y}");
                        var fallbackName = char.ToUpper(propType[0]) + propType.Substring(1);
                        var prop = new Dictionary
                        {
                            { "id", propId },
                            { "type", propType },
                            { "name", GetTileString(tileData, "name", fallbackName) },
                            { "grid_pos", cell }
                        };

                        var interactionText = GetTileString(tileData, "interaction_text", "");
                        if (!string.IsNullOrEmpty(interactionText))
                        {
                            prop["interaction_text"] = interactionText;
                        }

                        var lootItemIds = GetTileStringArray(tileData, "loot_item_ids");
                        if (lootItemIds.Count > 0)
                        {
                            prop["loot_item_ids"] = lootItemIds;
                            prop["loot_rolls_min"] = Mathf.Max(1, GetTileInt(tileData, "loot_rolls_min", 1));
                            prop["loot_rolls_max"] = Mathf.Max((int)prop["loot_rolls_min"], GetTileInt(tileData, "loot_rolls_max", (int)prop["loot_rolls_min"]));
                        }

                        props.Add(prop);
                        break;
                    }
                }
            }
        }

        if (transitions.Count > 0)
        {
            mapData["transitions"] = transitions;
        }

        if (props.Count > 0)
        {
            mapData["props"] = props;
        }

        if (encountersById.Count > 0)
        {
            var encounterList = new Array<Dictionary>();
            foreach (var pair in encountersById)
            {
                encounterList.Add(pair.Value);
            }

            mapData["encounters"] = encounterList;
        }

        if (playerSpawnBySlot.Count > 0 || playerLeaderSpawn.HasValue)
        {
            mapData["players"] = BuildPlayersFromMarkerSpawns(playerSpawnBySlot, playerLeaderSpawn);
        }
    }

    private Array<Dictionary> BuildPlayersFromMarkerSpawns(System.Collections.Generic.Dictionary<int, Vector2I> playerSpawnBySlot, Vector2I? playerLeaderSpawn)
    {
        var players = new Array<Dictionary>();
        if (_defaultParty.Count == 0)
        {
            return players;
        }

        var leaderCell = playerLeaderSpawn ?? new Vector2I(2, 7);
        for (var i = 0; i < _defaultParty.Count; i++)
        {
            var player = CopyDictionary(_defaultParty[i]);
            var fallbackCell = leaderCell + GetPartyFormationOffset(i);
            if (playerSpawnBySlot.TryGetValue(i, out var slotCell))
            {
                player["grid_pos"] = slotCell;
            }
            else
            {
                player["grid_pos"] = fallbackCell;
            }

            players.Add(player);
        }

        return players;
    }

    private static string GetTileString(TileData tileData, string key, string fallback)
    {
        var value = tileData.GetCustomData(key);
        if (value.VariantType == Variant.Type.Nil)
        {
            return fallback;
        }

        if (value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName)
        {
            return value.AsString();
        }

        return value.ToString();
    }

    private static int GetTileInt(TileData tileData, string key, int fallback)
    {
        var value = tileData.GetCustomData(key);
        if (value.VariantType == Variant.Type.Nil)
        {
            return fallback;
        }

        if (value.VariantType == Variant.Type.Int)
        {
            return (int)value;
        }

        if (value.VariantType == Variant.Type.Float)
        {
            return Mathf.RoundToInt((float)value);
        }

        return int.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
    }

    private static Array<string> GetTileStringArray(TileData tileData, string key)
    {
        var result = new Array<string>();
        var value = tileData.GetCustomData(key);
        if (value.VariantType == Variant.Type.Nil)
        {
            return result;
        }

        if (value.VariantType == Variant.Type.Array)
        {
            foreach (var entry in (Array)value)
            {
                var variant = entry;
                if (variant.VariantType == Variant.Type.String || variant.VariantType == Variant.Type.StringName)
                {
                    var str = variant.AsString().Trim();
                    if (!string.IsNullOrEmpty(str))
                    {
                        result.Add(str);
                    }
                }
            }

            return result;
        }

        var csv = value.ToString();
        if (string.IsNullOrEmpty(csv))
        {
            return result;
        }

        var parts = csv.Split(',');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }

    public void DrawMapFeaturesOverlay(
        CanvasItem canvas,
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
            var propType = GetString(prop, "type", "prop");
            var isOpened = openedPropIds.Contains(propId);
            var rect = CellRect(cell, cellSize);
            if (propType == "npc")
            {
                canvas.DrawRect(rect, new Color(0.08f, 0.12f, 0.11f, 0.25f), true);
                var texture = GetUnitsTexture();
                if (texture != null)
                {
                    var sourceRect = new Rect2(new Vector2(0.0f, UnitTileSize), new Vector2(UnitTileSize, UnitTileSize));
                    canvas.DrawTextureRectRegion(texture, rect, sourceRect);
                }
                canvas.DrawRect(rect, new Color(0.48f, 0.78f, 0.62f, 0.95f), false, 2.0f);
                continue;
            }

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
            var hasLoot = HasLootConfig(prop);
            if (hasLoot && openedPropIds.Contains(propId))
            {
                continue;
            }

            var propName = GetString(prop, "name", "Prop");
            var interactionText = GetString(prop, "interaction_text", "");
            var verb = hasLoot ? "Open" : "Inspect";
            var detail = hasLoot
                ? $"Open {propName} at ({propCell.X}, {propCell.Y})."
                : string.IsNullOrEmpty(interactionText)
                    ? $"Inspect {propName} at ({propCell.X}, {propCell.Y})."
                    : interactionText;
            entries.Add(new Dictionary
            {
                { "id", $"prop:{propId}" },
                { "label", $"{verb} {propName}" },
                { "detail", detail }
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

            var containerName = GetBagSourceName(bag, mapProps);

            for (var itemIndex = 0; itemIndex < itemIds.Count; itemIndex++)
            {
                var itemId = itemIds[itemIndex];
                var itemData = gameData?.GetItem(itemId) ?? new Dictionary();
                var itemName = GetString(itemData, "name", itemId);

                entries.Add(new Dictionary
                {
                    { "id", $"bag-item:{bagId}:{itemIndex}" },
                    { "label", itemName },
                    { "detail", $"Loot {itemName}." },
                    { "source_title", containerName },
                    { "loot_all_id", $"bag-all:{bagId}" }
                });
            }
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
            var propType = GetString(prop, "type", "prop");
            var hasLoot = HasLootConfig(prop);
            var interactionText = GetString(prop, "interaction_text", "");
            if (propType == "npc" && propName == "Mira the Vendor")
            {
                entries.Add(new Dictionary
                {
                    { "id", "vendor:mira" },
                    { "label", "Mira the Vendor" },
                    { "detail", "Talk or browse Mira's store." },
                    { "source_title", "Mira the Vendor" }
                });
                break;
            }

            if (hasLoot && openedPropIds.Contains(propId))
            {
                statusText = $"{propName} is empty.";
                break;
            }

            entries.Add(new Dictionary
            {
                { "id", $"prop:{propId}" },
                { "label", hasLoot ? $"Open {propName}" : $"Inspect {propName}" },
                { "detail", hasLoot
                    ? $"Open {propName} at ({propCell.X}, {propCell.Y})."
                    : string.IsNullOrEmpty(interactionText)
                        ? $"Inspect {propName} at ({propCell.X}, {propCell.Y})."
                        : interactionText }
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

                var containerName = GetBagSourceName(bag, mapProps);
                for (var itemIndex = 0; itemIndex < itemIds.Count; itemIndex++)
                {
                    var itemId = itemIds[itemIndex];
                    var itemData = gameData?.GetItem(itemId) ?? new Dictionary();
                    var itemName = GetString(itemData, "name", itemId);

                    entries.Add(new Dictionary
                    {
                        { "id", $"bag-item:{bagId}:{itemIndex}" },
                        { "label", itemName },
                        { "detail", $"Loot {itemName}." },
                        { "source_title", containerName },
                        { "loot_all_id", $"bag-all:{bagId}" }
                    });
                }
                break;
            }
        }

        if (!clickedInteractable || entries.Count == 0)
        {
            return false;
        }

        statusText = "Loot interaction opened.";
        return true;
    }

    private Texture2D GetUnitsTexture()
    {
        if (_unitsTexture == null)
        {
            _unitsTexture = GD.Load<Texture2D>(UnitsAtlasPath);
        }

        return _unitsTexture;
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

        if (interactionId.StartsWith("bag-all:"))
        {
            return TryPickupBagById(explorer, interactionId.Substring(8), lootBags, lootedBagIds, partyInventoryItemIds, gameData, out statusText, out logText, out changedState);
        }

        if (interactionId.StartsWith("bag-item:"))
        {
            var bagItemPayload = interactionId.Substring(9);
            var split = bagItemPayload.Split(':', 2);
            if (split.Length != 2 || !int.TryParse(split[1], out var itemIndex))
            {
                return false;
            }

            return TryPickupBagItemByIndex(explorer, split[0], itemIndex, lootBags, lootedBagIds, partyInventoryItemIds, gameData, out statusText, out logText, out changedState);
        }

        return false;
    }

    private bool TryPickupBagItemByIndex(Unit explorer, string bagId, int itemIndex, Array<Dictionary> lootBags, HashSet<string> lootedBagIds, List<string> partyInventoryItemIds, GameData gameData, out string statusText, out string logText, out bool changedState)
    {
        statusText = "";
        logText = "";
        changedState = false;

        if (itemIndex < 0)
        {
            return false;
        }

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

            if (itemIndex >= itemIds.Count)
            {
                return false;
            }

            var itemId = itemIds[itemIndex];
            itemIds.RemoveAt(itemIndex);
            bag["item_ids"] = itemIds;
            if (itemIds.Count == 0)
            {
                bag["item_id"] = "";
                lootedBagIds.Add(bagId);
            }

            partyInventoryItemIds.Add(itemId);
            changedState = true;

            var itemData = gameData?.GetItem(itemId) ?? new Dictionary();
            var itemName = GetString(itemData, "name", itemId);
            statusText = $"{explorer.UnitName} looted {itemName}.";
            logText = $"Loot acquired: {itemName}.";
            return true;
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

            var hasLoot = HasLootConfig(prop);
            var interactionText = GetString(prop, "interaction_text", "");
            if (!hasLoot)
            {
                statusText = string.IsNullOrEmpty(interactionText)
                    ? $"{explorer.UnitName} inspected {GetString(prop, "name", "prop")}."
                    : interactionText;
                logText = statusText;
                changedState = false;
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

    private static bool HasLootConfig(Dictionary prop)
    {
        if (TryGetStringArray(prop, "loot_item_ids").Count > 0)
        {
            return true;
        }

        var single = GetString(prop, "loot_item_id", "");
        return !string.IsNullOrEmpty(single);
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
        return dict.ContainsKey(key) ? (Vector2I)dict[key] : fallback;
    }

    private static string GetString(Dictionary dict, string key, string fallback)
    {
        return dict.ContainsKey(key) ? dict[key].AsString() : fallback;
    }

    private static int GetInt(Dictionary dict, string key, int fallback)
    {
        return dict.ContainsKey(key) ? (int)dict[key] : fallback;
    }

    private static Array<string> TryGetStringArray(Dictionary dict, string key)
    {
        if (!dict.ContainsKey(key))
        {
            return new Array<string>();
        }

        var raw = dict[key];
        if (raw.VariantType != Variant.Type.Array)
        {
            return new Array<string>();
        }

        var result = new Array<string>();
        foreach (var entry in (Array)raw)
        {
            var variant = entry;
            if (variant.VariantType == Variant.Type.String)
            {
                result.Add(variant.AsString());
            }
        }

        return result;
    }

    private static string GetBagSourceName(Dictionary bag, Array<Dictionary> mapProps)
    {
        var sourcePropId = GetString(bag, "source_prop_id", "");
        if (string.IsNullOrEmpty(sourcePropId))
        {
            return "Loot Bag";
        }

        foreach (var prop in mapProps)
        {
            if (GetString(prop, "id", "") != sourcePropId)
            {
                continue;
            }

            return GetString(prop, "name", "Loot Bag");
        }

        return "Loot Bag";
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
            0 => new Vector2I(0, 0),
            1 => new Vector2I(0, 1),
            2 => new Vector2I(0, -1),
            3 => new Vector2I(1, 0),
            _ => new Vector2I(index - 3, 0)
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
