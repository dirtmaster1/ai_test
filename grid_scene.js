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
        this.activeCharacterTurnHighlightGroup = new THREE.Group();
        this.activeCharacterTurnHighlightState = '';
        this.hoveredCharacter = null;
        this.hoveredCell = null;
        this.dungeonPropGroup = new THREE.Group();
        this.dungeonPropsByCell = new Map();
        this.lootBagGroup = new THREE.Group();
        this.lootBagState = '';
        this.lootDropsByCell = new Map();
        this.doorGroup = new THREE.Group();
        this.doorStatesByCell = new Map();
        this.doorMeshesByCell = new Map();
        this.mapTransitionsByCell = new Map();
        this.persistedMapStateById = new Map();
        this.activeDungeonMapId = 'forest-town';
        this.sharedLootInventory = {
            gold: 0,
            drops: [],
            items: []
        };
        this.nextLootItemId = 1;
        this.scene.add(this.reachableHighlightGroup);
        this.scene.add(this.abilityRangeHighlightGroup);
        this.scene.add(this.targetHighlightGroup);
        this.scene.add(this.activeCharacterTurnHighlightGroup);
        this.scene.add(this.dungeonPropGroup);
        this.scene.add(this.lootBagGroup);
        this.scene.add(this.doorGroup);

        // Initialize characters from character.js
        this.initializeCharacters();
        this.enemyArchetypeTemplatesById = this.buildEnemyArchetypeTemplateMap();
        this.characterHud = new Map();
        this.summonedAllies = [];
        this.summonedEnemies = [];
        this.nextSummonedAllyId = 1;
        this.nextSummonedEnemyId = 1;

        // Turn system
        this.turnOrder = this.createInitiativeTurnOrder(this.characters);
        this.activeTurnIndex = 0;
        this.turnTransitionDelay = 18;
        this.turnTransitionFrames = 0;
        this.sleepSkipTurnTimeoutId = null;
        this.enemyMoveTimer = 0;
        this.pendingCombatAction = null;
        this.isGameOver = false;
        this.gameOverStartTime = 0;
        this.gameOverFadeDurationMs = 3000;
        this.restartTriggered = false;
        this.combatTransitionStartTime = 0;
        this.combatTransitionDurationMs = 2500;
        this.combatTransitionActive = false;
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
        this.explorationClickMoveQueue = [];
        this.explorationClickMoveTimer = 0;
        this.explorationClickMoveStepInterval = 14;
        this.partyVisionRangeCells = 6;
        this.discoveredCells = new Set();
        this.visibleCells = new Set();
        this.visionNeedsRedraw = true;

        this.activeProjectiles = [];

        const dungeon = this.generateDungeonMap();
        this.dungeonMap = dungeon.map;
        this.dungeonRooms = dungeon.rooms || [];
        this.dungeonLayout = dungeon.layout || null;
        this.initializeDoorStates(dungeon);
        this.initializeMapTransitions(dungeon);
        this.placeCharacters(dungeon);
        this.populateDungeonProps(dungeon);
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

        const doorState = this.getDoorStateAtCell(gridX, gridY);
        if (doorState && !doorState.isOpen) {
            return true;
        }

        return this.dungeonMap[gridY][gridX] !== this.TILE_FLOOR;
    }

    initializeDoorStates(dungeon) {
        this.doorStatesByCell.clear();

        const layout = Array.isArray(dungeon) ? null : dungeon?.layout || this.dungeonLayout;
        const baseCellTypes = layout?.baseCellTypes || {};

        Object.entries(baseCellTypes).forEach(([cellKey, baseType]) => {
            if (baseType !== 'door') {
                return;
            }

            const cell = this.parseCellKey(cellKey);
            if (!cell) {
                return;
            }

            const anchorDirection = this.getDoorAnchorDirection(cell.gridX, cell.gridY);
            const swingDirection = this.getDoorSwingDirectionFromAnchor(anchorDirection);
            const closedRotation = this.getDoorClosedRotation(cell.gridX, cell.gridY);

            this.doorStatesByCell.set(cellKey, {
                gridX: cell.gridX,
                gridY: cell.gridY,
                isOpen: false,
                openness: 0,
                targetOpenness: 0,
                swingDirection,
                closedRotation,
                anchorDirectionX: anchorDirection.dx,
                anchorDirectionY: anchorDirection.dy
            });
        });

        if (this.doorStatesByCell.size > 0) {
            return;
        }

        for (let gridY = 0; gridY < this.gridHeight; gridY++) {
            for (let gridX = 0; gridX < this.gridWidth; gridX++) {
                if (this.dungeonMap?.[gridY]?.[gridX] !== this.TILE_DOOR) {
                    continue;
                }

                const cellKey = this.getCellKey(gridX, gridY);
                const anchorDirection = this.getDoorAnchorDirection(gridX, gridY);
                const swingDirection = this.getDoorSwingDirectionFromAnchor(anchorDirection);
                const closedRotation = this.getDoorClosedRotation(gridX, gridY);
                this.doorStatesByCell.set(cellKey, {
                    gridX,
                    gridY,
                    isOpen: false,
                    openness: 0,
                    targetOpenness: 0,
                    swingDirection,
                    closedRotation,
                    anchorDirectionX: anchorDirection.dx,
                    anchorDirectionY: anchorDirection.dy
                });
            }
        }
    }

    isStaticWallCell(gridX, gridY) {
        if (gridX < 0 || gridX >= this.gridWidth || gridY < 0 || gridY >= this.gridHeight) {
            return true;
        }

        const baseType = this.dungeonLayout?.baseCellTypes?.[this.getCellKey(gridX, gridY)] || null;
        if (baseType === 'wall') {
            return true;
        }

        return this.dungeonMap?.[gridY]?.[gridX] === this.TILE_WALL;
    }

    getDoorAnchorDirection(gridX, gridY) {
        const directions = [
            { dx: 1, dy: 0 },
            { dx: -1, dy: 0 },
            { dx: 0, dy: -1 },
            { dx: 0, dy: 1 }
        ];

        const maxDistance = 4;
        let bestDirection = null;
        let bestDistance = Number.POSITIVE_INFINITY;

        directions.forEach((direction) => {
            for (let distance = 1; distance <= maxDistance; distance++) {
                const sampleX = gridX + direction.dx * distance;
                const sampleY = gridY + direction.dy * distance;
                if (!this.isStaticWallCell(sampleX, sampleY)) {
                    continue;
                }

                if (distance < bestDistance) {
                    bestDistance = distance;
                    bestDirection = direction;
                }

                break;
            }
        });

        if (bestDirection) {
            return bestDirection;
        }

        return ((gridX + gridY) % 2 === 0)
            ? { dx: 1, dy: 0 }
            : { dx: -1, dy: 0 };
    }

    getDoorSwingDirectionFromAnchor(anchorDirection) {
        if (!anchorDirection) {
            return 1;
        }

        if (anchorDirection.dx > 0) {
            return 1;
        }

        if (anchorDirection.dx < 0) {
            return -1;
        }

        return anchorDirection.dy < 0 ? 1 : -1;
    }

    getDoorClosedRotation(gridX, gridY) {
        const hasWallLeft = this.isStaticWallCell(gridX - 1, gridY);
        const hasWallRight = this.isStaticWallCell(gridX + 1, gridY);
        const hasWallUp = this.isStaticWallCell(gridX, gridY - 1);
        const hasWallDown = this.isStaticWallCell(gridX, gridY + 1);

        const horizontalWallSegment = hasWallLeft || hasWallRight;
        const verticalWallSegment = hasWallUp || hasWallDown;

        if (horizontalWallSegment && !verticalWallSegment) {
            return 0;
        }

        if (verticalWallSegment && !horizontalWallSegment) {
            return Math.PI / 2;
        }

        return 0;
    }

    getDoorStateAtCell(gridX, gridY) {
        return this.doorStatesByCell.get(this.getCellKey(gridX, gridY)) || null;
    }

    initializeMapTransitions(dungeon) {
        this.mapTransitionsByCell.clear();

        const layout = Array.isArray(dungeon) ? null : dungeon?.layout || this.dungeonLayout;
        const transitionCells = layout?.transitionCells || {};
        Object.entries(transitionCells).forEach(([cellKey, transitionConfig]) => {
            const targetMapId = String(transitionConfig?.mapId || '').trim();
            if (!targetMapId) {
                return;
            }

            this.mapTransitionsByCell.set(cellKey, { mapId: targetMapId });
        });
    }

    resolveMapTransitionTargetMapId(targetMapId, sourceMapId = null) {
        const normalizedTarget = String(targetMapId || '').trim();
        const normalizedSource = String(sourceMapId || this.activeDungeonMapId || '').trim();

        const pairFallbackBySource = {
            'goblin-cave': 'forest-path',
            'forest-path': 'goblin-cave',
            'forest-town': 'forest-path',
            graveyard: 'forest-town'
        };

        if (normalizedTarget && normalizedTarget !== normalizedSource && this.getConfiguredDungeonLayoutById?.(normalizedTarget)) {
            return normalizedTarget;
        }

        const fallbackTarget = pairFallbackBySource[normalizedSource] || null;
        if (fallbackTarget && fallbackTarget !== normalizedSource && this.getConfiguredDungeonLayoutById?.(fallbackTarget)) {
            return fallbackTarget;
        }

        return null;
    }

    getMapTransitionAtCell(gridX, gridY) {
        return this.mapTransitionsByCell.get(this.getCellKey(gridX, gridY)) || null;
    }

    getDirectMapTransitionForCellKey(cellKey) {
        return this.dungeonLayout?.transitionCells?.[cellKey] || null;
    }

    isMapTransitionCell(gridX, gridY) {
        const cellKey = this.getCellKey(gridX, gridY);
        return this.dungeonLayout?.baseCellTypes?.[cellKey] === 'mapTransition';
    }

    cloneMapPersistenceValue(value) {
        if (typeof structuredClone === 'function') {
            try {
                return structuredClone(value);
            } catch (_error) {
                // Fall back to JSON cloning when structuredClone fails.
            }
        }

        return JSON.parse(JSON.stringify(value));
    }

    getCurrentMapPersistenceId() {
        return String(this.dungeonLayout?.id || this.activeDungeonMapId || '').trim();
    }

    saveCurrentMapPersistentState() {
        const mapId = this.getCurrentMapPersistenceId();
        if (!mapId) {
            return;
        }

        const enemyGroups = (this.enemyGroups || []).map((group) => ({
            id: group.id,
            isAggro: Boolean(group.isAggro),
            isCleared: Boolean(group.isCleared),
            members: (group.members || []).map((member) => ({
                id: member.id,
                mapPersistenceKey: member.mapPersistenceKey || null,
                gridX: member.gridX,
                gridY: member.gridY,
                hitPoints: member.hitPoints,
                magicPoints: member.magicPoints,
                isDead: Boolean(member.isDead),
                removedFromScene: Boolean(member.removedFromScene),
                fadeFrames: member.fadeFrames,
                activeEffects: this.cloneMapPersistenceValue(member.activeEffects || [])
            }))
        }));

        const propsByCell = {};
        this.dungeonPropsByCell.forEach((prop, cellKey) => {
            propsByCell[cellKey] = {
                mapPersistenceKey: prop.mapPersistenceKey || null,
                hasBeenSearched: Boolean(prop.hasBeenSearched),
                hasBeenTriggered: Boolean(prop.hasBeenTriggered),
                storeInventoryItemIds: this.cloneMapPersistenceValue(this.getVendorStockEntries(prop))
            };
        });

        const doorStatesByCell = {};
        this.doorStatesByCell.forEach((doorState, cellKey) => {
            const isOpen = Boolean(doorState?.isOpen);
            const openness = Number.isFinite(doorState?.openness)
                ? Math.max(0, Math.min(1, doorState.openness))
                : (isOpen ? 1 : 0);
            const targetOpenness = Number.isFinite(doorState?.targetOpenness)
                ? Math.max(0, Math.min(1, doorState.targetOpenness))
                : (isOpen ? 1 : 0);

            doorStatesByCell[cellKey] = {
                isOpen,
                openness,
                targetOpenness
            };
        });

        const lootDrops = [];
        this.lootDropsByCell.forEach((drop) => {
            lootDrops.push({
                gridX: drop.gridX,
                gridY: drop.gridY,
                gold: Math.max(0, Math.floor(drop.gold ?? 0)),
                equipmentDrops: this.cloneMapPersistenceValue(drop.equipmentDrops || []),
                sources: this.cloneMapPersistenceValue(drop.sources || []),
                sourceType: drop.sourceType || 'enemy',
                containerName: drop.containerName || 'Loot Bag'
            });
        });

        this.persistedMapStateById.set(mapId, {
            enemyGroups,
            propsByCell,
            doorStatesByCell,
            discoveredCellKeys: [...(this.discoveredCells || [])],
            lootDrops
        });
    }

    restorePersistentStateForCurrentMap() {
        const mapId = this.getCurrentMapPersistenceId();
        if (!mapId) {
            return;
        }

        // Discovery must be map-scoped: always reset before applying a snapshot.
        this.discoveredCells = new Set();
        this.visibleCells = new Set();
        this.visionNeedsRedraw = true;

        const snapshot = this.persistedMapStateById.get(mapId);
        if (!snapshot) {
            return;
        }

        if (Array.isArray(snapshot.discoveredCellKeys)) {
            snapshot.discoveredCellKeys.forEach((cellKey) => {
                if (typeof cellKey === 'string' && cellKey.includes(',')) {
                    this.discoveredCells.add(cellKey);
                }
            });
        }

        const enemyById = new Map((this.aiParty || []).map((enemy) => [enemy.id, enemy]));
        const enemyByPersistenceKey = new Map(
            (this.aiParty || [])
                .filter((enemy) => enemy.mapPersistenceKey)
                .map((enemy) => [enemy.mapPersistenceKey, enemy])
        );
        const groupById = new Map((this.enemyGroups || []).map((group) => [group.id, group]));

        (snapshot.enemyGroups || []).forEach((savedGroup) => {
            const group = groupById.get(savedGroup.id);
            if (!group) {
                return;
            }

            group.isAggro = Boolean(savedGroup.isAggro);
            group.isCleared = Boolean(savedGroup.isCleared);

            (savedGroup.members || []).forEach((savedMember) => {
                const enemy = enemyById.get(savedMember.id)
                    || (savedMember.mapPersistenceKey ? enemyByPersistenceKey.get(savedMember.mapPersistenceKey) : null);
                if (!enemy) {
                    return;
                }

                enemy.gridX = savedMember.gridX;
                enemy.gridY = savedMember.gridY;
                enemy.hitPoints = Math.max(0, Math.floor(savedMember.hitPoints ?? enemy.hitPoints ?? 0));
                enemy.magicPoints = Math.max(0, Math.floor(savedMember.magicPoints ?? enemy.magicPoints ?? 0));
                enemy.isDead = Boolean(savedMember.isDead);
                enemy.removedFromScene = Boolean(savedMember.removedFromScene);
                enemy.fadeFrames = Math.max(0, Math.floor(savedMember.fadeFrames ?? enemy.fadeFrames ?? 0));
                enemy.activeEffects = this.cloneMapPersistenceValue(savedMember.activeEffects || []);
                if (savedMember.mapPersistenceKey && !enemy.mapPersistenceKey) {
                    enemy.mapPersistenceKey = savedMember.mapPersistenceKey;
                }

                if (enemy.isDead) {
                    enemy.hitPoints = 0;
                    enemy.removedFromScene = true;
                    enemy.fadeFrames = 60;
                }
            });
        });

        const propSnapshot = snapshot.propsByCell || {};
        const propSnapshotByPersistenceKey = new Map(
            Object.values(propSnapshot)
                .filter((entry) => entry?.mapPersistenceKey)
                .map((entry) => [entry.mapPersistenceKey, entry])
        );
        this.dungeonPropsByCell.forEach((prop, cellKey) => {
            const directSavedProp = propSnapshot[cellKey];
            const keyedSavedProp = prop.mapPersistenceKey
                ? propSnapshotByPersistenceKey.get(prop.mapPersistenceKey)
                : null;
            const savedProp = directSavedProp || keyedSavedProp;
            if (!savedProp) {
                return;
            }

            prop.hasBeenSearched = Boolean(savedProp.hasBeenSearched);
            prop.hasBeenTriggered = Boolean(savedProp.hasBeenTriggered);
            prop.storeInventoryItemIds = this.normalizeVendorStockEntries(savedProp.storeInventoryItemIds);
        });

        const doorSnapshot = snapshot.doorStatesByCell || {};
        this.doorStatesByCell.forEach((doorState, cellKey) => {
            const savedDoorState = doorSnapshot[cellKey];
            if (!savedDoorState) {
                return;
            }

            const isOpen = Boolean(savedDoorState.isOpen);
            const openness = Number.isFinite(savedDoorState.openness)
                ? Math.max(0, Math.min(1, savedDoorState.openness))
                : (isOpen ? 1 : 0);
            const targetOpenness = Number.isFinite(savedDoorState.targetOpenness)
                ? Math.max(0, Math.min(1, savedDoorState.targetOpenness))
                : (isOpen ? 1 : 0);

            doorState.isOpen = isOpen;
            doorState.openness = openness;
            doorState.targetOpenness = targetOpenness;
        });

        this.lootDropsByCell.clear();
        (snapshot.lootDrops || []).forEach((savedDrop) => {
            const cellKey = this.getCellKey(savedDrop.gridX, savedDrop.gridY);
            this.lootDropsByCell.set(cellKey, {
                gridX: savedDrop.gridX,
                gridY: savedDrop.gridY,
                gold: Math.max(0, Math.floor(savedDrop.gold ?? 0)),
                equipmentDrops: (savedDrop.equipmentDrops || [])
                    .map((item) => this.ensureEquipmentItemInstance(item))
                    .filter(Boolean),
                sources: this.cloneMapPersistenceValue(savedDrop.sources || []),
                sourceType: savedDrop.sourceType || 'enemy',
                containerName: savedDrop.containerName || 'Loot Bag'
            });
        });
    }

    removeCharacterMeshFromScene(character) {
        if (!character?.mesh) {
            return;
        }

        if (character.mesh.parent) {
            character.mesh.parent.remove(character.mesh);
        }

        character.mesh.geometry?.dispose?.();

        const materials = Array.isArray(character.mesh.material)
            ? character.mesh.material
            : [character.mesh.material];
        materials.forEach((material) => material?.dispose?.());

        character.mesh = null;
        character.directionPointer = null;
    }

    syncCharacterSceneAfterMapTransition(previousCharacters = []) {
        const activeCharacters = new Set(this.characters || []);

        previousCharacters.forEach((character) => {
            if (activeCharacters.has(character)) {
                return;
            }

            this.removeCharacterMeshFromScene(character);
            if (this.characterHud?.has(character.id)) {
                const staleHud = this.characterHud.get(character.id);
                staleHud?.remove?.();
                this.characterHud.delete(character.id);
            }
        });

        this.characters.forEach((character) => {
            if (character.removedFromScene) {
                this.removeCharacterMeshFromScene(character);
                return;
            }

            if (!character.mesh) {
                this.setupCharacterSprite(
                    character,
                    this.createSpriteTexture(character.spriteFrame),
                    character.pointerColor
                );
            }

            const worldPos = this.getWorldPositionForCell(character.gridX, character.gridY);
            character.mesh.position.set(worldPos.x, worldPos.y, 0);
            character.mesh.userData.movementState = {
                initialized: true,
                active: false,
                startX: worldPos.x,
                startY: worldPos.y,
                targetX: worldPos.x,
                targetY: worldPos.y,
                startTime: performance.now(),
                durationMs: 220,
                mode: 'move',
                onComplete: null
            };
        });
    }

    transitionToConfiguredMap(mapId, interactor = null, transitionContext = null) {
        const normalizedMapId = String(mapId || '').trim();
        const layout = this.getConfiguredDungeonLayoutById?.(normalizedMapId) || null;
        if (!layout) {
            this.showToast('That destination is unavailable.', '#b8ad96', 2200);
            this.appendCombatLogEntry(`Transition failed: unknown destination '${normalizedMapId}'.`, '#d97d7d');
            return false;
        }

        this.saveCurrentMapPersistentState();

        try {
            const previousCharacters = Array.isArray(this.characters) ? [...this.characters] : [];
            this.closeLootMenu?.();
            this.activeLootCellKey = null;
            this.explorationClickMoveQueue = [];
            this.explorationClickMoveTimer = 0;
            this.pendingCombatAction = null;
            this.activeEnemyGroupId = null;
            this.clearSummonedAllies();
            this.clearSummonedEnemies();
            this.closeVendorStoreMenu?.();

            // Build the requested configured map directly to avoid accidental fallback to a different map id.
            const dungeon = this.buildConfiguredDungeonMap(layout);
            this.dungeonMap = dungeon.map;
            this.dungeonRooms = dungeon.rooms || [];
            this.dungeonLayout = dungeon.layout || null;
            this.activeDungeonMapId = this.dungeonLayout?.id || layout.id || normalizedMapId;

            const loadedMapId = String(this.dungeonLayout?.id || '').trim();
            if (!loadedMapId || loadedMapId !== normalizedMapId) {
                this.showToast('Map transition failed: destination mismatch.', '#d97d7d', 2600);
                this.appendCombatLogEntry(
                    `Transition mismatch: requested ${normalizedMapId}, loaded ${loadedMapId || 'unknown'}.`,
                    '#d97d7d'
                );
                return false;
            }

            this.lootDropsByCell.clear();
            this.lootBagState = '';
            this.initializeDoorStates(dungeon);
            this.initializeMapTransitions(dungeon);
            this.placeCharacters(dungeon, {
                forceTransitionSpawn: true,
                transitionContext: transitionContext || null
            });
            this.populateDungeonProps(dungeon);
            this.restorePersistentStateForCurrentMap();
            this.setupGrid();
            this.syncCharacterSceneAfterMapTransition(previousCharacters);
            this.enterExplorationMode();
            this.updatePartyVisionState();
            this.updateCharacterVisibilityByVision();
            this.updateTurnOrderQueue(this.getActiveTurnCharacter());
            this.updateCamera();

            const actorName = interactor?.name || 'Party';
            const destinationName = this.dungeonLayout?.name || layout.name || normalizedMapId;
            this.appendCombatLogEntry(`${actorName} travels to ${destinationName}.`, '#7fc9ff');
            this.showToast(`Travel: ${destinationName}`, '#7fc9ff', 2200);
            return true;
        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : String(error);
            this.showToast('Map transition failed unexpectedly.', '#d97d7d', 2600);
            this.appendCombatLogEntry(
                `Transition exception (${normalizedMapId}): ${errorMessage}`,
                '#d97d7d'
            );
            return false;
        }
    }

    tryUseMapTransitionAtCell(cellKey, interactor = null, screenX = null, screenY = null) {
        if (this.gameMode !== 'exploration') {
            return false;
        }

        const cell = this.parseCellKey(cellKey);
        if (!cell) {
            return false;
        }

        const transition = this.getDirectMapTransitionForCellKey(cellKey)
            || this.mapTransitionsByCell.get(cellKey);
        if (!transition?.mapId) {
            if (this.isMapTransitionCell(cell.gridX, cell.gridY)) {
                this.showToast('Transition is missing destination data.', '#d97d7d', 2400, screenX, screenY);
                this.appendCombatLogEntry(
                    `Transition data missing at (${cell.gridX}, ${cell.gridY}) on ${this.dungeonLayout?.id || 'unknown-map'}.`,
                    '#d97d7d'
                );
                return true;
            }
            return false;
        }

        const sourceMapId = String(this.dungeonLayout?.id || this.activeDungeonMapId || '').trim();
        const targetMapId = String(transition.mapId || '').trim();
        if (!targetMapId || !this.getConfiguredDungeonLayoutById?.(targetMapId)) {
            this.showToast('No linked destination.', '#b8ad96', 1800, screenX, screenY);
            return true;
        }

        if (targetMapId === sourceMapId) {
            this.showToast('Already in this area.', '#8f856f', 1800, screenX, screenY);
            return true;
        }

        const targetLayout = this.getConfiguredDungeonLayoutById?.(targetMapId) || null;
        const destinationName = targetLayout?.name || targetMapId;

        this.appendCombatLogEntry(
            `Transition click: ${sourceMapId} -> ${targetMapId} at (${cell.gridX}, ${cell.gridY}).`,
            '#7fc9ff'
        );

        const preferredCharacter = interactor || this.getLootInteractionCharacter();
        const actingCharacter = this.getNearbyPartyInteractionCharacter(cell.gridX, cell.gridY, 1, preferredCharacter);
        if (!actingCharacter) {
            const anchor = screenX !== null && screenY !== null
                ? { screenX, screenY }
                : this.getScreenPositionForCell(cell.gridX, cell.gridY);
            this.showToast('Need to be adjacent to travel.', '#d6cbb8', 2200, anchor?.screenX ?? null, anchor?.screenY ?? null);
            this.appendCombatLogEntry(
                `Transition blocked: no party member adjacent to (${cell.gridX}, ${cell.gridY}).`,
                '#d6cbb8'
            );
            return true;
        }

        const anchor = screenX !== null && screenY !== null
            ? { screenX, screenY }
            : this.getScreenPositionForCell(cell.gridX, cell.gridY);

        this.showConfirmationToast(
            `Travel to ${destinationName}?`,
            {
                confirmLabel: 'Travel',
                cancelLabel: 'Stay',
                color: '#7fc9ff',
                screenX: anchor?.screenX ?? null,
                screenY: anchor?.screenY ?? null,
                onConfirm: () => {
                    const didTransition = this.transitionToConfiguredMap(targetMapId, actingCharacter, {
                        sourceMapId,
                        sourceCellKey: cellKey,
                        targetMapId
                    });
                    if (!didTransition) {
                        this.appendCombatLogEntry(
                            `Transition aborted: ${sourceMapId} -> ${targetMapId}.`,
                            '#d97d7d'
                        );
                    }
                },
                onCancel: () => {
                    this.appendCombatLogEntry(
                        `Transition canceled: ${sourceMapId} -> ${targetMapId}.`,
                        '#b8ad96'
                    );
                }
            }
        );

        return true;
    }

    isDoorCell(gridX, gridY) {
        return this.getDoorStateAtCell(gridX, gridY) !== null;
    }

    isDoorOpenAtCell(gridX, gridY) {
        const doorState = this.getDoorStateAtCell(gridX, gridY);
        return Boolean(doorState?.isOpen);
    }

    isLineOfSightBlockingCell(gridX, gridY) {
        if (gridX < 0 || gridX >= this.gridWidth || gridY < 0 || gridY >= this.gridHeight) {
            return true;
        }

        const baseType = this.dungeonLayout?.baseCellTypes?.[this.getCellKey(gridX, gridY)] || null;
        if (baseType === 'wall') {
            return true;
        }

        if (baseType === 'door') {
            return !this.isDoorOpenAtCell(gridX, gridY);
        }

        if (this.isDoorCell(gridX, gridY)) {
            return !this.isDoorOpenAtCell(gridX, gridY);
        }

        return this.dungeonMap[gridY][gridX] !== this.TILE_FLOOR;
    }

    toggleDoorAtCell(gridX, gridY, interactor = null) {
        const doorState = this.getDoorStateAtCell(gridX, gridY);
        if (!doorState) {
            return false;
        }

        const nextIsOpen = !doorState.isOpen;
        doorState.isOpen = nextIsOpen;
        doorState.targetOpenness = nextIsOpen ? 1 : 0;

        const actorName = interactor?.name || 'Party';
        const actionLabel = nextIsOpen ? 'opens' : 'closes';
        this.appendCombatLogEntry(
            `${actorName} ${actionLabel} a door.`,
            nextIsOpen ? '#8ed1a3' : '#c9b28f'
        );

        this.updatePartyVisionState();
        return true;
    }

    tryToggleDoorAtCell(gridX, gridY, interactor = null) {
        if (!this.isDoorCell(gridX, gridY)) {
            return false;
        }

        const actingCharacter = interactor || this.getLootInteractionCharacter();
        if (!this.canCharacterInteractWithCell(actingCharacter, gridX, gridY, 1)) {
            return false;
        }

        return this.toggleDoorAtCell(gridX, gridY, actingCharacter);
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

    getVisionSourceCharacters() {
        if (this.gameMode === 'combat') {
            return this.getLivingCharacters(this.playerParty);
        }

        const source = this.getVisionSourceCharacter();
        return source ? [source] : [];
    }

    updatePartyVisionState() {
        const sources = this.getVisionSourceCharacters();
        if (sources.length === 0) {
            if (this.visibleCells.size > 0) {
                this.visibleCells = new Set();
                this.visionNeedsRedraw = true;
            }
            return;
        }

        const radius = this.partyVisionRangeCells ?? 6;
        const nextVisibleCells = new Set();
        let discoveredChanged = false;

        sources.forEach((source) => {
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
        });

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

    isCellVisibleToParty(gridX, gridY) {
        if (!Number.isFinite(gridX) || !Number.isFinite(gridY)) {
            return false;
        }

        return this.visibleCells.has(this.getCellKey(gridX, gridY));
    }

    isCharacterVisibleToParty(character) {
        if (!character || character.removedFromScene) {
            return false;
        }

        if (this.isPartyAlignedCharacter(character)) {
            return true;
        }

        return this.isCellVisibleToParty(character.gridX, character.gridY);
    }

    // --- Character Placement ---

    placeCharacters(dungeon, options = {}) {
        const forceTransitionSpawn = Boolean(options?.forceTransitionSpawn);
        const transitionContext = options?.transitionContext || null;
        const rooms = Array.isArray(dungeon) ? dungeon : dungeon?.rooms || [];
        const layout = Array.isArray(dungeon) ? null : dungeon?.layout || this.dungeonLayout;

        if (layout && forceTransitionSpawn) {
            this.placeConfiguredPlayerCharactersFromTransitions(layout, transitionContext);
        } else if (layout?.playerStarts && Object.keys(layout.playerStarts).length > 0) {
            this.placeConfiguredPlayerCharacters(layout.playerStarts);
        } else if (layout) {
            this.placeConfiguredPlayerCharactersFromTransitions(layout);
        } else {
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
        }

        const occupiedCells = new Set([
            this.getCellKey(this.wizard.gridX, this.wizard.gridY),
            this.getCellKey(this.warrior.gridX, this.warrior.gridY),
            this.getCellKey(this.cleric.gridX, this.cleric.gridY),
            this.getCellKey(this.ranger.gridX, this.ranger.gridY)
        ]);

        if (layout) {
            if (layout.enemyPlacements?.length) {
                this.spawnConfiguredEnemyPlacements(layout.enemyPlacements, occupiedCells);
            } else {
                this.enemyGroups = [];
                this.aiParty = [];
            }
        } else {
            this.spawnEnemyGroupsAcrossDungeon(rooms, occupiedCells);
            this.spawnTemporaryTestSpidersNearParty(occupiedCells);
        }
        this.characters = [...this.playerParty, ...this.aiParty];

        // Keep legacy references pointing at any matching enemy so existing UI text/helpers still work.
        this.goblin = this.aiParty.find((enemy) => enemy.id.includes('goblin-warrior'))
            || this.goblin
            || this.enemyArchetypeTemplatesById?.['goblin-warrior']
            || null;
        this.goblinArcher = this.aiParty.find((enemy) => enemy.id.includes('goblin-archer'))
            || this.goblinArcher
            || this.enemyArchetypeTemplatesById?.['goblin-archer']
            || null;
        this.goblinShaman = this.aiParty.find((enemy) => enemy.id.includes('goblin-shaman'))
            || this.goblinShaman
            || this.enemyArchetypeTemplatesById?.['goblin-shaman']
            || null;
        this.goblinBrute = this.aiParty.find((enemy) => enemy.id.includes('goblin-brute'))
            || this.goblinBrute
            || this.enemyArchetypeTemplatesById?.['goblin-brute']
            || null;
        this.direwolf = this.aiParty.find((enemy) => enemy.id.includes('dire-wolf'))
            || this.direwolf
            || this.enemyArchetypeTemplatesById?.['dire-wolf']
            || null;
        this.zombieEnemy = this.aiParty.find((enemy) => enemy.id.includes('zombie'))
            || this.zombieEnemy
            || this.enemyArchetypeTemplatesById?.zombie
            || null;
    }

    buildEnemyArchetypeTemplateMap() {
        const archetypes = [
            this.goblin,
            this.goblinArcher,
            this.goblinShaman,
            this.goblinBrute,
            this.goblinChieftain,
            this.giantSpider,
            this.direwolf,
            this.skeletonWarriorEnemy,
            this.skeletonMageEnemy,
            this.ghoulEnemy,
            this.specterEnemy,
            this.zombieEnemy,
            this.necromancerEnemy,
            ...(Array.isArray(this.undeadTestArchetypes) ? this.undeadTestArchetypes : [])
        ];

        return archetypes.reduce((result, archetype) => {
            if (!archetype?.id) {
                return result;
            }

            result[archetype.id] = archetype;
            return result;
        }, {});
    }

    placeConfiguredPlayerCharactersFromTransitions(layout, transitionContext = null) {
        const sourceMapId = String(transitionContext?.sourceMapId || '').trim();
        const transitionEntries = Object.entries(layout?.transitionCells || {})
            .filter(([, config]) => Boolean(config?.mapId))
            .map(([cellKey, config]) => {
                const parsed = this.parseCellKey(cellKey);
                if (!parsed) {
                    return null;
                }

                return {
                    gridX: parsed.gridX,
                    gridY: parsed.gridY,
                    mapId: String(config?.mapId || '').trim()
                };
            })
            .filter(Boolean)
            .sort((left, right) => (left.gridY - right.gridY) || (left.gridX - right.gridX));

        const preferredTransitionEntries = sourceMapId
            ? transitionEntries.filter((entry) => entry.mapId === sourceMapId)
            : [];
        const spawnAnchors = preferredTransitionEntries.length > 0
            ? preferredTransitionEntries
            : transitionEntries;

        const centerFallback = {
            x: Math.floor(this.gridWidth / 2),
            y: Math.floor(this.gridHeight / 2)
        };

        const entryAnchor = spawnAnchors[0]
            ? { x: spawnAnchors[0].gridX, y: spawnAnchors[0].gridY }
            : centerFallback;

        const occupiedCells = new Set();
        const candidateKeys = new Set();
        const spawnCandidates = [];
        const addCandidate = (x, y) => {
            if (x < 0 || x >= this.gridWidth || y < 0 || y >= this.gridHeight) {
                return;
            }

            if (this.dungeonMap?.[y]?.[x] !== this.TILE_FLOOR) {
                return;
            }

            const cellKey = this.getCellKey(x, y);
            if (candidateKeys.has(cellKey)) {
                return;
            }

            candidateKeys.add(cellKey);
            spawnCandidates.push({ x, y, key: cellKey });
        };

        spawnAnchors.forEach((entry) => {
            addCandidate(entry.gridX, entry.gridY);
        });

        for (let radius = 1; radius <= 3; radius++) {
            for (let dy = -radius; dy <= radius; dy++) {
                for (let dx = -radius; dx <= radius; dx++) {
                    addCandidate(entryAnchor.x + dx, entryAnchor.y + dy);
                }
            }
        }

        for (let y = 0; y < this.gridHeight; y++) {
            for (let x = 0; x < this.gridWidth; x++) {
                addCandidate(x, y);
            }
        }

        this.playerParty.forEach((character) => {
            const candidate = spawnCandidates.find((entry) => !occupiedCells.has(entry.key));
            if (candidate) {
                character.gridX = candidate.x;
                character.gridY = candidate.y;
                occupiedCells.add(candidate.key);
                return;
            }

            const fallback = this.findNearbyFloorTile(entryAnchor.x, entryAnchor.y, 0, 6, occupiedCells) || entryAnchor;
            character.gridX = fallback.x;
            character.gridY = fallback.y;
            occupiedCells.add(this.getCellKey(fallback.x, fallback.y));
        });
    }

    populateDungeonProps(dungeon) {
        this.dungeonPropsByCell.clear();

        const rooms = Array.isArray(dungeon) ? dungeon : dungeon?.rooms || [];
        const layout = Array.isArray(dungeon) ? null : dungeon?.layout || this.dungeonLayout;

        if (layout?.propPlacements?.length) {
            this.populateConfiguredDungeonProps(layout.propPlacements);
            return;
        }

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
        this.placeTemporarySpiderTestCrate();
    }

    placeConfiguredPlayerCharacters(playerStarts) {
        const occupiedCells = new Set();
        const anchor = Object.values(playerStarts)[0] || {
            x: Math.floor(this.gridWidth / 2),
            y: Math.floor(this.gridHeight / 2)
        };

        this.playerParty.forEach((character) => {
            const desiredStart = playerStarts[character.id];
            const desiredKey = desiredStart ? this.getCellKey(desiredStart.x, desiredStart.y) : null;
            const canUseDesiredStart = Boolean(
                desiredStart &&
                this.dungeonMap[desiredStart.y]?.[desiredStart.x] === this.TILE_FLOOR &&
                !occupiedCells.has(desiredKey)
            );

            if (canUseDesiredStart) {
                character.gridX = desiredStart.x;
                character.gridY = desiredStart.y;
                occupiedCells.add(desiredKey);
                return;
            }

            const fallback = this.findNearbyFloorTile(anchor.x, anchor.y, 0, 5, occupiedCells) || anchor;
            character.gridX = fallback.x;
            character.gridY = fallback.y;
            occupiedCells.add(this.getCellKey(fallback.x, fallback.y));
        });
    }

    populateConfiguredDungeonProps(propPlacements) {
        propPlacements.forEach((propPlacement, propIndex) => {
            if (this.dungeonMap[propPlacement.gridY]?.[propPlacement.gridX] !== this.TILE_FLOOR) {
                return;
            }

            const cellKey = this.getCellKey(propPlacement.gridX, propPlacement.gridY);
            this.dungeonPropsByCell.set(cellKey, {
                gridX: propPlacement.gridX,
                gridY: propPlacement.gridY,
                mapPersistenceKey: propPlacement.mapPersistenceKey || `prop:${propPlacement.frameId || 'prop'}:${propPlacement.gridX},${propPlacement.gridY}:${propIndex}`,
                frameId: propPlacement.frameId,
                spriteFrame: propPlacement.spriteFrame ? { ...propPlacement.spriteFrame } : null,
                name: propPlacement.name || window.GameData?.getDungeonPropDisplayName?.(propPlacement.frameId) || propPlacement.frameId,
                roomTheme: propPlacement.roomTheme || 'custom',
                searchable: Boolean(propPlacement.searchable),
                isVendor: Boolean(propPlacement.isVendor),
                vendorName: propPlacement.vendorName,
                graveSpawnArchetypeId: propPlacement.graveSpawnArchetypeId || null,
                graveSpawnChance: Number.isFinite(propPlacement.graveSpawnChance) ? Number(propPlacement.graveSpawnChance) : null,
                graveSpawnRange: Number.isFinite(propPlacement.graveSpawnRange) ? Number(propPlacement.graveSpawnRange) : null,
                signPostMessage: propPlacement.signPostMessage || '',
                signPostMessageColor: propPlacement.signPostMessageColor || null,
                hasConfiguredStoreInventory: Boolean(propPlacement.hasConfiguredStoreInventory),
                storeInventoryItemIds: this.normalizeVendorStockEntries(propPlacement.storeInventoryItemIds),
                storeBuyMultiplier: propPlacement.storeBuyMultiplier,
                storeSellMultiplier: propPlacement.storeSellMultiplier,
                hasBeenSearched: Boolean(propPlacement.hasBeenSearched),
                lootMode: propPlacement.lootMode,
                goldAmount: propPlacement.goldAmount,
                lootItemIds: Array.isArray(propPlacement.lootItemIds)
                    ? [...propPlacement.lootItemIds]
                    : []
            });
        });
    }

    spawnConfiguredEnemyPlacements(enemyPlacements, occupiedCells) {
        const groupsById = new Map();
        const groupMemberCounts = new Map();
        const groupSeedById = new Map();

        this.enemyGroups = [];
        this.aiParty = [];

        enemyPlacements.forEach((placement, placementIndex) => {
            const archetype = this.getEnemyArchetypeById(placement.archetypeId);
            if (!archetype) {
                return;
            }

            const groupId = placement.groupId || `group-${placementIndex + 1}`;
            if (!groupsById.has(groupId)) {
                groupsById.set(groupId, {
                    id: groupId,
                    members: [],
                    isAggro: false,
                    isCleared: false
                });
                groupSeedById.set(groupId, groupSeedById.size + 1);
                groupMemberCounts.set(groupId, 0);
            }

            const memberIndex = groupMemberCounts.get(groupId) || 0;
            const enemy = this.createEnemyFromArchetype(archetype, groupSeedById.get(groupId), memberIndex);
            enemy.mapPersistenceKey = placement.mapPersistenceKey
                || `enemy:${placement.archetypeId || archetype.id}:${placement.gridX},${placement.gridY}:${placementIndex}`;
            if (Number.isFinite(placement.experiencePoints)) {
                enemy.experiencePoints = Math.max(0, Math.floor(placement.experiencePoints));
            }
            enemy.configuredLoot = {
                lootMode: placement.lootMode,
                goldAmount: placement.goldAmount,
                lootItemIds: Array.isArray(placement.lootItemIds)
                    ? [...placement.lootItemIds]
                    : []
            };
            const desiredCellKey = this.getCellKey(placement.gridX, placement.gridY);
            const canUseDesiredCell =
                this.dungeonMap[placement.gridY]?.[placement.gridX] === this.TILE_FLOOR &&
                !occupiedCells.has(desiredCellKey);

            if (canUseDesiredCell) {
                enemy.gridX = placement.gridX;
                enemy.gridY = placement.gridY;
            } else {
                this.placeCharacterNear(
                    enemy,
                    placement.gridX,
                    placement.gridY,
                    0,
                    4,
                    occupiedCells,
                    { x: placement.gridX, y: placement.gridY }
                );
            }

            occupiedCells.add(this.getCellKey(enemy.gridX, enemy.gridY));
            enemy.encounterGroupId = groupId;
            groupsById.get(groupId).members.push(enemy);
            this.aiParty.push(enemy);
            groupMemberCounts.set(groupId, memberIndex + 1);
        });

        this.enemyGroups = [...groupsById.values()];
    }

    getEnemyArchetypeById(archetypeId) {
        const normalizedArchetypeId = String(archetypeId || '').trim();
        if (!normalizedArchetypeId) {
            return null;
        }

        if (!this.enemyArchetypeTemplatesById || Object.keys(this.enemyArchetypeTemplatesById).length === 0) {
            this.enemyArchetypeTemplatesById = this.buildEnemyArchetypeTemplateMap();
        }

        const fallbackArchetypes = {
            'goblin-warrior': this.goblin,
            'goblin-archer': this.goblinArcher,
            'goblin-shaman': this.goblinShaman,
            'goblin-brute': this.goblinBrute,
            'goblin-chieftain': this.goblinChieftain,
            'giant-spider': this.giantSpider,
            'dire-wolf': this.direwolf,
            'skeleton-warrior': this.skeletonWarriorEnemy,
            'skeleton-mage': this.skeletonMageEnemy,
            ghoul: this.ghoulEnemy,
            specter: this.specterEnemy,
            zombie: this.zombieEnemy,
            necromancer: this.necromancerEnemy
        };

        return this.enemyArchetypeTemplatesById[normalizedArchetypeId]
            || fallbackArchetypes[normalizedArchetypeId]
            || null;
    }

    placeTemporarySpiderTestCrate() {
        const crateCell = this.temporarySpiderCrateCell;
        if (!crateCell) {
            return;
        }

        const cellKey = this.getCellKey(crateCell.gridX, crateCell.gridY);
        const propConfig = window.GameData?.createDungeonPropConfig?.('crate') || {
            frameId: 'crate',
            name: 'Crate',
            roomTheme: 'storage',
            searchable: true
        };
        this.dungeonPropsByCell.set(cellKey, {
            gridX: crateCell.gridX,
            gridY: crateCell.gridY,
            frameId: propConfig.frameId,
            name: propConfig.name,
            roomTheme: propConfig.roomTheme,
            searchable: Boolean(propConfig.searchable),
            hasBeenSearched: false
        });
    }

    populateRoomPropsByTheme(room, theme, occupiedCells) {
        const framePool = window.GameData?.getDungeonPropThemePool?.(theme) || [];
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
            const propConfig = window.GameData?.createDungeonPropConfig?.(frameId, { roomTheme: theme }) || {
                frameId,
                name: frameId,
                roomTheme: theme,
                searchable: !frameId.startsWith('spikeTrap')
            };

            this.dungeonPropsByCell.set(pickedCell.key, {
                gridX: pickedCell.x,
                gridY: pickedCell.y,
                frameId: propConfig.frameId,
                name: propConfig.name,
                roomTheme: propConfig.roomTheme,
                searchable: Boolean(propConfig.searchable),
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
        const trapFrames = window.GameData?.getDungeonTrapPropIds?.() || ['spikeTrap1', 'spikeTrap2'];

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
            const propConfig = window.GameData?.createDungeonPropConfig?.(frameId, { roomTheme: 'trap' }) || {
                frameId,
                name: frameId,
                roomTheme: 'trap',
                searchable: false
            };
            this.dungeonPropsByCell.set(cellKey, {
                gridX: x,
                gridY: y,
                frameId: propConfig.frameId,
                name: propConfig.name,
                roomTheme: propConfig.roomTheme,
                searchable: Boolean(propConfig.searchable),
                hasBeenSearched: false
            });

            occupiedCells.add(cellKey);
        }
    }

    spawnEnemyGroupsAcrossDungeon(rooms, occupiedCells) {
        const baseArchetypes = [this.goblin, this.goblinArcher, this.goblinShaman, this.goblinBrute];
        const spiderArchetype = this.giantSpider;
        const enemyRooms = rooms.slice(1);
        const shuffledRooms = [...enemyRooms].sort(() => Math.random() - 0.5);

        const maxGroupsByRooms = Math.max(1, Math.min(6, shuffledRooms.length || 1));
        const minGroups = Math.min(4, maxGroupsByRooms);
        const baseGroupCount = minGroups + Math.floor(Math.random() * (maxGroupsByRooms - minGroups + 1));
        const maxSafeGroups = Math.max(2, (shuffledRooms.length || 1) * 3);
        const groupCount = Math.min(baseGroupCount * 2, maxSafeGroups);
        const specialRoom = shuffledRooms[0] || rooms[rooms.length - 1] || rooms[0] || null;
        const regularRooms = shuffledRooms.length > 1 ? shuffledRooms.slice(1) : shuffledRooms;
        const regularGroupCount = specialRoom ? Math.max(0, groupCount - 1) : groupCount;

        this.enemyGroups = [];
        this.aiParty = [];

        if (specialRoom && this.goblinChieftain) {
            this.spawnFixedEnemyGroup(specialRoom, occupiedCells, 'group-goblin-chieftain', [
                this.goblinChieftain,
                this.goblinBrute,
                this.goblin,
                this.goblin,
                this.goblinShaman,
                this.goblinArcher,
                this.goblinArcher,
                spiderArchetype,
                spiderArchetype,
                spiderArchetype
            ], 900);
        }

        for (let groupIndex = 0; groupIndex < regularGroupCount; groupIndex++) {
            const roomPool = regularRooms.length > 0 ? regularRooms : shuffledRooms;
            const room = roomPool[groupIndex % Math.max(1, roomPool.length)] || rooms[rooms.length - 1];
            const anchor = {
                x: Math.floor(room.x + room.w / 2),
                y: Math.floor(room.y + room.h / 2)
            };
            const fallback = this.findNearbyFloorTile(anchor.x, anchor.y, 0, 4, occupiedCells) || anchor;
            const memberCount = 2 + Math.floor(Math.random() * 3);
            const spiderCount = 1 + Math.floor(Math.random() * 3);
            const members = [];
            let memberIndex = 0;

            for (let goblinIndex = 0; goblinIndex < memberCount; goblinIndex++) {
                const archetype = baseArchetypes[(groupIndex + goblinIndex) % baseArchetypes.length];
                const enemy = this.createEnemyFromArchetype(archetype, groupIndex, memberIndex);

                this.placeCharacterNear(enemy, fallback.x, fallback.y, 0, 3, occupiedCells, fallback);
                occupiedCells.add(this.getCellKey(enemy.gridX, enemy.gridY));

                enemy.encounterGroupId = `group-${groupIndex + 1}`;
                members.push(enemy);
                this.aiParty.push(enemy);
                memberIndex += 1;
            }

            for (let spiderIndex = 0; spiderIndex < spiderCount; spiderIndex++) {
                const enemy = this.createEnemyFromArchetype(spiderArchetype, groupIndex, memberIndex);

                this.placeCharacterNear(enemy, fallback.x, fallback.y, 0, 3, occupiedCells, fallback);
                occupiedCells.add(this.getCellKey(enemy.gridX, enemy.gridY));

                enemy.encounterGroupId = `group-${groupIndex + 1}`;
                members.push(enemy);
                this.aiParty.push(enemy);
                memberIndex += 1;
            }

            this.enemyGroups.push({
                id: `group-${groupIndex + 1}`,
                members,
                isAggro: false,
                isCleared: false
            });
        }
    }

    spawnFixedEnemyGroup(room, occupiedCells, groupId, archetypes, groupIndexSeed = 0) {
        if (!room || !Array.isArray(archetypes) || archetypes.length === 0) {
            return false;
        }

        const anchor = {
            x: Math.floor(room.x + room.w / 2),
            y: Math.floor(room.y + room.h / 2)
        };
        const fallback = this.findNearbyFloorTile(anchor.x, anchor.y, 0, 4, occupiedCells) || anchor;
        const members = [];

        archetypes.forEach((archetype, memberIndex) => {
            const enemy = this.createEnemyFromArchetype(archetype, groupIndexSeed, memberIndex);
            this.placeCharacterNear(enemy, fallback.x, fallback.y, 0, 4, occupiedCells, fallback);
            occupiedCells.add(this.getCellKey(enemy.gridX, enemy.gridY));
            enemy.encounterGroupId = groupId;
            members.push(enemy);
            this.aiParty.push(enemy);
        });

        this.enemyGroups.push({
            id: groupId,
            members,
            isAggro: false,
            isCleared: false
        });

        return true;
    }

    spawnTemporaryTestSpidersNearParty(occupiedCells) {
        if (!this.giantSpider) {
            return;
        }

        this.temporarySpiderCrateCell = null;

        const partyAnchor = this.explorationLeadCharacter
            || this.wizard
            || this.playerParty?.[0]
            || null;
        if (!partyAnchor) {
            return;
        }

        const groupId = 'group-test-spiders';
        const members = [];
        const spawnOrder = [
            { x: partyAnchor.gridX + 1, y: partyAnchor.gridY },
            { x: partyAnchor.gridX, y: partyAnchor.gridY + 1 },
            { x: partyAnchor.gridX - 1, y: partyAnchor.gridY },
            { x: partyAnchor.gridX, y: partyAnchor.gridY - 1 },
            { x: partyAnchor.gridX + 1, y: partyAnchor.gridY + 1 },
            { x: partyAnchor.gridX - 1, y: partyAnchor.gridY + 1 },
            { x: partyAnchor.gridX + 1, y: partyAnchor.gridY - 1 },
            { x: partyAnchor.gridX - 1, y: partyAnchor.gridY - 1 }
        ];

        for (let index = 0; index < 2; index++) {
            const spider = this.createEnemyFromArchetype(this.giantSpider, 998, index);
            spider.experiencePoints = 2000;
            const spawn = spawnOrder.find((candidate) => {
                if (candidate.x < 0 || candidate.x >= this.gridWidth || candidate.y < 0 || candidate.y >= this.gridHeight) {
                    return false;
                }

                if (this.dungeonMap[candidate.y][candidate.x] !== this.TILE_FLOOR) {
                    return false;
                }

                return !occupiedCells.has(this.getCellKey(candidate.x, candidate.y));
            }) || this.findNearbyFloorTile(partyAnchor.gridX, partyAnchor.gridY, 1, 2, occupiedCells);

            if (!spawn) {
                break;
            }

            spider.gridX = spawn.x;
            spider.gridY = spawn.y;
            spider.encounterGroupId = groupId;
            occupiedCells.add(this.getCellKey(spawn.x, spawn.y));
            if (!this.temporarySpiderCrateCell) {
                this.temporarySpiderCrateCell = { gridX: spawn.x, gridY: spawn.y };
            }
            members.push(spider);
            this.aiParty.push(spider);
        }

        if (members.length > 0) {
            this.enemyGroups.push({
                id: groupId,
                members,
                isAggro: false,
                isCleared: false
            });
        }
    }

    createEnemyFromArchetype(archetype, groupIndex, memberIndex) {
        const idSuffix = `g${groupIndex + 1}m${memberIndex + 1}`;
        const clonedAbilities = (archetype.abilities || []).map((ability) => ({ ...ability }));
        const clonedSpells = (archetype.spells || []).map((spell) => ({ ...spell }));
        const clonedEquipment = Object.entries(archetype.equipment || {}).reduce((equipment, [slotKey, item]) => {
            const clonedItem = this.ensureEquipmentItemInstance(item, slotKey);
            if (clonedItem) {
                equipment[slotKey] = clonedItem;
            }
            return equipment;
        }, {});

        const archetypeMaxHp = Math.max(1, Math.floor(archetype.maxHitPoints ?? archetype.hitPoints ?? 1));
        const archetypeCurrentHp = Math.max(0, Math.min(archetypeMaxHp, Math.floor(archetype.hitPoints ?? archetypeMaxHp)));

        const enemy = this.createCharacter({
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
            armorClass: archetype.armorClass,
            attackCost: archetype.attackCost,
            maxActionsPerTurn: archetype.maxActionsPerTurn,
            bonusMovement: archetype.bonusMovement,
            experiencePoints: archetype.experiencePoints,
            abilities: clonedAbilities,
            spells: clonedSpells,
            equipment: clonedEquipment
        });

        // Archetype stats are already fully resolved; preserve their HP values as-is.
        enemy.maxHitPoints = archetypeMaxHp;
        enemy.hitPoints = archetypeCurrentHp;
        return enemy;
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

    isPartyAlignedCharacter(character) {
        return Boolean(character && (character.team === 'player' || character.team === 'ally'));
    }

    getCombatPartyMembers() {
        if (this.gameMode !== 'combat') {
            return [];
        }

        return [
            ...this.getLivingCharacters(this.playerParty),
            ...this.getLivingCharacters(this.summonedAllies ?? [])
        ];
    }

    getCombatEnemiesForPlayers() {
        return this.getCombatEnemyMembers({ visibleToPartyOnly: true });
    }

    getCombatAlliedEnemies() {
        return this.getCombatEnemyMembers();
    }

    getCombatEnemyMembers(options = {}) {
        const { visibleToPartyOnly = false } = options;

        if (this.gameMode !== 'combat') {
            return [];
        }

        const enemies = this.getAggroedEnemyMembers();
        if (!visibleToPartyOnly) {
            return enemies;
        }

        return enemies.filter((enemy) => this.isCharacterVisibleToParty(enemy));
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
            ...this.getCombatPartyMembers(),
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
        this.clearSummonedAllies();
        this.clearSummonedEnemies();
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
            this.resetCharacterTurnResources(character);
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
            alivePlayers.forEach((character) => this.advanceRoundBasedEffects(character));

            const survivingPlayers = this.getLivingCharacters(this.playerParty);
            let hadRecovery = false;

            survivingPlayers.forEach((character) => {
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

    getNearestEnemyToParty() {
        const alivePlayers = this.getLivingCharacters(this.playerParty);
        if (alivePlayers.length === 0) {
            return null;
        }

        let nearestEnemy = null;
        let nearestDistance = Infinity;

        for (const player of alivePlayers) {
            for (const enemy of this.characters) {
                if (this.isPartyAlignedCharacter(enemy) || enemy.isDead || enemy.removedFromScene) {
                    continue;
                }

                const distance = Math.hypot(enemy.gridX - player.gridX, enemy.gridY - player.gridY);
                if (distance < nearestDistance) {
                    nearestDistance = distance;
                    nearestEnemy = enemy;
                }
            }
        }

        return nearestEnemy;
    }

    getEnemyGroupDisplayName(group) {
        if (!group) {
            return 'Enemy';
        }

        const livingMembers = this.getLivingCharacters(group.members || []);
        const fallbackMembers = Array.isArray(group.members) ? group.members : [];
        const referenceEnemy = livingMembers[0] || fallbackMembers[0] || null;

        return referenceEnemy?.name || 'Enemy';
    }

    beginCombatWithGroup(group) {
        if (!group || group.isCleared || group.isAggro) {
            return;
        }

        group.isAggro = true;
        const enemyName = this.getEnemyGroupDisplayName(group);

        if (this.gameMode === 'combat') {
            this.refreshCombatTurnOrder(true);
            this.appendCombatLogEntry(
                `${enemyName} joins the battle.`,
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
            `${enemyName} spots the party. Combat begins.`,
            '#d34c4c'
        );

        // Start combat transition with camera pan to nearest enemy
        this.startCombatTransition();
        const nearestEnemy = this.getNearestEnemyToParty();
        if (nearestEnemy) {
            this.focusCameraOnCharacter(nearestEnemy, 1500);
        }

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
                const enemyName = this.getEnemyGroupDisplayName(group);
                this.appendCombatLogEntry(`${enemyName} has been defeated.`, '#d9c47d');
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
        this.triggerDungeonTrapAtCell(lead.gridX, lead.gridY);
        const leadCharacter = partyOrder[0];
        if (leadCharacter) {
            this.tryAutoOpenLootFromCharacterCell(leadCharacter);
        }
        this.triggerHauntedGravesNearParty();
        this.applyExplorationMovementRegen(1);
        this.tryTriggerEnemyAggro();
    }

    isExplorationMovementActive() {
        const partyOrder = this.getExplorationPartyOrder();
        return partyOrder.some((character) => character?.mesh?.userData?.movementState?.active);
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
        if (!Array.isArray(group) || group.length === 0) {
            return [];
        }

        return group.filter((character) => character && !character.isDead);
    }

    getLivingGoblinAllies() {
        const source = this.gameMode === 'combat'
            ? this.getCombatAlliedEnemies()
            : this.aiParty;
        return source.filter((character) => !character.isDead && character.race === 'goblin');
    }

    getCharacterBonusMovement(character) {
        return Math.max(0, Math.floor(character?.bonusMovement ?? 0));
    }

    getCharacterBonusMovementRemaining(character) {
        return Math.max(
            0,
            Math.floor(character?.bonusMovementRemaining ?? this.getCharacterBonusMovement(character))
        );
    }

    resetCharacterTurnResources(character) {
        if (!character) {
            return;
        }

        character.actionsRemaining = Math.max(0, Math.floor(character.maxActionsPerTurn ?? 0));
        character.bonusMovementRemaining = this.getCharacterBonusMovement(character);
    }

    getCharacterMovementBudget(character, reservedActionCost = 0) {
        if (!character || character.isDead) {
            return 0;
        }

        const actionsRemaining = Math.max(0, Math.floor(character.actionsRemaining ?? 0));
        const normalizedReserve = Math.max(0, Math.floor(reservedActionCost ?? 0));
        const reservedActions = actionsRemaining >= normalizedReserve ? normalizedReserve : 0;

        return this.getCharacterBonusMovementRemaining(character) + Math.max(0, actionsRemaining - reservedActions);
    }

    consumeCharacterMovement(character, steps = 1) {
        if (!character) {
            return false;
        }

        const normalizedSteps = Math.max(0, Math.floor(steps ?? 0));
        if (normalizedSteps <= 0 || this.getCharacterMovementBudget(character) < normalizedSteps) {
            return false;
        }

        for (let step = 0; step < normalizedSteps; step += 1) {
            const bonusRemaining = this.getCharacterBonusMovementRemaining(character);
            if (bonusRemaining > 0) {
                character.bonusMovementRemaining = bonusRemaining - 1;
            } else {
                character.actionsRemaining = Math.max(0, (character.actionsRemaining ?? 0) - 1);
            }
        }

        return true;
    }

    shouldEndCurrentTurn(character) {
        return this.getCharacterMovementBudget(character) <= 0;
    }

    getAbilityActionCost(character, ability = null) {
        return Math.max(0, Math.floor(ability?.actionCost ?? character?.attackCost ?? 0));
    }

    isCellTargetedAbility(ability) {
        return ability?.targetMode === 'cell';
    }

    isCombatActionPending(character = null) {
        if (this.gameMode !== 'combat' || !this.pendingCombatAction) {
            return false;
        }

        if (!character) {
            return true;
        }

        return this.pendingCombatAction.characterId === character.id;
    }

    beginPendingCombatAction(character) {
        if (this.gameMode !== 'combat' || !character) {
            return;
        }

        this.pendingCombatAction = { characterId: character.id };
        this.enemyMoveTimer = 0;
    }

    finishPendingCombatAction(character) {
        if (!this.pendingCombatAction || !character) {
            return;
        }

        const characterId = this.getCharacterTurnIdentity(character);
        if (!characterId || this.pendingCombatAction.characterId !== characterId) {
            return;
        }

        this.pendingCombatAction = null;
        if (this.gameMode !== 'combat') {
            return;
        }

        const activeCharacterId = this.getCharacterTurnIdentity(this.getActiveTurnCharacter());
        if (activeCharacterId !== characterId) {
            return;
        }

        if (this.shouldEndCurrentTurn(character)) {
            this.endCurrentTurn();
        }
    }

    getAbilityProjectileAnimation(ability) {
        return ability?.projectileAnimation || null;
    }

    doesAbilityResolveOnImpact(ability) {
        return Boolean(ability?.resolveOnImpact && this.getAbilityProjectileAnimation(ability));
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
            const leftIsPartyAligned = this.isPartyAlignedCharacter(left.character);
            const rightIsPartyAligned = this.isPartyAlignedCharacter(right.character);
            if (leftIsPartyAligned !== rightIsPartyAligned) {
                return leftIsPartyAligned ? -1 : 1;
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

    getCharacterTurnIdentity(character) {
        if (!character) {
            return null;
        }

        return String(character.id || '').trim() || null;
    }

    stabilizeCombatTurnState() {
        if (this.gameMode !== 'combat') {
            return;
        }

        const combatants = [
            ...this.getCombatPartyMembers(),
            ...this.getAggroedEnemyMembers()
        ].filter((character) => Boolean(character) && !character.isDead);

        if (combatants.length === 0) {
            this.pendingCombatAction = null;
            return;
        }

        const combatantIdSet = new Set(
            combatants
                .map((character) => this.getCharacterTurnIdentity(character))
                .filter(Boolean)
        );

        if (this.pendingCombatAction) {
            const pendingCharacterId = String(this.pendingCombatAction.characterId || '').trim() || null;
            const activeCharacter = this.getActiveTurnCharacter();
            const activeCharacterId = this.getCharacterTurnIdentity(activeCharacter);
            if (!pendingCharacterId || !combatantIdSet.has(pendingCharacterId) || activeCharacterId !== pendingCharacterId) {
                this.pendingCombatAction = null;
                this.enemyMoveTimer = 0;
            }
        }

        const hasTurnOrder = Array.isArray(this.turnOrder) && this.turnOrder.length > 0;
        const hasValidActiveIndex = hasTurnOrder && this.activeTurnIndex >= 0 && this.activeTurnIndex < this.turnOrder.length;
        const aliveTurnOrder = hasTurnOrder
            ? this.turnOrder.filter((character) => {
                if (!character || character.isDead) {
                    return false;
                }
                const characterId = this.getCharacterTurnIdentity(character);
                return Boolean(characterId) && combatantIdSet.has(characterId);
            })
            : [];

        if (!hasValidActiveIndex || aliveTurnOrder.length === 0) {
            const beforeRepairActiveId = this.getCharacterTurnIdentity(this.getActiveTurnCharacter());
            this.refreshCombatTurnOrder(false);
            const afterRepairActiveId = this.getCharacterTurnIdentity(this.getActiveTurnCharacter());
            if (afterRepairActiveId && afterRepairActiveId !== beforeRepairActiveId) {
                this.beginCurrentTurn();
            }
            return;
        }

        const activeCharacter = this.getActiveTurnCharacter();
        const activeCharacterId = this.getCharacterTurnIdentity(activeCharacter);
        if (!activeCharacterId || !combatantIdSet.has(activeCharacterId)) {
            const beforeRepairActiveId = activeCharacterId;
            this.refreshCombatTurnOrder(false);
            const afterRepairActiveId = this.getCharacterTurnIdentity(this.getActiveTurnCharacter());
            if (afterRepairActiveId && afterRepairActiveId !== beforeRepairActiveId) {
                this.beginCurrentTurn();
            }
        }
    }

    recoverIfEnemyTurnStalled(activeCharacter) {
        if (this.gameMode !== 'combat' || !activeCharacter || activeCharacter.team === 'player' || activeCharacter.isDead) {
            this.enemyTurnWatchState = null;
            return;
        }

        const watchKey = `${activeCharacter.id}|${this.activeTurnIndex}`;
        if (!this.enemyTurnWatchState || this.enemyTurnWatchState.key !== watchKey) {
            this.enemyTurnWatchState = { key: watchKey, frames: 0 };
            return;
        }

        this.enemyTurnWatchState.frames += 1;
        if (this.enemyTurnWatchState.frames < 360) {
            return;
        }

        const hasFlyingProjectiles = Array.isArray(this.activeProjectiles) && this.activeProjectiles.length > 0;
        if (this.pendingCombatAction && !hasFlyingProjectiles) {
            this.pendingCombatAction = null;
        }

        this.appendCombatLogEntry(
            `${activeCharacter.name} hesitates; recovering turn flow.`,
            '#c6b28f'
        );

        if (typeof this.forceEndCurrentAITurn === 'function') {
            this.forceEndCurrentAITurn(activeCharacter);
        } else {
            this.endCurrentTurn();
        }

        this.enemyTurnWatchState = null;
        this.enemyMoveTimer = 0;
    }

    beginCurrentTurn() {
        if (this.gameMode !== 'combat') {
            return;
        }

        if (this.sleepSkipTurnTimeoutId !== null) {
            clearTimeout(this.sleepSkipTurnTimeoutId);
            this.sleepSkipTurnTimeoutId = null;
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

        const startOfTurnResult = this.applyStartOfTurnEffects(activeCharacter);
        if (activeCharacter.isDead) {
            if (this.turnOrder[this.activeTurnIndex] === activeCharacter) {
                this.endCurrentTurn();
            }
            return;
        }

        if (startOfTurnResult?.skipTurn) {
            this.turnTransitionFrames = this.turnTransitionDelay;
            this.enemyMoveTimer = 0;
            this.updateCamera();
            this.showCharacterToast(activeCharacter, 'Zzzz', '#a9c4de', 1000);
            this.sleepSkipTurnTimeoutId = setTimeout(() => {
                this.sleepSkipTurnTimeoutId = null;
                if (this.gameMode !== 'combat') {
                    return;
                }
                if (this.turnOrder[this.activeTurnIndex] !== activeCharacter) {
                    return;
                }
                this.endCurrentTurn();
            }, 1000);
            return;
        }

        this.resetCharacterTurnResources(activeCharacter);
        this.getCharacterActionList(activeCharacter).forEach((action) => {
            if ((action.cooldownRemaining ?? 0) > 0) {
                action.cooldownRemaining -= 1;
            }
        });
        this.turnTransitionFrames = this.turnTransitionDelay;
        this.enemyMoveTimer = 0;
        this.updateCamera();
    }

    applyStartOfTurnEffects(character) {
        if (!character || !Array.isArray(character.activeEffects) || character.activeEffects.length === 0) {
            return { skipTurn: false };
        }

        return this.advanceRoundBasedEffects(character);
    }

    advanceRoundBasedEffects(character) {
        if (!character || !Array.isArray(character.activeEffects) || character.activeEffects.length === 0) {
            return { skipTurn: false };
        }

        const remainingEffects = [];
        let skipTurn = false;
        for (const effect of character.activeEffects) {
            if (!effect) {
                continue;
            }

            if (effect.type === 'poison') {
                if (effect.poisonStartsNextTurn) {
                    effect.poisonStartsNextTurn = false;
                    remainingEffects.push(effect);
                    continue;
                }

                const poisonDamage = Math.max(0, Math.floor(Number(effect.damagePerRound ?? 3) || 0));
                if (poisonDamage > 0) {
                    this.removeSleepEffect(character);
                }
                character.hitPoints = Math.max(0, character.hitPoints - poisonDamage);
                this.playHitAnimation(character);
                this.appendCombatLogEntry(`${character.name} suffers ${poisonDamage} poison damage.`, '#7bcf84');

                if (character.hitPoints <= 0) {
                    character.hitPoints = 0;
                    this.markCharacterDead(character);
                    character.activeEffects = remainingEffects;
                    return;
                }

                effect.roundsRemaining = (effect.roundsRemaining ?? 0) - 1;
                if (effect.roundsRemaining > 0) {
                    remainingEffects.push(effect);
                }
                continue;
            }

            if (effect.type === 'sleep') {
                skipTurn = true;
                this.appendCombatLogEntry(`${character.name} is asleep and misses the turn.`, '#8a9bb0');
                effect.roundsRemaining = (effect.roundsRemaining ?? 0) - 1;
                if (effect.roundsRemaining > 0) {
                    remainingEffects.push(effect);
                }
                continue;
            }

            effect.roundsRemaining = (effect.roundsRemaining ?? 0) - 1;
            if (effect.roundsRemaining > 0) {
                remainingEffects.push(effect);
            }
        }

        character.activeEffects = remainingEffects;
        return { skipTurn };
    }

    endCurrentTurn() {
        if (this.gameMode !== 'combat') {
            return;
        }

        const currentTurnCharacter = this.turnOrder[this.activeTurnIndex] || null;
        if (currentTurnCharacter && !currentTurnCharacter.isDead) {
            currentTurnCharacter.actionsRemaining = 0;
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

            if (this.handleHelpMenuKey(e, key)) {
                return;
            }

            if (!this.isPlayerTurn()) {
                return;
            }

            if (key === ' ') {
                if (this.gameMode === 'combat') {
                    if (this.isCombatActionPending()) {
                        return;
                    }
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

    handleHelpMenuKey(event, normalizedKey) {
        if (normalizedKey !== 'H') {
            return false;
        }

        event.preventDefault();
        this.toggleHelpMenu();
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
                this.hoveredCell = null;
                return;
            }

            this.hoveredCell = this.getGridCellFromPointerEvent(event);

            const rect = this.renderer.domElement.getBoundingClientRect();
            this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
            this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
            this.raycaster.setFromCamera(this.mouse, this.camera);

            const livingCharacters = this.characters.filter((character) =>
                !character.isDead &&
                character.mesh &&
                character.mesh.visible !== false &&
                this.isCharacterVisibleToParty(character)
            );
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
            this.hoveredCell = null;

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
            this.hoveredCell = null;
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

            const actionCharacter = this.getActionBarCharacter();
            if (this.isGameOver || !actionCharacter || actionCharacter.team !== 'player' || actionCharacter.isDead) {
                return;
            }

            if (this.isCombatActionPending(actionCharacter)) {
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

            const selectedAbility = window.CharacterData?.getCharacterActionById(actionCharacter, actionCharacter.selectedAbilityId) || null;
            const clickedAbilityTarget = selectedAbility
                ? this.findClickedAbilityTarget(event, actionCharacter, selectedAbility)
                : null;
            if (clickedAbilityTarget) {
                const didUseAbility = this.useAbilityOnTarget(actionCharacter, selectedAbility, clickedAbilityTarget);
                if (didUseAbility) {
                    return;
                }

                if (this.isAbilityTargetOutOfRange(actionCharacter, selectedAbility, clickedAbilityTarget)) {
                    this.showOutOfRangeToastAtCell(clickedAbilityTarget.gridX, clickedAbilityTarget.gridY);
                    return;
                }
            }

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

            const clickedAbilityCell = selectedAbility && this.isCellTargetedAbility(selectedAbility)
                ? this.findClickedAbilityCell(event, actionCharacter, selectedAbility)
                : null;
            if (clickedAbilityCell) {
                const didUseAbility = this.useAbilityOnTarget(actionCharacter, selectedAbility, clickedAbilityCell);
                if (didUseAbility) {
                    return;
                }

                if (this.isAbilityTargetOutOfRange(actionCharacter, selectedAbility, clickedAbilityCell)) {
                    this.showOutOfRangeToastAtCell(clickedAbilityCell.gridX, clickedAbilityCell.gridY);
                    return;
                }

                const invalidCellMessage = selectedAbility.id === 'charge'
                    ? 'Cannot charge there'
                    : selectedAbility.id === 'sleep'
                        ? 'No enemies there to sleep'
                        : 'Cannot target that cell';
                this.showOutOfRangeToastAtCell(clickedAbilityCell.gridX, clickedAbilityCell.gridY, invalidCellMessage);
                return;
            }

            const clickedCell = this.getGridCellFromPointerEvent(event);
            if (clickedCell && this.tryToggleDoorAtCell(clickedCell.gridX, clickedCell.gridY, interactor)) {
                return;
            }

            if (this.gameMode === 'exploration') {
                if (clickedCell && this.isMapTransitionCell(clickedCell.gridX, clickedCell.gridY)) {
                    this.tryUseMapTransitionAtCell(this.getCellKey(clickedCell.gridX, clickedCell.gridY), interactor, event.clientX, event.clientY);
                    return;
                }

                if (!clickedCell) {
                    return;
                }

                this.navigatePartyToCell(clickedCell.gridX, clickedCell.gridY);
                return;
            }

            if (this.gameMode !== 'combat') {
                return;
            }

            const activeCharacter = this.getActiveTurnCharacter();
            if (!activeCharacter || activeCharacter.isDead) {
                return;
            }
            if (selectedAbility && selectedAbility.type === 'buff') {
                return;
            }

            const isHeal = selectedAbility && selectedAbility.type === 'heal';
            const isInflictPain = selectedAbility && selectedAbility.id === 'inflict-pain';

            const targetPool = isHeal || isInflictPain
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
                const didUseAbility = this.useAbilityOnTarget(activeCharacter, selectedAbility, targetCharacter);
                if (!didUseAbility && this.isAbilityTargetOutOfRange(activeCharacter, selectedAbility, targetCharacter)) {
                    this.showOutOfRangeToastAtCell(targetCharacter.gridX, targetCharacter.gridY);
                }
            }
        });
    }

    getActionBarCharacter() {
        const activeCharacter = this.getActiveTurnCharacter();
        return activeCharacter && !activeCharacter.isDead ? activeCharacter : null;
    }

    getAbilityTargetPool(character, ability) {
        if (!character || !ability) {
            return [];
        }

        if (this.isCellTargetedAbility(ability)) {
            return [];
        }

        if (ability.type === 'heal' || ability.type === 'buff' || ability.id === 'inflict-pain') {
            return this.characters.filter((candidate) =>
                candidate.team === character.team &&
                !candidate.isDead &&
                !candidate.removedFromScene &&
                candidate.mesh
            );
        }

        if (this.gameMode === 'combat' && character.team === 'player') {
            return this.getCombatEnemiesForPlayers().filter((candidate) => candidate.mesh);
        }

        return this.characters.filter((candidate) =>
            candidate.team !== character.team &&
            !candidate.isDead &&
            !candidate.removedFromScene &&
            candidate.mesh &&
            this.isCharacterVisibleToParty(candidate)
        );
    }

    getGridCellFromPointerEvent(event) {
        const rect = this.renderer.domElement.getBoundingClientRect();
        this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
        this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
        this.raycaster.setFromCamera(this.mouse, this.camera);

        const worldW = this.gridWidth * this.cellSize;
        const worldH = this.gridHeight * this.cellSize;
        const worldX = this.raycaster.ray.origin.x;
        const worldY = this.raycaster.ray.origin.y;
        const gridX = Math.floor((worldX + worldW / 2) / this.cellSize);
        const gridY = Math.floor((worldH / 2 - worldY) / this.cellSize);

        if (gridX < 0 || gridX >= this.gridWidth || gridY < 0 || gridY >= this.gridHeight) {
            return null;
        }

        return { gridX, gridY };
    }

    findClickedAbilityCell(event, character, ability) {
        if (!character || !ability || !this.isCellTargetedAbility(ability)) {
            return null;
        }

        const cell = this.getGridCellFromPointerEvent(event);
        if (!cell) {
            return null;
        }

        return cell;
    }

    findClickedAbilityTarget(event, character, ability) {
        if (!character || !ability) {
            return null;
        }

        if (ability.type === 'buff' || this.isCellTargetedAbility(ability)) {
            return null;
        }

        const targetPool = this.getAbilityTargetPool(character, ability);
        if (targetPool.length === 0) {
            return null;
        }

        const rect = this.renderer.domElement.getBoundingClientRect();
        this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
        this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
        this.raycaster.setFromCamera(this.mouse, this.camera);

        const intersects = this.raycaster.intersectObjects(targetPool.map((candidate) => candidate.mesh));
        if (intersects.length > 0) {
            return targetPool.find((candidate) => candidate.mesh === intersects[0].object) || null;
        }

        const clickedCell = this.getGridCellFromPointerEvent(event);
        if (!clickedCell) {
            return null;
        }

        return targetPool.find((candidate) =>
            candidate.gridX === clickedCell.gridX &&
            candidate.gridY === clickedCell.gridY
        ) || null;
    }

    getAbilityUsageHandler(ability) {
        if (!ability) {
            return null;
        }

        const handlersById = {
            'call-of-the-wolf': (character, _target, selectedAbility) => this.castCallOfTheWolf(character, selectedAbility),
            'raise-undead': (character, _target, selectedAbility) => this.castRaiseUndead(character, selectedAbility),
            charge: (character, targetCell, selectedAbility) => Boolean(targetCell) && this.castCharge(character, targetCell, selectedAbility),
            fireball: (character, targetCell, selectedAbility) => Boolean(targetCell) && this.castAreaDamageSpell(character, targetCell, selectedAbility),
            'inflict-pain': (character) => this.castInflictPain(character),
            'magic-missile': (character, target) => Boolean(target) && this.castMagicMissile(character, target),
            'poison-dart': (character, target, selectedAbility) => Boolean(target) && this.castPoisonDart(character, target, selectedAbility),
            sleep: (character, targetCell, selectedAbility) => Boolean(targetCell) && this.castSleep(character, targetCell, selectedAbility),
            'battle-shout': (character) => this.castBattleShout(character),
            blessing: (character) => this.castBlessing(character)
        };

        if (handlersById[ability.id]) {
            return handlersById[ability.id];
        }

        const handlersByType = {
            heal: (character, target, selectedAbility) => Boolean(target) && this.castHeal(character, target, selectedAbility),
            attack: (character, target, selectedAbility) => Boolean(target) && this.characterAttack(character, target, selectedAbility)
        };

        if (handlersByType[ability.type]) {
            return handlersByType[ability.type];
        }

        if (ability.type === 'spell' && ability.damage !== undefined) {
            return (character, target, selectedAbility) => Boolean(target) && this.castDamageSpell(character, target, selectedAbility);
        }

        return (character, target, selectedAbility) => Boolean(target) && this.characterAttack(character, target, selectedAbility);
    }

    isAbilityOnCooldown(ability) {
        return (ability?.cooldownRemaining ?? 0) > 0;
    }

    startAbilityCooldown(ability) {
        const cooldown = ability?.cooldownTurns ?? 0;
        if (cooldown > 0 && ability) {
            ability.cooldownRemaining = cooldown;
        }
    }

    useAbilityOnTarget(character, ability, targetCharacter = null) {
        if (!character || character.isDead || !ability) {
            return false;
        }

        if (this.gameMode === 'combat' && this.isCombatActionPending(character)) {
            return false;
        }

        if (this.isAbilityOnCooldown(ability)) {
            return false;
        }

        const abilityHandler = this.getAbilityUsageHandler(ability);
        const didUseAbility = Boolean(abilityHandler?.(character, targetCharacter, ability));

        if (didUseAbility && this.gameMode === 'exploration' && targetCharacter && targetCharacter.team !== character.team) {
            this.beginCombatWithEnemyCharacter(targetCharacter);
        }

        return didUseAbility;
    }

    castDamageSpell(caster, target, spellAbility = null) {
        if (!target || target.isDead || caster?.team === target.team) {
            return false;
        }

        const context = this.getActionContext(caster, {
            requiredActionCost: Math.max(0, Math.floor(caster?.attackCost ?? 0))
        });
        if (!context) {
            return false;
        }

        const ability = spellAbility || window.CharacterData?.getCharacterActionById(caster, caster?.selectedAbilityId) || null;
        if (!ability || ability.type !== 'spell' || ability.damage === undefined) {
            return false;
        }

        const mpCost = Math.max(0, Math.floor(ability.mpCost ?? 0));
        if ((caster.magicPoints ?? 0) < mpCost) {
            return false;
        }

        if (!this.isAbilityTargetWithinRangeAndLos(caster, ability, target.gridX, target.gridY)) {
            return false;
        }

        this.faceCharacterToward(caster, target);
        const damage = this.getEffectiveAbilityDamage(caster, ability);
        this.spendActionAndMagic(caster, context, {
            actionCost: caster.attackCost,
            magicCost: mpCost
        });
        const projectileAnimation = this.getAbilityProjectileAnimation(ability);
        const resolvesOnImpact = this.doesAbilityResolveOnImpact(ability);

        if (!resolvesOnImpact) {
            if (damage > 0) {
                this.removeSleepEffect(target);
            }
            target.hitPoints -= damage;
        }

        if (!context.isExploration && resolvesOnImpact) {
            this.beginPendingCombatAction(caster);
        }

        this.appendCombatLogEntry(
            `${caster.name} casts ${ability.name} on ${target.name} for ${damage} dmg.`,
            caster.accentColor
        );

        const lethal = resolvesOnImpact ? (target.hitPoints - damage) <= 0 : target.hitPoints <= 0;
        if (lethal && !resolvesOnImpact) {
            target.hitPoints = 0;
        }

        const casterPos = this.getCharacterWorldPos(caster);
        const targetPos = this.getCharacterWorldPos(target);
        if (projectileAnimation === 'magic-missile') {
            this.spawnMagicMissileProjectiles(casterPos, targetPos, () => {
                if (damage > 0) {
                    this.removeSleepEffect(target);
                }
                target.hitPoints = Math.max(0, target.hitPoints - damage);
                this.playHitAnimation(target);
                if (target.hitPoints <= 0) {
                    target.hitPoints = 0;
                    this.markCharacterDead(target, caster);
                }
                if (!context.isExploration) {
                    this.finishPendingCombatAction(caster);
                }
            });
        } else {
            this.playHitAnimation(target);
            if (lethal) {
                this.markCharacterDead(target, caster);
            }
            if (!context.isExploration && resolvesOnImpact) {
                this.finishPendingCombatAction(caster);
            }
            this.finalizeActionUsage(caster, context);
        }

        return true;
    }

    castAreaDamageSpell(caster, targetCell, spellAbility = null) {
        if (!targetCell) {
            return false;
        }

        const context = this.getActionContext(caster, {
            requiredActionCost: Math.max(0, Math.floor(caster?.attackCost ?? 0))
        });
        if (!context) {
            return false;
        }

        const ability = spellAbility
            ? { ...spellAbility }
            : window.CharacterData?.getCharacterActionById(caster, caster?.selectedAbilityId) || null;
        if (!ability || ability.type !== 'spell' || ability.damage === undefined || !this.isCellTargetedAbility(ability)) {
            return false;
        }

        const mpCost = Math.max(0, Math.floor(ability.mpCost ?? 0));
        if ((caster.magicPoints ?? 0) < mpCost) {
            return false;
        }

        if (!this.isAbilityTargetWithinRangeAndLos(caster, ability, targetCell.gridX, targetCell.gridY)) {
            return false;
        }

        const radius = Math.max(0, Math.floor(ability.radius ?? 0));
        const targets = this.characters.filter((candidate) =>
            !candidate.isDead &&
            !candidate.removedFromScene &&
            (ability.affectsAllCharacters || candidate.team !== caster.team) &&
            this.getAttackDistanceBetweenPositions(targetCell.gridX, targetCell.gridY, candidate.gridX, candidate.gridY) <= radius
        );

        if (targets.length === 0) {
            return false;
        }

        this.faceCharacterToward(caster, { gridX: targetCell.gridX, gridY: targetCell.gridY });
        const damage = Math.max(0, Math.floor(ability.damage ?? 0));
        this.spendActionAndMagic(caster, context, {
            actionCost: caster.attackCost,
            magicCost: mpCost
        });

        targets.forEach((target) => {
            if (damage > 0) {
                this.removeSleepEffect(target);
            }
            target.hitPoints = Math.max(0, target.hitPoints - damage);
            this.playHitAnimation(target);
            if (target.hitPoints <= 0) {
                target.hitPoints = 0;
                this.markCharacterDead(target, caster);
            }
        });

        const targetNames = targets.map((target) => target.name).join(', ');
        this.appendCombatLogEntry(
            `${caster.name} casts ${ability.name}, dealing ${damage} damage to ${targets.length} ${targets.length === 1 ? 'target' : 'targets'}: ${targetNames}.`,
            caster.accentColor
        );

        if (!context.isExploration) {
            this.finalizeActionUsage(caster, context);
        } else {
            const firstEnemyTarget = targets.find((target) => target.team !== caster.team) || null;
            if (firstEnemyTarget) {
                this.beginCombatWithEnemyCharacter(firstEnemyTarget);
            }
        }

        this.startAbilityCooldown(ability);
        return true;
    }

    beginCombatWithEnemyCharacter(character) {
        if (!character || character.team === 'player') {
            return false;
        }

        const enemyGroup = this.getEnemyGroupById(character.encounterGroupId);
        if (!enemyGroup) {
            return false;
        }

        this.beginCombatWithGroup(enemyGroup);
        return true;
    }

    clearSummonedAllies() {
        if (!Array.isArray(this.summonedAllies) || this.summonedAllies.length === 0) {
            this.summonedAllies = [];
            return;
        }

        const summonedSet = new Set(this.summonedAllies);
        this.summonedAllies.forEach((ally) => {
            ally.removedFromScene = true;
            if (ally.mesh?.parent) {
                ally.mesh.parent.remove(ally.mesh);
            }
        });

        this.characters = this.characters.filter((character) => !summonedSet.has(character));
        this.summonedAllies = [];
    }

    clearSummonedEnemies() {
        if (!Array.isArray(this.summonedEnemies) || this.summonedEnemies.length === 0) {
            this.summonedEnemies = [];
            return;
        }

        const summonedSet = new Set(this.summonedEnemies);
        this.summonedEnemies.forEach((enemy) => {
            enemy.removedFromScene = true;
            if (enemy.mesh?.parent) {
                enemy.mesh.parent.remove(enemy.mesh);
            }
        });

        this.enemyGroups.forEach((group) => {
            group.members = (group.members || []).filter((member) => !summonedSet.has(member));
        });
        this.aiParty = this.aiParty.filter((character) => !summonedSet.has(character));
        this.characters = this.characters.filter((character) => !summonedSet.has(character));
        this.summonedEnemies = [];
    }

    findSummonPlacementNearCharacter(character, minDistance = 1, maxDistance = 2) {
        if (!character) {
            return null;
        }

        const occupiedCells = new Set(
            this.characters
                .filter((candidate) => !candidate.isDead && !candidate.removedFromScene)
                .map((candidate) => this.getCellKey(candidate.gridX, candidate.gridY))
        );

        return this.findNearbyFloorTile(character.gridX, character.gridY, minDistance, maxDistance, occupiedCells);
    }

    findSummonPlacementsTowardOpponents(character, summonCount = 2, minDistance = 1, maxDistance = 3) {
        if (!character || summonCount <= 0) {
            return [];
        }

        const opponents = this.getLivingCharacters(this.getOpposingGroupForCharacter(character));
        if (opponents.length === 0) {
            return [];
        }

        const primaryTarget = opponents.reduce((closest, candidate) => {
            if (!closest) {
                return candidate;
            }

            const candidateDistance = this.getAttackDistanceBetweenPositions(character.gridX, character.gridY, candidate.gridX, candidate.gridY);
            const closestDistance = this.getAttackDistanceBetweenPositions(character.gridX, character.gridY, closest.gridX, closest.gridY);
            return candidateDistance < closestDistance ? candidate : closest;
        }, null);

        if (!primaryTarget) {
            return [];
        }

        const occupiedCells = new Set(
            this.characters
                .filter((candidate) => !candidate.isDead && !candidate.removedFromScene)
                .map((candidate) => this.getCellKey(candidate.gridX, candidate.gridY))
        );

        const toTargetX = primaryTarget.gridX - character.gridX;
        const toTargetY = primaryTarget.gridY - character.gridY;
        const toTargetLength = Math.hypot(toTargetX, toTargetY) || 1;

        const candidates = [];
        for (let y = character.gridY - maxDistance; y <= character.gridY + maxDistance; y++) {
            for (let x = character.gridX - maxDistance; x <= character.gridX + maxDistance; x++) {
                if (x < 0 || x >= this.gridWidth || y < 0 || y >= this.gridHeight) {
                    continue;
                }

                if (this.dungeonMap[y]?.[x] !== this.TILE_FLOOR) {
                    continue;
                }

                const cellDistance = this.getAttackDistanceBetweenPositions(character.gridX, character.gridY, x, y);
                if (cellDistance < minDistance || cellDistance > maxDistance) {
                    continue;
                }

                const cellKey = this.getCellKey(x, y);
                if (occupiedCells.has(cellKey)) {
                    continue;
                }

                const toCellX = x - character.gridX;
                const toCellY = y - character.gridY;
                const toCellLength = Math.hypot(toCellX, toCellY) || 1;
                const directionDot = (toCellX * toTargetX + toCellY * toTargetY) / (toCellLength * toTargetLength);
                const forwardBias = Math.max(0, directionDot);
                const targetDistance = this.getAttackDistanceBetweenPositions(x, y, primaryTarget.gridX, primaryTarget.gridY);

                const score = (forwardBias * 100) - (cellDistance * 6) - (targetDistance * 1.2);
                candidates.push({ x, y, score, cellDistance, targetDistance });
            }
        }

        candidates.sort((left, right) => {
            if (right.score !== left.score) {
                return right.score - left.score;
            }
            if (left.cellDistance !== right.cellDistance) {
                return left.cellDistance - right.cellDistance;
            }
            if (left.targetDistance !== right.targetDistance) {
                return left.targetDistance - right.targetDistance;
            }
            if (left.y !== right.y) {
                return left.y - right.y;
            }
            return left.x - right.x;
        });

        return candidates.slice(0, summonCount).map(({ x, y }) => ({ x, y }));
    }

    createWolfCompanion(caster) {
        const wolf = this.createCharacter({
            id: `wolf-companion-${this.nextSummonedAllyId++}`,
            name: 'Wolf Companion',
            role: 'AI',
            team: 'ally',
            accentColor: '#b9c68b',
            pointerColor: 0xd6f0a2,
            spriteFrame: this.getCharacterSpriteFrame('wolf'),
            race: 'wolf',
            strength: 10,
            dexterity: 12,
            intelligence: 2,
            wisdom: 6,
            initiative: 10,
            hitPoints: 10,
            maxHitPoints: 10,
            magicPoints: 0,
            maxMagicPoints: 0,
            armorClass: 1,
            attackCost: 2,
            maxActionsPerTurn: 5,
            abilities: ['wolf-bite'],
            spells: []
        });
        wolf.isSummonedWolf = true;
        wolf.summonerId = caster?.id || null;
        return wolf;
    }

    createSummonedSkeletonWarrior(caster) {
        const skeleton = this.createCharacter({
            id: `raised-skeleton-warrior-${this.nextSummonedEnemyId++}`,
            name: 'Raised Skeleton Warrior',
            role: 'AI',
            team: 'ai',
            accentColor: '#cbbca3',
            pointerColor: 0xe4d3b2,
            spriteFrame: this.getCharacterSpriteFrame('skeletonWarrior'),
            race: 'undead',
            strength: 11,
            dexterity: 8,
            intelligence: 3,
            wisdom: 3,
            hitPoints: 11,
            maxHitPoints: 11,
            armorClass: 2,
            experiencePoints: 0,
            abilities: ['sword-slash'],
            equipment: {
                hands: 'short-sword'
            },
            spells: []
        });

        skeleton.isSummonedUndead = true;
        skeleton.summonerId = caster?.id || null;
        return skeleton;
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

    getNearbyPartyInteractionCharacter(gridX, gridY, maxDistance = 1, preferredCharacter = null) {
        const candidates = this.getLivingCharacters(this.playerParty)
            .filter((character) => this.canCharacterInteractWithCell(character, gridX, gridY, maxDistance));

        if (candidates.length === 0) {
            return null;
        }

        if (preferredCharacter && candidates.includes(preferredCharacter)) {
            return preferredCharacter;
        }

        return candidates.sort((left, right) => {
            const leftDistance = this.getAttackDistanceBetweenPositions(left.gridX, left.gridY, gridX, gridY);
            const rightDistance = this.getAttackDistanceBetweenPositions(right.gridX, right.gridY, gridX, gridY);
            return leftDistance - rightDistance;
        })[0];
    }

    canCharacterInteractWithCell(character, gridX, gridY, maxDistance = 1) {
        if (!character || character.isDead) {
            return false;
        }

        const distance = this.getAttackDistanceBetweenPositions(character.gridX, character.gridY, gridX, gridY);
        return distance <= maxDistance;
    }

    isCellOccupiedByLivingEnemy(cellKey) {
        const cell = this.parseCellKey(cellKey);
        if (!cell) {
            return false;
        }

        return this.characters.some((character) =>
            !this.isPartyAlignedCharacter(character) &&
            !character.isDead &&
            !character.removedFromScene &&
            character.gridX === cell.gridX &&
            character.gridY === cell.gridY
        );
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

    getScreenPositionForCell(gridX, gridY) {
        if (!this.renderer?.domElement || !this.camera) {
            return null;
        }

        const worldPos = this.getWorldPositionForCell(gridX, gridY);
        return this.getScreenPositionForWorldPosition(worldPos.x, worldPos.y);
    }

    getScreenPositionForWorldPosition(worldX, worldY) {
        if (!this.renderer?.domElement || !this.camera) {
            return null;
        }

        const vec = new THREE.Vector3(worldX, worldY, 0);
        vec.project(this.camera);

        const rect = this.renderer.domElement.getBoundingClientRect();
        return {
            screenX: (vec.x * 0.5 + 0.5) * rect.width + rect.left,
            screenY: (-vec.y * 0.5 + 0.5) * rect.height + rect.top
        };
    }

    showCharacterToast(character, message, color = '#d6cbb8', durationMs = 2200) {
        if (!character) {
            this.showToast(message, color, durationMs);
            return;
        }

        const worldPos = this.getCharacterRenderedWorldPos(character);
        const anchor = worldPos
            ? this.getScreenPositionForWorldPosition(worldPos.x, worldPos.y)
            : this.getScreenPositionForCell(character.gridX, character.gridY);
        this.showToast(message, color, durationMs, anchor?.screenX ?? null, anchor?.screenY ?? null);
    }

    showOutOfRangeToastAtCell(gridX, gridY, message = 'Skill is out of range') {
        const anchor = this.getScreenPositionForCell(gridX, gridY);
        this.showToast(message, '#d6cbb8', 2200, anchor?.screenX ?? null, anchor?.screenY ?? null);
    }

    isAbilityTargetOutOfRange(character, ability, targetCharacter) {
        if (!character || !ability || !targetCharacter) {
            return false;
        }

        if (this.isCellTargetedAbility(ability)) {
            if (ability.id === 'charge') {
                return !this.getChargeDestination(character, ability, targetCharacter.gridX, targetCharacter.gridY);
            }

            const range = this.getEffectiveAbilityRange(character, ability);
            const distance = this.getAttackDistanceBetweenPositions(character.gridX, character.gridY, targetCharacter.gridX, targetCharacter.gridY);
            return distance > range;
        }

        const targetInfo = this.getExpectedActionEffect(character, targetCharacter, ability);
        return Boolean(targetInfo) && !targetInfo.withinRange;
    }

    trySearchDungeonPropAtCell(cellKey, interactor = null, screenX = null, screenY = null) {
        const prop = this.dungeonPropsByCell.get(cellKey);
        if (!prop) {
            return false;
        }

        const propDisplayName = prop.name || window.GameData?.getDungeonPropDisplayName?.(prop.frameId) || prop.frameId || 'container';

        const actingCharacter = interactor || this.getLootInteractionCharacter();
        const canInteract = this.canCharacterInteractWithCell(actingCharacter, prop.gridX, prop.gridY, 1);

        if (prop.isVendor) {
            if (!canInteract) {
                const anchor = screenX !== null && screenY !== null
                    ? { screenX, screenY }
                    : this.getScreenPositionForCell(prop.gridX, prop.gridY);
                this.showToast('Need to be adjacent to talk.', '#d6cbb8', 2200, anchor?.screenX ?? null, anchor?.screenY ?? null);
                return false;
            }

            const anchor = screenX !== null && screenY !== null
                ? { screenX, screenY }
                : this.getScreenPositionForCell(prop.gridX, prop.gridY);

            this.showConfirmationToast(
                `${prop.vendorName || prop.name || 'Vendor'}`,
                {
                    confirmLabel: 'Shop',
                    cancelLabel: 'Talk',
                    color: '#8fd3ff',
                    screenX: anchor?.screenX ?? null,
                    screenY: anchor?.screenY ?? null,
                    onConfirm: () => {
                        this.openVendorStoreMenu(prop, actingCharacter);
                    },
                    onCancel: () => {
                        this.showToast('Welcome to Forest Town!', '#8fd3ff', 2200, anchor?.screenX ?? null, anchor?.screenY ?? null);
                    }
                }
            );
            return true;
        }

        if (prop.signPostMessage) {
            const anchor = screenX !== null && screenY !== null
                ? { screenX, screenY }
                : this.getScreenPositionForCell(prop.gridX, prop.gridY);

            if (!canInteract) {
                this.showToast('Need to be adjacent to read.', '#d6cbb8', 2200, anchor?.screenX ?? null, anchor?.screenY ?? null);
                return false;
            }

            this.showToast(
                prop.signPostMessage,
                prop.signPostMessageColor || '#d6cbb8',
                2400,
                anchor?.screenX ?? null,
                anchor?.screenY ?? null
            );
            return true;
        }

        if (!prop.searchable) {
            return false;
        }

        if (this.isCellOccupiedByLivingEnemy(cellKey)) {
            return false;
        }

        if (!canInteract) {
            const anchor = screenX !== null && screenY !== null
                ? { screenX, screenY }
                : this.getScreenPositionForCell(prop.gridX, prop.gridY);
            this.showToast('Skill is out of range', '#d6cbb8', 2200, anchor?.screenX ?? null, anchor?.screenY ?? null);
            return false;
        }

        if (!this.lootDropsByCell.has(cellKey) && !prop.hasBeenSearched) {
            prop.hasBeenSearched = true;
            const loot = this.rollDungeonPropLoot(prop);
            const hasGold = (loot?.gold ?? 0) > 0;
            const hasEquipment = Array.isArray(loot?.equipmentDrops) && loot.equipmentDrops.length > 0;
            if (loot && (hasGold || hasEquipment)) {
                this.registerLootDropAtCell(prop.gridX, prop.gridY, loot, {
                    sourceType: 'prop',
                    sourceLabel: prop.roomTheme || 'Room Fixture',
                    containerName: `Search: ${propDisplayName}`
                });
                this.appendCombatLogEntry(
                    `${actingCharacter.name} searches ${propDisplayName} and finds something.`,
                    '#d9c47d'
                );
                this.saveCurrentMapPersistentState();
            } else {
                this.showToast('Empty', '#8f856f', 2200, screenX, screenY);
                this.saveCurrentMapPersistentState();
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
            const anchor = this.getScreenPositionForCell(character.gridX, character.gridY);
            const searched = this.trySearchDungeonPropAtCell(cellKey, character, anchor?.screenX ?? null, anchor?.screenY ?? null);
            if (searched) {
                return true;
            }
        }

        if (this.lootDropsByCell.has(cellKey)) {
            return this.openLootMenuForCell(cellKey, character);
        }

        return false;
    }

    isTriggeredDungeonTrap(prop) {
        if (!prop || prop.hasBeenTriggered) {
            return false;
        }

        const frameId = String(prop.frameId || '').toLowerCase();
        const roomTheme = String(prop.roomTheme || '').toLowerCase();
        return roomTheme === 'trap' || frameId.includes('trap');
    }

    triggerDungeonTrapAtCell(gridX, gridY) {
        const cellKey = this.getCellKey(gridX, gridY);
        const prop = this.dungeonPropsByCell.get(cellKey);

        if (!this.isTriggeredDungeonTrap(prop)) {
            return false;
        }

        prop.hasBeenTriggered = true;

        const affectedParty = this.getLivingCharacters(this.playerParty);
        affectedParty.forEach((character) => {
            character.hitPoints = Math.max(0, character.hitPoints - 3);
            this.playHitAnimation(character);
            if (character.hitPoints <= 0) {
                this.markCharacterDead(character);
            }
        });

        this.appendCombatLogEntry(
            `The party triggers ${prop.frameId} and takes 3 damage each.`,
            '#d97d7d'
        );

        return true;
    }

    isHauntedGraveProp(prop) {
        if (!prop || prop.hasBeenTriggered) {
            return false;
        }

        const spawnArchetypeId = String(prop.graveSpawnArchetypeId || '').trim();
        if (spawnArchetypeId) {
            return true;
        }

        const frameId = String(prop.frameId || '').toLowerCase();
        const roomTheme = String(prop.roomTheme || '').toLowerCase();
        return roomTheme === 'graveyard' && frameId.includes('tomb');
    }

    triggerHauntedGravesNearParty() {
        if (this.gameMode !== 'exploration') {
            return false;
        }

        const party = this.getLivingCharacters(this.playerParty);
        if (party.length === 0) {
            return false;
        }

        let didSpawn = false;
        this.dungeonPropsByCell.forEach((prop) => {
            if (!this.isHauntedGraveProp(prop)) {
                return;
            }

            const triggerRange = Math.max(1, Math.floor(prop.graveSpawnRange ?? 5));
            const isPartyNearby = party.some((member) =>
                this.getAttackDistanceBetweenPositions(member.gridX, member.gridY, prop.gridX, prop.gridY) <= triggerRange
            );
            if (!isPartyNearby) {
                return;
            }

            prop.hasBeenTriggered = true;

            const spawnChance = Math.max(0, Math.min(1, Number(prop.graveSpawnChance ?? 0.25)));
            if (Math.random() > spawnChance) {
                return;
            }

            if (this.spawnEnemyFromHauntedGrave(prop, party)) {
                didSpawn = true;
            }
        });

        if (didSpawn) {
            this.saveCurrentMapPersistentState();
        }

        return didSpawn;
    }

    spawnEnemyFromHauntedGrave(prop, partyMembers = []) {
        const spawnArchetypeId = String(prop?.graveSpawnArchetypeId || 'zombie').trim() || 'zombie';
        const archetype = this.getEnemyArchetypeById(spawnArchetypeId);
        if (!archetype) {
            return false;
        }

        const occupiedCells = new Set(
            this.characters
                .filter((candidate) => !candidate.isDead && !candidate.removedFromScene)
                .map((candidate) => this.getCellKey(candidate.gridX, candidate.gridY))
        );

        const spawn = this.findNearbyFloorTile(prop.gridX, prop.gridY, 1, 1, occupiedCells);
        if (!spawn) {
            return false;
        }

        const groupId = `group-grave-${spawnArchetypeId}-${prop.gridX}-${prop.gridY}-${Date.now()}`;
        const enemy = this.createEnemyFromArchetype(archetype, this.enemyGroups.length + 1, 0);
        enemy.gridX = spawn.x;
        enemy.gridY = spawn.y;
        enemy.encounterGroupId = groupId;

        const nearestPartyMember = partyMembers.reduce((closest, member) => {
            if (!closest) {
                return member;
            }

            const currentDistance = this.getAttackDistanceBetweenPositions(enemy.gridX, enemy.gridY, member.gridX, member.gridY);
            const closestDistance = this.getAttackDistanceBetweenPositions(enemy.gridX, enemy.gridY, closest.gridX, closest.gridY);
            return currentDistance < closestDistance ? member : closest;
        }, null);
        if (nearestPartyMember) {
            this.faceCharacterToward(enemy, nearestPartyMember);
        }

        this.aiParty.push(enemy);
        this.characters.push(enemy);
        this.enemyGroups.push({
            id: groupId,
            members: [enemy],
            isAggro: false,
            isCleared: false
        });

        this.setupCharacterSprite(
            enemy,
            this.createSpriteTexture(enemy.spriteFrame),
            enemy.pointerColor
        );

        this.appendCombatLogEntry(
            `A ${enemy.name} claws its way out of a nearby grave.`,
            '#d97d7d'
        );

        return true;
    }

    rollDungeonPropLoot(prop) {
        if (!prop || !prop.searchable) {
            return null;
        }

        const customLootItemIds = Array.isArray(prop.lootItemIds)
            ? prop.lootItemIds.filter((itemId) => typeof itemId === 'string' && itemId.trim().length > 0)
            : [];
        const customLootMode = prop.lootMode === 'all' ? 'all' : 'random';

        const lootProfile = window.GameData?.getDungeonPropLootProfile?.(prop.frameId) || {
            chance: 0.35,
            minGold: 1,
            maxGold: 4,
            equipmentDropChance: 0.1
        };

        let chance = lootProfile.chance;
        let minGold = lootProfile.minGold;
        let maxGold = lootProfile.maxGold;

        if (customLootItemIds.length > 0 && customLootMode === 'all') {
            chance = 1;
        }

        if (Math.random() > chance) {
            return null;
        }

        const hasConfiguredGold = Number.isFinite(prop.goldAmount);
        const gold = hasConfiguredGold
            ? Math.max(0, Math.floor(prop.goldAmount))
            : minGold + Math.floor(Math.random() * (maxGold - minGold + 1));
        const equipmentDropChance = lootProfile.equipmentDropChance;
        const equipmentDrops = [];
        if (customLootItemIds.length > 0) {
            if (customLootMode === 'all') {
                const configuredDrops = this.createEquipmentItemsFromIds(customLootItemIds);
                if (configuredDrops.length > 0) {
                    equipmentDrops.push(...configuredDrops);
                }
            } else if (Math.random() < equipmentDropChance) {
                const equipment = this.rollEquipmentLootItem(customLootItemIds);
                if (equipment) {
                    equipmentDrops.push(equipment);
                }
            }
        } else if (Math.random() < equipmentDropChance) {
            const equipment = this.rollEquipmentLootItem();
            if (equipment) {
                equipmentDrops.push(equipment);
            }
        }

        return { gold, equipmentDrops };
    }

    getEquipmentLootTable(lootItemIds = null) {
        const defaultLootIds = ['small-shield', 'long-bow', 'mages-amulet', 'healers-circlet'];
        const hasCustomLootIds = Array.isArray(lootItemIds) && lootItemIds.length > 0;
        const lootIds = hasCustomLootIds
            ? lootItemIds
            : (window.GameData?.EQUIPMENT_LOOT_ITEM_IDS ?? defaultLootIds);

        return lootIds
            .map((itemId) => window.GameData?.getItemTemplateById(itemId) || null)
            .filter(Boolean);
    }

    getVendorStockItemIds(vendorProp = null) {
        return this.getVendorStockEntries(vendorProp).map((entry) => entry.itemId);
    }

    normalizeVendorStockEntries(stockEntries) {
        if (!Array.isArray(stockEntries)) {
            return [];
        }

        return stockEntries
            .map((entry) => {
                if (typeof entry === 'string') {
                    const itemId = entry.trim();
                    if (!itemId) {
                        return null;
                    }

                    return { itemId, amount: 1 };
                }

                if (!entry || typeof entry !== 'object' || Array.isArray(entry)) {
                    return null;
                }

                const itemId = String(entry.itemId ?? entry.id ?? '').trim();
                if (!itemId) {
                    return null;
                }

                const rawAmount = entry.amount;
                const normalizedAmount = rawAmount === undefined
                    ? 1
                    : Math.max(0, Math.floor(Number(rawAmount) || 0));

                if (normalizedAmount <= 0) {
                    return null;
                }

                return { itemId, amount: normalizedAmount };
            })
            .filter(Boolean);
    }

    getVendorStockEntries(vendorProp = null) {
        const configured = this.normalizeVendorStockEntries(vendorProp?.storeInventoryItemIds);
        if (configured.length > 0 || (vendorProp && vendorProp.hasConfiguredStoreInventory)) {
            if (vendorProp) {
                vendorProp.storeInventoryItemIds = configured.map((entry) => ({ ...entry }));
            }

            return configured;
        }

        const fallback = this.normalizeVendorStockEntries(window.GameData?.EQUIPMENT_LOOT_ITEM_IDS ?? ['small-shield', 'long-bow']);
        if (vendorProp) {
            vendorProp.storeInventoryItemIds = fallback.map((entry) => ({ ...entry }));
        }

        return fallback;
    }

    getVendorItemPrice(itemOrTemplate, mode = 'buy', vendorProp = null) {
        if (!itemOrTemplate) {
            return 0;
        }

        const modifiers = itemOrTemplate.modifiers || {};
        const baseValue = 18 +
            (Math.max(0, modifiers.armorClass ?? 0) * 26) +
            (Math.max(0, modifiers.attackDamage ?? 0) * 14) +
            (Math.max(0, modifiers.attackRange ?? 0) * 7) +
            (Math.max(0, modifiers.spellDamage ?? 0) * 18) +
            (Math.max(0, modifiers.healingBonus ?? 0) * 16) +
            (Math.max(0, modifiers.strength ?? 0) * 14) +
            (Math.max(0, modifiers.dexterity ?? 0) * 14) +
            (Math.max(0, modifiers.intelligence ?? 0) * 14) +
            (Math.max(0, modifiers.wisdom ?? 0) * 14);

        const isSell = mode === 'sell';
        const multiplier = isSell
            ? Math.max(0.05, Number(vendorProp?.storeSellMultiplier ?? 0.5) || 0.5)
            : Math.max(0.1, Number(vendorProp?.storeBuyMultiplier ?? 1) || 1);

        return Math.max(1, Math.round(baseValue * multiplier));
    }

    buyVendorItem(vendorProp, itemId) {
        if (!itemId) {
            return false;
        }

        const stockEntries = this.getVendorStockEntries(vendorProp);
        const stockIndex = stockEntries.findIndex((entry) => entry.itemId === itemId && entry.amount > 0);
        if (stockIndex < 0) {
            this.showToast('That item is unavailable.', '#b8ad96', 2200);
            return false;
        }

        const template = window.GameData?.getItemTemplateById(itemId);
        if (!template) {
            this.showToast('That item is unavailable.', '#b8ad96', 2200);
            return false;
        }

        const price = this.getVendorItemPrice(template, 'buy', vendorProp);
        const currentGold = Math.max(0, Math.floor(this.sharedLootInventory?.gold ?? 0));
        if (currentGold < price) {
            this.showToast('Not enough gold.', '#d97d7d', 2200);
            return false;
        }

        const purchasedItem = this.createEquipmentItemInstance(template);
        if (!purchasedItem) {
            this.showToast('Purchase failed.', '#d97d7d', 2200);
            return false;
        }

        this.sharedLootInventory.gold = currentGold - price;
        this.addEquipmentItemToSharedInventory(purchasedItem);
        stockEntries[stockIndex].amount -= 1;
        if (stockEntries[stockIndex].amount <= 0) {
            stockEntries.splice(stockIndex, 1);
        }
        vendorProp.storeInventoryItemIds = stockEntries.map((entry) => ({ ...entry }));
        this.appendCombatLogEntry(
            `Party buys ${this.getEquipmentItemLabel(purchasedItem)} for ${price} gold.`,
            '#d9c47d'
        );
        this.showToast(`Purchased ${this.getEquipmentItemLabel(purchasedItem)}.`, purchasedItem.accentColor || '#d9c47d', 2200);
        return true;
    }

    sellVendorItem(vendorProp, instanceId) {
        if (!instanceId) {
            return false;
        }

        const sharedItems = this.getSharedLootItems();
        const itemIndex = sharedItems.findIndex((item) => item.instanceId === instanceId);
        if (itemIndex < 0) {
            this.showToast('That item is no longer available.', '#b8ad96', 2200);
            return false;
        }

        const item = this.ensureEquipmentItemInstance(sharedItems[itemIndex]);
        if (!item) {
            this.showToast('Unable to sell item.', '#d97d7d', 2200);
            return false;
        }

        const template = window.GameData?.getItemTemplateById(item.id) || item;
        const price = this.getVendorItemPrice(template, 'sell', vendorProp);
        sharedItems.splice(itemIndex, 1);
        this.sharedLootInventory.gold = Math.max(0, Math.floor(this.sharedLootInventory?.gold ?? 0)) + price;
        this.appendCombatLogEntry(
            `Party sells ${this.getEquipmentItemLabel(item)} for ${price} gold.`,
            '#d9c47d'
        );
        this.showToast(`Sold ${this.getEquipmentItemLabel(item)}.`, '#d9c47d', 2200);
        return true;
    }

    createEquipmentItemsFromIds(lootItemIds) {
        const table = this.getEquipmentLootTable(lootItemIds);
        return table
            .map((template) => this.createEquipmentItemInstance(template))
            .filter(Boolean);
    }

    createEquipmentItemInstance(template) {
        if (!template) {
            return null;
        }

        if (template.type === 'spell-scroll') {
            return this.createInventoryItemInstance(template);
        }

        return {
            instanceId: `eq-${this.nextLootItemId++}`,
            id: template.id,
            name: template.name,
            slot: template.slot,
            handType: template.handType || null,
            type: template.type || this.getEquipmentItemType(template, template.slot),
            appliesToAbilityId: template.appliesToAbilityId || null,
            modifiers: { ...(template.modifiers || {}) },
            accentColor: template.accentColor || '#d6cbb8'
        };
    }

    createInventoryItemInstance(template) {
        if (!template) {
            return null;
        }

        return {
            instanceId: `item-${this.nextLootItemId++}`,
            id: template.id,
            name: template.name,
            type: template.type || 'item',
            spellId: template.spellId || null,
            allowedClasses: Array.isArray(template.allowedClasses) ? [...template.allowedClasses] : [],
            accentColor: template.accentColor || '#d6cbb8',
            modifiers: { ...(template.modifiers || {}) }
        };
    }

    getLegacyEquipmentTemplate(itemName, fallbackSlot = null) {
        const normalized = String(itemName || '').trim();
        if (!normalized) {
            return null;
        }

        const armorClassMatch = normalized.match(/([+-]?\d+)\s*AC/i);
        const attackDamageMatch = normalized.match(/([+-]?\d+)\s*DMG/i);
        const attackRangeMatch = normalized.match(/Range\s*([+-]?\d+)/i);

        const template = window.GameData?.getItemTemplateByRef(normalized);
        if (!template) {
            return null;
        }

        const resolved = {
            ...template,
            slot: fallbackSlot || template.slot,
            modifiers: { ...(template.modifiers || {}) }
        };

        if (armorClassMatch) {
            resolved.modifiers.armorClass = Number(armorClassMatch[1]);
        }
        if (attackDamageMatch) {
            resolved.modifiers.attackDamage = Number(attackDamageMatch[1]);
        }
        if (attackRangeMatch) {
            resolved.modifiers.attackRange = Number(attackRangeMatch[1]);
        }

        return resolved;
    }

    ensureEquipmentItemInstance(item, fallbackSlot = null) {
        if (!item) {
            return null;
        }

        if (typeof item !== 'object') {
            const parsedTemplate = this.getLegacyEquipmentTemplate(item, fallbackSlot);
            if (parsedTemplate) {
                return this.createEquipmentItemInstance(parsedTemplate);
            }

            return this.createEquipmentItemInstance({
                id: `legacy-${fallbackSlot || 'item'}`,
                name: String(item),
                slot: fallbackSlot || 'hands',
                type: 'armor',
                modifiers: {},
                accentColor: '#b8ad96'
            });
        }

        const template = item.id ? window.GameData?.getItemTemplateById(item.id) || null : null;
        const ensured = template?.type === 'spell-scroll'
            ? { ...template, ...item }
            : { ...item };
        if (ensured.type === 'spell-scroll') {
            if (!ensured.instanceId) {
                ensured.instanceId = `item-${this.nextLootItemId++}`;
            }
            if (!ensured.name) {
                ensured.name = 'Unknown Scroll';
            }
            if (!ensured.spellId && template?.spellId) {
                ensured.spellId = template.spellId;
            }
            if (!Array.isArray(ensured.allowedClasses)) {
                ensured.allowedClasses = Array.isArray(template?.allowedClasses) ? [...template.allowedClasses] : [];
            }
            return ensured;
        }

        if (!ensured.instanceId) {
            ensured.instanceId = `eq-${this.nextLootItemId++}`;
        }
        if (!ensured.modifiers || typeof ensured.modifiers !== 'object') {
            ensured.modifiers = {};
        }
        if (!ensured.slot && fallbackSlot) {
            ensured.slot = fallbackSlot;
        }
        if (!ensured.name) {
            ensured.name = 'Unknown Item';
        }
        ensured.type = this.getEquipmentItemType(ensured, fallbackSlot);
        return ensured;
    }

    rollEquipmentLootItem(lootItemIds = null) {
        const table = this.getEquipmentLootTable(lootItemIds);
        if (!Array.isArray(table) || table.length === 0) {
            return null;
        }

        const selected = table[Math.floor(Math.random() * table.length)];
        return this.createEquipmentItemInstance(selected);
    }

    getEquipmentItemLabel(item) {
        if (!item) {
            return 'Unknown Item';
        }

        if (item.type === 'spell-scroll') {
            return item.name || 'Spell Scroll';
        }

        if (item.handType) {
            return `${item.name} ${item.handType}`;
        }

        return item.name;
    }

    getEquipmentItemModifierSummary(item) {
        if (!item?.modifiers) {
            if (item?.type === 'spell-scroll') {
                const spell = window.GameData?.getSpellTemplateById(item.spellId) || null;
                return spell ? `Teaches ${spell.name}` : 'Teaches a spell';
            }
            return '';
        }

        if (item.type === 'spell-scroll') {
            const spell = window.GameData?.getSpellTemplateById(item.spellId) || null;
            return spell ? `Teaches ${spell.name}` : 'Teaches a spell';
        }

        const parts = [];
        if ((item.modifiers.armorClass ?? 0) > 0) {
            parts.push(`+${item.modifiers.armorClass} AC`);
        }
        if ((item.modifiers.attackDamage ?? 0) > 0) {
            parts.push(`${item.modifiers.attackDamage} DMG`);
        }
        if ((item.modifiers.attackRange ?? 0) > 0) {
            parts.push(`Range ${item.modifiers.attackRange}`);
        }
        if ((item.modifiers.spellDamage ?? 0) > 0) {
            parts.push(`+${item.modifiers.spellDamage} spell dmg`);
        }
        if ((item.modifiers.healingBonus ?? 0) > 0) {
            parts.push(`+${item.modifiers.healingBonus} healing`);
        }
        if ((item.modifiers.strength ?? 0) > 0) {
            parts.push(`+${item.modifiers.strength} STR`);
        }

        return parts.join(', ');
    }

    getEquipmentItemSlotLabel(slot) {
        const labels = {
            head: 'Head',
            body: 'Body',
            hands: 'Hands',
            rightHand: 'Right Hand',
            leftHand: 'Left Hand',
            legs: 'Legs',
            feet: 'Feet',
            neck: 'Neck'
        };
        return labels[slot] || slot || 'Slot';
    }

    getEquipmentItemTypeLabel(type) {
        if (type === 'spell-scroll') {
            return 'Spell Scroll';
        }
        return type === 'weapon' ? 'Weapon' : 'Armor';
    }

    getEquipmentItemType(item, slotKey = null) {
        if (!item) {
            return 'armor';
        }

        if (item.type === 'spell-scroll') {
            return 'spell-scroll';
        }

        if (item.type === 'weapon' || item.type === 'armor') {
            return item.type;
        }

        const handType = String(item.handType ?? '').toUpperCase();
        if (handType === '2H' || item.appliesToAbilityId || (item.modifiers?.attackDamage ?? 0) > 0 || (item.modifiers?.attackRange ?? 0) > 0) {
            return 'weapon';
        }

        return 'armor';
    }

    coerceEquipmentItem(item, fallbackSlot = null) {
        if (!item) {
            return null;
        }

        if (typeof item !== 'object') {
            return this.getLegacyEquipmentTemplate(item, fallbackSlot) || {
                name: String(item),
                slot: fallbackSlot || 'hands',
                type: 'armor',
                modifiers: {},
                accentColor: '#b8ad96'
            };
        }

        const coerced = { ...item };
        if (!coerced.modifiers || typeof coerced.modifiers !== 'object') {
            coerced.modifiers = {};
        }
        if (!coerced.slot && fallbackSlot) {
            coerced.slot = fallbackSlot;
        }
        if (!coerced.name) {
            coerced.name = 'Unknown Item';
        }
        coerced.type = this.getEquipmentItemType(coerced, fallbackSlot);
        return coerced;
    }

    getCharacterEquippedItem(character, slotKey) {
        if (slotKey === 'hands') {
            const equipped = character?.equipment?.rightHand ?? character?.equipment?.leftHand ?? character?.equipment?.hands;
            return this.coerceEquipmentItem(equipped, 'hands');
        }

        if (slotKey === 'rightHand') {
            const equipped = character?.equipment?.rightHand ?? character?.equipment?.hands;
            return this.coerceEquipmentItem(equipped, 'rightHand');
        }

        if (slotKey === 'leftHand') {
            const equipped = character?.equipment?.leftHand ?? null;
            return this.coerceEquipmentItem(equipped, 'leftHand');
        }

        const equipped = character?.equipment?.[slotKey];
        return this.coerceEquipmentItem(equipped, slotKey);
    }

    getCharacterHandItems(character) {
        return {
            rightHand: this.getCharacterEquippedItem(character, 'rightHand'),
            leftHand: this.getCharacterEquippedItem(character, 'leftHand')
        };
    }

    isTwoHandedEquipmentItem(item) {
        return String(item?.handType ?? '').toUpperCase() === '2H';
    }

    getCharacterHandSlotState(character, slotKey) {
        const handItems = this.getCharacterHandItems(character);
        const item = handItems[slotKey] ?? null;
        const blockerKey = slotKey === 'rightHand' ? 'leftHand' : 'rightHand';
        const blocker = handItems[blockerKey] ?? null;

        return {
            item,
            blocker,
            isBlocked: this.isTwoHandedEquipmentItem(blocker),
            blockerLabel: blocker ? this.getEquipmentItemLabel(blocker) : null
        };
    }

    syncCharacterHandAliases(character) {
        if (!character?.equipment) {
            return;
        }

        character.equipment.hands = character.equipment.rightHand ?? null;
    }

    getCharacterAttackEquipmentItem(character, ability = null) {
        return this.getCharacterWeaponItem(character, ability);
    }

    getCharacterWeaponItems(character) {
        return ['rightHand', 'leftHand']
            .map((slotKey) => this.getCharacterEquippedItem(character, slotKey))
            .filter((item) => item && this.getEquipmentItemType(item, item.slot) === 'weapon');
    }

    getCharacterWeaponItem(character, ability = null) {
        const weaponItems = this.getCharacterWeaponItems(character);
        if (weaponItems.length === 0) {
            return null;
        }

        if (ability?.id) {
            const matchedWeapon = weaponItems.find((item) => item.appliesToAbilityId === ability.id);
            if (matchedWeapon) {
                return matchedWeapon;
            }
        }

        return weaponItems[0] || null;
    }

    getCharacterMeleeWeaponDamage(character, ability = null) {
        const weaponItems = this.getCharacterWeaponItems(character);
        const matchedWeaponItems = ability?.id
            ? weaponItems.filter((item) => item.appliesToAbilityId === ability.id)
            : [];
        let damageSources = matchedWeaponItems.length > 0 ? matchedWeaponItems : weaponItems.filter((item) => item.appliesToAbilityId === 'melee');

        if (damageSources.length === 0 && ability?.weaponDriven) {
            damageSources = weaponItems;
        }

        const weaponDamage = damageSources.reduce((totalDamage, item) => {
            return totalDamage + Math.max(0, item.modifiers?.attackDamage ?? 0);
        }, 0);

        if (weaponDamage > 0 || ability?.damage === undefined) {
            return weaponDamage;
        }

        return Math.max(weaponDamage, ability.damage ?? 0);
    }

    getCharacterMeleeDamageBonus(character) {
        return this.getCharacterStrengthDamageBonus(character)
            + this.getCharacterEffectBonus(character, 'damageBonus');
    }

    canCharacterUseAttackAbility(character, ability = null) {
        if (!ability || ability.type !== 'attack') {
            return true;
        }

        if (ability.weaponDriven && !this.getCharacterWeaponItem(character, ability)) {
            return false;
        }

        if (this.getCharacterWeaponItem(character, ability)) {
            return true;
        }

        return this.getEffectiveAbilityRange(character, ability) <= 1;
    }

    getEquipmentPlacementForItem(character, item) {
        if (!character || !item) {
            return null;
        }

        if (item.slot !== 'hands') {
            return {
                targetSlot: item.slot,
                slotsToClear: [item.slot]
            };
        }

        const rightHandItem = this.getCharacterEquippedItem(character, 'rightHand');
        const leftHandItem = this.getCharacterEquippedItem(character, 'leftHand');

        if (this.isTwoHandedEquipmentItem(item)) {
            return {
                targetSlot: 'rightHand',
                slotsToClear: ['rightHand', 'leftHand']
            };
        }

        if (this.isTwoHandedEquipmentItem(rightHandItem)) {
            return {
                targetSlot: 'rightHand',
                slotsToClear: ['rightHand']
            };
        }

        if (!rightHandItem) {
            return {
                targetSlot: 'rightHand',
                slotsToClear: []
            };
        }

        if (!leftHandItem) {
            return {
                targetSlot: 'leftHand',
                slotsToClear: []
            };
        }

        return {
            targetSlot: 'rightHand',
            slotsToClear: ['rightHand']
        };
    }

    getCharacterPrimaryAttackAbility(character) {
        return this.getCharacterMeleeAttackAbility(character) || this.getCharacterRangedAttackAbility(character);
    }

    getCharacterPrimaryAttackDamage(character) {
        return this.getCharacterMeleeAttackDamage(character);
    }

    getCharacterMeleeAttackAbility(character) {
        const abilities = Array.isArray(character?.abilities) ? character.abilities : [];
        const meleeAbility = abilities.find((ability) => ability.type === 'attack' && this.getEffectiveAbilityRange(character, ability) <= 1) || null;
        return this.canCharacterUseAttackAbility(character, meleeAbility) ? meleeAbility : null;
    }

    getCharacterRangedAttackAbility(character) {
        const abilities = Array.isArray(character?.abilities) ? character.abilities : [];
        const rangedAbility = abilities.find((ability) => ability.type === 'attack' && this.getEffectiveAbilityRange(character, ability) > 1) || null;
        return this.canCharacterUseAttackAbility(character, rangedAbility) ? rangedAbility : null;
    }

    getCharacterMeleeAttackDamage(character) {
        const meleeAbility = this.getCharacterMeleeAttackAbility(character);
        if (meleeAbility) {
            return this.getEffectiveAbilityDamage(character, meleeAbility);
        }

        return 0;
    }

    getCharacterRangedAttackDamage(character) {
        const rangedAbility = this.getCharacterRangedAttackAbility(character);
        if (rangedAbility) {
            return this.getEffectiveAbilityDamage(character, rangedAbility);
        }

        return 0;
    }

    getCharacterEquipmentBonusSummary(character) {
        const summary = {
            armorClass: 0,
            attackDamage: 0,
            attackRange: 0,
            spellDamage: 0,
            healingBonus: 0,
            strength: 0
        };

        ['head', 'body', 'rightHand', 'leftHand', 'legs', 'feet', 'neck'].forEach((slotKey) => {
            const item = this.getCharacterEquippedItem(character, slotKey);
            if (!item) {
                return;
            }

            summary.armorClass += item.modifiers?.armorClass ?? 0;

            if (slotKey === 'rightHand' || slotKey === 'leftHand') {
                summary.attackDamage += item.modifiers?.attackDamage ?? 0;
                summary.attackRange += item.modifiers?.attackRange ?? 0;
            }

            if (slotKey === 'neck') {
                summary.spellDamage += item.modifiers?.spellDamage ?? 0;
            }

            if (slotKey === 'head') {
                summary.healingBonus += item.modifiers?.healingBonus ?? 0;
            }

            summary.strength += item.modifiers?.strength ?? 0;
        });

        return summary;
    }

    getCharacterStrengthDamageBonus(character) {
        const totalStrength = (character?.strength ?? 10) + this.getCharacterEquipmentBonusSummary(character).strength;
        return Math.floor(Math.max(0, totalStrength - 10) / 2);
    }

    getCharacterDexterityDamageBonus(character) {
        return Math.floor(Math.max(0, (character?.dexterity ?? 10) - 10) / 2);
    }

    getCharacterIntelligenceDamageBonus(character) {
        return Math.floor(Math.max(0, (character?.intelligence ?? 10) - 10) / 2);
    }

    getCharacterWisdomHealingBonus(character) {
        return Math.floor(Math.max(0, (character?.wisdom ?? 10) - 10) / 2);
    }

    getCharacterEffectBonus(character, bonusKey) {
        const effects = Array.isArray(character?.activeEffects) ? character.activeEffects : [];
        return effects.reduce((total, effect) => {
            if (!effect || effect.roundsRemaining !== undefined && effect.roundsRemaining <= 0) {
                return total;
            }

            const directBonus = effect[bonusKey];
            const modifierBonus = effect.modifiers?.[bonusKey];
            return total + (directBonus ?? modifierBonus ?? 0);
        }, 0);
    }

    getCharacterArmorClass(character) {
        if (!character) {
            return 0;
        }

        const equipmentArmorClassBonus = this.getCharacterEquipmentBonusSummary(character).armorClass;
        const effectArmorClassBonus =
            this.getCharacterEffectBonus(character, 'armorClass')
            + this.getCharacterEffectBonus(character, 'acBonus')
            + this.getCharacterEffectBonus(character, 'armorClassBonus');

        return Math.max(
            0,
            (character.armorClass ?? 0)
            + equipmentArmorClassBonus
            + effectArmorClassBonus
        );
    }

    getEffectiveArmorClassValue(character) {
        if (!character) {
            return 0;
        }

        return typeof this.getCharacterArmorClass === 'function'
            ? this.getCharacterArmorClass(character)
            : (character.armorClass ?? 0);
    }

    getCharacterEquipmentSummaryText(character) {
        const summary = this.getCharacterEquipmentBonusSummary(character);
        const parts = [];

        if (summary.armorClass > 0) {
            parts.push(`AC +${summary.armorClass}`);
        }
        if (summary.attackDamage > 0) {
            parts.push(`DMG +${summary.attackDamage}`);
        }
        if (summary.attackRange > 0) {
            parts.push(`Range +${summary.attackRange}`);
        }
        if (summary.spellDamage > 0) {
            parts.push(`Spell +${summary.spellDamage}`);
        }
        if (summary.healingBonus > 0) {
            parts.push(`Heal +${summary.healingBonus}`);
        }
        if (summary.strength > 0) {
            parts.push(`STR +${summary.strength}`);
        }

        return parts.length > 0 ? parts.join(' • ') : 'No equipment bonuses';
    }

    getCharacterSpellDamageBonus(character) {
        return this.getCharacterIntelligenceDamageBonus(character)
            + this.getCharacterEquipmentBonusSummary(character).spellDamage
            + this.getCharacterEffectBonus(character, 'spellDamage');
    }

    getCharacterHealingBonus(character) {
        return this.getCharacterWisdomHealingBonus(character)
            + this.getCharacterEquipmentBonusSummary(character).healingBonus
            + this.getCharacterEffectBonus(character, 'healingBonus');
    }

    getAbilityEquipmentOverride(character, ability) {
        if (!character || !ability) {
            return null;
        }

        const weaponItem = this.getCharacterWeaponItem(character, ability);
        if (!weaponItem || weaponItem.appliesToAbilityId !== ability.id) {
            return null;
        }

        return {
            damage: weaponItem.modifiers?.attackDamage,
            range: weaponItem.modifiers?.attackRange
        };
    }

    getEffectiveAbilityRange(character, ability) {
        const override = this.getAbilityEquipmentOverride(character, ability);
        return override?.range ?? (ability?.range ?? 1);
    }

    getEffectiveAbilityDamage(character, ability) {
        const override = this.getAbilityEquipmentOverride(character, ability);
        const strengthBonus = this.getCharacterStrengthDamageBonus(character);
        const dexterityBonus = this.getCharacterDexterityDamageBonus(character);
        const intelligenceBonus = this.getCharacterIntelligenceDamageBonus(character);

        if (ability?.type === 'spell' || ability?.id === 'magic-missile') {
            const storedDamage = ability?.damage ?? 0;
            const baseDamage = storedDamage > 0 ? Math.max(0, storedDamage - intelligenceBonus) : 0;
            return Math.max(baseDamage, override?.damage ?? 0) + this.getCharacterSpellDamageBonus(character);
        }

        if (ability?.type === 'attack') {
            if (this.getEffectiveAbilityRange(character, ability) > 1) {
                const storedDamage = ability?.damage ?? 0;
                const baseDamage = storedDamage > 0 ? Math.max(0, storedDamage - dexterityBonus) : 0;
                return Math.max(baseDamage, override?.damage ?? 0) + dexterityBonus;
            }

            return this.getCharacterMeleeWeaponDamage(character, ability) + this.getCharacterMeleeDamageBonus(character);
        }

        return override?.damage ?? (ability?.damage ?? 0);
    }

    getEffectiveAbilityHealAmount(character, ability) {
        const baseHeal = ability?.healAmount ?? 0;
        const wisdomBonus = this.getCharacterWisdomHealingBonus(character);
        const healBase = baseHeal > 0 ? Math.max(0, baseHeal - wisdomBonus) : 0;
        return healBase + this.getCharacterHealingBonus(character);
    }

    registerLootDropAtCell(gridX, gridY, loot, options = {}) {
        if (!loot) {
            return;
        }

        const cellKey = this.getCellKey(gridX, gridY);
        const existingDrop = this.lootDropsByCell.get(cellKey);
        const goldToAdd = Math.max(0, Math.floor(loot.gold ?? 0));
        const equipmentToAdd = Array.isArray(loot.equipmentDrops)
            ? loot.equipmentDrops
                .map((item) => this.ensureEquipmentItemInstance(item))
                .filter((item) => item)
            : [];
        if (goldToAdd <= 0 && equipmentToAdd.length === 0 && !existingDrop) {
            return;
        }

        if (existingDrop) {
            existingDrop.gold = (existingDrop.gold ?? 0) + goldToAdd;
            if (!Array.isArray(existingDrop.equipmentDrops)) {
                existingDrop.equipmentDrops = [];
            }
            existingDrop.equipmentDrops.push(...equipmentToAdd);
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
            equipmentDrops: equipmentToAdd,
            sources: options.sourceLabel ? [options.sourceLabel] : [],
            sourceType: options.sourceType || 'enemy',
            containerName: options.containerName || 'Loot Bag'
        });
    }

    navigatePartyToCell(targetX, targetY) {
        const partyOrder = this.getExplorationPartyOrder();
        const lead = partyOrder[0];
        if (!lead) {
            return;
        }

        if (targetX < 0 || targetX >= this.gridWidth || targetY < 0 || targetY >= this.gridHeight) {
            return;
        }
        if (this.isObstacle(targetX, targetY)) {
            return;
        }

        // BFS from lead position to target
        const start = { x: lead.gridX, y: lead.gridY };
        if (start.x === targetX && start.y === targetY) {
            return;
        }

        const visited = new Set();
        const queue = [{ x: start.x, y: start.y, path: [] }];
        visited.add(`${start.x},${start.y}`);

        const dirs = [
            { dx: 0, dy: -1, key: 'W' },
            { dx: 0, dy: 1, key: 'S' },
            { dx: -1, dy: 0, key: 'A' },
            { dx: 1, dy: 0, key: 'D' }
        ];

        while (queue.length > 0) {
            const current = queue.shift();
            for (const dir of dirs) {
                const nx = current.x + dir.dx;
                const ny = current.y + dir.dy;
                const cellKey = `${nx},${ny}`;

                if (nx < 0 || nx >= this.gridWidth || ny < 0 || ny >= this.gridHeight) {
                    continue;
                }
                if (visited.has(cellKey) || this.isObstacle(nx, ny)) {
                    continue;
                }
                // Block on non-party living characters (enemies)
                const blockedByOther = this.characters.some((c) =>
                    !c.isDead &&
                    !c.removedFromScene &&
                    !partyOrder.includes(c) &&
                    c.gridX === nx &&
                    c.gridY === ny
                );
                if (blockedByOther) {
                    continue;
                }

                const newPath = [...current.path, dir.key];
                if (nx === targetX && ny === targetY) {
                    this.explorationClickMoveQueue = newPath;
                    this.explorationClickMoveTimer = 0;
                    return;
                }
                visited.add(cellKey);
                queue.push({ x: nx, y: ny, path: newPath });
            }
        }
        // No path found — clear any existing queue
        this.explorationClickMoveQueue = [];
    }

    getChargeDestination(character, ability, targetX, targetY) {
        if (!character || !ability) {
            return null;
        }

        if (targetX < 0 || targetX >= this.gridWidth || targetY < 0 || targetY >= this.gridHeight) {
            return null;
        }

        if (this.isObstacle(targetX, targetY) || this.isOccupied(targetX, targetY, character)) {
            return null;
        }

        const chargeRange = this.getEffectiveAbilityRange(character, ability);
        return this.getReachablePositions(character, chargeRange).find((position) =>
            position.steps > 0 && position.x === targetX && position.y === targetY
        ) || null;
    }

    handleMovement(key) {
        if (this.gameMode === 'exploration') {
            this.explorationClickMoveQueue = [];
            this.movePartyInExploration(key);
            return;
        }

        const activeCharacter = this.getActiveTurnCharacter();
        if (
            this.isGameOver ||
            !this.isPlayerTurn() ||
            !activeCharacter ||
            activeCharacter.isDead ||
            this.isCombatActionPending(activeCharacter) ||
            this.getCharacterMovementBudget(activeCharacter) <= 0
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

        if (!this.consumeCharacterMovement(activeCharacter, 1)) {
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

        if (this.shouldEndCurrentTurn(activeCharacter)) {
            this.endCurrentTurn();
        }
    }

    getActionContext(character, options = {}) {
        const {
            combatOnly = false,
            requirePlayerInExploration = true,
            requiredActionCost = null,
            blockPendingInCombat = true
        } = options;

        const inCombat = this.gameMode === 'combat';
        const inExploration = this.gameMode === 'exploration';
        if (!inCombat && !inExploration) {
            return null;
        }

        if (combatOnly && !inCombat) {
            return null;
        }

        if (!character || character.isDead) {
            return null;
        }

        if (inExploration && requirePlayerInExploration && character.team !== 'player') {
            return null;
        }

        if (inCombat) {
            if (blockPendingInCombat && this.isCombatActionPending()) {
                return null;
            }

            const activeCharacter = this.getActiveTurnCharacter();
            const activeCharacterId = this.getCharacterTurnIdentity(activeCharacter);
            const characterId = this.getCharacterTurnIdentity(character);
            if (!characterId || activeCharacterId !== characterId) {
                return null;
            }

            const normalizedCost = requiredActionCost === null
                ? null
                : Math.max(0, Math.floor(requiredActionCost));
            if (normalizedCost !== null && (character.actionsRemaining ?? 0) < normalizedCost) {
                return null;
            }
        }

        return {
            isExploration: inExploration,
            isCombat: inCombat
        };
    }

    resolveAbilityById(character, abilityId, providedAbility = null) {
        if (providedAbility?.id === abilityId) {
            return providedAbility;
        }

        return window.CharacterData?.getCharacterActionById(character, abilityId) || null;
    }

    isAbilityTargetWithinRangeAndLos(caster, ability, targetX, targetY) {
        if (!caster || !ability || !Number.isFinite(targetX) || !Number.isFinite(targetY)) {
            return false;
        }

        const range = this.getEffectiveAbilityRange(caster, ability);
        const distance = this.getAttackDistanceBetweenPositions(caster.gridX, caster.gridY, targetX, targetY);
        if (distance > range) {
            return false;
        }

        if (this.requiresLineOfSight(ability, caster) && !this.hasLineOfSightBetweenCells(caster.gridX, caster.gridY, targetX, targetY)) {
            return false;
        }

        return true;
    }

    spendActionAndMagic(character, context, options = {}) {
        if (!character || !context) {
            return;
        }

        const actionCost = Math.max(0, Math.floor(options.actionCost ?? character.attackCost ?? 0));
        const magicCost = Math.max(0, Math.floor(options.magicCost ?? 0));

        character.magicPoints = Math.max(0, (character.magicPoints ?? 0) - magicCost);

        if (!context.isExploration) {
            character.actionsRemaining = Math.max(0, (character.actionsRemaining ?? 0) - actionCost);
        }
    }

    finalizeActionUsage(character, context) {
        if (!character || !context || context.isExploration) {
            return;
        }

        if (this.shouldEndCurrentTurn(character)) {
            this.endCurrentTurn();
        }
    }

    forEachTargetWithinRange(caster, targets, range, callback) {
        if (!caster || !Array.isArray(targets) || !callback) {
            return 0;
        }

        const normalizedRange = Math.max(0, Math.floor(range ?? 0));
        let affectedCount = 0;

        targets.forEach((target) => {
            if (!target || target.isDead || target.removedFromScene) {
                return;
            }

            const dx = Math.abs((target.gridX ?? 0) - (caster.gridX ?? 0));
            const dy = Math.abs((target.gridY ?? 0) - (caster.gridY ?? 0));
            if (dx > normalizedRange || dy > normalizedRange) {
                return;
            }

            callback(target);
            affectedCount += 1;
        });

        return affectedCount;
    }

    castCharge(caster, targetCell, chargeAbility = null) {
        if (!targetCell) {
            return false;
        }

        const ability = this.resolveAbilityById(caster, 'charge', chargeAbility);
        if (!ability) {
            return false;
        }

        const actionCost = this.getAbilityActionCost(caster, ability);
        const context = this.getActionContext(caster, {
            combatOnly: true,
            requiredActionCost: actionCost
        });
        if (!context) {
            return false;
        }

        const destination = this.getChargeDestination(caster, ability, targetCell.gridX, targetCell.gridY);
        if (!destination) {
            return false;
        }

        const finalStep = destination.path[destination.path.length - 1] || null;
        if (finalStep?.facing) {
            this.updateCharacterFacing(caster, finalStep.facing);
        }

        this.spendActionAndMagic(caster, context, { actionCost });
        this.beginPendingCombatAction(caster);

        const startPos = this.getCharacterWorldPos(caster);
        caster.gridX = destination.x;
        caster.gridY = destination.y;
        const endPos = this.getCharacterWorldPos(caster);

        this.spawnChargeBurstEffect(startPos, caster.accentColor);
        this.updateCharacterPosition(caster, {
            forceAnimation: true,
            durationMs: 140,
            movementMode: 'charge',
            onComplete: () => {
                this.spawnChargeBurstEffect(endPos, caster.accentColor);
                this.tryAutoOpenLootFromCharacterCell(caster);
                this.finishPendingCombatAction(caster);
            }
        });
        this.focusCameraOnCharacter(caster, 650);
        this.appendCombatLogEntry(
            `${caster.name} charges ${destination.steps} ${destination.steps === 1 ? 'cell' : 'cells'} forward.`,
            caster.accentColor
        );

        this.startAbilityCooldown(ability);
        return true;
    }

    // --- Combat ---

    characterAttack(attacker, target, attackAbility = null) {
        if (!target || target.isDead) {
            return false;
        }

        const attackActionCost = Math.max(0, Math.floor(attacker?.attackCost ?? 0));
        const context = this.getActionContext(attacker, {
            requiredActionCost: attackActionCost
        });
        if (!context || attacker.team === target.team) {
            return false;
        }

        const resolvedAttackAbility = attackAbility && attackAbility.type === 'attack'
            ? attackAbility
            : (window.CharacterData?.getCharacterActionList(attacker) || []).find((ability) => ability.id === attacker.selectedAbilityId && ability.type === 'attack') || null;

        if (!this.canCharacterUseAttackAbility(attacker, resolvedAttackAbility)) {
            return false;
        }

        const attackRange = this.getEffectiveAbilityRange(attacker, resolvedAttackAbility);
        const baseDamage = this.getEffectiveAbilityDamage(attacker, resolvedAttackAbility);
        const damageKind = attackRange > 1 ? 'ranged' : 'melee';
        const sourceLabel = this.getCharacterAttackSourceLabel(attacker, resolvedAttackAbility);

        if (!this.isAbilityTargetWithinRangeAndLos(attacker, resolvedAttackAbility, target.gridX, target.gridY)) {
            return false;
        }

        this.faceCharacterToward(attacker, target);
        const projectileAnimation = this.getAbilityProjectileAnimation(resolvedAttackAbility);
        const resolvesOnImpact = this.doesAbilityResolveOnImpact(resolvedAttackAbility);

        this.spendActionAndMagic(attacker, context, { actionCost: attackActionCost });

        if (!context.isExploration) {
            if (resolvesOnImpact) {
                this.beginPendingCombatAction(attacker);
            }
        }

        if (projectileAnimation === 'arrow') {
            const attackerPos = this.getCharacterWorldPos(attacker);
            const targetPos = this.getCharacterWorldPos(target);
            this.spawnArrowProjectile(attackerPos, targetPos, () => {
                this.spawnArrowImpactEffect(targetPos);
                const damageDealt = this.applyPhysicalAttackDamage(target, baseDamage, attacker);
                this.appendCombatLogEntry(
                    `${attacker.name} attacks ${target.name} for ${damageDealt} ${damageKind} dmg with ${sourceLabel}.`,
                    attacker.accentColor
                );
                this.applyOnHitAttackEffects(attacker, target, resolvedAttackAbility, damageDealt);
                if (!context.isExploration) {
                    this.finishPendingCombatAction(attacker);
                }
            });
        } else {
            const damageDealt = this.applyPhysicalAttackDamage(target, baseDamage, attacker);

            this.appendCombatLogEntry(
                `${attacker.name} attacks ${target.name} for ${damageDealt} ${damageKind} dmg with ${sourceLabel}.`,
                attacker.accentColor
            );
            this.applyOnHitAttackEffects(attacker, target, resolvedAttackAbility, damageDealt);
            this.finalizeActionUsage(attacker, context);
        }

        return true;
    }

    applyPhysicalAttackDamage(target, baseDamage, attacker = null) {
        if (!target || target.isDead) {
            return;
        }

        const physicalDamage = Math.max(0, baseDamage - this.getCharacterArmorClass(target));
        if (physicalDamage > 0) {
            this.removeSleepEffect(target);
        }
        target.hitPoints -= physicalDamage;
        this.playHitAnimation(target);

        if (target.hitPoints <= 0) {
            target.hitPoints = 0;
            this.markCharacterDead(target, attacker);
        }

        return physicalDamage;
    }

    applyOnHitAttackEffects(attacker, target, attackAbility, damageDealt = 0) {
        if (!attacker || !target || target.isDead || damageDealt <= 0 || attackAbility?.id !== 'venomous-bite') {
            return false;
        }

        if (Math.random() >= 0.5) {
            return false;
        }

        this.applyPoisonEffect(target);
        this.appendCombatLogEntry(`${target.name} is poisoned.`, '#7bcf84');
        return true;
    }

    castPoisonDart(caster, target, poisonDartAbility = null) {
        if (!target || target.isDead || caster?.team === target.team) {
            return false;
        }

        const context = this.getActionContext(caster, {
            requiredActionCost: Math.max(0, Math.floor(caster?.attackCost ?? 0))
        });
        if (!context) {
            return false;
        }

        const ability = this.resolveAbilityById(caster, 'poison-dart', poisonDartAbility);
        if (!ability) {
            return false;
        }

        const mpCost = Math.max(0, Math.floor(ability.mpCost ?? 0));
        if ((caster.magicPoints ?? 0) < mpCost) {
            return false;
        }

        if (!this.isAbilityTargetWithinRangeAndLos(caster, ability, target.gridX, target.gridY)) {
            return false;
        }

        this.faceCharacterToward(caster, target);
        this.spendActionAndMagic(caster, context, {
            actionCost: caster.attackCost,
            magicCost: mpCost
        });
        this.applyPoisonEffect(target);

        this.appendCombatLogEntry(
            `${caster.name} casts ${ability.name} on ${target.name}, poisoning them.`,
            caster.accentColor
        );

        if (!context.isExploration) {
            this.finalizeActionUsage(caster, context);
        } else {
            this.beginCombatWithEnemyCharacter(target);
        }

        return true;
    }

    castCallOfTheWolf(caster, summonAbility = null) {
        const ability = this.resolveAbilityById(caster, 'call-of-the-wolf', summonAbility);
        if (!ability) {
            return false;
        }

        const actionCost = this.getAbilityActionCost(caster, ability);
        const context = this.getActionContext(caster, {
            combatOnly: true,
            requiredActionCost: actionCost
        });
        if (!context) {
            return false;
        }

        const mpCost = Math.max(0, Math.floor(ability.mpCost ?? 0));
        if ((caster.magicPoints ?? 0) < mpCost) {
            return false;
        }

        const existingWolf = (this.summonedAllies ?? []).find((ally) => ally?.isSummonedWolf && !ally.isDead && !ally.removedFromScene);
        if (existingWolf) {
            this.showToast('A wolf is already fighting with the party.', '#b9c68b', 2200);
            return false;
        }

        const summonCell = this.findSummonPlacementNearCharacter(caster, 1, 2);
        if (!summonCell) {
            this.showToast('No room to summon a wolf here.', '#b9c68b', 2200);
            return false;
        }

        const wolf = this.createWolfCompanion(caster);
        wolf.gridX = summonCell.x;
        wolf.gridY = summonCell.y;
        this.summonedAllies.push(wolf);
        this.characters.push(wolf);
        this.setupCharacterSprite(
            wolf,
            this.createSpriteTexture(wolf.spriteFrame),
            wolf.pointerColor
        );

        this.spendActionAndMagic(caster, context, {
            actionCost,
            magicCost: mpCost
        });

        this.refreshCombatTurnOrder(true);
        this.updateTurnOrderQueue(this.getActiveTurnCharacter());

        this.appendCombatLogEntry(
            `${caster.name} casts ${ability.name} and summons a wolf companion.`,
            caster.accentColor
        );

        this.finalizeActionUsage(caster, context);
        this.startAbilityCooldown(ability);
        return true;
    }

    castRaiseUndead(caster, raiseUndeadAbility = null) {
        const ability = this.resolveAbilityById(caster, 'raise-undead', raiseUndeadAbility);
        if (!ability) {
            return false;
        }

        const actionCost = this.getAbilityActionCost(caster, ability);
        const context = this.getActionContext(caster, {
            combatOnly: true,
            requiredActionCost: actionCost
        });
        if (!context) {
            return false;
        }

        const mpCost = Math.max(0, Math.floor(ability.mpCost ?? 0));
        if ((caster.magicPoints ?? 0) < mpCost) {
            return false;
        }

        const summonCount = Math.max(1, Math.floor(ability.summonCount ?? 2));
        const summonCells = this.findSummonPlacementsTowardOpponents(caster, summonCount, 1, 3);
        if (summonCells.length < summonCount) {
            return false;
        }

        const enemyGroup = this.getEnemyGroupById(caster.encounterGroupId);
        const summoned = summonCells.map((cell) => {
            const skeleton = this.createSummonedSkeletonWarrior(caster);
            skeleton.gridX = cell.x;
            skeleton.gridY = cell.y;
            skeleton.encounterGroupId = caster.encounterGroupId || null;
            return skeleton;
        });

        summoned.forEach((skeleton) => {
            this.summonedEnemies.push(skeleton);
            this.characters.push(skeleton);
            this.aiParty.push(skeleton);
            if (enemyGroup?.members) {
                enemyGroup.members.push(skeleton);
            }

            this.setupCharacterSprite(
                skeleton,
                this.createSpriteTexture(skeleton.spriteFrame),
                skeleton.pointerColor
            );
        });

        this.spendActionAndMagic(caster, context, {
            actionCost,
            magicCost: mpCost
        });

        this.refreshCombatTurnOrder(true);
        this.updateTurnOrderQueue(this.getActiveTurnCharacter());

        this.appendCombatLogEntry(
            `${caster.name} casts ${ability.name} and raises ${summoned.length} skeleton warriors.`,
            caster.accentColor
        );

        this.finalizeActionUsage(caster, context);
        this.startAbilityCooldown(ability);
        return true;
    }

    applyPoisonEffect(target, damagePerRound = 3, roundsRemaining = 3) {
        if (!target || target.isDead) {
            return false;
        }

        if (!Array.isArray(target.activeEffects)) {
            target.activeEffects = [];
        }

        const normalizedDamagePerRound = Math.max(0, Math.floor(Number(damagePerRound) || 0));
        const normalizedRoundsRemaining = Math.max(1, Math.floor(Number(roundsRemaining) || 0));

        const existing = target.activeEffects.find((effect) => effect.type === 'poison');
        if (existing) {
            existing.damagePerRound = normalizedDamagePerRound;
            existing.roundsRemaining = normalizedRoundsRemaining;
            existing.poisonStartsNextTurn = true;
            return true;
        }

        target.activeEffects.push({
            type: 'poison',
            damagePerRound: normalizedDamagePerRound,
            roundsRemaining: normalizedRoundsRemaining,
            poisonStartsNextTurn: true
        });

        return true;
    }

    getCharacterTraits(character) {
        const traits = new Set();
        if (!character) {
            return traits;
        }

        const race = String(character.race || '').toLowerCase();
        const characterId = String(character.id || '').toLowerCase();
        if (race) {
            traits.add(race);
        }
        if (race === 'undead' || character.isSummonedUndead || characterId.includes('skeleton') || characterId.includes('zombie') || characterId.includes('ghoul') || characterId.includes('specter') || characterId.includes('spectre')) {
            traits.add('undead');
        }
        if (characterId.includes('mage') || characterId.includes('necromancer') || (character.maxMagicPoints ?? 0) > 0) {
            traits.add('caster');
        }
        if (characterId.includes('archer') || this.getAbilityForCharacter?.(character, 'bow-shot')) {
            traits.add('ranged');
        }
        if (characterId.includes('brute') || characterId.includes('warrior') || characterId.includes('zombie')) {
            traits.add('frontliner');
        }
        if (characterId.includes('spider')) {
            traits.add('spider');
        }
        if (race === 'wolf' || character.isSummonedWolf) {
            traits.add('beast');
        }

        return traits;
    }

    hasCharacterTrait(character, trait) {
        return this.getCharacterTraits(character).has(String(trait || '').toLowerCase());
    }

    isCharacterImmuneToEffect(character, effectType) {
        const normalizedEffectType = String(effectType || '').toLowerCase();
        if (!character || !normalizedEffectType) {
            return false;
        }

        return normalizedEffectType === 'sleep' && this.hasCharacterTrait(character, 'undead');
    }

    applyStatusEffect(target, effectConfig) {
        if (!target || target.isDead || !effectConfig?.type) {
            return { applied: false, immune: false };
        }

        if (this.isCharacterImmuneToEffect(target, effectConfig.type)) {
            return { applied: false, immune: true };
        }

        if (!Array.isArray(target.activeEffects)) {
            target.activeEffects = [];
        }

        const duration = Math.max(1, Math.floor(effectConfig.roundsRemaining ?? effectConfig.duration ?? 1));
        const existing = target.activeEffects.find((effect) => effect.type === effectConfig.type);
        if (existing) {
            Object.assign(existing, effectConfig, {
                roundsRemaining: Math.max(existing.roundsRemaining ?? 0, duration)
            });
        } else {
            target.activeEffects.push({ ...effectConfig, roundsRemaining: duration });
        }

        return { applied: true, immune: false };
    }

    removeSleepEffect(target) {
        if (!target || !Array.isArray(target.activeEffects) || target.activeEffects.length === 0) {
            return false;
        }

        const remainingEffects = target.activeEffects.filter((effect) => effect?.type !== 'sleep');
        const removed = remainingEffects.length !== target.activeEffects.length;
        if (removed) {
            target.activeEffects = remainingEffects;
            this.appendCombatLogEntry(`${target.name} wakes up.`, '#a9c4de');
        }

        return removed;
    }

    castMagicMissile(caster, target) {
        const ability = this.resolveAbilityById(caster, 'magic-missile');
        if (!ability) {
            return false;
        }

        const didCast = this.castDamageSpell(caster, target, ability);
        if (didCast) {
            this.startAbilityCooldown(ability);
        }
        return didCast;
    }

    castSleep(caster, targetCell, sleepAbility = null) {
        if (!targetCell) {
            return false;
        }

        const context = this.getActionContext(caster, {
            requiredActionCost: Math.max(0, Math.floor(caster?.attackCost ?? 0))
        });
        if (!context) {
            return false;
        }

        const ability = this.resolveAbilityById(caster, 'sleep', sleepAbility);
        if (!ability) {
            return false;
        }

        const mpCost = Math.max(0, Math.floor(ability.mpCost ?? 0));
        if ((caster.magicPoints ?? 0) < mpCost) {
            return false;
        }

        if (!this.isAbilityTargetWithinRangeAndLos(caster, ability, targetCell.gridX, targetCell.gridY)) {
            return false;
        }

        const radius = Math.max(0, Math.floor(ability.radius ?? 1));
        const duration = Math.max(1, Math.floor(ability.duration ?? 2));
        const allNearbyEnemies = this.characters.filter((candidate) =>
            candidate.team !== caster.team &&
            !candidate.isDead &&
            !candidate.removedFromScene &&
            this.getAttackDistanceBetweenPositions(targetCell.gridX, targetCell.gridY, candidate.gridX, candidate.gridY) <= radius
        );

        if (allNearbyEnemies.length === 0) {
            return false;
        }

        this.spendActionAndMagic(caster, context, {
            actionCost: caster.attackCost,
            magicCost: mpCost
        });
        const affectedEnemies = [];
        const immuneEnemies = [];
        allNearbyEnemies.forEach((enemy) => {
            const result = this.applyStatusEffect(enemy, { type: 'sleep', roundsRemaining: duration });
            if (result.immune) {
                immuneEnemies.push(enemy);
                return;
            }

            if (result.applied) {
                affectedEnemies.push(enemy);
                this.spawnHealEffect(this.getCharacterWorldPos(caster), this.getCharacterWorldPos(enemy));
            }
        });

        let logMessage = `${caster.name} casts ${ability.name}`;
        if (affectedEnemies.length > 0) {
            logMessage += `, sending ${affectedEnemies.length} ${affectedEnemies.length === 1 ? 'enemy' : 'enemies'} to sleep`;
        }
        if (immuneEnemies.length > 0) {
            logMessage += affectedEnemies.length > 0 ? '. ' : ' ';
            logMessage += `${immuneEnemies.length} ${immuneEnemies.length === 1 ? 'undead is' : 'undead are'} immune to sleep.`;
        }
        logMessage += affectedEnemies.length > 0 && immuneEnemies.length === 0 ? '.' : '';

        this.appendCombatLogEntry(logMessage, caster.accentColor);

        if (!context.isExploration) {
            this.finalizeActionUsage(caster, context);
        } else {
            this.beginCombatWithEnemyCharacter(affectedEnemies[0] || immuneEnemies[0] || allNearbyEnemies[0]);
        }

        this.startAbilityCooldown(ability);
        return true;
    }

    castBattleShout(caster) {
        const context = this.getActionContext(caster, {
            requiredActionCost: Math.max(0, Math.floor(caster?.attackCost ?? 0))
        });
        if (!context) {
            return false;
        }

        const ability = window.CharacterData?.getCharacterActionById(caster, 'battle-shout');
        if (!ability) {
            return false;
        }

        const damageBonus = ability.damageBonus ?? 1;
        const duration = ability.duration ?? 2;
        const range = ability.range ?? 3;

        const allies = this.getLivingCharacters(this.playerParty);
        const affectedCount = this.forEachTargetWithinRange(caster, allies, range, (ally) => {
            const existing = ally.activeEffects.find((e) => e.type === 'battle-shout');
            if (existing) {
                existing.damageBonus = Math.max(existing.damageBonus ?? 0, damageBonus);
                existing.roundsRemaining = duration;
            } else {
                ally.activeEffects.push({ type: 'battle-shout', damageBonus, roundsRemaining: duration });
            }

            const pos = this.getCharacterWorldPos(ally);
            this.spawnBattleShoutEffect(pos);
        });

        this.appendCombatLogEntry(
            `${caster.name} uses ${ability.name} and grants +${damageBonus} dmg to ${affectedCount} ${affectedCount === 1 ? 'ally' : 'allies'}.`,
            caster.accentColor
        );

        this.spendActionAndMagic(caster, context, { actionCost: caster.attackCost });
        this.finalizeActionUsage(caster, context);
        this.startAbilityCooldown(ability);
        return true;
    }

    castBlessing(caster) {
        const context = this.getActionContext(caster, {
            requiredActionCost: Math.max(0, Math.floor(caster?.attackCost ?? 0))
        });
        if (!context) {
            return false;
        }

        const ability = window.CharacterData?.getCharacterActionById(caster, 'blessing');
        if (!ability) {
            return false;
        }

        const mpCost = ability.mpCost ?? 0;
        if ((caster.magicPoints ?? 0) < mpCost) {
            return false;
        }

        const acBonus = ability.acBonus ?? ability.modifiers?.armorClass ?? 1;
        const duration = ability.duration ?? 3;
        const range = ability.range ?? 3;
        const casterPos = this.getCharacterWorldPos(caster);
        const affectedCount = this.forEachTargetWithinRange(caster, this.getLivingCharacters(this.playerParty), range, (ally) => {
            const existing = ally.activeEffects.find((effect) => effect.type === 'blessing');
            if (existing) {
                existing.acBonus = Math.max(existing.acBonus ?? 0, acBonus);
                existing.roundsRemaining = duration;
            } else {
                ally.activeEffects.push({ type: 'blessing', acBonus, roundsRemaining: duration });
            }

            this.spawnHealEffect(casterPos, this.getCharacterWorldPos(ally));
        });

        this.appendCombatLogEntry(
            `${caster.name} casts ${ability.name} and grants +${acBonus} AC to ${affectedCount} ${affectedCount === 1 ? 'ally' : 'allies'}.`,
            caster.accentColor
        );

        this.spendActionAndMagic(caster, context, {
            actionCost: caster.attackCost,
            magicCost: mpCost
        });
        this.finalizeActionUsage(caster, context);
        this.startAbilityCooldown(ability);
        return true;
    }

    castInflictPain(caster) {
        const context = this.getActionContext(caster, {
            requiredActionCost: Math.max(0, Math.floor(caster?.attackCost ?? 0))
        });
        if (!context) {
            return false;
        }

        const ability = window.CharacterData?.getCharacterActionById(caster, 'inflict-pain');
        if (!ability) {
            return false;
        }

        const mpCost = ability.mpCost ?? 0;
        if (caster.magicPoints < mpCost) {
            return false;
        }

        const damageBonus = ability.damageBonus ?? 1;
        const duration = ability.duration ?? 2;
        const range = ability.range ?? 2;
        const affectedCount = this.forEachTargetWithinRange(caster, this.getLivingGoblinAllies(), range, (ally) => {
            const pos = this.getCharacterWorldPos(ally);
            this.spawnInflictPainEffect(pos);

            const existing = ally.activeEffects.find((effect) => effect.type === 'inflict-pain');
            if (existing) {
                existing.damageBonus = Math.max(existing.damageBonus ?? 0, damageBonus);
                existing.roundsRemaining = Math.max(existing.roundsRemaining ?? 0, duration);
            } else {
                ally.activeEffects.push({ type: 'inflict-pain', damageBonus, roundsRemaining: duration });
            }
        });

        this.appendCombatLogEntry(
            `${caster.name} casts ${ability.name} and grants +${damageBonus} dmg to ${affectedCount} ${affectedCount === 1 ? 'ally' : 'allies'}.`,
            caster.accentColor
        );

        this.spendActionAndMagic(caster, context, {
            actionCost: caster.attackCost,
            magicCost: mpCost
        });
        this.finalizeActionUsage(caster, context);

        return true;
    }

    castHeal(caster, target, healAbility = null) {
        if (!target || target.isDead || caster?.team !== target.team) {
            return false;
        }

        const context = this.getActionContext(caster, {
            requiredActionCost: Math.max(0, Math.floor(caster?.attackCost ?? 0))
        });
        if (!context) {
            return false;
        }

        const ability = healAbility?.type === 'heal'
            ? healAbility
            : window.CharacterData?.getCharacterActionById(caster, caster.selectedAbilityId)
                || window.CharacterData?.getCharacterActionById(caster, 'lesser-heal')
                || window.CharacterData?.getCharacterActionById(caster, 'mend-flesh')
                || window.CharacterData?.getCharacterActionById(caster, 'heal')
                || window.CharacterData?.getCharacterActionById(caster, 'cure-poison');
        if (!ability) {
            return false;
        }

        if (ability.type !== 'heal') {
            return false;
        }

        const mpCost = Math.max(0, Math.floor(ability.mpCost ?? 0));
        if ((caster.magicPoints ?? 0) < mpCost) {
            return false;
        }

        if (!this.isAbilityTargetWithinRangeAndLos(caster, ability, target.gridX, target.gridY)) {
            return false;
        }

        if (caster !== target) {
            this.faceCharacterToward(caster, target);
        }

        const curedPoison = ability.curePoison ? this.curePoisonEffect(target) : false;
        const healAmount = ability.curePoison ? 0 : this.getEffectiveAbilityHealAmount(caster, ability);
        const restored = ability.curePoison
            ? 0
            : Math.min(healAmount, target.maxHitPoints - target.hitPoints);
        if (!ability.curePoison) {
            target.hitPoints = Math.min(target.maxHitPoints, target.hitPoints + healAmount);
        }
        this.spendActionAndMagic(caster, context, {
            actionCost: caster.attackCost,
            magicCost: mpCost
        });

        const resultParts = [];
        if (restored > 0) {
            resultParts.push(`${restored} healing`);
        }
        if (curedPoison) {
            resultParts.push('cures poison');
        }
        if (resultParts.length === 0) {
            resultParts.push('no effect');
        }

        this.appendCombatLogEntry(
            `${caster.name} casts ${ability.name} on ${target.name} for ${resultParts.join(' and ')}.`,
            caster.accentColor
        );

        const casterPos = this.getCharacterWorldPos(caster);
        const targetPos = this.getCharacterWorldPos(target);
        this.spawnHealEffect(casterPos, targetPos);

        this.finalizeActionUsage(caster, context);

        return true;
    }

    curePoisonEffect(target) {
        if (!target || !Array.isArray(target.activeEffects) || target.activeEffects.length === 0) {
            return false;
        }

        const remainingEffects = target.activeEffects.filter((effect) => effect?.type !== 'poison');
        const cured = remainingEffects.length !== target.activeEffects.length;
        target.activeEffects = remainingEffects;
        return cured;
    }

    // --- Death / Game State ---

    getTotalExperienceRequiredForLevel(level) {
        if (level <= 1) {
            return 0;
        }

        return 500 * Math.pow(2, level - 2);
    }

    getLevelUpBonusesForCharacter(character, newLevel = character?.level ?? 1) {
        if (!character || character.team !== 'player') {
            return null;
        }

        switch (character.id) {
            case 'warrior':
                return {
                    hp: 4,
                    strength: 2,
                    maxActionsPerTurn: newLevel === 3 ? 1 : 0,
                    unlockedAbilities: newLevel === 3 ? ['charge'] : []
                };
            case 'cleric':
                return {
                    hp: 3,
                    mp: 2,
                    wisdom: 1,
                    maxActionsPerTurn: newLevel === 3 ? 1 : 0,
                    unlockedSpells: newLevel === 3 ? ['blessing'] : []
                };
            case 'wizard':
                return {
                    hp: 2,
                    mp: 3,
                    intelligence: 1,
                    maxActionsPerTurn: newLevel === 3 ? 1 : 0,
                    unlockedSpells: newLevel === 3 ? ['sleep'] : []
                };
            case 'ranger':
                return {
                    hp: 3,
                    initiative: 1,
                    dexterity: 1,
                    maxActionsPerTurn: newLevel === 3 ? 1 : 0,
                    unlockedSpells: newLevel === 3 ? ['call-of-the-wolf'] : []
                };
            default:
                return null;
        }
    }

    applyLevelUpBonuses(character, newLevel) {
        const bonuses = this.getLevelUpBonusesForCharacter(character, newLevel);
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

        const actionGain = Math.max(0, Math.floor(bonuses.maxActionsPerTurn ?? 0));
        if (actionGain > 0) {
            character.maxActionsPerTurn = Math.max(0, Math.floor(character.maxActionsPerTurn ?? 0)) + actionGain;
            character.actionsRemaining = Math.max(0, Math.floor(character.actionsRemaining ?? 0)) + actionGain;
        }

        const unlockedAbilities = Array.isArray(bonuses.unlockedAbilities)
            ? bonuses.unlockedAbilities
                .map((abilityId) => window.GameData?.getAbilityTemplateById(abilityId))
                .filter(Boolean)
            : [];

        const unlockedSpells = Array.isArray(bonuses.unlockedSpells)
            ? bonuses.unlockedSpells
                .map((spellId) => window.GameData?.getSpellTemplateById(spellId))
                .filter(Boolean)
            : [];

        if (unlockedAbilities.length > 0) {
            character.abilities ||= [];
            unlockedAbilities.forEach((ability) => {
                if (!character.abilities.some((existing) => existing.id === ability.id)) {
                    character.abilities.push({ ...ability });
                }
            });
        }

        if (unlockedSpells.length > 0) {
            character.spells ||= [];
            unlockedSpells.forEach((spell) => {
                if (!character.spells.some((existing) => existing.id === spell.id)) {
                    character.spells.push({ ...spell });
                }
            });
        }

        const bonusParts = [];
        if (hpGain > 0) bonusParts.push(`+${hpGain} HP`);
        if (mpGain > 0) bonusParts.push(`+${mpGain} MP`);
        if ((bonuses.strength ?? 0) > 0) bonusParts.push(`+${bonuses.strength} STR`);
        if ((bonuses.wisdom ?? 0) > 0) bonusParts.push(`+${bonuses.wisdom} WIS`);
        if ((bonuses.intelligence ?? 0) > 0) bonusParts.push(`+${bonuses.intelligence} INT`);
        if ((bonuses.dexterity ?? 0) > 0) bonusParts.push(`+${bonuses.dexterity} DEX`);
        if ((bonuses.initiative ?? 0) > 0) bonusParts.push(`+${bonuses.initiative} Initiative`);
        if (actionGain > 0) bonusParts.push(`+${actionGain} Action`);
        unlockedAbilities.forEach((ability) => bonusParts.push(`Unlocked ${ability.name}`));
        unlockedSpells.forEach((spell) => bonusParts.push(`Unlocked ${spell.name}`));

        if (!Array.isArray(character.pendingLevelUpNotices)) {
            character.pendingLevelUpNotices = [];
        }
        character.pendingLevelUpNotices.push(`Level ${newLevel}: ${bonusParts.join(', ')}`);

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
        if (!defeatedEnemy || defeatedEnemy.team !== 'ai' || !this.isPartyAlignedCharacter(defeatedBy)) {
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
        if (!defeatedEnemy) {
            return null;
        }

        const configuredLoot = defeatedEnemy.configuredLoot || null;
        const configuredLootItemIds = Array.isArray(configuredLoot?.lootItemIds)
            ? configuredLoot.lootItemIds.filter((itemId) => typeof itemId === 'string' && itemId.trim().length > 0)
            : [];
        const configuredLootMode = configuredLoot?.lootMode === 'all' ? 'all' : 'random';
        if (configuredLoot && (configuredLootItemIds.length > 0 || Number.isFinite(configuredLoot.goldAmount))) {
            const hasConfiguredGold = Number.isFinite(configuredLoot.goldAmount);
            const gold = hasConfiguredGold
                ? Math.max(0, Math.floor(configuredLoot.goldAmount))
                : 0;
            let equipmentDrops = [];

            if (configuredLootItemIds.length > 0) {
                if (configuredLootMode === 'all') {
                    equipmentDrops = this.createEquipmentItemsFromIds(configuredLootItemIds);
                } else {
                    const roll = this.rollEquipmentLootItem(configuredLootItemIds);
                    if (roll) {
                        equipmentDrops = [roll];
                    }
                }
            }

            if (gold <= 0 && equipmentDrops.length === 0) {
                return null;
            }

            return {
                gold,
                equipmentDrops
            };
        }

        if (defeatedEnemy.race !== 'goblin') {
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

        const equipmentDrops = Array.isArray(drop.equipmentDrops) ? drop.equipmentDrops : [];
        equipmentDrops.forEach((item) => {
            const summary = this.getEquipmentItemModifierSummary(item);
            const isSpellScroll = item.type === 'spell-scroll';
            items.push({
                itemKey: `equipment:${item.instanceId}`,
                label: this.getEquipmentItemLabel(item),
                quantity: 1,
                accentColor: item.accentColor || '#d6cbb8',
                detail: isSpellScroll
                    ? (summary || 'Spell Scroll')
                    : summary
                    ? `${this.getEquipmentItemSlotLabel(item.slot)} • ${summary}`
                    : this.getEquipmentItemSlotLabel(item.slot)
            });
        });

        return items;
    }

    getSharedLootItems() {
        if (!this.sharedLootInventory) {
            this.sharedLootInventory = { gold: 0, drops: [], items: [] };
        }

        if (!Array.isArray(this.sharedLootInventory?.items)) {
            this.sharedLootInventory.items = [];
        }

        this.sharedLootInventory.items = this.sharedLootInventory.items
            .map((item) => this.ensureEquipmentItemInstance(item))
            .filter(Boolean);

        return this.sharedLootInventory.items;
    }

    maybeRenderActiveInventoryCharacter(character) {
        if (this.activeInventoryCharacter === character) {
            this.renderCharacterInventory();
        }
    }

    applyCharacterArmorFromEquipmentItem(character, item, direction = 1) {
        // AC is derived by getCharacterArmorClass from base stats, equipment, and effects.
        // Retained as a no-op for backward compatibility with older call sites.
        void character;
        void item;
        void direction;
    }

    isLootDropEmpty(drop) {
        if (!drop) {
            return true;
        }

        const hasGold = (drop.gold ?? 0) > 0;
        const hasEquipment = Array.isArray(drop.equipmentDrops) && drop.equipmentDrops.length > 0;
        return !hasGold && !hasEquipment;
    }

    takeEquipmentFromDrop(drop, instanceId) {
        if (!drop || !instanceId) {
            return null;
        }

        const equipmentDrops = Array.isArray(drop.equipmentDrops) ? drop.equipmentDrops : [];
        const equipmentIndex = equipmentDrops.findIndex((item) => item.instanceId === instanceId);
        if (equipmentIndex < 0) {
            return null;
        }

        const takenEquipment = this.ensureEquipmentItemInstance(equipmentDrops[equipmentIndex]);
        equipmentDrops.splice(equipmentIndex, 1);
        drop.equipmentDrops = equipmentDrops;
        return takenEquipment;
    }

    addItemToSharedInventory(itemKey, quantity) {
        if (!itemKey || quantity <= 0) {
            return;
        }

        if (itemKey === 'gold') {
            this.sharedLootInventory.gold += quantity;
        }
    }

    addEquipmentItemToSharedInventory(item) {
        const normalizedItem = this.ensureEquipmentItemInstance(item);
        if (!normalizedItem) {
            return;
        }

        const sharedItems = this.getSharedLootItems();
        sharedItems.unshift(normalizedItem);
        if (sharedItems.length > 80) {
            sharedItems.length = 80;
        }
    }

    getCharacterClassKey(character) {
        const id = String(character?.id || '').toLowerCase();
        const role = String(character?.role || '').toLowerCase();
        if (id.includes('wizard') || id.includes('mage') || role.includes('wizard') || role.includes('mage')) {
            return 'wizard';
        }
        if (id.includes('cleric') || role.includes('cleric')) {
            return 'cleric';
        }
        if (id.includes('ranger') || role.includes('ranger')) {
            return 'ranger';
        }
        if (id.includes('warrior') || role.includes('warrior')) {
            return 'warrior';
        }

        return id || role || 'unknown';
    }

    canCharacterLearnSpellScroll(character, item) {
        if (!character || character.team !== 'player' || !item || item.type !== 'spell-scroll') {
            return false;
        }

        const spell = window.GameData?.getSpellTemplateById(item.spellId) || null;
        if (!spell) {
            return false;
        }

        if ((character.spells || []).some((knownSpell) => knownSpell.id === spell.id)) {
            return false;
        }

        const allowedClasses = Array.isArray(item.allowedClasses) ? item.allowedClasses : [];
        if (allowedClasses.length === 0) {
            return true;
        }

        return allowedClasses.includes(this.getCharacterClassKey(character));
    }

    learnSpellScrollForCharacter(character, instanceId) {
        if (!character || character.team !== 'player' || !instanceId) {
            return false;
        }

        const items = this.getSharedLootItems();
        const itemIndex = items.findIndex((item) => item.instanceId === instanceId);
        if (itemIndex < 0) {
            this.showToast('That scroll is no longer in shared loot.', '#b8ad96', 2200);
            return false;
        }

        const item = this.ensureEquipmentItemInstance(items[itemIndex]);
        if (item?.type !== 'spell-scroll') {
            this.showToast('That item is not a spell scroll.', '#b8ad96', 2200);
            return false;
        }

        const spell = window.GameData?.getSpellTemplateById(item.spellId) || null;
        if (!spell) {
            this.showToast('This scroll has no readable spell.', '#b8ad96', 2200);
            return false;
        }

        if (!this.canCharacterLearnSpellScroll(character, item)) {
            this.showToast(`${character.name} cannot learn ${spell.name}.`, '#b8ad96', 2200);
            return false;
        }

        character.spells ||= [];
        character.spells.push({ ...spell });
        items.splice(itemIndex, 1);
        this.showToast(`${character.name} learns ${spell.name}.`, spell.accentColor || item.accentColor || character.accentColor, 2400);
        this.appendCombatLogEntry(`${character.name} learns ${spell.name} from a scroll.`, character.accentColor);
        this.maybeRenderActiveInventoryCharacter(character);
        this.updateActionBar?.();
        return true;
    }

    getInventoryEquipmentItemById(instanceId) {
        const items = this.sharedLootInventory?.items;
        if (!Array.isArray(items)) {
            return null;
        }

        return items.find((item) => item.instanceId === instanceId) || null;
    }

    equipSharedLootItemToCharacter(character, instanceId) {
        if (!character || character.team !== 'player' || !instanceId) {
            return false;
        }

        const items = this.getSharedLootItems();
        if (items.length === 0) {
            this.showToast('No shared loot items available.', '#b8ad96', 2200);
            return false;
        }

        const itemIndex = items.findIndex((item) => item.instanceId === instanceId);
        if (itemIndex < 0) {
            this.showToast('That item is no longer in shared loot.', '#b8ad96', 2200);
            return false;
        }

        const item = this.ensureEquipmentItemInstance(items[itemIndex]);
        const placement = this.getEquipmentPlacementForItem(character, item);
        const slotKey = placement?.targetSlot;
        if (!slotKey) {
            this.showToast('This item cannot be equipped.', '#b8ad96', 2200);
            return false;
        }

        items.splice(itemIndex, 1);

        const slotsToClear = Array.isArray(placement.slotsToClear) ? placement.slotsToClear : [slotKey];
        slotsToClear.forEach((clearSlotKey) => {
            if (character.equipment?.[clearSlotKey]) {
                this.unequipCharacterItemToSharedInventory(character, clearSlotKey);
            }
        });

        character.equipment[slotKey] = item;
        this.syncCharacterHandAliases(character);
        this.showToast(
            `${character.name} equips ${this.getEquipmentItemLabel(item)}.`,
            item.accentColor || character.accentColor,
            2400
        );

        this.maybeRenderActiveInventoryCharacter(character);

        return true;
    }

    unequipCharacterItemToSharedInventory(character, slotKey) {
        if (!character || !slotKey) {
            this.showToast('Unable to unequip this item.', '#b8ad96', 2200);
            return false;
        }

        if (character.team !== 'player') {
            this.showToast('Only party members can unequip items.', '#b8ad96', 2200);
            return false;
        }

        const equipped = slotKey === 'rightHand'
            ? (character.equipment?.rightHand ?? character.equipment?.hands ?? null)
            : character.equipment?.[slotKey];
        if (!equipped) {
            this.showToast('That slot is already empty.', '#b8ad96', 2200);
            return false;
        }

        const unequippedItem = this.ensureEquipmentItemInstance(equipped, slotKey);
        if (!unequippedItem) {
            this.showToast('Unable to parse equipped item.', '#b8ad96', 2200);
            return false;
        }

        this.addEquipmentItemToSharedInventory(unequippedItem);
        character.equipment[slotKey] = null;
        if (slotKey === 'rightHand') {
            character.equipment.hands = null;
        }
        this.syncCharacterHandAliases(character);
        this.showToast(
            `${character.name} unequips ${this.getEquipmentItemLabel(unequippedItem)}.`,
            '#c8bea8',
            2200
        );

        this.maybeRenderActiveInventoryCharacter(character);

        return true;
    }

    takeLootItem(cellKey, itemKey) {
        const drop = this.lootDropsByCell.get(cellKey);
        if (!drop || !itemKey) {
            return false;
        }

        let takenQuantity = 0;
        let takenEquipment = null;
        if (itemKey === 'gold') {
            takenQuantity = drop.gold ?? 0;
            drop.gold = 0;
        } else if (itemKey.startsWith('equipment:')) {
            const targetId = itemKey.slice('equipment:'.length);
            takenEquipment = this.takeEquipmentFromDrop(drop, targetId);
        }

        if (takenQuantity <= 0 && !takenEquipment) {
            return false;
        }

        const actorName = this.getLootInteractionCharacter()?.name ?? 'Party';
        if (takenQuantity > 0) {
            this.addItemToSharedInventory(itemKey, takenQuantity);
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
        }

        if (takenEquipment) {
            this.addEquipmentItemToSharedInventory(takenEquipment);
            this.appendCombatLogEntry(
                `${actorName} picks up ${this.getEquipmentItemLabel(takenEquipment)}.`,
                takenEquipment.accentColor || '#d9c47d'
            );
        }

        if (this.isLootDropEmpty(drop)) {
            this.lootDropsByCell.delete(cellKey);
            this.closeLootMenu();
        }

        this.saveCurrentMapPersistentState();

        if (this.activeInventoryCharacter && this.activeInventoryTab === 'shared') {
            this.renderCharacterInventory();
        }

        if (this.activeLootCellKey === cellKey) {
            this.renderLootMenuForCell(cellKey);
        }

        return true;
    }

    takeAllLootFromCell(cellKey, options = {}) {
        const drop = this.lootDropsByCell.get(cellKey);
        if (!drop) {
            return false;
        }

        const propsOnly = Boolean(options?.propsOnly);
        if (propsOnly && drop.sourceType !== 'prop') {
            return false;
        }

        const itemKeys = this.getLootMenuItemsForCell(cellKey).map((entry) => entry.itemKey);
        if (itemKeys.length === 0) {
            return false;
        }

        let tookAny = false;
        itemKeys.forEach((itemKey) => {
            tookAny = this.takeLootItem(cellKey, itemKey) || tookAny;
        });

        return tookAny;
    }

    leaveLootItemOnGround(cellKey, itemKey) {
        const item = this.getLootMenuItemsForCell(cellKey).find((entry) => entry.itemKey === itemKey);
        if (!item) {
            return;
        }

        const actorName = this.getActiveTurnCharacter()?.name ?? 'Party';
        if (item.itemKey === 'gold') {
            this.appendCombatLogEntry(`${actorName} leaves ${item.quantity} ${item.label.toLowerCase()} on the ground.`, '#8f856f');
        } else {
            this.appendCombatLogEntry(`${actorName} leaves ${item.label} on the ground.`, '#8f856f');
        }
        this.closeLootMenu();
    }

    openLootMenuForCell(cellKey, interactor = null) {
        if (!cellKey || !this.lootDropsByCell.has(cellKey)) {
            return false;
        }

        if (this.isCellOccupiedByLivingEnemy(cellKey)) {
            return false;
        }

        const actingCharacter = interactor || this.getLootInteractionCharacter();
        const cell = this.parseCellKey(cellKey);
        if (!actingCharacter || !cell || !this.canCharacterInteractWithCell(actingCharacter, cell.gridX, cell.gridY, 1)) {
            if (cell) {
                this.showOutOfRangeToastAtCell(cell.gridX, cell.gridY);
            }
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

        if (this.gameMode === 'combat') {
            const activeCharacterId = this.getCharacterTurnIdentity(this.getActiveTurnCharacter());
            const characterId = this.getCharacterTurnIdentity(character);
            if (characterId && activeCharacterId === characterId) {
                this.endCurrentTurn();
            }
        }

        this.saveCurrentMapPersistentState();
    }

    areAllPartyMembersDead(group) {
        return group.length > 0 && group.every((character) => character.isDead);
    }

    // --- Game Loop ---

    update() {
        const nowMs = performance.now();
        this.stabilizeCombatTurnState();
        const activeCharacter = this.getActiveTurnCharacter();
        this.recoverIfEnemyTurnStalled(activeCharacter);
        this.updatePartyVisionState();
        this.updateCharacterVisibilityByVision();

        this.updateProjectiles(nowMs);
        this.updateReachableMovementHighlights(activeCharacter);
        this.updateAbilityRangeHighlights(activeCharacter);
        this.updateTargetHighlights(activeCharacter);
        this.updateActiveCharacterTurnHighlight(activeCharacter);
        this.updateTargetPreview(activeCharacter);
        this.updateLootBagMarkers();
        this.updateDoorAnimations(nowMs);

        this.characters.forEach((character) => {
            this.updateCharacterCard(character, activeCharacter);
            this.fadeAndRemoveCharacter(character);
            this.updateHitAnimation(character, nowMs);
        });
        this.updateCharacterMovementAnimations(nowMs);
        this.updateTurnOrderQueue(activeCharacter);

        if (!this.isGameOver && this.areAllPartyMembersDead(this.playerParty)) {
            this.startGameOverSequence();
        }

        this.resolveCombatGroupState();

        this.updateCombatTransition();

        if (this.isGameOver) {
            this.updateGameOverSequence();
            return;
        }

        if (this.gameMode === 'exploration') {
            this.tryTriggerEnemyAggro();
            if (this.explorationClickMoveQueue.length > 0) {
                if (!this.isExplorationMovementActive()) {
                    this.explorationClickMoveTimer += 1;
                    if (this.explorationClickMoveTimer >= this.explorationClickMoveStepInterval) {
                        this.explorationClickMoveTimer = 0;
                        const nextKey = this.explorationClickMoveQueue.shift();
                        this.movePartyInExploration(nextKey);
                        if (this.gameMode !== 'exploration') {
                            this.explorationClickMoveQueue = [];
                        }
                    }
                }
            }
            return;
        }

        if (this.gameMode === 'combat') {
            this.tryTriggerEnemyAggro();
        }

        if (this.turnTransitionFrames > 0) {
            this.turnTransitionFrames -= 1;
            return;
        }

        const turnActor = this.getActiveTurnCharacter();
        if (turnActor && turnActor.team !== 'player' && !this.isCombatActionPending(turnActor)) {
            this.enemyMoveTimer += 1;
            if (this.enemyMoveTimer >= 24) {
                const turnActorId = this.getCharacterTurnIdentity(turnActor);
                const didAct = this.moveAICharacter(turnActor);

                if (!didAct) {
                    const activeAfterAi = this.getActiveTurnCharacter();
                    const activeAfterAiId = this.getCharacterTurnIdentity(activeAfterAi);
                    if (turnActorId && activeAfterAiId === turnActorId) {
                        turnActor.actionsRemaining = 0;
                        turnActor.bonusMovementRemaining = 0;
                        this.appendCombatLogEntry(
                            `${turnActor.name} turn auto-advanced (AI recovery).`,
                            '#c6b28f'
                        );
                        this.endCurrentTurn();
                    }
                }

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
