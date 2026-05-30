// Authored dungeon layouts are colocated with generation to keep map bootstrapping in one file.
const cloneConfiguredVendorStockEntries = (entries) => Array.isArray(entries)
    ? entries.map((entry) => {
        if (entry && typeof entry === 'object' && !Array.isArray(entry)) {
            return { ...entry };
        }

        return entry;
    })
    : [];

(() => {
    if (window.CustomDungeonMaps) {
        return;
    }

    const createTokenRow = (row) => row.trim().split(/\s+/);

    window.CustomDungeonMaps = {
        createTokenRow,

        goblinCave: {
            id: 'goblin-cave',
            name: 'Goblin Cave',
            size: 21,
            layoutRows: [
                createTokenRow('wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa'),
                createTokenRow('wa __ __ __ __ __ wa __ __ __ __ __ wa __ __ GCB GB GW1 GA __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ dr __ __ GW GS GA __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ dr __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ __ __ wa'),
                createTokenRow('wa wa __ __ wa wa wa wa wa __ __ wa wa wa wa dr dr wa wa wa wa'),
                createTokenRow('wa GW __ GS __ __ __ __ wa __ __ __ __ __ wa __ __ GA __ __ wa'),
                createTokenRow('wa __ __ __ GSP __ __ __ wa __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ GW1 __ __ __ __ __ wa __ __ __ __ __ __ __ __ __ GB __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ wa'),
                createTokenRow('wa wa __ __ wa wa wa wa wa __ __ __ __ wa wa wa wa __ __ wa wa'),
                createTokenRow('wa __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ wa __ __ __ wa'),
                createTokenRow('wa __ __ __ wa __ __ __ __ __ wa __ __ __ __ GA wa __ __ __ wa'),
                createTokenRow('wa __ __ wa wa __ __ wa wa wa wa __ __ wa wa wa wa __ __ wa wa'),
                createTokenRow('wa __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ wa __ GB __ wa'),
                createTokenRow('wa __ __ __ wa __ __ __ __ __ __ __ __ __ __ __ wa __ __ __ wa'),
                createTokenRow('wa wa dr dr wa __ __ wa wa __ __ __ wa wa __ __ wa __ __ wa wa'),
                createTokenRow('wa GSP __ __ __ __ __ __ wa __ __ __ wa __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ wa __ __ __ wa __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ wa __ __ __ wa __ __ __ __ __ __ __ wa'),
                createTokenRow('wa mt mt wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa')
            ],
            propRows: [
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ bd __ __ __ __ wr1 __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ chI __ __ chS __ __ __ __ __ __ __ __ __ __ __ __ wr2 __ ch2 __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ tb __ chI __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ st1 __ __ __ st2 __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ br1 __ cr __ __ __ __ __ __ __ __ __ __ __ __ __ br2 __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ ch1 __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ chS __'),
                createTokenRow('__ bd __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __')
            ],
            baseLegend: {
                wa: { type: 'wall' },
                mt: { type: 'mapTransition', mapId: 'forest-path' },
                dr: { type: 'door' },
                __: { type: 'floor' }
            },
            propLegend: window.GameData?.createDungeonPropLegendFromTokens?.({
                bd: 'bed',
                tb: 'tableCandles',
                wr1: 'weaponRack1',
                wr2: 'weaponRack2',
                chI: 'chestClosedIron',
                chS: 'chestClosedSteel',
                ch1: {
                    propId: 'chestClosedGold',
                    lootMode: 'all',
                    goldAmount: 50,
                    lootItemIds: ['healers-circlet', 'mages-amulet', 'long-bow', 'small-shield']
                },
                ch2: {
                    propId: 'chestClosedGold',
                    lootMode: 'all',
                    goldAmount: 200,
                    lootItemIds: ['chain-mail']
                },
                br1: 'barrels1',
                br2: 'barrels2',
                cr: 'crate',
                st1: 'spikeTrap1',
                st2: 'spikeTrap2'
            }) || {},
            encounterLegend: {
                PW: { kind: 'player', characterId: 'warrior' },
                PZ: { kind: 'player', characterId: 'wizard' },
                PC: { kind: 'player', characterId: 'cleric' },
                PR: { kind: 'player', characterId: 'ranger' },
                GW: {
                    kind: 'enemy',
                    archetypeId: 'goblin-warrior',
                    lootMode: 'all',
                    lootItemIds: ['small-shield']
                },
                GW1: { kind: 'enemy', archetypeId: 'goblin-warrior'},
                GA: { kind: 'enemy', archetypeId: 'goblin-archer' },
                GS: { kind: 'enemy', archetypeId: 'goblin-shaman' },
                GB: { kind: 'enemy', archetypeId: 'goblin-brute' },
                GSP: {
                    kind: 'enemy',
                    archetypeId: 'giant-spider',
                    experiencePoints: 2000
                },
                GCB: { kind: 'enemy', archetypeId: 'goblin-chieftain' },
            }
        },

        forestPath: {
            id: 'forest-path',
            name: 'Forest Path',
            size: 20,
            layoutRows: [
                createTokenRow('wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa m3 m3 wa wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ DW __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ wa DW __ __ __ __ wa __ __ __ __ __ __ wa __ __ __ wa'),
                createTokenRow('wa __ wa __ __ wa __ __ wa __ wa wa __ wa __ __ __ wa __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ wa wa __ __ wa __ __ __ __ wa __ wa __ wa __ wa __ wa'),
                createTokenRow('wa __ __ __ wa __ __ __ wa __ __ __ __ __ __ wa __ __ __ wa'),
                createTokenRow('wa __ wa __ __ __ wa __ wa __ __ wa __ __ __ __ wa __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ wa __ __ __ __ __ wa'),
                createTokenRow('wa __ wa DW __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ DW __ __ __ __ __ __ __ wa __ __ __ wa __ __ __ wa'),
                createTokenRow('wa __ wa DW __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ wa __ __ __ __ __ wa __ __ __ __ wa __ __ __ __ __ wa'),
                createTokenRow('wa __ wa __ __ __ wa __ wa GS GS wa __ wa __ __ __ wa __ wa'),
                createTokenRow('wa __ __ __ wa __ __ __ __ GS GS __ __ __ __ __ __ wa __ wa'),
                createTokenRow('wa __ wa wa wa __ wa __ __ wa __ wa __ wa __ wa __ wa __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa wa wa wa wa wa wa wa wa mt mt wa wa wa wa wa wa wa wa wa')
            ],
            propRows: Array.from({ length: 20 }, () => createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __')),
            baseLegend: {
                wa: { type: 'wall' },
                mt: { type: 'mapTransition', mapId: 'goblin-cave' },
                m3: { type: 'mapTransition', mapId: 'forest-town' },
                __: { type: 'floor' }
            },
            propLegend: {},
            encounterLegend: { DW: {
                    kind: 'enemy',
                    archetypeId: 'dire-wolf',
                },
                GS: {
                    kind: 'enemy',
                    archetypeId: 'giant-spider',
                },    
            }
        },

        forestTown: {
            id: 'forest-town',
            name: 'Forest Town',
            size: 20,
            layoutRows: [
                createTokenRow('wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ wa wa wa wa wa wa __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ wa __ PR __ __ wa __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ wa PW PZ PC __ wa __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ wa __ __ __ __ wa __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ wa __ __ __ __ wa __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ wa wa dr dr wa wa __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa wa wa wa wa wa wa wa wa mt mt wa wa wa wa wa wa wa wa wa')
            ],
            propRows: [
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ bu __ __ __ __ __ __ __ __ __ __ __ tr __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ vn __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ tr __ __ __ __ __ __ __ __ __ __ __ __ __ bu __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __')
            ],
            baseLegend: {
                wa: { type: 'wall' },
                dr: { type: 'door' },
                mt: { type: 'mapTransition', mapId: 'forest-path' },
                __: { type: 'floor' }
            },
            propLegend: window.GameData?.createDungeonPropLegendFromTokens?.({
                bu: { propId: 'stoneDebris1', name: 'Bush', searchable: false, roomTheme: 'forest' },
                tr: { propId: 'stoneUrn', name: 'Tree', searchable: false, roomTheme: 'forest' },
                hs: { propId: 'workbench', name: 'House Furnishing', searchable: false, roomTheme: 'house' },
                vn: {
                    propId: 'tableSkull',
                    name: 'Mira the Vendor',
                    searchable: false,
                    roomTheme: 'town',
                    // Character tileset row 1, column 5 sprite (adventurer) for friendly NPC look.
                    spriteFrame: {
                        imagePath: 'character_tileset_1.png',
                        x: 550,
                        y: 2,
                        width: 114,
                        height: 148
                    },
                    isVendor: true,
                    vendorName: 'Mira the Vendor',
                    storeInventoryItemIds: [
                        'small-shield',
                        'long-bow',
                        'steel-helm  ',
                    ],
                    storeBuyMultiplier: 1,
                    storeSellMultiplier: 0.5
                }
            }) || {},
            encounterLegend: {
                PW: { kind: 'player', characterId: 'warrior' },
                PZ: { kind: 'player', characterId: 'wizard' },
                PC: { kind: 'player', characterId: 'cleric' },
                PR: { kind: 'player', characterId: 'ranger' }
            }
        }
    };
})();

// Dungeon map generation logic
window.MapGenerator = {

    generateDungeonMap() {
        // Dungeon tile types
        this.TILE_VOID = 0;
        this.TILE_FLOOR = 1;
        this.TILE_WALL = 2;
        this.TILE_DOOR = 3;

        const configuredLayout = this.getConfiguredDungeonLayout();
        if (configuredLayout) {
            return this.buildConfiguredDungeonMap(configuredLayout);
        }

        const VOID = this.TILE_VOID;
        const FLOOR = this.TILE_FLOOR;
        const WALL = this.TILE_WALL;

        const map = Array.from({ length: this.gridHeight }, () =>
            new Array(this.gridWidth).fill(VOID)
        );

        const rooms = [];

        this.carveDungeonRooms(map, rooms, FLOOR);
        this.connectDungeonRooms(map, rooms, FLOOR);
        this.paintDungeonWalls(map, VOID, FLOOR, WALL);

        return { map, rooms };
    },

    getConfiguredDungeonLayout() {
        const activeMapId = this.activeDungeonMapId || 'forest-town';
        return this.getConfiguredDungeonLayoutById(activeMapId)
            || window.CustomDungeonMaps?.forestTown
            || window.CustomDungeonMaps?.goblinCave
            || null;
    },

    getConfiguredDungeonLayoutById(mapId) {
        if (!mapId || !window.CustomDungeonMaps) {
            return null;
        }

        const normalizedMapId = String(mapId).trim();
        if (!normalizedMapId) {
            return null;
        }

        const configuredMaps = Object.values(window.CustomDungeonMaps)
            .filter((entry) => entry && typeof entry === 'object' && !Array.isArray(entry));

        const byId = configuredMaps.find((entry) => entry.id === normalizedMapId) || null;
        if (byId) {
            return byId;
        }

        const normalizedKey = normalizedMapId
            .replace(/-([a-z])/g, (_, letter) => letter.toUpperCase())
            .replace(/[^a-zA-Z0-9]/g, '');
        if (window.CustomDungeonMaps[normalizedKey]) {
            return window.CustomDungeonMaps[normalizedKey];
        }

        return null;
    },

    buildConfiguredDungeonMap(layoutConfig) {
        const VOID = this.TILE_VOID;
        const FLOOR = this.TILE_FLOOR;
        const WALL = this.TILE_WALL;
        const map = Array.from({ length: this.gridHeight }, () =>
            new Array(this.gridWidth).fill(VOID)
        );

        const normalized = this.normalizeConfiguredDungeonLayout(layoutConfig);
        const offsetX = Math.floor((this.gridWidth - normalized.size) / 2);
        const offsetY = Math.floor((this.gridHeight - normalized.size) / 2);
        const propPlacements = [];
        const enemyPlacements = [];
        const playerStarts = {};
        const baseCellTypes = {};
        const transitionCells = {};

        for (let rowIndex = 0; rowIndex < normalized.size; rowIndex++) {
            for (let columnIndex = 0; columnIndex < normalized.size; columnIndex++) {
                const gridX = offsetX + columnIndex;
                const gridY = offsetY + rowIndex;
                const cellKey = `${gridX},${gridY}`;
                const baseToken = normalized.baseRows[rowIndex][columnIndex];
                const baseCell = this.resolveConfiguredBaseCell(baseToken, VOID, FLOOR, WALL, normalized.baseLegend);
                map[gridY][gridX] = baseCell.tileValue;
                baseCellTypes[cellKey] = baseCell.baseType;
                if (baseCell.baseType === 'mapTransition') {
                    const transitionConfig = normalized.baseLegend[baseToken] || {};
                    if (transitionConfig.mapId) {
                        transitionCells[cellKey] = {
                            mapId: transitionConfig.mapId
                        };
                    }
                }

                const propToken = normalized.propRows[rowIndex][columnIndex];
                if (propToken !== '__') {
                    const propConfig = normalized.propLegend[propToken];
                    if (propConfig) {
                        propPlacements.push({
                            gridX,
                            gridY,
                            frameId: propConfig.frameId,
                            spriteFrame: propConfig.spriteFrame ? { ...propConfig.spriteFrame } : null,
                            name: propConfig.name,
                            roomTheme: propConfig.roomTheme || 'custom',
                            searchable: Boolean(propConfig.searchable),
                            isVendor: Boolean(propConfig.isVendor),
                            vendorName: propConfig.vendorName,
                            hasConfiguredStoreInventory: Array.isArray(propConfig.storeInventoryItemIds),
                            storeInventoryItemIds: cloneConfiguredVendorStockEntries(propConfig.storeInventoryItemIds),
                            storeBuyMultiplier: propConfig.storeBuyMultiplier,
                            storeSellMultiplier: propConfig.storeSellMultiplier,
                            hasBeenSearched: false,
                            lootMode: propConfig.lootMode,
                            goldAmount: propConfig.goldAmount,
                            lootItemIds: Array.isArray(propConfig.lootItemIds)
                                ? [...propConfig.lootItemIds]
                                : []
                        });
                    }
                }

                const encounterToken = normalized.encounterRows[rowIndex][columnIndex];
                if (encounterToken === '__') {
                    continue;
                }

                const encounterConfig = normalized.encounterLegend[encounterToken];
                if (!encounterConfig) {
                    continue;
                }

                if (encounterConfig.kind === 'player') {
                    playerStarts[encounterConfig.characterId] = { x: gridX, y: gridY };
                    continue;
                }

                enemyPlacements.push({
                    gridX,
                    gridY,
                    archetypeId: encounterConfig.archetypeId,
                    lootMode: encounterConfig.lootMode,
                    goldAmount: encounterConfig.goldAmount,
                    lootItemIds: Array.isArray(encounterConfig.lootItemIds)
                        ? [...encounterConfig.lootItemIds]
                        : [],
                    experiencePoints: encounterConfig.experiencePoints
                });
            }
        }

        return {
            map,
            rooms: [],
            layout: {
                id: normalized.id,
                name: normalized.name,
                size: normalized.size,
                offsetX,
                offsetY,
                playerStarts,
                enemyPlacements,
                propPlacements,
                baseCellTypes,
                transitionCells
            }
        };
    },

    normalizeConfiguredDungeonLayout(layoutConfig) {
        const baseRowsFromConfig = (layoutConfig.baseRows || []).map((row) => {
            if (Array.isArray(row)) {
                return [...row];
            }

            if (typeof row === 'string') {
                return row.split('');
            }

            return [];
        });

        const layoutRows = (layoutConfig.layoutRows || []).map((row) => Array.isArray(row) ? [...row] : []);
        const hasCombinedLayout = layoutRows.length > 0;
        const baseLegend = layoutConfig.baseLegend || {};
        const encounterLegend = layoutConfig.encounterLegend || {};

        const splitCombinedToken = (token) => {
            const normalizedToken = String(token || '__');
            if (!normalizedToken.includes('|')) {
                if (
                    Object.prototype.hasOwnProperty.call(encounterLegend, normalizedToken)
                    && !Object.prototype.hasOwnProperty.call(baseLegend, normalizedToken)
                ) {
                    return {
                        baseToken: '__',
                        encounterToken: normalizedToken
                    };
                }

                return {
                    baseToken: normalizedToken,
                    encounterToken: '__'
                };
            }

            const [basePart, encounterPart] = normalizedToken.split('|');
            return {
                baseToken: basePart || '__',
                encounterToken: encounterPart || '__'
            };
        };

        const baseRows = hasCombinedLayout
            ? layoutRows.map((row) => row.map((token) => splitCombinedToken(token).baseToken))
            : baseRowsFromConfig;
        const encounterRowsFromCombined = hasCombinedLayout
            ? layoutRows.map((row) => row.map((token) => splitCombinedToken(token).encounterToken))
            : [];

        const size = layoutConfig.size || (hasCombinedLayout ? layoutRows.length : baseRows.length);
        const propRows = (layoutConfig.propRows || []).map((row) => Array.isArray(row) ? row : []);
        const encounterRows = hasCombinedLayout
            ? encounterRowsFromCombined
            : (layoutConfig.encounterRows || []).map((row) => Array.isArray(row) ? row : []);

        const hasValidSquareBase =
            baseRows.length === size &&
            baseRows.every((row) => row.length === size);
        const hasValidPropLayer =
            propRows.length === size &&
            propRows.every((row) => row.length === size);
        const hasValidEncounterLayer =
            encounterRows.length === size &&
            encounterRows.every((row) => row.length === size);

        if (!hasValidSquareBase || !hasValidPropLayer || !hasValidEncounterLayer) {
            throw new Error(`Invalid configured dungeon layout: ${layoutConfig.id || 'unknown-layout'}`);
        }

        if (size > this.gridWidth || size > this.gridHeight) {
            throw new Error(`Configured dungeon layout is too large for the active grid: ${size}`);
        }

        return {
            id: layoutConfig.id || 'configured-layout',
            name: layoutConfig.name || 'Configured Layout',
            size,
            baseRows,
            propRows,
            encounterRows,
            baseLegend: layoutConfig.baseLegend || {},
            propLegend: layoutConfig.propLegend || {},
            encounterLegend: layoutConfig.encounterLegend || {}
        };
    },

    resolveConfiguredBaseCell(token, VOID, FLOOR, WALL, baseLegend = {}) {
        const baseType = baseLegend[token]?.type;

        switch (baseType || token) {
            case 'wall':
            case '#':
                return { tileValue: WALL, baseType: 'wall' };
            case 'door':
            case '+':
                // Doors are authored distinctly even though they currently behave like floor tiles.
                return { tileValue: FLOOR, baseType: 'door' };
            case 'mapTransition':
            case 'mt':
                return { tileValue: FLOOR, baseType: 'mapTransition' };
            case 'floor':
            case '.':
                return { tileValue: FLOOR, baseType: 'floor' };
            case 'void':
            case '~':
            default:
                return { tileValue: VOID, baseType: 'void' };
        }
    },

    carveDungeonRooms(map, rooms, FLOOR) {
        for (let attempt = 0; attempt < 120; attempt++) {
            const rw = 5 + Math.floor(Math.random() * 12);
            const rh = 4 + Math.floor(Math.random() * 9);
            const rx = 2 + Math.floor(Math.random() * (this.gridWidth - rw - 4));
            const ry = 2 + Math.floor(Math.random() * (this.gridHeight - rh - 4));

            const overlaps = rooms.some((room) =>
                rx < room.x + room.w + 2 && rx + rw + 2 > room.x &&
                ry < room.y + room.h + 2 && ry + rh + 2 > room.y
            );
            if (overlaps) {
                continue;
            }

            rooms.push({ x: rx, y: ry, w: rw, h: rh });

            for (let y = ry; y < ry + rh; y++) {
                for (let x = rx; x < rx + rw; x++) {
                    map[y][x] = FLOOR;
                }
            }
        }
    },

    connectDungeonRooms(map, rooms, FLOOR) {
        for (let i = 1; i < rooms.length; i++) {
            const a = rooms[i - 1];
            const b = rooms[i];
            const ax = Math.floor(a.x + a.w / 2);
            const ay = Math.floor(a.y + a.h / 2);
            const bx = Math.floor(b.x + b.w / 2);
            const by = Math.floor(b.y + b.h / 2);

            const hMin = Math.min(ax, bx);
            const hMax = Math.max(ax, bx);
            for (let x = hMin; x <= hMax; x++) {
                map[ay][x] = FLOOR;
                if (ay + 1 < this.gridHeight) {
                    map[ay + 1][x] = FLOOR;
                }
            }

            const vMin = Math.min(ay, by);
            const vMax = Math.max(ay, by);
            for (let y = vMin; y <= vMax; y++) {
                map[y][bx] = FLOOR;
                if (bx + 1 < this.gridWidth) {
                    map[y][bx + 1] = FLOOR;
                }
            }
        }
    },

    paintDungeonWalls(map, VOID, FLOOR, WALL) {
        for (let y = 0; y < this.gridHeight; y++) {
            for (let x = 0; x < this.gridWidth; x++) {
                if (map[y][x] !== VOID) {
                    continue;
                }
                if (this.hasAdjacentFloor(map, x, y, FLOOR)) {
                    map[y][x] = WALL;
                }
            }
        }
    },

    hasAdjacentFloor(map, x, y, FLOOR) {
        for (let dy = -1; dy <= 1; dy++) {
            for (let dx = -1; dx <= 1; dx++) {
                if (dx === 0 && dy === 0) {
                    continue;
                }
                const ny = y + dy;
                const nx = x + dx;
                if (
                    ny >= 0 && ny < this.gridHeight &&
                    nx >= 0 && nx < this.gridWidth &&
                    map[ny][nx] === FLOOR
                ) {
                    return true;
                }
            }
        }
        return false;
    }
};

