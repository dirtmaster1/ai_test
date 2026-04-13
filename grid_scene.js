// Dungeon Crawler - Grid Scene
class GridScene {
    constructor(containerId = 'gameContainer') {
        // Grid settings
        this.gridWidth = 100;
        this.gridHeight = 100;
        this.viewWidth = 15;
        this.viewHeight = 10;
        this.cellSize = 64;

        // Create container
        this.container = document.getElementById(containerId);
        if (!this.container) {
            this.container = document.createElement('div');
            this.container.id = containerId;
            document.body.appendChild(this.container);
        }

        // Scene, Camera, Renderer setup
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(0x000000);

        const viewW = this.viewWidth * this.cellSize;
        const viewH = this.viewHeight * this.cellSize;

        // Orthographic camera for 2D-like view (shows 15x10 viewport)
        this.camera = new THREE.OrthographicCamera(
            -viewW / 2,
            viewW / 2,
            viewH / 2,
            -viewH / 2,
            0.1,
            1000
        );
        this.camera.position.z = 500;

        // Renderer
        this.renderer = new THREE.WebGLRenderer({ antialias: true });
        this.renderer.setSize(viewW, viewH);
        this.renderer.setPixelRatio(window.devicePixelRatio);
        this.container.appendChild(this.renderer.domElement);

        this.wizard = this.createCharacter({
            id: 'wizard',
            name: 'Blue Wizard',
            role: 'Player',
            team: 'player',
            accentColor: '#4f86ff',
            pointerColor: 0x00eeff,
            spriteRows: this.getWizardSpriteRows(),
            portraitLabel: 'WZ'
        });

        this.dwarf = this.createCharacter({
            id: 'dwarf-warrior',
            name: 'Dwarven Warrior',
            role: 'Player',
            team: 'player',
            accentColor: '#c78a3b',
            pointerColor: 0xffb347,
            spriteRows: this.getDwarfSpriteRows(),
            portraitLabel: 'DW'
        });

        this.goblin = this.createCharacter({
            id: 'goblin-warrior',
            name: 'Goblin Warrior',
            role: 'AI',
            team: 'ai',
            accentColor: '#d34c4c',
            pointerColor: 0xff6600,
            spriteRows: this.getGoblinSpriteRows(),
            portraitLabel: 'GB'
        });

        this.characters = [this.wizard, this.dwarf, this.goblin];
        this.playerParty = [this.wizard, this.dwarf];
        this.aiParty = [this.goblin];
        this.playerActiveIndex = 0;
        this.aiActionIndex = 0;
        this.characterHud = new Map();

        // Turn system
        this.currentTurn = 'player';
        this.movesThisTurn = 0;
        this.maxMovesPerTurn = 5;
        this.goblinMoveTimer = 0;
        this.isGameOver = false;
        this.gameOutcome = null;
        this.victoryStartTime = 0;
        this.victoryFadeDurationMs = 3000;
        this.restartTriggered = false;

        // Dungeon tile types
        this.TILE_VOID = 0;
        this.TILE_FLOOR = 1;
        this.TILE_WALL = 2;

        // Generate dungeon and place characters inside rooms
        const dungeon = this.generateDungeonMap();
        this.dungeonMap = dungeon.map;
        this.placeCharacters(dungeon.rooms);

        // Input handling
        this.keysPressed = {};
        this.setupInputListeners();

        // Setup scene
        this.setupGrid();
        this.setupCharacters();
        this.setupUI();
        this.setupAttackListener();
        this.updateCamera();

        // Start animation loop
        this.animate();
    }

    createCharacter(config) {
        return {
            id: config.id,
            name: config.name,
            role: config.role,
            team: config.team,
            accentColor: config.accentColor,
            pointerColor: config.pointerColor,
            spriteRows: config.spriteRows,
            portraitLabel: config.portraitLabel,
            gridX: 0,
            gridY: 0,
            mesh: null,
            baseColorHex: 0xffffff,
            facing: 'right',
            directionPointer: null,
            hitPoints: 10,
            maxHitPoints: 10,
            attackDamage: 5,
            attackCost: 3,
            hitAnimEndTime: 0,
            isDead: false,
            fadeFrames: 0,
            removedFromScene: false
        };
    }

    getCellKey(gridX, gridY) {
        return `${gridX},${gridY}`;
    }

    placeCharacters(rooms) {
        if (rooms.length < 1) {
            return;
        }

        const startRoom = rooms[0];
        this.wizard.gridX = Math.floor(startRoom.x + startRoom.w / 2);
        this.wizard.gridY = Math.floor(startRoom.y + startRoom.h / 2);

        const occupiedCells = new Set([
            this.getCellKey(this.wizard.gridX, this.wizard.gridY)
        ]);

        const dwarfSpawn = this.findNearbyFloorTile(
            this.wizard.gridX,
            this.wizard.gridY,
            1,
            3,
            occupiedCells
        );
        if (dwarfSpawn) {
            this.dwarf.gridX = dwarfSpawn.x;
            this.dwarf.gridY = dwarfSpawn.y;
            occupiedCells.add(this.getCellKey(dwarfSpawn.x, dwarfSpawn.y));
        } else {
            this.dwarf.gridX = this.wizard.gridX;
            this.dwarf.gridY = Math.min(this.gridHeight - 1, this.wizard.gridY + 1);
            occupiedCells.add(this.getCellKey(this.dwarf.gridX, this.dwarf.gridY));
        }

        const goblinSpawn = this.findNearbyFloorTile(
            this.wizard.gridX,
            this.wizard.gridY,
            2,
            5,
            occupiedCells
        );
        if (goblinSpawn) {
            this.goblin.gridX = goblinSpawn.x;
            this.goblin.gridY = goblinSpawn.y;
            return;
        }

        const fallbackRoom = rooms[rooms.length - 1];
        this.goblin.gridX = Math.floor(fallbackRoom.x + fallbackRoom.w / 2);
        this.goblin.gridY = Math.floor(fallbackRoom.y + fallbackRoom.h / 2);
    }

    findNearbyFloorTile(originX, originY, minDistance, maxDistance, occupiedCells = new Set()) {
        const candidates = [];
        for (let y = originY - maxDistance; y <= originY + maxDistance; y++) {
            for (let x = originX - maxDistance; x <= originX + maxDistance; x++) {
                if (x < 0 || x >= this.gridWidth || y < 0 || y >= this.gridHeight) {
                    continue;
                }
                if (this.dungeonMap[y][x] !== this.TILE_FLOOR) {
                    continue;
                }

                const distance = Math.max(Math.abs(x - originX), Math.abs(y - originY));
                if (distance < minDistance || distance > maxDistance) {
                    continue;
                }

                if (occupiedCells.has(this.getCellKey(x, y))) {
                    continue;
                }

                candidates.push({ x, y });
            }
        }

        if (candidates.length === 0) {
            return null;
        }

        return candidates[Math.floor(Math.random() * candidates.length)];
    }

    setupGrid() {
        const TILE_RES = 16;
        const canvasW = this.gridWidth * TILE_RES;
        const canvasH = this.gridHeight * TILE_RES;
        const canvas = document.createElement('canvas');
        canvas.width = canvasW;
        canvas.height = canvasH;
        const ctx = canvas.getContext('2d');

        for (let cy = 0; cy < this.gridHeight; cy++) {
            for (let cx = 0; cx < this.gridWidth; cx++) {
                const tile = this.dungeonMap[cy][cx];
                this.drawDungeonTile(ctx, tile, cx * TILE_RES, cy * TILE_RES, TILE_RES, cx, cy);
            }
        }

        const texture = new THREE.CanvasTexture(canvas);
        texture.magFilter = THREE.NearestFilter;
        texture.minFilter = THREE.NearestFilter;

        const worldW = this.gridWidth * this.cellSize;
        const worldH = this.gridHeight * this.cellSize;
        const geo = new THREE.PlaneGeometry(worldW, worldH);
        const mat = new THREE.MeshBasicMaterial({ map: texture });
        const mesh = new THREE.Mesh(geo, mat);
        mesh.position.z = -2;
        this.scene.add(mesh);
    }

    drawDungeonTile(ctx, tileType, px, py, T, cx, cy) {
        if (tileType === this.TILE_VOID) {
            ctx.fillStyle = '#000000';
            ctx.fillRect(px, py, T, T);
        } else if (tileType === this.TILE_WALL) {
            this.drawWallTile(ctx, px, py, T, cx, cy);
        } else {
            this.drawFloorTile(ctx, px, py, T, cx, cy);
        }
    }

    drawWallTile(ctx, px, py, T, cx, cy) {
        const H = T >> 1;

        ctx.fillStyle = '#0e1218';
        ctx.fillRect(px, py, T, T);

        ctx.fillStyle = '#07080f';
        ctx.fillRect(px, py + H, T, 1);
        if ((cx + cy) % 2 === 0) {
            ctx.fillRect(px + H, py, 1, H);
        } else {
            ctx.fillRect(px + H, py + H + 1, 1, H - 1);
        }

        ctx.fillStyle = 'rgba(100, 140, 220, 0.09)';
        ctx.fillRect(px, py, T, 2);
        ctx.fillStyle = 'rgba(80, 110, 190, 0.05)';
        ctx.fillRect(px, py + 2, 2, H - 2);

        ctx.fillStyle = 'rgba(0, 0, 0, 0.70)';
        ctx.fillRect(px, py + T - 2, T, 2);
        ctx.fillStyle = 'rgba(0, 0, 0, 0.50)';
        ctx.fillRect(px + T - 2, py, 2, T);

        const s = (cx * 1664525 + cy * 214013) >>> 0;
        if (s % 9 < 2) {
            ctx.fillStyle = 'rgba(255,255,255,0.025)';
            ctx.fillRect(px + 1 + (s % (H - 2)), py + 1 + ((s >> 8) % (H - 2)), H - 2, H - 2);
        }
        if (s % 11 === 3) {
            ctx.fillStyle = 'rgba(0,0,0,0.25)';
            ctx.fillRect(px + 1 + (s % (T - 3)), py + 1 + ((s >> 4) % (T - 3)), 2, 2);
        }
    }

    drawFloorTile(ctx, px, py, T, cx, cy) {
        const s = (cx * 1664525 ^ cy * 214013 ^ 0xDEAD) >>> 0;
        const v = s % 5;

        ctx.fillStyle = `rgb(${22 + v * 2},${17 + v},12)`;
        ctx.fillRect(px, py, T, T);

        ctx.fillStyle = 'rgba(0,0,0,0.60)';
        ctx.fillRect(px, py, T, 1);
        ctx.fillRect(px, py, 1, T);

        ctx.fillStyle = 'rgba(255,220,140,0.055)';
        ctx.fillRect(px + 1, py + 1, T - 2, 1);
        ctx.fillRect(px + 1, py + 2, 1, T - 3);

        ctx.fillStyle = 'rgba(0,0,0,0.35)';
        ctx.fillRect(px + 1, py + T - 2, T - 2, 1);
        ctx.fillRect(px + T - 2, py + 1, 1, T - 2);

        if (s % 8 === 0) {
            ctx.fillStyle = 'rgba(0,0,0,0.45)';
            const crackX = px + 2 + (s % (T - 5));
            const crackY = py + 2 + ((s >> 8) % (T - 5));
            ctx.fillRect(crackX, crackY, 4, 1);
            ctx.fillRect(crackX + 3, crackY, 1, 3);
        }

        if (s % 13 === 5) {
            ctx.fillStyle = 'rgba(0,0,0,0.50)';
            ctx.fillRect(px + 2 + (s % (T - 5)), py + 2 + ((s >> 5) % (T - 5)), 2, 2);
        }
    }

    generateDungeonMap() {
        const VOID = this.TILE_VOID;
        const FLOOR = this.TILE_FLOOR;
        const WALL = this.TILE_WALL;

        const map = Array.from({ length: this.gridHeight }, () =>
            new Array(this.gridWidth).fill(VOID)
        );

        const rooms = [];

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

        for (let y = 0; y < this.gridHeight; y++) {
            for (let x = 0; x < this.gridWidth; x++) {
                if (map[y][x] !== VOID) {
                    continue;
                }
                let nearFloor = false;
                outer:
                for (let dy = -1; dy <= 1; dy++) {
                    for (let dx = -1; dx <= 1; dx++) {
                        if (dx === 0 && dy === 0) {
                            continue;
                        }
                        const ny = y + dy;
                        const nx = x + dx;
                        if (ny >= 0 && ny < this.gridHeight && nx >= 0 && nx < this.gridWidth && map[ny][nx] === FLOOR) {
                            nearFloor = true;
                            break outer;
                        }
                    }
                }
                if (nearFloor) {
                    map[y][x] = WALL;
                }
            }
        }

        return { map, rooms };
    }

    isObstacle(gridX, gridY) {
        if (gridX < 0 || gridX >= this.gridWidth || gridY < 0 || gridY >= this.gridHeight) {
            return true;
        }
        return this.dungeonMap[gridY][gridX] !== this.TILE_FLOOR;
    }

    isOccupied(gridX, gridY, excludedCharacter = null) {
        return this.characters.some((character) =>
            character !== excludedCharacter &&
            !character.isDead &&
            !character.removedFromScene &&
            character.gridX === gridX &&
            character.gridY === gridY
        );
    }

    createSpriteTexture(rows) {
        const canvas = document.createElement('canvas');
        canvas.width = 16;
        canvas.height = 16;
        const ctx = canvas.getContext('2d');
        ctx.clearRect(0, 0, 16, 16);
        rows.forEach((row, y) => {
            row.forEach((color, x) => {
                if (color !== null) {
                    ctx.fillStyle = color;
                    ctx.fillRect(x, y, 1, 1);
                }
            });
        });
        const texture = new THREE.CanvasTexture(canvas);
        texture.magFilter = THREE.NearestFilter;
        texture.minFilter = THREE.NearestFilter;
        return texture;
    }

    getWizardSpriteRows() {
        const _ = null;
        const DH = '#0a2f7a';
        const LH = '#1a5cc8';
        const GS = '#ffd700';
        const SK = '#ffbb88';
        const EY = '#1a0800';
        const WB = '#d0d0d0';
        const RB = '#1050b0';
        const RL = '#3a7ee0';
        const ST = '#5d3a28';
        return [
            [_, _, _, _, _, _, DH, DH, DH, _, _, _, _, _, _, _],
            [_, _, _, _, _, DH, DH, LH, DH, DH, _, _, _, _, _, _],
            [_, _, _, _, DH, DH, LH, DH, DH, DH, DH, _, _, _, _, _],
            [_, _, _, DH, DH, GS, DH, DH, DH, DH, DH, _, _, _, _, _],
            [_, _, DH, DH, DH, DH, DH, DH, DH, DH, DH, DH, _, _, _, _],
            [_, _, DH, DH, LH, DH, DH, DH, DH, LH, DH, DH, _, _, _, _],
            [_, _, _, SK, SK, SK, SK, SK, SK, SK, SK, _, _, _, _, _],
            [_, _, _, SK, EY, SK, SK, EY, SK, SK, SK, _, _, _, _, _],
            [_, _, _, SK, SK, SK, SK, SK, SK, SK, SK, _, _, _, _, _],
            [_, ST, WB, WB, WB, WB, WB, WB, WB, WB, WB, _, _, _, _, _],
            [_, ST, WB, RB, RB, RB, RB, RB, RB, RB, WB, _, _, _, _, _],
            [_, ST, WB, RB, RL, RB, RB, RL, RB, RB, WB, _, _, _, _, _],
            [GS, ST, WB, RB, RB, RB, RB, RB, RB, RB, WB, _, _, _, _, _],
            [_, _, WB, RB, RB, RB, RB, RB, RB, RB, WB, _, _, _, _, _],
            [_, _, WB, WB, RB, RB, RB, RB, RB, WB, WB, _, _, _, _, _],
            [_, _, _, WB, WB, RB, RB, RB, WB, WB, _, _, _, _, _, _]
        ];
    }

    getDwarfSpriteRows() {
        const _ = null;
        const HM = '#5f6878';
        const HH = '#9ea8ba';
        const SK = '#d8a272';
        const EY = '#170d08';
        const BR = '#6b3c1d';
        const BH = '#b0682f';
        const AR = '#6d4c34';
        const AH = '#9b7854';
        const ST = '#3d2718';
        const AX = '#cfd5de';
        const SH = '#818998';
        const BT = '#21150f';
        return [
            [_, _, _, _, _, _, HH, HH, HH, _, _, _, _, _, _, _],
            [_, _, _, _, _, HM, HM, HH, HM, HM, _, _, _, _, _, _],
            [_, _, _, _, HM, HM, HM, HM, HM, HM, HM, _, _, _, _, _],
            [_, _, _, HM, HH, HM, HM, HM, HM, HH, HM, _, _, _, _, _],
            [_, _, HM, HM, HM, HM, HM, HM, HM, HM, HM, HM, _, _, _, _],
            [_, _, _, SK, SK, SK, SK, SK, SK, SK, SK, _, _, _, _, _],
            [_, _, _, SK, EY, SK, BH, BH, SK, EY, SK, _, _, _, _, _],
            [_, _, _, SK, BH, BH, BR, BR, BH, BH, SK, _, _, _, _, _],
            [_, AX, SH, BR, BR, BR, BR, BR, BR, BR, BR, _, _, _, _, _],
            [_, ST, AR, AR, AH, AR, AR, AH, AR, AR, AR, _, _, _, _, _],
            [_, ST, AR, AH, AR, AR, AR, AR, AH, AR, AR, _, _, _, _, _],
            [AX, ST, AR, AR, AR, AR, AR, AR, AR, AR, AR, _, _, _, _, _],
            [_, _, AR, AR, AR, AR, AR, AR, AR, AR, AR, _, _, _, _, _],
            [_, _, AR, AR, _, _, _, _, AR, AR, _, _, _, _, _, _],
            [_, _, BT, BT, _, _, _, _, BT, BT, _, _, _, _, _, _],
            [_, BT, BT, _, _, _, _, _, _, BT, BT, _, _, _, _, _]
        ];
    }

    getGoblinSpriteRows() {
        const _ = null;
        const GR = '#4aaa30';
        const DG = '#2a6618';
        const YE = '#ffee00';
        const BK = '#0a0800';
        const TB = '#fffacc';
        const IR = '#5a5a7a';
        const LI = '#9090b8';
        const RU = '#252535';
        const BR = '#8c4a20';
        const LG = '#3a1e0e';
        const BT = '#181010';
        const SW = '#d4d4e8';
        const SH = '#707080';
        return [
            [_, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _],
            [_, _, _, _, GR, GR, GR, GR, GR, _, _, _, _, _, _, _],
            [_, _, DG, GR, GR, GR, GR, GR, GR, GR, DG, _, _, _, _, _],
            [_, _, _, GR, YE, GR, GR, GR, YE, GR, _, _, _, _, _, _],
            [_, _, _, GR, GR, GR, DG, GR, GR, GR, _, _, _, _, _, _],
            [_, _, _, GR, TB, BK, BK, BK, TB, GR, _, _, _, _, _, _],
            [_, _, _, IR, IR, IR, IR, IR, IR, IR, _, _, _, _, _, _],
            [SW, SH, IR, LI, IR, IR, RU, IR, IR, LI, IR, _, _, _, _, _],
            [_, SW, IR, IR, IR, BR, BR, IR, IR, IR, _, _, _, _, _, _],
            [_, SW, GR, IR, IR, IR, IR, IR, IR, GR, _, _, _, _, _, _],
            [_, _, GR, _, LG, LG, LG, LG, _, GR, _, _, _, _, _, _],
            [_, _, GR, _, LG, LG, LG, LG, _, GR, _, _, _, _, _, _],
            [_, _, _, _, LG, LG, _, LG, LG, _, _, _, _, _, _, _],
            [_, _, _, _, LG, LG, _, LG, LG, _, _, _, _, _, _, _],
            [_, _, _, _, BT, BT, _, BT, BT, _, _, _, _, _, _, _],
            [_, _, _, BT, BT, _, _, _, BT, BT, _, _, _, _, _, _]
        ];
    }

    createPortraitCanvas(rows, accentColor, label) {
        const canvas = document.createElement('canvas');
        canvas.width = 72;
        canvas.height = 72;
        const ctx = canvas.getContext('2d');

        ctx.fillStyle = '#0d0d0d';
        ctx.fillRect(0, 0, 72, 72);

        const bgGradient = ctx.createLinearGradient(0, 0, 72, 72);
        bgGradient.addColorStop(0, this.hexToRgba(accentColor, 0.36));
        bgGradient.addColorStop(1, 'rgba(12, 10, 8, 0.96)');
        ctx.fillStyle = bgGradient;
        ctx.fillRect(4, 4, 64, 64);

        ctx.strokeStyle = accentColor;
        ctx.lineWidth = 2;
        ctx.strokeRect(5, 5, 62, 62);

        ctx.fillStyle = 'rgba(255,255,255,0.04)';
        for (let y = 0; y < 64; y += 8) {
            ctx.fillRect(4, 4 + y, 64, 1);
        }

        const pixelSize = 3;
        const offsetX = 12;
        const offsetY = 10;
        rows.forEach((row, y) => {
            row.forEach((color, x) => {
                if (color !== null) {
                    ctx.fillStyle = color;
                    ctx.fillRect(offsetX + x * pixelSize, offsetY + y * pixelSize, pixelSize, pixelSize);
                }
            });
        });

        ctx.fillStyle = 'rgba(0, 0, 0, 0.55)';
        ctx.fillRect(6, 54, 60, 12);
        ctx.fillStyle = accentColor;
        ctx.font = 'bold 9px Georgia';
        ctx.textAlign = 'right';
        ctx.fillText(label, 62, 63);

        return canvas;
    }

    setupCharacters() {
        this.characters.forEach((character) => {
            this.setupCharacterSprite(
                character,
                this.createSpriteTexture(character.spriteRows),
                character.pointerColor
            );
        });
    }

    setupCharacterSprite(character, texture, pointerColor) {
        const size = this.cellSize - 4;
        const geometry = new THREE.PlaneGeometry(size, size);
        const material = new THREE.MeshBasicMaterial({ map: texture, transparent: true, color: 0xffffff });
        character.mesh = new THREE.Mesh(geometry, material);
        character.directionPointer = this.createDirectionPointer(pointerColor);
        character.mesh.add(character.directionPointer);
        this.updateCharacterFacing(character, character.facing);
        this.updateCharacterPosition(character);
        this.scene.add(character.mesh);
    }

    updateCharacterPosition(character) {
        if (!character.mesh) {
            return;
        }

        const worldW = this.gridWidth * this.cellSize;
        const worldH = this.gridHeight * this.cellSize;
        const x = (character.gridX * this.cellSize + this.cellSize / 2) - worldW / 2;
        const y = (worldH / 2) - (character.gridY * this.cellSize + this.cellSize / 2);
        character.mesh.position.set(x, y, 0);
    }

    getLivingCharacters(group) {
        return group.filter((character) => !character.isDead);
    }

    getActivePlayerCharacter() {
        const livingPlayers = this.getLivingCharacters(this.playerParty);
        if (livingPlayers.length === 0) {
            return null;
        }

        const selected = this.playerParty[this.playerActiveIndex];
        if (selected && !selected.isDead) {
            return selected;
        }

        this.playerActiveIndex = this.playerParty.indexOf(livingPlayers[0]);
        return livingPlayers[0];
    }

    cycleActivePlayerCharacter(direction = 1) {
        const livingPlayers = this.getLivingCharacters(this.playerParty);
        if (livingPlayers.length <= 1) {
            return;
        }

        const activeCharacter = this.getActivePlayerCharacter();
        const activeIndex = livingPlayers.indexOf(activeCharacter);
        const nextIndex = (activeIndex + direction + livingPlayers.length) % livingPlayers.length;
        this.playerActiveIndex = this.playerParty.indexOf(livingPlayers[nextIndex]);
        this.updateCamera();
    }

    setActivePlayerByPartyIndex(index) {
        const candidate = this.playerParty[index];
        if (!candidate || candidate.isDead) {
            return;
        }
        this.playerActiveIndex = index;
        this.updateCamera();
    }

    updateCamera() {
        const focusCharacter = this.getActivePlayerCharacter() || this.getLivingCharacters(this.aiParty)[0] || this.characters[0];
        if (!focusCharacter) {
            return;
        }

        const worldW = this.gridWidth * this.cellSize;
        const worldH = this.gridHeight * this.cellSize;
        this.camera.position.x = (focusCharacter.gridX * this.cellSize + this.cellSize / 2) - worldW / 2;
        this.camera.position.y = (worldH / 2) - (focusCharacter.gridY * this.cellSize + this.cellSize / 2);
    }

    createDirectionPointer(color) {
        const pointerShape = new THREE.Shape();
        pointerShape.moveTo(8, 0);
        pointerShape.lineTo(-6, 5);
        pointerShape.lineTo(-6, -5);
        pointerShape.closePath();

        const pointerGeometry = new THREE.ShapeGeometry(pointerShape);
        const pointerMaterial = new THREE.MeshBasicMaterial({ color });
        const pointerMesh = new THREE.Mesh(pointerGeometry, pointerMaterial);
        pointerMesh.position.set(0, 0, 1);

        return pointerMesh;
    }

    updateCharacterFacing(character, facing) {
        if (!character || !character.directionPointer) {
            return;
        }

        character.facing = facing;

        const rotationByDirection = {
            right: 0,
            down: -Math.PI / 2,
            left: Math.PI,
            up: Math.PI / 2
        };

        character.directionPointer.rotation.z = rotationByDirection[facing] ?? 0;
    }

    faceCharacterToward(attacker, target) {
        const dx = target.gridX - attacker.gridX;
        const dy = target.gridY - attacker.gridY;

        if (Math.abs(dx) >= Math.abs(dy)) {
            this.updateCharacterFacing(attacker, dx >= 0 ? 'right' : 'left');
            return;
        }

        this.updateCharacterFacing(attacker, dy >= 0 ? 'down' : 'up');
    }

    setupUI() {
        const hudRoot = document.getElementById('battleHud') || this.container;
        hudRoot.style.pointerEvents = 'none';
        hudRoot.innerHTML = '';

        const playerSection = this.createPartySection('Player Party', 'Controlled units');
        const enemySection = this.createPartySection('Enemy Party', 'Hostiles');
        hudRoot.appendChild(playerSection.section);
        hudRoot.appendChild(enemySection.section);

        this.playerParty.forEach((character) => {
            const card = this.createCombatCard(character);
            playerSection.content.appendChild(card.card);
            this.characterHud.set(character.id, card);
        });

        this.aiParty.forEach((character) => {
            const card = this.createCombatCard(character);
            enemySection.content.appendChild(card.card);
            this.characterHud.set(character.id, card);
        });

        this.victoryText = document.createElement('div');
        this.victoryText.id = 'victoryText';
        this.victoryText.textContent = 'Victory!';
        this.victoryText.style.position = 'absolute';
        this.victoryText.style.top = '50%';
        this.victoryText.style.left = '50%';
        this.victoryText.style.transform = 'translate(-50%, -50%)';
        this.victoryText.style.fontFamily = 'Arial, sans-serif';
        this.victoryText.style.fontSize = '96px';
        this.victoryText.style.fontWeight = '900';
        this.victoryText.style.letterSpacing = '4px';
        this.victoryText.style.color = '#ffd700';
        this.victoryText.style.textShadow = '0 0 16px rgba(255, 215, 0, 0.6), 0 0 32px rgba(255, 140, 0, 0.45)';
        this.victoryText.style.pointerEvents = 'none';
        this.victoryText.style.opacity = '0';
        this.victoryText.style.display = 'none';
        this.victoryText.style.zIndex = '20';

        this.container.style.position = 'relative';
        this.container.appendChild(this.victoryText);
    }

    createPartySection(title, subtitle) {
        const section = document.createElement('section');
        section.style.marginBottom = '16px';

        const heading = document.createElement('div');
        heading.style.marginBottom = '10px';

        const titleText = document.createElement('div');
        titleText.style.fontSize = '13px';
        titleText.style.fontWeight = 'bold';
        titleText.style.letterSpacing = '0.08em';
        titleText.style.textTransform = 'uppercase';
        titleText.style.color = '#e3d6bb';
        titleText.textContent = title;

        const subtitleText = document.createElement('div');
        subtitleText.style.fontSize = '10px';
        subtitleText.style.color = '#8d8169';
        subtitleText.style.marginTop = '2px';
        subtitleText.textContent = subtitle;

        heading.appendChild(titleText);
        heading.appendChild(subtitleText);
        section.appendChild(heading);

        const content = document.createElement('div');
        section.appendChild(content);

        return { section, content };
    }

    createCombatCard(character) {
        const card = document.createElement('div');
        card.style.marginBottom = '12px';
        card.style.padding = '12px';
        card.style.border = `1px solid ${character.accentColor}`;
        card.style.borderRadius = '6px';
        card.style.background = 'linear-gradient(180deg, rgba(22, 22, 20, 0.94), rgba(12, 12, 12, 0.94))';
        card.style.boxShadow = `inset 0 0 0 1px ${this.hexToRgba(character.accentColor, 0.15)}`;
        card.style.transition = 'box-shadow 140ms ease, border-color 140ms ease, background 140ms ease, transform 140ms ease';

        const topRow = document.createElement('div');
        topRow.style.display = 'flex';
        topRow.style.alignItems = 'center';
        topRow.style.gap = '12px';
        topRow.style.marginBottom = '10px';

        const portraitFrame = document.createElement('div');
        portraitFrame.style.width = '72px';
        portraitFrame.style.height = '72px';
        portraitFrame.style.flex = '0 0 72px';
        portraitFrame.style.borderRadius = '4px';
        portraitFrame.style.overflow = 'hidden';
        portraitFrame.style.boxShadow = `0 0 0 1px ${this.hexToRgba(character.accentColor, 0.28)}`;
        portraitFrame.appendChild(this.createPortraitCanvas(character.spriteRows, character.accentColor, character.portraitLabel));

        const textColumn = document.createElement('div');
        textColumn.style.minWidth = '0';
        textColumn.style.flex = '1';

        const nameText = document.createElement('div');
        nameText.style.fontSize = '16px';
        nameText.style.fontWeight = 'bold';
        nameText.style.color = character.accentColor;
        nameText.textContent = character.name;

        const roleText = document.createElement('div');
        roleText.style.fontSize = '10px';
        roleText.style.letterSpacing = '0.14em';
        roleText.style.textTransform = 'uppercase';
        roleText.style.color = '#b6aa8f';
        roleText.style.marginTop = '4px';
        roleText.textContent = character.role;

        textColumn.appendChild(nameText);
        textColumn.appendChild(roleText);
        topRow.appendChild(portraitFrame);
        topRow.appendChild(textColumn);
        card.appendChild(topRow);

        const hpLabelRow = document.createElement('div');
        hpLabelRow.style.display = 'flex';
        hpLabelRow.style.justifyContent = 'space-between';
        hpLabelRow.style.fontSize = '11px';
        hpLabelRow.style.color = '#bcb29c';
        hpLabelRow.style.marginBottom = '6px';

        const hpLabel = document.createElement('div');
        hpLabel.textContent = 'Vitality';

        const hpText = document.createElement('div');
        hpText.style.color = '#f0e8d2';

        hpLabelRow.appendChild(hpLabel);
        hpLabelRow.appendChild(hpText);
        card.appendChild(hpLabelRow);

        const hpTrack = document.createElement('div');
        hpTrack.style.height = '10px';
        hpTrack.style.marginBottom = '10px';
        hpTrack.style.border = '1px solid rgba(255,255,255,0.12)';
        hpTrack.style.borderRadius = '999px';
        hpTrack.style.background = 'rgba(0, 0, 0, 0.42)';
        hpTrack.style.overflow = 'hidden';

        const hpFill = document.createElement('div');
        hpFill.style.height = '100%';
        hpFill.style.width = '100%';
        hpFill.style.background = `linear-gradient(90deg, ${character.accentColor}, ${this.hexToRgba(character.accentColor, 0.55)})`;
        hpTrack.appendChild(hpFill);
        card.appendChild(hpTrack);

        const moves = document.createElement('div');
        moves.style.marginBottom = '8px';
        moves.style.fontSize = '12px';
        moves.style.color = '#bcb29c';
        card.appendChild(moves);

        return { card, nameText, roleText, hpText, hpFill, moves };
    }

    hexToRgba(hex, alpha) {
        const value = hex.replace('#', '');
        const bigint = parseInt(value, 16);
        const r = (bigint >> 16) & 255;
        const g = (bigint >> 8) & 255;
        const b = bigint & 255;
        return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    }

    setCombatCardActiveState(card, accentColor, isTeamActive, isSelected, isDead) {
        if (isDead) {
            card.style.borderColor = '#5b5b5b';
            card.style.background = 'linear-gradient(180deg, rgba(26, 26, 26, 0.9), rgba(16, 16, 16, 0.9))';
            card.style.boxShadow = 'inset 0 0 0 1px rgba(120, 120, 120, 0.10)';
            card.style.transform = 'translateX(0)';
            return;
        }

        if (isSelected) {
            card.style.borderColor = accentColor;
            card.style.background = `linear-gradient(180deg, ${this.hexToRgba(accentColor, 0.34)}, rgba(18, 18, 18, 0.96))`;
            card.style.boxShadow = `0 0 0 1px ${this.hexToRgba(accentColor, 0.36)}, 0 0 28px ${this.hexToRgba(accentColor, 0.26)}, inset 0 0 0 1px ${this.hexToRgba(accentColor, 0.22)}`;
            card.style.transform = 'translateX(4px)';
            return;
        }

        if (isTeamActive) {
            card.style.borderColor = accentColor;
            card.style.background = `linear-gradient(180deg, ${this.hexToRgba(accentColor, 0.22)}, rgba(18, 18, 18, 0.96))`;
            card.style.boxShadow = `0 0 0 1px ${this.hexToRgba(accentColor, 0.26)}, 0 0 16px ${this.hexToRgba(accentColor, 0.18)}, inset 0 0 0 1px ${this.hexToRgba(accentColor, 0.16)}`;
            card.style.transform = 'translateX(2px)';
            return;
        }

        card.style.borderColor = this.hexToRgba(accentColor, 0.7);
        card.style.background = 'linear-gradient(180deg, rgba(22, 22, 20, 0.94), rgba(12, 12, 12, 0.94))';
        card.style.boxShadow = `inset 0 0 0 1px ${this.hexToRgba(accentColor, 0.15)}`;
        card.style.transform = 'translateX(0)';
    }

    setupInputListeners() {
        document.addEventListener('keydown', (e) => {
            const key = e.key.toUpperCase();

            if (this.isGameOver) {
                return;
            }

            if (key === 'TAB') {
                e.preventDefault();
            }

            if (this.keysPressed[key]) {
                return;
            }

            this.keysPressed[key] = true;

            if (this.currentTurn === 'player') {
                if (key === 'TAB' || key === 'E') {
                    this.cycleActivePlayerCharacter(1);
                    return;
                }
                if (key === 'Q') {
                    this.cycleActivePlayerCharacter(-1);
                    return;
                }
                if (key >= '1' && key <= String(this.playerParty.length)) {
                    this.setActivePlayerByPartyIndex(Number(key) - 1);
                    return;
                }
            }

            if (this.currentTurn !== 'player') {
                return;
            }

            this.handleMovement(key);
        });

        document.addEventListener('keyup', (e) => {
            const key = e.key.toUpperCase();
            this.keysPressed[key] = false;
        });
    }

    setupAttackListener() {
        this.raycaster = new THREE.Raycaster();
        this.mouse = new THREE.Vector2();

        this.renderer.domElement.addEventListener('click', (event) => {
            if (this.isGameOver || this.currentTurn !== 'player') {
                return;
            }

            const activeCharacter = this.getActivePlayerCharacter();
            if (!activeCharacter || activeCharacter.isDead) {
                return;
            }

            const aiTargets = this.getLivingCharacters(this.aiParty).filter((character) => character.mesh);
            if (aiTargets.length === 0) {
                return;
            }

            const rect = this.renderer.domElement.getBoundingClientRect();
            this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
            this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
            this.raycaster.setFromCamera(this.mouse, this.camera);

            const intersects = this.raycaster.intersectObjects(aiTargets.map((character) => character.mesh));
            if (intersects.length === 0) {
                return;
            }

            const targetCharacter = aiTargets.find((character) => character.mesh === intersects[0].object);
            if (targetCharacter) {
                this.characterAttack(activeCharacter, targetCharacter);
            }
        });
    }

    handleMovement(key) {
        const activeCharacter = this.getActivePlayerCharacter();
        if (
            this.isGameOver ||
            this.currentTurn !== 'player' ||
            this.movesThisTurn >= this.maxMovesPerTurn ||
            !activeCharacter ||
            activeCharacter.isDead
        ) {
            return;
        }

        let newX = activeCharacter.gridX;
        let newY = activeCharacter.gridY;

        switch (key) {
            case 'W':
                newY = Math.max(0, activeCharacter.gridY - 1);
                break;
            case 'S':
                newY = Math.min(this.gridHeight - 1, activeCharacter.gridY + 1);
                break;
            case 'A':
                newX = Math.max(0, activeCharacter.gridX - 1);
                break;
            case 'D':
                newX = Math.min(this.gridWidth - 1, activeCharacter.gridX + 1);
                break;
            default:
                return;
        }

        if (this.isObstacle(newX, newY) || this.isOccupied(newX, newY, activeCharacter)) {
            return;
        }

        const dx = newX - activeCharacter.gridX;
        const dy = newY - activeCharacter.gridY;

        activeCharacter.gridX = newX;
        activeCharacter.gridY = newY;

        if (dx > 0) {
            this.updateCharacterFacing(activeCharacter, 'right');
        } else if (dx < 0) {
            this.updateCharacterFacing(activeCharacter, 'left');
        } else if (dy > 0) {
            this.updateCharacterFacing(activeCharacter, 'down');
        } else if (dy < 0) {
            this.updateCharacterFacing(activeCharacter, 'up');
        }

        this.updateCharacterPosition(activeCharacter);
        this.updateCamera();
        this.movesThisTurn++;

        if (this.movesThisTurn >= this.maxMovesPerTurn) {
            this.switchTurn();
        }
    }

    switchTurn() {
        this.currentTurn = this.currentTurn === 'player' ? 'ai' : 'player';
        this.movesThisTurn = 0;

        if (this.currentTurn === 'player') {
            this.getActivePlayerCharacter();
            return;
        }

        this.aiActionIndex = 0;
    }

    characterAttack(attacker, target) {
        if (!attacker || !target || attacker.isDead || target.isDead || attacker.team === target.team) {
            return false;
        }
        if (this.currentTurn !== attacker.team || this.movesThisTurn >= this.maxMovesPerTurn) {
            return false;
        }

        const dx = Math.abs(target.gridX - attacker.gridX);
        const dy = Math.abs(target.gridY - attacker.gridY);
        if (dx > 1 || dy > 1) {
            return false;
        }

        const movesLeft = this.maxMovesPerTurn - this.movesThisTurn;
        if (movesLeft < attacker.attackCost) {
            return false;
        }

        this.faceCharacterToward(attacker, target);
        target.hitPoints -= attacker.attackDamage;
        this.playHitAnimation(target);

        if (target.hitPoints <= 0) {
            target.hitPoints = 0;
            this.markCharacterDead(target);
        }

        this.movesThisTurn += attacker.attackCost;
        if (this.movesThisTurn >= this.maxMovesPerTurn) {
            this.switchTurn();
        }

        return true;
    }

    playHitAnimation(character) {
        if (!character || !character.mesh || character.removedFromScene) {
            return;
        }

        character.hitAnimEndTime = performance.now() + 1000;
    }

    updateHitAnimation(character, nowMs) {
        if (!character || !character.mesh || character.removedFromScene) {
            return;
        }

        if (!character.hitAnimEndTime || nowMs >= character.hitAnimEndTime) {
            character.mesh.scale.set(1, 1, 1);
            character.mesh.material.color.setHex(character.baseColorHex);
            character.hitAnimEndTime = 0;
            return;
        }

        const durationMs = 1000;
        const remainingMs = character.hitAnimEndTime - nowMs;
        const progress = 1 - (remainingMs / durationMs);

        const envelope = 1 - progress;
        const pulse = 1 + (Math.sin(progress * Math.PI * 10) * 0.18 * envelope);
        character.mesh.scale.set(pulse, pulse, 1);

        const flashAmount = Math.abs(Math.sin(progress * Math.PI * 12)) * 0.65 * envelope;
        const baseColor = new THREE.Color(character.baseColorHex);
        const flashTarget = character.baseColorHex === 0xffffff
            ? new THREE.Color(0xff4444)
            : new THREE.Color(0xffffff);
        const hitColor = baseColor.clone().lerp(flashTarget, flashAmount);
        character.mesh.material.color.copy(hitColor);
    }

    getNearestLivingOpponent(character) {
        const enemyGroup = character.team === 'player' ? this.aiParty : this.playerParty;
        const livingOpponents = this.getLivingCharacters(enemyGroup);
        if (livingOpponents.length === 0) {
            return null;
        }

        let nearest = livingOpponents[0];
        let nearestDistance = Number.POSITIVE_INFINITY;
        livingOpponents.forEach((candidate) => {
            const distance = Math.abs(candidate.gridX - character.gridX) + Math.abs(candidate.gridY - character.gridY);
            if (distance < nearestDistance) {
                nearest = candidate;
                nearestDistance = distance;
            }
        });
        return nearest;
    }

    getCurrentAIActor() {
        const livingAI = this.getLivingCharacters(this.aiParty);
        if (livingAI.length === 0) {
            return null;
        }
        return livingAI[this.aiActionIndex % livingAI.length];
    }

    advanceAIActor() {
        const livingAI = this.getLivingCharacters(this.aiParty);
        if (livingAI.length === 0) {
            return;
        }
        this.aiActionIndex = (this.aiActionIndex + 1) % livingAI.length;
    }

    moveAICharacter(character) {
        if (this.isGameOver || this.currentTurn !== 'ai' || this.movesThisTurn >= this.maxMovesPerTurn || !character || character.isDead) {
            return;
        }

        const target = this.getNearestLivingOpponent(character);
        if (!target) {
            return;
        }

        if (this.characterAttack(character, target)) {
            this.advanceAIActor();
            return;
        }

        const dx = target.gridX - character.gridX;
        const dy = target.gridY - character.gridY;
        const absDx = Math.abs(dx);
        const absDy = Math.abs(dy);

        let newX = character.gridX;
        let newY = character.gridY;

        if (absDx > absDy) {
            newX = dx > 0 ? Math.min(this.gridWidth - 1, character.gridX + 1) : Math.max(0, character.gridX - 1);
        } else {
            newY = dy > 0 ? Math.min(this.gridHeight - 1, character.gridY + 1) : Math.max(0, character.gridY - 1);
        }

        const blocked = (x, y) => this.isObstacle(x, y) || this.isOccupied(x, y, character);

        if (blocked(newX, newY)) {
            if (absDx > absDy) {
                newX = character.gridX;
                newY = dy > 0 ? Math.min(this.gridHeight - 1, character.gridY + 1) : Math.max(0, character.gridY - 1);
            } else {
                newY = character.gridY;
                newX = dx > 0 ? Math.min(this.gridWidth - 1, character.gridX + 1) : Math.max(0, character.gridX - 1);
            }

            if (blocked(newX, newY)) {
                this.movesThisTurn++;
                this.advanceAIActor();
                if (this.movesThisTurn >= this.maxMovesPerTurn) {
                    this.switchTurn();
                }
                return;
            }
        }

        const moveDx = newX - character.gridX;
        const moveDy = newY - character.gridY;

        character.gridX = newX;
        character.gridY = newY;

        if (moveDx > 0) {
            this.updateCharacterFacing(character, 'right');
        } else if (moveDx < 0) {
            this.updateCharacterFacing(character, 'left');
        } else if (moveDy > 0) {
            this.updateCharacterFacing(character, 'down');
        } else if (moveDy < 0) {
            this.updateCharacterFacing(character, 'up');
        }

        this.updateCharacterPosition(character);
        this.movesThisTurn++;
        this.advanceAIActor();
        if (this.movesThisTurn >= this.maxMovesPerTurn) {
            this.switchTurn();
        }
    }

    markCharacterDead(character) {
        if (character.isDead) {
            return;
        }

        character.isDead = true;
        character.fadeFrames = 0;

        if (character.hitPoints < 0) {
            character.hitPoints = 0;
        }

        if (character.team === 'player' && this.getActivePlayerCharacter() === character) {
            this.getActivePlayerCharacter();
        }
    }

    fadeAndRemoveCharacter(character) {
        if (!character.isDead || !character.mesh || character.removedFromScene) {
            return;
        }

        const fadeDurationFrames = 60;
        character.fadeFrames += 1;

        const opacity = Math.max(0, 1 - (character.fadeFrames / fadeDurationFrames));
        character.mesh.material.transparent = true;
        character.mesh.material.opacity = opacity;

        character.mesh.children.forEach((child) => {
            if (child.material) {
                child.material.transparent = true;
                child.material.opacity = opacity;
            }
        });

        if (character.fadeFrames >= fadeDurationFrames) {
            this.scene.remove(character.mesh);
            character.mesh = null;
            character.removedFromScene = true;
        }
    }

    areAllPartyMembersDead(group) {
        return group.length > 0 && group.every((character) => character.isDead);
    }

    startVictorySequence() {
        if (this.isGameOver) {
            return;
        }

        this.isGameOver = true;
        this.gameOutcome = 'victory';
        this.victoryStartTime = performance.now();
        this.victoryText.textContent = 'Victory!';
        this.victoryText.style.color = '#ffd700';
        this.victoryText.style.textShadow = '0 0 16px rgba(255, 215, 0, 0.6), 0 0 32px rgba(255, 140, 0, 0.45)';
        this.victoryText.style.display = 'block';
        this.victoryText.style.opacity = '1';
    }

    startGameOverSequence() {
        if (this.isGameOver) {
            return;
        }

        this.isGameOver = true;
        this.gameOutcome = 'defeat';
        this.victoryStartTime = performance.now();
        this.victoryText.textContent = 'Game Over';
        this.victoryText.style.color = '#ff4d4d';
        this.victoryText.style.textShadow = '0 0 16px rgba(255, 77, 77, 0.6), 0 0 32px rgba(128, 0, 0, 0.45)';
        this.victoryText.style.display = 'block';
        this.victoryText.style.opacity = '1';
    }

    updateVictorySequence() {
        if (!this.isGameOver) {
            return;
        }

        const elapsedMs = performance.now() - this.victoryStartTime;
        const progress = Math.min(1, elapsedMs / this.victoryFadeDurationMs);
        this.victoryText.style.opacity = String(1 - progress);

        if (progress >= 1 && !this.restartTriggered) {
            this.restartTriggered = true;
            window.location.reload();
        }
    }

    updateCharacterCard(character, movesLeft, aiActor) {
        const hud = this.characterHud.get(character.id);
        if (!hud) {
            return;
        }

        const deadColor = '#666666';
        const aliveInfoColor = '#ffffff';
        const hpRatio = Math.max(0, character.hitPoints / character.maxHitPoints);
        const isSelected = character.team === 'player' && this.getActivePlayerCharacter() === character;
        const isTeamActive = this.currentTurn === character.team;

        hud.hpText.textContent = `${character.hitPoints} / ${character.maxHitPoints}`;
        hud.hpFill.style.width = `${hpRatio * 100}%`;
        hud.hpFill.style.opacity = character.isDead ? '0.35' : '1';

        hud.card.style.opacity = character.isDead ? '0.65' : '1';
        hud.nameText.style.color = character.isDead ? deadColor : character.accentColor;
        hud.roleText.style.color = character.isDead ? deadColor : '#b6aa8f';
        hud.hpText.style.color = character.isDead ? deadColor : aliveInfoColor;

        if (character.isDead) {
            hud.moves.textContent = 'Defeated';
            hud.moves.style.color = deadColor;
        } else if (character.team === 'player' && isSelected && this.currentTurn === 'player') {
            hud.moves.textContent = `${movesLeft} of ${this.maxMovesPerTurn} actions remaining`;
            hud.moves.style.color = '#e1d6c1';
        } else if (character.team === 'player' && isSelected) {
            hud.moves.textContent = 'Selected';
            hud.moves.style.color = '#bcb29c';
        } else if (character.team === 'player' && this.currentTurn === 'player') {
            hud.moves.textContent = 'Ready';
            hud.moves.style.color = '#bcb29c';
        } else if (character.team === 'ai' && this.currentTurn === 'ai' && aiActor === character) {
            hud.moves.textContent = `${movesLeft} of ${this.maxMovesPerTurn} actions remaining`;
            hud.moves.style.color = '#e1d6c1';
        } else {
            hud.moves.textContent = '';
            hud.moves.style.color = '#bcb29c';
        }

        this.setCombatCardActiveState(hud.card, character.accentColor, isTeamActive, isSelected && isTeamActive, character.isDead);
    }

    update() {
        const nowMs = performance.now();
        const movesLeft = this.maxMovesPerTurn - this.movesThisTurn;
        const aiActor = this.getCurrentAIActor();

        this.characters.forEach((character) => {
            this.updateCharacterCard(character, movesLeft, aiActor);
            this.fadeAndRemoveCharacter(character);
            this.updateHitAnimation(character, nowMs);
        });

        if (!this.isGameOver && this.areAllPartyMembersDead(this.playerParty)) {
            this.startGameOverSequence();
        }

        if (!this.isGameOver && this.areAllPartyMembersDead(this.aiParty)) {
            this.startVictorySequence();
        }

        if (this.isGameOver) {
            this.updateVictorySequence();
            return;
        }

        this.goblinMoveTimer++;
        if (this.currentTurn === 'ai' && this.goblinMoveTimer >= 30) {
            this.moveAICharacter(this.getCurrentAIActor());
            this.goblinMoveTimer = 0;
        }
    }

    animate() {
        this.update();
        this.updateCamera();
        this.renderer.render(this.scene, this.camera);
        requestAnimationFrame(() => this.animate());
    }
}

// Initialize the scene when the page loads
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        new GridScene('gameContainer');
    });
} else {
    new GridScene('gameContainer');
}