using Godot;
using Godot.Collections;
using System.Threading.Tasks;

public partial class BattleController
{
    private enum MapTransitionOutcome
    {
        None,
        Stayed,
        Transitioned
    }

    // Architecture: Map/encounter orchestration only.
    private void EnterExplorationMode(string statusText = null)
    {
        ResetPlayerAbilityCooldowns();
        ClearCombatOnlyStatusEffectsForParty();
        _flowState = BattleFlowState.Exploration;
        _awaitingPlayerAttackDirection = false;
        _awaitingShadowStrikeAttack = false;
        _awaitingItemIdentifyTarget = false;
        _shadowStrikeDestination = new Vector2I(-1, -1);
        ClearMovementPreviewPath();
        PruneInvalidUnitReferences();
        _explorerUnit = GetExplorerUnit();
        UpdateFogOfWar();

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
            SyncHudFromGameState();
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

            var aggroRange = enemy.AggroRange;

            foreach (var player in _playerUnits)
            {
                if (!IsUsableUnit(player) || player.IsDead)
                {
                    continue;
                }

                var enemyCell = enemy.GetClosestCell(player.GridPos);
                if (Manhattan(player.GetClosestCell(enemyCell), enemyCell) <= aggroRange
                    && HasClearUnitLineOfSight(enemy, player))
                {
                    StartCombat(enemy);
                    return;
                }
            }
        }
    }

    private async void StartCombat(Unit triggeringEnemy)
    {
        if (_flowState != BattleFlowState.Exploration)
        {
            return;
        }

        if (!IsUsableUnit(triggeringEnemy) || triggeringEnemy.IsDead || triggeringEnemy.Team != "enemy")
        {
            return;
        }

        if (!_isRestoringCombatSave)
        {
            SaveRecoveryCheckpoint();
        }
        PruneInvalidUnitReferences();

        _activeCombatEnemyUnitIds.Clear();
        _activeCombatEncounterIds.Clear();
        AddChainedAggroEnemies(triggeringEnemy);

        if (_activeCombatEnemyUnitIds.Count == 0)
        {
            return;
        }

        _activeEncounterId = triggeringEnemy.EncounterId;
        _flowState = BattleFlowState.CombatIntro;
        var requestId = ++_combatIntroRequestId;
        var mapId = _currentMapId;
        var encounterId = _activeEncounterId;
        _explorationMoveRequestId++;
        _hasPendingExplorationClickMove = false;
        _hasPendingExplorationManualMove = false;
        ClearMovementPreviewPath();
        _awaitingPlayerAttackDirection = false;
        _awaitingShadowStrikeAttack = false;
        _awaitingItemIdentifyTarget = false;
        _shadowStrikeDestination = new Vector2I(-1, -1);
        EnlistNearbyCombatEnemies();
        _eventBus?.EmitSignal(EventBus.SignalName.CombatStarted);

        if (TryGetCombatIntroPages(encounterId, out var title, out var pages))
        {
            foreach (var unit in _allUnits)
            {
                if (IsUsableUnit(unit))
                {
                    unit.SetActive(false);
                }
            }
            SyncHudFromGameState();
            for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            {
                var next = await ShowCombatIntroPageAsync(
                    title, GetString(pages[pageIndex], "text", ""), pageIndex < pages.Count - 1);
                if (!IsInsideTree() || requestId != _combatIntroRequestId
                    || _flowState != BattleFlowState.CombatIntro || mapId != _currentMapId)
                {
                    return;
                }
                if (!next)
                {
                    break;
                }
            }

            if (!_completedCombatIntroEncounterIdsByMap.TryGetValue(mapId, out var completed))
            {
                completed = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
                _completedCombatIntroEncounterIdsByMap[mapId] = completed;
            }
            completed.Add(encounterId);
            _persistence?.PersistSaveGame(false);
            BeginPostPlayerActionMouseMoveLock();
        }

        _flowState = BattleFlowState.Combat;
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
        _hud?.ShowCombatBanner("COMBAT START", new Color(0.95f, 0.42f, 0.32f, 1.0f));
        var enemyCount = _activeCombatEnemyUnitIds.Count;
        var enemyUnitLabel = enemyCount == 1 ? "enemy" : "enemies";
        _hud?.AddCombatLogEntry($"Combat started. {enemyCount} {enemyUnitLabel} engaged.");
        SetStatusHelp();
        CenterViewOnCurrentFocus();
    }

    private void CancelPendingCombatIntro()
    {
        _combatIntroRequestId++;
        if (IsInstanceValid(_combatIntroDialog))
        {
            _combatIntroDialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
        }
    }

    private void SaveRecoveryCheckpoint()
    {
        if (_recoveryPersistence == null || !_recoveryPersistence.TryPersistSaveGame())
        {
            RejectPlayerAction("Could not save the recovery checkpoint. Your previous checkpoint is still available.");
        }
    }

    private void ShowDefeatRecoveryDialog()
    {
        if (IsInstanceValid(_defeatRecoveryDialog) && _defeatRecoveryDialog.Visible)
        {
            return;
        }

        var dialog = new AcceptDialog
        {
            Title = "The Party Has Fallen",
            DialogText = "Your party has been defeated. Choose how you would like to continue.",
            Exclusive = true,
            Unresizable = true
        };
        dialog.GetOkButton().Text = "Respawn in Forest Town";
        var recoveryButton = dialog.AddButton("Load Recovery Save", true, "load_recovery");
        recoveryButton.Disabled = _recoveryPersistence == null || !_recoveryPersistence.HasSaveGame();
        recoveryButton.TooltipText = recoveryButton.Disabled
            ? "No recovery save is available."
            : "Return to the state saved before the last combat.";
        dialog.AddButton("Main Menu", true, "main_menu");
        AddChild(dialog);
        TacticalTheme.ApplyDialog(dialog);
        _defeatRecoveryDialog = dialog;
        dialog.Confirmed += HandleDefeatRespawnRequested;
        dialog.CustomAction += HandleDefeatRecoveryCustomAction;
        dialog.CloseRequested += HandleDefeatDialogCloseRequested;
        dialog.PopupCentered(new Vector2I(620, 280));
    }

    private void HandleDefeatRespawnRequested()
    {
        CloseDefeatRecoveryDialog();
        RespawnPartyInForestTown();
    }

    private void HandleDefeatDialogCloseRequested()
    {
        HandleDefeatRespawnRequested();
    }

    private void HandleDefeatRecoveryCustomAction(StringName action)
    {
        if (action == "load_recovery")
        {
            if (_recoveryPersistence == null || !_recoveryPersistence.TryLoadSaveGame(false))
            {
                RejectPlayerAction("The recovery save could not be loaded.");
                return;
            }

            CloseDefeatRecoveryDialog();
            _persistence?.PersistSaveGame(false);
            _hud?.ShowCombatBanner("RECOVERY LOADED", TacticalTheme.BrassBright);
            return;
        }

        if (action == "main_menu")
        {
            var error = GetTree().ChangeSceneToFile(StartScreenPath);
            if (error != Error.Ok)
            {
                GD.PushError($"Could not return to the main menu: {error}.");
                RejectPlayerAction("Could not return to the main menu.");
                if (IsInstanceValid(_defeatRecoveryDialog))
                {
                    _defeatRecoveryDialog.PopupCentered(new Vector2I(620, 280));
                }
                return;
            }

            CloseDefeatRecoveryDialog();
        }
    }

    private void CloseDefeatRecoveryDialog()
    {
        if (!IsInstanceValid(_defeatRecoveryDialog))
        {
            return;
        }

        _defeatRecoveryDialog.Confirmed -= HandleDefeatRespawnRequested;
        _defeatRecoveryDialog.CustomAction -= HandleDefeatRecoveryCustomAction;
        _defeatRecoveryDialog.CloseRequested -= HandleDefeatDialogCloseRequested;
        _defeatRecoveryDialog.Hide();
        _defeatRecoveryDialog.QueueFree();
        _defeatRecoveryDialog = null;
    }

    private void RespawnPartyInForestTown()
    {
        if (_playerUnits.Count == 0)
        {
            RejectPlayerAction("No party members are available to respawn.");
            ShowDefeatRecoveryDialog();
            return;
        }

        foreach (var unit in _playerUnits)
        {
            if (!IsUsableUnit(unit))
            {
                continue;
            }

            if (unit.IsDead)
            {
                unit.Resurrect();
            }
            unit.RestoreFromRest();
            unit.ClearNegativeStatusEffects();
            unit.ClearStatusEffectsByScope("combat_only");
            unit.ClearAbilityCooldowns();
            unit.ClearDefending();
        }

        var townData = _mapLoader?.LoadMapStub("forest-town") ?? new MapLoader().LoadMapStub("forest-town");
        var spawnCell = new Vector2I(9, 9);
        var playerSpawns = TryGetDictionaryArray(townData, "players");
        if (playerSpawns.Count > 0)
        {
            spawnCell = GetVector2I(playerSpawns[0], "grid_pos", spawnCell);
        }

        _activeCombatEnemyUnitIds.Clear();
        _activeCombatEncounterIds.Clear();
        _isEndingTurn = false;
        _isEnemyTurnProcessing = false;
        SpawnMapEncounter("forest-town", preserveParty: true, leadSpawnCell: spawnCell);
        EnterExplorationMode("Your party has been restored in Forest Town.");
        _eventBus?.EmitSignal(EventBus.SignalName.CombatEnded);
        _persistence?.PersistSaveGame(false);
        SaveRecoveryCheckpoint();
        _hud?.ShowCombatBanner("PARTY RESTORED", new Color(0.42f, 0.88f, 0.56f, 1.0f));
        QueueRedraw();
    }

    private bool TryGetCombatIntroPages(string encounterId, out string title, out Array<Dictionary> pages)
    {
        title = "";
        pages = new Array<Dictionary>();
        if (!_combatIntroDialogueIdsByEncounter.TryGetValue(encounterId, out var dialogueId)
            || (_completedCombatIntroEncounterIdsByMap.TryGetValue(_currentMapId, out var completed)
                && completed.Contains(encounterId)))
        {
            return false;
        }

        var dialogue = _gameData?.GetDialogue(dialogueId) ?? new Dictionary();
        title = GetString(dialogue, "title", "Combat");
        pages = TryGetDictionaryArray(dialogue, "pages");
        var valid = pages.Count > 0;
        foreach (var page in pages)
        {
            valid &= !string.IsNullOrWhiteSpace(GetString(page, "text", "")) && !page.ContainsKey("choices");
        }
        if (!valid)
        {
            GD.PushError($"Encounter '{_currentMapId}/{encounterId}' references invalid combat intro '{dialogueId}'. Supply nonempty text pages without NPC choices.");
            RejectPlayerAction("Combat intro could not be loaded.");
        }
        return valid;
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

    private void AddChainedAggroEnemies(Unit triggeringEnemy)
    {
        var queue = new System.Collections.Generic.Queue<Unit>();
        _activeCombatEnemyUnitIds.Add(triggeringEnemy.UnitId);
        if (_flowState == BattleFlowState.Combat)
        {
            _turnManager?.AddUnit(triggeringEnemy);
        }
        if (!string.IsNullOrEmpty(triggeringEnemy.EncounterId))
        {
            _activeCombatEncounterIds.Add(triggeringEnemy.EncounterId);
        }
        queue.Enqueue(triggeringEnemy);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var chainRange = current.AggroRange;
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
                if (_flowState == BattleFlowState.Combat)
                {
                    _turnManager?.AddUnit(candidate);
                }
                if (!string.IsNullOrEmpty(candidate.EncounterId))
                {
                    _activeCombatEncounterIds.Add(candidate.EncounterId);
                }
                queue.Enqueue(candidate);
            }
        }
    }

    private void EnlistNearbyCombatEnemies()
    {
        if (_flowState is not (BattleFlowState.Combat or BattleFlowState.CombatIntro))
        {
            return;
        }

        foreach (var enemy in _enemyUnits)
        {
            if (!IsUsableUnit(enemy) || enemy.IsDead || enemy.Team != "enemy" || string.IsNullOrEmpty(enemy.UnitId)
                || _activeCombatEnemyUnitIds.Contains(enemy.UnitId))
            {
                continue;
            }

            foreach (var player in _playerUnits)
            {
                if (!IsUsableUnit(player) || player.IsDead)
                {
                    continue;
                }

                var enemyCell = enemy.GetClosestCell(player.GridPos);
                if (Manhattan(player.GetClosestCell(enemyCell), enemyCell) <= enemy.AggroRange
                    && HasClearUnitLineOfSight(enemy, player))
                {
                    AddChainedAggroEnemies(enemy);
                    break;
                }
            }
        }
    }

    private void EnlistCombatEnemy(Unit enemy)
    {
        if (_flowState != BattleFlowState.Combat || !IsUsableUnit(enemy) || enemy.IsDead
            || enemy.Team != "enemy" || string.IsNullOrEmpty(enemy.UnitId)
            || _activeCombatEnemyUnitIds.Contains(enemy.UnitId))
        {
            return;
        }

        AddChainedAggroEnemies(enemy);
    }

    private async Task<MapTransitionOutcome> TryHandleMapTransitionAsync()
    {
        if (_flowState != BattleFlowState.Exploration)
        {
            return MapTransitionOutcome.None;
        }

        var explorer = GetExplorerUnit();
        if (explorer == null)
        {
            return MapTransitionOutcome.None;
        }

        if (_mapLoader != null && _mapLoader.TryGetTransitionForCell(_mapTransitions, explorer.GridPos, _currentMapId, out var toMap, out var spawnCell))
        {
            var shouldTransition = await ConfirmMapTransitionAsync(toMap);
            if (!shouldTransition)
            {
                return MapTransitionOutcome.Stayed;
            }

            TransitionToMap(toMap, spawnCell);
            return MapTransitionOutcome.Transitioned;
        }

        return MapTransitionOutcome.None;
    }

    private async Task<bool> ConfirmMapTransitionAsync(string toMapId)
    {
        var mapName = GetMapDisplayName(toMapId);
        var confirmation = new ConfirmationDialog
        {
            Title = "Map Transition",
            DialogText = $"Travel to {mapName}?"
        };

        confirmation.GetOkButton().Text = "Travel";
        confirmation.AddCancelButton("Stay");
        AddChild(confirmation);
        TacticalTheme.ApplyDialog(confirmation);

        var completion = new TaskCompletionSource<bool>();

        void HandleConfirmed()
        {
            completion.TrySetResult(true);
        }

        void HandleCanceled()
        {
            completion.TrySetResult(false);
        }

        confirmation.Confirmed += HandleConfirmed;
        confirmation.Canceled += HandleCanceled;
        confirmation.CloseRequested += HandleCanceled;

        try
        {
            confirmation.PopupCentered();
            return await completion.Task;
        }
        finally
        {
            confirmation.Confirmed -= HandleConfirmed;
            confirmation.Canceled -= HandleCanceled;
            confirmation.CloseRequested -= HandleCanceled;
            confirmation.QueueFree();
        }
    }

    private async Task<bool> ConfirmRestPointAsync(string restPointName)
    {
        var confirmation = new ConfirmationDialog
        {
            Title = "Rest Point",
            DialogText = $"Rest at {restPointName} and restore the party to full health and magic?"
        };

        confirmation.GetOkButton().Text = "Rest";
        confirmation.AddCancelButton("Not now");
        AddChild(confirmation);
        TacticalTheme.ApplyDialog(confirmation);

        var completion = new TaskCompletionSource<bool>();

        void HandleConfirmed()
        {
            completion.TrySetResult(true);
        }

        void HandleCanceled()
        {
            completion.TrySetResult(false);
        }

        confirmation.Confirmed += HandleConfirmed;
        confirmation.Canceled += HandleCanceled;
        confirmation.CloseRequested += HandleCanceled;

        try
        {
            confirmation.PopupCentered();
            return await completion.Task;
        }
        finally
        {
            confirmation.Confirmed -= HandleConfirmed;
            confirmation.Canceled -= HandleCanceled;
            confirmation.CloseRequested -= HandleCanceled;
            confirmation.QueueFree();
        }
    }

    private static string GetMapDisplayName(string mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId))
        {
            return "Unknown";
        }

        if (MapTokenCatalog.Maps.TryGetValue(mapId, out var definition) && !string.IsNullOrWhiteSpace(definition.Name))
        {
            return definition.Name;
        }

        return mapId.Replace("-", " ");
    }

    private void TransitionToMap(string toMapId, Vector2I spawnCell)
    {
        var mapName = GetMapDisplayName(toMapId);
        SaveClearedEncounterStateForCurrentMap();
        SpawnMapEncounter(toMapId, preserveParty: true, leadSpawnCell: spawnCell);
        _hud?.ShowCombatBanner($"ZONE: {mapName}", TacticalTheme.BrassBright);
        _hud?.AddCombatLogEntry($"Entered {mapName}.");
        EnterExplorationMode($"Transitioned to {mapName}. Keep exploring.");
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
