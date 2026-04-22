// AI - enemy movement, pathfinding, and tactical decisions
window.GridAI = {

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
    },

    getAttackDistanceBetweenPositions(fromX, fromY, toX, toY) {
        return Math.max(Math.abs(toX - fromX), Math.abs(toY - fromY));
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

    moveGoblinArcher(character, target) {
        const bowAbility = character.abilities.find((ability) => ability.id === 'bow-shot');
        if (!bowAbility) {
            return false;
        }

        character.selectedAbilityId = bowAbility.id;

        const bowRange = bowAbility.range ?? 1;
        const currentDistance = this.getAttackDistanceBetweenPositions(character.gridX, character.gridY, target.gridX, target.gridY);

        const adjacentMoves = this.getAdjacentMoves(character).map((move) => ({
            ...move,
            distanceToTarget: this.getAttackDistanceBetweenPositions(move.x, move.y, target.gridX, target.gridY)
        }));

        if (currentDistance === bowRange) {
            if (character.actionsRemaining >= character.attackCost) {
                return this.characterAttack(character, target, bowAbility);
            }

            const fallbackRetreatMoves = adjacentMoves
                .filter((move) => move.distanceToTarget > currentDistance)
                .sort((left, right) => right.distanceToTarget - left.distanceToTarget);

            if (fallbackRetreatMoves.length > 0) {
                return this.moveCharacterToCell(character, fallbackRetreatMoves[0]);
            }

            character.actionsRemaining = 0;
            this.endCurrentTurn();
            return true;
        }

        if (currentDistance < bowRange) {
            const retreatMoves = adjacentMoves
                .filter((move) => move.distanceToTarget > currentDistance && move.distanceToTarget <= bowRange)
                .sort((left, right) => right.distanceToTarget - left.distanceToTarget);

            if (retreatMoves.length > 0) {
                return this.moveCharacterToCell(character, retreatMoves[0]);
            }

            if (character.actionsRemaining >= character.attackCost) {
                return this.characterAttack(character, target, bowAbility);
            }

            character.actionsRemaining = 0;
            this.endCurrentTurn();
            return true;
        }

        const approachMoves = adjacentMoves
            .filter((move) => move.distanceToTarget < currentDistance)
            .sort((left, right) => {
                const leftDelta = Math.abs(left.distanceToTarget - bowRange);
                const rightDelta = Math.abs(right.distanceToTarget - bowRange);
                if (leftDelta !== rightDelta) {
                    return leftDelta - rightDelta;
                }
                return left.distanceToTarget - right.distanceToTarget;
            });

        if (approachMoves.length > 0) {
            return this.moveCharacterToCell(character, approachMoves[0]);
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

        const target = this.getNearestLivingOpponent(character);
        if (!target) {
            this.endCurrentTurn();
            return;
        }

        if (character === this.goblinShaman) {
            this.moveGoblinShaman(character);
            return;
        }

        if (character === this.goblinArcher) {
            this.moveGoblinArcher(character, target);
            return;
        }

        if (this.characterAttack(character, target)) {
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
                character.actionsRemaining = 0;
                this.endCurrentTurn();
                return;
            }
        }

        const moveDx = newX - character.gridX;
        const moveDy = newY - character.gridY;
        let facing = character.facing;

        if (moveDx > 0) {
            facing = 'right';
        } else if (moveDx < 0) {
            facing = 'left';
        } else if (moveDy > 0) {
            facing = 'down';
        } else if (moveDy < 0) {
            facing = 'up';
        }

        this.moveCharacterToCell(character, { x: newX, y: newY, facing });
    }
};
