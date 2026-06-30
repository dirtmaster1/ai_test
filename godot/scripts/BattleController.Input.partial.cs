using Godot;

public partial class BattleController
{
    // Architecture: Input orchestration only (keyboard/mouse -> high-level intent).
    public override void _Input(InputEvent @event)
    {
        if (_flowState == BattleFlowState.Defeat)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseEvent)
        {
            HandleMouseInput(mouseEvent);
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            HandleMouseHoverInput(mouseMotion);
            return;
        }

        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        if (keyEvent.Keycode == Key.I)
        {
            _hud?.ToggleInventoryVisible();
            SyncHudFromGameState();
            return;
        }

        if (keyEvent.Keycode == Key.H)
        {
            _hud?.ToggleHelpVisible();
            return;
        }

        if (keyEvent.Keycode == Key.Tab && _hud != null && _hud.IsInventoryVisible())
        {
            CycleInventoryTarget(keyEvent.ShiftPressed ? -1 : 1);
            SyncHudFromGameState();
            return;
        }

        if (_flowState == BattleFlowState.Exploration)
        {
            HandleExplorationInput(keyEvent);
            return;
        }

        if (_awaitingPlayerAttackDirection)
        {
            HandlePlayerAttackDirectionInput(keyEvent);
            return;
        }

        if (keyEvent.Keycode == Key.Space)
        {
            var activePlayer = GetActivePlayerUnit();
            if (TryRequestEndTurn(activePlayer, manualInput: true))
            {
                _awaitingPlayerAttackDirection = false;
                ClearMovementPreviewPath();
                _hud?.SetSelectedAction("None");
                QueueRedraw();
            }
            return;
        }

        var active = GetActivePlayerUnit();
        if (active == null)
        {
            return;
        }

        if (keyEvent.Keycode == Key.F)
        {
            if (!active.CanUseAbilityThisTurn())
            {
                _hud?.SetStatusText("Ability already used this turn.");
                return;
            }

            var selectedAbilityId = GetSelectedAbilityId(active);
            if (string.IsNullOrEmpty(selectedAbilityId) || !active.HasAbility(selectedAbilityId))
            {
                _hud?.SetStatusText("No ability selected for this unit.");
                return;
            }

            var cooldownRemaining = active.GetAbilityCooldownRemaining(selectedAbilityId);
            if (cooldownRemaining > 0)
            {
                _hud?.SetStatusText($"{selectedAbilityId} is on cooldown ({cooldownRemaining} turn{(cooldownRemaining == 1 ? "" : "s")} remaining).");
                return;
            }

            var actionProfile = ResolveActionProfile(active, selectedAbilityId);
            if (!CanCastAction(active, actionProfile, true))
            {
                return;
            }

            _awaitingPlayerAttackDirection = true;
            SetSelectedAbilityId(active, selectedAbilityId);
            _hud?.SetStatusText("Choose attack direction: WASD / Arrows (Esc to cancel)");
            QueueRedraw();
            return;
        }

        var delta = KeyToDelta(keyEvent.Keycode);
        if (delta == Vector2I.Zero)
        {
            return;
        }

        var moveResult = ResolveMoveAction(active, active.GridPos + delta, endTurnOnSuccess: false);
        ApplyActionResult(moveResult);
    }

    private void HandleMouseInput(InputEventMouseButton mouseEvent)
    {
        if (_awaitingPlayerAttackDirection)
        {
            HandleMouseAttackInput(mouseEvent);
            return;
        }

        if (mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            var clickedCell = WorldToCell(GetGlobalMousePosition());
            if (_flowState == BattleFlowState.Exploration && TryOpenExplorationInteractionAtCell(clickedCell))
            {
                return;
            }

            if (TrySelectCharacterAtCell(clickedCell))
            {
                return;
            }
        }

        HandleMouseMoveInput(mouseEvent);
    }

    private void HandleMouseHoverInput(InputEventMouseMotion _mouseMotion)
    {
        QueueRedraw();

        if (_flowState != BattleFlowState.Combat || _awaitingPlayerAttackDirection)
        {
            ClearMovementPreviewPath();
            return;
        }

        var active = GetActivePlayerUnit();
        if (active == null || !active.CanMoveThisTurn())
        {
            ClearMovementPreviewPath();
            return;
        }

        var hoveredCell = WorldToCell(GetGlobalMousePosition());
        if (!IsInBounds(hoveredCell) || hoveredCell == active.GridPos)
        {
            ClearMovementPreviewPath();
            return;
        }

        var previewPath = FindPath(active, active.GridPos, hoveredCell, active.RemainingMovement);
        if (previewPath.Count == 0)
        {
            SetMovementHoverState(hoveredCell, reachable: false, pathCost: -1);
            ClearMovementPreviewPath();
            return;
        }

        SetMovementHoverState(hoveredCell, reachable: true, pathCost: previewPath.Count);
        SetMovementPreviewPath(previewPath);
    }

    private void HandleMouseMoveInput(InputEventMouseButton mouseEvent)
    {
        if (_flowState != BattleFlowState.Combat || !mouseEvent.Pressed || mouseEvent.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        var active = GetActivePlayerUnit();
        if (active == null)
        {
            return;
        }

        var clickedCell = WorldToCell(GetGlobalMousePosition());
        if (!IsInBounds(clickedCell))
        {
            return;
        }

        if (clickedCell == active.GridPos)
        {
            return;
        }

        if (!active.CanMoveThisTurn())
        {
            _hud?.SetStatusText("No movement left this turn.");
            return;
        }

        var path = FindPath(active, active.GridPos, clickedCell, active.RemainingMovement);
        if (path.Count == 0)
        {
            _hud?.SetStatusText($"Cannot path to that cell within {active.RemainingMovement} move.");
            return;
        }

        foreach (var step in path)
        {
            var moveResult = ResolveMoveAction(active, step, endTurnOnSuccess: false);
            if (!moveResult.Success)
            {
                break;
            }

            ApplyActionResult(moveResult);
        }

        ClearMovementPreviewPath();
        SetStatusHelp();
    }

    private void HandleExplorationInput(InputEventKey keyEvent)
    {
        var explorer = GetExplorerUnit();
        if (explorer == null)
        {
            _hud?.SetStatusText("No living player unit available to explore.");
            return;
        }

        var delta = KeyToDelta(keyEvent.Keycode);
        if (delta == Vector2I.Zero)
        {
            return;
        }

        if (!TryMoveExplorationParty(delta))
        {
            return;
        }

        if (TryHandleMapTransition())
        {
            return;
        }

        SetStatusHelp();
        TryStartCombatFromAggro();
    }

    private void HandleMouseAttackInput(InputEventMouseButton mouseEvent)
    {
        if (!_awaitingPlayerAttackDirection || !mouseEvent.Pressed)
        {
            return;
        }

        if (mouseEvent.ButtonIndex == MouseButton.Right)
        {
            CancelAttackMode();
            return;
        }

        if (mouseEvent.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        var active = GetActivePlayerUnit();
        if (active == null)
        {
            CancelAttackMode(false);
            return;
        }

        var actionProfile = ResolveActionProfile(active, GetSelectedAbilityId(active));
        var clickedCell = WorldToCell(GetGlobalMousePosition());
        if (!Unit.IsWithinRange(active.GridPos, clickedCell, actionProfile.Range))
        {
            _hud?.SetStatusText($"Click a target cell within range ({actionProfile.Range}).");
            return;
        }

        CancelAttackMode(false);
        TryResolvePlayerActionAtCell(active, clickedCell);
    }

    private void HandlePlayerAttackDirectionInput(InputEventKey keyEvent)
    {
        if (keyEvent.Keycode == Key.Escape)
        {
            CancelAttackMode();
            return;
        }

        var active = GetActivePlayerUnit();
        if (active == null)
        {
            CancelAttackMode(false);
            return;
        }

        var delta = KeyToDelta(keyEvent.Keycode);
        if (delta == Vector2I.Zero)
        {
            return;
        }

        var actionProfile = ResolveActionProfile(active, GetSelectedAbilityId(active));
        if (!TryGetDirectionalActionTargetCell(active, delta, actionProfile, out var targetCell))
        {
            _hud?.SetStatusText($"No valid {actionProfile.ActionType} target in that direction.");
            return;
        }

        CancelAttackMode(false);
        TryResolvePlayerActionAtCell(active, targetCell);
    }
}
