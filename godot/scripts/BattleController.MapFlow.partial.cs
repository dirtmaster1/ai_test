using Godot;
using Godot.Collections;

public partial class BattleController
{
    // Architecture: Map/encounter orchestration only.
    private void EnterExplorationMode(string statusText = null)
    {
        _flowState = BattleFlowState.Exploration;
        _awaitingPlayerAttackDirection = false;
        ClearMovementPreviewPath();
        PruneInvalidUnitReferences();
        _explorerUnit = GetExplorerUnit();

        foreach (var unit in _allUnits)
        {
            if (!IsUsableUnit(unit))
            {
                continue;
            }

            unit.SetActive(false);
        }

        if (!string.IsNullOrEmpty(statusText))
        {
        }
        else
        {
            SetStatusHelp();
        }

        CenterViewOnCurrentFocus();
    }

    private void TryStartCombatFromAggro()
    {
        if (_flowState != BattleFlowState.Exploration)
        {
            return;
        }

        PruneInvalidUnitReferences();

        foreach (var enemy in _enemyUnits)
        {
            if (!IsUsableUnit(enemy) || enemy.IsDead)
            {
                continue;
            }

            var encounterId = enemy.EncounterId;
            if (string.IsNullOrEmpty(encounterId) || _clearedEncounterIds.Contains(encounterId))
            {
                continue;
            }

            var aggroRange = GetEncounterAggroRange(encounterId);

            foreach (var player in _playerUnits)
            {
                if (!IsUsableUnit(player) || player.IsDead)
                {
                    continue;
                }

                if (Manhattan(player.GridPos, enemy.GridPos) <= aggroRange)
                {
                    StartCombat(enemy);
                    return;
                }
            }
        }
    }

    private void StartCombat(Unit triggeringEnemy)
    {
        if (_flowState == BattleFlowState.Combat)
        {
            return;
        }

        if (!IsUsableUnit(triggeringEnemy) || triggeringEnemy.IsDead || triggeringEnemy.Team != "enemy")
        {
            return;
        }

        PruneInvalidUnitReferences();

        _activeCombatEnemyUnitIds.Clear();
        _activeCombatEncounterIds.Clear();
        AddChainedAggroEnemies(triggeringEnemy, 4);

        if (_activeCombatEnemyUnitIds.Count == 0)
        {
            return;
        }

        _activeEncounterId = triggeringEnemy.EncounterId;
        _flowState = BattleFlowState.Combat;
        _awaitingPlayerAttackDirection = false;
        _eventBus?.EmitSignal(EventBus.SignalName.CombatStarted);

        var combatUnits = new Array<Unit>();
        foreach (var player in _playerUnits)
        {
            if (IsUsableUnit(player) && !player.IsDead)
            {
                combatUnits.Add(player);
            }
        }

        foreach (var enemy in _enemyUnits)
        {
            if (IsEnemyInActiveCombat(enemy))
            {
                combatUnits.Add(enemy);
            }
        }

        _turnManager.SetupTurnOrder(combatUnits);
        SetStatusHelp();
        CenterViewOnCurrentFocus();
    }

    private void StartCombat(string encounterId)
    {
        foreach (var enemy in _enemyUnits)
        {
            if (IsUsableUnit(enemy) && !enemy.IsDead && enemy.EncounterId == encounterId)
            {
                StartCombat(enemy);
                return;
            }
        }
    }

    private void AddChainedAggroEnemies(Unit triggeringEnemy, int chainRange)
    {
        var queue = new System.Collections.Generic.Queue<Unit>();
        _activeCombatEnemyUnitIds.Add(triggeringEnemy.UnitId);
        if (!string.IsNullOrEmpty(triggeringEnemy.EncounterId))
        {
            _activeCombatEncounterIds.Add(triggeringEnemy.EncounterId);
        }
        queue.Enqueue(triggeringEnemy);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var candidate in _enemyUnits)
            {
                if (!IsUsableUnit(candidate) || candidate.IsDead || candidate.Team != "enemy" || string.IsNullOrEmpty(candidate.UnitId))
                {
                    continue;
                }

                if (_activeCombatEnemyUnitIds.Contains(candidate.UnitId))
                {
                    continue;
                }

                if (Manhattan(current.GridPos, candidate.GridPos) > chainRange)
                {
                    continue;
                }

                _activeCombatEnemyUnitIds.Add(candidate.UnitId);
                if (!string.IsNullOrEmpty(candidate.EncounterId))
                {
                    _activeCombatEncounterIds.Add(candidate.EncounterId);
                }
                queue.Enqueue(candidate);
            }
        }
    }

    private bool TryHandleMapTransition()
    {
        if (_flowState != BattleFlowState.Exploration)
        {
            return false;
        }

        var explorer = GetExplorerUnit();
        if (explorer == null)
        {
            return false;
        }

        if (_mapLoader != null && _mapLoader.TryGetTransitionForCell(_mapTransitions, explorer.GridPos, _currentMapId, out var toMap, out var spawnCell))
        {
            TransitionToMap(toMap, spawnCell);
            return true;
        }

        return false;
    }

    private void TransitionToMap(string toMapId, Vector2I spawnCell)
    {
        SaveClearedEncounterStateForCurrentMap();
        SpawnMapEncounter(toMapId, preserveParty: true, leadSpawnCell: spawnCell);
        EnterExplorationMode($"Transitioned to {toMapId}. Keep exploring.");
        _persistence.PersistSaveGame(false);
        QueueRedraw();
    }

    private Unit GetExplorerUnit()
    {
        PruneInvalidUnitReferences();

        if (IsUsableUnit(_explorerUnit) && !_explorerUnit.IsDead && _explorerUnit.Team == "player")
        {
            return _explorerUnit;
        }

        foreach (var unit in _playerUnits)
        {
            if (IsUsableUnit(unit) && !unit.IsDead)
            {
                _explorerUnit = unit;
                return unit;
            }
        }

        return null;
    }
}
