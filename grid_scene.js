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
        this.camera.position.z = 500;

        this.renderer = new THREE.WebGLRenderer({ antialias: true });
        this.renderer.setSize(viewW, viewH);
        this.renderer.setPixelRatio(window.devicePixelRatio);
        this.container.appendChild(this.renderer.domElement);

        this.abilityRangeHighlightGroup = new THREE.Group();
        this.abilityRangeHighlightState = '';
        this.scene.add(this.abilityRangeHighlightGroup);

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

        this.activeProjectiles = [];

        const dungeon = this.generateDungeonMap();
        this.dungeonMap = dungeon.map;
        this.placeCharacters(dungeon.rooms);
        this.beginCurrentTurn();

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

        const enemyAnchorFallback = this.findFallbackRoomCenter(rooms, -1);
        const enemyAnchor = this.findNearbyFloorTile(this.wizard.gridX, this.wizard.gridY, 4, 8, occupiedCells) || enemyAnchorFallback;

        this.placeCharacterNear(this.goblin, enemyAnchor.x, enemyAnchor.y, 0, 2, occupiedCells, enemyAnchorFallback);
        occupiedCells.add(this.getCellKey(this.goblin.gridX, this.goblin.gridY));

        this.placeCharacterNear(this.goblinBrute, this.goblin.gridX, this.goblin.gridY, 1, 2, occupiedCells, this.findFallbackRoomCenter(rooms, -2));
        occupiedCells.add(this.getCellKey(this.goblinBrute.gridX, this.goblinBrute.gridY));

        this.placeCharacterNear(this.goblinArcher, this.goblin.gridX, this.goblin.gridY, 1, 2, occupiedCells, this.findFallbackRoomCenter(rooms, -3));
        occupiedCells.add(this.getCellKey(this.goblinArcher.gridX, this.goblinArcher.gridY));

        this.placeCharacterNear(this.goblinShaman, this.goblin.gridX, this.goblin.gridY, 1, 2, occupiedCells, this.findFallbackRoomCenter(rooms, -4));
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
        return this.aiParty.filter((character) => !character.isDead && character.race === 'goblin');
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
        return this.turnOrder.filter((character) => !character.isDead);
    }

    getActiveTurnCharacter() {
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
        const activeCharacter = this.getActiveTurnCharacter();
        return Boolean(activeCharacter && activeCharacter.team === 'player');
    }

    beginCurrentTurn() {
        const activeCharacter = this.getActiveTurnCharacter();
        if (!activeCharacter) {
            return;
        }

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
                this.endCurrentTurn();
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
            if (this.isGameOver || !this.isPlayerTurn()) {
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
                : this.getLivingCharacters(this.aiParty).filter((c) => c.mesh);

            if (targetPool.length === 0) {
                return;
            }

            const rect = this.renderer.domElement.getBoundingClientRect();
            this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
            this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
            this.raycaster.setFromCamera(this.mouse, this.camera);

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

        const dx = Math.abs(target.gridX - attacker.gridX);
        const dy = Math.abs(target.gridY - attacker.gridY);
        if (dx > attackRange || dy > attackRange) {
            return false;
        }

        this.faceCharacterToward(attacker, target);
        if (resolvedAttackAbility?.id === 'bow-shot') {
            const attackerPos = this.getCharacterWorldPos(attacker);
            const targetPos = this.getCharacterWorldPos(target);
            this.spawnArrowProjectile(attackerPos, targetPos, () => {
                this.spawnArrowImpactEffect(targetPos);
                this.applyPhysicalAttackDamage(target, baseDamage);
            });
        } else {
            this.applyPhysicalAttackDamage(target, baseDamage);
        }

        attacker.actionsRemaining -= attacker.attackCost;
        if (attacker.actionsRemaining <= 0) {
            this.endCurrentTurn();
        }

        return true;
    }

    applyPhysicalAttackDamage(target, baseDamage) {
        if (!target || target.isDead) {
            return;
        }

        const physicalDamage = Math.max(0, baseDamage - target.armorClass);
        target.hitPoints -= physicalDamage;
        this.playHitAnimation(target);

        if (target.hitPoints <= 0) {
            target.hitPoints = 0;
            this.markCharacterDead(target);
        }
    }

    castMagicMissile(caster, target) {
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

        this.faceCharacterToward(caster, target);
        const damage = ability.damage ?? caster.meleeAttackDamage;
        target.hitPoints -= damage;
        caster.magicPoints -= ability.mpCost;

        const lethal = target.hitPoints <= 0;
        if (lethal) {
            target.hitPoints = 0;
        }

        const casterPos = this.getCharacterWorldPos(caster);
        const targetPos = this.getCharacterWorldPos(target);
        this.spawnMagicMissileProjectiles(casterPos, targetPos, () => {
            this.playHitAnimation(target);
            if (lethal) {
                this.markCharacterDead(target);
            }
        });

        caster.actionsRemaining -= caster.attackCost;
        if (caster.actionsRemaining <= 0) {
            this.endCurrentTurn();
        }

        return true;
    }

    castBattleShout(caster) {
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
        allies.forEach((ally) => {
            const dx = Math.abs(ally.gridX - caster.gridX);
            const dy = Math.abs(ally.gridY - caster.gridY);
            if (dx <= range && dy <= range) {
                ally.armorClass += acBonus;
                const pos = this.getCharacterWorldPos(ally);
                this.spawnBattleShoutEffect(pos);
            }
        });

        caster.actionsRemaining -= caster.attackCost;
        if (caster.actionsRemaining <= 0) {
            this.endCurrentTurn();
        }

        return true;
    }

    castInflictPain(caster) {
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

        this.getLivingGoblinAllies().forEach((ally) => {
            const dx = Math.abs(ally.gridX - caster.gridX);
            const dy = Math.abs(ally.gridY - caster.gridY);
            if (dx <= range && dy <= range) {
                ally.meleeAttackDamage += damageBonus;
                const pos = this.getCharacterWorldPos(ally);
                this.spawnInflictPainEffect(pos);
            }
        });

        caster.magicPoints -= mpCost;
        caster.actionsRemaining -= caster.attackCost;
        if (caster.actionsRemaining <= 0) {
            this.endCurrentTurn();
        }

        return true;
    }

    castHeal(caster, target) {
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

        if (caster !== target) {
            this.faceCharacterToward(caster, target);
        }

        const healAmount = ability.healAmount ?? 5;
        target.hitPoints = Math.min(target.maxHitPoints, target.hitPoints + healAmount);
        caster.magicPoints -= ability.mpCost;

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

    markCharacterDead(character) {
        if (character.isDead) {
            return;
        }

        character.isDead = true;
        character.fadeFrames = 0;
        character.actionsRemaining = 0;

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
        this.updateAbilityRangeHighlights(activeCharacter);

        this.characters.forEach((character) => {
            this.updateCharacterCard(character, activeCharacter);
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
