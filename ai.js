// AI - enemy movement, pathfinding, and tactical decisions
window.GridAI = {

    getOpposingGroupForCharacter(character) {
        if (!character) {
            return [];
        }

        if (character.team === 'player') {
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

        if (character.team === 'player') {
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

    getAbilityForCharacter(character, abilityId = character?.selectedAbilityId) {
        if (!character || !abilityId) {
            return null;
        }

        return character.abilities.find((ability) => ability.id === abilityId) || null;
    },

    getBestOffensiveAbility(character) {
        if (!character?.abilities?.length) {
            return null;
        }

        const offensiveAbilities = character.abilities.filter((ability) =>
            (ability.type === 'attack' || ability.type === 'spell') &&
            ((ability.mpCost ?? 0) === 0 || character.magicPoints >= (ability.mpCost ?? 0))
        );

        if (offensiveAbilities.length === 0) {
            return null;
        }

        return offensiveAbilities.reduce((bestAbility, candidate) => {
            const bestDamage = bestAbility?.damage ?? character.meleeAttackDamage;
            const candidateDamage = candidate.damage ?? character.meleeAttackDamage;
            if (candidateDamage !== bestDamage) {
                return candidateDamage > bestDamage ? candidate : bestAbility;
            }
            return (candidate.range ?? 1) > (bestAbility?.range ?? 1) ? candidate : bestAbility;
        }, offensiveAbilities[0]);
    },

    getAbilityTargetTeam(ability) {
        if (!ability) {
            return 'enemy';
        }

        if (ability.type === 'heal' || ability.type === 'buff') {
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

            if (this.isObstacle(gridX, gridY)) {
                return false;
            }
        }

        return true;
    },

    requiresLineOfSight(ability) {
        if (!ability) {
            return false;
        }

        const range = ability.range ?? 1;
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
        const lineOfSightRequired = this.requiresLineOfSight(resolvedAbility);
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
        const amount = isSpell ? baseDamage : Math.max(0, baseDamage - target.armorClass);

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

    getTargetRolePriority(target) {
        const targetId = target?.id ?? '';
        if (targetId.includes('wizard') || targetId.includes('cleric')) {
            return 7;
        }
        if (targetId.includes('ranger') || targetId.includes('archer')) {
            return 5;
        }
        if (targetId.includes('shaman')) {
            return 4;
        }
        if (targetId.includes('dwarf') || targetId.includes('brute')) {
            return 2;
        }
        return 3;
    },

    scoreTargetForCharacter(character, target, ability = null) {
        const effect = this.getExpectedActionEffect(character, target, ability);
        if (!effect || !effect.correctTeam) {
            return Number.NEGATIVE_INFINITY;
        }

        const missingHp = target.maxHitPoints - target.hitPoints;
        const killBonus = effect.amount >= target.hitPoints ? 28 : 0;
        const immediateReachBonus = effect.withinRange ? 18 : 0;
        const pressureBonus = effect.distance <= (effect.ability.range ?? 1) + Math.max(0, character.actionsRemaining - character.attackCost)
            ? 8
            : 0;

        return (
            effect.amount * 8 +
            missingHp * 1.2 +
            this.getTargetRolePriority(target) * 3 +
            killBonus +
            immediateReachBonus +
            pressureBonus -
            effect.distance * 1.5 -
            target.armorClass * 1.2
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

    moveCharacterToCell(character, destination) {
        if (!character || !destination) {
            return false;
        }

        character.gridX = destination.x;
        character.gridY = destination.y;
        this.updateCharacterFacing(character, destination.facing);
        this.updateCharacterPosition(character);
        character.actionsRemaining -= 1;

        if (character.actionsRemaining <= 0) {
            this.endCurrentTurn();
        }

        return true;
    },

    getBestRangedPlan(character, ability) {
        const maxMoveSteps = Math.max(0, character.actionsRemaining - character.attackCost);
        const reachablePositions = this.getReachablePositions(character, maxMoveSteps);
        const preferredRange = Math.max(2, ability.range ?? 1);
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
                const score = baseTargetScore + safety * 2 - distanceDelta * 2.5 - candidatePosition.steps * 1.2;

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
        const maxMoveSteps = Math.max(0, character.actionsRemaining - character.attackCost);
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
            const desiredRange = ability?.range ?? 1;
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
        const bowAbility = character.abilities.find((ability) => ability.id === 'bow-shot');
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

        const fallbackMove = this.chooseBestAdvanceMove(character, fallbackTarget, bowAbility);
        if (fallbackMove) {
            return this.moveCharacterToCell(character, fallbackMove);
        }

        character.actionsRemaining = 0;
        this.endCurrentTurn();
        return true;
    },

    moveGoblinShaman(character) {
        const healAbility = character.abilities.find((ability) => ability.id === 'heal');
        const buffAbility = character.abilities.find((ability) => ability.id === 'inflict-pain');
        if (!healAbility || !buffAbility) {
            character.actionsRemaining = 0;
            this.endCurrentTurn();
            return true;
        }

        if (character.actionsRemaining < character.attackCost) {
            character.actionsRemaining = 0;
            this.endCurrentTurn();
            return true;
        }

        if (character.magicPoints >= healAbility.mpCost) {
            const healTarget = this.getNearbyHurtGoblin(character, healAbility.range ?? 3);
            if (healTarget) {
                character.selectedAbilityId = healAbility.id;
                return this.castHeal(character, healTarget);
            }
        }

        if (character.magicPoints < (buffAbility.mpCost ?? 0)) {
            character.actionsRemaining = 0;
            this.endCurrentTurn();
            return true;
        }

        character.selectedAbilityId = buffAbility.id;

        const maxMoveSteps = Math.max(0, character.actionsRemaining - character.attackCost);
        const reachablePositions = this.getReachablePositions(character, maxMoveSteps);
        const buffRange = buffAbility.range ?? 2;

        let bestPosition = reachablePositions[0];
        let bestCoverage = this.countGoblinCoverageAt(bestPosition.x, bestPosition.y, buffRange);

        reachablePositions.forEach((candidate) => {
            const coverage = this.countGoblinCoverageAt(candidate.x, candidate.y, buffRange);
            if (coverage > bestCoverage) {
                bestCoverage = coverage;
                bestPosition = candidate;
                return;
            }

            if (coverage === bestCoverage && candidate.steps < bestPosition.steps) {
                bestPosition = candidate;
            }
        });

        if (bestPosition.path.length > 0) {
            return this.moveCharacterToCell(character, bestPosition.path[0]);
        }

        return this.castInflictPain(character);
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

    moveAICharacter(character) {
        if (
            this.isGameOver ||
            !character ||
            character.isDead ||
            this.getActiveTurnCharacter() !== character ||
            character.actionsRemaining <= 0
        ) {
            return;
        }

        const canBuff = character.abilities.some((ability) => ability.id === 'inflict-pain');
        const canBow = character.abilities.some((ability) => ability.id === 'bow-shot');

        if (canBuff) {
            this.moveGoblinShaman(character);
            return;
        }

        if (canBow) {
            this.moveGoblinArcher(character);
            return;
        }

        this.moveAIMeleeCharacter(character);
    }
};
