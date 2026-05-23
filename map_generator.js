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
        return window.CustomDungeonMaps?.starterKeep || null;
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

        for (let rowIndex = 0; rowIndex < normalized.size; rowIndex++) {
            for (let columnIndex = 0; columnIndex < normalized.size; columnIndex++) {
                const gridX = offsetX + columnIndex;
                const gridY = offsetY + rowIndex;
                const cellKey = `${gridX},${gridY}`;
                const baseToken = normalized.baseRows[rowIndex][columnIndex];
                const baseCell = this.resolveConfiguredBaseCell(baseToken, VOID, FLOOR, WALL, normalized.baseLegend);
                map[gridY][gridX] = baseCell.tileValue;
                baseCellTypes[cellKey] = baseCell.baseType;

                const propToken = normalized.propRows[rowIndex][columnIndex];
                if (propToken !== '__') {
                    const propConfig = normalized.propLegend[propToken];
                    if (propConfig) {
                        propPlacements.push({
                            gridX,
                            gridY,
                            frameId: propConfig.frameId,
                            roomTheme: propConfig.roomTheme || 'custom',
                            searchable: Boolean(propConfig.searchable),
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
                baseCellTypes
            }
        };
    },

    normalizeConfiguredDungeonLayout(layoutConfig) {
        const baseRows = (layoutConfig.baseRows || []).map((row) => {
            if (Array.isArray(row)) {
                return [...row];
            }

            if (typeof row === 'string') {
                return row.split('');
            }

            return [];
        });
        const size = layoutConfig.size || baseRows.length;
        const propRows = (layoutConfig.propRows || []).map((row) => Array.isArray(row) ? row : []);
        const encounterRows = (layoutConfig.encounterRows || []).map((row) => Array.isArray(row) ? row : []);

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
