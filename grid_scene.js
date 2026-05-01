// Dungeon Crawler - Grid Scene (main orchestrator)
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

        this.camera = new THREE.OrthographicCamera(
            -viewW / 2,
            viewW / 2,
            viewH / 2,
            -viewH / 2,
            0.1,
            1000
        );
        this.camera.zoom = 0.75;
        this.camera.updateProjectionMatrix();
        this.camera.position.z = 500;

        this.renderer = new THREE.WebGLRenderer({ antialias: true });
        this.renderer.setSize(viewW, viewH);
        this.renderer.setPixelRatio(window.devicePixelRatio);
        this.container.appendChild(this.renderer.domElement);

        this.reachableHighlightGroup = new THREE.Group();
        this.reachableHighlightState = '';
        this.abilityRangeHighlightGroup = new THREE.Group();
        this.abilityRangeHighlightState = '';
        this.targetHighlightGroup = new THREE.Group();
        this.targetHighlightState = '';
        this.hoveredCharacter = null;
        this.lootBagGroup = new THREE.Group();
        this.lootBagState = '';
        this.lootDropsByCell = new Map();
        this.sharedLootInventory = {
            gold: 0,
            gems: {
                garnet: 0,
                peridot: 0,
                citrine: 0
            },
            drops: []
        };
        this.scene.add(this.reachableHighlightGroup);
        this.scene.add(this.abilityRangeHighlightGroup);
        this.scene.add(this.targetHighlightGroup);
        this.scene.add(this.lootBagGroup);

        // Initialize characters from character.js
        this.initializeCharacters();
        this.characterHud = new Map();

        // Turn system
        this.turnOrder = this.createInitiativeTurnOrder(this.characters);
        this.activeTurnIndex = 0;
        this.turnTransitionDelay = 18;
        this.turnTransitionFrames = 0;
        this.enemyMoveTimer = 0;
        this.isGameOver = false;
        this.gameOutcome = null;
        this.victoryStartTime = 0;
        this.victoryFadeDurationMs = 3000;
        this.restartTriggered = false;
        this.cameraPanOffsetX = 0;
        this.cameraPanOffsetY = 0;
        this.isDraggingCamera = false;
        this.suppressClickAfterDrag = false;
        this.minCameraZoom = 0.6;
        this.maxCameraZoom = 2.4;
        this.cameraZoomStep = 0.15;
        this.gameMode = 'exploration';
        this.aggroRangeCells = 6;
        this.enemyGroups = [];
        this.activeEnemyGroupId = null;
        this.explorationLeadCharacter = null;

        this.activeProjectiles = [];

        const dungeon = this.generateDungeonMap();
        this.dungeonMap = dungeon.map;
        this.placeCharacters(dungeon.rooms);
        this.enterExplorationMode();

        this.keysPressed = {};
        this.setupInputListeners();

        this.setupGrid();
        this.setupCharacters();
        this.setupUI();
        this.setupAttackListener();
        this.updateCamera();

        this.animate();
    }

    // --- Grid / Map ---

    getCellKey(gridX, gridY) {
        return `${gridX},${gridY}`;
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

    // --- Character Placement ---

    placeCharacters(rooms) {
        if (rooms.length < 1) {
            return;
        }

        const startRoom = rooms[0];
        const playerFormation = this.findPlayerStartFormation(startRoom);
        this.wizard.gridX = playerFormation.wizard.x;
        this.wizard.gridY = playerFormation.wizard.y;
        this.dwarf.gridX = playerFormation.dwarf.x;
        this.dwarf.gridY = playerFormation.dwarf.y;
        this.cleric.gridX = playerFormation.cleric.x;
        this.cleric.gridY = playerFormation.cleric.y;
        this.ranger.gridX = playerFormation.ranger.x;
        this.ranger.gridY = playerFormation.ranger.y;

        const occupiedCells = new Set([
            this.getCellKey(this.wizard.gridX, this.wizard.gridY),
            this.getCellKey(this.dwarf.gridX, this.dwarf.gridY),
            this.getCellKey(this.cleric.gridX, this.cleric.gridY),
            this.getCellKey(this.ranger.gridX, this.ranger.gridY)
        ]);

        this.spawnEnemyGroupsAcrossDungeon(rooms, occupiedCells);
        this.characters = [...this.playerParty, ...this.aiParty];

        // Keep legacy references pointing at any matching enemy so existing UI text/helpers still work.
        this.goblin = this.aiParty.find((enemy) => enemy.id.includes('goblin-warrior')) || this.aiParty[0] || null;
        this.goblinArcher = this.aiParty.find((enemy) => enemy.id.includes('goblin-archer')) || this.aiParty[0] || null;
        this.goblinShaman = this.aiParty.find((enemy) => enemy.id.includes('goblin-shaman')) || this.aiParty[0] || null;
        this.goblinBrute = this.aiParty.find((enemy) => enemy.id.includes('goblin-brute')) || this.aiParty[0] || null;
    }

    spawnEnemyGroupsAcrossDungeon(rooms, occupiedCells) {
        const baseArchetypes = [this.goblin, this.goblinArcher, this.goblinShaman, this.goblinBrute];
        const enemyRooms = rooms.slice(1);
        const shuffledRooms = [...enemyRooms].sort(() => Math.random() - 0.5);

        const maxGroupsByRooms = Math.max(1, Math.min(6, shuffledRooms.length || 1));
        const minGroups = Math.min(4, maxGroupsByRooms);
        const groupCount = minGroups + Math.floor(Math.random() * (maxGroupsByRooms - minGroups + 1));

        this.enemyGroups = [];
        this.aiParty = [];

        for (let groupIndex = 0; groupIndex < groupCount; groupIndex++) {
            const room = shuffledRooms[groupIndex % Math.max(1, shuffledRooms.length)] || rooms[rooms.length - 1];
            const anchor = {
                x: Math.floor(room.x + room.w / 2),
                y: Math.floor(room.y + room.h / 2)
            };
            const fallback = this.findNearbyFloorTile(anchor.x, anchor.y, 0, 4, occupiedCells) || anchor;
            const memberCount = 2 + Math.floor(Math.random() * 3);
            const members = [];

            for (let memberIndex = 0; memberIndex < memberCount; memberIndex++) {
                const archetype = baseArchetypes[(groupIndex + memberIndex) % baseArchetypes.length];
                const enemy = this.createEnemyFromArchetype(archetype, groupIndex, memberIndex);

                this.placeCharacterNear(enemy, fallback.x, fallback.y, 0, 3, occupiedCells, fallback);
                occupiedCells.add(this.getCellKey(enemy.gridX, enemy.gridY));

                enemy.encounterGroupId = `group-${groupIndex + 1}`;
                members.push(enemy);
                this.aiParty.push(enemy);
            }

            this.enemyGroups.push({
                id: `group-${groupIndex + 1}`,
                members,
                isAggro: false,
                isCleared: false
            });
        }
    }

    createEnemyFromArchetype(archetype, groupIndex, memberIndex) {
        const idSuffix = `g${groupIndex + 1}m${memberIndex + 1}`;
        const clonedAbilities = (archetype.abilities || []).map((ability) => ({ ...ability }));

        return this.createCharacter({
            id: `${archetype.id}-${idSuffix}`,
            name: archetype.name,
            role: archetype.role,
            team: archetype.team,
            accentColor: archetype.accentColor,
            pointerColor: archetype.pointerColor,
            spriteRows: archetype.spriteRows,
            race: archetype.race,
            strength: archetype.strength,
            dexterity: archetype.dexterity,
            intelligence: archetype.intelligence,
            wisdom: archetype.wisdom,
            initiative: archetype.initiative,
            hitPoints: archetype.maxHitPoints,
            maxHitPoints: archetype.maxHitPoints,
            magicPoints: archetype.maxMagicPoints,
            maxMagicPoints: archetype.maxMagicPoints,
            meleeAttackDamage: archetype.meleeAttackDamage,
            armorClass: archetype.armorClass,
            attackCost: archetype.attackCost,
            maxActionsPerTurn: archetype.maxActionsPerTurn,
            experiencePoints: archetype.experiencePoints,
            abilities: clonedAbilities
        });
    }

    getEnemyGroupById(groupId) {
        if (!groupId) {
            return null;
        }
        return this.enemyGroups.find((group) => group.id === groupId) || null;
    }

    getActiveEnemyGroup() {
        return this.getEnemyGroupById(this.activeEnemyGroupId);
    }

    getAggroedEnemyGroups() {
        return this.enemyGroups.filter((group) => group.isAggro && !group.isCleared);
    }

    getAggroedEnemyMembers() {
        return this.getAggroedEnemyGroups().flatMap((group) => this.getLivingCharacters(group.members));
    }

    getCombatEnemiesForPlayers() {
        if (this.gameMode !== 'combat') {
            return [];
        }

        return this.getAggroedEnemyMembers();
    }

    getCombatAlliedEnemies() {
        if (this.gameMode !== 'combat') {
            return [];
        }

        return this.getAggroedEnemyMembers();
    }

    setExplorationLeadCharacter(character) {
        if (!character || this.gameMode !== 'exploration' || character.team !== 'player' || character.isDead) {
            return false;
        }

        this.explorationLeadCharacter = character;
        this.updateTurnOrderQueue(this.getActiveTurnCharacter());
        this.appendCombatLogEntry(`${character.name} is now leading the party.`, character.accentColor);
        this.updateCamera();
        return true;
    }

    refreshCombatTurnOrder(preserveCurrentCharacter = true) {
        const currentCharacter = preserveCurrentCharacter ? this.getActiveTurnCharacter() : null;
        const combatants = [
            ...this.getLivingCharacters(this.playerParty),
            ...this.getAggroedEnemyMembers()
        ];

        this.turnOrder = this.createInitiativeTurnOrder(combatants);

        if (this.turnOrder.length === 0) {
            this.activeTurnIndex = 0;
            return;
        }

        if (currentCharacter && !currentCharacter.isDead) {
            const preservedIndex = this.turnOrder.indexOf(currentCharacter);
            this.activeTurnIndex = preservedIndex >= 0 ? preservedIndex : 0;
            return;
        }

        this.activeTurnIndex = 0;
    }

    enterExplorationMode() {
        this.gameMode = 'exploration';
        const alivePlayers = this.getLivingCharacters(this.playerParty);
        this.explorationLeadCharacter = alivePlayers.find((character) => character === this.dwarf)
            || alivePlayers.find((character) => character === this.wizard)
            || alivePlayers[0]
            || null;
        this.turnOrder = [...alivePlayers];
        this.activeTurnIndex = 0;
        this.turnTransitionFrames = 0;
        this.enemyMoveTimer = 0;
        alivePlayers.forEach((character) => {
            character.actionsRemaining = character.maxActionsPerTurn;
        });
    }

    beginCombatWithGroup(group) {
        if (!group || group.isCleared || group.isAggro) {
            return;
        }

        group.isAggro = true;

        if (this.gameMode === 'combat') {
            this.refreshCombatTurnOrder(true);
            this.appendCombatLogEntry(
                `Enemy group ${group.id} joins the battle.`,
                '#d34c4c'
            );
            return;
        }

        this.gameMode = 'combat';
        this.activeEnemyGroupId = group.id;

        this.refreshCombatTurnOrder(false);
        this.turnTransitionFrames = 0;
        this.enemyMoveTimer = 0;

        this.appendCombatLogEntry(
            `Enemy group ${group.id} spots the party. Combat begins.`,
            '#d34c4c'
        );

        this.beginCurrentTurn();
    }

    resolveCombatGroupState() {
        if (this.gameMode !== 'combat') {
            return;
        }

        let hadGroupDefeated = false;
        this.enemyGroups.forEach((group) => {
            if (!group.isAggro || group.isCleared) {
                return;
            }

            const livingMembers = this.getLivingCharacters(group.members);
            if (livingMembers.length === 0) {
                group.isCleared = true;
                hadGroupDefeated = true;
                this.appendCombatLogEntry(`Enemy group ${group.id} has been defeated.`, '#d9c47d');
            }
        });

        if (hadGroupDefeated) {
            this.refreshCombatTurnOrder(true);
        }

        if (this.getAggroedEnemyMembers().length === 0) {
            this.activeEnemyGroupId = null;
            this.enterExplorationMode();
        }
    }

    tryTriggerEnemyAggro() {
        if (this.isGameOver || (this.gameMode !== 'exploration' && this.gameMode !== 'combat')) {
            return false;
        }

        const alivePlayers = this.getLivingCharacters(this.playerParty);
        for (const group of this.enemyGroups) {
            if (group.isCleared || group.isAggro) {
                continue;
            }

            const livingMembers = this.getLivingCharacters(group.members);
            if (livingMembers.length === 0) {
                group.isCleared = true;
                continue;
            }

            const shouldAggro = livingMembers.some((enemy) =>
                alivePlayers.some((player) => {
                    const distance = this.getAttackDistanceBetweenPositions(enemy.gridX, enemy.gridY, player.gridX, player.gridY);
                    return distance <= this.aggroRangeCells &&
                        this.hasLineOfSightBetweenCells(enemy.gridX, enemy.gridY, player.gridX, player.gridY);
                })
            );

            if (shouldAggro) {
                this.beginCombatWithGroup(group);
                return true;
            }
        }

        return false;
    }

    getExplorationPartyOrder() {
        const alivePlayers = this.getLivingCharacters(this.playerParty);
        const lead = alivePlayers.find((character) => character === this.explorationLeadCharacter) || alivePlayers[0] || null;
        if (!lead) {
            return [];
        }

        return [lead, ...alivePlayers.filter((character) => character !== lead)];
    }

    movePartyInExploration(key) {
        const partyOrder = this.getExplorationPartyOrder();
        const lead = partyOrder[0];
        if (!lead) {
            return;
        }

        let newX = lead.gridX;
        let newY = lead.gridY;

        switch (key) {
            case 'W':
                newY -= 1;
                break;
            case 'S':
                newY += 1;
                break;
            case 'A':
                newX -= 1;
                break;
            case 'D':
                newX += 1;
                break;
            default:
                return;
        }

        const outOfBounds = newX < 0 || newX >= this.gridWidth || newY < 0 || newY >= this.gridHeight;
        if (outOfBounds || this.isObstacle(newX, newY)) {
            return;
        }

        const blockingNonParty = this.characters.some((character) =>
            !character.isDead &&
            !character.removedFromScene &&
            !partyOrder.includes(character) &&
            character.gridX === newX &&
            character.gridY === newY
        );
        if (blockingNonParty) {
            return;
        }

        const previousPositions = new Map(partyOrder.map((character) => [character.id, { x: character.gridX, y: character.gridY }]));
        const occupiedByParty = new Set();

        const leadDx = newX - lead.gridX;
        const leadDy = newY - lead.gridY;
        lead.gridX = newX;
        lead.gridY = newY;
        occupiedByParty.add(this.getCellKey(lead.gridX, lead.gridY));

        if (leadDx > 0) {
            this.updateCharacterFacing(lead, 'right');
        } else if (leadDx < 0) {
            this.updateCharacterFacing(lead, 'left');
        } else if (leadDy > 0) {
            this.updateCharacterFacing(lead, 'down');
        } else if (leadDy < 0) {
            this.updateCharacterFacing(lead, 'up');
        }

        for (let i = 1; i < partyOrder.length; i++) {
            const character = partyOrder[i];
            const predecessor = partyOrder[i - 1];
            const target = previousPositions.get(predecessor.id);
            if (!target) {
                continue;
            }

            const blocked = this.isObstacle(target.x, target.y) || occupiedByParty.has(this.getCellKey(target.x, target.y));
            if (blocked) {
                continue;
            }

            const blockedByEnemy = this.characters.some((candidate) =>
                !candidate.isDead &&
                !candidate.removedFromScene &&
                !partyOrder.includes(candidate) &&
                candidate.gridX === target.x &&
                candidate.gridY === target.y
            );
            if (blockedByEnemy) {
                continue;
            }

            const dx = target.x - character.gridX;
            const dy = target.y - character.gridY;
            character.gridX = target.x;
            character.gridY = target.y;
            occupiedByParty.add(this.getCellKey(character.gridX, character.gridY));

            if (dx > 0) {
                this.updateCharacterFacing(character, 'right');
            } else if (dx < 0) {
                this.updateCharacterFacing(character, 'left');
            } else if (dy > 0) {
                this.updateCharacterFacing(character, 'down');
            } else if (dy < 0) {
                this.updateCharacterFacing(character, 'up');
            }
        }

        partyOrder.forEach((character) => this.updateCharacterPosition(character));
        this.updateCamera();
        this.tryTriggerEnemyAggro();
    }

    findPlayerStartFormation(startRoom) {
        const centerX = Math.floor(startRoom.x + startRoom.w / 2);
        const centerY = Math.floor(startRoom.y + startRoom.h / 2);
        const formations = [
            {
                wizard: { x: centerX, y: centerY },
                dwarf: { x: centerX - 1, y: centerY },
                cleric: { x: centerX + 1, y: centerY },
                ranger: { x: centerX, y: centerY + 1 }
            },
            {
                wizard: { x: centerX, y: centerY },
                dwarf: { x: centerX, y: centerY - 1 },
                cleric: { x: centerX, y: centerY + 1 },
                ranger: { x: centerX + 1, y: centerY }
            },
            {
                wizard: { x: centerX, y: centerY },
                dwarf: { x: centerX - 1, y: centerY },
                cleric: { x: centerX, y: centerY + 1 },
                ranger: { x: centerX + 1, y: centerY }
            },
            {
                wizard: { x: centerX, y: centerY },
                dwarf: { x: centerX + 1, y: centerY },
                cleric: { x: centerX, y: centerY + 1 },
                ranger: { x: centerX - 1, y: centerY }
            }
        ];

        const validFormation = formations.find((formation) =>
            [formation.wizard, formation.dwarf, formation.cleric, formation.ranger].every((position) =>
                position.x >= 0 &&
                position.x < this.gridWidth &&
                position.y >= 0 &&
                position.y < this.gridHeight &&
                this.dungeonMap[position.y][position.x] === this.TILE_FLOOR
            )
        );

        if (validFormation) {
            return validFormation;
        }

        return {
            wizard: { x: centerX, y: centerY },
            dwarf: { x: Math.max(0, centerX - 1), y: centerY },
            cleric: { x: Math.min(this.gridWidth - 1, centerX + 1), y: centerY },
            ranger: { x: centerX, y: Math.min(this.gridHeight - 1, centerY + 1) }
        };
    }

    placeCharacterNear(character, originX, originY, minDistance, maxDistance, occupiedCells, fallbackPosition) {
        const spawn = this.findNearbyFloorTile(originX, originY, minDistance, maxDistance, occupiedCells);
        if (spawn) {
            character.gridX = spawn.x;
            character.gridY = spawn.y;
            return;
        }

        character.gridX = fallbackPosition.x;
        character.gridY = fallbackPosition.y;
    }

    findFallbackRoomCenter(rooms, roomOffsetFromEnd) {
        const roomIndex = Math.max(0, rooms.length + roomOffsetFromEnd);
        const room = rooms[roomIndex] || rooms[rooms.length - 1];
        return {
            x: Math.floor(room.x + room.w / 2),
            y: Math.floor(room.y + room.h / 2)
        };
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

    // --- Turn System ---

    getLivingCharacters(group) {
        return group.filter((character) => !character.isDead);
    }

    getLivingGoblinAllies() {
        const source = this.gameMode === 'combat'
            ? this.getCombatAlliedEnemies()
            : this.aiParty;
        return source.filter((character) => !character.isDead && character.race === 'goblin');
    }

    createInitiativeTurnOrder(characters) {
        const seededCharacters = characters.map((character, index) => ({
            character,
            index,
            randomTieBreaker: Math.random()
        }));

        seededCharacters.sort((left, right) => {
            const leftInitiative = left.character.initiative ?? 0;
            const rightInitiative = right.character.initiative ?? 0;
            if (leftInitiative !== rightInitiative) {
                return rightInitiative - leftInitiative;
            }

            const leftIsPlayer = left.character.team === 'player';
            const rightIsPlayer = right.character.team === 'player';
            if (leftIsPlayer !== rightIsPlayer) {
                return leftIsPlayer ? -1 : 1;
            }

            if (left.randomTieBreaker !== right.randomTieBreaker) {
                return left.randomTieBreaker - right.randomTieBreaker;
            }

            return left.index - right.index;
        });

        return seededCharacters.map((entry) => entry.character);
    }

    getAliveTurnOrder() {
        if (this.gameMode !== 'combat') {
            return this.getLivingCharacters(this.playerParty);
        }
        return this.turnOrder.filter((character) => !character.isDead);
    }

    getActiveTurnCharacter() {
        if (this.gameMode !== 'combat') {
            return this.explorationLeadCharacter || this.getLivingCharacters(this.playerParty)[0] || null;
        }

        const aliveTurnOrder = this.getAliveTurnOrder();
        if (aliveTurnOrder.length === 0) {
            return null;
        }

        const currentCharacter = this.turnOrder[this.activeTurnIndex];
        if (currentCharacter && !currentCharacter.isDead) {
            return currentCharacter;
        }

        const fallbackCharacter = aliveTurnOrder[0];
        this.activeTurnIndex = this.turnOrder.indexOf(fallbackCharacter);
        return fallbackCharacter;
    }

    isPlayerTurn() {
        if (this.gameMode !== 'combat') {
            return true;
        }
        const activeCharacter = this.getActiveTurnCharacter();
        return Boolean(activeCharacter && activeCharacter.team === 'player');
    }

    beginCurrentTurn() {
        if (this.gameMode !== 'combat') {
            return;
        }

        const activeCharacter = this.getActiveTurnCharacter();
        if (!activeCharacter) {
            return;
        }

        this.hoveredCharacter = null;

        if (activeCharacter.maxMagicPoints > 0) {
            const mpRegen = activeCharacter.mpRegen ?? 0;
            if (mpRegen > 0) {
                activeCharacter.magicPoints = Math.min(
                    activeCharacter.maxMagicPoints,
                    activeCharacter.magicPoints + mpRegen
                );
            }
        }

        activeCharacter.actionsRemaining = activeCharacter.maxActionsPerTurn;
        this.turnTransitionFrames = this.turnTransitionDelay;
        this.enemyMoveTimer = 0;
        this.updateCamera();
    }

    endCurrentTurn() {
        if (this.gameMode !== 'combat') {
            return;
        }

        const activeCharacter = this.getActiveTurnCharacter();
        if (activeCharacter) {
            activeCharacter.actionsRemaining = 0;
        }

        const aliveTurnOrder = this.getAliveTurnOrder();
        if (aliveTurnOrder.length === 0) {
            return;
        }

        let nextIndex = this.activeTurnIndex;
        for (let step = 0; step < this.turnOrder.length; step++) {
            nextIndex = (nextIndex + 1) % this.turnOrder.length;
            const candidate = this.turnOrder[nextIndex];
            if (candidate && !candidate.isDead) {
                this.activeTurnIndex = nextIndex;
                this.beginCurrentTurn();
                return;
            }
        }
    }

    // --- Input ---

    setupInputListeners() {
        document.addEventListener('keydown', (e) => {
            const key = e.key.toUpperCase();

            if (this.handleCameraZoomKey(e, key)) {
                return;
            }

            if (this.isGameOver) {
                return;
            }

            if (key === ' ') {
                e.preventDefault();
            }

            if (this.keysPressed[key]) {
                return;
            }

            this.keysPressed[key] = true;

            if (!this.isPlayerTurn()) {
                return;
            }

            if (key === ' ') {
                if (this.gameMode === 'combat') {
                    this.endCurrentTurn();
                }
                return;
            }

            if (this.gameMode === 'exploration') {
                this.movePartyInExploration(key);
                return;
            }

            this.handleMovement(key);
        });

        document.addEventListener('keyup', (e) => {
            const key = e.key.toUpperCase();
            this.keysPressed[key] = false;
        });
    }

    handleCameraZoomKey(event, normalizedKey) {
        if (normalizedKey === '+' || normalizedKey === '=' || normalizedKey === 'ADD') {
            event.preventDefault();
            this.adjustCameraZoom(this.cameraZoomStep);
            return true;
        }

        if (normalizedKey === '-' || normalizedKey === '_' || normalizedKey === 'SUBTRACT') {
            event.preventDefault();
            this.adjustCameraZoom(-this.cameraZoomStep);
            return true;
        }

        return false;
    }

    adjustCameraZoom(zoomDelta) {
        if (!this.camera) {
            return;
        }

        const nextZoom = Math.min(this.maxCameraZoom, Math.max(this.minCameraZoom, this.camera.zoom + zoomDelta));
        if (Math.abs(nextZoom - this.camera.zoom) < 0.0001) {
            return;
        }

        this.camera.zoom = nextZoom;
        this.camera.updateProjectionMatrix();
        this.updateCamera();
    }

    resetCameraPanToActiveCharacter() {
        this.cameraPanOffsetX = 0;
        this.cameraPanOffsetY = 0;
        this.cameraFocusCharacterId = null;
        this.cameraFocusExpiresAt = 0;
        this.updateCamera();
    }

    setupAttackListener() {
        this.raycaster = new THREE.Raycaster();
        this.mouse = new THREE.Vector2();
        this.renderer.domElement.style.cursor = 'grab';

        const panDragState = {
            active: false,
            hasDragged: false,
            startClientX: 0,
            startClientY: 0,
            startPanX: 0,
            startPanY: 0
        };

        const updatePointerTarget = (event) => {
            if (this.isGameOver || this.isDraggingCamera) {
                this.hoveredCharacter = null;
                return;
            }

            const livingCharacters = this.characters.filter((character) => !character.isDead && character.mesh);
            if (livingCharacters.length === 0) {
                this.hoveredCharacter = null;
                return;
            }

            const rect = this.renderer.domElement.getBoundingClientRect();
            this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
            this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
            this.raycaster.setFromCamera(this.mouse, this.camera);

            const intersects = this.raycaster.intersectObjects(livingCharacters.map((character) => character.mesh));
            const hoveredMesh = intersects[0]?.object ?? null;
            this.hoveredCharacter = livingCharacters.find((character) => character.mesh === hoveredMesh) || null;
        };

        const stopPanDrag = (event) => {
            if (!panDragState.active) {
                return;
            }

            panDragState.active = false;
            this.isDraggingCamera = false;
            this.renderer.domElement.style.cursor = 'grab';

            if (panDragState.hasDragged) {
                this.suppressClickAfterDrag = true;
            }

            panDragState.hasDragged = false;
            if (event?.pointerId !== undefined) {
                this.renderer.domElement.releasePointerCapture(event.pointerId);
            }
        };

        this.renderer.domElement.addEventListener('pointerdown', (event) => {
            if (event.button !== 0) {
                return;
            }

            panDragState.active = true;
            panDragState.hasDragged = false;
            panDragState.startClientX = event.clientX;
            panDragState.startClientY = event.clientY;
            panDragState.startPanX = this.cameraPanOffsetX;
            panDragState.startPanY = this.cameraPanOffsetY;
            this.isDraggingCamera = true;
            this.renderer.domElement.style.cursor = 'grabbing';
            this.renderer.domElement.setPointerCapture(event.pointerId);
        });

        this.renderer.domElement.addEventListener('pointermove', (event) => {
            if (!panDragState.active) {
                updatePointerTarget(event);
                return;
            }

            const dragX = event.clientX - panDragState.startClientX;
            const dragY = event.clientY - panDragState.startClientY;
            if (!panDragState.hasDragged && Math.hypot(dragX, dragY) < 4) {
                return;
            }

            panDragState.hasDragged = true;
            this.hoveredCharacter = null;

            const rect = this.renderer.domElement.getBoundingClientRect();
            const zoom = this.camera.zoom || 1;
            const worldPerPixelX = ((this.camera.right - this.camera.left) / zoom) / Math.max(1, rect.width);
            const worldPerPixelY = ((this.camera.top - this.camera.bottom) / zoom) / Math.max(1, rect.height);

            this.cameraPanOffsetX = panDragState.startPanX - (dragX * worldPerPixelX);
            this.cameraPanOffsetY = panDragState.startPanY + (dragY * worldPerPixelY);
            this.updateCamera();
            event.preventDefault();
        });

        this.renderer.domElement.addEventListener('pointerup', stopPanDrag);
        this.renderer.domElement.addEventListener('pointercancel', stopPanDrag);
        this.renderer.domElement.addEventListener('dblclick', (event) => {
            event.preventDefault();
            this.resetCameraPanToActiveCharacter();
        });

        this.renderer.domElement.addEventListener('mousemove', updatePointerTarget);
        this.renderer.domElement.addEventListener('mouseleave', () => {
            this.hoveredCharacter = null;
            this.isDraggingCamera = false;
            panDragState.active = false;
            panDragState.hasDragged = false;
            this.renderer.domElement.style.cursor = 'grab';
        });

        this.renderer.domElement.addEventListener('click', (event) => {
            if (this.suppressClickAfterDrag) {
                this.suppressClickAfterDrag = false;
                return;
            }

            if (this.isGameOver || !this.isPlayerTurn()) {
                return;
            }

            updatePointerTarget(event);

            const activeCharacter = this.getActiveTurnCharacter();
            if (!activeCharacter || activeCharacter.isDead) {
                return;
            }

            const rect = this.renderer.domElement.getBoundingClientRect();
            this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
            this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
            this.raycaster.setFromCamera(this.mouse, this.camera);

            const lootBagIntersects = this.raycaster.intersectObjects(this.lootBagGroup.children);
            if (lootBagIntersects.length > 0) {
                const clickedLootMesh = lootBagIntersects[0].object;
                const lootCellKey = clickedLootMesh?.userData?.lootCellKey;
                if (lootCellKey && this.lootDropsByCell.has(lootCellKey)) {
                    this.openLootMenuForCell(lootCellKey);
                    return;
                }
            }

            if (this.gameMode !== 'combat') {
                return;
            }

            const selectedAbility = activeCharacter.abilities.find((a) => a.id === activeCharacter.selectedAbilityId);
            if (selectedAbility && selectedAbility.type === 'buff') {
                return;
            }

            const isHeal = selectedAbility && selectedAbility.type === 'heal';

            const targetPool = isHeal
                ? this.getLivingCharacters(this.playerParty).filter((c) => c.mesh)
                : this.getCombatEnemiesForPlayers().filter((c) => c.mesh);

            if (targetPool.length === 0) {
                return;
            }

            const intersects = this.raycaster.intersectObjects(targetPool.map((c) => c.mesh));
            if (intersects.length === 0) {
                return;
            }

            const targetCharacter = targetPool.find((c) => c.mesh === intersects[0].object);
            if (targetCharacter) {
                if (isHeal) {
                    this.castHeal(activeCharacter, targetCharacter);
                } else if (selectedAbility && selectedAbility.id === 'magic-missile') {
                    this.castMagicMissile(activeCharacter, targetCharacter);
                } else {
                    this.characterAttack(activeCharacter, targetCharacter, selectedAbility);
                }
            }
        });
    }

    handleMovement(key) {
        if (this.gameMode === 'exploration') {
            this.movePartyInExploration(key);
            return;
        }

        const activeCharacter = this.getActiveTurnCharacter();
        if (
            this.isGameOver ||
            !this.isPlayerTurn() ||
            !activeCharacter ||
            activeCharacter.isDead ||
            activeCharacter.actionsRemaining <= 0
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
        activeCharacter.actionsRemaining -= 1;

        if (activeCharacter.actionsRemaining <= 0) {
            this.endCurrentTurn();
        }
    }

    // --- Combat ---

    characterAttack(attacker, target, attackAbility = null) {
        if (this.gameMode !== 'combat') {
            return false;
        }

        if (!attacker || !target || attacker.isDead || target.isDead || attacker.team === target.team) {
            return false;
        }

        const activeCharacter = this.getActiveTurnCharacter();
        if (activeCharacter !== attacker || attacker.actionsRemaining < attacker.attackCost) {
            return false;
        }

        const resolvedAttackAbility = attackAbility && attackAbility.type === 'attack'
            ? attackAbility
            : attacker.abilities.find((ability) => ability.id === attacker.selectedAbilityId && ability.type === 'attack') || null;

        const attackRange = resolvedAttackAbility?.range ?? 1;
        const baseDamage = resolvedAttackAbility?.damage ?? attacker.meleeAttackDamage;
        const damageKind = attackRange > 1 ? 'ranged' : 'melee';
        const sourceLabel = this.getCharacterAttackSourceLabel(attacker, resolvedAttackAbility);

        const dx = Math.abs(target.gridX - attacker.gridX);
        const dy = Math.abs(target.gridY - attacker.gridY);
        if (dx > attackRange || dy > attackRange) {
            return false;
        }

        if (this.requiresLineOfSight(resolvedAttackAbility) && !this.hasLineOfSightBetweenCells(attacker.gridX, attacker.gridY, target.gridX, target.gridY)) {
            return false;
        }

        const damageDealt = Math.max(0, baseDamage - target.armorClass);

        this.faceCharacterToward(attacker, target);
        if (resolvedAttackAbility?.id === 'bow-shot') {
            const attackerPos = this.getCharacterWorldPos(attacker);
            const targetPos = this.getCharacterWorldPos(target);
            this.spawnArrowProjectile(attackerPos, targetPos, () => {
                this.spawnArrowImpactEffect(targetPos);
                this.applyPhysicalAttackDamage(target, baseDamage, attacker);
            });
        } else {
            this.applyPhysicalAttackDamage(target, baseDamage, attacker);
        }

        this.appendCombatLogEntry(
            `${attacker.name} attacks ${target.name} for ${damageDealt} ${damageKind} dmg with ${sourceLabel}.`,
            attacker.accentColor
        );

        attacker.actionsRemaining -= attacker.attackCost;
        if (attacker.actionsRemaining <= 0) {
            this.endCurrentTurn();
        }

        return true;
    }

    applyPhysicalAttackDamage(target, baseDamage, attacker = null) {
        if (!target || target.isDead) {
            return;
        }

        const physicalDamage = Math.max(0, baseDamage - target.armorClass);
        target.hitPoints -= physicalDamage;
        this.playHitAnimation(target);

        if (target.hitPoints <= 0) {
            target.hitPoints = 0;
            this.markCharacterDead(target, attacker);
        }
    }

    castMagicMissile(caster, target) {
        if (this.gameMode !== 'combat') {
            return false;
        }

        if (!caster || !target || caster.isDead || target.isDead || caster.team === target.team) {
            return false;
        }

        const activeCharacter = this.getActiveTurnCharacter();
        if (activeCharacter !== caster || caster.actionsRemaining < caster.attackCost) {
            return false;
        }

        const ability = caster.abilities.find((a) => a.id === 'magic-missile');
        if (!ability) {
            return false;
        }

        if (caster.magicPoints < ability.mpCost) {
            return false;
        }

        const dx = Math.abs(target.gridX - caster.gridX);
        const dy = Math.abs(target.gridY - caster.gridY);
        if (dx > ability.range || dy > ability.range) {
            return false;
        }

        if (this.requiresLineOfSight(ability) && !this.hasLineOfSightBetweenCells(caster.gridX, caster.gridY, target.gridX, target.gridY)) {
            return false;
        }

        this.faceCharacterToward(caster, target);
        const damage = ability.damage ?? caster.meleeAttackDamage;
        target.hitPoints -= damage;
        caster.magicPoints -= ability.mpCost;

        this.appendCombatLogEntry(
            `${caster.name} casts ${ability.name} on ${target.name} for ${damage} dmg.`,
            caster.accentColor
        );

        const lethal = target.hitPoints <= 0;
        if (lethal) {
            target.hitPoints = 0;
        }

        const casterPos = this.getCharacterWorldPos(caster);
        const targetPos = this.getCharacterWorldPos(target);
        this.spawnMagicMissileProjectiles(casterPos, targetPos, () => {
            this.playHitAnimation(target);
            if (lethal) {
                this.markCharacterDead(target, caster);
            }
        });

        caster.actionsRemaining -= caster.attackCost;
        if (caster.actionsRemaining <= 0) {
            this.endCurrentTurn();
        }

        return true;
    }

    castBattleShout(caster) {
        if (this.gameMode !== 'combat') {
            return false;
        }

        if (!caster || caster.isDead) {
            return false;
        }

        const activeCharacter = this.getActiveTurnCharacter();
        if (activeCharacter !== caster || caster.actionsRemaining < caster.attackCost) {
            return false;
        }

        const ability = caster.abilities.find((a) => a.id === 'battle-shout');
        if (!ability) {
            return false;
        }

        const acBonus = ability.acBonus ?? 1;
        const range = ability.range ?? 3;

        const allies = this.getLivingCharacters(this.playerParty);
        let affectedCount = 0;
        allies.forEach((ally) => {
            const dx = Math.abs(ally.gridX - caster.gridX);
            const dy = Math.abs(ally.gridY - caster.gridY);
            if (dx <= range && dy <= range) {
                ally.armorClass += acBonus;
                affectedCount += 1;
                const pos = this.getCharacterWorldPos(ally);
                this.spawnBattleShoutEffect(pos);
            }
        });

        this.appendCombatLogEntry(
            `${caster.name} uses ${ability.name} and grants +${acBonus} AC to ${affectedCount} ${affectedCount === 1 ? 'ally' : 'allies'}.`,
            caster.accentColor
        );

        caster.actionsRemaining -= caster.attackCost;
        if (caster.actionsRemaining <= 0) {
            this.endCurrentTurn();
        }

        return true;
    }

    castInflictPain(caster) {
        if (this.gameMode !== 'combat') {
            return false;
        }

        if (!caster || caster.isDead) {
            return false;
        }

        const activeCharacter = this.getActiveTurnCharacter();
        if (activeCharacter !== caster || caster.actionsRemaining < caster.attackCost) {
            return false;
        }

        const ability = caster.abilities.find((a) => a.id === 'inflict-pain');
        if (!ability) {
            return false;
        }

        const mpCost = ability.mpCost ?? 0;
        if (caster.magicPoints < mpCost) {
            return false;
        }

        const damageBonus = ability.damageBonus ?? 1;
        const range = ability.range ?? 2;
        let affectedCount = 0;

        this.getLivingGoblinAllies().forEach((ally) => {
            const dx = Math.abs(ally.gridX - caster.gridX);
            const dy = Math.abs(ally.gridY - caster.gridY);
            if (dx <= range && dy <= range) {
                ally.meleeAttackDamage += damageBonus;
                affectedCount += 1;
                const pos = this.getCharacterWorldPos(ally);
                this.spawnInflictPainEffect(pos);
            }
        });

        this.appendCombatLogEntry(
            `${caster.name} casts ${ability.name} and grants +${damageBonus} dmg to ${affectedCount} ${affectedCount === 1 ? 'ally' : 'allies'}.`,
            caster.accentColor
        );

        caster.magicPoints -= mpCost;
        caster.actionsRemaining -= caster.attackCost;
        if (caster.actionsRemaining <= 0) {
            this.endCurrentTurn();
        }

        return true;
    }

    castHeal(caster, target) {
        if (this.gameMode !== 'combat') {
            return false;
        }

        if (!caster || !target || caster.isDead || target.isDead || caster.team !== target.team) {
            return false;
        }

        const activeCharacter = this.getActiveTurnCharacter();
        if (activeCharacter !== caster || caster.actionsRemaining < caster.attackCost) {
            return false;
        }

        const ability = caster.abilities.find((a) => a.id === 'heal');
        if (!ability) {
            return false;
        }

        if (caster.magicPoints < ability.mpCost) {
            return false;
        }

        const dx = Math.abs(target.gridX - caster.gridX);
        const dy = Math.abs(target.gridY - caster.gridY);
        if (dx > ability.range || dy > ability.range) {
            return false;
        }

        if (this.requiresLineOfSight(ability) && !this.hasLineOfSightBetweenCells(caster.gridX, caster.gridY, target.gridX, target.gridY)) {
            return false;
        }

        if (caster !== target) {
            this.faceCharacterToward(caster, target);
        }

        const healAmount = ability.healAmount ?? 5;
        const restored = Math.min(healAmount, target.maxHitPoints - target.hitPoints);
        target.hitPoints = Math.min(target.maxHitPoints, target.hitPoints + healAmount);
        caster.magicPoints -= ability.mpCost;

        this.appendCombatLogEntry(
            `${caster.name} casts ${ability.name} on ${target.name} for ${restored} healing.`,
            caster.accentColor
        );

        const casterPos = this.getCharacterWorldPos(caster);
        const targetPos = this.getCharacterWorldPos(target);
        this.spawnHealEffect(casterPos, targetPos);

        caster.actionsRemaining -= caster.attackCost;
        if (caster.actionsRemaining <= 0) {
            this.endCurrentTurn();
        }

        return true;
    }

    // --- Death / Game State ---

    getTotalExperienceRequiredForLevel(level) {
        if (level <= 1) {
            return 0;
        }

        return 500 * Math.pow(2, level - 2);
    }

    addExperienceToPlayer(character, amount) {
        if (!character || character.team !== 'player' || amount <= 0) {
            return;
        }

        character.experiencePoints = (character.experiencePoints ?? 0) + amount;

        let nextLevel = (character.level ?? 1) + 1;
        while (character.experiencePoints >= this.getTotalExperienceRequiredForLevel(nextLevel)) {
            character.level = nextLevel;
            nextLevel += 1;
        }
    }

    awardEnemyDefeatExperience(defeatedEnemy, defeatedBy) {
        if (!defeatedEnemy || defeatedEnemy.team !== 'ai' || defeatedBy?.team !== 'player') {
            return;
        }

        const totalExperience = Math.max(0, Math.floor(defeatedEnemy.experiencePoints ?? 0));
        if (totalExperience <= 0) {
            return;
        }

        const alivePlayers = this.getLivingCharacters(this.playerParty);
        if (alivePlayers.length === 0) {
            return;
        }

        const baseShare = Math.floor(totalExperience / alivePlayers.length);
        let remainder = totalExperience % alivePlayers.length;

        alivePlayers.forEach((playerCharacter) => {
            const share = baseShare + (remainder > 0 ? 1 : 0);
            if (remainder > 0) {
                remainder -= 1;
            }
            this.addExperienceToPlayer(playerCharacter, share);
        });

        this.appendCombatLogEntry(
            `Party gains ${totalExperience} experience points for killing a ${defeatedEnemy.name}.`,
            '#d9c47d'
        );
    }

    rollEnemyLoot(defeatedEnemy) {
        if (!defeatedEnemy || defeatedEnemy.race !== 'goblin') {
            return null;
        }

        let goldAmount = 0;
        if (Math.random() < 0.25) {
            goldAmount = 1 + Math.floor(Math.random() * 5);
        }

        const gems = [];
        if (Math.random() < 0.10) {
            const lesserGems = ['garnet', 'peridot', 'citrine'];
            const randomGem = lesserGems[Math.floor(Math.random() * lesserGems.length)];
            gems.push(randomGem);
        }

        if (goldAmount <= 0 && gems.length === 0) {
            return null;
        }

        return {
            gold: goldAmount,
            gems
        };
    }

    registerEnemyLootDrop(defeatedEnemy, loot) {
        if (!defeatedEnemy || !loot) {
            return;
        }

        const cellKey = this.getCellKey(defeatedEnemy.gridX, defeatedEnemy.gridY);
        const existingDrop = this.lootDropsByCell.get(cellKey);

        if (existingDrop) {
            existingDrop.gold += loot.gold ?? 0;
            existingDrop.gems.push(...(loot.gems ?? []));
            existingDrop.sources.push(defeatedEnemy.name);
        } else {
            this.lootDropsByCell.set(cellKey, {
                gridX: defeatedEnemy.gridX,
                gridY: defeatedEnemy.gridY,
                gold: loot.gold ?? 0,
                gems: [...(loot.gems ?? [])],
                sources: [defeatedEnemy.name]
            });
        }
    }

    getLootMenuItemsForCell(cellKey) {
        const drop = this.lootDropsByCell.get(cellKey);
        if (!drop) {
            return [];
        }

        const items = [];
        if ((drop.gold ?? 0) > 0) {
            items.push({
                itemKey: 'gold',
                label: 'Gold',
                quantity: drop.gold,
                accentColor: '#ffd86a'
            });
        }

        const gemCounts = {
            garnet: 0,
            peridot: 0,
            citrine: 0
        };
        (drop.gems ?? []).forEach((gemName) => {
            if (gemCounts[gemName] === undefined) {
                gemCounts[gemName] = 0;
            }
            gemCounts[gemName] += 1;
        });

        const gemMeta = [
            { key: 'garnet', label: 'Lesser Gem (Garnet)', accentColor: '#f28b8b' },
            { key: 'peridot', label: 'Lesser Gem (Peridot)', accentColor: '#b7f28b' },
            { key: 'citrine', label: 'Lesser Gem (Citrine)', accentColor: '#f2d08b' }
        ];

        gemMeta.forEach((meta) => {
            const quantity = gemCounts[meta.key] ?? 0;
            if (quantity > 0) {
                items.push({
                    itemKey: `gem:${meta.key}`,
                    label: meta.label,
                    quantity,
                    accentColor: meta.accentColor
                });
            }
        });

        return items;
    }

    addItemToSharedInventory(itemKey, quantity) {
        if (!itemKey || quantity <= 0) {
            return;
        }

        if (itemKey === 'gold') {
            this.sharedLootInventory.gold += quantity;
            return;
        }

        if (itemKey.startsWith('gem:')) {
            const gemName = itemKey.slice(4);
            if (this.sharedLootInventory.gems[gemName] === undefined) {
                this.sharedLootInventory.gems[gemName] = 0;
            }
            this.sharedLootInventory.gems[gemName] += quantity;
        }
    }

    takeLootItem(cellKey, itemKey) {
        const drop = this.lootDropsByCell.get(cellKey);
        if (!drop || !itemKey) {
            return false;
        }

        let takenQuantity = 0;
        if (itemKey === 'gold') {
            takenQuantity = drop.gold ?? 0;
            drop.gold = 0;
        } else if (itemKey.startsWith('gem:')) {
            const gemName = itemKey.slice(4);
            const remaining = [];
            (drop.gems ?? []).forEach((gem) => {
                if (gem === gemName) {
                    takenQuantity += 1;
                } else {
                    remaining.push(gem);
                }
            });
            drop.gems = remaining;
        }

        if (takenQuantity <= 0) {
            return false;
        }

        this.addItemToSharedInventory(itemKey, takenQuantity);

        const actorName = this.getActiveTurnCharacter()?.name ?? 'Party';
        const pickedLabel = itemKey === 'gold'
            ? `${takenQuantity} gold`
            : `${takenQuantity} ${itemKey.slice(4)}`;
        this.appendCombatLogEntry(`${actorName} picks up ${pickedLabel}.`, '#d9c47d');

        this.sharedLootInventory.drops.unshift({
            enemyName: actorName,
            gridX: drop.gridX,
            gridY: drop.gridY,
            gold: itemKey === 'gold' ? takenQuantity : 0,
            gems: itemKey.startsWith('gem:') ? new Array(takenQuantity).fill(itemKey.slice(4)) : []
        });
        if (this.sharedLootInventory.drops.length > 30) {
            this.sharedLootInventory.drops.length = 30;
        }

        const hasGold = (drop.gold ?? 0) > 0;
        const hasGems = (drop.gems ?? []).length > 0;
        if (!hasGold && !hasGems) {
            this.lootDropsByCell.delete(cellKey);
            this.closeLootMenu();
        }

        if (this.activeInventoryCharacter && this.activeInventoryTab === 'shared') {
            this.renderCharacterInventory();
        }

        if (this.activeLootCellKey === cellKey) {
            this.renderLootMenuForCell(cellKey);
        }

        return true;
    }

    leaveLootItemOnGround(cellKey, itemKey) {
        const item = this.getLootMenuItemsForCell(cellKey).find((entry) => entry.itemKey === itemKey);
        if (!item) {
            return;
        }

        const actorName = this.getActiveTurnCharacter()?.name ?? 'Party';
        this.appendCombatLogEntry(`${actorName} leaves ${item.quantity} ${item.label.toLowerCase()} on the ground.`, '#8f856f');
        this.closeLootMenu();
    }

    openLootMenuForCell(cellKey) {
        if (!cellKey || !this.lootDropsByCell.has(cellKey)) {
            return;
        }

        this.openLootMenu(cellKey);
    }

    markCharacterDead(character, defeatedBy = null) {
        if (character.isDead) {
            return;
        }

        character.isDead = true;
        character.fadeFrames = 0;
        character.actionsRemaining = 0;

        this.appendCombatLogEntry(
            `${character.name} dies.`,
            character.accentColor
        );

        const droppedLoot = this.rollEnemyLoot(character);
        if (droppedLoot) {
            this.registerEnemyLootDrop(character, droppedLoot);
            this.appendCombatLogEntry(
                `${character.name} dropped some treasure.`,
                '#d9c47d'
            );
        }

        this.awardEnemyDefeatExperience(character, defeatedBy);

        if (character.hitPoints < 0) {
            character.hitPoints = 0;
        }

        if (this.getActiveTurnCharacter() === character) {
            this.endCurrentTurn();
        }
    }

    areAllPartyMembersDead(group) {
        return group.length > 0 && group.every((character) => character.isDead);
    }

    // --- Game Loop ---

    update() {
        const nowMs = performance.now();
        const activeCharacter = this.getActiveTurnCharacter();

        this.updateProjectiles(nowMs);
        this.updateReachableMovementHighlights(activeCharacter);
        this.updateAbilityRangeHighlights(activeCharacter);
        this.updateTargetHighlights(activeCharacter);
        this.updateTargetPreview(activeCharacter);
        this.updateLootBagMarkers();

        this.characters.forEach((character) => {
            this.updateCharacterCard(character, activeCharacter);
            this.fadeAndRemoveCharacter(character);
            this.updateHitAnimation(character, nowMs);
        });
        this.updateTurnOrderQueue(activeCharacter);

        if (!this.isGameOver && this.areAllPartyMembersDead(this.playerParty)) {
            this.startGameOverSequence();
        }

        this.resolveCombatGroupState();

        if (!this.isGameOver && this.areAllPartyMembersDead(this.aiParty)) {
            this.startVictorySequence();
        }

        if (this.isGameOver) {
            this.updateVictorySequence();
            return;
        }

        if (this.gameMode === 'exploration') {
            this.tryTriggerEnemyAggro();
            return;
        }

        if (this.turnTransitionFrames > 0) {
            this.turnTransitionFrames -= 1;
            return;
        }

        if (activeCharacter && activeCharacter.team === 'ai') {
            this.enemyMoveTimer += 1;
            if (this.enemyMoveTimer >= 24) {
                this.moveAICharacter(activeCharacter);
                this.enemyMoveTimer = 0;
            }
        }
    }

    animate() {
        this.update();
        this.updateCamera();
        this.renderer.render(this.scene, this.camera);
        requestAnimationFrame(() => this.animate());
    }
}

// Mix in methods from module files
Object.assign(GridScene.prototype, window.CharacterData);
Object.assign(GridScene.prototype, window.MapGenerator);
Object.assign(GridScene.prototype, window.GridGraphics);
Object.assign(GridScene.prototype, window.GridUI);
Object.assign(GridScene.prototype, window.GridAI);

// Bootstrap
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        new GridScene('gameContainer');
    });
} else {
    new GridScene('gameContainer');
}
