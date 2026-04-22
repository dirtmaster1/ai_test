// Dungeon map generation logic
window.MapGenerator = {

    generateDungeonMap() {
        // Dungeon tile types
        this.TILE_VOID = 0;
        this.TILE_FLOOR = 1;
        this.TILE_WALL = 2;

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
