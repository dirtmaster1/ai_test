using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class BattleController : Node2D
{
    private const int GridWidth = 20;
    private const int GridHeight = 15;
    private const int CellSize = 64;
    private const int DefaultAggroTriggerRange = 4;
    private const float GridLineThickness = 2.0f;
    private const ulong ManualEndTurnDebounceMs = 220;
    private const float EnemyActionDelaySeconds = 0.24f;

    private enum BattleFlowState
    {
        Exploration,
        Combat,
        Defeat
    }

    private Node2D _unitsRoot;
    private TurnManager _turnManager;
    private AiDirector _aiDirector;
    private MapLoader _mapLoader;
    private HudController _hud;
    private EventBus _eventBus;
    private GameData _gameData;

    private readonly PackedScene _unitScene = GD.Load<PackedScene>("res://scenes/Unit.tscn");
    private readonly Array<Unit> _allUnits = new();
    private readonly Array<Unit> _playerUnits = new();
    private readonly Array<Unit> _enemyUnits = new();
    private readonly Array<Vector2I> _blockedCells = new();
    private readonly Array<Dictionary> _mapTransitions = new();
    private readonly Array<Dictionary> _mapProps = new();
    private readonly Array<Dictionary> _lootBags = new();
    private readonly System.Collections.Generic.Dictionary<string, int> _encounterAggroRanges = new();
    private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>> _equippedItemsByUnitId = new();
    private readonly System.Collections.Generic.List<string> _partyInventoryItemIds = new();
    private readonly System.Collections.Generic.HashSet<string> _clearedEncounterIds = new();
    private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>> _clearedEncounterIdsByMap = new();
    private readonly System.Collections.Generic.HashSet<string> _openedPropIds = new();
    private readonly System.Collections.Generic.HashSet<string> _lootedBagIds = new();
    private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>> _openedPropIdsByMap = new();
    private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>> _lootedBagIdsByMap = new();
    private readonly System.Collections.Generic.Dictionary<string, Array<Dictionary>> _lootBagsByMap = new();
    private readonly RandomNumberGenerator _lootRng = new();

    private BattleFlowState _flowState = BattleFlowState.Exploration;
    private bool _awaitingPlayerAttackDirection;
    private Unit _explorerUnit;
    private string _activeEncounterId = "";
    private string _currentMapId = "map-a";
    private string _selectedInventoryUnitId = "";
    private readonly System.Collections.Generic.Dictionary<string, string> _selectedAbilityIdByUnitId = new();
    private string _lastActionSummary = "";
    private readonly Array<Vector2I> _movementPreviewPath = new();
    private bool _hasMovementHoverCell;
    private Vector2I _movementHoverCell = new(-1, -1);
    private bool _movementHoverReachable;
    private int _movementHoverCost = -1;
    private ulong _lastManualEndTurnAtMs;
    private bool _isEndingTurn;
    private bool _isEnemyTurnProcessing;

    private static readonly Vector2I[] AttackDirections =
    {
        new(0, -1),
        new(0, 1),
        new(-1, 0),
        new(1, 0)
    };

    private readonly struct CombatActionResult
    {
        public bool Success { get; }
        public bool ShouldEndTurn { get; }
        public bool CombatEnded { get; }

        private CombatActionResult(bool success, bool shouldEndTurn, bool combatEnded)
        {
            Success = success;
            ShouldEndTurn = shouldEndTurn;
            CombatEnded = combatEnded;
        }

        public static CombatActionResult Failed => new(false, false, false);
        public static CombatActionResult MoveResolved => new(true, false, false);
        public static CombatActionResult MoveAndEndTurnResolved => new(true, true, false);
        public static CombatActionResult PassResolved => new(true, true, false);
        public static CombatActionResult AttackResolved => new(true, false, false);
        public static CombatActionResult CombatResolvedResult => new(true, false, true);
    }

    private readonly struct ActionProfile
    {
        public string ActionId { get; }
        public string ActionName { get; }
        public string ActionType { get; }
        public int Range { get; }
        public int Damage { get; }
        public int HealAmount { get; }
        public int CooldownTurns { get; }
        public int MagicPointCost { get; }
        public bool IsMagical { get; }

        public ActionProfile(string actionId, string actionName, string actionType, int range, int damage, int healAmount, int cooldownTurns, int magicPointCost, bool isMagical)
        {
            ActionId = actionId;
            ActionName = actionName;
            ActionType = actionType;
            Range = range;
            Damage = damage;
            HealAmount = healAmount;
            CooldownTurns = cooldownTurns;
            MagicPointCost = magicPointCost;
            IsMagical = isMagical;
        }
    }

    // Lifecycle and rendering
    public override void _Ready()
    {
        _unitsRoot = GetNode<Node2D>("Units");
        _turnManager = GetNode<TurnManager>("TurnManager");
        _aiDirector = GetNode<AiDirector>("AiDirector");
        _mapLoader = GetNodeOrNull<MapLoader>("MapLoader");
        _hud = GetNodeOrNull<HudController>("HUD");
        _eventBus = GetNodeOrNull<EventBus>("/root/EventBus");
        _gameData = GetNodeOrNull<GameData>("/root/GameData");
        _lootRng.Randomize();

        SpawnMapEncounter(_currentMapId);
        _turnManager.TurnChanged += OnTurnChanged;
        if (_hud != null)
        {
            _hud.AbilityPressed += OnHudAbilityPressed;
            _hud.EndTurnPressed += OnHudEndTurnPressed;
            _hud.EquipItemRequested += OnHudEquipItemRequested;
            _hud.UnequipItemRequested += OnHudUnequipItemRequested;
            _hud.InventoryCycleRequested += OnHudInventoryCycleRequested;
            _hud.LootConfirmRequested += OnHudLootConfirmRequested;
        }

        EnterExplorationMode();
        RefreshHudPanels();
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        if (_turnManager != null)
        {
            _turnManager.TurnChanged -= OnTurnChanged;
        }

        if (_hud != null)
        {
            _hud.AbilityPressed -= OnHudAbilityPressed;
            _hud.EndTurnPressed -= OnHudEndTurnPressed;
            _hud.EquipItemRequested -= OnHudEquipItemRequested;
            _hud.UnequipItemRequested -= OnHudUnequipItemRequested;
            _hud.InventoryCycleRequested -= OnHudInventoryCycleRequested;
            _hud.LootConfirmRequested -= OnHudLootConfirmRequested;
        }
    }

    public override void _Draw()
    {
        _hud?.ClearWorldHoverTooltip();

        var viewportSize = GetViewportRect().Size;
        DrawRect(
            new Rect2(Vector2.Zero, viewportSize),
            new Color(0.05f, 0.05f, 0.06f),
            true
        );

        DrawRect(
            new Rect2(Vector2.Zero, new Vector2(GridWidth * CellSize, GridHeight * CellSize)),
            new Color(0.08f, 0.08f, 0.1f),
            true
        );

        for (var x = 0; x <= GridWidth; x++)
        {
            var lineX = x * CellSize + 0.5f;
            DrawLine(
                new Vector2(lineX, 0),
                new Vector2(lineX, GridHeight * CellSize),
                new Color(0.2f, 0.2f, 0.24f),
                GridLineThickness
            );
        }

        for (var y = 0; y <= GridHeight; y++)
        {
            var lineY = y * CellSize + 0.5f;
            DrawLine(
                new Vector2(0, lineY),
                new Vector2(GridWidth * CellSize, lineY),
                new Color(0.2f, 0.2f, 0.24f),
                GridLineThickness
            );
        }

        _mapLoader?.DrawMapFeaturesOverlay(this, _blockedCells, _mapTransitions, GridWidth, GridHeight, CellSize);
        DrawMapInteractablesOverlay();
        DrawMovementPreviewOverlay();
        DrawAttackPreviewOverlay();
        DrawHoveredUnitTooltip();
        DrawHoveredInteractableTooltip();
    }

    // Input and player control
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
            RefreshHudPanels();
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
            RefreshHudPanels();
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
            if (_flowState == BattleFlowState.Exploration && TryHandleExplorationClickInteraction(clickedCell))
            {
                return;
            }

            if (TrySelectPartyMemberAtCell(clickedCell))
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
        if (!IsWithinRange(active.GridPos, clickedCell, actionProfile.Range))
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

    // Turn flow and action resolution
    private void OnTurnChanged(Unit activeUnit)
    {
        if (_flowState != BattleFlowState.Combat)
        {
            return;
        }

        PruneInvalidUnitReferences();

        _awaitingPlayerAttackDirection = false;
        ClearMovementPreviewPath();
        QueueRedraw();

        foreach (var unit in _allUnits)
        {
            if (!IsUsableUnit(unit))
            {
                continue;
            }

            unit.SetActive(unit == activeUnit);
        }

        if (activeUnit == null)
        {
            _hud?.SetStatusText("No active unit");
            return;
        }

        activeUnit.ResetTurnResources();

        if (activeUnit.Team == "enemy")
        {
            RunEnemyTurn(activeUnit);
        }
        else
        {
            SetStatusHelp();
        }
    }

    private void OnHudAbilityPressed(string abilityId)
    {
        if (_flowState != BattleFlowState.Combat)
        {
            return;
        }

        var active = GetActivePlayerUnit();
        if (active == null)
        {
            return;
        }

        if (!active.CanUseAbilityThisTurn())
        {
            _hud?.SetStatusText("Ability already used this turn.");
            SetStatusHelp();
            return;
        }

        if (string.IsNullOrEmpty(abilityId) || !active.HasAbility(abilityId))
        {
            _hud?.SetStatusText("That ability is not available for this unit.");
            SetStatusHelp();
            return;
        }

        var cooldownRemaining = active.GetAbilityCooldownRemaining(abilityId);
        if (cooldownRemaining > 0)
        {
            _hud?.SetStatusText($"{abilityId} is on cooldown ({cooldownRemaining} turn{(cooldownRemaining == 1 ? "" : "s")} remaining).");
            SetStatusHelp();
            return;
        }

        var actionProfile = ResolveActionProfile(active, abilityId);
        if (!CanCastAction(active, actionProfile, true))
        {
            return;
        }

        _awaitingPlayerAttackDirection = true;
        SetSelectedAbilityId(active, abilityId);
        ClearMovementPreviewPath();
        _hud?.SetSelectedAction($"{actionProfile.ActionId} (targeting)");
        _hud?.SetStatusText("Ability selected. Click target cell or use direction keys. Right-click or Esc to cancel.");
        RefreshHudPanels();
        QueueRedraw();
    }

    private void OnHudEndTurnPressed()
    {
        if (_flowState != BattleFlowState.Combat)
        {
            return;
        }

        if (GetActivePlayerUnit() == null)
        {
            return;
        }

        var activePlayer = GetActivePlayerUnit();
        if (TryRequestEndTurn(activePlayer, manualInput: true))
        {
            _awaitingPlayerAttackDirection = false;
            ClearMovementPreviewPath();
            _hud?.SetSelectedAction("None");
            QueueRedraw();
        }
    }

    private void OnHudEquipItemRequested(string itemId)
    {
        var target = GetInventoryTargetUnit();
        if (target == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(itemId) || _gameData == null)
        {
            return;
        }

        var itemData = _gameData.GetItem(itemId);
        if (itemData.Count == 0)
        {
            _hud?.SetStatusText("Cannot equip unknown item.");
            return;
        }

        EquipItemToUnit(target, itemData, itemId);
        ApplyEquippedItemBonuses(target);

        var itemName = GetString(itemData, "name", itemId);
        _hud?.SetStatusText($"{target.UnitName} equipped {itemName}.");
        _hud?.AddCombatLogEntry($"{target.UnitName} equipped {itemName}.");
        SetStatusHelp();
    }

    private void OnHudUnequipItemRequested(string equippedSlotKey)
    {
        var target = GetInventoryTargetUnit();
        if (target == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(equippedSlotKey))
        {
            return;
        }

        if (!TryGetEquippedItemAtSlot(target, equippedSlotKey, out var itemId))
        {
            _hud?.SetStatusText($"{target.UnitName} has nothing equipped in that slot.");
            return;
        }

        if (!UnequipSlotForUnit(target, equippedSlotKey))
        {
            _hud?.SetStatusText($"{target.UnitName} does not have that item equipped.");
            return;
        }

        ApplyEquippedItemBonuses(target);
        var itemName = _gameData == null
            ? itemId
            : GetString(_gameData.GetItem(itemId), "name", itemId);
        _hud?.SetStatusText($"{target.UnitName} unequipped {itemName}.");
        _hud?.AddCombatLogEntry($"{target.UnitName} unequipped {itemName}.");
        SetStatusHelp();
    }

    private void OnHudInventoryCycleRequested(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        CycleInventoryTarget(delta);
        RefreshHudPanels();
    }

    private void OnHudLootConfirmRequested(string interactionId)
    {
        if (_flowState != BattleFlowState.Exploration)
        {
            _hud?.SetLootPanelVisible(false);
            return;
        }

        var explorer = GetExplorerUnit();
        if (explorer == null)
        {
            _hud?.SetStatusText("No living player unit available to interact.");
            return;
        }

        if (!TryResolveExplorationInteractionById(explorer, interactionId))
        {
            _hud?.SetStatusText("That interaction is no longer available.");
        }

        var nearbyEntries = BuildNearbyLootEntries(explorer);
        _hud?.SetLootEntries(nearbyEntries);
        _hud?.SetLootPanelVisible(nearbyEntries.Count > 0);
        SetStatusHelp();
    }

    private void TryResolvePlayerActionAtCell(Unit active, Vector2I targetCell)
    {
        if (!active.CanUseAbilityThisTurn())
        {
            _hud?.SetStatusText("Ability already used this turn.");
            return;
        }

        var selectedAbilityId = GetSelectedAbilityId(active);
        var actionProfile = ResolveActionProfile(active, selectedAbilityId);
        if (active.GetAbilityCooldownRemaining(actionProfile.ActionId) > 0)
        {
            _hud?.SetStatusText($"{actionProfile.ActionId} is on cooldown.");
            return;
        }

        if (!CanCastAction(active, actionProfile, true))
        {
            return;
        }

        if (actionProfile.ActionType == "heal")
        {
            var allyTarget = GetLivingAllyAtCell(active.Team, targetCell);
            if (allyTarget != null)
            {
                if (!TryHealTarget(active, allyTarget, actionProfile.HealAmount, actionProfile.Range, actionProfile.ActionId, actionProfile.ActionName, actionProfile.CooldownTurns, actionProfile.MagicPointCost, actionProfile.IsMagical))
                {
                    return;
                }

                var result = ResolveSuccessfulAction(actionProfile.ActionType);
                ApplyActionResult(result);
                return;
            }
        }
        else
        {
            var attackTarget = GetLivingEnemyAtCell(active.Team, targetCell);
            if (attackTarget != null)
            {
                if (!TryAttackTarget(active, attackTarget, actionProfile.Damage, actionProfile.Range, actionProfile.ActionId, actionProfile.ActionName, actionProfile.CooldownTurns, actionProfile.MagicPointCost, actionProfile.IsMagical))
                {
                    return;
                }

                var result = ResolveSuccessfulAction(actionProfile.ActionType);
                ApplyActionResult(result);
                return;
            }
        }

        if (actionProfile.ActionType != "attack")
        {
            _hud?.SetStatusText("No valid target for this action.");
            return;
        }

        if (actionProfile.Range > 1)
        {
            _hud?.SetStatusText("No valid ranged target on that cell.");
            return;
        }

        var moveResult = ResolveMoveAction(active, targetCell, endTurnOnSuccess: false);
        if (moveResult.Success)
        {
            _hud?.SetStatusText("Moved. Attack mode canceled.");
            ApplyActionResult(moveResult);
        }
        else
        {
            _hud?.SetStatusText("Cannot move there. Attack mode canceled.");
        }
    }

    private async void RunEnemyTurn(Unit enemyUnit)
    {
        if (_flowState != BattleFlowState.Combat || _isEnemyTurnProcessing)
        {
            return;
        }

        _isEnemyTurnProcessing = true;
        try
        {
            await DelayEnemyActionStep();
            if (!IsCurrentActiveUnit(enemyUnit))
            {
                return;
            }

            var actionBeforeMove = TryUsePrimaryAction(enemyUnit);
            if (actionBeforeMove.Success)
            {
                ApplyActionResult(actionBeforeMove);
                if (actionBeforeMove.CombatEnded)
                {
                    return;
                }

                await DelayEnemyActionStep();
                if (!IsCurrentActiveUnit(enemyUnit))
                {
                    return;
                }
            }

            // Enemies can spend up to their movement budget and use one primary ability each turn.
            while (_flowState == BattleFlowState.Combat && IsCurrentActiveUnit(enemyUnit) && enemyUnit.CanMoveThisTurn())
            {
                var target = _aiDirector.ChooseTarget(enemyUnit, _playerUnits);
                if (target == null)
                {
                    break;
                }

                var step = _aiDirector.ChooseStepTowardTarget(enemyUnit, target);
                if (step == enemyUnit.GridPos)
                {
                    break;
                }

                var moveResult = ResolveMoveAction(enemyUnit, step, endTurnOnSuccess: false);
                if (!moveResult.Success)
                {
                    break;
                }

                await DelayEnemyActionStep();
                if (!IsCurrentActiveUnit(enemyUnit))
                {
                    return;
                }
            }

            if (!IsCurrentActiveUnit(enemyUnit))
            {
                return;
            }

            var actionAfterMove = TryUsePrimaryAction(enemyUnit);
            if (actionAfterMove.Success)
            {
                ApplyActionResult(actionAfterMove);
                if (actionAfterMove.CombatEnded)
                {
                    return;
                }

                await DelayEnemyActionStep();
                if (!IsCurrentActiveUnit(enemyUnit))
                {
                    return;
                }
            }

            if (_flowState == BattleFlowState.Combat && IsCurrentActiveUnit(enemyUnit))
            {
                TryRequestEndTurn(enemyUnit, manualInput: false);
            }
        }
        finally
        {
            _isEnemyTurnProcessing = false;

            // If another enemy became active while this async turn was unwinding, immediately hand off.
            if (_flowState == BattleFlowState.Combat)
            {
                var activeAfter = _turnManager?.GetActiveUnit();
                if (activeAfter != null && activeAfter.Team == "enemy")
                {
                    RunEnemyTurn(activeAfter);
                }
            }
        }
    }

    private void ApplyActionResult(CombatActionResult result)
    {
        if (!result.Success || result.CombatEnded)
        {
            return;
        }

        if (result.ShouldEndTurn)
        {
            var activeBeforeEnd = _turnManager.GetActiveUnit();
            TryRequestEndTurn(activeBeforeEnd, manualInput: false);
            return;
        }

        var active = _turnManager.GetActiveUnit();
        EndTurnIfNoActionsRemain(active);
    }

    private void EndTurnIfNoActionsRemain(Unit unit)
    {
        if (_flowState != BattleFlowState.Combat || !IsUsableUnit(unit) || unit.IsDead)
        {
            return;
        }

        if (unit.Team != "enemy")
        {
            return;
        }

        if (!unit.CanMoveThisTurn() && !CanUnitUseAnyAbilityNow(unit))
        {
            TryRequestEndTurn(unit, manualInput: false);
        }
    }

    private bool TryRequestEndTurn(Unit expectedActiveUnit, bool manualInput)
    {
        if (_flowState != BattleFlowState.Combat || _turnManager == null)
        {
            return false;
        }

        if (expectedActiveUnit == null)
        {
            return false;
        }

        if (_isEndingTurn)
        {
            return false;
        }

        var currentActive = _turnManager.GetActiveUnit();
        if (currentActive != expectedActiveUnit)
        {
            return false;
        }

        if (manualInput)
        {
            var now = Time.GetTicksMsec();
            if (now - _lastManualEndTurnAtMs < ManualEndTurnDebounceMs)
            {
                return false;
            }

            _lastManualEndTurnAtMs = now;
        }

        _isEndingTurn = true;
        try
        {
            _turnManager.EndTurn();
            return true;
        }
        finally
        {
            _isEndingTurn = false;
        }
    }

    private bool IsCurrentActiveUnit(Unit unit)
    {
        if (_flowState != BattleFlowState.Combat || _turnManager == null || unit == null)
        {
            return false;
        }

        return _turnManager.GetActiveUnit() == unit;
    }

    private async System.Threading.Tasks.Task DelayEnemyActionStep()
    {
        var tree = GetTree();
        if (tree == null)
        {
            return;
        }

        await ToSignal(tree.CreateTimer(EnemyActionDelaySeconds), SceneTreeTimer.SignalName.Timeout);
    }

    // Encounter setup
    private void SpawnMapEncounter(string mapId, bool preserveParty = false, Vector2I leadSpawnCell = default)
    {
        SaveClearedEncounterStateForCurrentMap();

        if (preserveParty)
        {
            ClearEnemyUnitsFromScene();
        }
        else
        {
            ClearUnitsFromScene();
            _allUnits.Clear();
            _playerUnits.Clear();
            _enemyUnits.Clear();
            _equippedItemsByUnitId.Clear();
            _partyInventoryItemIds.Clear();
        }

        _blockedCells.Clear();
        _mapTransitions.Clear();
        _mapProps.Clear();
        _lootBags.Clear();
        _encounterAggroRanges.Clear();
        _activeEncounterId = "";
        _currentMapId = mapId;

        var mapData = _mapLoader?.LoadMapStub(mapId) ?? new MapLoader().LoadMapStub(mapId);
        _currentMapId = GetString(mapData, "id", mapId);
        LoadClearedEncounterStateForCurrentMap();
        LoadMapInteractionStateForCurrentMap();

        var blocked = TryGetVector2IArray(mapData, "blocked");
        foreach (var blockedCell in blocked)
        {
            _blockedCells.Add(blockedCell);
        }

        var transitions = TryGetDictionaryArray(mapData, "transitions");
        foreach (var transition in transitions)
        {
            _mapTransitions.Add(transition);
        }

        LoadPropsFromMap(mapData);

        if (preserveParty)
        {
            PositionPartyForMapTransition(leadSpawnCell);
        }
        else
        {
            SpawnPlayersFromMap(mapData);
        }

        SpawnEnemiesFromMap(mapData);
    }

    private void SpawnPlayersFromMap(Dictionary mapData)
    {
        var players = TryGetDictionaryArray(mapData, "players");
        foreach (var config in players)
        {
            SpawnUnit(config);
        }
    }

    private void SpawnEnemiesFromMap(Dictionary mapData)
    {
        var encounters = TryGetDictionaryArray(mapData, "encounters");
        foreach (var encounter in encounters)
        {
            var encounterId = GetString(encounter, "id", "encounter");
            var aggroRange = GetInt(encounter, "aggro_range", DefaultAggroTriggerRange);
            _encounterAggroRanges[encounterId] = aggroRange;

            if (_clearedEncounterIds.Contains(encounterId))
            {
                continue;
            }

            var enemies = TryGetDictionaryArray(encounter, "enemies");
            foreach (var enemyConfig in enemies)
            {
                enemyConfig["encounter_id"] = encounterId;
                SpawnUnit(enemyConfig);
            }
        }
    }

    private void SpawnUnit(Dictionary config)
    {
        var unit = _unitScene.Instantiate<Unit>();
        _unitsRoot.AddChild(unit);
        unit.Setup(config);
        _allUnits.Add(unit);

        if (unit.Team == "player")
        {
            _playerUnits.Add(unit);
            ApplyStartingEquipmentFromConfig(unit, config);
        }
        else
        {
            _enemyUnits.Add(unit);
        }

        ApplyEquippedItemBonuses(unit);
    }

    // Combat and grid rules
    private bool TryMoveUnit(Unit unit, Vector2I targetCell)
    {
        if (!IsUsableUnit(unit) || unit.IsDead)
        {
            return false;
        }

        if (!IsInBounds(targetCell))
        {
            return false;
        }

        if (IsBlockedCell(targetCell))
        {
            return false;
        }

        if (IsOccupied(targetCell, unit))
        {
            return false;
        }

        var fromCell = unit.GridPos;
        unit.SetGridPos(targetCell);
        _eventBus?.EmitSignal(EventBus.SignalName.UnitMoved, unit, fromCell, targetCell);
        SetStatusHelp();
        return true;
    }

    private CombatActionResult TryUsePrimaryAction(Unit actor)
    {
        if (_flowState == BattleFlowState.Combat && !actor.CanUseAbilityThisTurn())
        {
            return CombatActionResult.Failed;
        }

        var actionProfile = ResolveActionProfile(actor, GetSelectedAbilityId(actor));
        if (actor.GetAbilityCooldownRemaining(actionProfile.ActionId) > 0)
        {
            return CombatActionResult.Failed;
        }

        if (!CanCastAction(actor, actionProfile, true))
        {
            return CombatActionResult.Failed;
        }

        Unit target = actionProfile.ActionType == "heal"
            ? FindMostInjuredAllyInRange(actor, actionProfile.Range)
            : FindNearestEnemyInRange(actor, actionProfile.Range);

        if (target == null)
        {
            _hud?.SetStatusText($"{actor.UnitName} has no {actionProfile.ActionType} target in range ({actionProfile.Range}).");
            return CombatActionResult.Failed;
        }

        var actionSuccess = actionProfile.ActionType == "heal"
            ? TryHealTarget(actor, target, actionProfile.HealAmount, actionProfile.Range, actionProfile.ActionId, actionProfile.ActionName, actionProfile.CooldownTurns, actionProfile.MagicPointCost, actionProfile.IsMagical)
            : TryAttackTarget(actor, target, actionProfile.Damage, actionProfile.Range, actionProfile.ActionId, actionProfile.ActionName, actionProfile.CooldownTurns, actionProfile.MagicPointCost, actionProfile.IsMagical);

        if (!actionSuccess)
        {
            return CombatActionResult.Failed;
        }

        return ResolveSuccessfulAction(actionProfile.ActionType);
    }

    private CombatActionResult ResolveSuccessfulAction(string actionType)
    {
        if (actionType == "attack")
        {
            CleanupDefeatedUnits();
        }

        if (CheckCombatResolved())
        {
            return CombatActionResult.CombatResolvedResult;
        }

        return CombatActionResult.AttackResolved;
    }

    private CombatActionResult ResolveMoveAction(Unit unit, Vector2I targetCell, bool endTurnOnSuccess)
    {
        if (_flowState == BattleFlowState.Combat && !unit.CanMoveThisTurn())
        {
            _hud?.SetStatusText($"{unit.UnitName} has no movement left this turn.");
            return CombatActionResult.Failed;
        }

        if (!TryMoveUnit(unit, targetCell))
        {
            return CombatActionResult.Failed;
        }

        if (_flowState == BattleFlowState.Combat)
        {
            unit.TrySpendMovement();
            SetStatusHelp();
        }

        return endTurnOnSuccess ? CombatActionResult.MoveAndEndTurnResolved : CombatActionResult.MoveResolved;
    }

    private bool TryAttackTarget(Unit attacker, Unit target, int damage, int range, string actionId = "attack", string actionName = "Attack", int cooldownTurns = 0, int magicPointCost = 0, bool isMagical = false)
    {
        if (_flowState == BattleFlowState.Combat && !attacker.CanUseAbilityThisTurn())
        {
            _hud?.SetStatusText($"{attacker.UnitName} has already used an ability this turn.");
            return false;
        }

        if (!CanAttack(attacker, target, range))
        {
            _hud?.SetStatusText($"{attacker.UnitName} has no clear line to {target.UnitName}.");
            return false;
        }

        if (!CanCastAction(attacker, new ActionProfile(actionId, actionName, "attack", range, damage, 0, cooldownTurns, magicPointCost, isMagical), true))
        {
            return false;
        }

        if (!attacker.TrySpendMagicPoints(magicPointCost))
        {
            _hud?.SetStatusText($"{attacker.UnitName} does not have enough MP to cast {actionName}.");
            return false;
        }

        var mitigatedDamage = isMagical ? damage : Mathf.Max(0, damage - target.armor_class);
        target.ApplyDamage(mitigatedDamage);
        if (_flowState == BattleFlowState.Combat)
        {
            attacker.MarkAbilityUsed(actionId, cooldownTurns);
        }

        _eventBus?.EmitSignal(EventBus.SignalName.ActionUsed, attacker, actionId, target.UnitId);

        var resultText = isMagical
            ? $"{attacker.UnitName} casts {actionName} on {target.UnitName}, dealing {mitigatedDamage} damage."
            : $"{attacker.UnitName} hits {target.UnitName} for {mitigatedDamage}.";
        if (!isMagical && target.armor_class > 0)
        {
            var reducedBy = Mathf.Max(0, damage - mitigatedDamage);
            if (reducedBy > 0)
            {
                resultText += $" ({reducedBy} blocked by armor_class)";
            }
        }

        if (magicPointCost > 0)
        {
            resultText += $" (MP -{magicPointCost})";
        }

        if (target.IsDead)
        {
            resultText += $" {target.UnitName} is defeated.";
        }

        _lastActionSummary = resultText;
        _hud?.SetStatusText(resultText);
        _hud?.AddCombatLogEntry(resultText);
        return true;
    }

    private bool TryHealTarget(Unit actor, Unit target, int healAmount, int range, string actionId, string actionName, int cooldownTurns = 0, int magicPointCost = 0, bool isMagical = false)
    {
        if (_flowState == BattleFlowState.Combat && !actor.CanUseAbilityThisTurn())
        {
            _hud?.SetStatusText($"{actor.UnitName} has already used an ability this turn.");
            return false;
        }

        if (!CanHeal(actor, target, range))
        {
            _hud?.SetStatusText($"{actor.UnitName} cannot heal {target.UnitName} from there.");
            return false;
        }

        if (!CanCastAction(actor, new ActionProfile(actionId, actionName, "heal", range, 0, healAmount, cooldownTurns, magicPointCost, isMagical), true))
        {
            return false;
        }

        var healed = target.ApplyHealing(healAmount);
        if (healed <= 0)
        {
            _hud?.SetStatusText($"{target.UnitName} is already at full health.");
            return false;
        }

        if (!actor.TrySpendMagicPoints(magicPointCost))
        {
            _hud?.SetStatusText($"{actor.UnitName} does not have enough MP to cast {actionName}.");
            return false;
        }

        if (_flowState == BattleFlowState.Combat)
        {
            actor.MarkAbilityUsed(actionId, cooldownTurns);
        }

        _eventBus?.EmitSignal(EventBus.SignalName.ActionUsed, actor, actionId, target.UnitId);
        var resultText = isMagical
            ? $"{actor.UnitName} casts {actionName} on {target.UnitName}, restoring {healed} HP."
            : $"{actor.UnitName} heals {target.UnitName} for {healed}.";
        if (magicPointCost > 0)
        {
            resultText += $" (MP -{magicPointCost})";
        }
        _lastActionSummary = resultText;
        _hud?.SetStatusText(resultText);
        _hud?.AddCombatLogEntry(resultText);
        return true;
    }

    private Unit FindNearestEnemyInRange(Unit attacker, int range)
    {
        Unit nearest = null;
        var nearestDistance = int.MaxValue;

        foreach (var unit in _allUnits)
        {
            if (!IsValidAttackTarget(attacker, unit))
            {
                continue;
            }

            var distance = RangeDistance(attacker.GridPos, unit.GridPos);
            if (distance <= range && distance < nearestDistance && CanAttack(attacker, unit, range))
            {
                nearest = unit;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private Unit FindMostInjuredAllyInRange(Unit actor, int range)
    {
        Unit mostInjured = null;
        var maxMissingHp = 0;

        foreach (var unit in _allUnits)
        {
            if (!IsValidAllyTarget(actor, unit))
            {
                continue;
            }

            if (!CanUseActionAtRange(actor, unit, range))
            {
                continue;
            }

            var missingHp = unit.MaxHitPoints - unit.HitPoints;
            if (missingHp > maxMissingHp)
            {
                maxMissingHp = missingHp;
                mostInjured = unit;
            }
        }

        return mostInjured;
    }

    private void CleanupDefeatedUnits()
    {
        RemoveDeadFromTeam(_playerUnits);
        RemoveDeadFromTeam(_enemyUnits);
    }

    private static void RemoveDeadFromTeam(Array<Unit> units)
    {
        for (var i = units.Count - 1; i >= 0; i--)
        {
            if (!IsUsableUnit(units[i]) || units[i].IsDead)
            {
                units.RemoveAt(i);
            }
        }
    }

    private bool CheckCombatResolved()
    {
        if (_playerUnits.Count == 0)
        {
            _flowState = BattleFlowState.Defeat;
            _eventBus?.EmitSignal(EventBus.SignalName.CombatEnded);
            _hud?.SetStatusText("Defeat. All player units were defeated.");
            return true;
        }

        if (_enemyUnits.Count == 0)
        {
            _eventBus?.EmitSignal(EventBus.SignalName.CombatEnded);
            EnterExplorationMode("Encounter cleared. Exploration resumed.");
            return true;
        }

        if (!HasLivingEnemiesInEncounter(_activeEncounterId))
        {
            _clearedEncounterIds.Add(_activeEncounterId);
            SaveClearedEncounterStateForCurrentMap();
            _activeEncounterId = "";
            _eventBus?.EmitSignal(EventBus.SignalName.CombatEnded);
            EnterExplorationMode("Encounter cleared. Exploration resumed.");
            return true;
        }

        return false;
    }

    private Unit GetActivePlayerUnit()
    {
        if (_flowState != BattleFlowState.Combat)
        {
            return null;
        }

        var active = _turnManager.GetActiveUnit();
        if (active == null || active.Team != "player" || active.IsDead)
        {
            return null;
        }

        return active;
    }

    private static bool IsInBounds(Vector2I cell)
    {
        return cell.X >= 0 && cell.X < GridWidth && cell.Y >= 0 && cell.Y < GridHeight;
    }

    private bool IsBlockedCell(Vector2I cell)
    {
        foreach (var blocked in _blockedCells)
        {
            if (blocked == cell)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsOccupied(Vector2I cell, Unit ignoreUnit = null)
    {
        foreach (var unit in _allUnits)
        {
            if (!IsUsableUnit(unit) || unit == ignoreUnit || unit.IsDead)
            {
                continue;
            }

            if (unit.GridPos == cell)
            {
                return true;
            }
        }

        return false;
    }

    private Unit GetLivingEnemyAtCell(string attackerTeam, Vector2I cell)
    {
        foreach (var unit in _allUnits)
        {
            if (unit == null || unit.IsDead || unit.Team == attackerTeam)
            {
                continue;
            }

            if (!IsValidAttackTargetByTeam(attackerTeam, unit))
            {
                continue;
            }

            if (unit.GridPos == cell)
            {
                return unit;
            }
        }

        return null;
    }

    private Unit GetLivingAllyAtCell(string actorTeam, Vector2I cell)
    {
        foreach (var unit in _allUnits)
        {
            if (!IsUsableUnit(unit) || unit.IsDead || unit.Team != actorTeam)
            {
                continue;
            }

            if (unit.GridPos == cell)
            {
                return unit;
            }
        }

        return null;
    }

    private Unit GetLivingUnitAtCell(Vector2I cell)
    {
        foreach (var unit in _allUnits)
        {
            if (!IsUsableUnit(unit) || unit.IsDead)
            {
                continue;
            }

            if (unit.GridPos == cell)
            {
                return unit;
            }
        }

        return null;
    }

    // Math and data helpers
    private Array<Vector2I> FindPath(Unit mover, Vector2I start, Vector2I goal, int maxSteps)
    {
        var path = new Array<Vector2I>();
        if (maxSteps <= 0 || start == goal)
        {
            return path;
        }

        if (!IsInBounds(goal) || IsBlockedCell(goal) || IsOccupied(goal, mover))
        {
            return path;
        }

        var frontier = new Queue<Vector2I>();
        var cameFrom = new System.Collections.Generic.Dictionary<Vector2I, Vector2I>();
        var distance = new System.Collections.Generic.Dictionary<Vector2I, int>();

        frontier.Enqueue(start);
        distance[start] = 0;

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            var currentDistance = distance[current];
            if (current == goal)
            {
                break;
            }

            if (currentDistance >= maxSteps)
            {
                continue;
            }

            foreach (var dir in AttackDirections)
            {
                var next = current + dir;
                if (!IsInBounds(next) || IsBlockedCell(next) || IsOccupied(next, mover))
                {
                    continue;
                }

                if (distance.ContainsKey(next))
                {
                    continue;
                }

                distance[next] = currentDistance + 1;
                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (!distance.ContainsKey(goal))
        {
            return path;
        }

        var cursor = goal;
        while (cursor != start)
        {
            path.Insert(0, cursor);
            cursor = cameFrom[cursor];
        }

        return path;
    }

    private static Vector2I KeyToDelta(Key keycode)
    {
        return keycode switch
        {
            Key.W or Key.Up => new Vector2I(0, -1),
            Key.S or Key.Down => new Vector2I(0, 1),
            Key.A or Key.Left => new Vector2I(-1, 0),
            Key.D or Key.Right => new Vector2I(1, 0),
            _ => Vector2I.Zero
        };
    }

    private bool CanAttack(Unit attacker, Unit target, int range)
    {
        if (attacker == null || target == null || attacker.IsDead || target.IsDead)
        {
            return false;
        }

        if (attacker.Team == target.Team)
        {
            return false;
        }

        return CanUseActionAtRange(attacker, target, range);
    }

    private bool CanHeal(Unit actor, Unit target, int range)
    {
        if (actor == null || target == null || actor.IsDead || target.IsDead)
        {
            return false;
        }

        if (actor.Team != target.Team)
        {
            return false;
        }

        return CanUseActionAtRange(actor, target, range);
    }

    private bool CanUseActionAtRange(Unit actor, Unit target, int range)
    {
        return IsWithinRange(actor.GridPos, target.GridPos, range) && HasLineOfSight(actor, target);
    }

    private static bool IsWithinRange(Vector2I from, Vector2I to, int range)
    {
        return RangeDistance(from, to) <= range;
    }

    private static int RangeDistance(Vector2I a, Vector2I b)
    {
        return Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
    }

    private bool HasLineOfSight(Unit attacker, Unit target)
    {
        var points = GetLinePoints(attacker.GridPos, target.GridPos);
        for (var i = 1; i < points.Count - 1; i++)
        {
            var point = points[i];
            if (IsCellBlockingLineOfSight(point, attacker, target))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsCellBlockingLineOfSight(Vector2I cell, Unit attacker, Unit target)
    {
        foreach (var unit in _allUnits)
        {
            if (!IsUsableUnit(unit) || unit.IsDead || unit == attacker || unit == target)
            {
                continue;
            }

            if (unit.GridPos == cell)
            {
                return true;
            }
        }

        return false;
    }

    private static Array<Vector2I> GetLinePoints(Vector2I start, Vector2I end)
    {
        var points = new Array<Vector2I>();

        var x0 = start.X;
        var y0 = start.Y;
        var x1 = end.X;
        var y1 = end.Y;
        var dx = Mathf.Abs(x1 - x0);
        var dy = Mathf.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            points.Add(new Vector2I(x0, y0));
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            var e2 = err * 2;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return points;
    }

    private static int Manhattan(Vector2I a, Vector2I b)
    {
        return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
    }

    private ActionProfile ResolveActionProfile(Unit actor, string abilityId = null)
    {
        if (actor == null)
        {
            return new ActionProfile("attack", "Attack", "attack", 1, 0, 0, 0, 0, false);
        }

        abilityId = string.IsNullOrEmpty(abilityId) ? GetSelectedAbilityId(actor) : abilityId;
        if (!actor.HasAbility(abilityId))
        {
            abilityId = actor.PrimaryAbilityId;
        }

        var fallback = new ActionProfile(abilityId, abilityId, "attack", actor.AttackRange, actor.AttackDamage, 0, 0, 0, false);
        if (_gameData == null || string.IsNullOrEmpty(abilityId))
        {
            return fallback;
        }

        var actionData = _gameData.GetAbility(abilityId);
        var isMagical = false;
        if (actionData.Count == 0)
        {
            actionData = _gameData.GetSpell(abilityId);
            isMagical = actionData.Count > 0;
        }

        if (actionData.Count == 0)
        {
            return fallback;
        }

        var actionName = GetString(actionData, "name", abilityId);
        var actionType = GetString(actionData, "type", "attack");
        var range = Mathf.Max(1, GetInt(actionData, "range", actor.AttackRange));
        var damage = Mathf.Max(0, GetInt(actionData, "damage", actor.AttackDamage));
        var healAmount = Mathf.Max(0, GetInt(actionData, "heal_amount", 0));
        var cooldownTurns = Mathf.Max(0, GetInt(actionData, "cooldown", 0));
        var mpCost = Mathf.Max(0, GetInt(actionData, "mp_cost", 0));

        return new ActionProfile(abilityId, actionName, actionType, range, damage, healAmount, cooldownTurns, mpCost, isMagical);
    }

    private bool CanCastAction(Unit actor, ActionProfile actionProfile, bool reportStatus)
    {
        if (actor == null)
        {
            return false;
        }

        if (actionProfile.MagicPointCost <= 0)
        {
            return true;
        }

        if (actor.HasEnoughMagicPoints(actionProfile.MagicPointCost))
        {
            return true;
        }

        if (reportStatus)
        {
            _hud?.SetStatusText(
                $"{actor.UnitName} does not have enough MP to cast {actionProfile.ActionName} ({actor.MagicPoints}/{actionProfile.MagicPointCost})."
            );
        }

        return false;
    }

    private string GetSelectedAbilityId(Unit unit)
    {
        if (unit == null)
        {
            return "";
        }

        if (_selectedAbilityIdByUnitId.TryGetValue(unit.UnitId, out var selectedId) && unit.HasAbility(selectedId))
        {
            return selectedId;
        }

        if (!string.IsNullOrEmpty(unit.PrimaryAbilityId) && unit.HasAbility(unit.PrimaryAbilityId))
        {
            _selectedAbilityIdByUnitId[unit.UnitId] = unit.PrimaryAbilityId;
            return unit.PrimaryAbilityId;
        }

        if (unit.AbilityIds != null && unit.AbilityIds.Count > 0)
        {
            _selectedAbilityIdByUnitId[unit.UnitId] = unit.AbilityIds[0];
            return unit.AbilityIds[0];
        }

        return "";
    }

    private void SetSelectedAbilityId(Unit unit, string abilityId)
    {
        if (unit == null || string.IsNullOrEmpty(unit.UnitId) || string.IsNullOrEmpty(abilityId) || !unit.HasAbility(abilityId))
        {
            return;
        }

        _selectedAbilityIdByUnitId[unit.UnitId] = abilityId;
    }

    private bool CanUnitUseAnyAbilityNow(Unit unit)
    {
        if (unit == null || unit.IsDead || !unit.CanUseAbilityThisTurn() || unit.AbilityIds == null)
        {
            return false;
        }

        foreach (var abilityId in unit.AbilityIds)
        {
            if (unit.CanUseAbility(abilityId) && CanCastAction(unit, ResolveActionProfile(unit, abilityId), false))
            {
                return true;
            }
        }

        return false;
    }

    private Array<Dictionary> BuildAbilityEntriesForHud(Unit unit)
    {
        var entries = new Array<Dictionary>();
        if (unit == null || unit.AbilityIds == null)
        {
            return entries;
        }

        var selectedId = GetSelectedAbilityId(unit);
        foreach (var abilityId in unit.AbilityIds)
        {
            var profile = ResolveActionProfile(unit, abilityId);
            var actionName = string.IsNullOrEmpty(profile.ActionName) ? GetActionDisplayName(abilityId) : profile.ActionName;
            var cooldownRemaining = unit.GetAbilityCooldownRemaining(abilityId);
            var valueText = profile.ActionType == "heal"
                ? $"Heal: {profile.HealAmount}"
                : $"Damage: {profile.Damage}";
            var mpCostLabel = profile.MagicPointCost <= 0
                ? "MP Cost: none"
                : $"MP Cost: {profile.MagicPointCost}";
            var cooldownLabel = profile.CooldownTurns <= 0
                ? "Cooldown: none"
                : $"Cooldown: {profile.CooldownTurns} turn{(profile.CooldownTurns == 1 ? "" : "s")}";
            var stateLabel = cooldownRemaining > 0
                ? $"Status: on cooldown ({cooldownRemaining} remaining)"
                : !CanCastAction(unit, profile, false)
                    ? $"Status: needs MP ({unit.MagicPoints}/{profile.MagicPointCost})"
                : "Status: ready";
            entries.Add(new Dictionary
            {
                { "id", abilityId },
                { "label", actionName },
                { "detail", $"{actionName}\nType: {profile.ActionType}\nRange: {profile.Range}\n{valueText}\n{mpCostLabel}\n{cooldownLabel}\n{stateLabel}" },
                { "cooldown_remaining", cooldownRemaining },
                { "is_selected", abilityId == selectedId ? 1 : 0 }
            });
        }

        return entries;
    }

    private string GetActionDisplayName(string actionId)
    {
        if (string.IsNullOrEmpty(actionId) || _gameData == null)
        {
            return actionId;
        }

        var actionData = _gameData.GetAbility(actionId);
        if (actionData.Count == 0)
        {
            actionData = _gameData.GetSpell(actionId);
        }

        return actionData.Count == 0
            ? actionId
            : GetString(actionData, "name", actionId);
    }

    private static int GetInt(Dictionary dict, string key, int fallback)
    {
        return dict.ContainsKey(key) ? (int)((Variant)dict[key]) : fallback;
    }

    // UI helpers
    private void CancelAttackMode(bool restoreHelpText = true)
    {
        _awaitingPlayerAttackDirection = false;
        _hud?.SetSelectedAction("None");
        ClearMovementPreviewPath();
        QueueRedraw();
        if (restoreHelpText)
        {
            SetStatusHelp();
        }
    }

    private void DrawAttackPreviewOverlay()
    {
        if (!_awaitingPlayerAttackDirection)
        {
            return;
        }

        var active = _turnManager?.GetActiveUnit();
        if (!IsUsableUnit(active) || active.Team != "player" || active.IsDead)
        {
            return;
        }

        var actionProfile = ResolveActionProfile(active, GetSelectedAbilityId(active));
        var center = CellCenter(active.GridPos);
        DrawArc(center, 28.0f, 0.0f, Mathf.Tau, 40, new Color(1.0f, 0.85f, 0.35f, 0.95f), 3.0f);

        for (var dx = -actionProfile.Range; dx <= actionProfile.Range; dx++)
        {
            for (var dy = -actionProfile.Range; dy <= actionProfile.Range; dy++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var cell = active.GridPos + new Vector2I(dx, dy);
                if (!IsInBounds(cell) || !IsWithinRange(active.GridPos, cell, actionProfile.Range))
                {
                    continue;
                }

                var cellRect = new Rect2(new Vector2(cell.X * CellSize, cell.Y * CellSize), new Vector2(CellSize, CellSize));
                var target = actionProfile.ActionType == "heal"
                    ? GetLivingAllyAtCell(active.Team, cell)
                    : GetLivingEnemyAtCell(active.Team, cell);
                var valid = target != null && CanUseActionAtRange(active, target, actionProfile.Range);

                var fill = valid ? new Color(0.2f, 0.9f, 0.3f, 0.25f) : new Color(0.9f, 0.25f, 0.25f, 0.12f);
                var edge = valid ? new Color(0.3f, 1.0f, 0.45f, 0.9f) : new Color(1.0f, 0.4f, 0.4f, 0.5f);
                DrawRect(cellRect, fill, true);
                DrawRect(cellRect, edge, false, 2.0f);
            }
        }
    }

    private void DrawMovementPreviewOverlay()
    {
        if (_flowState != BattleFlowState.Combat || _awaitingPlayerAttackDirection)
        {
            return;
        }

        var active = GetActivePlayerUnit();
        if (active == null)
        {
            return;
        }

        if (_hasMovementHoverCell && _movementHoverCell != active.GridPos)
        {
            var hoverRect = new Rect2(new Vector2(_movementHoverCell.X * CellSize, _movementHoverCell.Y * CellSize), new Vector2(CellSize, CellSize));
            if (_movementHoverReachable)
            {
                DrawRect(hoverRect, new Color(0.2f, 0.85f, 0.35f, 0.12f), true);
                DrawRect(hoverRect, new Color(0.3f, 1.0f, 0.45f, 0.9f), false, 2.0f);
            }
            else
            {
                DrawRect(hoverRect, new Color(0.9f, 0.2f, 0.2f, 0.12f), true);
                DrawRect(hoverRect, new Color(1.0f, 0.35f, 0.35f, 0.9f), false, 2.0f);
            }

            var label = _movementHoverReachable
                ? $"{_movementHoverCost}/{active.RemainingMovement}"
                : $"X/{active.RemainingMovement}";
            var labelColor = _movementHoverReachable
                ? new Color(0.78f, 1.0f, 0.86f, 1.0f)
                : new Color(1.0f, 0.72f, 0.72f, 1.0f);
            var labelPos = new Vector2(_movementHoverCell.X * CellSize + CellSize / 2.0f, _movementHoverCell.Y * CellSize + CellSize - 10.0f);
            DrawString(ThemeDB.FallbackFont, labelPos, label, HorizontalAlignment.Center, CellSize - 8.0f, ThemeDB.FallbackFontSize, labelColor);
        }

        if (_movementPreviewPath.Count == 0)
        {
            return;
        }

        for (var i = 0; i < _movementPreviewPath.Count; i++)
        {
            var cell = _movementPreviewPath[i];
            var cellRect = new Rect2(new Vector2(cell.X * CellSize, cell.Y * CellSize), new Vector2(CellSize, CellSize));
            var alpha = 0.12f + (0.06f * i);
            var clampedAlpha = Mathf.Clamp(alpha, 0.12f, 0.28f);
            DrawRect(cellRect, new Color(0.22f, 0.72f, 1.0f, clampedAlpha), true);
            DrawRect(cellRect, new Color(0.35f, 0.85f, 1.0f, 0.92f), false, 2.0f);
        }

        var startCenter = CellCenter(active.GridPos);
        foreach (var cell in _movementPreviewPath)
        {
            var nextCenter = CellCenter(cell);
            DrawLine(startCenter, nextCenter, new Color(0.45f, 0.9f, 1.0f, 0.9f), 2.0f);
            startCenter = nextCenter;
        }
    }

    private void DrawMapInteractablesOverlay()
    {
        foreach (var prop in _mapProps)
        {
            var cell = GetVector2I(prop, "grid_pos", new Vector2I(-9999, -9999));
            if (!IsInBounds(cell))
            {
                continue;
            }

            var propId = GetString(prop, "id", "prop");
            var isOpened = _openedPropIds.Contains(propId);
            var rect = new Rect2(new Vector2(cell.X * CellSize, cell.Y * CellSize), new Vector2(CellSize, CellSize));
            if (isOpened)
            {
                DrawRect(rect, new Color(0.34f, 0.3f, 0.24f, 0.24f), true);
                DrawRect(rect, new Color(0.62f, 0.56f, 0.46f, 0.75f), false, 2.0f);
                DrawCircle(rect.GetCenter(), 5.0f, new Color(0.78f, 0.74f, 0.66f, 0.72f));
                DrawLine(rect.Position + new Vector2(12.0f, 12.0f), rect.End - new Vector2(12.0f, 12.0f), new Color(0.88f, 0.84f, 0.74f, 0.8f), 2.0f);
                DrawLine(new Vector2(rect.End.X - 12.0f, rect.Position.Y + 12.0f), new Vector2(rect.Position.X + 12.0f, rect.End.Y - 12.0f), new Color(0.88f, 0.84f, 0.74f, 0.8f), 2.0f);
            }
            else
            {
                DrawRect(rect, new Color(0.5f, 0.34f, 0.14f, 0.38f), true);
                DrawRect(rect, new Color(0.85f, 0.62f, 0.32f, 0.95f), false, 2.0f);
                DrawCircle(rect.GetCenter(), 6.0f, new Color(1.0f, 0.86f, 0.45f, 0.95f));
            }
        }

        foreach (var bag in _lootBags)
        {
            var cell = GetVector2I(bag, "grid_pos", new Vector2I(-9999, -9999));
            if (!IsInBounds(cell))
            {
                continue;
            }

            var isEmpty = GetBagItemIds(bag).Count == 0;
            var rect = new Rect2(new Vector2(cell.X * CellSize, cell.Y * CellSize), new Vector2(CellSize, CellSize));
            if (isEmpty)
            {
                DrawRect(rect, new Color(0.32f, 0.29f, 0.24f, 0.2f), true);
                DrawCircle(rect.GetCenter(), 8.0f, new Color(0.72f, 0.68f, 0.6f, 0.72f));
                DrawArc(rect.GetCenter(), 10.0f, 0.0f, Mathf.Tau, 24, new Color(0.84f, 0.8f, 0.7f, 0.78f), 2.0f);
                DrawLine(rect.Position + new Vector2(14.0f, 14.0f), rect.End - new Vector2(14.0f, 14.0f), new Color(0.88f, 0.84f, 0.74f, 0.78f), 2.0f);
            }
            else
            {
                DrawRect(rect, new Color(0.6f, 0.46f, 0.2f, 0.28f), true);
                DrawCircle(rect.GetCenter(), 9.0f, new Color(0.97f, 0.78f, 0.25f, 0.95f));
                DrawArc(rect.GetCenter(), 11.0f, 0.0f, Mathf.Tau, 24, new Color(1.0f, 0.94f, 0.65f, 0.95f), 2.0f);
            }
        }
    }

    private void DrawHoveredUnitTooltip()
    {
        var cell = WorldToCell(GetGlobalMousePosition());
        if (!IsInBounds(cell))
        {
            return;
        }

        var unit = GetLivingUnitAtCell(cell);
        if (!IsUsableUnit(unit) || unit.IsDead)
        {
            return;
        }

        var titleColor = unit.Team == "enemy"
            ? new Color(1.0f, 0.78f, 0.78f, 1.0f)
            : new Color(0.78f, 0.95f, 1.0f, 1.0f);
        _hud?.SetWorldHoverTooltip(
            GetGlobalMousePosition(),
            $"{unit.UnitName} [{unit.Team}]",
            $"HP: {unit.HitPoints}/{unit.MaxHitPoints}\nMP: {unit.MagicPoints}/{unit.MaxMagicPoints}\nArmor Class: {unit.armor_class} | Atk: {unit.AttackDamage} | Range: {unit.AttackRange}",
            new Color(0.05f, 0.05f, 0.08f, 0.86f),
            new Color(0.82f, 0.86f, 0.94f, 0.95f),
            titleColor,
            new Color(0.95f, 0.98f, 1.0f, 1.0f)
        );
    }

    private void DrawHoveredInteractableTooltip()
    {
        var cell = WorldToCell(GetGlobalMousePosition());
        if (!IsInBounds(cell) || GetLivingUnitAtCell(cell) != null)
        {
            return;
        }

        var title = "";
        var details = "";

        foreach (var prop in _mapProps)
        {
            var propCell = GetVector2I(prop, "grid_pos", new Vector2I(-9999, -9999));
            if (propCell != cell)
            {
                continue;
            }

            var propId = GetString(prop, "id", "prop");
            var propName = GetString(prop, "name", "Chest");
            title = propName;
            details = _openedPropIds.Contains(propId)
                ? "Lootable object\nEmpty"
                : "Lootable object\nClosed";
            break;
        }

        if (string.IsNullOrEmpty(title))
        {
            foreach (var bag in _lootBags)
            {
                var bagCell = GetVector2I(bag, "grid_pos", new Vector2I(-9999, -9999));
                if (bagCell != cell)
                {
                    continue;
                }

                var itemIds = GetBagItemIds(bag);
                title = itemIds.Count > 0 ? "Loot Bag" : "Loot Bag (Empty)";
                details = itemIds.Count > 0
                    ? $"Pickup container\nContains: {JoinItemNames(itemIds)}"
                    : "Pickup container\nEmpty";
                break;
            }
        }

        if (string.IsNullOrEmpty(title))
        {
            foreach (var transition in _mapTransitions)
            {
                var fromCell = GetVector2I(transition, "from_cell", new Vector2I(-9999, -9999));
                if (fromCell != cell)
                {
                    continue;
                }

                var toMap = GetString(transition, "to_map", "unknown");
                title = "Map Transition";
                details = $"Exit cell\nLeads to: {toMap}";
                break;
            }
        }

        if (string.IsNullOrEmpty(title))
        {
            return;
        }

        _hud?.SetWorldHoverTooltip(
            GetGlobalMousePosition(),
            title,
            details,
            new Color(0.09f, 0.08f, 0.06f, 0.9f),
            new Color(0.95f, 0.86f, 0.6f, 0.92f),
            new Color(1.0f, 0.95f, 0.8f, 1.0f),
            new Color(0.95f, 0.9f, 0.78f, 1.0f)
        );
    }

    private bool TryHandleExplorationClickInteraction(Vector2I clickedCell)
    {
        if (_flowState != BattleFlowState.Exploration)
        {
            return false;
        }

        var explorer = GetExplorerUnit();
        if (explorer == null || !IsInBounds(clickedCell))
        {
            return false;
        }

        Dictionary clickedEntry = null;
        var clickedInteractable = false;

        foreach (var prop in _mapProps)
        {
            var propCell = GetVector2I(prop, "grid_pos", new Vector2I(-9999, -9999));
            if (propCell != clickedCell)
            {
                continue;
            }

            clickedInteractable = true;
            if (Manhattan(explorer.GridPos, propCell) > 1)
            {
                _hud?.SetStatusText("Move adjacent to interact with that object.");
                return true;
            }

            var propId = GetString(prop, "id", "prop");
            var propName = GetString(prop, "name", "Prop");
            if (_openedPropIds.Contains(propId))
            {
                _hud?.SetStatusText($"{propName} is empty.");
                return true;
            }

            clickedEntry = new Dictionary
            {
                { "id", $"prop:{propId}" },
                { "label", $"Open {propName}" },
                { "detail", $"Open {propName} at ({propCell.X}, {propCell.Y})." }
            };
            break;
        }

        if (clickedEntry == null)
        {
            foreach (var bag in _lootBags)
            {
                var bagCell = GetVector2I(bag, "grid_pos", new Vector2I(-9999, -9999));
                if (bagCell != clickedCell)
                {
                    continue;
                }

                clickedInteractable = true;
                if (Manhattan(explorer.GridPos, bagCell) > 1)
                {
                    _hud?.SetStatusText("Move adjacent to pick up that loot bag.");
                    return true;
                }

                var bagId = GetString(bag, "id", "bag");
                var itemIds = GetBagItemIds(bag);
                if (itemIds.Count == 0)
                {
                    _hud?.SetStatusText("This loot bag is empty.");
                    return true;
                }

                clickedEntry = new Dictionary
                {
                    { "id", $"bag:{bagId}" },
                    { "label", $"Pick up loot bag ({itemIds.Count} item{(itemIds.Count == 1 ? "" : "s")})" },
                    { "detail", $"Pick up bag at ({bagCell.X}, {bagCell.Y}). Contains: {JoinItemNames(itemIds)}." }
                };
                break;
            }
        }

        if (!clickedInteractable || clickedEntry == null)
        {
            return false;
        }

        _hud?.SetLootEntries(new Array<Dictionary> { clickedEntry });
        _hud?.SetLootPanelVisible(true);
        _hud?.SetStatusText("Loot interaction opened. Confirm to proceed.");
        return true;
    }

    private Array<Dictionary> BuildNearbyLootEntries(Unit explorer)
    {
        var entries = new Array<Dictionary>();
        if (explorer == null)
        {
            return entries;
        }

        foreach (var prop in _mapProps)
        {
            var propCell = GetVector2I(prop, "grid_pos", new Vector2I(-9999, -9999));
            if (Manhattan(explorer.GridPos, propCell) > 1)
            {
                continue;
            }

            var propId = GetString(prop, "id", "prop");
            if (_openedPropIds.Contains(propId))
            {
                continue;
            }

            var propName = GetString(prop, "name", "Prop");
            entries.Add(new Dictionary
            {
                { "id", $"prop:{propId}" },
                { "label", $"Open {propName}" },
                { "detail", $"Open {propName} at ({propCell.X}, {propCell.Y})." }
            });
        }

        foreach (var bag in _lootBags)
        {
            var bagCell = GetVector2I(bag, "grid_pos", new Vector2I(-9999, -9999));
            if (Manhattan(explorer.GridPos, bagCell) > 1)
            {
                continue;
            }

            var bagId = GetString(bag, "id", "bag");
            var itemIds = GetBagItemIds(bag);
            if (itemIds.Count == 0)
            {
                continue;
            }

            entries.Add(new Dictionary
            {
                { "id", $"bag:{bagId}" },
                { "label", $"Pick up loot bag ({itemIds.Count} item{(itemIds.Count == 1 ? "" : "s")})" },
                { "detail", $"Pick up bag at ({bagCell.X}, {bagCell.Y}). Contains: {JoinItemNames(itemIds)}." }
            });
        }

        return entries;
    }

    private bool TryResolveExplorationInteractionById(Unit explorer, string interactionId)
    {
        if (string.IsNullOrEmpty(interactionId))
        {
            return false;
        }

        if (interactionId.StartsWith("prop:"))
        {
            return TryOpenPropById(explorer, interactionId.Substring(5));
        }

        if (interactionId.StartsWith("bag:"))
        {
            return TryPickupBagById(explorer, interactionId.Substring(4));
        }

        return false;
    }

    private bool TryOpenPropById(Unit explorer, string propId)
    {
        for (var i = _mapProps.Count - 1; i >= 0; i--)
        {
            var prop = _mapProps[i];
            if (GetString(prop, "id", "") != propId)
            {
                continue;
            }

            var propCell = GetVector2I(prop, "grid_pos", new Vector2I(-9999, -9999));
            if (Manhattan(explorer.GridPos, propCell) > 1)
            {
                return false;
            }

            if (_openedPropIds.Contains(propId))
            {
                _hud?.SetStatusText($"{GetString(prop, "name", "prop")} is empty.");
                return true;
            }

            _openedPropIds.Add(propId);

            var drops = BuildPropLootDrops(prop);
            if (drops.Count > 0)
            {
                var bag = new Dictionary
                {
                    { "id", $"bag-{propId}" },
                    { "grid_pos", propCell },
                    { "item_ids", drops },
                    { "source_prop_id", propId }
                };
                _lootBags.Add(bag);
                _hud?.SetStatusText($"{explorer.UnitName} opened {GetString(prop, "name", "prop")} and revealed loot.");
                _hud?.AddCombatLogEntry($"Opened {GetString(prop, "name", "prop")}: found {JoinItemNames(drops)}.");
            }
            else
            {
                _hud?.SetStatusText($"{explorer.UnitName} opened {GetString(prop, "name", "prop")}. It was empty.");
            }

            RefreshHudPanels();
            SaveMapInteractionStateForCurrentMap();
            QueueRedraw();
            return true;
        }

        return false;
    }

    private bool TryPickupBagById(Unit explorer, string bagId)
    {
        for (var i = _lootBags.Count - 1; i >= 0; i--)
        {
            var bag = _lootBags[i];
            if (GetString(bag, "id", "") != bagId)
            {
                continue;
            }

            var bagCell = GetVector2I(bag, "grid_pos", new Vector2I(-9999, -9999));
            if (Manhattan(explorer.GridPos, bagCell) > 1)
            {
                return false;
            }

            var itemIds = GetBagItemIds(bag);
            if (itemIds.Count == 0)
            {
                _hud?.SetStatusText("This loot bag is empty.");
                return true;
            }

            _lootedBagIds.Add(bagId);
            bag["item_ids"] = new Array<string>();
            bag["item_id"] = "";

            if (itemIds.Count > 0)
            {
                foreach (var itemId in itemIds)
                {
                    _partyInventoryItemIds.Add(itemId);
                }

                var pickupSummary = JoinItemNames(itemIds);
                _hud?.SetStatusText($"{explorer.UnitName} picked up {pickupSummary}.");
                _hud?.AddCombatLogEntry($"Loot acquired: {pickupSummary}.");
            }

            RefreshHudPanels();
            SaveMapInteractionStateForCurrentMap();
            QueueRedraw();
            return true;
        }

        return false;
    }

    private void SetMovementPreviewPath(Array<Vector2I> path)
    {
        if (ArePathsEqual(_movementPreviewPath, path))
        {
            return;
        }

        _movementPreviewPath.Clear();
        foreach (var step in path)
        {
            _movementPreviewPath.Add(step);
        }

        QueueRedraw();
    }

    private void SetMovementHoverState(Vector2I cell, bool reachable, int pathCost)
    {
        if (_hasMovementHoverCell && _movementHoverCell == cell && _movementHoverReachable == reachable && _movementHoverCost == pathCost)
        {
            return;
        }

        _hasMovementHoverCell = true;
        _movementHoverCell = cell;
        _movementHoverReachable = reachable;
        _movementHoverCost = pathCost;
        QueueRedraw();
    }

    private void ClearMovementPreviewPath()
    {
        if (_movementPreviewPath.Count == 0 && !_hasMovementHoverCell)
        {
            return;
        }

        _movementPreviewPath.Clear();
        _hasMovementHoverCell = false;
        _movementHoverCell = new Vector2I(-1, -1);
        _movementHoverReachable = false;
        _movementHoverCost = -1;
        QueueRedraw();
    }

    private static bool ArePathsEqual(Array<Vector2I> left, Array<Vector2I> right)
    {
        if (left == null || right == null)
        {
            return false;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    private bool TryGetDirectionalActionTargetCell(Unit active, Vector2I direction, ActionProfile actionProfile, out Vector2I targetCell)
    {
        targetCell = active.GridPos + direction;
        for (var distance = 1; distance <= actionProfile.Range; distance++)
        {
            var cell = active.GridPos + direction * distance;
            if (!IsInBounds(cell))
            {
                break;
            }

            var target = actionProfile.ActionType == "heal"
                ? GetLivingAllyAtCell(active.Team, cell)
                : GetLivingEnemyAtCell(active.Team, cell);
            if (target != null && CanUseActionAtRange(active, target, actionProfile.Range))
            {
                targetCell = cell;
                return true;
            }

            if (GetLivingUnitAtCell(cell) != null)
            {
                break;
            }
        }

        return false;
    }

    private static Vector2 CellCenter(Vector2I cell)
    {
        return new Vector2(cell.X * CellSize + CellSize / 2.0f, cell.Y * CellSize + CellSize / 2.0f);
    }

    private static Vector2I WorldToCell(Vector2 world)
    {
        return new Vector2I(
            Mathf.FloorToInt(world.X / CellSize),
            Mathf.FloorToInt(world.Y / CellSize)
        );
    }

    private void SetStatusHelp()
    {
        if (_flowState == BattleFlowState.Exploration)
        {
            var explorer = GetExplorerUnit();
            if (explorer == null)
            {
                _hud?.SetStatusText("Exploration: no living player units.");
                return;
            }

            _hud?.SetStatusText($"Map: {_currentMapId} | Exploration: {explorer.UnitName}");
            _hud?.SetActionDetails($"Exploration party leader: {explorer.UnitName}");
            _hud?.SetSelectedAction("None");
            RefreshHudPanels();
            return;
        }

        if (_flowState == BattleFlowState.Defeat)
        {
            _hud?.SetStatusText("Defeat. All player units were defeated.");
            _hud?.SetActionDetails("Defeat state. Restart the encounter to continue.");
            _hud?.SetSelectedAction("None");
            RefreshHudPanels();
            return;
        }

        var active = _turnManager.GetActiveUnit();
        if (active == null)
        {
            _hud?.SetStatusText("No active unit");
            return;
        }

        var selectedAbilityId = GetSelectedAbilityId(active);
        var selectedProfile = ResolveActionProfile(active, selectedAbilityId);
        var cooldownRemaining = active.GetAbilityCooldownRemaining(selectedProfile.ActionId);
        var abilityState = !active.CanUseAbilityThisTurn()
            ? "used"
            : cooldownRemaining > 0
                ? $"cooldown ({cooldownRemaining})"
                : "ready";
        var combatPrefix = string.IsNullOrEmpty(_lastActionSummary) ? "" : $"Last action: {_lastActionSummary} | ";
        _hud?.SetStatusText(
            $"{combatPrefix}Turn: {active.UnitName} ({active.Team}) | HP: {active.HitPoints}/{active.MaxHitPoints} | MP: {active.MagicPoints}/{active.MaxMagicPoints} | Move: {active.RemainingMovement}/{Unit.MaxMovementPerTurn} | Ability: {abilityState}"
        );

        
        _hud?.SetSelectedAction(_awaitingPlayerAttackDirection ? $"{selectedProfile.ActionId} (targeting)" : selectedProfile.ActionId);
        RefreshHudPanels();
    }

    private void RefreshHudPanels()
    {
        if (_hud == null)
        {
            return;
        }

        _hud.SetHelpText(BuildHelpText());

        var active = _turnManager?.GetActiveUnit();
        _hud.SetTurnQueue(BuildTurnQueueForHud(), active);
        _hud.SetActiveUnit(_flowState == BattleFlowState.Combat ? active : null);

        var activePlayer = GetActivePlayerUnit();
        var abilityEnabled = _flowState == BattleFlowState.Combat && activePlayer != null && activePlayer.CanUseAbilityThisTurn();
        _hud.SetActionButtonsEnabled(abilityEnabled, _flowState == BattleFlowState.Combat);
        _hud.SetAbilityButtons(BuildAbilityEntriesForHud(activePlayer), abilityEnabled);

        var inventoryTarget = GetInventoryTargetUnit();
        if (inventoryTarget != null)
        {
            _hud.SetInventoryUnitName(inventoryTarget.UnitName);
            _hud.SetInventoryEquippedSummary(BuildInventoryEquippedSummary(inventoryTarget));
            _hud.SetInventoryEquippedItems(BuildInventoryEquippedEntries(inventoryTarget));
            _hud.SetInventoryItems(BuildInventoryItemsForHud(), GetEquippedItemIds(inventoryTarget));
        }
        else
        {
            _hud.SetInventoryEquippedSummary("Equipped: none");
            _hud.SetInventoryEquippedItems(new Array<Dictionary>());
        }

        if (_flowState == BattleFlowState.Exploration)
        {
            var explorer = GetExplorerUnit();
            _hud.SetLootEntries(BuildNearbyLootEntries(explorer));
        }
        else
        {
            _hud.SetLootPanelVisible(false);
        }
    }

    private string BuildHelpText()
    {
        var common =
            "CONTROLS\n" +
            "- Inventory: I\n" +
            "- Help: H\n" +
            "- Inspect: hover units and interactables\n" +
            "- Inventory target: click party member portrait\n" +
            "- Cycle target: Tab / Shift+Tab or Prev/Next Member\n";

        if (_flowState == BattleFlowState.Exploration)
        {
            return common +
                "\nEXPLORATION\n" +
                "- Move party: WASD or Arrow keys\n" +
                "- Interact: left-click chest/loot while adjacent (range 1)\n" +
                "- Confirm pickups in Nearby Loot\n" +
                "- Map transitions: step on glowing edge cells\n" +
                "- Combat starts when enemies engage your party";
        }

        if (_flowState == BattleFlowState.Combat)
        {
            return common +
                "\nCOMBAT\n" +
                "- Move: WASD/Arrow keys or click reachable cells\n" +
                "- Ability: F, then choose direction/cell\n" +
                "- End turn: Space or End Turn button\n" +
                "- Limits: one ability use and limited movement each turn\n" +
                "- Win encounter to return to exploration";
        }

        return common +
            "\nDEFEAT\n" +
            "- All party members are down\n" +
            "- Restart encounter or reload to continue";
    }

    private Array<Dictionary> BuildInventoryItemsForHud()
    {
        var items = new Array<Dictionary>();
        if (_gameData == null)
        {
            return items;
        }

        var equippedUsage = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var equippedBySlot in _equippedItemsByUnitId.Values)
        {
            foreach (var entry in equippedBySlot)
            {
                var equippedId = entry.Value;
                equippedUsage[equippedId] = equippedUsage.TryGetValue(equippedId, out var count) ? count + 1 : 1;
            }
        }

        foreach (var itemId in _partyInventoryItemIds)
        {
            if (equippedUsage.TryGetValue(itemId, out var equippedCount) && equippedCount > 0)
            {
                equippedUsage[itemId] = equippedCount - 1;
                continue;
            }

            var itemData = _gameData.GetItem(itemId);
            if (itemData.Count == 0)
            {
                continue;
            }

            items.Add(itemData);
        }

        return items;
    }

    private Unit GetInventoryTargetUnit()
    {
        var selected = GetSelectedInventoryUnit();
        if (selected != null)
        {
            return selected;
        }

        var fallback = _flowState == BattleFlowState.Combat
            ? GetActivePlayerUnit()
            : GetExplorerUnit();

        if (fallback != null)
        {
            _selectedInventoryUnitId = fallback.UnitId;
        }

        return fallback;
    }

    private Array<string> GetEquippedItemIds(Unit unit)
    {
        var result = new Array<string>();
        if (unit == null || string.IsNullOrEmpty(unit.UnitId))
        {
            return result;
        }

        if (!_equippedItemsByUnitId.TryGetValue(unit.UnitId, out var equippedBySlot))
        {
            return result;
        }

        foreach (var entry in equippedBySlot)
        {
            result.Add(entry.Value);
        }

        return result;
    }

    private string GetEquippedItemSummary(Unit unit)
    {
        if (_gameData == null)
        {
            return "None";
        }

        var equippedIds = GetEquippedItemIds(unit);
        if (equippedIds.Count == 0)
        {
            return "None";
        }

        var names = new List<string>();
        foreach (var itemId in equippedIds)
        {
            var item = _gameData.GetItem(itemId);
            names.Add(item.Count == 0 ? itemId : GetString(item, "name", itemId));
        }

        return string.Join(", ", names);
    }

    private string BuildInventoryEquippedSummary(Unit unit)
    {
        if (unit == null || _gameData == null || string.IsNullOrEmpty(unit.UnitId))
        {
            return "Equipped: none";
        }

        if (!_equippedItemsByUnitId.TryGetValue(unit.UnitId, out var equippedBySlot) || equippedBySlot.Count == 0)
        {
            return "Equipped: none";
        }

        var orderedSlots = new List<string>(equippedBySlot.Keys);
        orderedSlots.Sort();

        var parts = new List<string>();
        foreach (var slot in orderedSlots)
        {
            var itemId = equippedBySlot[slot];
            var itemData = _gameData.GetItem(itemId);
            var itemName = itemData.Count == 0 ? itemId : GetString(itemData, "name", itemId);
            var slotLabel = slot.Replace("-a", " A").Replace("-b", " B");
            parts.Add($"{slotLabel}: {itemName}");
        }

        return $"Equipped: {string.Join(" | ", parts)}";
    }

    private Array<Dictionary> BuildInventoryEquippedEntries(Unit unit)
    {
        var entries = new Array<Dictionary>();
        if (unit == null || _gameData == null || string.IsNullOrEmpty(unit.UnitId))
        {
            return entries;
        }

        if (!_equippedItemsByUnitId.TryGetValue(unit.UnitId, out var equippedBySlot) || equippedBySlot.Count == 0)
        {
            return entries;
        }

        var orderedSlots = new List<string>(equippedBySlot.Keys);
        orderedSlots.Sort();

        foreach (var slot in orderedSlots)
        {
            var itemId = equippedBySlot[slot];
            var itemData = _gameData.GetItem(itemId);
            var itemName = itemData.Count == 0 ? itemId : GetString(itemData, "name", itemId);
            var slotLabel = slot.Replace("-a", " A").Replace("-b", " B");
            entries.Add(new Dictionary
            {
                { "slot_key", slot },
                { "label", $"{slotLabel}: {itemName}" },
                { "detail", $"Equipped in {slotLabel}. Select Unequip to return it to shared inventory." }
            });
        }

        return entries;
    }

    private Unit GetSelectedInventoryUnit()
    {
        if (string.IsNullOrEmpty(_selectedInventoryUnitId))
        {
            return null;
        }

        foreach (var unit in _playerUnits)
        {
            if (IsUsableUnit(unit) && !unit.IsDead && unit.UnitId == _selectedInventoryUnitId)
            {
                return unit;
            }
        }

        return null;
    }

    private bool TrySelectPartyMemberAtCell(Vector2I cell)
    {
        foreach (var unit in _playerUnits)
        {
            if (!IsUsableUnit(unit) || unit.IsDead)
            {
                continue;
            }

            if (unit.GridPos != cell)
            {
                continue;
            }

            _selectedInventoryUnitId = unit.UnitId;
            _hud?.SetStatusText($"Selected {unit.UnitName} for inventory management.");
            RefreshHudPanels();
            return true;
        }

        return false;
    }

    private void CycleInventoryTarget(int delta)
    {
        var party = new List<Unit>();
        foreach (var unit in _playerUnits)
        {
            if (IsUsableUnit(unit) && !unit.IsDead)
            {
                party.Add(unit);
            }
        }

        if (party.Count == 0)
        {
            _selectedInventoryUnitId = "";
            return;
        }

        var currentIndex = -1;
        for (var i = 0; i < party.Count; i++)
        {
            if (party[i].UnitId == _selectedInventoryUnitId)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex < 0)
        {
            _selectedInventoryUnitId = party[0].UnitId;
            return;
        }

        var next = (currentIndex + delta) % party.Count;
        if (next < 0)
        {
            next += party.Count;
        }

        _selectedInventoryUnitId = party[next].UnitId;
    }

    private void ApplyEquippedItemBonuses(Unit unit)
    {
        if (unit == null)
        {
            return;
        }

        unit.SetWeaponBonuses(0, 0);
        unit.SetArmorBonuses(0, 0, 0);

        if (_gameData == null || unit == null || string.IsNullOrEmpty(unit.UnitId))
        {
            return;
        }

        if (!_equippedItemsByUnitId.TryGetValue(unit.UnitId, out var equippedBySlot) || equippedBySlot.Count == 0)
        {
            return;
        }

        var totalWeaponDamage = 0;
        var bestWeaponRange = 0;
        var total_armor_class = 0;
        foreach (var entry in equippedBySlot)
        {
            var itemData = _gameData.GetItem(entry.Value);
            if (itemData.Count == 0)
            {
                continue;
            }

            var itemType = GetString(itemData, "type", "item");
            if (itemType == "weapon")
            {
                var base_dmg = GetInt(itemData, "base_damage", 0);
                var bonus_dmg = GetInt(itemData, "bonus_damage", 0);
                var range = GetInt(itemData, "range", 0);
                totalWeaponDamage += base_dmg + bonus_dmg;
                bestWeaponRange = Mathf.Max(bestWeaponRange, range);
            }
            else if (itemType == "armor")
            {
                var base_armor_class = GetInt(itemData, "base_armor_class", 0);
                var bonus_armor_class = GetInt(itemData, "bonus_armor_class", 0);
                total_armor_class += base_armor_class + bonus_armor_class;
            }
        }

        unit.SetWeaponBonuses(totalWeaponDamage, bestWeaponRange);
        unit.SetArmorBonuses(total_armor_class, 0, 0);
    }

    private void ApplyStartingEquipmentFromConfig(Unit unit, Dictionary config)
    {
        if (unit == null || config == null)
        {
            return;
        }

        var startingItems = TryGetStringArray(config, "starting_equipment");
        foreach (var itemId in startingItems)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                continue;
            }

            _partyInventoryItemIds.Add(itemId);
            if (_gameData == null)
            {
                continue;
            }

            var itemData = _gameData.GetItem(itemId);
            if (itemData.Count == 0)
            {
                continue;
            }

            EquipItemToUnit(unit, itemData, itemId);
        }
    }

    private void UnequipAllItemsForUnit(Unit unit)
    {
        if (unit == null || string.IsNullOrEmpty(unit.UnitId))
        {
            return;
        }

        _equippedItemsByUnitId.Remove(unit.UnitId);
    }

    private bool UnequipSlotForUnit(Unit unit, string slotKey)
    {
        if (unit == null || string.IsNullOrEmpty(unit.UnitId) || string.IsNullOrEmpty(slotKey))
        {
            return false;
        }

        if (!_equippedItemsByUnitId.TryGetValue(unit.UnitId, out var equippedBySlot) || equippedBySlot.Count == 0)
        {
            return false;
        }

        var removed = equippedBySlot.Remove(slotKey);
        if (!removed)
        {
            return false;
        }

        if (equippedBySlot.Count == 0)
        {
            _equippedItemsByUnitId.Remove(unit.UnitId);
        }

        return removed;
    }

    private bool TryGetEquippedItemAtSlot(Unit unit, string slotKey, out string itemId)
    {
        itemId = "";
        if (unit == null || string.IsNullOrEmpty(unit.UnitId) || string.IsNullOrEmpty(slotKey))
        {
            return false;
        }

        if (!_equippedItemsByUnitId.TryGetValue(unit.UnitId, out var equippedBySlot) || !equippedBySlot.TryGetValue(slotKey, out var value))
        {
            return false;
        }

        itemId = value;
        return !string.IsNullOrEmpty(itemId);
    }

    private void EquipItemToUnit(Unit unit, Dictionary itemData, string itemId)
    {
        if (unit == null || string.IsNullOrEmpty(unit.UnitId) || itemData == null)
        {
            return;
        }

        var slot = GetString(itemData, "slot", "");
        if (string.IsNullOrEmpty(slot))
        {
            return;
        }

        if (!_equippedItemsByUnitId.TryGetValue(unit.UnitId, out var equippedBySlot))
        {
            equippedBySlot = new System.Collections.Generic.Dictionary<string, string>();
            _equippedItemsByUnitId[unit.UnitId] = equippedBySlot;
        }

        if (slot == "2-handed")
        {
            equippedBySlot.Remove("1-handed-a");
            equippedBySlot.Remove("1-handed-b");
            equippedBySlot["2-handed"] = itemId;
            return;
        }

        if (slot == "1-handed")
        {
            equippedBySlot.Remove("2-handed");
            if (!equippedBySlot.ContainsKey("1-handed-a"))
            {
                equippedBySlot["1-handed-a"] = itemId;
                return;
            }

            if (!equippedBySlot.ContainsKey("1-handed-b"))
            {
                equippedBySlot["1-handed-b"] = itemId;
                return;
            }

            equippedBySlot["1-handed-a"] = itemId;
            return;
        }

        equippedBySlot[slot] = itemId;
    }

    private Array<Unit> BuildTurnQueueForHud()
    {
        if (_flowState != BattleFlowState.Combat)
        {
            return new Array<Unit>();
        }

        if (_turnManager == null)
        {
            return new Array<Unit>();
        }

        return _turnManager.GetTurnOrderFromActive();
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
            _hud?.SetStatusText(statusText);
        }
        else
        {
            SetStatusHelp();
        }
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
                    StartCombat(encounterId);
                    return;
                }
            }
        }
    }

    private void StartCombat(string encounterId)
    {
        if (_flowState == BattleFlowState.Combat)
        {
            return;
        }

        PruneInvalidUnitReferences();

        _activeEncounterId = encounterId;
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
            if (IsUsableUnit(enemy) && !enemy.IsDead && enemy.EncounterId == encounterId)
            {
                combatUnits.Add(enemy);
            }
        }

        _turnManager.SetupTurnOrder(combatUnits);
        SetStatusHelp();
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

        foreach (var transition in _mapTransitions)
        {
            var fromCell = GetVector2I(transition, "from_cell", new Vector2I(-9999, -9999));
            if (explorer.GridPos != fromCell)
            {
                continue;
            }

            var toMap = GetString(transition, "to_map", _currentMapId);
            var spawnCell = GetVector2I(transition, "spawn_cell", explorer.GridPos);
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
        QueueRedraw();
    }

    private void PositionPartyForMapTransition(Vector2I leadSpawnCell)
    {
        var livingIndex = 0;
        foreach (var player in _playerUnits)
        {
            if (!IsUsableUnit(player) || player.IsDead)
            {
                continue;
            }

            var nextCell = leadSpawnCell + GetPartyFormationOffset(livingIndex);
            if (!IsInBounds(nextCell) || IsBlockedCell(nextCell))
            {
                nextCell = leadSpawnCell;
            }

            player.SetGridPos(nextCell);
            livingIndex++;
        }
    }

    private static Vector2I GetPartyFormationOffset(int index)
    {
        return index switch
        {
            0 => new Vector2I(0, 0),
            1 => new Vector2I(0, 1),
            2 => new Vector2I(0, -1),
            3 => new Vector2I(1, 0),
            _ => new Vector2I(index - 3, 0)
        };
    }

    private void ClearUnitsFromScene()
    { 
        foreach (Node child in _unitsRoot.GetChildren())
        {
            child.QueueFree();
        }
    }

    private void ClearEnemyUnitsFromScene()
    {
        for (var i = _allUnits.Count - 1; i >= 0; i--)
        {
            var unit = _allUnits[i];
            if (!IsUsableUnit(unit))
            {
                _allUnits.RemoveAt(i);
                continue;
            }

            if (unit.Team != "enemy")
            {
                continue;
            }

            unit.QueueFree();
            _allUnits.RemoveAt(i);
        }

        _enemyUnits.Clear();
    }

    private int GetEncounterAggroRange(string encounterId)
    {
        return _encounterAggroRanges.TryGetValue(encounterId, out var range) ? range : DefaultAggroTriggerRange;
    }

    private void LoadPropsFromMap(Dictionary mapData)
    {
        _mapProps.Clear();
        _lootBags.Clear();

        if (_lootBagsByMap.TryGetValue(_currentMapId, out var storedBags))
        {
            foreach (var bag in storedBags)
            {
                _lootBags.Add(CopyDictionary(bag));
            }
        }

        var props = TryGetDictionaryArray(mapData, "props");
        foreach (var prop in props)
        {
            _mapProps.Add(CopyDictionary(prop));
        }
    }

    private void LoadClearedEncounterStateForCurrentMap()
    {
        _clearedEncounterIds.Clear();

        if (!_clearedEncounterIdsByMap.TryGetValue(_currentMapId, out var stored))
        {
            return;
        }

        foreach (var encounterId in stored)
        {
            _clearedEncounterIds.Add(encounterId);
        }
    }

    private void LoadMapInteractionStateForCurrentMap()
    {
        _openedPropIds.Clear();
        _lootedBagIds.Clear();

        if (_openedPropIdsByMap.TryGetValue(_currentMapId, out var opened))
        {
            foreach (var propId in opened)
            {
                _openedPropIds.Add(propId);
            }
        }

        if (_lootedBagIdsByMap.TryGetValue(_currentMapId, out var looted))
        {
            foreach (var bagId in looted)
            {
                _lootedBagIds.Add(bagId);
            }
        }
    }

    private void SaveClearedEncounterStateForCurrentMap()
    {
        if (string.IsNullOrEmpty(_currentMapId))
        {
            return;
        }

        var snapshot = new System.Collections.Generic.HashSet<string>();
        foreach (var encounterId in _clearedEncounterIds)
        {
            snapshot.Add(encounterId);
        }

        _clearedEncounterIdsByMap[_currentMapId] = snapshot;
        SaveMapInteractionStateForCurrentMap();
    }

    private void SaveMapInteractionStateForCurrentMap()
    {
        if (string.IsNullOrEmpty(_currentMapId))
        {
            return;
        }

        var openedSnapshot = new System.Collections.Generic.HashSet<string>();
        foreach (var propId in _openedPropIds)
        {
            openedSnapshot.Add(propId);
        }

        var lootedSnapshot = new System.Collections.Generic.HashSet<string>();
        foreach (var bagId in _lootedBagIds)
        {
            lootedSnapshot.Add(bagId);
        }

        var lootBagSnapshot = new Array<Dictionary>();
        foreach (var bag in _lootBags)
        {
            lootBagSnapshot.Add(CopyDictionary(bag));
        }

        _openedPropIdsByMap[_currentMapId] = openedSnapshot;
        _lootedBagIdsByMap[_currentMapId] = lootedSnapshot;
        _lootBagsByMap[_currentMapId] = lootBagSnapshot;
    }

    private bool HasLivingEnemiesInEncounter(string encounterId)
    {
        foreach (var enemy in _enemyUnits)
        {
            if (IsUsableUnit(enemy) && !enemy.IsDead && enemy.EncounterId == encounterId)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsValidAttackTarget(Unit attacker, Unit candidate)
    {
        if (!IsUsableUnit(attacker) || !IsUsableUnit(candidate) || candidate.IsDead || candidate.Team == attacker.Team)
        {
            return false;
        }

        return IsValidAttackTargetByTeam(attacker.Team, candidate);
    }

    private bool IsValidAllyTarget(Unit actor, Unit candidate)
    {
        if (!IsUsableUnit(actor) || !IsUsableUnit(candidate) || candidate.IsDead || candidate.Team != actor.Team)
        {
            return false;
        }

        if (_flowState != BattleFlowState.Combat)
        {
            return true;
        }

        if (candidate.Team != "enemy")
        {
            return true;
        }

        return candidate.EncounterId == _activeEncounterId;
    }

    private bool IsValidAttackTargetByTeam(string attackerTeam, Unit candidate)
    {
        if (!IsUsableUnit(candidate) || candidate.IsDead || candidate.Team == attackerTeam)
        {
            return false;
        }

        if (_flowState != BattleFlowState.Combat)
        {
            return true;
        }

        if (candidate.Team != "enemy")
        {
            return true;
        }

        return candidate.EncounterId == _activeEncounterId;
    }

    private static Array<Dictionary> TryGetDictionaryArray(Dictionary dict, string key)
    {
        if (!dict.ContainsKey(key))
        {
            return new Array<Dictionary>();
        }

        var raw = (Variant)dict[key];
        if (raw.VariantType != Variant.Type.Array)
        {
            return new Array<Dictionary>();
        }

        var result = new Array<Dictionary>();
        foreach (var entry in (Array)raw)
        {
            var variant = (Variant)entry;
            if (variant.VariantType == Variant.Type.Dictionary)
            {
                result.Add((Dictionary)variant);
            }
        }

        return result;
    }

    private static Array<Vector2I> TryGetVector2IArray(Dictionary dict, string key)
    {
        if (!dict.ContainsKey(key))
        {
            return new Array<Vector2I>();
        }

        var raw = (Variant)dict[key];
        if (raw.VariantType != Variant.Type.Array)
        {
            return new Array<Vector2I>();
        }

        var result = new Array<Vector2I>();
        foreach (var entry in (Array)raw)
        {
            var variant = (Variant)entry;
            if (variant.VariantType == Variant.Type.Vector2I)
            {
                result.Add((Vector2I)variant);
            }
        }

        return result;
    }

    private static Array<string> TryGetStringArray(Dictionary dict, string key)
    {
        if (!dict.ContainsKey(key))
        {
            return new Array<string>();
        }

        var raw = (Variant)dict[key];
        if (raw.VariantType != Variant.Type.Array)
        {
            return new Array<string>();
        }

        var result = new Array<string>();
        foreach (var entry in (Array)raw)
        {
            var variant = (Variant)entry;
            if (variant.VariantType == Variant.Type.String)
            {
                result.Add(variant.AsString());
            }
        }

        return result;
    }

    private Array<string> BuildPropLootDrops(Dictionary prop)
    {
        var pool = TryGetStringArray(prop, "loot_item_ids");
        var legacySingle = GetString(prop, "loot_item_id", "");
        if (pool.Count == 0 && !string.IsNullOrEmpty(legacySingle))
        {
            pool.Add(legacySingle);
        }

        if (pool.Count == 0)
        {
            return new Array<string>();
        }

        var minRolls = Mathf.Max(1, GetInt(prop, "loot_rolls_min", 1));
        var maxRolls = Mathf.Max(minRolls, GetInt(prop, "loot_rolls_max", minRolls));
        var desiredRolls = _lootRng.RandiRange(minRolls, maxRolls);
        var rolls = Mathf.Clamp(desiredRolls, 1, pool.Count);

        var indices = new System.Collections.Generic.List<int>();
        for (var i = 0; i < pool.Count; i++)
        {
            indices.Add(i);
        }

        for (var i = indices.Count - 1; i > 0; i--)
        {
            var swap = _lootRng.RandiRange(0, i);
            (indices[i], indices[swap]) = (indices[swap], indices[i]);
        }

        var drops = new Array<string>();
        for (var i = 0; i < rolls; i++)
        {
            drops.Add(pool[indices[i]]);
        }

        return drops;
    }

    private Array<string> BuildPropLootDropsPreview(Dictionary prop)
    {
        var pool = TryGetStringArray(prop, "loot_item_ids");
        var legacySingle = GetString(prop, "loot_item_id", "");
        if (pool.Count == 0 && !string.IsNullOrEmpty(legacySingle))
        {
            pool.Add(legacySingle);
        }

        return pool;
    }

    private static Array<string> GetBagItemIds(Dictionary bag)
    {
        var itemIds = TryGetStringArray(bag, "item_ids");
        if (itemIds.Count > 0)
        {
            return itemIds;
        }

        var fallbackItem = GetString(bag, "item_id", "");
        if (!string.IsNullOrEmpty(fallbackItem))
        {
            itemIds.Add(fallbackItem);
        }

        return itemIds;
    }

    private string JoinItemNames(Array<string> itemIds)
    {
        if (itemIds.Count == 0)
        {
            return "nothing";
        }

        var names = new System.Collections.Generic.List<string>();
        foreach (var itemId in itemIds)
        {
            var itemData = _gameData?.GetItem(itemId) ?? new Dictionary();
            names.Add(GetString(itemData, "name", itemId));
        }

        return string.Join(", ", names);
    }

    private static string GetString(Dictionary dict, string key, string fallback)
    {
        return dict.ContainsKey(key) ? ((Variant)dict[key]).AsString() : fallback;
    }

    private static Vector2I GetVector2I(Dictionary dict, string key, Vector2I fallback)
    {
        return dict.ContainsKey(key) ? (Vector2I)((Variant)dict[key]) : fallback;
    }

    private static Dictionary CopyDictionary(Dictionary source)
    {
        var copy = new Dictionary();
        foreach (var key in source.Keys)
        {
            copy[key] = source[key];
        }

        return copy;
    }

    private static bool IsUsableUnit(Unit unit)
    {
        return unit != null && GodotObject.IsInstanceValid(unit) && !unit.IsQueuedForDeletion();
    }

    private void PruneInvalidUnitReferences()
    {
        for (var i = _allUnits.Count - 1; i >= 0; i--)
        {
            if (!IsUsableUnit(_allUnits[i]))
            {
                _allUnits.RemoveAt(i);
            }
        }

        for (var i = _playerUnits.Count - 1; i >= 0; i--)
        {
            if (!IsUsableUnit(_playerUnits[i]))
            {
                _playerUnits.RemoveAt(i);
            }
        }

        for (var i = _enemyUnits.Count - 1; i >= 0; i--)
        {
            if (!IsUsableUnit(_enemyUnits[i]))
            {
                _enemyUnits.RemoveAt(i);
            }
        }

        if (!IsUsableUnit(_explorerUnit))
        {
            _explorerUnit = null;
        }
    }

    private bool TryMoveExplorationParty(Vector2I delta)
    {
        if (_flowState != BattleFlowState.Exploration)
        {
            return false;
        }

        var leader = GetExplorerUnit();
        if (!IsUsableUnit(leader) || leader.IsDead)
        {
            return false;
        }

        var orderedParty = new System.Collections.Generic.List<Unit>();
        var priorPositions = new System.Collections.Generic.Dictionary<Unit, Vector2I>();

        orderedParty.Add(leader);
        priorPositions[leader] = leader.GridPos;

        foreach (var player in _playerUnits)
        {
            if (!IsUsableUnit(player) || player.IsDead || player == leader)
            {
                continue;
            }

            orderedParty.Add(player);
            priorPositions[player] = player.GridPos;
        }

        if (!TryMoveUnit(leader, priorPositions[leader] + delta))
        {
            return false;
        }

        var partySet = new System.Collections.Generic.HashSet<Unit>(orderedParty);
        for (var i = 1; i < orderedParty.Count; i++)
        {
            var follower = orderedParty[i];
            var nextCell = priorPositions[orderedParty[i - 1]];

            if (!IsUsableUnit(follower) || follower.IsDead)
            {
                continue;
            }

            if (CanExplorationFollowerEnterCell(nextCell, partySet))
            {
                follower.SetGridPos(nextCell);
            }
        }

        return true;
    }

    private bool CanExplorationFollowerEnterCell(Vector2I cell, System.Collections.Generic.HashSet<Unit> partyMembers)
    {
        if (!IsInBounds(cell) || IsBlockedCell(cell))
        {
            return false;
        }

        foreach (var unit in _allUnits)
        {
            if (!IsUsableUnit(unit) || unit.IsDead || partyMembers.Contains(unit))
            {
                continue;
            }

            if (unit.GridPos == cell)
            {
                return false;
            }
        }

        return true;
    }
}
