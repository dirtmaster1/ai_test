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
        this.dungeonPropGroup = new THREE.Group();
        this.dungeonPropsByCell = new Map();
        this.lootBagGroup = new THREE.Group();
        this.lootBagState = '';
        this.lootDropsByCell = new Map();
        this.sharedLootInventory = {
            gold: 0,
            drops: []
        };
        this.scene.add(this.reachableHighlightGroup);
        this.scene.add(this.abilityRangeHighlightGroup);
        this.scene.add(this.targetHighlightGroup);
        this.scene.add(this.dungeonPropGroup);
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
        this.explorationMarchingOrderIds = [];
        this.explorationCellsMovedSinceRegen = 0;
        this.explorationRegenStride = 10;
        this.partyVisionRangeCells = 6;
        this.discoveredCells = new Set();
        this.visibleCells = new Set();
        this.visionNeedsRedraw = true;

        this.activeProjectiles = [];

        const dungeon = this.generateDungeonMap();
        this.dungeonMap = dungeon.map;
        this.dungeonRooms = dungeon.rooms;
        this.placeCharacters(this.dungeonRooms);
        this.populateDungeonProps(this.dungeonRooms);
        this.enterExplorationMode();
        this.updatePartyVisionState();

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

    getVisionSourceCharacter() {
        const alivePlayers = this.getLivingCharacters(this.playerParty);
        if (alivePlayers.length === 0) {
            return null;
        }

        if (this.explorationLeadCharacter && !this.explorationLeadCharacter.isDead) {
            return this.explorationLeadCharacter;
        }

        const activeCharacter = this.getActiveTurnCharacter();
        if (activeCharacter && activeCharacter.team === 'player' && !activeCharacter.isDead) {
            return activeCharacter;
        }

        return alivePlayers[0] || null;
    }

    updatePartyVisionState() {
        const source = this.getVisionSourceCharacter();
        if (!source) {
            if (this.visibleCells.size > 0) {
                this.visibleCells = new Set();
                this.visionNeedsRedraw = true;
            }
            return;
        }

        const radius = this.partyVisionRangeCells ?? 6;
        const nextVisibleCells = new Set();
        let discoveredChanged = false;

        for (let gridY = source.gridY - radius; gridY <= source.gridY + radius; gridY++) {
            for (let gridX = source.gridX - radius; gridX <= source.gridX + radius; gridX++) {
                if (gridX < 0 || gridX >= this.gridWidth || gridY < 0 || gridY >= this.gridHeight) {
                    continue;
                }

                const distance = this.getAttackDistanceBetweenPositions(source.gridX, source.gridY, gridX, gridY);
                if (distance > radius) {
                    continue;
                }

                if (!this.hasLineOfSightBetweenCells(source.gridX, source.gridY, gridX, gridY)) {
                    continue;
                }

                const cellKey = this.getCellKey(gridX, gridY);
                nextVisibleCells.add(cellKey);

                if (!this.discoveredCells.has(cellKey)) {
                    this.discoveredCells.add(cellKey);
                    discoveredChanged = true;
                }
            }
        }

        let visibilityChanged = nextVisibleCells.size !== this.visibleCells.size;
        if (!visibilityChanged) {
            for (const cellKey of nextVisibleCells) {
                if (!this.visibleCells.has(cellKey)) {
                    visibilityChanged = true;
                    break;
                }
            }
        }

        if (visibilityChanged) {
            this.visibleCells = nextVisibleCells;
        }

        if (visibilityChanged || discoveredChanged) {
            this.visionNeedsRedraw = true;
        }
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
        this.warrior.gridX = playerFormation.warrior.x;
        this.warrior.gridY = playerFormation.warrior.y;
        this.cleric.gridX = playerFormation.cleric.x;
        this.cleric.gridY = playerFormation.cleric.y;
        this.ranger.gridX = playerFormation.ranger.x;
        this.ranger.gridY = playerFormation.ranger.y;

        const occupiedCells = new Set([
            this.getCellKey(this.wizard.gridX, this.wizard.gridY),
            this.getCellKey(this.warrior.gridX, this.warrior.gridY),
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

    populateDungeonProps(rooms) {
        this.dungeonPropsByCell.clear();

        if (!rooms || rooms.length === 0) {
            return;
        }

        const occupiedCells = new Set(
            this.characters
                .filter((character) => !character.isDead)
                .map((character) => this.getCellKey(character.gridX, character.gridY))
        );

        const themedRooms = rooms.slice(1);
        const shuffledRooms = [...themedRooms].sort(() => Math.random() - 0.5);

        const roomThemes = ['barracks', 'armory', 'storage'];
        shuffledRooms.forEach((room, index) => {
            if (room.w < 5 || room.h < 5) {
                return;
            }

            const theme = roomThemes[index % roomThemes.length];
            this.populateRoomPropsByTheme(room, theme, occupiedCells);
        });

        this.populateSpikeTraps(shuffledRooms, occupiedCells);
    }

    populateRoomPropsByTheme(room, theme, occupiedCells) {
        const themeFrames = {
            barracks: ['bed', 'chestClosedIron', 'tableCandles', 'chair', 'chair'],
            armory: ['weaponRack1', 'weaponRack2', 'weaponRack3', 'chestClosedSteel'],
            storage: ['crate', 'barrels1', 'barrel', 'barrels2', 'chestClosedGold']
        };

        const framePool = themeFrames[theme] || [];
        if (framePool.length === 0) {
            return;
        }

        const interiorCells = [];
        for (let y = room.y + 1; y < room.y + room.h - 1; y++) {
            for (let x = room.x + 1; x < room.x + room.w - 1; x++) {
                if (this.dungeonMap[y]?.[x] !== this.TILE_FLOOR) {
                    continue;
                }

                const cellKey = this.getCellKey(x, y);
                if (occupiedCells.has(cellKey) || this.dungeonPropsByCell.has(cellKey)) {
                    continue;
                }

                interiorCells.push({ x, y, key: cellKey });
            }
        }

        if (interiorCells.length === 0) {
            return;
        }

        const roomArea = room.w * room.h;
        const minProps = Math.max(2, Math.floor(roomArea / 36));
        const maxProps = Math.min(framePool.length + 1, Math.max(minProps, Math.floor(roomArea / 20)));
        const targetProps = minProps + Math.floor(Math.random() * Math.max(1, maxProps - minProps + 1));

        for (let i = 0; i < targetProps && interiorCells.length > 0; i++) {
            const pickIndex = Math.floor(Math.random() * interiorCells.length);
            const pickedCell = interiorCells.splice(pickIndex, 1)[0];
            const frameId = framePool[Math.floor(Math.random() * framePool.length)];
            const isSearchable = !frameId.startsWith('spikeTrap');

            this.dungeonPropsByCell.set(pickedCell.key, {
                gridX: pickedCell.x,
                gridY: pickedCell.y,
                frameId,
                roomTheme: theme,
                searchable: isSearchable,
                hasBeenSearched: false
            });

            occupiedCells.add(pickedCell.key);
        }
    }

    populateSpikeTraps(rooms, occupiedCells) {
        if (!rooms || rooms.length === 0) {
            return;
        }

        const trapAttempts = Math.min(12, Math.max(4, Math.floor(rooms.length * 1.2)));
        const trapFrames = ['spikeTrap1', 'spikeTrap2'];

        for (let i = 0; i < trapAttempts; i++) {
            const room = rooms[Math.floor(Math.random() * rooms.length)];
            const x = room.x + Math.floor(Math.random() * room.w);
            const y = room.y + Math.floor(Math.random() * room.h);

            if (x <= room.x || x >= room.x + room.w - 1 || y <= room.y || y >= room.y + room.h - 1) {
                continue;
            }

            if (this.dungeonMap[y]?.[x] !== this.TILE_FLOOR) {
                continue;
            }

            const cellKey = this.getCellKey(x, y);
            if (occupiedCells.has(cellKey) || this.dungeonPropsByCell.has(cellKey)) {
                continue;
            }

            const frameId = trapFrames[Math.floor(Math.random() * trapFrames.length)];
            this.dungeonPropsByCell.set(cellKey, {
                gridX: x,
                gridY: y,
                frameId,
                roomTheme: 'trap',
                searchable: false,
                hasBeenSearched: false
            });

            occupiedCells.add(cellKey);
        }
    }

    spawnEnemyGroupsAcrossDungeon(rooms, occupiedCells) {
        const baseArchetypes = [this.goblin, this.goblinArcher, this.goblinShaman, this.goblinBrute];
        const enemyRooms = rooms.slice(1);
        const shuffledRooms = [...enemyRooms].sort(() => Math.random() - 0.5);

        const maxGroupsByRooms = Math.max(1, Math.min(6, shuffledRooms.length || 1));
        const minGroups = Math.min(4, maxGroupsByRooms);
        const baseGroupCount = minGroups + Math.floor(Math.random() * (maxGroupsByRooms - minGroups + 1));
        const maxSafeGroups = Math.max(2, (shuffledRooms.length || 1) * 3);
        const groupCount = Math.min(baseGroupCount * 2, maxSafeGroups);

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
            spriteFrame: archetype.spriteFrame,
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

        this.syncExplorationMarchingOrder();
        const leadIndex = this.explorationMarchingOrderIds.indexOf(character.id);
        if (leadIndex > 0) {
            this.explorationMarchingOrderIds.splice(leadIndex, 1);
            this.explorationMarchingOrderIds.unshift(character.id);
        } else if (leadIndex < 0) {
            this.explorationMarchingOrderIds.unshift(character.id);
        }

        this.explorationLeadCharacter = this.getExplorationPartyOrder()[0] || character;
        this.turnOrder = [...this.getExplorationPartyOrder()];
        this.updatePartyVisionState();
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

        if (!this.explorationMarchingOrderIds || this.explorationMarchingOrderIds.length === 0) {
            const defaultLead = alivePlayers.find((character) => character === this.warrior)
                || alivePlayers.find((character) => character === this.wizard)
                || alivePlayers[0]
                || null;
            this.explorationMarchingOrderIds = alivePlayers
                .filter((character) => character !== defaultLead)
                .map((character) => character.id);
            if (defaultLead) {
                this.explorationMarchingOrderIds.unshift(defaultLead.id);
            }
        }

        const explorationOrder = this.getExplorationPartyOrder();
        this.explorationLeadCharacter = explorationOrder[0] || null;
        this.turnOrder = [...explorationOrder];
        this.activeTurnIndex = 0;
        this.turnTransitionFrames = 0;
        this.enemyMoveTimer = 0;
        this.explorationCellsMovedSinceRegen = 0;
        alivePlayers.forEach((character) => {
            character.actionsRemaining = character.maxActionsPerTurn;
        });
    }

    applyExplorationMovementRegen(cellsMoved = 1) {
        if (this.gameMode !== 'exploration' || cellsMoved <= 0) {
            return;
        }

        this.explorationCellsMovedSinceRegen += cellsMoved;

        while (this.explorationCellsMovedSinceRegen >= this.explorationRegenStride) {
            this.explorationCellsMovedSinceRegen -= this.explorationRegenStride;

            const alivePlayers = this.getLivingCharacters(this.playerParty);
            let hadRecovery = false;

            alivePlayers.forEach((character) => {
                if (character.hitPoints < character.maxHitPoints) {
                    character.hitPoints = Math.min(character.maxHitPoints, character.hitPoints + 1);
                    hadRecovery = true;
                }

                if ((character.maxMagicPoints ?? 0) > 0 && character.magicPoints < character.maxMagicPoints) {
                    character.magicPoints = Math.min(character.maxMagicPoints, character.magicPoints + 1);
                    hadRecovery = true;
                }
            });

            if (hadRecovery) {
                this.appendCombatLogEntry('Exploration recovery: party restores 1 HP and 1 MP.', '#8fd3ff');
            }
        }
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

        let didAggro = false;
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
                didAggro = true;
            }
        }

        return didAggro;
    }

    getExplorationPartyOrder() {
        const alivePlayers = this.getLivingCharacters(this.playerParty);
        if (alivePlayers.length === 0) {
            this.explorationMarchingOrderIds = [];
            return [];
        }

        this.syncExplorationMarchingOrder(alivePlayers);
        const aliveById = new Map(alivePlayers.map((character) => [character.id, character]));
        return this.explorationMarchingOrderIds
            .map((characterId) => aliveById.get(characterId))
            .filter(Boolean);
    }

    syncExplorationMarchingOrder(alivePlayers = null) {
        const livingPlayers = alivePlayers || this.getLivingCharacters(this.playerParty);
        const livingIds = new Set(livingPlayers.map((character) => character.id));
        const currentOrder = Array.isArray(this.explorationMarchingOrderIds)
            ? this.explorationMarchingOrderIds.filter((characterId) => livingIds.has(characterId))
            : [];

        const currentOrderSet = new Set(currentOrder);
        const appendOrder = livingPlayers
            .map((character) => character.id)
            .filter((characterId) => !currentOrderSet.has(characterId));

        this.explorationMarchingOrderIds = [...currentOrder, ...appendOrder];
    }

    reorderExplorationMarchingOrder(draggedCharacterId, targetCharacterId) {
        if (this.gameMode !== 'exploration' || !draggedCharacterId || !targetCharacterId || draggedCharacterId === targetCharacterId) {
            return false;
        }

        const partyOrder = this.getExplorationPartyOrder();
        const livingPlayerIds = new Set(partyOrder.map((character) => character.id));
        if (!livingPlayerIds.has(draggedCharacterId) || !livingPlayerIds.has(targetCharacterId)) {
            return false;
        }

        const fromIndex = this.explorationMarchingOrderIds.indexOf(draggedCharacterId);
        const toIndex = this.explorationMarchingOrderIds.indexOf(targetCharacterId);
        if (fromIndex < 0 || toIndex < 0 || fromIndex === toIndex) {
            return false;
        }

        this.explorationMarchingOrderIds.splice(fromIndex, 1);
        const insertionIndex = fromIndex < toIndex ? toIndex - 1 : toIndex;
        this.explorationMarchingOrderIds.splice(insertionIndex, 0, draggedCharacterId);

        const nextOrder = this.getExplorationPartyOrder();
        this.explorationLeadCharacter = nextOrder[0] || null;
        this.turnOrder = [...nextOrder];
        this.updatePartyVisionState();
        this.updateTurnOrderQueue(this.getActiveTurnCharacter());
        this.updateCamera();
        this.appendCombatLogEntry('Marching order updated.', '#8fd3ff');
        return true;
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
        const leadCharacter = partyOrder[0];
        if (leadCharacter) {
            this.tryAutoOpenLootFromCharacterCell(leadCharacter);
        }
        this.applyExplorationMovementRegen(1);
        this.tryTriggerEnemyAggro();
    }

    findPlayerStartFormation(startRoom) {
        const centerX = Math.floor(startRoom.x + startRoom.w / 2);
        const centerY = Math.floor(startRoom.y + startRoom.h / 2);
        const formations = [
            {
                wizard: { x: centerX, y: centerY },
                warrior: { x: centerX - 1, y: centerY },
                cleric: { x: centerX + 1, y: centerY },
                ranger: { x: centerX, y: centerY + 1 }
            },
            {
                wizard: { x: centerX, y: centerY },
                warrior: { x: centerX, y: centerY - 1 },
                cleric: { x: centerX, y: centerY + 1 },
                ranger: { x: centerX + 1, y: centerY }
            },
            {
                wizard: { x: centerX, y: centerY },
                warrior: { x: centerX - 1, y: centerY },
                cleric: { x: centerX, y: centerY + 1 },
                ranger: { x: centerX + 1, y: centerY }
            },
            {
                wizard: { x: centerX, y: centerY },
                warrior: { x: centerX + 1, y: centerY },
                cleric: { x: centerX, y: centerY + 1 },
                ranger: { x: centerX - 1, y: centerY }
            }
        ];

        const validFormation = formations.find((formation) =>
            [formation.wizard, formation.warrior, formation.cleric, formation.ranger].every((position) =>
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
            warrior: { x: Math.max(0, centerX - 1), y: centerY },
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
            return this.getExplorationPartyOrder();
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

            const targetElement = e.target;
            if (
                targetElement instanceof HTMLElement &&
                (targetElement.isContentEditable ||
                    targetElement.tagName === 'INPUT' ||
                    targetElement.tagName === 'TEXTAREA' ||
                    targetElement.tagName === 'SELECT')
            ) {
                return;
            }

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

            if (this.handleCharacterInventoryKey(e, key)) {
                return;
            }

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

    handleCharacterInventoryKey(event, normalizedKey) {
        if (normalizedKey !== 'I') {
            return false;
        }

        const openCharacter = this.getInventoryHotkeyCharacter();
        if (!openCharacter) {
            return true;
        }

        event.preventDefault();
        this.openCharacterInventory(openCharacter);
        return true;
    }

    getInventoryHotkeyCharacter() {
        const alivePlayers = this.getLivingCharacters(this.playerParty);
        if (alivePlayers.length === 0) {
            return null;
        }

        if (this.activeInventoryCharacter && !this.activeInventoryCharacter.isDead) {
            return this.activeInventoryCharacter;
        }

        const activeCharacter = this.getActiveTurnCharacter();
        if (activeCharacter && activeCharacter.team === 'player' && !activeCharacter.isDead) {
            return activeCharacter;
        }

        if (this.explorationLeadCharacter && !this.explorationLeadCharacter.isDead) {
            return this.explorationLeadCharacter;
        }

        return alivePlayers[0] || null;
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

            const rect = this.renderer.domElement.getBoundingClientRect();
            this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
            this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
            this.raycaster.setFromCamera(this.mouse, this.camera);

            const livingCharacters = this.characters.filter((character) => !character.isDead && character.mesh);
            if (livingCharacters.length === 0) {
                this.hoveredCharacter = null;
                return;
            }

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

            const interactor = this.getLootInteractionCharacter();
            if (!interactor || interactor.isDead) {
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
                    this.openLootMenuForCell(lootCellKey, interactor);
                    return;
                }
            }

            const dungeonPropIntersects = this.raycaster.intersectObjects(this.dungeonPropGroup.children);
            if (dungeonPropIntersects.length > 0) {
                const clickedPropMesh = dungeonPropIntersects[0].object;
                const propCellKey = clickedPropMesh?.userData?.propCellKey;
                if (propCellKey && this.dungeonPropsByCell.has(propCellKey)) {
                    if (this.trySearchDungeonPropAtCell(propCellKey, interactor, event.clientX, event.clientY)) {
                        return;
                    }
                }
            }

            if (this.gameMode !== 'combat') {
                return;
            }

            const activeCharacter = this.getActiveTurnCharacter();
            if (!activeCharacter || activeCharacter.isDead) {
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

    getLootInteractionCharacter() {
        if (this.gameMode === 'combat') {
            const activeCharacter = this.getActiveTurnCharacter();
            return activeCharacter && activeCharacter.team === 'player' && !activeCharacter.isDead
                ? activeCharacter
                : null;
        }

        const lead = this.explorationLeadCharacter;
        if (lead && !lead.isDead) {
            return lead;
        }

        return this.getLivingCharacters(this.playerParty)[0] || null;
    }

    canCharacterInteractWithCell(character, gridX, gridY, maxDistance = 1) {
        if (!character || character.isDead) {
            return false;
        }

        const distance = this.getAttackDistanceBetweenPositions(character.gridX, character.gridY, gridX, gridY);
        return distance <= maxDistance;
    }

    parseCellKey(cellKey) {
        if (!cellKey || typeof cellKey !== 'string') {
            return null;
        }

        const [gridXRaw, gridYRaw] = cellKey.split(',');
        const gridX = Number(gridXRaw);
        const gridY = Number(gridYRaw);
        if (!Number.isFinite(gridX) || !Number.isFinite(gridY)) {
            return null;
        }

        return { gridX, gridY };
    }

    trySearchDungeonPropAtCell(cellKey, interactor = null, screenX = null, screenY = null) {
        const prop = this.dungeonPropsByCell.get(cellKey);
        if (!prop || !prop.searchable) {
            return false;
        }

        const actingCharacter = interactor || this.getLootInteractionCharacter();
        if (!this.canCharacterInteractWithCell(actingCharacter, prop.gridX, prop.gridY, 1)) {
            return false;
        }

        if (!this.lootDropsByCell.has(cellKey) && !prop.hasBeenSearched) {
            prop.hasBeenSearched = true;
            const loot = this.rollDungeonPropLoot(prop);
            if (loot && (loot.gold ?? 0) > 0) {
                this.registerLootDropAtCell(prop.gridX, prop.gridY, loot, {
                    sourceType: 'prop',
                    sourceLabel: prop.roomTheme || 'Room Fixture',
                    containerName: `Search: ${prop.frameId}`
                });
                this.appendCombatLogEntry(
                    `${actingCharacter.name} searches ${prop.frameId} and finds something.`,
                    '#d9c47d'
                );
            } else {
                this.showToast('Empty', '#8f856f', 2200, screenX, screenY);
                return true;
            }
        }

        if (!this.lootDropsByCell.has(cellKey) && prop.hasBeenSearched) {
            this.showToast('Empty', '#8f856f', 2200, screenX, screenY);
            return true;
        }

        if (this.lootDropsByCell.has(cellKey)) {
            return this.openLootMenuForCell(cellKey, actingCharacter);
        }

        return true;
    }

    tryAutoOpenLootFromCharacterCell(character) {
        if (!character || character.team !== 'player' || character.isDead) {
            return false;
        }

        const cellKey = this.getCellKey(character.gridX, character.gridY);
        if (this.dungeonPropsByCell.has(cellKey)) {
            const worldPos = this.getWorldPositionForCell(character.gridX, character.gridY);
            const vec = new THREE.Vector3(worldPos.x, worldPos.y, 0);
            vec.project(this.camera);
            const rect = this.renderer.domElement.getBoundingClientRect();
            const screenX = (vec.x * 0.5 + 0.5) * rect.width + rect.left;
            const screenY = (-vec.y * 0.5 + 0.5) * rect.height + rect.top;
            const searched = this.trySearchDungeonPropAtCell(cellKey, character, screenX, screenY);
            if (searched) {
                return true;
            }
        }

        if (this.lootDropsByCell.has(cellKey)) {
            return this.openLootMenuForCell(cellKey, character);
        }

        return false;
    }

    rollDungeonPropLoot(prop) {
        if (!prop || !prop.searchable) {
            return null;
        }

        const chestFrames = new Set(['chestClosedIron', 'chestClosedGold', 'chestClosedSteel']);
        const rackFrames = new Set(['weaponRack1', 'weaponRack2', 'weaponRack3']);
        const storageFrames = new Set(['crate', 'barrel', 'barrels1', 'barrels2']);

        let chance = 0.35;
        let minGold = 1;
        let maxGold = 4;

        if (chestFrames.has(prop.frameId)) {
            chance = 0.9;
            minGold = 4;
            maxGold = 14;
        } else if (rackFrames.has(prop.frameId)) {
            chance = 0.55;
            minGold = 2;
            maxGold = 8;
        } else if (storageFrames.has(prop.frameId)) {
            chance = 0.65;
            minGold = 1;
            maxGold = 7;
        }

        if (Math.random() > chance) {
            return null;
        }

        const gold = minGold + Math.floor(Math.random() * (maxGold - minGold + 1));
        return { gold };
    }

    registerLootDropAtCell(gridX, gridY, loot, options = {}) {
        if (!loot) {
            return;
        }

        const cellKey = this.getCellKey(gridX, gridY);
        const existingDrop = this.lootDropsByCell.get(cellKey);
        const goldToAdd = Math.max(0, Math.floor(loot.gold ?? 0));
        if (goldToAdd <= 0 && !existingDrop) {
            return;
        }

        if (existingDrop) {
            existingDrop.gold = (existingDrop.gold ?? 0) + goldToAdd;
            if (options.sourceLabel) {
                existingDrop.sources.push(options.sourceLabel);
            }
            if (options.sourceType) {
                existingDrop.sourceType = options.sourceType;
            }
            if (options.containerName) {
                existingDrop.containerName = options.containerName;
            }
            return;
        }

        this.lootDropsByCell.set(cellKey, {
            gridX,
            gridY,
            gold: goldToAdd,
            sources: options.sourceLabel ? [options.sourceLabel] : [],
            sourceType: options.sourceType || 'enemy',
            containerName: options.containerName || 'Loot Bag'
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
        this.tryAutoOpenLootFromCharacterCell(activeCharacter);
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

    getLevelUpBonusesForCharacter(character) {
        if (!character || character.team !== 'player') {
            return null;
        }

        switch (character.id) {
            case 'dwarf-warrior':
                return { hp: 2, strength: 2 };
            case 'cleric':
                return { hp: 1, mp: 1, wisdom: 1 };
            case 'wizard':
                return { hp: 1, mp: 2, intelligence: 1 };
            case 'ranger-aragon':
                return { hp: 1, initiative: 1, dexterity: 1 };
            default:
                return null;
        }
    }

    applyLevelUpBonuses(character, newLevel) {
        const bonuses = this.getLevelUpBonusesForCharacter(character);
        if (!bonuses) {
            return;
        }

        const hpGain = bonuses.hp ?? 0;
        if (hpGain > 0) {
            character.maxHitPoints += hpGain;
            character.hitPoints = Math.min(character.maxHitPoints, character.hitPoints + hpGain);
        }

        const mpGain = bonuses.mp ?? 0;
        if (mpGain > 0) {
            character.maxMagicPoints = (character.maxMagicPoints ?? 0) + mpGain;
            character.magicPoints = Math.min(character.maxMagicPoints, (character.magicPoints ?? 0) + mpGain);
        }

        character.strength = (character.strength ?? 0) + (bonuses.strength ?? 0);
        character.wisdom = (character.wisdom ?? 0) + (bonuses.wisdom ?? 0);
        character.intelligence = (character.intelligence ?? 0) + (bonuses.intelligence ?? 0);
        character.dexterity = (character.dexterity ?? 0) + (bonuses.dexterity ?? 0);
        character.initiative = (character.initiative ?? 0) + (bonuses.initiative ?? 0);

        const bonusParts = [];
        if (hpGain > 0) bonusParts.push(`+${hpGain} HP`);
        if (mpGain > 0) bonusParts.push(`+${mpGain} MP`);
        if ((bonuses.strength ?? 0) > 0) bonusParts.push(`+${bonuses.strength} STR`);
        if ((bonuses.wisdom ?? 0) > 0) bonusParts.push(`+${bonuses.wisdom} WIS`);
        if ((bonuses.intelligence ?? 0) > 0) bonusParts.push(`+${bonuses.intelligence} INT`);
        if ((bonuses.dexterity ?? 0) > 0) bonusParts.push(`+${bonuses.dexterity} DEX`);
        if ((bonuses.initiative ?? 0) > 0) bonusParts.push(`+${bonuses.initiative} Initiative`);

        this.appendCombatLogEntry(
            `${character.name} reached level ${newLevel}: ${bonusParts.join(', ')}.`,
            character.accentColor || '#d9c47d'
        );
    }

    addExperienceToPlayer(character, amount) {
        if (!character || character.team !== 'player' || amount <= 0) {
            return;
        }

        character.experiencePoints = (character.experiencePoints ?? 0) + amount;

        let nextLevel = (character.level ?? 1) + 1;
        while (character.experiencePoints >= this.getTotalExperienceRequiredForLevel(nextLevel)) {
            character.level = nextLevel;
            this.applyLevelUpBonuses(character, nextLevel);
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

        if (goldAmount <= 0) {
            return null;
        }

        return {
            gold: goldAmount
        };
    }

    registerEnemyLootDrop(defeatedEnemy, loot) {
        if (!defeatedEnemy || !loot) {
            return;
        }

        this.registerLootDropAtCell(defeatedEnemy.gridX, defeatedEnemy.gridY, loot, {
            sourceType: 'enemy',
            sourceLabel: defeatedEnemy.name,
            containerName: 'Loot Bag'
        });
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

        return items;
    }

    addItemToSharedInventory(itemKey, quantity) {
        if (!itemKey || quantity <= 0) {
            return;
        }

        if (itemKey === 'gold') {
            this.sharedLootInventory.gold += quantity;
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
        }

        if (takenQuantity <= 0) {
            return false;
        }

        this.addItemToSharedInventory(itemKey, takenQuantity);

        const actorName = this.getLootInteractionCharacter()?.name ?? 'Party';
        this.appendCombatLogEntry(`${actorName} picks up ${takenQuantity} gold.`, '#d9c47d');

        this.sharedLootInventory.drops.unshift({
            enemyName: actorName,
            gridX: drop.gridX,
            gridY: drop.gridY,
            gold: takenQuantity
        });
        if (this.sharedLootInventory.drops.length > 30) {
            this.sharedLootInventory.drops.length = 30;
        }

        const hasGold = (drop.gold ?? 0) > 0;
        if (!hasGold) {
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

    openLootMenuForCell(cellKey, interactor = null) {
        if (!cellKey || !this.lootDropsByCell.has(cellKey)) {
            return false;
        }

        const actingCharacter = interactor || this.getLootInteractionCharacter();
        const cell = this.parseCellKey(cellKey);
        if (!actingCharacter || !cell || !this.canCharacterInteractWithCell(actingCharacter, cell.gridX, cell.gridY, 1)) {
            return false;
        }

        this.openLootMenu(cellKey);
        return true;
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
        this.updatePartyVisionState();

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

        if (this.gameMode === 'combat') {
            this.tryTriggerEnemyAggro();
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
