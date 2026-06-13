// AI - enemy movement, pathfinding, and tactical decisions
window.GridAI = {

    getOpposingGroupForCharacter(character) {
        if (!character) {
            return [];
        }

        if (this.isPartyAlignedCharacter?.(character)) {
            if (this.gameMode === 'combat') {
                return this.getCombatEnemiesForPlayers();
            }
            return this.aiParty;
        }

        return this.playerParty;
    },

    getAllyGroupForCharacter(character) {
        if (!character) {
            return [];
        }

        if (this.isPartyAlignedCharacter?.(character)) {
            if (this.gameMode === 'combat') {
                return this.getCombatPartyMembers?.() || this.playerParty;
            }
            return this.playerParty;
        }

        if (this.gameMode === 'combat') {
            return this.getCombatAlliedEnemies();
        }

        return this.aiParty;
    },

    getNearestLivingOpponent(character) {
        const enemyGroup = this.getOpposingGroupForCharacter(character);
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
    },

    getGoblinRole(character) {
        const characterId = character?.id || '';

        if (characterId.includes('goblin-archer')) {
            return 'archer';
        }

        if (characterId.includes('goblin-shaman')) {
            return 'shaman';
        }

        if (characterId.includes('goblin-brute')) {
            return 'brute';
        }

        if (characterId.includes('goblin-warrior')) {
            return 'warrior';
        }

        return null;
    },

    getAbilityForCharacter(character, abilityId = character?.selectedAbilityId) {
        if (!character || !abilityId) {
            return null;
        }

        return window.CharacterData?.getCharacterActionById(character, abilityId) || null;
    },

    getCharacterActionList(character) {
        return window.CharacterData?.getCharacterActionList(character) || [];
    },

    getBestOffensiveAbility(character) {
        const actions = this.getCharacterActionList(character);
        if (actions.length === 0) {
            return null;
        }

        const offensiveAbilities = actions.filter((ability) =>
            (ability.type === 'attack' || (ability.type === 'spell' && ability.id !== 'inflict-pain')) &&
            (ability.type !== 'attack' || this.canCharacterUseAttackAbility(character, ability)) &&
            ((ability.mpCost ?? 0) === 0 || character.magicPoints >= (ability.mpCost ?? 0))
        );

        if (offensiveAbilities.length === 0) {
            return null;
        }

        return offensiveAbilities.reduce((bestAbility, candidate) => {
            const bestDamage = this.getEffectiveAbilityDamage(character, bestAbility) ?? 0;
            const candidateDamage = this.getEffectiveAbilityDamage(character, candidate) ?? 0;
            if (candidateDamage !== bestDamage) {
                return candidateDamage > bestDamage ? candidate : bestAbility;
            }
            const candidateRange = this.getEffectiveAbilityRange(character, candidate);
            const bestRange = this.getEffectiveAbilityRange(character, bestAbility);
            return candidateRange > bestRange ? candidate : bestAbility;
        }, offensiveAbilities[0]);
    },

    getAbilityTargetTeam(ability) {
        if (!ability) {
            return 'enemy';
        }

        if (ability.type === 'heal' || ability.type === 'buff' || ability.id === 'inflict-pain') {
            return 'ally';
        }

        return 'enemy';
    },

    getAttackDistanceBetweenPositions(fromX, fromY, toX, toY) {
        return Math.max(Math.abs(toX - fromX), Math.abs(toY - fromY));
    },

    hasLineOfSightBetweenCells(fromX, fromY, toX, toY) {
        const deltaX = toX - fromX;
        const deltaY = toY - fromY;
        const steps = Math.max(Math.abs(deltaX), Math.abs(deltaY));

        if (steps <= 1) {
            return true;
        }

        let previousCellKey = this.getCellKey(fromX, fromY);
        const traversedCells = new Set();

        for (let step = 1; step < steps; step++) {
            const progress = step / steps;
            const sampleX = fromX + deltaX * progress;
            const sampleY = fromY + deltaY * progress;
            const gridX = Math.round(sampleX);
            const gridY = Math.round(sampleY);
            const cellKey = this.getCellKey(gridX, gridY);

            if (cellKey === previousCellKey || traversedCells.has(cellKey)) {
                continue;
            }

            traversedCells.add(cellKey);
            previousCellKey = cellKey;

            if (gridX === fromX && gridY === fromY) {
                continue;
            }

            if (gridX === toX && gridY === toY) {
                continue;
            }

            const isBlockingCell = typeof this.isLineOfSightBlockingCell === 'function'
                ? this.isLineOfSightBlockingCell(gridX, gridY)
                : this.isObstacle(gridX, gridY);
            if (isBlockingCell) {
                return false;
            }
        }

        return true;
    },

    requiresLineOfSight(ability, character = null) {
        if (!ability) {
            return false;
        }

        const range = character ? this.getEffectiveAbilityRange(character, ability) : (ability.range ?? 1);
        return range > 1 && (ability.type === 'attack' || ability.type === 'spell' || ability.type === 'heal');
    },

    getAdjacentMoves(character, originX = character.gridX, originY = character.gridY) {
        const candidates = [
            { x: originX + 1, y: originY, facing: 'right' },
            { x: originX - 1, y: originY, facing: 'left' },
            { x: originX, y: originY + 1, facing: 'down' },
            { x: originX, y: originY - 1, facing: 'up' }
        ];

        return candidates.filter(({ x, y }) => !this.isObstacle(x, y) && !this.isOccupied(x, y, character));
    },

    getReachablePositions(character, maxSteps) {
        const startKey = this.getCellKey(character.gridX, character.gridY);
        const queue = [{ x: character.gridX, y: character.gridY, steps: 0, path: [] }];
        const bestSteps = new Map([[startKey, 0]]);
        const reachable = [{ x: character.gridX, y: character.gridY, steps: 0, path: [] }];

        while (queue.length > 0) {
            const current = queue.shift();
            if (current.steps >= maxSteps) {
                continue;
            }

            const nextMoves = this.getAdjacentMoves(character, current.x, current.y);
            nextMoves.forEach((move) => {
                const nextSteps = current.steps + 1;
                const key = this.getCellKey(move.x, move.y);
                if (bestSteps.has(key) && bestSteps.get(key) <= nextSteps) {
                    return;
                }

                bestSteps.set(key, nextSteps);
                const nextNode = {
                    x: move.x,
                    y: move.y,
                    steps: nextSteps,
                    path: [...current.path, move]
                };
                reachable.push(nextNode);
                queue.push(nextNode);
            });
        }

        return reachable;
    },

    getExpectedActionEffect(character, target, ability = null, originX = character?.gridX, originY = character?.gridY) {
        const resolvedAbility = ability || this.getAbilityForCharacter(character) || this.getBestOffensiveAbility(character);
        if (!character || !target || !resolvedAbility) {
            return null;
        }

        const targetMode = this.getAbilityTargetTeam(resolvedAbility);
        const correctTeam = targetMode === 'ally'
            ? character.team === target.team
            : character.team !== target.team;
        const range = this.getEffectiveAbilityRange(character, resolvedAbility);
        const distance = this.getAttackDistanceBetweenPositions(originX, originY, target.gridX, target.gridY);
        const withinRange = distance <= range;
        const lineOfSightRequired = this.requiresLineOfSight(resolvedAbility, character);
        const hasLineOfSight = !lineOfSightRequired || this.hasLineOfSightBetweenCells(originX, originY, target.gridX, target.gridY);
        const canAfford = (resolvedAbility.mpCost ?? 0) <= (character.magicPoints ?? 0);
        const canAct = character.actionsRemaining >= character.attackCost;

        if (resolvedAbility.type === 'heal') {
            const amount = Math.max(0, Math.min(
                this.getEffectiveAbilityHealAmount(character, resolvedAbility),
                target.maxHitPoints - target.hitPoints
            ));

            return {
                ability: resolvedAbility,
                amount,
                effectType: 'heal',
                description: `Restore ${amount} HP`,
                distance,
                withinRange,
                hasLineOfSight,
                correctTeam,
                canAfford,
                canAct,
                isValid: correctTeam && withinRange && hasLineOfSight && canAfford && canAct && amount > 0,
                damageKind: 'heal'
            };
        }

        if (resolvedAbility.id === 'inflict-pain') {
            return {
                ability: resolvedAbility,
                amount: 0,
                effectType: 'inflict-pain',
                description: `Grant ${resolvedAbility.damageBonus ?? 1} DMG`,
                distance,
                withinRange,
                hasLineOfSight,
                correctTeam,
                canAfford,
                canAct,
                isValid: correctTeam && withinRange && hasLineOfSight && canAfford && canAct,
                damageKind: 'buff'
            };
        }

        if (resolvedAbility.type === 'buff') {
            const amount = resolvedAbility.acBonus ?? resolvedAbility.damageBonus ?? 0;
            return {
                ability: resolvedAbility,
                amount,
                effectType: 'buff',
                description: `Apply ${resolvedAbility.name}`,
                distance,
                withinRange,
                hasLineOfSight,
                correctTeam,
                canAfford,
                canAct,
                isValid: correctTeam && withinRange && hasLineOfSight && canAfford && canAct,
                damageKind: 'buff'
            };
        }

        const baseDamage = this.getEffectiveAbilityDamage(character, resolvedAbility);
        const isSpell = resolvedAbility.type === 'spell' || resolvedAbility.id === 'magic-missile';
        const effectiveArmorClass = this.getEffectiveArmorClassValue(target);
        const amount = isSpell ? baseDamage : Math.max(0, baseDamage - effectiveArmorClass);

        return {
            ability: resolvedAbility,
            amount,
            effectType: 'damage',
            description: `Deal ${amount} damage`,
            distance,
            withinRange,
            hasLineOfSight,
            correctTeam,
            canAfford,
            canAct,
            isValid: correctTeam && withinRange && hasLineOfSight && canAfford && canAct,
            damageKind: isSpell ? 'magic' : (range > 1 ? 'ranged' : 'melee')
        };
    },

    getTargetRolePriority(character, target) {
        const targetId = target?.id ?? '';
        const attackerRole = this.getGoblinRole(character);

        if (targetId.includes('wizard') || targetId.includes('cleric')) {
            return attackerRole === 'archer' ? 7 : 6;
        }
        if (targetId.includes('ranger') || targetId.includes('archer')) {
            return attackerRole === 'archer' ? 7 : 5;
        }
        if (targetId.includes('shaman')) {
            return attackerRole === 'archer' ? 6 : 4;
        }
        if (targetId.includes('dwarf') || targetId.includes('brute')) {
            return attackerRole === 'warrior' || attackerRole === 'brute' ? 5 : 2;
        }
        return 3;
    },

    scoreTargetForCharacter(character, target, ability = null) {
        const effect = this.getExpectedActionEffect(character, target, ability);
        if (!effect || !effect.correctTeam) {
            return Number.NEGATIVE_INFINITY;
        }

        const attackerRole = this.getGoblinRole(character);
        const isFrontliner = attackerRole === 'warrior' || attackerRole === 'brute';
        const isArcher = attackerRole === 'archer';
        const missingHp = target.maxHitPoints - target.hitPoints;
        const killBonus = effect.amount >= target.hitPoints ? (isFrontliner ? 34 : 28) : 0;
        const immediateReachBonus = effect.withinRange ? (isFrontliner ? 22 : 18) : 0;
        const pressureBonus = effect.distance <= this.getEffectiveAbilityRange(character, effect.ability) + Math.max(0, character.actionsRemaining - character.attackCost)
            ? (isFrontliner ? 16 : 8)
            : 0;
        const weakTargetBonus = target.hitPoints <= Math.max(4, Math.ceil(target.maxHitPoints * 0.45)) ? (isFrontliner ? 18 : 10) : 0;
        const rolePriority = this.getTargetRolePriority(character, target);
        const distanceWeight = isFrontliner ? 4.5 : (isArcher ? 2.4 : 1.5);
        const roleWeight = isFrontliner ? 1.1 : 3;
        const hpWeight = isFrontliner ? 2.5 : 1.2;
        const focusPenalty = isFrontliner && effect.distance > 1 ? effect.distance * 1.2 : 0;

        return (
            effect.amount * 8 +
            missingHp * hpWeight +
            rolePriority * roleWeight +
            killBonus +
            immediateReachBonus +
            pressureBonus +
            weakTargetBonus -
            effect.distance * distanceWeight -
            focusPenalty -
            this.getEffectiveArmorClassValue(target) * 1.2
        );
    },

    getBestOpponentTarget(character, ability = null) {
        const enemyGroup = this.getOpposingGroupForCharacter(character);
        const livingOpponents = this.getLivingCharacters(enemyGroup);
        if (livingOpponents.length === 0) {
            return null;
        }

        let bestTarget = livingOpponents[0];
        let bestScore = Number.NEGATIVE_INFINITY;
        livingOpponents.forEach((candidate) => {
            const score = this.scoreTargetForCharacter(character, candidate, ability);
            if (score > bestScore) {
                bestTarget = candidate;
                bestScore = score;
            }
        });

        return bestTarget;
    },

    getBestFrontlineTarget(character, ability = null) {
        const enemyGroup = this.getOpposingGroupForCharacter(character);
        const livingOpponents = this.getLivingCharacters(enemyGroup);
        if (livingOpponents.length === 0) {
            return null;
        }

        const nearestOpponent = this.getNearestLivingOpponent(character);
        let bestTarget = livingOpponents[0];
        let bestScore = Number.NEGATIVE_INFINITY;

        livingOpponents.forEach((candidate) => {
            const effect = this.getExpectedActionEffect(character, candidate, ability);
            if (!effect) {
                return;
            }

            const missingHp = candidate.maxHitPoints - candidate.hitPoints;
            const lowHpBonus = candidate.hitPoints <= Math.max(4, Math.ceil(candidate.maxHitPoints * 0.5)) ? 20 : 0;
            const nearestBonus = nearestOpponent && candidate === nearestOpponent ? 24 : 0;
            const score = (
                missingHp * 3.25 +
                lowHpBonus +
                nearestBonus +
                this.getTargetRolePriority(character, candidate) * 1.1 -
                effect.distance * 4.75 -
                this.getEffectiveArmorClassValue(candidate) * 1.5
            );

            if (score > bestScore) {
                bestTarget = candidate;
                bestScore = score;
            }
        });

        return bestTarget;
    },

    getBestImmediateAttackTarget(character, ability = null) {
        const enemyGroup = this.getOpposingGroupForCharacter(character);
        const livingOpponents = this.getLivingCharacters(enemyGroup);
        let bestTarget = null;
        let bestScore = Number.NEGATIVE_INFINITY;

        livingOpponents.forEach((candidate) => {
            const effect = this.getExpectedActionEffect(character, candidate, ability);
            if (!effect?.isValid) {
                return;
            }

            const score = this.scoreTargetForCharacter(character, candidate, ability);
            if (score > bestScore) {
                bestTarget = candidate;
                bestScore = score;
            }
        });

        return bestTarget;
    },

    getDistanceToNearestOpponentAt(character, gridX, gridY) {
        const enemyGroup = this.getOpposingGroupForCharacter(character);
        const livingOpponents = this.getLivingCharacters(enemyGroup);
        if (livingOpponents.length === 0) {
            return Number.POSITIVE_INFINITY;
        }

        return livingOpponents.reduce((nearestDistance, candidate) => {
            const distance = this.getAttackDistanceBetweenPositions(gridX, gridY, candidate.gridX, candidate.gridY);
            return Math.min(nearestDistance, distance);
        }, Number.POSITIVE_INFINITY);
    },

    countAdjacentAlliesAt(character, gridX, gridY) {
        const allyGroup = this.getAllyGroupForCharacter(character);
        return this.getLivingCharacters(allyGroup).filter((ally) =>
            ally !== character &&
            this.getAttackDistanceBetweenPositions(gridX, gridY, ally.gridX, ally.gridY) <= 1
        ).length;
    },

    countGoblinCoverageAt(gridX, gridY, range) {
        return this.getLivingGoblinAllies().filter((ally) =>
            Math.abs(ally.gridX - gridX) <= range &&
            Math.abs(ally.gridY - gridY) <= range
        ).length;
    },

    getNearbyHurtGoblin(caster, range) {
        const candidates = this.getLivingGoblinAllies().filter((ally) =>
            ally.hitPoints < ally.maxHitPoints &&
            Math.abs(ally.gridX - caster.gridX) <= range &&
            Math.abs(ally.gridY - caster.gridY) <= range
        );

        if (candidates.length === 0) {
            return null;
        }

        candidates.sort((left, right) => {
            const leftMissing = left.maxHitPoints - left.hitPoints;
            const rightMissing = right.maxHitPoints - right.hitPoints;
            if (leftMissing !== rightMissing) {
                return rightMissing - leftMissing;
            }

            const leftDistance = this.getAttackDistanceBetweenPositions(caster.gridX, caster.gridY, left.gridX, left.gridY);
            const rightDistance = this.getAttackDistanceBetweenPositions(caster.gridX, caster.gridY, right.gridX, right.gridY);
            return leftDistance - rightDistance;
        });

        return candidates[0];
    },

    getNearbyHurtGoblinAt(caster, range, originX = caster?.gridX, originY = caster?.gridY) {
        if (!caster) {
            return null;
        }

        const candidates = this.getLivingGoblinAllies().filter((ally) => {
            if (ally.hitPoints >= ally.maxHitPoints) {
                return false;
            }

            const effect = this.getExpectedActionEffect(caster, ally, this.getAbilityForCharacter(caster, 'mend-flesh'), originX, originY);
            return Boolean(effect?.isValid) && effect.distance <= range;
        });

        if (candidates.length === 0) {
            return null;
        }

        candidates.sort((left, right) => {
            const leftMissing = left.maxHitPoints - left.hitPoints;
            const rightMissing = right.maxHitPoints - right.hitPoints;
            if (leftMissing !== rightMissing) {
                return rightMissing - leftMissing;
            }

            const leftDistance = this.getAttackDistanceBetweenPositions(originX, originY, left.gridX, left.gridY);
            const rightDistance = this.getAttackDistanceBetweenPositions(originX, originY, right.gridX, right.gridY);
            return leftDistance - rightDistance;
        });

        return candidates[0];
    },

    getDistanceToNearestGoblinAllyAt(character, gridX, gridY) {
        const allies = this.getLivingGoblinAllies().filter((ally) => ally !== character);
        if (allies.length === 0) {
            return 0;
        }

        return allies.reduce((nearestDistance, ally) => {
            const distance = this.getAttackDistanceBetweenPositions(gridX, gridY, ally.gridX, ally.gridY);
            return Math.min(nearestDistance, distance);
        }, Number.POSITIVE_INFINITY);
    },

    moveCharacterToCell(character, destination) {
        if (!character || !destination) {
            return false;
        }

        if (!this.consumeCharacterMovement(character, 1)) {
            return false;
        }

        character.gridX = destination.x;
        character.gridY = destination.y;
        this.updateCharacterFacing(character, destination.facing);
        this.updateCharacterPosition(character);

        if (this.shouldEndCurrentTurn(character)) {
            this.endCurrentTurn();
        }

        return true;
    },

    getBestRangedPlan(character, ability) {
        const maxMoveSteps = this.getCharacterMovementBudget(character, character.attackCost);
        const reachablePositions = this.getReachablePositions(character, maxMoveSteps);
        const preferredRange = Math.max(3, this.getEffectiveAbilityRange(character, ability));
        let bestPlan = null;

        reachablePositions.forEach((candidatePosition) => {
            this.getLivingCharacters(this.getOpposingGroupForCharacter(character)).forEach((target) => {
                const effect = this.getExpectedActionEffect(
                    character,
                    target,
                    ability,
                    candidatePosition.x,
                    candidatePosition.y
                );

                if (!effect?.correctTeam || !effect.withinRange || !effect.canAfford) {
                    return;
                }

                const baseTargetScore = this.scoreTargetForCharacter(character, target, ability);
                const distanceDelta = Math.abs(effect.distance - preferredRange);
                const safety = this.getDistanceToNearestOpponentAt(character, candidatePosition.x, candidatePosition.y);
                const score = baseTargetScore + safety * 2.5 - distanceDelta * 3.5 - candidatePosition.steps * 1.2;

                if (!bestPlan || score > bestPlan.score) {
                    bestPlan = {
                        position: candidatePosition,
                        target,
                        score
                    };
                }
            });
        });

        return bestPlan;
    },

    getBestMeleePlan(character, ability) {
        const maxMoveSteps = this.getCharacterMovementBudget(character, character.attackCost);
        const reachablePositions = this.getReachablePositions(character, maxMoveSteps);
        let bestPlan = null;

        this.getLivingCharacters(this.getOpposingGroupForCharacter(character)).forEach((target) => {
            reachablePositions.forEach((candidatePosition) => {
                const effect = this.getExpectedActionEffect(
                    character,
                    target,
                    ability,
                    candidatePosition.x,
                    candidatePosition.y
                );

                if (!effect?.correctTeam || !effect.withinRange) {
                    return;
                }

                const support = this.countAdjacentAlliesAt(character, candidatePosition.x, candidatePosition.y);
                const score = this.scoreTargetForCharacter(character, target, ability) + support * 1.5 - candidatePosition.steps * 1.2;

                if (!bestPlan || score > bestPlan.score) {
                    bestPlan = {
                        position: candidatePosition,
                        target,
                        score
                    };
                }
            });
        });

        return bestPlan;
    },

    findShortestPathToAttackPosition(character, target, ability = null) {
        if (!character || !target || target.isDead) {
            return null;
        }

        const startKey = this.getCellKey(character.gridX, character.gridY);
        const queue = [{ x: character.gridX, y: character.gridY, path: [] }];
        const visited = new Set([startKey]);

        while (queue.length > 0) {
            const current = queue.shift();
            const effect = this.getExpectedActionEffect(
                character,
                target,
                ability,
                current.x,
                current.y
            );

            if (effect?.correctTeam && effect.withinRange && effect.hasLineOfSight) {
                return current.path;
            }

            this.getAdjacentMoves(character, current.x, current.y).forEach((move) => {
                const cellKey = this.getCellKey(move.x, move.y);
                if (visited.has(cellKey)) {
                    return;
                }

                visited.add(cellKey);
                queue.push({
                    x: move.x,
                    y: move.y,
                    path: [...current.path, move]
                });
            });
        }

        return null;
    },

    chooseBestAdvanceMove(character, target, ability = null) {
        const shortestPath = this.findShortestPathToAttackPosition(character, target, ability);
        if (shortestPath?.length > 0) {
            return shortestPath[0];
        }

        const adjacentMoves = this.getAdjacentMoves(character);
        if (adjacentMoves.length === 0) {
            return null;
        }

        let bestMove = null;
        let bestScore = Number.NEGATIVE_INFINITY;

        adjacentMoves.forEach((candidate) => {
            const distanceToTarget = this.getAttackDistanceBetweenPositions(candidate.x, candidate.y, target.gridX, target.gridY);
            const support = this.countAdjacentAlliesAt(character, candidate.x, candidate.y);
            const safety = this.getDistanceToNearestOpponentAt(character, candidate.x, candidate.y);
            const desiredRange = this.getEffectiveAbilityRange(character, ability);
            const score = (
                this.scoreTargetForCharacter(character, target, ability) -
                Math.abs(distanceToTarget - desiredRange) * 5 -
                distanceToTarget * 1.4 +
                support * 1.2 +
                Math.min(safety, desiredRange + 1)
            );

            if (score > bestScore) {
                bestMove = candidate;
                bestScore = score;
            }
        });

        return bestMove;
    },

    moveGoblinArcher(character) {
        const bowAbility = this.getAbilityForCharacter(character, 'bow-shot');
        if (!bowAbility) {
            return false;
        }

        character.selectedAbilityId = bowAbility.id;

        const immediateTarget = this.getBestImmediateAttackTarget(character, bowAbility);
        if (immediateTarget && character.actionsRemaining >= character.attackCost) {
            return this.characterAttack(character, immediateTarget, bowAbility);
        }

        const rangedPlan = this.getBestRangedPlan(character, bowAbility);
        if (rangedPlan?.position?.path?.length > 0) {
            return this.moveCharacterToCell(character, rangedPlan.position.path[0]);
        }

        const fallbackTarget = rangedPlan?.target || this.getBestOpponentTarget(character, bowAbility);
        if (!fallbackTarget) {
            this.endCurrentTurn();
            return true;
        }

        const distanceToFallbackTarget = this.getAttackDistanceBetweenPositions(
            character.gridX,
            character.gridY,
            fallbackTarget.gridX,
            fallbackTarget.gridY
        );

        const idealMinimumDistance = Math.max(3, this.getEffectiveAbilityRange(character, bowAbility) - 3);
        if (distanceToFallbackTarget <= idealMinimumDistance) {
            const retreatMoves = this.getReachablePositions(character, this.getCharacterMovementBudget(character, character.attackCost));
            let bestRetreat = null;
            let bestRetreatScore = Number.NEGATIVE_INFINITY;

            retreatMoves.forEach((candidate) => {
                if (candidate.steps === 0) {
                    return;
                }

                const distanceToTarget = this.getAttackDistanceBetweenPositions(candidate.x, candidate.y, fallbackTarget.gridX, fallbackTarget.gridY);
                const safety = this.getDistanceToNearestOpponentAt(character, candidate.x, candidate.y);
                const score = distanceToTarget * 5 + safety * 1.5 - candidate.steps * 1.1;

                if (score > bestRetreatScore) {
                    bestRetreat = candidate;
                    bestRetreatScore = score;
                }
            });

            if (bestRetreat?.path?.length > 0) {
                return this.moveCharacterToCell(character, bestRetreat.path[0]);
            }
        }

        const fallbackMove = this.chooseBestAdvanceMove(character, fallbackTarget, bowAbility);
        if (fallbackMove) {
            return this.moveCharacterToCell(character, fallbackMove);
        }

        character.actionsRemaining = 0;
        this.endCurrentTurn();
        return true;
    },

    moveGoblinShaman(character) {
        const healAbility = this.getAbilityForCharacter(character, 'mend-flesh');
        const poisonDartAbility = this.getAbilityForCharacter(character, 'poison-dart');
        if (!healAbility || !poisonDartAbility) {
            character.actionsRemaining = 0;
            character.bonusMovementRemaining = 0;
            this.endCurrentTurn();
            return true;
        }

        if (character.actionsRemaining >= character.attackCost && character.magicPoints >= healAbility.mpCost) {
            const healTarget = this.getNearbyHurtGoblin(character, healAbility.range ?? 3);
            if (healTarget) {
                character.selectedAbilityId = healAbility.id;
                return this.castHeal(character, healTarget);
            }
        }

        if (character.actionsRemaining >= character.attackCost && character.magicPoints >= (poisonDartAbility.mpCost ?? 0)) {
            const poisonTarget = this.getBestImmediateAttackTarget(character, poisonDartAbility);
            if (poisonTarget) {
                character.selectedAbilityId = poisonDartAbility.id;
                return this.castPoisonDart(character, poisonTarget, poisonDartAbility);
            }
        }

        const maxMoveSteps = this.getCharacterMovementBudget(character, character.attackCost);
        const reachablePositions = this.getReachablePositions(character, maxMoveSteps);
        let bestPosition = reachablePositions[0];
        let bestScore = Number.NEGATIVE_INFINITY;

        reachablePositions.forEach((candidate) => {
            const adjacentSupport = this.countAdjacentAlliesAt(character, candidate.x, candidate.y);
            const goblinCoverage = this.countGoblinCoverageAt(candidate.x, candidate.y, 2);
            const nearestAllyDistance = this.getDistanceToNearestGoblinAllyAt(character, candidate.x, candidate.y);
            const healTarget = character.magicPoints >= (healAbility.mpCost ?? 0)
                ? this.getNearbyHurtGoblinAt(character, healAbility.range ?? 3, candidate.x, candidate.y)
                : null;

            let poisonOptionScore = 0;
            if (character.magicPoints >= (poisonDartAbility.mpCost ?? 0)) {
                const poisonTarget = this.getLivingCharacters(this.getOpposingGroupForCharacter(character)).find((target) => {
                    const effect = this.getExpectedActionEffect(character, target, poisonDartAbility, candidate.x, candidate.y);
                    return Boolean(effect?.isValid);
                });
                if (poisonTarget) {
                    poisonOptionScore = 6;
                }
            }

            const score = (
                adjacentSupport * 4 +
                goblinCoverage * 2.5 +
                (healTarget ? 10 : 0) +
                poisonOptionScore -
                nearestAllyDistance * 3 -
                candidate.steps * 1.25
            );

            if (score > bestScore) {
                bestPosition = candidate;
                bestScore = score;
                return;
            }

            if (score === bestScore && candidate.steps < bestPosition.steps) {
                bestPosition = candidate;
            }
        });

        if (bestPosition?.path?.length > 0) {
            return this.moveCharacterToCell(character, bestPosition.path[0]);
        }

        character.actionsRemaining = 0;
        character.bonusMovementRemaining = 0;
        this.endCurrentTurn();
        return true;
    },

    moveSkeletonMage(character) {
        const graveChillAbility = this.getAbilityForCharacter(character, 'grave-chill');
        const nearestTarget = this.getNearestLivingOpponent(character);

        if (!nearestTarget) {
            return this.forceEndCurrentAITurn(character);
        }

        const canCastGraveChill = Boolean(
            graveChillAbility
            && character.actionsRemaining >= character.attackCost
            && character.magicPoints >= (graveChillAbility.mpCost ?? 0)
        );

        if (canCastGraveChill) {
            const immediateSpellTarget = this.getBestImmediateAttackTarget(character, graveChillAbility);
            character.selectedAbilityId = graveChillAbility.id;

            if (immediateSpellTarget && this.useAbilityOnTarget(character, graveChillAbility, immediateSpellTarget)) {
                return true;
            }

            const spellPath = this.findShortestPathToAttackPosition(character, nearestTarget, graveChillAbility);
            if (spellPath?.length > 0) {
                return this.moveCharacterToCell(character, spellPath[0]);
            }

            const spellAdvanceMove = this.chooseBestAdvanceMove(character, nearestTarget, graveChillAbility);
            if (spellAdvanceMove) {
                return this.moveCharacterToCell(character, spellAdvanceMove);
            }
        }

        const meleeFallback = this.getAbilityForCharacter(character, 'sword-slash') || this.getBestOffensiveAbility(character);
        if (!meleeFallback) {
            return this.forceEndCurrentAITurn(character);
        }

        character.selectedAbilityId = meleeFallback.id;
        const immediateMeleeTarget = this.getExpectedActionEffect(character, nearestTarget, meleeFallback)?.isValid
            ? nearestTarget
            : null;

        if (immediateMeleeTarget && this.characterAttack(character, immediateMeleeTarget, meleeFallback)) {
            return true;
        }

        const meleePath = this.findShortestPathToAttackPosition(character, nearestTarget, meleeFallback);
        if (meleePath?.length > 0) {
            return this.moveCharacterToCell(character, meleePath[0]);
        }

        const meleeAdvanceMove = this.chooseBestAdvanceMove(character, nearestTarget, meleeFallback);
        if (meleeAdvanceMove) {
            return this.moveCharacterToCell(character, meleeAdvanceMove);
        }

        return this.forceEndCurrentAITurn(character);
    },

    moveNecromancer(character) {
        const raiseUndeadAbility = this.getAbilityForCharacter(character, 'raise-undead');
        const nearestTarget = this.getNearestLivingOpponent(character);

        if (!nearestTarget) {
            return this.forceEndCurrentAITurn(character);
        }

        const canCastRaiseUndead = Boolean(
            raiseUndeadAbility
            && character.actionsRemaining >= character.attackCost
            && character.magicPoints >= (raiseUndeadAbility.mpCost ?? 0)
        );

        // Necromancer always prioritizes summoning while resources and placement allow it.
        if (canCastRaiseUndead) {
            character.selectedAbilityId = raiseUndeadAbility.id;
            if (this.useAbilityOnTarget(character, raiseUndeadAbility, null)) {
                return true;
            }
        }

        const fallbackMeleeAbility = {
            id: 'melee',
            name: 'Melee Strike',
            type: 'attack',
            range: 1,
            mpCost: 0,
            damage: 0
        };

        const shortestPath = this.findShortestPathToAttackPosition(character, nearestTarget, fallbackMeleeAbility);
        if (shortestPath?.length > 0) {
            return this.moveCharacterToCell(character, shortestPath[0]);
        }

        const fallbackMove = this.chooseBestAdvanceMove(character, nearestTarget, fallbackMeleeAbility);
        if (fallbackMove) {
            return this.moveCharacterToCell(character, fallbackMove);
        }

        return this.forceEndCurrentAITurn(character);
    },

    moveAIMeleeCharacter(character) {
        const ability = this.getBestOffensiveAbility(character);
        const immediateTarget = this.getBestImmediateAttackTarget(character, ability);
        if (immediateTarget && this.characterAttack(character, immediateTarget, ability)) {
            return true;
        }

        const plan = this.getBestMeleePlan(character, ability);
        if (plan?.position?.path?.length > 0) {
            return this.moveCharacterToCell(character, plan.position.path[0]);
        }

        const fallbackTarget = plan?.target || this.getBestOpponentTarget(character, ability) || this.getNearestLivingOpponent(character);
        if (!fallbackTarget) {
            this.endCurrentTurn();
            return true;
        }

        const fallbackMove = this.chooseBestAdvanceMove(character, fallbackTarget, ability);
        if (fallbackMove) {
            return this.moveCharacterToCell(character, fallbackMove);
        }

        character.actionsRemaining = 0;
        this.endCurrentTurn();
        return true;
    },

    moveGoblinFrontliner(character) {
        const ability = this.getBestOffensiveAbility(character);
        const immediateTarget = this.getBestImmediateAttackTarget(character, ability)
            || this.getBestFrontlineTarget(character, ability);

        if (immediateTarget && this.characterAttack(character, immediateTarget, ability)) {
            return true;
        }

        const plan = this.getBestMeleePlan(character, ability);
        if (plan?.position?.path?.length > 0) {
            return this.moveCharacterToCell(character, plan.position.path[0]);
        }

        const fallbackTarget = plan?.target || this.getBestFrontlineTarget(character, ability) || this.getNearestLivingOpponent(character);
        if (!fallbackTarget) {
            this.endCurrentTurn();
            return true;
        }

        const fallbackMove = this.chooseBestAdvanceMove(character, fallbackTarget, ability);
        if (fallbackMove) {
            return this.moveCharacterToCell(character, fallbackMove);
        }

        character.actionsRemaining = 0;
        this.endCurrentTurn();
        return true;
    },

    moveWolfCompanion(character) {
        const ability = this.getAbilityForCharacter(character, 'wolf-bite') || this.getBestOffensiveAbility(character);
        const target = this.getNearestLivingOpponent(character);
        if (!ability || !target) {
            return this.forceEndCurrentAITurn(character);
        }

        const immediateTarget = this.getExpectedActionEffect(character, target, ability)?.isValid ? target : null;
        if (immediateTarget && this.characterAttack(character, immediateTarget, ability)) {
            return true;
        }

        const shortestPath = this.findShortestPathToAttackPosition(character, target, ability);
        if (shortestPath?.length > 0) {
            return this.moveCharacterToCell(character, shortestPath[0]);
        }

        const fallbackMove = this.chooseBestAdvanceMove(character, target, ability);
        if (fallbackMove) {
            return this.moveCharacterToCell(character, fallbackMove);
        }

        return this.forceEndCurrentAITurn(character);
    },

    forceEndCurrentAITurn(character) {
        if (!character || this.gameMode !== 'combat' || this.getActiveTurnCharacter() !== character) {
            return false;
        }

        character.actionsRemaining = 0;
        character.bonusMovementRemaining = 0;
        this.endCurrentTurn();
        return true;
    },

    moveAICharacter(character) {
        if (
            this.isGameOver ||
            !character ||
            character.isDead ||
            this.getActiveTurnCharacter() !== character
        ) {
            return false;
        }

        if (this.getCharacterMovementBudget(character) <= 0) {
            return this.forceEndCurrentAITurn(character);
        }

        const actions = this.getCharacterActionList(character);
        const canBow = actions.some((ability) => ability.id === 'bow-shot');
        const goblinRole = this.getGoblinRole(character);

        let didAct = false;

        if (character.isSummonedWolf) {
            didAct = this.moveWolfCompanion(character);
        } else if ((character.id || '').includes('necromancer')) {
            didAct = this.moveNecromancer(character);
        } else if ((character.id || '').includes('skeleton-mage')) {
            didAct = this.moveSkeletonMage(character);
        } else if (goblinRole === 'warrior' || goblinRole === 'brute') {
            didAct = this.moveGoblinFrontliner(character);
        } else if (goblinRole === 'shaman') {
            didAct = this.moveGoblinShaman(character);
        } else if (canBow) {
            didAct = this.moveGoblinArcher(character);
        } else {
            didAct = this.moveAIMeleeCharacter(character);
        }

        if (didAct) {
            return true;
        }

        return this.forceEndCurrentAITurn(character);
    }
};
