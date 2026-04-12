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
        
        // Wizard (player) character
        this.wizard = {
            gridX: 50,
            gridY: 50,
            mesh: null,
            baseColorHex: 0xffffff,
            facing: 'right',
            directionPointer: null,
            hitPoints: 10,
            hitAnimEndTime: 0,
            isDead: false,
            fadeFrames: 0,
            removedFromScene: false
        };

        // Goblin (AI) character
        this.goblin = {
            gridX: 45,
            gridY: 45,
            mesh: null,
            baseColorHex: 0xffffff,
            facing: 'right',
            directionPointer: null,
            hitPoints: 10,
            hitAnimEndTime: 0,
            isDead: false,
            fadeFrames: 0,
            removedFromScene: false
        };
        
        // Turn system
        this.currentTurn = 'blue'; // 'blue' or 'red'
        this.movesThisTurn = 0;
        this.maxMovesPerTurn = 5;
        this.goblinMoveTimer = 0;
        this.isGameOver = false;
        this.gameOutcome = null;
        this.victoryStartTime = 0;
        this.victoryFadeDurationMs = 3000;
        this.restartTriggered = false;
        
        // Dungeon tile types
        this.TILE_VOID  = 0;  // Void darkness
        this.TILE_FLOOR = 1;  // Walkable stone floor
        this.TILE_WALL  = 2;  // Impassable stone wall

        // Generate dungeon and place characters inside rooms
        const dungeon = this.generateDungeonMap();
        this.dungeonMap = dungeon.map;
        if (dungeon.rooms.length >= 1) {
            // Wizard always starts in the first room's centre
            const r0 = dungeon.rooms[0];
            this.wizard.gridX = Math.floor(r0.x + r0.w / 2);
            this.wizard.gridY = Math.floor(r0.y + r0.h / 2);

            // Find a floor tile that is 2–4 cells (Chebyshev) from the wizard.
            // Room centres are always far apart, so we scan tiles directly.
            const MIN_DIST = 2;
            const MAX_DIST = 4;
            const wx = this.wizard.gridX;
            const wy = this.wizard.gridY;
            const candidates = [];
            for (let y = wy - MAX_DIST; y <= wy + MAX_DIST; y++) {
                for (let x = wx - MAX_DIST; x <= wx + MAX_DIST; x++) {
                    if (x < 0 || x >= this.gridWidth || y < 0 || y >= this.gridHeight) continue;
                    if (this.dungeonMap[y][x] !== this.TILE_FLOOR) continue;
                    const dist = Math.max(Math.abs(x - wx), Math.abs(y - wy));
                    if (dist >= MIN_DIST && dist <= MAX_DIST) candidates.push({ x, y });
                }
            }
            if (candidates.length > 0) {
                const pick = candidates[Math.floor(Math.random() * candidates.length)];
                this.goblin.gridX = pick.x;
                this.goblin.gridY = pick.y;
            } else {
                // Fallback: last room's centre
                const rN = dungeon.rooms[dungeon.rooms.length - 1];
                this.goblin.gridX = Math.floor(rN.x + rN.w / 2);
                this.goblin.gridY = Math.floor(rN.y + rN.h / 2);
            }
        }

        // Input handling
        this.keysPressed = {};
        this.setupInputListeners();
        
        // Setup scene
        this.setupGrid();
        this.setupWizard();
        this.setupGoblin();
        this.setupUI();
        this.setupAttackListener();
        this.updateCamera();
        
        // Start animation loop
        this.animate();
    }
    
    setupGrid() {
        const TILE_RES = 16; // canvas pixels per grid cell
        const canvasW  = this.gridWidth  * TILE_RES;
        const canvasH  = this.gridHeight * TILE_RES;
        const canvas   = document.createElement('canvas');
        canvas.width   = canvasW;
        canvas.height  = canvasH;
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

        const worldW = this.gridWidth  * this.cellSize;
        const worldH = this.gridHeight * this.cellSize;
        const geo  = new THREE.PlaneGeometry(worldW, worldH);
        const mat  = new THREE.MeshBasicMaterial({ map: texture });
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

        // Dark cold-stone base
        ctx.fillStyle = '#0e1218';
        ctx.fillRect(px, py, T, T);

        // Mortar joints – running-bond brickwork
        ctx.fillStyle = '#07080f';
        ctx.fillRect(px, py + H, T, 1);          // horizontal seam
        if ((cx + cy) % 2 === 0) {               // vertical seam alternates per row
            ctx.fillRect(px + H, py, 1, H);
        } else {
            ctx.fillRect(px + H, py + H + 1, 1, H - 1);
        }

        // Top-edge ambient highlight (cold ceiling light)
        ctx.fillStyle = 'rgba(100, 140, 220, 0.09)';
        ctx.fillRect(px, py, T, 2);
        ctx.fillStyle = 'rgba(80, 110, 190, 0.05)';
        ctx.fillRect(px, py + 2, 2, H - 2);

        // Bottom / right shadow
        ctx.fillStyle = 'rgba(0, 0, 0, 0.70)';
        ctx.fillRect(px, py + T - 2, T, 2);
        ctx.fillStyle = 'rgba(0, 0, 0, 0.50)';
        ctx.fillRect(px + T - 2, py, 2, T);

        // Stone micro-texture variation
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

        // Dark warm stone slab with subtle per-tile colour variation
        ctx.fillStyle = `rgb(${22 + v * 2},${17 + v},12)`;
        ctx.fillRect(px, py, T, T);

        // Grout lines (top and left edges)
        ctx.fillStyle = 'rgba(0,0,0,0.60)';
        ctx.fillRect(px, py, T, 1);
        ctx.fillRect(px, py, 1, T);

        // Top-left highlight (faint torch / ambient light)
        ctx.fillStyle = 'rgba(255,220,140,0.055)';
        ctx.fillRect(px + 1, py + 1, T - 2, 1);
        ctx.fillRect(px + 1, py + 2, 1, T - 3);

        // Bottom-right inner shadow
        ctx.fillStyle = 'rgba(0,0,0,0.35)';
        ctx.fillRect(px + 1, py + T - 2, T - 2, 1);
        ctx.fillRect(px + T - 2, py + 1,   1, T - 2);

        // Random crack
        if (s % 8 === 0) {
            ctx.fillStyle = 'rgba(0,0,0,0.45)';
            const crackX = px + 2 + (s       % (T - 5));
            const crackY = py + 2 + ((s >> 8) % (T - 5));
            ctx.fillRect(crackX,     crackY, 4, 1);
            ctx.fillRect(crackX + 3, crackY, 1, 3);
        }

        // Small pebble / debris
        if (s % 13 === 5) {
            ctx.fillStyle = 'rgba(0,0,0,0.50)';
            ctx.fillRect(px + 2 + (s % (T - 5)), py + 2 + ((s >> 5) % (T - 5)), 2, 2);
        }
    }
    
    generateDungeonMap() {
        const VOID  = this.TILE_VOID;
        const FLOOR = this.TILE_FLOOR;
        const WALL  = this.TILE_WALL;

        // Initialise every cell as void darkness
        const map = Array.from({ length: this.gridHeight }, () =>
            new Array(this.gridWidth).fill(VOID)
        );

        const rooms = [];

        // Place rooms via random attempts with overlap reject
        for (let attempt = 0; attempt < 120; attempt++) {
            const rw = 5  + Math.floor(Math.random() * 12); // 5-16 wide
            const rh = 4  + Math.floor(Math.random() * 9);  // 4-12 tall
            const rx = 2  + Math.floor(Math.random() * (this.gridWidth  - rw - 4));
            const ry = 2  + Math.floor(Math.random() * (this.gridHeight - rh - 4));

            // Reject if overlaps any existing room (2-cell padding)
            const overlaps = rooms.some(r =>
                rx < r.x + r.w + 2 && rx + rw + 2 > r.x &&
                ry < r.y + r.h + 2 && ry + rh + 2 > r.y
            );
            if (overlaps) continue;

            rooms.push({ x: rx, y: ry, w: rw, h: rh });

            // Carve room floor
            for (let y = ry; y < ry + rh; y++) {
                for (let x = rx; x < rx + rw; x++) {
                    map[y][x] = FLOOR;
                }
            }
        }

        // Connect adjacent rooms with 2-wide L-shaped corridors
        for (let i = 1; i < rooms.length; i++) {
            const a  = rooms[i - 1];
            const b  = rooms[i];
            const ax = Math.floor(a.x + a.w / 2);
            const ay = Math.floor(a.y + a.h / 2);
            const bx = Math.floor(b.x + b.w / 2);
            const by = Math.floor(b.y + b.h / 2);

            // Horizontal leg
            const hMin = Math.min(ax, bx);
            const hMax = Math.max(ax, bx);
            for (let x = hMin; x <= hMax; x++) {
                map[ay][x] = FLOOR;
                if (ay + 1 < this.gridHeight) map[ay + 1][x] = FLOOR;
            }

            // Vertical leg
            const vMin = Math.min(ay, by);
            const vMax = Math.max(ay, by);
            for (let y = vMin; y <= vMax; y++) {
                map[y][bx] = FLOOR;
                if (bx + 1 < this.gridWidth) map[y][bx + 1] = FLOOR;
            }
        }

        // Surround floor with wall tiles
        for (let y = 0; y < this.gridHeight; y++) {
            for (let x = 0; x < this.gridWidth; x++) {
                if (map[y][x] !== VOID) continue;
                let nearFloor = false;
                outer:
                for (let dy = -1; dy <= 1; dy++) {
                    for (let dx = -1; dx <= 1; dx++) {
                        if (dx === 0 && dy === 0) continue;
                        const ny = y + dy, nx = x + dx;
                        if (ny >= 0 && ny < this.gridHeight &&
                            nx >= 0 && nx < this.gridWidth  &&
                            map[ny][nx] === FLOOR) {
                            nearFloor = true;
                            break outer;
                        }
                    }
                }
                if (nearFloor) map[y][x] = WALL;
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
    
    createSpriteTexture(rows) {
        const canvas = document.createElement('canvas');
        canvas.width  = 16;
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

    createWizardTexture() {
        const _ = null;
        const DH = '#0a2f7a'; // dark hat blue
        const LH = '#1a5cc8'; // lighter hat highlight
        const GS = '#ffd700'; // gold star on hat
        const SK = '#ffbb88'; // skin
        const EY = '#1a0800'; // eye
        const WB = '#d0d0d0'; // white beard
        const RB = '#1050b0'; // robe body
        const RL = '#3a7ee0'; // robe highlight stripe
        const ST = '#5d3a28'; // wooden staff
        return this.createSpriteTexture([
//          0   1   2   3   4   5   6   7   8   9  10  11  12  13  14  15
            [_,  _,  _,  _,  _,  _,  DH, DH, DH, _,  _,  _,  _,  _,  _,  _ ],  // 0  hat tip
            [_,  _,  _,  _,  _,  DH, DH, LH, DH, DH, _,  _,  _,  _,  _,  _ ],  // 1
            [_,  _,  _,  _,  DH, DH, LH, DH, DH, DH, DH, _,  _,  _,  _,  _ ],  // 2
            [_,  _,  _,  DH, DH, GS, DH, DH, DH, DH, DH, _,  _,  _,  _,  _ ],  // 3  gold star
            [_,  _,  DH, DH, DH, DH, DH, DH, DH, DH, DH, DH, _,  _,  _,  _ ],  // 4
            [_,  _,  DH, DH, LH, DH, DH, DH, DH, LH, DH, DH, _,  _,  _,  _ ],  // 5  hat brim
            [_,  _,  _,  SK, SK, SK, SK, SK, SK, SK, SK, _,  _,  _,  _,  _ ],  // 6  face
            [_,  _,  _,  SK, EY, SK, SK, EY, SK, SK, SK, _,  _,  _,  _,  _ ],  // 7  eyes
            [_,  _,  _,  SK, SK, SK, SK, SK, SK, SK, SK, _,  _,  _,  _,  _ ],  // 8
            [_,  ST, WB, WB, WB, WB, WB, WB, WB, WB, WB, _,  _,  _,  _,  _ ],  // 9  beard / staff
            [_,  ST, WB, RB, RB, RB, RB, RB, RB, RB, WB, _,  _,  _,  _,  _ ],  // 10 robe
            [_,  ST, WB, RB, RL, RB, RB, RL, RB, RB, WB, _,  _,  _,  _,  _ ],  // 11 highlight
            [GS, ST, WB, RB, RB, RB, RB, RB, RB, RB, WB, _,  _,  _,  _,  _ ],  // 12 staff gem
            [_,  _,  WB, RB, RB, RB, RB, RB, RB, RB, WB, _,  _,  _,  _,  _ ],  // 13
            [_,  _,  WB, WB, RB, RB, RB, RB, RB, WB, WB, _,  _,  _,  _,  _ ],  // 14 robe base
            [_,  _,  _,  WB, WB, RB, RB, RB, WB, WB, _,  _,  _,  _,  _,  _ ],  // 15 feet
        ]);
    }

    createGoblinTexture() {
        const _ = null;
        const GR = '#4aaa30'; // goblin green skin
        const DG = '#2a6618'; // dark green (ears, shadows)
        const YE = '#ffee00'; // glowing yellow eyes
        const BK = '#0a0800'; // very dark (mouth cavity)
        const TB = '#fffacc'; // tusk / fang ivory
        const IR = '#5a5a7a'; // iron armor
        const LI = '#9090b8'; // iron highlight
        const RU = '#252535'; // iron rivet / shadow
        const BR = '#8c4a20'; // leather belt
        const LG = '#3a1e0e'; // leather pants
        const BT = '#181010'; // boots
        const SW = '#d4d4e8'; // sword blade
        const SH = '#707080'; // sword shade
        return this.createSpriteTexture([
//          0   1   2   3   4   5   6   7   8   9  10  11  12  13  14  15
            [_,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _,  _ ],  //  0  empty
            [_,  _,  _,  _,  GR, GR, GR, GR, GR, _,  _,  _,  _,  _,  _,  _ ],  //  1  head top
            [_,  _,  DG, GR, GR, GR, GR, GR, GR, GR, DG, _,  _,  _,  _,  _ ],  //  2  head + pointy ears
            [_,  _,  _,  GR, YE, GR, GR, GR, YE, GR, _,  _,  _,  _,  _,  _ ],  //  3  glowing eyes
            [_,  _,  _,  GR, GR, GR, DG, GR, GR, GR, _,  _,  _,  _,  _,  _ ],  //  4  nose
            [_,  _,  _,  GR, TB, BK, BK, BK, TB, GR, _,  _,  _,  _,  _,  _ ],  //  5  fangs / grin
            [_,  _,  _,  IR, IR, IR, IR, IR, IR, IR, _,  _,  _,  _,  _,  _ ],  //  6  gorget / collar
            [SW, SH, IR, LI, IR, IR, RU, IR, IR, LI, IR, _,  _,  _,  _,  _ ],  //  7  chest plate + sword
            [_,  SW, IR, IR, IR, BR, BR, IR, IR, IR, _,  _,  _,  _,  _,  _ ],  //  8  belt
            [_,  SW, GR, IR, IR, IR, IR, IR, IR, GR, _,  _,  _,  _,  _,  _ ],  //  9  lower torso + arms
            [_,  _,  GR, _,  LG, LG, LG, LG, _,  GR, _,  _,  _,  _,  _,  _ ],  // 10  thighs + arms
            [_,  _,  GR, _,  LG, LG, LG, LG, _,  GR, _,  _,  _,  _,  _,  _ ],  // 11  knees  + arms
            [_,  _,  _,  _,  LG, LG, _,  LG, LG, _,  _,  _,  _,  _,  _,  _ ],  // 12  legs split
            [_,  _,  _,  _,  LG, LG, _,  LG, LG, _,  _,  _,  _,  _,  _,  _ ],  // 13  legs
            [_,  _,  _,  _,  BT, BT, _,  BT, BT, _,  _,  _,  _,  _,  _,  _ ],  // 14  boots
            [_,  _,  _,  BT, BT, _,  _,  _,  BT, BT, _,  _,  _,  _,  _,  _ ],  // 15  boot toes
        ]);
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

    setupWizard() {
        this.setupCharacterSprite(this.wizard, this.createWizardTexture(), 0x00eeff);
    }

    setupGoblin() {
        this.setupCharacterSprite(this.goblin, this.createGoblinTexture(), 0xff6600);
    }
    
    updateCharacterPosition(character) {
        const worldW = this.gridWidth  * this.cellSize;
        const worldH = this.gridHeight * this.cellSize;
        const x = (character.gridX * this.cellSize + this.cellSize / 2) - worldW / 2;
        const y = (worldH / 2) - (character.gridY * this.cellSize + this.cellSize / 2);
        character.mesh.position.set(x, y, 0);
    }

    updateCamera() {
        const worldW = this.gridWidth  * this.cellSize;
        const worldH = this.gridHeight * this.cellSize;
        this.camera.position.x = (this.wizard.gridX * this.cellSize + this.cellSize / 2) - worldW / 2;
        this.camera.position.y = (worldH / 2) - (this.wizard.gridY * this.cellSize + this.cellSize / 2);
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
    
    setupUI() {
        const hudRoot = document.getElementById('battleHud') || this.container;
        hudRoot.style.pointerEvents = 'none';

        const blueCard = this.createCombatCard('Blue Wizard', 'Player', '#4f86ff');
        this.blueSection = blueCard.card;
        this.blueTitle = blueCard.name;
        this.blueRoleText = blueCard.role;
        this.blueHPText = blueCard.hpText;
        this.blueHPFill = blueCard.hpFill;
        this.blueMovesText = blueCard.moves;
        this.blueAccentColor = blueCard.accentColor;
        hudRoot.appendChild(blueCard.card);

        const redCard = this.createCombatCard('Goblin Warrior', 'AI', '#d34c4c');
        this.redSection = redCard.card;
        this.redTitle = redCard.name;
        this.redRoleText = redCard.role;
        this.redHPText = redCard.hpText;
        this.redHPFill = redCard.hpFill;
        this.redMovesText = redCard.moves;
        this.redAccentColor = redCard.accentColor;
        hudRoot.appendChild(redCard.card);

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

    createCombatCard(name, role, accentColor) {
        const card = document.createElement('div');
        card.style.marginBottom = '12px';
        card.style.padding = '12px';
        card.style.border = `1px solid ${accentColor}`;
        card.style.borderRadius = '6px';
        card.style.background = 'linear-gradient(180deg, rgba(22, 22, 20, 0.94), rgba(12, 12, 12, 0.94))';
        card.style.boxShadow = `inset 0 0 0 1px ${this.hexToRgba(accentColor, 0.15)}`;
        card.style.transition = 'box-shadow 140ms ease, border-color 140ms ease, background 140ms ease, transform 140ms ease';

        const header = document.createElement('div');
        header.style.display = 'flex';
        header.style.justifyContent = 'space-between';
        header.style.alignItems = 'baseline';
        header.style.gap = '10px';
        header.style.marginBottom = '10px';

        const nameText = document.createElement('div');
        nameText.style.fontSize = '16px';
        nameText.style.fontWeight = 'bold';
        nameText.style.color = accentColor;
        nameText.textContent = name;

        const roleText = document.createElement('div');
        roleText.style.fontSize = '10px';
        roleText.style.letterSpacing = '0.14em';
        roleText.style.textTransform = 'uppercase';
        roleText.style.color = '#b6aa8f';
        roleText.textContent = role;

        header.appendChild(nameText);
        header.appendChild(roleText);
        card.appendChild(header);

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
        hpFill.style.background = `linear-gradient(90deg, ${accentColor}, ${this.hexToRgba(accentColor, 0.55)})`;
        hpTrack.appendChild(hpFill);
        card.appendChild(hpTrack);

        const moves = document.createElement('div');
        moves.style.marginBottom = '8px';
        moves.style.fontSize = '12px';
        moves.style.color = '#bcb29c';

        card.appendChild(moves);

        return { card, name: nameText, role: roleText, hpText, hpFill, moves, accentColor };
    }

    hexToRgba(hex, alpha) {
        const value = hex.replace('#', '');
        const bigint = parseInt(value, 16);
        const r = (bigint >> 16) & 255;
        const g = (bigint >> 8) & 255;
        const b = bigint & 255;
        return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    }

    setCombatCardActiveState(card, accentColor, isActive, isDead) {
        if (isDead) {
            card.style.borderColor = '#5b5b5b';
            card.style.background = 'linear-gradient(180deg, rgba(26, 26, 26, 0.9), rgba(16, 16, 16, 0.9))';
            card.style.boxShadow = 'inset 0 0 0 1px rgba(120, 120, 120, 0.10)';
            card.style.transform = 'translateX(0)';
            return;
        }

        if (isActive) {
            card.style.borderColor = accentColor;
            card.style.background = `linear-gradient(180deg, ${this.hexToRgba(accentColor, 0.24)}, rgba(18, 18, 18, 0.96))`;
            card.style.boxShadow = `0 0 0 1px ${this.hexToRgba(accentColor, 0.30)}, 0 0 22px ${this.hexToRgba(accentColor, 0.22)}, inset 0 0 0 1px ${this.hexToRgba(accentColor, 0.18)}`;
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

            if (this.isGameOver) return;
            
            // Only accept input during blue's turn
            if (this.currentTurn !== 'blue') return;
            
            // Only trigger movement on key press, not on hold
            if (!this.keysPressed[key]) {
                this.keysPressed[key] = true;
                this.handleMovement(key);
            }
        });
        
        document.addEventListener('keyup', (e) => {
            const key = e.key.toUpperCase();
            this.keysPressed[key] = false;
        });
    }
    
    setupAttackListener() {
        // Create raycaster and mouse vector for click detection
        this.raycaster = new THREE.Raycaster();
        this.mouse = new THREE.Vector2();
        
        this.renderer.domElement.addEventListener('click', (event) => {
            // Only allow attacks during blue's turn
            if (this.isGameOver) return;
            if (this.currentTurn !== 'blue') return;
            if (this.wizard.isDead || this.goblin.isDead || !this.goblin.mesh) return;
            
            // Calculate mouse position in normalized device coordinates
            const rect = this.renderer.domElement.getBoundingClientRect();
            this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
            this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
            
            // Update the picking ray with the camera and mouse position
            this.raycaster.setFromCamera(this.mouse, this.camera);
            
            // Check if the goblin was clicked
            const intersects = this.raycaster.intersectObject(this.goblin.mesh);
            
            if (intersects.length > 0) {
                this.wizardAttackGoblin();
            }
        });
    }
    
    handleMovement(key) {
        if (this.isGameOver || this.currentTurn !== 'blue' || this.movesThisTurn >= this.maxMovesPerTurn || this.wizard.isDead) {
            return;
        }
        
        let newX = this.wizard.gridX;
        let newY = this.wizard.gridY;
        
        switch (key) {
            case 'W': newY = Math.max(0, this.wizard.gridY - 1);                       break;
            case 'S': newY = Math.min(this.gridHeight - 1, this.wizard.gridY + 1);     break;
            case 'A': newX = Math.max(0, this.wizard.gridX - 1);                       break;
            case 'D': newX = Math.min(this.gridWidth - 1, this.wizard.gridX + 1);      break;
            default:  return;
        }
        
        if (newX === this.goblin.gridX && newY === this.goblin.gridY) return;
        if (this.isObstacle(newX, newY)) return;
        
        const dx = newX - this.wizard.gridX;
        const dy = newY - this.wizard.gridY;

        this.wizard.gridX = newX;
        this.wizard.gridY = newY;

        if      (dx > 0) this.updateCharacterFacing(this.wizard, 'right');
        else if (dx < 0) this.updateCharacterFacing(this.wizard, 'left');
        else if (dy > 0) this.updateCharacterFacing(this.wizard, 'down');
        else if (dy < 0) this.updateCharacterFacing(this.wizard, 'up');
        
        this.updateCharacterPosition(this.wizard);
        this.updateCamera();
        this.movesThisTurn++;
        
        if (this.movesThisTurn >= this.maxMovesPerTurn) {
            this.switchTurn();
        }
    }
    
    switchTurn() {
        this.currentTurn = this.currentTurn === 'blue' ? 'red' : 'blue';
        this.movesThisTurn = 0;
    }
    
    wizardAttackGoblin() {
        const movesLeft = this.maxMovesPerTurn - this.movesThisTurn;
        if (movesLeft < 3 || this.wizard.isDead || this.goblin.isDead) return;
        
        const attackDamage = 5;
        this.goblin.hitPoints -= attackDamage;
        this.playHitAnimation(this.goblin);
        
        if (this.goblin.hitPoints <= 0) {
            this.goblin.hitPoints = 0;
            this.markCharacterDead(this.goblin);
        }
        
        this.movesThisTurn += 3;
        if (this.movesThisTurn >= this.maxMovesPerTurn) {
            this.switchTurn();
        }
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

        // Pulse size quickly and taper out over the 1 second window.
        const envelope = 1 - progress;
        const pulse = 1 + (Math.sin(progress * Math.PI * 10) * 0.18 * envelope);
        character.mesh.scale.set(pulse, pulse, 1);

        // Flash: for sprite-based characters (baseColorHex === white) flash red;
        // for solid-color characters flash toward white as normal.
        const flashAmount = Math.abs(Math.sin(progress * Math.PI * 12)) * 0.65 * envelope;
        const baseColor = new THREE.Color(character.baseColorHex);
        const flashTarget = character.baseColorHex === 0xffffff
            ? new THREE.Color(0xff4444)
            : new THREE.Color(0xffffff);
        const hitColor = baseColor.clone().lerp(flashTarget, flashAmount);
        character.mesh.material.color.copy(hitColor);
    }
    
    moveGoblin() {
        if (this.isGameOver || this.currentTurn !== 'red' || this.movesThisTurn >= this.maxMovesPerTurn || this.goblin.isDead) {
            return;
        }

        if (this.goblinAttackWizard()) return;
        
        const dx = this.wizard.gridX - this.goblin.gridX;
        const dy = this.wizard.gridY - this.goblin.gridY;
        const absDx = Math.abs(dx);
        const absDy = Math.abs(dy);
        
        let newX = this.goblin.gridX;
        let newY = this.goblin.gridY;
        
        if (absDx > absDy) {
            if (dx > 0) newX = Math.min(this.gridWidth  - 1, this.goblin.gridX + 1);
            else        newX = Math.max(0,                   this.goblin.gridX - 1);
        } else {
            if (dy > 0) newY = Math.min(this.gridHeight - 1, this.goblin.gridY + 1);
            else        newY = Math.max(0,                   this.goblin.gridY - 1);
        }

        // Resolve primary-direction block: try the alternate axis
        const blocked = (x, y) => (x === this.wizard.gridX && y === this.wizard.gridY) || this.isObstacle(x, y);

        if (blocked(newX, newY)) {
            if (absDx > absDy) {
                newX = this.goblin.gridX;
                if (dy > 0) newY = Math.min(this.gridHeight - 1, this.goblin.gridY + 1);
                else        newY = Math.max(0,                   this.goblin.gridY - 1);
            } else {
                newY = this.goblin.gridY;
                if (dx > 0) newX = Math.min(this.gridWidth  - 1, this.goblin.gridX + 1);
                else        newX = Math.max(0,                   this.goblin.gridX - 1);
            }
            if (blocked(newX, newY)) {
                this.movesThisTurn++;
                if (this.movesThisTurn >= this.maxMovesPerTurn) this.switchTurn();
                return;
            }
        }
        
        const moveDx = newX - this.goblin.gridX;
        const moveDy = newY - this.goblin.gridY;

        this.goblin.gridX = newX;
        this.goblin.gridY = newY;

        if      (moveDx > 0) this.updateCharacterFacing(this.goblin, 'right');
        else if (moveDx < 0) this.updateCharacterFacing(this.goblin, 'left');
        else if (moveDy > 0) this.updateCharacterFacing(this.goblin, 'down');
        else if (moveDy < 0) this.updateCharacterFacing(this.goblin, 'up');
        
        this.updateCharacterPosition(this.goblin);
        this.movesThisTurn++;
        if (this.movesThisTurn >= this.maxMovesPerTurn) this.switchTurn();
    }

    goblinAttackWizard() {
        if (this.isGameOver || this.currentTurn !== 'red' || this.wizard.isDead || this.goblin.isDead) {
            return false;
        }

        const dx = Math.abs(this.wizard.gridX - this.goblin.gridX);
        const dy = Math.abs(this.wizard.gridY - this.goblin.gridY);
        if (dx > 1 || dy > 1) return false;

        const movesLeft = this.maxMovesPerTurn - this.movesThisTurn;
        if (movesLeft < 3) return false;

        const attackDamage = 5;
        this.wizard.hitPoints -= attackDamage;
        this.playHitAnimation(this.wizard);

        if (this.wizard.hitPoints <= 0) {
            this.wizard.hitPoints = 0;
            this.markCharacterDead(this.wizard);
        }

        this.movesThisTurn += 3;
        if (this.movesThisTurn >= this.maxMovesPerTurn) this.switchTurn();

        return true;
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

        // If dead character had the active turn, pass turn immediately.
        if ((character === this.wizard && this.currentTurn === 'blue') ||
            (character === this.goblin && this.currentTurn === 'red')) {
            this.switchTurn();
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

        // Fade child visuals (outline) as well.
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

    getAICharacters() {
        return [this.goblin];
    }

    areAllAICharactersDead() {
        const aiCharacters = this.getAICharacters();
        return aiCharacters.length > 0 && aiCharacters.every((character) => character.isDead);
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
    
    update() {
        const nowMs = performance.now();
        const movesLeft = this.maxMovesPerTurn - this.movesThisTurn;
        const blueHpRatio = Math.max(0, this.wizard.hitPoints / 10);
        const redHpRatio = Math.max(0, this.goblin.hitPoints / 10);

        this.blueHPText.textContent = `${this.wizard.hitPoints} / 10`;
        this.blueHPFill.style.width = `${blueHpRatio * 100}%`;
        this.redHPText.textContent  = `${this.goblin.hitPoints} / 10`;
        this.redHPFill.style.width = `${redHpRatio * 100}%`;

        const deadColor     = '#666666';
        const aliveInfoColor = '#ffffff';

        this.blueSection.style.opacity = this.wizard.isDead ? '0.65' : '1';
        this.redSection.style.opacity  = this.goblin.isDead ? '0.65' : '1';

        this.blueTitle.style.color = this.wizard.isDead ? deadColor : '#0066ff';
        this.redTitle.style.color  = this.goblin.isDead ? deadColor : '#ff3333';

        this.blueRoleText.style.color = this.wizard.isDead ? deadColor : '#b6aa8f';
        this.redRoleText.style.color = this.goblin.isDead ? deadColor : '#b6aa8f';
        this.blueHPText.style.color       = this.wizard.isDead ? deadColor : aliveInfoColor;
        this.redHPText.style.color        = this.goblin.isDead ? deadColor : aliveInfoColor;
        this.blueHPFill.style.opacity = this.wizard.isDead ? '0.35' : '1';
        this.redHPFill.style.opacity = this.goblin.isDead ? '0.35' : '1';

        this.setCombatCardActiveState(this.blueSection, this.blueAccentColor, this.currentTurn === 'blue', this.wizard.isDead);
        this.setCombatCardActiveState(this.redSection, this.redAccentColor, this.currentTurn === 'red', this.goblin.isDead);

        if (this.wizard.isDead) {
            this.blueMovesText.textContent = 'Defeated';
            this.blueMovesText.style.color = deadColor;
        } else if (this.currentTurn === 'blue') {
            this.blueMovesText.textContent = `${movesLeft} of ${this.maxMovesPerTurn} actions remaining`;
            this.blueMovesText.style.color = '#bcb29c';
        } else {
            this.blueMovesText.textContent = '';
            this.blueMovesText.style.color = '#bcb29c';
        }

        if (this.goblin.isDead) {
            this.redMovesText.textContent = 'Defeated';
            this.redMovesText.style.color = deadColor;
        } else if (this.currentTurn === 'red') {
            this.redMovesText.textContent = `${movesLeft} of ${this.maxMovesPerTurn} actions remaining`;
            this.redMovesText.style.color = '#bcb29c';
        } else {
            this.redMovesText.textContent = '';
            this.redMovesText.style.color = '#bcb29c';
        }

        this.fadeAndRemoveCharacter(this.wizard);
        this.fadeAndRemoveCharacter(this.goblin);
        this.updateHitAnimation(this.wizard, nowMs);
        this.updateHitAnimation(this.goblin, nowMs);

        if (!this.isGameOver && this.wizard.isDead) {
            this.startGameOverSequence();
        }

        if (!this.isGameOver && this.areAllAICharactersDead()) {
            this.startVictorySequence();
        }

        if (this.isGameOver) {
            this.updateVictorySequence();
            return;
        }

        this.goblinMoveTimer++;
        if (this.goblinMoveTimer >= 30) {
            this.moveGoblin();
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
