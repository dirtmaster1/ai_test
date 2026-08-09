using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BattleController : Node2D, IGamePersistenceHost
{
    // Architecture: Core orchestration state and cross-system coordination.
    private const int DefaultGridWidth = 20;
    private const int DefaultGridHeight = 15;
    private const int CellSize = 64;
    private const int MaxPartyMembers = 5;
    private const int DefaultAggroTriggerRange = 4;
    private const float GridLineThickness = 2.0f;
    private const ulong ManualEndTurnDebounceMs = 220;
    private const ulong PostPlayerActionMouseMoveLockMs = 300;
    private const float EnemyActionDelaySeconds = 0.24f;
    private const string SaveFilePath = "user://dark_dungeon_tactics_save.json";

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
    private GamePersistence _persistence;

    private readonly PackedScene _unitScene = GD.Load<PackedScene>("res://scenes/Unit.tscn");
    private readonly Array<Unit> _allUnits = new();
    private readonly Array<Unit> _playerUnits = new();
    private readonly Array<Unit> _enemyUnits = new();
    private readonly Array<Vector2I> _wallCells = new();
    private readonly HashSet<Vector2I> _walkableCells = new();
    private readonly Array<Dictionary> _mapDoors = new();
    private readonly Array<Dictionary> _mapTransitions = new();
    private readonly Array<Dictionary> _mapProps = new();
    private readonly Array<Dictionary> _lootBags = new();
    private readonly System.Collections.Generic.Dictionary<string, int> _encounterAggroRanges = new();
    private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>> _equippedItemsByUnitId = new();
    private readonly List<string> _partyInventoryItemIds = new();
    private readonly Array<Dictionary> _reservePartyRoster = new();
    private readonly System.Collections.Generic.Dictionary<string, int> _vendorGoldById = new();
    private readonly System.Collections.Generic.Dictionary<string, List<string>> _vendorInventoryItemIdsById = new();
    private readonly HashSet<string> _recruitedNpcIds = new();
    private readonly HashSet<string> _clearedEncounterIds = new();
    private readonly HashSet<string> _activeCombatEnemyUnitIds = new();
    private readonly HashSet<string> _activeCombatEncounterIds = new();
    private readonly System.Collections.Generic.Dictionary<string, HashSet<string>> _clearedEncounterIdsByMap = new();
    private readonly HashSet<string> _openedDoorIds = new();
    private readonly HashSet<string> _openedPropIds = new();
    private readonly HashSet<string> _defeatedEnemyIds = new();
    private readonly HashSet<string> _lootedBagIds = new();
    private readonly System.Collections.Generic.Dictionary<string, HashSet<string>> _openedDoorIdsByMap = new();
    private readonly System.Collections.Generic.Dictionary<string, HashSet<string>> _openedPropIdsByMap = new();
    private readonly System.Collections.Generic.Dictionary<string, HashSet<string>> _defeatedEnemyIdsByMap = new();
    private readonly System.Collections.Generic.Dictionary<string, HashSet<string>> _lootedBagIdsByMap = new();
    private readonly System.Collections.Generic.Dictionary<string, Array<Dictionary>> _lootBagsByMap = new();
    private readonly RandomNumberGenerator _lootRng = new();

    private int _gridWidth = DefaultGridWidth;
    private int _gridHeight = DefaultGridHeight;

    private BattleFlowState _flowState = BattleFlowState.Exploration;
    private bool _awaitingPlayerAttackDirection;
    private Unit _explorerUnit;
    private string _activeEncounterId = "";
    private string _currentMapId = "forest-town";
    private string _selectedCharacterUnitId = "";
    private int _partyGold = 25;
    private readonly System.Collections.Generic.Dictionary<string, string> _selectedAbilityIdByUnitId = new();
    private readonly Array<Vector2I> _movementPreviewPath = new();
    private bool _hasMovementHoverCell;
    private Vector2I _movementHoverCell = new(-1, -1);
    private bool _movementHoverReachable;
    private int _movementHoverCost = -1;
    private bool _hasActiveLootCell;
    private Vector2I _activeLootCell = new(-1, -1);
    private string _activeVendorId = "milo";
    private ulong _lastManualEndTurnAtMs;
    private bool _isEndingTurn;
    private bool _isEnemyTurnProcessing;
    private bool _isExplorationAutoMoving;
    private bool _isPanningView;
    private bool _leftMouseClickCandidate;
    private ulong _mouseMoveInputLockedUntilMs;
    private Vector2 _viewPanStartMouseGlobal;
    private Vector2 _viewPanStartPosition;
    private const float ViewPanDragThreshold = 8.0f;
    private const float ViewPanOverscroll = 96.0f;
    private const float ViewRightEdgeFollowBuffer = 72.0f;
    private const float ExplorationStepSeconds = 0.14f;

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
        public static CombatActionResult AttackResolved => new(true, false, false);
        public static CombatActionResult CombatResolvedResult => new(true, false, true);
    }

    private readonly struct ActionProfile
    {
        public string ActionId { get; }
        public string ActionName { get; }
        public string ActionType { get; }
        public int Range { get; }
        public int AreaRadius { get; }
        public int Damage { get; }
        public int HealAmount { get; }
        public int CooldownTurns { get; }
        public int MagicPointCost { get; }
        public bool IsMagical { get; }
        public bool IgnoresActionCost { get; }
        public bool RequiresRangedWeapon { get; }

        public ActionProfile(string actionId, string actionName, string actionType, int range, int areaRadius, int damage, int healAmount, int cooldownTurns, int magicPointCost, bool isMagical, bool ignoresActionCost, bool requiresRangedWeapon)
        {
            ActionId = actionId;
            ActionName = actionName;
            ActionType = actionType;
            Range = range;
            AreaRadius = areaRadius;
            Damage = damage;
            HealAmount = healAmount;
            CooldownTurns = cooldownTurns;
            MagicPointCost = magicPointCost;
            IsMagical = isMagical;
            IgnoresActionCost = ignoresActionCost;
            RequiresRangedWeapon = requiresRangedWeapon;
        }
    }

    // Architecture: Lifecycle, rendering, and combat-resolution orchestration.
    public override void _Ready()
    {
        _unitsRoot = GetNode<Node2D>("Units");
        _turnManager = GetNode<TurnManager>("TurnManager");
        _aiDirector = GetNode<AiDirector>("AiDirector");
        _mapLoader = GetNodeOrNull<MapLoader>("MapLoader");
        _hud = GetNodeOrNull<HudController>("HUD");
        if (_hud != null)
        {
            _hud.Visible = true;
        }

        _eventBus = GetNodeOrNull<EventBus>("/root/EventBus");
        _gameData = GetNodeOrNull<GameData>("/root/GameData");
        _persistence = new GamePersistence(this, SaveFilePath);
        _lootRng.Randomize();

        _turnManager.TurnChanged += OnTurnChanged;
        if (_hud != null)
        {
            _hud.AbilityPressed += OnHudAbilityPressed;
            _hud.EndTurnPressed += OnHudEndTurnPressed;
            _hud.EquipItemRequested += OnHudEquipItemRequested;
            _hud.UnequipItemRequested += OnHudUnequipItemRequested;
            _hud.InventoryCycleRequested += OnHudInventoryCycleRequested;
            _hud.LootConfirmRequested += OnHudLootConfirmRequested;
            _hud.VendorBuyRequested += OnHudVendorBuyRequested;
            _hud.VendorSellRequested += OnHudVendorSellRequested;
            _hud.ReserveStoreRequested += OnHudReserveStoreRequested;
            _hud.ReserveBringRequested += OnHudReserveBringRequested;
            _hud.TurnOrderUnitFocused += OnHudTurnOrderUnitFocused;
            _hud.PartyUnitSelected += OnHudPartyUnitSelected;
            _hud.PartyOrderRequested += OnHudPartyOrderRequested;
        }

        EnsureDefaultVendorState();

        if (!_persistence.TryLoadSaveGame(false))
        {
            SpawnMapEncounter(_currentMapId);
            EnterExplorationMode();
        }

        SyncHudFromGameState();
        CenterViewOnCurrentFocus();
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
            _hud.VendorBuyRequested -= OnHudVendorBuyRequested;
            _hud.VendorSellRequested -= OnHudVendorSellRequested;
            _hud.ReserveStoreRequested -= OnHudReserveStoreRequested;
            _hud.ReserveBringRequested -= OnHudReserveBringRequested;
            _hud.TurnOrderUnitFocused -= OnHudTurnOrderUnitFocused;
            _hud.PartyUnitSelected -= OnHudPartyUnitSelected;
            _hud.PartyOrderRequested -= OnHudPartyOrderRequested;
        }

        _persistence.PersistSaveGame(false);
    }

    public override void _Draw()
    {
        var viewportSize = GetViewportRect().Size;
        DrawRect(
            new Rect2(Vector2.Zero, viewportSize),
            new Color(0.05f, 0.05f, 0.06f),
            true
        );

        DrawRect(
            new Rect2(Vector2.Zero, new Vector2(_gridWidth * CellSize, _gridHeight * CellSize)),
            new Color(0.08f, 0.08f, 0.1f),
            true
        );

        for (var x = 0; x <= _gridWidth; x++)
        {
            var lineX = x * CellSize + 0.5f;
            DrawLine(
                new Vector2(lineX, 0),
                new Vector2(lineX, _gridHeight * CellSize),
                new Color(0.2f, 0.2f, 0.24f),
                GridLineThickness
            );
        }

        for (var y = 0; y <= _gridHeight; y++)
        {
            var lineY = y * CellSize + 0.5f;
            DrawLine(
                new Vector2(0, lineY),
                new Vector2(_gridWidth * CellSize, lineY),
                new Color(0.2f, 0.2f, 0.24f),
                GridLineThickness
            );
        }
    }

    public void DrawWorldOverlays(CanvasItem canvas)
    {
        if (canvas == null)
        {
            return;
        }

        _hud?.ClearWorldHoverTooltip();
        _mapLoader?.DrawMapFeaturesOverlay(canvas, _mapTransitions, _gridWidth, _gridHeight, CellSize);
        DrawFocusedUnitCellHighlight(canvas);
        DrawMapInteractablesOverlay(canvas);
        DrawMovementPreviewOverlay(canvas);
        DrawAttackPreviewOverlay(canvas);
        DrawHoveredUnitTooltip();
        DrawHoveredInteractableTooltip();
    }

    // Architecture: Turn flow and action resolution orchestration.
    private void OnTurnChanged(Unit activeUnit)
    {
        if (_flowState != BattleFlowState.Combat)
        {
            return;
        }

        PruneInvalidUnitReferences();

        // Always reset to movement-ready input when turn ownership changes.
        _mouseMoveInputLockedUntilMs = 0;
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
            return;
        }

        activeUnit.ResetTurnResources();
        var shouldSkipTurn = activeUnit.TryGetTurnSkippingStatusName(out var skipStatusName);
        ApplyStartOfTurnStatusEffects(activeUnit);
        if (activeUnit.IsDead)
        {
            CleanupDefeatedUnits();
            if (CheckCombatResolved())
            {
                return;
            }

            AdvanceTurnAfterStartOfTurnDeath(activeUnit);
            return;
        }

        if (shouldSkipTurn)
        {
            var statusLabel = string.IsNullOrEmpty(skipStatusName) ? "an effect" : skipStatusName.ToLowerInvariant();
            _hud?.AddCombatLogEntry($"{activeUnit.UnitName} is affected by {statusLabel} and skips the turn.");
            ScheduleTurnSkipForStatus(activeUnit);
            SyncHudFromGameState();
            QueueRedraw();
            return;
        }

        CenterViewOnCurrentFocus();

        if (activeUnit.Team == "enemy")
        {
            RunEnemyTurn(activeUnit);
        }
        else
        {
            SetStatusHelp();
        }
    }

    private async void AdvanceTurnAfterStartOfTurnDeath(Unit deadUnit)
    {
        var tree = GetTree();
        if (tree == null)
        {
            return;
        }

        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (_flowState != BattleFlowState.Combat
            || !IsUsableUnit(deadUnit)
            || !deadUnit.IsDead
            || !IsCurrentActiveUnit(deadUnit))
        {
            return;
        }

        TryRequestEndTurn(deadUnit, manualInput: false);
    }

    private async void ScheduleTurnSkipForStatus(Unit skippedUnit)
    {
        var tree = GetTree();
        if (tree == null)
        {
            return;
        }

        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (_flowState != BattleFlowState.Combat || !IsUsableUnit(skippedUnit) || !IsCurrentActiveUnit(skippedUnit))
        {
            return;
        }

        TryRequestEndTurn(skippedUnit, manualInput: false);
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

        if (string.IsNullOrEmpty(abilityId) || !active.HasAbility(abilityId))
        {
            SetStatusHelp();
            return;
        }

        var actionProfile = ResolveActionProfile(active, abilityId);
        if (!actionProfile.IgnoresActionCost && !active.CanUseAbilityThisTurn())
        {
            SetStatusHelp();
            return;
        }

        var cooldownRemaining = active.GetAbilityCooldownRemaining(abilityId);
        if (cooldownRemaining > 0)
        {
            SetStatusHelp();
            return;
        }

        if (!CanCastAction(active, actionProfile))
        {
            return;
        }

        SetSelectedAbilityId(active, abilityId);

        if (actionProfile.ActionType == "defend")
        {
            var result = TryDefend(active, actionProfile);
            ApplyActionResult(result);
            _awaitingPlayerAttackDirection = false;
            ClearMovementPreviewPath();
            SyncHudFromGameState();
            QueueRedraw();
            return;
        }

        if (actionProfile.ActionType == "protection")
        {
            if (!TryUseProtectionAura(active, actionProfile))
            {
                return;
            }

            var result = ResolveSuccessfulAction(actionProfile.ActionType);
            ApplyActionResult(result);
            _awaitingPlayerAttackDirection = false;
            ClearMovementPreviewPath();
            SyncHudFromGameState();
            QueueRedraw();
            return;
        }

        _awaitingPlayerAttackDirection = true;
        ClearMovementPreviewPath();
        SyncHudFromGameState();
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
            QueueRedraw();
        }
    }

    private void OnHudTurnOrderUnitFocused(string unitId)
    {
        if (_flowState != BattleFlowState.Combat)
        {
            return;
        }

        var unit = FindUnitById(unitId);
        if (!IsUsableUnit(unit) || unit.IsDead)
        {
            return;
        }

        CenterViewOnCell(unit.GridPos);
        var highlightColor = unit.Team == "enemy"
            ? new Color(0.95f, 0.18f, 0.16f, 1.0f)
            : new Color(0.18f, 0.9f, 0.36f, 1.0f);
        unit.FlashFocusHighlight(highlightColor);
    }

    private void OnHudPartyUnitSelected(string unitId)
    {
        var unit = FindUnitById(unitId);
        if (!IsUsableUnit(unit) || unit.Team != "player")
        {
            return;
        }

        _selectedCharacterUnitId = unit.UnitId;
        SyncHudFromGameState();
        _hud?.SetCharacterVisible(true);
    }

    private void OnHudPartyOrderRequested(string sourceUnitId, string targetUnitId)
    {
        if (_flowState != BattleFlowState.Exploration
            || string.IsNullOrEmpty(sourceUnitId)
            || string.IsNullOrEmpty(targetUnitId)
            || sourceUnitId == targetUnitId)
        {
            return;
        }

        var sourceIndex = -1;
        var targetIndex = -1;
        for (var i = 0; i < _playerUnits.Count; i++)
        {
            if (_playerUnits[i]?.UnitId == sourceUnitId)
            {
                sourceIndex = i;
            }
            if (_playerUnits[i]?.UnitId == targetUnitId)
            {
                targetIndex = i;
            }
        }

        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        var movedUnit = _playerUnits[sourceIndex];
        _playerUnits.RemoveAt(sourceIndex);
        _playerUnits.Insert(targetIndex, movedUnit);

        foreach (var unit in _playerUnits)
        {
            if (IsUsableUnit(unit) && !unit.IsDead)
            {
                _explorerUnit = unit;
                break;
            }
        }

        SyncHudFromGameState();
        CenterViewOnCurrentFocus();
        QueueRedraw();
    }

    private void OnHudReserveStoreRequested(string partyUnitId)
    {
        if (_flowState != BattleFlowState.Exploration)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(partyUnitId))
        {
            return;
        }

        if (_playerUnits.Count <= 1)
        {
            _hud?.AddCombatLogEntry("At least one member must remain in the active party.");
            return;
        }

        var unit = FindUnitById(partyUnitId);
        if (!IsUsableUnit(unit) || unit.Team != "player")
        {
            return;
        }

        AddOrUpdateReserveUnit(unit);
        RemovePartyUnit(unit, movedToReserve: true);
        var explorer = GetExplorerUnit();
        if (explorer == null)
        {
            _explorerUnit = _playerUnits.Count > 0 ? _playerUnits[0] : null;
        }

        _hud?.AddCombatLogEntry($"{unit.UnitName} moved to reserves.");
        SyncHudFromGameState();
        SaveMapInteractionStateForCurrentMap();
        _persistence.PersistSaveGame(false);
        QueueRedraw();
    }

    private void OnHudReserveBringRequested(string reserveUnitId, string replacePartyUnitId)
    {
        if (_flowState != BattleFlowState.Exploration)
        {
            return;
        }

        var explorer = GetExplorerUnit();
        if (!IsUsableUnit(explorer))
        {
            return;
        }

        BeginReserveRecruitInteraction(explorer, reserveUnitId, replacePartyUnitId);
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
            return;
        }

        EquipItemToUnit(target, itemData, itemId);
        ApplyEquippedItemBonuses(target);

        var itemName = GetString(itemData, "name", itemId);
        _hud?.AddCombatLogEntry($"{target.UnitName} equipped {itemName}.");
        SetStatusHelp();
        _persistence.PersistSaveGame(false);
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
            return;
        }

        if (!UnequipSlotForUnit(target, equippedSlotKey))
        {
            return;
        }

        EnsureSharedInventoryHasUnequippedCount(itemId, 1);

        ApplyEquippedItemBonuses(target);
        var itemName = _gameData == null
            ? itemId
            : GetString(_gameData.GetItem(itemId), "name", itemId);
        _hud?.AddCombatLogEntry($"{target.UnitName} unequipped {itemName}.");
        SetStatusHelp();
        _persistence.PersistSaveGame(false);
    }

    private void OnHudInventoryCycleRequested(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        CycleInventoryTarget(delta);
        SyncHudFromGameState();
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
            return;
        }

        if (!string.IsNullOrEmpty(interactionId) && interactionId.StartsWith("rest:"))
        {
            _hasActiveLootCell = false;
            _activeLootCell = new Vector2I(-1, -1);
            _hud?.SetLootPanelVisible(false);
            BeginRestPointInteraction(interactionId.Substring(5));
            return;
        }

        if (!TryExecuteExplorationInteractionById(explorer, interactionId))
        {
        }

        var refreshedEntries = new Array<Dictionary>();
        if (_hasActiveLootCell && _mapLoader != null)
        {
            _mapLoader.TryBuildExplorationClickLootEntries(explorer, _activeLootCell, _mapProps, _lootBags, _openedPropIds, _gameData, out refreshedEntries, out _);
        }

        if (refreshedEntries.Count > 0)
        {
            _hud?.SetLootEntries(refreshedEntries);
            _hud?.PositionLootPanelAboveCell(_activeLootCell, CellSize);
            _hud?.SetLootPanelVisible(true);
        }
        else
        {
            _hasActiveLootCell = false;
            _activeLootCell = new Vector2I(-1, -1);
            _hud?.SetLootPanelVisible(false);
        }

        SetStatusHelp();
        _persistence.PersistSaveGame(false);
    }

    private void TryResolvePlayerActionAtCell(Unit active, Vector2I targetCell)
    {
        var selectedAbilityId = GetSelectedAbilityId(active);
        var actionProfile = ResolveActionProfile(active, selectedAbilityId);

        if (!actionProfile.IgnoresActionCost && !active.CanUseAbilityThisTurn())
        {
            return;
        }

        if (active.GetAbilityCooldownRemaining(actionProfile.ActionId) > 0)
        {
            return;
        }

        if (!CanCastAction(active, actionProfile))
        {
            return;
        }

        if (actionProfile.ActionType == "defend")
        {
            return;
        }

        if (actionProfile.ActionType == "protection")
        {
            if (!TryUseProtectionAura(active, actionProfile))
            {
                return;
            }

            var result = ResolveSuccessfulAction(actionProfile.ActionType);
            ApplyActionResult(result);
            BeginPostPlayerActionMouseMoveLock();
            return;
        }

        if (actionProfile.ActionType == "sleep")
        {
            if (!TryCastSleepAtCell(active, targetCell, actionProfile))
            {
                return;
            }

            var result = ResolveSuccessfulAction(actionProfile.ActionType);
            ApplyActionResult(result);
            BeginPostPlayerActionMouseMoveLock();
            return;
        }

        if (actionProfile.ActionType == "charge")
        {
            if (!TryUseCharge(active, targetCell, actionProfile))
            {
                return;
            }

            var result = ResolveSuccessfulAction("attack");
            ApplyActionResult(result);
            BeginPostPlayerActionMouseMoveLock();
            return;
        }

        if (actionProfile.ActionType == "pin")
        {
            if (!TryUsePinShot(active, targetCell, actionProfile))
            {
                return;
            }

            var result = ResolveSuccessfulAction("attack");
            ApplyActionResult(result);
            BeginPostPlayerActionMouseMoveLock();
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
                BeginPostPlayerActionMouseMoveLock();
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
                BeginPostPlayerActionMouseMoveLock();
                return;
            }
        }

        if (actionProfile.ActionType != "attack")
        {
            return;
        }

        if (actionProfile.Range > 1)
        {
            return;
        }

        var moveResult = ResolveMoveAction(active, targetCell, endTurnOnSuccess: false);
        if (moveResult.Success)
        {
            ApplyActionResult(moveResult);
        }
        else
        {
        }
    }

    private bool TryUseProtectionAura(Unit caster, ActionProfile actionProfile)
    {
        if (!IsUsableUnit(caster) || caster.IsDead || _flowState != BattleFlowState.Combat)
        {
            return false;
        }

        if (!caster.TrySpendMagicPoints(actionProfile.MagicPointCost))
        {
            return false;
        }

        var actionData = GetActionData(actionProfile.ActionId, out _);
        var radius = Mathf.Max(0, GetInt(actionData, "area_radius", 3));
        var durationTurns = Mathf.Max(1, GetInt(actionData, "duration_turns", 2));
        var armorClassBonus = Mathf.Max(1, GetInt(actionData, "armor_class_bonus", 2));
        var buffedUnits = new List<string>();

        foreach (var unit in _allUnits)
        {
            if (!IsUsableUnit(unit) || unit.IsDead || unit.Team != caster.Team)
            {
                continue;
            }

            if (!Unit.IsWithinRange(caster.GridPos, unit.GridPos, radius))
            {
                continue;
            }

            unit.ApplyStatusEffect(
                "protection",
                "Protected",
                true,
                durationTurns,
                startDelayTurns: 0,
                damagePerTurn: 0,
                stackingMode: "refresh",
                maxStacks: 1,
                stackAmount: 1,
                scope: "combat_only",
                skipTurn: false,
                preventMovement: false,
                preventActions: false,
                wakeOnDamage: false,
                armorClassBonus: armorClassBonus
            );
            buffedUnits.Add(unit.UnitName);
        }

        caster.MarkAbilityUsed(actionProfile.ActionId, actionProfile.CooldownTurns);
        _eventBus?.EmitSignal(EventBus.SignalName.ActionUsed, caster, actionProfile.ActionId, caster.UnitId);

        if (buffedUnits.Count > 0)
        {
            _hud?.AddCombatLogEntry($"{caster.UnitName} casts {actionProfile.ActionName}. {string.Join(", ", buffedUnits)} gain +{armorClassBonus} armor class for {durationTurns} turn{(durationTurns == 1 ? "" : "s")}." + (actionProfile.MagicPointCost > 0 ? $" (MP -{actionProfile.MagicPointCost})" : ""));
        }
        else
        {
            _hud?.AddCombatLogEntry($"{caster.UnitName} casts {actionProfile.ActionName}, but no allies are in range." + (actionProfile.MagicPointCost > 0 ? $" (MP -{actionProfile.MagicPointCost})" : ""));
        }

        SyncHudFromGameState();
        QueueRedraw();
        return true;
    }

    private bool TryCastSleepAtCell(Unit caster, Vector2I centerCell, ActionProfile actionProfile)
    {
        if (!IsUsableUnit(caster) || caster.IsDead || _flowState != BattleFlowState.Combat)
        {
            return false;
        }

        if (!IsInBounds(centerCell) || !Unit.IsWithinRange(caster.GridPos, centerCell, actionProfile.Range) || !HasClearLineOfSight(caster.GridPos, centerCell))
        {
            return false;
        }

        var actionData = GetActionData(actionProfile.ActionId, out _);
        var radius = Mathf.Max(0, GetInt(actionData, "area_radius", actionProfile.AreaRadius));
        var durationTurns = Mathf.Max(1, GetInt(actionData, "duration_turns", 2));
        var sleptUnits = new List<string>();
        var immuneUnits = new List<string>();

        foreach (var unit in _allUnits)
        {
            if (!IsUsableUnit(unit) || unit.IsDead || !Unit.IsWithinRange(centerCell, unit.GridPos, radius))
            {
                continue;
            }

            if (IsUndead(unit))
            {
                immuneUnits.Add(unit.UnitName);
                continue;
            }

            sleptUnits.Add(unit.UnitName);
        }

        if (sleptUnits.Count == 0)
        {
            return false;
        }

        if (!caster.TrySpendMagicPoints(actionProfile.MagicPointCost))
        {
            return false;
        }

        foreach (var unit in _allUnits)
        {
            if (!IsUsableUnit(unit) || unit.IsDead || !Unit.IsWithinRange(centerCell, unit.GridPos, radius) || IsUndead(unit))
            {
                continue;
            }

            unit.ApplyStatusEffect(
                "sleep",
                "Asleep",
                false,
                durationTurns,
                startDelayTurns: 0,
                damagePerTurn: 0,
                stackingMode: "refresh",
                maxStacks: 1,
                stackAmount: 1,
                scope: "combat_only",
                skipTurn: true,
                preventMovement: true,
                preventActions: true,
                wakeOnDamage: true,
                armorClassBonus: 0
            );
        }

        caster.MarkAbilityUsed(actionProfile.ActionId, actionProfile.CooldownTurns);
        _eventBus?.EmitSignal(EventBus.SignalName.ActionUsed, caster, actionProfile.ActionId, caster.UnitId);

        var log = $"{caster.UnitName} casts {actionProfile.ActionName}. {string.Join(", ", sleptUnits)} fall asleep for {durationTurns} turns.";
        if (immuneUnits.Count > 0)
        {
            log += $" Undead are immune: {string.Join(", ", immuneUnits)}.";
        }

        if (actionProfile.MagicPointCost > 0)
        {
            log += $" (MP -{actionProfile.MagicPointCost})";
        }

        _hud?.AddCombatLogEntry(log);
        SyncHudFromGameState();
        QueueRedraw();
        return true;
    }

    private bool TryUseCharge(Unit attacker, Vector2I targetCell, ActionProfile actionProfile)
    {
        if (!IsUsableUnit(attacker) || attacker.IsDead || _flowState != BattleFlowState.Combat)
        {
            return false;
        }

        var target = GetLivingEnemyAtCell(attacker.Team, targetCell);
        if (!IsUsableUnit(target) || target.IsDead)
        {
            return false;
        }

        var maxChargeRange = Mathf.Max(1, actionProfile.Range);
        if (!Unit.IsWithinRange(attacker.GridPos, target.GridPos, maxChargeRange))
        {
            return false;
        }

        if (!TryFindChargeDestination(attacker, target, maxChargeRange, out var destinationCell, out var cellsUsed))
        {
            return false;
        }

        if (cellsUsed <= 0)
        {
            return false;
        }

        if (destinationCell != attacker.GridPos)
        {
            if (!TryMoveUnit(attacker, destinationCell))
            {
                return false;
            }

            if (ResolveCombatTraps(attacker, out var combatEnded))
            {
                if (combatEnded || attacker.IsDead)
                {
                    return false;
                }
            }
        }

        var chargeDamage = 2 * cellsUsed;
        return TryAttackTarget(attacker, target, chargeDamage, 1, actionProfile.ActionId, actionProfile.ActionName, actionProfile.CooldownTurns, actionProfile.MagicPointCost, actionProfile.IsMagical, consumeAction: false);
    }

    private bool TryFindChargeDestination(Unit attacker, Unit target, int maxChargeRange, out Vector2I destinationCell, out int cellsUsed)
    {
        destinationCell = attacker.GridPos;
        cellsUsed = -1;

        foreach (var direction in AttackDirections)
        {
            var candidate = target.GridPos + direction;
            if (!IsInBounds(candidate) || IsBlockedCell(candidate) || (candidate != attacker.GridPos && IsOccupied(candidate, attacker)))
            {
                continue;
            }

            var steps = candidate == attacker.GridPos
                ? 0
                : FindPath(attacker, attacker.GridPos, candidate, maxChargeRange).Count;

            if (candidate != attacker.GridPos && steps <= 0)
            {
                continue;
            }

            if (steps > maxChargeRange)
            {
                continue;
            }

            if (cellsUsed == -1 || steps < cellsUsed)
            {
                cellsUsed = steps;
                destinationCell = candidate;
            }
        }

        return cellsUsed >= 0;
    }

    private bool TryUsePinShot(Unit attacker, Vector2I targetCell, ActionProfile actionProfile)
    {
        if (!IsUsableUnit(attacker) || attacker.IsDead || _flowState != BattleFlowState.Combat)
        {
            return false;
        }

        var target = GetLivingEnemyAtCell(attacker.Team, targetCell);
        if (!IsUsableUnit(target) || target.IsDead)
        {
            return false;
        }

        if (!TryAttackTarget(attacker, target, actionProfile.Damage, actionProfile.Range, actionProfile.ActionId, actionProfile.ActionName, actionProfile.CooldownTurns, actionProfile.MagicPointCost, actionProfile.IsMagical, consumeAction: false))
        {
            return false;
        }

        var actionData = GetActionData(actionProfile.ActionId, out _);
        var durationTurns = Mathf.Max(1, GetInt(actionData, "duration_turns", 1));
        var effectiveDurationTurns = durationTurns + 1;
        target.ApplyStatusEffect(
            "pinned",
            "Pinned",
            false,
            effectiveDurationTurns,
            startDelayTurns: 0,
            damagePerTurn: 0,
            stackingMode: "refresh",
            maxStacks: 1,
            stackAmount: 1,
            scope: "combat_only",
            skipTurn: false,
            preventMovement: true,
            preventActions: false,
            wakeOnDamage: false,
            armorClassBonus: 0
        );
        _hud?.AddCombatLogEntry($"{target.UnitName} is pinned and cannot move for {durationTurns} turn{(durationTurns == 1 ? "" : "s")}. ");
        SyncHudFromGameState();
        QueueRedraw();
        return true;
    }

    private void BeginPostPlayerActionMouseMoveLock()
    {
        _mouseMoveInputLockedUntilMs = Time.GetTicksMsec() + PostPlayerActionMouseMoveLockMs;
        ClearMovementPreviewPath();
    }

    private bool IsMouseMoveInputLocked()
    {
        return Time.GetTicksMsec() < _mouseMoveInputLockedUntilMs;
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

            var actionBeforeMove = TryExecuteAiAction(enemyUnit);
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

            // Enemies can spend up to their movement budget while pathing toward a usable attack position.
            while (_flowState == BattleFlowState.Combat && IsCurrentActiveUnit(enemyUnit) && enemyUnit.CanMoveThisTurn())
            {
                if (!_aiDirector.TryChooseStepTowardActionRange(
                    enemyUnit,
                    _playerUnits,
                    BuildAiActionOptions(enemyUnit),
                    target => IsValidAttackTarget(enemyUnit, target),
                    cell => IsInBounds(cell) && !IsBlockedCell(cell) && !IsOccupied(cell, enemyUnit),
                    HasClearLineOfSight,
                    goal => FindPath(enemyUnit, enemyUnit.GridPos, goal, enemyUnit.RemainingMovement),
                    out var step))
                {
                    break;
                }

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

            var actionAfterMove = TryExecuteAiAction(enemyUnit);
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

        if (!_turnManager.IsActiveUnit(expectedActiveUnit))
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
            return _turnManager.EndTurnIfActive(expectedActiveUnit);
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

        return _turnManager.IsActiveUnit(unit);
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
    private void SpawnMapEncounter(string mapId, bool preserveParty = false, Vector2I leadSpawnCell = default, bool preserveCurrentMapState = true)
    {
        if (preserveCurrentMapState)
        {
            SaveClearedEncounterStateForCurrentMap();
        }

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
            _reservePartyRoster.Clear();
            _partyGold = 25;
            _vendorGoldById.Clear();
            _vendorInventoryItemIdsById.Clear();
            EnsureDefaultVendorState();
        }

        _wallCells.Clear();
        _walkableCells.Clear();
        _mapDoors.Clear();
        _mapTransitions.Clear();
        _mapProps.Clear();
        _lootBags.Clear();
        _encounterAggroRanges.Clear();
        _activeEncounterId = "";
        _currentMapId = mapId;

        var mapData = _mapLoader?.LoadMapStub(mapId) ?? new MapLoader().LoadMapStub(mapId);
        _currentMapId = GetString(mapData, "id", mapId);
        _gridWidth = Mathf.Max(1, GetInt(mapData, "width", DefaultGridWidth));
        _gridHeight = Mathf.Max(1, GetInt(mapData, "height", DefaultGridHeight));
        ClampViewPositionToBounds();
        _mapLoader?.SetActiveMapVisual(_currentMapId);
        LoadClearedEncounterStateForCurrentMap();
        LoadMapInteractionStateForCurrentMap();

        var walls = TryGetVector2IArray(mapData, "walls");

        foreach (var wallCell in walls)
        {
            _wallCells.Add(wallCell);
        }

        var walkableCells = TryGetVector2IArray(mapData, "walkable_cells");
        foreach (var walkableCell in walkableCells)
        {
            _walkableCells.Add(walkableCell);
        }

        var doors = TryGetDictionaryArray(mapData, "doors");
        foreach (var door in doors)
        {
            var copiedDoor = CopyDictionary(door);
            _mapDoors.Add(copiedDoor);

            var doorId = GetString(copiedDoor, "id", "");
            if (!string.IsNullOrEmpty(doorId) && GetBool(copiedDoor, "is_open", false))
            {
                _openedDoorIds.Add(doorId);
            }
        }

        var transitions = TryGetDictionaryArray(mapData, "transitions");
        foreach (var transition in transitions)
        {
            _mapTransitions.Add(transition);
        }

        LoadPropsFromMap(mapData);
        SyncDoorVisualStateForCurrentMap();

        if (preserveParty)
        {
            PositionPartyForMapTransition(leadSpawnCell);
        }
        else
        {
            SpawnPlayersFromMap(mapData);
        }

        SpawnEnemiesFromMap(mapData);
        CenterViewOnCurrentFocus();
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
                var enemyId = GetString(enemyConfig, "id", "");
                if (!string.IsNullOrEmpty(enemyId) && _defeatedEnemyIds.Contains(enemyId))
                {
                    continue;
                }

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
            ApplyStartingEquipmentFromConfig(unit, config, addToPartyInventory: true);
        }
        else
        {
            _enemyUnits.Add(unit);
            ApplyStartingEquipmentFromConfig(unit, config, addToPartyInventory: false);
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
        QueueRedraw();
        return true;
    }

    private CombatActionResult TryExecuteAiAction(Unit actor)
    {
        if (actor == null || actor.Team != "enemy" || actor.AbilityIds == null)
        {
            return CombatActionResult.Failed;
        }

        var choice = _aiDirector.ChooseAction(actor, BuildAiActionOptions(actor), _allUnits, CanAiActionTarget);
        if (!choice.HasChoice || choice.Target == null)
        {
            return CombatActionResult.Failed;
        }

        var actionProfile = ResolveActionProfile(actor, choice.AbilityId);
        SetSelectedAbilityId(actor, choice.AbilityId);
        var actionSuccess = actionProfile.ActionType == "heal"
            ? TryHealTarget(actor, choice.Target, actionProfile.HealAmount, actionProfile.Range, actionProfile.ActionId, actionProfile.ActionName, actionProfile.CooldownTurns, actionProfile.MagicPointCost, actionProfile.IsMagical)
            : TryAttackTarget(actor, choice.Target, actionProfile.Damage, actionProfile.Range, actionProfile.ActionId, actionProfile.ActionName, actionProfile.CooldownTurns, actionProfile.MagicPointCost, actionProfile.IsMagical);

        if (!actionSuccess)
        {
            return CombatActionResult.Failed;
        }

        return ResolveSuccessfulAction(actionProfile.ActionType);
    }

    private List<AiDirector.ActionOption> BuildAiActionOptions(Unit actor)
    {
        var options = new List<AiDirector.ActionOption>();
        if (!IsUsableUnit(actor) || actor.AbilityIds == null)
        {
            return options;
        }

        foreach (var abilityId in actor.AbilityIds)
        {
            if (IsPassiveAbilityId(abilityId))
            {
                continue;
            }

            var profile = ResolveActionProfile(actor, abilityId);
            var canUseNow = CanUseActionProfileNow(actor, profile);
            var canPlanFromMovement = profile.ActionType == "attack"
                && actor.GetAbilityCooldownRemaining(profile.ActionId) <= 0
                && CanCastAction(actor, profile);
            options.Add(new AiDirector.ActionOption(abilityId, profile.ActionType, profile.Range, canUseNow, canPlanFromMovement));
        }

        return options;
    }

    private bool CanAiActionTarget(Unit actor, Unit target, AiDirector.ActionOption option)
    {
        if (option.ActionType == "heal")
        {
            return IsValidAllyTarget(actor, target)
                && actor.CanHealTarget(target, option.Range, _allUnits)
                && HasClearLineOfSight(actor.GridPos, target.GridPos);
        }

        return IsValidAttackTarget(actor, target)
            && actor.CanAttackTarget(target, option.Range, _allUnits)
            && HasClearLineOfSight(actor.GridPos, target.GridPos);
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
            return CombatActionResult.Failed;
        }

        if (!TryMoveUnit(unit, targetCell))
        {
            return CombatActionResult.Failed;
        }

        if (_flowState == BattleFlowState.Combat)
        {
            if (ResolveCombatTraps(unit, out var combatEnded))
            {
                if (combatEnded)
                {
                    return CombatActionResult.CombatResolvedResult;
                }

                if (unit.IsDead)
                {
                    TryRequestEndTurn(unit, manualInput: false);
                    return CombatActionResult.Failed;
                }
            }

            unit.TrySpendMovement();
            SetStatusHelp();
        }

        return endTurnOnSuccess ? CombatActionResult.MoveAndEndTurnResolved : CombatActionResult.MoveResolved;
    }

    private bool TryAttackTarget(Unit attacker, Unit target, int damage, int range, string actionId = "attack", string actionName = "Attack", int cooldownTurns = 0, int magicPointCost = 0, bool isMagical = false, bool consumeAction = true)
    {
        if (_flowState == BattleFlowState.Combat && consumeAction && !attacker.CanUseAbilityThisTurn())
        {
            return false;
        }

        if (!attacker.CanAttackTarget(target, range, _allUnits))
        {
            return false;
        }

        if (!HasClearLineOfSight(attacker.GridPos, target.GridPos))
        {
            return false;
        }

        if (!CanCastAction(attacker, new ActionProfile(actionId, actionName, "attack", range, 0, damage, 0, cooldownTurns, magicPointCost, isMagical, !consumeAction, false)))
        {
            return false;
        }

        if (!attacker.TrySpendMagicPoints(magicPointCost))
        {
            return false;
        }

        var preDefenseDamage = isMagical ? damage : Mathf.Max(0, damage - target.ArmorClass);
        var mitigatedDamage = target.ApplyDamage(preDefenseDamage);
        if (_flowState == BattleFlowState.Combat)
        {
            if (consumeAction)
            {
                attacker.MarkAbilityUsed(actionId, cooldownTurns);
            }
            else
            {
                attacker.MarkAbilityCooldownOnly(actionId, cooldownTurns);
            }
        }

        _eventBus?.EmitSignal(EventBus.SignalName.ActionUsed, attacker, actionId, target.UnitId);

        var resultText = isMagical
            ? $"{attacker.UnitName} casts {actionName} on {target.UnitName}, dealing {mitigatedDamage} damage."
            : actionId == "attack" || actionId == "melee" || actionId == "ranged"
                ? $"{attacker.UnitName} hits {target.UnitName} for {mitigatedDamage}."
                : $"{attacker.UnitName} uses {actionName} on {target.UnitName}, dealing {mitigatedDamage} damage.";
        if (!isMagical && target.ArmorClass > 0)
        {
            var reducedBy = Mathf.Max(0, damage - preDefenseDamage);
            if (reducedBy > 0)
            {
                resultText += $" ({reducedBy} blocked by armor_class)";
            }
        }

        if (target.IsDefending && preDefenseDamage > mitigatedDamage)
        {
            resultText += $" ({preDefenseDamage - mitigatedDamage} blocked by defend)";
        }

        if (magicPointCost > 0)
        {
            resultText += $" (MP -{magicPointCost})";
        }

        if (mitigatedDamage > 0 && !target.IsDead)
        {
            var awakenedEffects = target.ClearWakeOnDamageStatusEffects();
            if (awakenedEffects.Count > 0)
            {
                resultText += $" {target.UnitName} wakes up.";
            }

            var onHitStatusText = TryApplyOnHitStatusEffects(attacker, target);
            if (!string.IsNullOrEmpty(onHitStatusText))
            {
                resultText += $" {onHitStatusText}";
            }
        }

        if (target.IsDead)
        {
            resultText += $" {target.UnitName} is defeated.";
            var xpSummary = AwardExperienceForDefeat(attacker, target);
            if (!string.IsNullOrEmpty(xpSummary))
            {
                resultText += $" {xpSummary}";
            }
        }

        _hud?.AddCombatLogEntry(resultText);
        return true;
    }

    private CombatActionResult TryDefend(Unit actor, ActionProfile actionProfile)
    {
        if (_flowState == BattleFlowState.Combat && !actor.CanUseAbilityThisTurn())
        {
            return CombatActionResult.Failed;
        }

        if (!CanCastAction(actor, actionProfile))
        {
            return CombatActionResult.Failed;
        }

        actor.MarkDefending();
        if (_flowState == BattleFlowState.Combat)
        {
            actor.MarkAbilityUsed(actionProfile.ActionId, actionProfile.CooldownTurns);
        }

        _eventBus?.EmitSignal(EventBus.SignalName.ActionUsed, actor, actionProfile.ActionId, actor.UnitId);
        var resultText = $"{actor.UnitName} defends, reducing incoming damage by {Unit.DefendDamageReductionPercent}% until their next turn.";
        _hud?.AddCombatLogEntry(resultText);
        SyncHudFromGameState();
        QueueRedraw();
        return CombatActionResult.AttackResolved;
    }

    private string AwardExperienceForDefeat(Unit attacker, Unit defeatedTarget)
    {
        if (!IsUsableUnit(attacker) || !IsUsableUnit(defeatedTarget))
        {
            return "";
        }

        if (attacker.Team != "player" || defeatedTarget.Team != "enemy")
        {
            return "";
        }

        var livingParty = new List<Unit>();
        foreach (var unit in _playerUnits)
        {
            if (IsUsableUnit(unit) && !unit.IsDead)
            {
                livingParty.Add(unit);
            }
        }

        if (livingParty.Count == 0)
        {
            return "";
        }

        var xpReward = Mathf.Max(1, defeatedTarget.MaxHitPoints + defeatedTarget.MaxMagicPoints);
        var xpShare = Mathf.Max(1, xpReward / livingParty.Count);
        var levelUpNames = new List<string>();
        foreach (var unit in livingParty)
        {
            var levelsGained = unit.GrantExperience(xpShare);
            if (levelsGained > 0)
            {
                levelUpNames.Add($"{unit.UnitName} to level {unit.Level}");
            }
        }

        if (levelUpNames.Count > 0)
        {
            return $"Party gains {xpReward} XP ({xpShare} each). Level up: {string.Join(", ", levelUpNames)}!";
        }

        return $"Party gains {xpReward} XP ({xpShare} each).";
    }

    private bool TryHealTarget(Unit actor, Unit target, int healAmount, int range, string actionId, string actionName, int cooldownTurns = 0, int magicPointCost = 0, bool isMagical = false, bool consumeAction = true)
    {
        if (_flowState == BattleFlowState.Combat && consumeAction && !actor.CanUseAbilityThisTurn())
        {
            return false;
        }

        if (!actor.CanHealTarget(target, range, _allUnits))
        {
            return false;
        }

        if (!HasClearLineOfSight(actor.GridPos, target.GridPos))
        {
            return false;
        }

        if (!CanCastAction(actor, new ActionProfile(actionId, actionName, "heal", range, 0, 0, healAmount, cooldownTurns, magicPointCost, isMagical, !consumeAction, false)))
        {
            return false;
        }

        var healable = Mathf.Min(Mathf.Max(0, healAmount), Mathf.Max(0, target.MaxHitPoints - target.HitPoints));
        if (healable <= 0)
        {
            return false;
        }

        if (!actor.TrySpendMagicPoints(magicPointCost))
        {
            return false;
        }

        var healed = target.ApplyHealing(healAmount);

        if (_flowState == BattleFlowState.Combat)
        {
            if (consumeAction)
            {
                actor.MarkAbilityUsed(actionId, cooldownTurns);
            }
            else
            {
                actor.MarkAbilityCooldownOnly(actionId, cooldownTurns);
            }
        }

        _eventBus?.EmitSignal(EventBus.SignalName.ActionUsed, actor, actionId, target.UnitId);
        var resultText = isMagical
            ? $"{actor.UnitName} casts {actionName} on {target.UnitName}, restoring {healed} HP."
            : $"{actor.UnitName} heals {target.UnitName} for {healed}.";
        if (magicPointCost > 0)
        {
            resultText += $" (MP -{magicPointCost})";
        }
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

            var distance = Unit.RangeDistance(attacker.GridPos, unit.GridPos);
            if (distance <= range && distance < nearestDistance && attacker.CanAttackTarget(unit, range, _allUnits) && HasClearLineOfSight(attacker.GridPos, unit.GridPos))
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

            if (!actor.CanUseActionAtRange(unit, range, _allUnits))
            {
                continue;
            }

            if (!HasClearLineOfSight(actor.GridPos, unit.GridPos))
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
        RecordDefeatedEnemiesForCurrentMap();
        RemoveDeadFromTeam(_playerUnits);
        RemoveDeadFromTeam(_enemyUnits);
    }

    private void RecordDefeatedEnemiesForCurrentMap()
    {
        foreach (var enemy in _enemyUnits)
        {
            if (!IsUsableUnit(enemy) || !enemy.IsDead || string.IsNullOrEmpty(enemy.UnitId))
            {
                continue;
            }

            _defeatedEnemyIds.Add(enemy.UnitId);
        }
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
            _hud?.ShowCombatBanner("COMBAT ENDED - DEFEAT", new Color(0.9f, 0.28f, 0.25f, 1.0f));
            _hud?.AddCombatLogEntry("Combat ended. The party was defeated.");
            _flowState = BattleFlowState.Defeat;
            ClearCombatOnlyDebuffsForParty();
            _eventBus?.EmitSignal(EventBus.SignalName.CombatEnded);
            SyncHudFromGameState();
            _persistence.PersistSaveGame(false);
            return true;
        }

        if (_enemyUnits.Count == 0)
        {
            _hud?.ShowCombatBanner("COMBAT ENDED - VICTORY", new Color(0.42f, 0.88f, 0.56f, 1.0f));
            _hud?.AddCombatLogEntry("Combat ended. Encounter cleared.");
            _eventBus?.EmitSignal(EventBus.SignalName.CombatEnded);
            EnterExplorationMode("Encounter cleared. Exploration resumed.");
            _persistence.PersistSaveGame(false);
            return true;
        }

        if (!HasLivingActiveCombatEnemies())
        {
            _hud?.ShowCombatBanner("COMBAT ENDED - VICTORY", new Color(0.42f, 0.88f, 0.56f, 1.0f));
            _hud?.AddCombatLogEntry("Combat ended. Encounter cleared.");
            MarkClearedEncounterIdsForActiveCombat();
            SaveClearedEncounterStateForCurrentMap();
            _activeEncounterId = "";
            _activeCombatEnemyUnitIds.Clear();
            _activeCombatEncounterIds.Clear();
            _eventBus?.EmitSignal(EventBus.SignalName.CombatEnded);
            EnterExplorationMode("Encounter cleared. Exploration resumed.");
            _persistence.PersistSaveGame(false);
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

    private bool IsInBounds(Vector2I cell)
    {
        if (_walkableCells.Count > 0)
        {
            return _walkableCells.Contains(cell);
        }

        return cell.X >= 0 && cell.X < _gridWidth && cell.Y >= 0 && cell.Y < _gridHeight;
    }

    private bool IsBlockedCell(Vector2I cell)
    {
        foreach (var wall in _wallCells)
        {
            if (wall == cell)
            {
                return true;
            }
        }

        if (TryGetDoorAtCell(cell, out var door) && !IsDoorOpen(door))
        {
            return true;
        }

        return false;
    }

    private bool TryGetDoorAtCell(Vector2I cell, out Dictionary door)
    {
        foreach (var entry in _mapDoors)
        {
            var doorCell = GetVector2I(entry, "cell", new Vector2I(-9999, -9999));
            if (doorCell != cell)
            {
                continue;
            }

            door = entry;
            return true;
        }

        door = null;
        return false;
    }

    private bool IsDoorOpen(Dictionary door)
    {
        if (door == null)
        {
            return false;
        }

        var doorId = GetString(door, "id", "");
        if (!string.IsNullOrEmpty(doorId) && _openedDoorIds.Contains(doorId))
        {
            return true;
        }

        return GetBool(door, "is_open", false);
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

    private static int Manhattan(Vector2I a, Vector2I b)
    {
        return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
    }

    private bool HasClearLineOfSight(Vector2I from, Vector2I to)
    {
        var points = Unit.GetLinePoints(from, to);
        for (var i = 1; i < points.Count - 1; i++)
        {
            if (IsBlockedCell(points[i]))
            {
                return false;
            }
        }

        return true;
    }

    private ActionProfile ResolveActionProfile(Unit actor, string abilityId = null)
    {
        if (actor == null)
        {
            return new ActionProfile("attack", "Attack", "attack", 1, 0, 0, 0, 0, 0, false, false, false);
        }

        abilityId = string.IsNullOrEmpty(abilityId) ? GetSelectedAbilityId(actor) : abilityId;
        if (!actor.HasAbility(abilityId))
        {
            abilityId = actor.PrimaryAbilityId;
        }

        var fallback = new ActionProfile(abilityId, abilityId, "attack", actor.AttackRange, 0, actor.AttackDamage, 0, 0, 0, false, false, false);
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

        var configuredRange = GetInt(actionData, "range", actor.AttackRange);
        var configuredDamage = GetInt(actionData, "damage", actor.AttackDamage);
        var areaRadius = Mathf.Max(0, GetInt(actionData, "area_radius", 0));
        var ignoresActionCost = GetBool(actionData, "ignores_action_cost", false);
        var requiresRangedWeapon = GetBool(actionData, "requires_ranged_weapon", false);

        // Physical attacks commonly use 0 in data as a placeholder for "use unit stats".
        var shouldUseActorCombatStats = (actionType == "attack" || actionType == "pin") && !isMagical;
        var range = shouldUseActorCombatStats && configuredRange <= 0
            ? actor.AttackRange
            : configuredRange;
        var damage = shouldUseActorCombatStats && configuredDamage <= 0
            ? actor.AttackDamage
            : configuredDamage;

        range = actionType is "defend" or "protection" ? 0 : Mathf.Max(1, range);
        damage = Mathf.Max(0, damage);
        var healAmount = Mathf.Max(0, GetInt(actionData, "heal_amount", 0));
        var cooldownTurns = Mathf.Max(0, GetInt(actionData, "cooldown", 0));
        var mpCost = Mathf.Max(0, GetInt(actionData, "mp_cost", 0));

        return new ActionProfile(abilityId, actionName, actionType, range, areaRadius, damage, healAmount, cooldownTurns, mpCost, isMagical, ignoresActionCost, requiresRangedWeapon);
    }

    private bool CanCastAction(Unit actor, ActionProfile actionProfile)
    {
        if (actor == null)
        {
            return false;
        }

        if (actionProfile.RequiresRangedWeapon && !CanUseRangedWeaponAbility(actor))
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

        return false;
    }

    private bool CanUseActionProfileNow(Unit actor, ActionProfile actionProfile)
    {
        if (!IsUsableUnit(actor) || actor.IsDead)
        {
            return false;
        }

        if (!actionProfile.IgnoresActionCost && !actor.CanUseAbilityThisTurn())
        {
            return false;
        }

        if (actor.GetAbilityCooldownRemaining(actionProfile.ActionId) > 0)
        {
            return false;
        }

        return CanCastAction(actor, actionProfile);
    }

    private bool CanUseRangedWeaponAbility(Unit actor)
    {
        if (!IsUsableUnit(actor) || _gameData == null || string.IsNullOrEmpty(actor.UnitId))
        {
            return false;
        }

        if (!_equippedItemsByUnitId.TryGetValue(actor.UnitId, out var equippedBySlot) || equippedBySlot.Count == 0)
        {
            return false;
        }

        foreach (var itemId in equippedBySlot.Values)
        {
            var itemData = _gameData.GetItem(itemId);
            if (itemData.Count == 0)
            {
                continue;
            }

            if (GetString(itemData, "type", "") != "weapon")
            {
                continue;
            }

            if (GetInt(itemData, "range", 0) > 1)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyStartOfTurnStatusEffects(Unit activeUnit)
    {
        if (!IsUsableUnit(activeUnit) || activeUnit.IsDead)
        {
            return;
        }

        var events = activeUnit.ProcessStartOfTurnStatusEffects();
        foreach (var entry in events)
        {
            var statusName = GetString(entry, "display_name", "Effect");
            var damage = GetInt(entry, "damage", 0);
            var turnsLeft = GetInt(entry, "remaining_turns", 0);
            var stacks = Mathf.Max(1, GetInt(entry, "stacks", 1));

            if (damage > 0)
            {
                var message = $"{activeUnit.UnitName} takes {damage} damage from {statusName.ToLowerInvariant()}";
                if (stacks > 1)
                {
                    message += $" ({stacks} stacks)";
                }

                if (turnsLeft > 0)
                {
                    message += $" ({turnsLeft} turn{(turnsLeft == 1 ? "" : "s")} left)";
                }

                message += ".";

                var awakenedEffects = activeUnit.ClearWakeOnDamageStatusEffects();
                if (awakenedEffects.Count > 0)
                {
                    message += $" {activeUnit.UnitName} wakes up.";
                }

                _hud?.AddCombatLogEntry(message);
            }
        }

        SyncHudFromGameState();
        QueueRedraw();
    }

    private string TryApplyOnHitStatusEffects(Unit attacker, Unit target)
    {
        if (!IsUsableUnit(attacker) || !IsUsableUnit(target) || target.IsDead || _gameData == null)
        {
            return "";
        }

        var appliedMessages = new List<string>();
        foreach (var abilityId in attacker.AbilityIds)
        {
            var abilityData = _gameData.GetAbility(abilityId);
            if (abilityData.Count == 0)
            {
                continue;
            }

            if (GetString(abilityData, "type", "").ToLowerInvariant() != "passive")
            {
                continue;
            }

            if (GetString(abilityData, "trigger", "").ToLowerInvariant() != "on_hit")
            {
                continue;
            }

            var effectId = GetString(abilityData, "effect_id", "");
            if (string.IsNullOrEmpty(effectId))
            {
                continue;
            }

            var effectName = GetString(abilityData, "effect_name", effectId);
            var durationTurns = Mathf.Max(1, GetInt(abilityData, "duration_turns", 1));
            var startDelayTurns = Mathf.Max(0, GetInt(abilityData, "start_delay_turns", 0));
            var damagePerTurn = Mathf.Max(0, GetInt(abilityData, "damage_per_turn", 0));
            var effectKind = GetString(abilityData, "effect_kind", "debuff").ToLowerInvariant();
            var isBuff = effectKind == "buff";
            var stackingMode = GetString(abilityData, "stacking_mode", "refresh");
            var maxStacks = Mathf.Max(1, GetInt(abilityData, "max_stacks", 1));
            var stackAmount = Mathf.Max(1, GetInt(abilityData, "stack_amount", 1));
            var effectScope = GetString(abilityData, "effect_scope", "persistent");

            var applied = target.ApplyStatusEffect(effectId, effectName, isBuff, durationTurns, startDelayTurns, damagePerTurn, stackingMode, maxStacks, stackAmount, effectScope);
            if (!GetBool(applied, "applied", false))
            {
                continue;
            }

            var message = $"{target.UnitName} is {effectName.ToLowerInvariant()}";
            if (durationTurns > 0)
            {
                message += $" for {durationTurns} turn{(durationTurns == 1 ? "" : "s")}";
            }

            var stacks = Mathf.Max(1, GetInt(applied, "stacks", 1));
            if (stacks > 1)
            {
                message += $" ({stacks} stacks)";
            }

            message += ".";
            appliedMessages.Add(message);
        }

        return appliedMessages.Count == 0 ? "" : string.Join(" ", appliedMessages);
    }

    private bool IsPassiveAbilityId(string abilityId)
    {
        if (string.IsNullOrEmpty(abilityId) || _gameData == null)
        {
            return false;
        }

        var abilityData = _gameData.GetAbility(abilityId);
        if (abilityData.Count == 0)
        {
            return false;
        }

        return GetString(abilityData, "type", "attack").ToLowerInvariant() == "passive";
    }

    private void ClearCombatOnlyDebuffsForParty()
    {
        foreach (var unit in _playerUnits)
        {
            if (!IsUsableUnit(unit))
            {
                continue;
            }

            var removedCount = unit.ClearStatusEffectsByScope("combat_only", includeBuffs: false);
            if (removedCount > 0)
            {
                _hud?.AddCombatLogEntry($"{unit.UnitName} shakes off lingering combat debuffs.");
            }
        }
    }

    private string GetSelectedAbilityId(Unit unit)
    {
        if (unit == null)
        {
            return "";
        }

        if (_selectedAbilityIdByUnitId.TryGetValue(unit.UnitId, out var selectedId) && unit.HasAbility(selectedId) && !IsPassiveAbilityId(selectedId))
        {
            return selectedId;
        }

        if (!string.IsNullOrEmpty(unit.PrimaryAbilityId) && unit.HasAbility(unit.PrimaryAbilityId) && !IsPassiveAbilityId(unit.PrimaryAbilityId))
        {
            _selectedAbilityIdByUnitId[unit.UnitId] = unit.PrimaryAbilityId;
            return unit.PrimaryAbilityId;
        }

        if (unit.AbilityIds != null && unit.AbilityIds.Count > 0)
        {
            foreach (var abilityId in unit.AbilityIds)
            {
                if (IsPassiveAbilityId(abilityId))
                {
                    continue;
                }

                _selectedAbilityIdByUnitId[unit.UnitId] = abilityId;
                return abilityId;
            }
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
        if (unit == null || unit.IsDead || unit.AbilityIds == null)
        {
            return false;
        }

        foreach (var abilityId in unit.AbilityIds)
        {
            if (IsPassiveAbilityId(abilityId))
            {
                continue;
            }

            var profile = ResolveActionProfile(unit, abilityId);
            if (!profile.IgnoresActionCost && !unit.CanUseAbilityThisTurn())
            {
                continue;
            }

            if (unit.GetAbilityCooldownRemaining(abilityId) <= 0 && CanCastAction(unit, profile))
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
            if (IsPassiveAbilityId(abilityId))
            {
                continue;
            }

            var profile = ResolveActionProfile(unit, abilityId);
            var actionName = string.IsNullOrEmpty(profile.ActionName) ? GetActionDisplayName(abilityId) : profile.ActionName;
            var cooldownRemaining = unit.GetAbilityCooldownRemaining(abilityId);
            var valueText = profile.ActionType == "heal"
                ? $"Heal: {profile.HealAmount}"
                : profile.ActionType == "defend"
                    ? $"Effect: damage taken -{Unit.DefendDamageReductionPercent}%"
                    : profile.ActionType == "sleep"
                        ? $"Effect: sleep ({profile.AreaRadius}-cell radius)"
                        : profile.ActionType == "protection"
                            ? $"Effect: +armor class aura ({profile.AreaRadius}-cell radius)"
                            : profile.ActionType == "charge"
                                ? "Effect: charge attack (free action)"
                                : profile.ActionType == "pin"
                                    ? "Effect: pin target for 1 turn"
                                    : $"Damage: {profile.Damage}";
            var mpCostLabel = profile.MagicPointCost <= 0
                ? "MP Cost: none"
                : $"MP Cost: {profile.MagicPointCost}";
            var cooldownLabel = profile.CooldownTurns <= 0
                ? "Cooldown: none"
                : $"Cooldown: {profile.CooldownTurns} turn{(profile.CooldownTurns == 1 ? "" : "s")}";
            var actionCostLabel = profile.IgnoresActionCost
                ? "Action Cost: free"
                : "Action Cost: uses action";
            var requirementLabel = profile.RequiresRangedWeapon
                ? "Requirement: ranged weapon equipped"
                : "Requirement: none";
            var isEnabled = CanUseActionProfileNow(unit, profile);
            var stateLabel = cooldownRemaining > 0
                ? $"Status: on cooldown ({cooldownRemaining} remaining)"
                : profile.RequiresRangedWeapon && !CanUseRangedWeaponAbility(unit)
                    ? "Status: requires a ranged weapon"
                    : !profile.IgnoresActionCost && !unit.CanUseAbilityThisTurn()
                        ? "Status: action already used"
                    : !CanCastAction(unit, profile)
                        ? $"Status: needs MP ({unit.MagicPoints}/{profile.MagicPointCost})"
                        : "Status: ready";
            entries.Add(new Dictionary
            {
                { "id", abilityId },
                { "label", actionName },
                { "detail", $"{actionName}\nType: {profile.ActionType}\nRange: {profile.Range}\nArea Radius: {profile.AreaRadius}\n{valueText}\n{mpCostLabel}\n{cooldownLabel}\n{actionCostLabel}\n{requirementLabel}\n{stateLabel}" },
                { "cooldown_remaining", cooldownRemaining },
                { "is_enabled", isEnabled ? 1 : 0 },
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

    private Dictionary GetActionData(string actionId, out bool isSpell)
    {
        isSpell = false;
        if (string.IsNullOrEmpty(actionId) || _gameData == null)
        {
            return new Dictionary();
        }

        var actionData = _gameData.GetAbility(actionId);
        if (actionData.Count > 0)
        {
            return actionData;
        }

        actionData = _gameData.GetSpell(actionId);
        isSpell = actionData.Count > 0;
        return actionData;
    }

    private static bool IsUndead(Unit unit)
    {
        if (!IsUsableUnit(unit))
        {
            return false;
        }

        return string.Equals(unit.Race, "undead", System.StringComparison.OrdinalIgnoreCase);
    }

    private static int GetInt(Dictionary dict, string key, int fallback)
    {
        return dict.ContainsKey(key) ? (int)((Variant)dict[key]) : fallback;
    }

    private static float GetFloat(Dictionary dict, string key, float fallback)
    {
        if (!dict.ContainsKey(key))
        {
            return fallback;
        }

        var value = (Variant)dict[key];
        if (value.VariantType == Variant.Type.Float)
        {
            return (float)value;
        }

        if (value.VariantType == Variant.Type.Int)
        {
            return (int)value;
        }

        return float.TryParse(value.AsString(), out var parsed) ? parsed : fallback;
    }

    // UI helpers

    private static bool GetBool(Dictionary dict, string key, bool fallback)
    {
        if (!dict.ContainsKey(key))
        {
            return fallback;
        }

        var value = (Variant)dict[key];
        return value.VariantType switch
        {
            Variant.Type.Bool => (bool)value,
            Variant.Type.Int => (int)value != 0,
            Variant.Type.Float => !Mathf.IsZeroApprox((float)value),
            _ => bool.TryParse(value.AsString(), out var parsed) ? parsed : fallback
        };
    }

    private void CancelAttackMode(bool restoreHelpText = true)
    {
        _awaitingPlayerAttackDirection = false;
        ClearMovementPreviewPath();
        QueueRedraw();
        if (restoreHelpText)
        {
            SetStatusHelp();
        }
    }

    private void DrawAttackPreviewOverlay(CanvasItem canvas)
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
        canvas.DrawArc(center, 28.0f, 0.0f, Mathf.Tau, 40, new Color(1.0f, 0.85f, 0.35f, 0.95f), 3.0f);

        for (var dx = -actionProfile.Range; dx <= actionProfile.Range; dx++)
        {
            for (var dy = -actionProfile.Range; dy <= actionProfile.Range; dy++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var cell = active.GridPos + new Vector2I(dx, dy);
                if (!IsInBounds(cell) || !Unit.IsWithinRange(active.GridPos, cell, actionProfile.Range))
                {
                    continue;
                }

                var cellRect = new Rect2(new Vector2(cell.X * CellSize, cell.Y * CellSize), new Vector2(CellSize, CellSize));
                var target = actionProfile.ActionType == "heal"
                    ? GetLivingAllyAtCell(active.Team, cell)
                    : GetLivingEnemyAtCell(active.Team, cell);
                var valid = target != null && active.CanUseActionAtRange(target, actionProfile.Range, _allUnits);

                var fill = valid ? new Color(0.2f, 0.9f, 0.3f, 0.25f) : new Color(0.9f, 0.25f, 0.25f, 0.12f);
                var edge = valid ? new Color(0.3f, 1.0f, 0.45f, 0.9f) : new Color(1.0f, 0.4f, 0.4f, 0.5f);
                canvas.DrawRect(cellRect, fill, true);
                canvas.DrawRect(cellRect, edge, false, 2.0f);
            }
        }

        if (!TryGetAreaPreviewCenterCell(active, actionProfile, out var areaCenter))
        {
            return;
        }

        DrawAreaPreviewOverlay(canvas, areaCenter, actionProfile.AreaRadius);
    }

    private bool TryGetAreaPreviewCenterCell(Unit active, ActionProfile actionProfile, out Vector2I centerCell)
    {
        centerCell = new Vector2I(-1, -1);

        if (actionProfile.Range <= 0 || actionProfile.AreaRadius <= 0)
        {
            return false;
        }

        var hoveredCell = WorldToCell(ToLocal(GetGlobalMousePosition()));
        if (!IsInBounds(hoveredCell) || !Unit.IsWithinRange(active.GridPos, hoveredCell, actionProfile.Range))
        {
            return false;
        }

        centerCell = hoveredCell;
        return true;
    }

    private void DrawAreaPreviewOverlay(CanvasItem canvas, Vector2I centerCell, int radius)
    {
        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                var cell = centerCell + new Vector2I(dx, dy);
                if (!IsInBounds(cell) || !Unit.IsWithinRange(centerCell, cell, radius))
                {
                    continue;
                }

                var cellRect = new Rect2(new Vector2(cell.X * CellSize, cell.Y * CellSize), new Vector2(CellSize, CellSize));
                canvas.DrawRect(cellRect, new Color(0.45f, 0.75f, 1.0f, 0.2f), true);
                canvas.DrawRect(cellRect, new Color(0.62f, 0.9f, 1.0f, 0.95f), false, 2.0f);
            }
        }

        var centerRect = new Rect2(new Vector2(centerCell.X * CellSize, centerCell.Y * CellSize), new Vector2(CellSize, CellSize));
        canvas.DrawRect(centerRect, new Color(0.85f, 0.95f, 1.0f, 0.2f), true);
        canvas.DrawRect(centerRect, new Color(0.95f, 1.0f, 1.0f, 1.0f), false, 3.0f);
    }

    private void DrawMovementPreviewOverlay(CanvasItem canvas)
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
                canvas.DrawRect(hoverRect, new Color(0.2f, 0.85f, 0.35f, 0.12f), true);
                canvas.DrawRect(hoverRect, new Color(0.3f, 1.0f, 0.45f, 0.9f), false, 2.0f);
            }
            else
            {
                canvas.DrawRect(hoverRect, new Color(0.9f, 0.2f, 0.2f, 0.12f), true);
                canvas.DrawRect(hoverRect, new Color(1.0f, 0.35f, 0.35f, 0.9f), false, 2.0f);
            }

            var label = _movementHoverReachable
                ? $"{_movementHoverCost}/{active.RemainingMovement}"
                : $"X/{active.RemainingMovement}";
            var labelColor = _movementHoverReachable
                ? new Color(0.78f, 1.0f, 0.86f, 1.0f)
                : new Color(1.0f, 0.72f, 0.72f, 1.0f);
            var labelPos = new Vector2(_movementHoverCell.X * CellSize + CellSize / 2.0f, _movementHoverCell.Y * CellSize + CellSize - 10.0f);
            canvas.DrawString(ThemeDB.FallbackFont, labelPos, label, HorizontalAlignment.Center, CellSize - 8.0f, ThemeDB.FallbackFontSize, labelColor);
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
            canvas.DrawRect(cellRect, new Color(0.22f, 0.72f, 1.0f, clampedAlpha), true);
            canvas.DrawRect(cellRect, new Color(0.35f, 0.85f, 1.0f, 0.92f), false, 2.0f);
        }

        var startCenter = CellCenter(active.GridPos);
        foreach (var cell in _movementPreviewPath)
        {
            var nextCenter = CellCenter(cell);
            canvas.DrawLine(startCenter, nextCenter, new Color(0.45f, 0.9f, 1.0f, 0.9f), 2.0f);
            startCenter = nextCenter;
        }
    }

    private void DrawMapInteractablesOverlay(CanvasItem canvas)
    {
        _mapLoader?.DrawMapInteractablesOverlay(canvas, _mapProps, _lootBags, _openedPropIds, CellSize);
    }

    private void DrawFocusedUnitCellHighlight(CanvasItem canvas)
    {
        Unit highlightedUnit = null;

        if (_flowState == BattleFlowState.Combat)
        {
            highlightedUnit = _turnManager?.GetActiveUnit();
        }
        else if (_flowState == BattleFlowState.Exploration)
        {
            highlightedUnit = GetSelectedCharacterPartyUnit() ?? GetExplorerUnit();
        }

        if (!IsUsableUnit(highlightedUnit) || highlightedUnit.IsDead || !IsInBounds(highlightedUnit.GridPos))
        {
            return;
        }

        // Use current world position so the highlight stays in sync with tweened movement.
        var topLeft = highlightedUnit.Position - new Vector2(CellSize * 0.5f, CellSize * 0.5f);

        var rect = new Rect2(
            topLeft,
            new Vector2(CellSize, CellSize)
        );

        var isEnemyTurn = _flowState == BattleFlowState.Combat && highlightedUnit.Team == "enemy";
        var fillColor = isEnemyTurn
            ? new Color(0.95f, 0.16f, 0.14f, 0.22f)
            : new Color(0.2f, 0.9f, 0.3f, 0.2f);
        var borderColor = isEnemyTurn
            ? new Color(1.0f, 0.24f, 0.2f, 0.95f)
            : new Color(0.35f, 1.0f, 0.45f, 0.9f);

        canvas.DrawRect(rect, fillColor, true);
        canvas.DrawRect(rect, borderColor, false, 3.0f);
    }

    private void DrawHoveredUnitTooltip()
    {
        var cell = WorldToCell(ToLocal(GetGlobalMousePosition()));
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
            $"HP: {unit.HitPoints}/{unit.MaxHitPoints}\nMP: {unit.MagicPoints}/{unit.MaxMagicPoints}\nArmor Class: {unit.ArmorClass} | Atk: {unit.AttackDamage} | Range: {unit.AttackRange}",
            new Color(0.05f, 0.05f, 0.08f, 0.86f),
            new Color(0.82f, 0.86f, 0.94f, 0.95f),
            titleColor,
            new Color(0.95f, 0.98f, 1.0f, 1.0f)
        );
    }

    private void DrawHoveredInteractableTooltip()
    {
        var cell = WorldToCell(ToLocal(GetGlobalMousePosition()));
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
            var propType = GetString(prop, "type", "prop");
            var interactionText = GetString(prop, "interaction_text", "");
            var hasLoot = TryGetStringArray(prop, "loot_item_ids").Count > 0
                || !string.IsNullOrEmpty(GetString(prop, "loot_item_id", ""))
                || GetInt(prop, "gold_amount", 0) > 0;
            title = propName;
            if (hasLoot)
            {
                details = _openedPropIds.Contains(propId)
                    ? $"{propType} (loot)\nEmpty"
                    : $"{propType} (loot)\nClosed";
            }
            else
            {
                details = string.IsNullOrEmpty(interactionText)
                    ? $"{propType}\nInteract"
                    : $"{propType}\n{interactionText}";
            }
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

                var itemIds = _mapLoader?.GetBagItemIds(bag) ?? new Array<string>();
                title = itemIds.Count > 0 ? "Loot Bag" : "Loot Bag (Empty)";
                details = itemIds.Count > 0
                    ? $"Pickup container\nContains: {_mapLoader?.JoinItemNames(itemIds, _gameData) ?? "nothing"}"
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

    private bool TryOpenExplorationInteractionAtCell(Vector2I clickedCell)
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

        if (_mapLoader == null || !_mapLoader.TryBuildExplorationClickLootEntries(explorer, clickedCell, _mapProps, _lootBags, _openedPropIds, _gameData, out var entries, out var statusText))
        {
            return false;
        }

        AppendReserveEntriesForRestCell(clickedCell, entries);

        if (!string.IsNullOrEmpty(statusText))
        {
        }

        if (entries.Count > 0)
        {
            var firstInteractionId = GetString(entries[0], "id", "");
            if (entries.Count == 1 && !string.IsNullOrEmpty(firstInteractionId))
            {
                if (firstInteractionId.StartsWith("vendor:"))
                {
                    OpenVendor(firstInteractionId.Substring(7));
                    return true;
                }

                if (firstInteractionId.StartsWith("rest:"))
                {
                    _hasActiveLootCell = false;
                    _activeLootCell = new Vector2I(-1, -1);
                    _hud?.SetLootPanelVisible(false);
                    BeginRestPointInteraction(firstInteractionId.Substring(5));
                    return true;
                }

                if (TryExecuteExplorationInteractionById(explorer, firstInteractionId))
                {
                    _mapLoader.TryBuildExplorationClickLootEntries(explorer, clickedCell, _mapProps, _lootBags, _openedPropIds, _gameData, out entries, out _);
                    AppendReserveEntriesForRestCell(clickedCell, entries);
                }
            }

            if (entries.Count == 0)
            {
                _hasActiveLootCell = false;
                _activeLootCell = new Vector2I(-1, -1);
                _hud?.SetLootPanelVisible(false);
                return true;
            }

            _hasActiveLootCell = true;
            _activeLootCell = clickedCell;
            _hud?.SetLootEntries(entries);
            _hud?.PositionLootPanelAboveCell(clickedCell, CellSize);
            _hud?.SetLootPanelVisible(true);
        }

        return true;
    }

    private async void BeginRestPointInteraction(string propId)
    {
        if (_flowState != BattleFlowState.Exploration)
        {
            return;
        }

        var restPointName = GetRestPointNameById(propId);
        var shouldRest = await ConfirmRestPointAsync(restPointName);
        if (!shouldRest || _flowState != BattleFlowState.Exploration)
        {
            return;
        }

        var restoredCount = 0;
        foreach (var unit in _playerUnits)
        {
            if (!IsUsableUnit(unit) || unit.IsDead)
            {
                continue;
            }

            unit.RestoreFromRest();
            restoredCount++;
        }

        if (restoredCount <= 0)
        {
            return;
        }

        _hud?.ShowCombatBanner("The weary party rests...", new Color(0.64f, 0.88f, 0.78f, 1.0f));
        _hud?.AddCombatLogEntry("The weary party rests and restores health and magic.");
        SyncHudFromGameState();
        _persistence.PersistSaveGame(false);
        QueueRedraw();
    }

    private string GetRestPointNameById(string propId)
    {
        if (string.IsNullOrEmpty(propId))
        {
            return "Rest Point";
        }

        foreach (var prop in _mapProps)
        {
            if (GetString(prop, "id", "") != propId)
            {
                continue;
            }

            return GetString(prop, "name", "Rest Point");
        }

        return "Rest Point";
    }

    private void OpenVendor(string vendorId)
    {
        if (string.IsNullOrEmpty(vendorId))
        {
            return;
        }

        _activeVendorId = vendorId;
        EnsureDefaultVendorState();
        _hud?.SetLootPanelVisible(false);
        _hud?.OpenVendorPanel(GetVendorDisplayName(vendorId));
        RefreshVendorHud(vendorId, "");
    }

    private void OnHudVendorBuyRequested(string itemId)
    {
        var message = TryBuyVendorItem(_activeVendorId, itemId);
        RefreshVendorHud(_activeVendorId, message);
        SyncHudFromGameState();
        _persistence.PersistSaveGame(false);
    }

    private void OnHudVendorSellRequested(string itemId)
    {
        var message = TrySellVendorItem(_activeVendorId, itemId);
        RefreshVendorHud(_activeVendorId, message);
        SyncHudFromGameState();
        _persistence.PersistSaveGame(false);
    }

    private string TryBuyVendorItem(string vendorId, string itemId)
    {
        EnsureDefaultVendorState();
        if (string.IsNullOrEmpty(itemId) || !_vendorInventoryItemIdsById.TryGetValue(vendorId, out var vendorInventory) || !vendorInventory.Contains(itemId))
        {
            return $"{GetVendorDisplayName(vendorId)} does not have that item in stock.";
        }

        var price = GetItemBuyPrice(vendorId, itemId);
        if (_partyGold < price)
        {
            return $"Not enough gold. {GetItemName(itemId)} costs {price} gp.";
        }

        _partyGold -= price;
        _vendorGoldById[vendorId] = GetVendorGold(vendorId) + price;
        vendorInventory.Remove(itemId);
        _partyInventoryItemIds.Add(itemId);
        _hud?.AddCombatLogEntry($"Bought {GetItemName(itemId)} for {price} gp.");
        return $"Bought {GetItemName(itemId)} for {price} gp.";
    }

    private string TrySellVendorItem(string vendorId, string itemId)
    {
        EnsureDefaultVendorState();
        if (string.IsNullOrEmpty(itemId) || !HasUnequippedSharedItem(itemId))
        {
            return "That item is not available to sell.";
        }

        var price = GetItemSellPrice(vendorId, itemId);
        var vendorGold = GetVendorGold(vendorId);
        if (vendorGold < price)
        {
            return $"{GetVendorDisplayName(vendorId)} only has {vendorGold} gp.";
        }

        _partyInventoryItemIds.Remove(itemId);
        _partyGold += price;
        _vendorGoldById[vendorId] = vendorGold - price;
        GetVendorInventory(vendorId).Add(itemId);
        _hud?.AddCombatLogEntry($"Sold {GetItemName(itemId)} for {price} gp.");
        return $"Sold {GetItemName(itemId)} for {price} gp.";
    }

    private void RefreshVendorHud(string vendorId, string message)
    {
        _hud?.SetVendorItems(BuildVendorBuyItemsForHud(vendorId), BuildVendorSellItemsForHud(vendorId));
        _hud?.SetVendorStatus($"Party: {_partyGold} gp | {GetVendorDisplayName(vendorId)}: {GetVendorGold(vendorId)} gp");
        if (!string.IsNullOrEmpty(message))
        {
            _hud?.SetVendorTransactionMessage(message);
        }
    }

    private Array<Dictionary> BuildVendorBuyItemsForHud(string vendorId)
    {
        var result = new Array<Dictionary>();
        var counts = CountItems(GetVendorInventory(vendorId));
        foreach (var entry in counts)
        {
            var itemData = CopyDictionary(_gameData?.GetItem(entry.Key) ?? new Dictionary());
            if (itemData.Count == 0)
            {
                continue;
            }

            itemData["quantity"] = entry.Value;
            itemData["price"] = GetItemBuyPrice(vendorId, entry.Key);
            result.Add(itemData);
        }

        return result;
    }

    private Array<Dictionary> BuildVendorSellItemsForHud(string vendorId)
    {
        var result = new Array<Dictionary>();
        var sellable = new List<string>();
        foreach (var item in BuildInventoryItemsForHud())
        {
            var itemId = GetString(item, "id", "");
            if (!string.IsNullOrEmpty(itemId))
            {
                sellable.Add(itemId);
            }
        }

        var counts = CountItems(sellable);
        foreach (var entry in counts)
        {
            var itemData = CopyDictionary(_gameData?.GetItem(entry.Key) ?? new Dictionary());
            if (itemData.Count == 0)
            {
                continue;
            }

            itemData["quantity"] = entry.Value;
            itemData["price"] = GetItemSellPrice(vendorId, entry.Key);
            result.Add(itemData);
        }

        return result;
    }

    private void EnsureDefaultVendorState()
    {
        if (_gameData?.Vendors != null)
        {
            foreach (var vendorKey in _gameData.Vendors.Keys)
            {
                var vendorId = ((Variant)vendorKey).AsString();
                if (!string.IsNullOrWhiteSpace(vendorId))
                {
                    EnsureVendorState(vendorId);
                }
            }
        }

        EnsureVendorState("milo");
        EnsureVendorState("mira");
    }

    private void EnsureVendorState(string vendorId)
    {
        if (string.IsNullOrEmpty(vendorId))
        {
            return;
        }

        var profile = _gameData?.GetVendorProfile(vendorId) ?? new Dictionary();

        if (!_vendorGoldById.ContainsKey(vendorId))
        {
            _vendorGoldById[vendorId] = Mathf.Max(0, GetInt(profile, "starting_gold", 100));
        }

        if (!_vendorInventoryItemIdsById.ContainsKey(vendorId))
        {
            var configuredInventory = TryGetStringArray(profile, "starting_inventory_item_ids");
            var inventory = new List<string>();
            foreach (var itemId in configuredInventory)
            {
                if (!string.IsNullOrWhiteSpace(itemId))
                {
                    inventory.Add(itemId);
                }
            }

            if (inventory.Count == 0)
            {
                inventory.Add("leather-armor");
                inventory.Add("leather-armor");
                inventory.Add("small-shield");
                inventory.Add("short-sword");
            }

            _vendorInventoryItemIdsById[vendorId] = inventory;
        }
    }

    private List<string> GetVendorInventory(string vendorId)
    {
        EnsureDefaultVendorState();
        if (!_vendorInventoryItemIdsById.TryGetValue(vendorId, out var inventory))
        {
            inventory = new List<string>();
            _vendorInventoryItemIdsById[vendorId] = inventory;
        }

        return inventory;
    }

    private int GetVendorGold(string vendorId)
    {
        EnsureDefaultVendorState();
        return _vendorGoldById.TryGetValue(vendorId, out var gold) ? Mathf.Max(0, gold) : 0;
    }

    private string GetVendorDisplayName(string vendorId)
    {
        if (string.IsNullOrEmpty(vendorId))
        {
            return "Vendor";
        }

        var profile = _gameData?.GetVendorProfile(vendorId) ?? new Dictionary();
        var configuredName = GetString(profile, "display_name", "");
        if (!string.IsNullOrWhiteSpace(configuredName))
        {
            return configuredName;
        }

        if (string.Equals(vendorId, "milo", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Milo the Vendor";
        }

        if (string.Equals(vendorId, "mira", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Mira the Vendor";
        }

        var normalized = vendorId.Replace('-', ' ').Replace('_', ' ').Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return "Vendor";
        }

        return char.ToUpperInvariant(normalized[0]) + normalized.Substring(1) + " the Vendor";
    }

    private static System.Collections.Generic.Dictionary<string, int> CountItems(IEnumerable<string> itemIds)
    {
        var counts = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var itemId in itemIds)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                continue;
            }

            counts[itemId] = counts.TryGetValue(itemId, out var count) ? count + 1 : 1;
        }

        return counts;
    }

    private float GetVendorBuyMultiplier(string vendorId)
    {
        var profile = _gameData?.GetVendorProfile(vendorId) ?? new Dictionary();
        return Mathf.Max(0.1f, GetFloat(profile, "buy_price_multiplier", 1.0f));
    }

    private float GetVendorSellMultiplier(string vendorId)
    {
        var profile = _gameData?.GetVendorProfile(vendorId) ?? new Dictionary();
        return Mathf.Max(0.1f, GetFloat(profile, "sell_price_multiplier", 1.0f));
    }

    private int GetItemBuyPrice(string vendorId, string itemId)
    {
        var basePrice = GetBaseItemBuyPrice(itemId);
        var multiplier = GetVendorBuyMultiplier(vendorId);
        return Mathf.Max(1, Mathf.RoundToInt(basePrice * multiplier));
    }

    private int GetItemSellPrice(string vendorId, string itemId)
    {
        var baseSellPrice = Mathf.Max(1, GetBaseItemBuyPrice(itemId) / 2);
        var multiplier = GetVendorSellMultiplier(vendorId);
        return Mathf.Max(1, Mathf.RoundToInt(baseSellPrice * multiplier));
    }

    private int GetBaseItemBuyPrice(string itemId)
    {
        return itemId switch
        {
            "cloth-robe" => 5,
            "small-shield" => 10,
            "iron-helmet" => 10,
            "leather-armor" => 15,
            "short-bow" => 18,
            "short-sword" => 20,
            "healers-circlet" => 25,
            "mages-amulet" => 25,
            "chain-mail" => 30,
            "fireball-scroll" => 30,
            "long-bow" => 35,
            "war-axe" => 40,
            "chieftain-club" => 45,
            _ => 10
        };
    }

    private string GetItemName(string itemId)
    {
        var item = _gameData?.GetItem(itemId) ?? new Dictionary();
        return GetString(item, "name", itemId);
    }

    private bool HasUnequippedSharedItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        var sharedCount = 0;
        foreach (var sharedItemId in _partyInventoryItemIds)
        {
            if (sharedItemId == itemId)
            {
                sharedCount++;
            }
        }

        var equippedCount = 0;
        foreach (var equippedBySlot in _equippedItemsByUnitId.Values)
        {
            foreach (var equippedItemId in equippedBySlot.Values)
            {
                if (equippedItemId == itemId)
                {
                    equippedCount++;
                }
            }
        }

        return sharedCount > equippedCount;
    }

    private Array<Dictionary> BuildNearbyLootEntries(Unit explorer)
    {
        if (_mapLoader == null)
        {
            return new Array<Dictionary>();
        }

        return _mapLoader.BuildNearbyLootEntries(explorer, _mapProps, _lootBags, _openedPropIds, _gameData);
    }

    private bool TryExecuteExplorationInteractionById(Unit explorer, string interactionId)
    {
        if (TryHandleReserveInteractionById(explorer, interactionId))
        {
            return true;
        }

        if (TryHandleNpcInteractionById(explorer, interactionId))
        {
            return true;
        }

        if (_mapLoader == null)
        {
            return false;
        }

        if (!_mapLoader.TryResolveExplorationInteractionById(explorer, interactionId, _mapProps, _lootBags, _openedPropIds, _lootedBagIds, _partyInventoryItemIds, ref _partyGold, _gameData, _lootRng, out var statusText, out var logText, out var changedState))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(statusText))
        {
        }

        if (!string.IsNullOrEmpty(logText))
        {
            _hud?.AddCombatLogEntry(logText);
        }

        if (changedState)
        {
            SyncHudFromGameState();
            SaveMapInteractionStateForCurrentMap();
            _persistence.PersistSaveGame(false);
            QueueRedraw();
        }

        return true;
    }

    private bool TryHandleReserveInteractionById(Unit explorer, string interactionId)
    {
        if (string.IsNullOrWhiteSpace(interactionId) || explorer == null)
        {
            return false;
        }

        if (interactionId.StartsWith("reserve-recruit:"))
        {
            BeginReserveRecruitInteraction(explorer, interactionId.Substring(16));
            return true;
        }

        return false;
    }

    private bool TryHandleNpcInteractionById(Unit explorer, string interactionId)
    {
        if (string.IsNullOrEmpty(interactionId) || explorer == null)
        {
            return false;
        }

        if (interactionId.StartsWith("npc-dialogue:"))
        {
            BeginNpcDialogueInteraction(interactionId.Substring(13));
            return true;
        }

        if (interactionId.StartsWith("npc-recruit:"))
        {
            BeginNpcRecruitInteraction(explorer, interactionId.Substring(12));
            return true;
        }

        return false;
    }

    private async void BeginNpcDialogueInteraction(string npcId)
    {
        if (_flowState != BattleFlowState.Exploration || string.IsNullOrWhiteSpace(npcId))
        {
            return;
        }

        if (!TryGetNpcPropById(npcId, out var npcProp))
        {
            return;
        }

        var speakerName = GetString(npcProp, "name", "Traveler");
        var fallbackLine = GetString(npcProp, "interaction_text", "...");
        var dialogueId = GetString(npcProp, "dialogue_id", "");

        if (string.IsNullOrWhiteSpace(dialogueId))
        {
            await ShowSimpleDialoguePageAsync(speakerName, fallbackLine, false);
            return;
        }

        var dialogue = _gameData?.GetDialogue(dialogueId) ?? new Dictionary();
        var pages = TryGetDictionaryArray(dialogue, "pages");
        if (pages.Count == 0)
        {
            var fallbackText = GetString(dialogue, "text", fallbackLine);
            await ShowSimpleDialoguePageAsync(speakerName, fallbackText, false);
            return;
        }

        var title = GetString(dialogue, "title", speakerName);
        var pageIndex = 0;
        while (pageIndex >= 0 && pageIndex < pages.Count)
        {
            var page = pages[pageIndex];
            var pageText = GetString(page, "text", fallbackLine);
            var choices = TryGetDictionaryArray(page, "choices");

            if (choices.Count == 0)
            {
                var hasNext = pageIndex < pages.Count - 1;
                var shouldAdvance = await ShowSimpleDialoguePageAsync(title, pageText, hasNext);
                if (!shouldAdvance)
                {
                    break;
                }

                pageIndex++;
                continue;
            }

            var selectedChoiceIndex = await ShowDialogueChoicesAsync(title, pageText, choices);
            if (selectedChoiceIndex < 0 || selectedChoiceIndex >= choices.Count)
            {
                break;
            }

            var selectedChoice = choices[selectedChoiceIndex];
            if (GetBool(selectedChoice, "end", false))
            {
                break;
            }

            var nextPage = GetInt(selectedChoice, "next_page", pageIndex + 1);
            if (nextPage == pageIndex)
            {
                nextPage++;
            }

            pageIndex = Mathf.Clamp(nextPage, 0, pages.Count);
        }
    }

    private async void BeginNpcRecruitInteraction(Unit explorer, string npcId)
    {
        if (_flowState != BattleFlowState.Exploration || explorer == null || string.IsNullOrWhiteSpace(npcId))
        {
            return;
        }

        if (!TryGetNpcPropById(npcId, out var npcProp))
        {
            return;
        }

        var recruitTemplateId = GetString(npcProp, "recruit_template_id", "");
        if (string.IsNullOrWhiteSpace(recruitTemplateId))
        {
            _hud?.AddCombatLogEntry("This NPC is not recruitable.");
            return;
        }

        var recruitOnce = GetBool(npcProp, "recruit_once", true);
        var recruitUnitId = BuildNpcRecruitUnitId(npcId, recruitTemplateId);
        var alreadyInParty = false;
        foreach (var unit in _playerUnits)
        {
            if (IsUsableUnit(unit) && unit.UnitId == recruitUnitId)
            {
                alreadyInParty = true;
                break;
            }
        }

        if (alreadyInParty)
        {
            _hud?.AddCombatLogEntry($"{GetString(npcProp, "name", "This ally")} is already in your party.");
            return;
        }

        if (recruitOnce && _recruitedNpcIds.Contains(npcId))
        {
            _hud?.AddCombatLogEntry($"{GetString(npcProp, "name", "This ally")} has already joined before.");
            return;
        }

        Unit replacedUnit = null;
        if (_playerUnits.Count >= MaxPartyMembers)
        {
            var replacementIndex = await ShowPartyReplacementChoiceAsync($"Your party is full ({MaxPartyMembers}). Choose someone to send to reserves.");
            if (replacementIndex < 0 || replacementIndex >= _playerUnits.Count)
            {
                _hud?.AddCombatLogEntry("Recruit cancelled.");
                return;
            }

            replacedUnit = _playerUnits[replacementIndex];
            AddOrUpdateReserveUnit(replacedUnit);
            RemovePartyUnit(replacedUnit, movedToReserve: true);
        }

        var template = CopyDictionary(_gameData?.GetCharacterTemplate(recruitTemplateId) ?? new Dictionary());
        if (template.Count == 0)
        {
            _hud?.AddCombatLogEntry($"Recruit template '{recruitTemplateId}' was not found.");
            return;
        }

        var spawnNear = GetVector2I(npcProp, "grid_pos", explorer.GridPos);
        if (!TryFindRecruitSpawnCell(spawnNear, out var recruitCell))
        {
            recruitCell = explorer.GridPos;
        }

        template["id"] = recruitUnitId;
        template["team"] = "player";
        template["grid_pos"] = recruitCell;
        template["name"] = GetString(npcProp, "name", GetString(template, "name", "Ally"));

        SpawnUnit(template);
        _recruitedNpcIds.Add(npcId);

        var recruitedName = GetString(template, "name", "Ally");
        if (replacedUnit != null)
        {
            var removedName = replacedUnit.UnitName;
            _hud?.AddCombatLogEntry($"{recruitedName} joined. {removedName} was moved to reserves.");
        }
        else
        {
            _hud?.AddCombatLogEntry($"{recruitedName} joined the party.");
        }

        SyncHudFromGameState();
        SaveMapInteractionStateForCurrentMap();
        _persistence.PersistSaveGame(false);
        QueueRedraw();
    }

    private async void BeginReserveRecruitInteraction(Unit explorer, string unitId, string preferredReplaceUnitId = "")
    {
        if (_flowState != BattleFlowState.Exploration || explorer == null || string.IsNullOrWhiteSpace(unitId))
        {
            return;
        }

        if (!TryTakeReserveUnit(unitId, out var reserveConfig))
        {
            _hud?.AddCombatLogEntry("That reserve member is no longer available.");
            return;
        }

        Unit replacedUnit = null;
        if (_playerUnits.Count >= MaxPartyMembers)
        {
            var replacementIndex = -1;
            if (!string.IsNullOrWhiteSpace(preferredReplaceUnitId))
            {
                for (var i = 0; i < _playerUnits.Count; i++)
                {
                    if (_playerUnits[i]?.UnitId == preferredReplaceUnitId)
                    {
                        replacementIndex = i;
                        break;
                    }
                }
            }

            if (replacementIndex < 0)
            {
                replacementIndex = await ShowPartyReplacementChoiceAsync($"Your party is full ({MaxPartyMembers}). Choose someone to send to reserves.");
            }

            if (replacementIndex < 0 || replacementIndex >= _playerUnits.Count)
            {
                AddOrUpdateReserveUnit(reserveConfig);
                _hud?.AddCombatLogEntry("Reserve recruitment cancelled.");
                return;
            }

            replacedUnit = _playerUnits[replacementIndex];
            AddOrUpdateReserveUnit(replacedUnit);
            RemovePartyUnit(replacedUnit, movedToReserve: true);
        }

        if (!TryFindRecruitSpawnCell(explorer.GridPos, out var recruitCell))
        {
            recruitCell = explorer.GridPos;
        }

        reserveConfig["team"] = "player";
        reserveConfig["grid_pos"] = recruitCell;
        SpawnUnit(CopyDictionary(reserveConfig));

        var recruitedName = GetString(reserveConfig, "name", "Ally");
        if (replacedUnit != null)
        {
            _hud?.AddCombatLogEntry($"{recruitedName} rejoined. {replacedUnit.UnitName} moved to reserves.");
        }
        else
        {
            _hud?.AddCombatLogEntry($"{recruitedName} rejoined from reserves.");
        }

        SyncHudFromGameState();
        SaveMapInteractionStateForCurrentMap();
        _persistence.PersistSaveGame(false);
        QueueRedraw();
    }

    private bool TryGetNpcPropById(string npcId, out Dictionary npcProp)
    {
        foreach (var prop in _mapProps)
        {
            if (GetString(prop, "type", "") != "npc")
            {
                continue;
            }

            var candidateId = GetString(prop, "npc_id", GetString(prop, "id", ""));
            if (candidateId == npcId)
            {
                npcProp = prop;
                return true;
            }
        }

        npcProp = null;
        return false;
    }

    private static string BuildNpcRecruitUnitId(string npcId, string templateId)
    {
        return $"npc-recruit:{npcId}:{templateId}";
    }

    private bool TryFindRecruitSpawnCell(Vector2I preferredCell, out Vector2I spawnCell)
    {
        var candidates = new List<Vector2I>
        {
            preferredCell,
            preferredCell + new Vector2I(0, -1),
            preferredCell + new Vector2I(0, 1),
            preferredCell + new Vector2I(-1, 0),
            preferredCell + new Vector2I(1, 0),
            preferredCell + new Vector2I(-1, -1),
            preferredCell + new Vector2I(1, -1),
            preferredCell + new Vector2I(-1, 1),
            preferredCell + new Vector2I(1, 1)
        };

        foreach (var candidate in candidates)
        {
            if (!IsInBounds(candidate) || IsBlockedCell(candidate) || IsOccupied(candidate))
            {
                continue;
            }

            spawnCell = candidate;
            return true;
        }

        spawnCell = preferredCell;
        return false;
    }

    private void AppendReserveEntriesForRestCell(Vector2I clickedCell, Array<Dictionary> entries)
    {
        if (entries == null || _reservePartyRoster.Count == 0)
        {
            return;
        }

        Dictionary restPoint = null;
        foreach (var prop in _mapProps)
        {
            if (GetString(prop, "type", "") == "rest_point"
                && GetVector2I(prop, "grid_pos", new Vector2I(-9999, -9999)) == clickedCell)
            {
                restPoint = prop;
                break;
            }
        }

        if (restPoint == null)
        {
            return;
        }

        var sourceTitle = GetString(restPoint, "name", "Rest Point");
        foreach (var reserveUnit in _reservePartyRoster)
        {
            var unitId = GetString(reserveUnit, "id", "");
            if (string.IsNullOrWhiteSpace(unitId))
            {
                continue;
            }

            var unitName = GetString(reserveUnit, "name", "Companion");
            entries.Add(new Dictionary
            {
                { "id", $"reserve-recruit:{unitId}" },
                { "label", $"Invite {unitName} from reserves" },
                { "detail", $"Ask {unitName} to rejoin your active party." },
                { "source_title", sourceTitle }
            });
        }
    }

    private Dictionary BuildUnitRosterEntry(Unit unit)
    {
        var abilityIds = new Array<string>();
        foreach (var abilityId in unit.AbilityIds)
        {
            if (!string.IsNullOrWhiteSpace(abilityId))
            {
                abilityIds.Add(abilityId);
            }
        }

        return new Dictionary
        {
            { "id", unit.UnitId },
            { "name", unit.UnitName },
            { "race", unit.Race },
            { "team", "player" },
            { "grid_pos", unit.GridPos },
            { "primary_ability_id", unit.PrimaryAbilityId },
            { "ability_ids", abilityIds },
            { "initiative", unit.Initiative },
            { "hit_points", unit.HitPoints },
            { "max_hit_points", unit.MaxHitPoints },
            { "magic_points", unit.MagicPoints },
            { "max_magic_points", unit.MaxMagicPoints },
            { "magic_point_regen_per_turn", unit.MagicPointRegenPerTurn },
            { "intelligence", unit.Intelligence },
            { "strength", unit.Strength },
            { "wisdom", unit.Wisdom },
            { "dexterity", unit.Dexterity },
            { "constitution", unit.Constitution },
            { "level", unit.Level },
            { "experience", unit.Experience },
            { "base_unarmed_damage", unit.BaseUnarmedDamage },
            { "weapon_attack_damage_bonus", unit.WeaponAttackDamageBonus },
            { "weapon_attack_range_bonus", unit.WeaponAttackRangeBonus },
            { "armor_class_bonus", unit.ArmorClassBonus },
            { "movement_per_turn", unit.MovementPerTurn },
        };
    }

    private void AddOrUpdateReserveUnit(Unit unit)
    {
        if (!IsUsableUnit(unit) || string.IsNullOrWhiteSpace(unit.UnitId))
        {
            return;
        }

        AddOrUpdateReserveUnit(BuildUnitRosterEntry(unit));
    }

    private void AddOrUpdateReserveUnit(Dictionary reserveConfig)
    {
        if (reserveConfig == null || reserveConfig.Count == 0)
        {
            return;
        }

        var unitId = GetString(reserveConfig, "id", "");
        if (string.IsNullOrWhiteSpace(unitId))
        {
            return;
        }

        for (var i = 0; i < _reservePartyRoster.Count; i++)
        {
            if (GetString(_reservePartyRoster[i], "id", "") != unitId)
            {
                continue;
            }

            _reservePartyRoster[i] = CopyDictionary(reserveConfig);
            return;
        }

        _reservePartyRoster.Add(CopyDictionary(reserveConfig));
    }

    private bool TryTakeReserveUnit(string unitId, out Dictionary reserveConfig)
    {
        for (var i = 0; i < _reservePartyRoster.Count; i++)
        {
            if (GetString(_reservePartyRoster[i], "id", "") != unitId)
            {
                continue;
            }

            reserveConfig = CopyDictionary(_reservePartyRoster[i]);
            _reservePartyRoster.RemoveAt(i);
            return true;
        }

        reserveConfig = null;
        return false;
    }

    private void RemovePartyUnit(Unit unit, bool movedToReserve = false)
    {
        if (!IsUsableUnit(unit))
        {
            return;
        }

        _playerUnits.Remove(unit);
        _allUnits.Remove(unit);
        _selectedAbilityIdByUnitId.Remove(unit.UnitId);
        _equippedItemsByUnitId.Remove(unit.UnitId);

        if (!movedToReserve && unit.UnitId.StartsWith("npc-recruit:"))
        {
            var split = unit.UnitId.Split(':');
            if (split.Length >= 3)
            {
                _recruitedNpcIds.Remove(split[1]);
            }
        }

        if (_selectedCharacterUnitId == unit.UnitId)
        {
            _selectedCharacterUnitId = "";
        }

        if (_explorerUnit == unit)
        {
            _explorerUnit = null;
        }

        unit.QueueFree();
    }

    private async Task<bool> ShowSimpleDialoguePageAsync(string title, string bodyText, bool hasNext)
    {
        var dialog = new ConfirmationDialog
        {
            Title = title,
            DialogText = bodyText,
            Exclusive = true
        };

        dialog.GetOkButton().Text = hasNext ? "Next" : "Close";
        if (hasNext)
        {
            dialog.AddCancelButton("End");
        }

        AddChild(dialog);

        var completion = new TaskCompletionSource<bool>();

        void HandleConfirmed()
        {
            completion.TrySetResult(true);
        }

        void HandleCanceled()
        {
            completion.TrySetResult(false);
        }

        dialog.Confirmed += HandleConfirmed;
        dialog.Canceled += HandleCanceled;
        dialog.CloseRequested += HandleCanceled;

        try
        {
            dialog.PopupCentered();
            var confirmed = await completion.Task;
            return hasNext && confirmed;
        }
        finally
        {
            dialog.Confirmed -= HandleConfirmed;
            dialog.Canceled -= HandleCanceled;
            dialog.CloseRequested -= HandleCanceled;
            dialog.QueueFree();
        }
    }

    private async Task<int> ShowDialogueChoicesAsync(string title, string bodyText, Array<Dictionary> choices)
    {
        var dialog = new ConfirmationDialog
        {
            Title = title,
            DialogText = bodyText,
            Exclusive = true
        };

        var optionList = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        for (var i = 0; i < choices.Count; i++)
        {
            var label = GetString(choices[i], "label", $"Choice {i + 1}");
            optionList.AddItem(label, i);
        }

        dialog.AddChild(optionList);
        dialog.GetOkButton().Text = "Choose";
        dialog.AddCancelButton("End");
        AddChild(dialog);

        var completion = new TaskCompletionSource<int>();

        void HandleConfirmed()
        {
            completion.TrySetResult((int)optionList.GetSelectedId());
        }

        void HandleCanceled()
        {
            completion.TrySetResult(-1);
        }

        dialog.Confirmed += HandleConfirmed;
        dialog.Canceled += HandleCanceled;
        dialog.CloseRequested += HandleCanceled;

        try
        {
            dialog.PopupCentered(new Vector2I(560, 240));
            return await completion.Task;
        }
        finally
        {
            dialog.Confirmed -= HandleConfirmed;
            dialog.Canceled -= HandleCanceled;
            dialog.CloseRequested -= HandleCanceled;
            dialog.QueueFree();
        }
    }

    private async Task<int> ShowPartyReplacementChoiceAsync(string prompt)
    {
        var choices = new Array<Dictionary>();
        for (var i = 0; i < _playerUnits.Count; i++)
        {
            var unit = _playerUnits[i];
            if (!IsUsableUnit(unit))
            {
                continue;
            }

            choices.Add(new Dictionary
            {
                { "label", $"{unit.UnitName} (HP {unit.HitPoints}/{unit.MaxHitPoints})" },
                { "index", i }
            });
        }

        if (choices.Count == 0)
        {
            return -1;
        }

        var selected = await ShowDialogueChoicesAsync("Party Full", prompt, choices);
        if (selected < 0 || selected >= choices.Count)
        {
            return -1;
        }

        return GetInt(choices[selected], "index", -1);
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

        if (actionProfile.ActionType == "sleep")
        {
            var furthestInBounds = targetCell;
            var foundAny = false;
            for (var distance = 1; distance <= actionProfile.Range; distance++)
            {
                var cell = active.GridPos + direction * distance;
                if (!IsInBounds(cell))
                {
                    break;
                }

                furthestInBounds = cell;
                foundAny = true;
            }

            if (!foundAny)
            {
                return false;
            }

            targetCell = furthestInBounds;
            return true;
        }

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
            if (target != null && active.CanUseActionAtRange(target, actionProfile.Range, _allUnits))
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

    private bool IsPointInsideVisibleGrid(Vector2 globalPoint)
    {
        var worldBounds = GetWorldPixelBounds();
        var gridRect = new Rect2(GlobalPosition + worldBounds.Position, worldBounds.Size);
        return gridRect.HasPoint(globalPoint);
    }

    private void SetViewPositionClamped(Vector2 targetPosition)
    {
        Position = GetClampedViewPosition(targetPosition);
    }

    private Vector2 GetClampedViewPosition(Vector2 targetPosition)
    {
        var viewportSize = GetViewportRect().Size;
        var worldBounds = GetWorldPixelBounds();
        var worldLeft = worldBounds.Position.X;
        var worldTop = worldBounds.Position.Y;
        var worldRight = worldLeft + worldBounds.Size.X;
        var worldBottom = worldTop + worldBounds.Size.Y;

        var minX = viewportSize.X - worldRight;
        var maxX = -worldLeft;
        if (minX > maxX)
        {
            var centeredX = (minX + maxX) * 0.5f;
            minX = centeredX;
            maxX = centeredX;
        }

        var minY = viewportSize.Y - worldBottom;
        var maxY = -worldTop;
        if (minY > maxY)
        {
            var centeredY = (minY + maxY) * 0.5f;
            minY = centeredY;
            maxY = centeredY;
        }

        // Extra right-edge slack keeps the party clear of right-side HUD panels near map boundaries.
        minX -= ViewPanOverscroll + ViewRightEdgeFollowBuffer;
        maxX += ViewPanOverscroll;
        minY -= ViewPanOverscroll;
        maxY += ViewPanOverscroll;

        return new Vector2(
            Mathf.Clamp(targetPosition.X, minX, maxX),
            Mathf.Clamp(targetPosition.Y, minY, maxY)
        );
    }

    private Rect2 GetWorldPixelBounds()
    {
        if (_walkableCells.Count == 0)
        {
            return new Rect2(0.0f, 0.0f, _gridWidth * CellSize, _gridHeight * CellSize);
        }

        var hasAny = false;
        var minCellX = 0;
        var maxCellX = 0;
        var minCellY = 0;
        var maxCellY = 0;

        foreach (var cell in _walkableCells)
        {
            if (!hasAny)
            {
                minCellX = cell.X;
                maxCellX = cell.X;
                minCellY = cell.Y;
                maxCellY = cell.Y;
                hasAny = true;
                continue;
            }

            if (cell.X < minCellX)
            {
                minCellX = cell.X;
            }
            else if (cell.X > maxCellX)
            {
                maxCellX = cell.X;
            }

            if (cell.Y < minCellY)
            {
                minCellY = cell.Y;
            }
            else if (cell.Y > maxCellY)
            {
                maxCellY = cell.Y;
            }
        }

        if (!hasAny)
        {
            return new Rect2(0.0f, 0.0f, _gridWidth * CellSize, _gridHeight * CellSize);
        }

        var width = (maxCellX - minCellX + 1) * CellSize;
        var height = (maxCellY - minCellY + 1) * CellSize;
        return new Rect2(minCellX * CellSize, minCellY * CellSize, width, height);
    }

    private void ClampViewPositionToBounds()
    {
        SetViewPositionClamped(Position);
    }

    private Unit GetCurrentViewFocusUnit()
    {
        if (_flowState == BattleFlowState.Exploration)
        {
            return GetExplorerUnit();
        }

        if (_flowState == BattleFlowState.Combat)
        {
            return _turnManager?.GetActiveUnit() ?? GetActivePlayerUnit();
        }

        return null;
    }

    private void CenterViewOnCell(Vector2I cell)
    {
        SetViewPositionClamped(GetCenteredViewTargetForCell(cell));
    }

    private Vector2 GetCenteredViewTargetForCell(Vector2I cell)
    {
        var viewportSize = GetViewportRect().Size;
        return new Vector2(
            viewportSize.X * 0.5f - (cell.X * CellSize + CellSize * 0.5f),
            viewportSize.Y * 0.5f - (cell.Y * CellSize + CellSize * 0.5f)
        );
    }

    private void CenterViewOnCurrentFocus()
    {
        var focus = GetCurrentViewFocusUnit();
        if (!IsUsableUnit(focus) || focus.IsDead)
        {
            ClampViewPositionToBounds();
            return;
        }

        CenterViewOnCell(focus.GridPos);
    }

    private Array<Dictionary> BuildInventoryItemsForHud()
    {
        var items = new Array<Dictionary>();
        if (_gameData == null)
        {
            return items;
        }

        var equippedUsage = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var player in _playerUnits)
        {
            if (!IsUsableUnit(player) || string.IsNullOrEmpty(player.UnitId))
            {
                continue;
            }

            if (!_equippedItemsByUnitId.TryGetValue(player.UnitId, out var equippedBySlot))
            {
                continue;
            }

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

    private Array<Dictionary> BuildActivePartyReserveEntriesForHud()
    {
        var entries = new Array<Dictionary>();
        foreach (var unit in _playerUnits)
        {
            if (!IsUsableUnit(unit))
            {
                continue;
            }

            entries.Add(new Dictionary
            {
                { "id", unit.UnitId },
                { "label", $"{unit.UnitName} (HP {unit.HitPoints}/{unit.MaxHitPoints}, MP {unit.MagicPoints}/{unit.MaxMagicPoints})" },
                { "detail", $"Send {unit.UnitName} to reserves." }
            });
        }

        return entries;
    }

    private Array<Dictionary> BuildReserveRosterEntriesForHud()
    {
        var entries = new Array<Dictionary>();
        foreach (var unit in _reservePartyRoster)
        {
            var unitId = GetString(unit, "id", "");
            if (string.IsNullOrWhiteSpace(unitId))
            {
                continue;
            }

            var name = GetString(unit, "name", "Companion");
            var hp = GetInt(unit, "hit_points", 0);
            var maxHp = Mathf.Max(1, GetInt(unit, "max_hit_points", 1));
            var mp = GetInt(unit, "magic_points", 0);
            var maxMp = Mathf.Max(0, GetInt(unit, "max_magic_points", 0));
            entries.Add(new Dictionary
            {
                { "id", unitId },
                { "label", $"{name} (HP {hp}/{maxHp}, MP {mp}/{maxMp})" },
                { "detail", $"Bring {name} back into the active party." }
            });
        }

        return entries;
    }

    private Unit GetInventoryTargetUnit()
    {
        var selected = GetSelectedCharacterPartyUnit();
        if (selected != null)
        {
            return selected;
        }

        var fallback = _flowState == BattleFlowState.Combat
            ? GetActivePlayerUnit()
            : GetExplorerUnit();

        if (fallback != null)
        {
            _selectedCharacterUnitId = fallback.UnitId;
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

    private Unit GetSelectedCharacterPartyUnit()
    {
        if (string.IsNullOrEmpty(_selectedCharacterUnitId))
        {
            return null;
        }

        foreach (var unit in _playerUnits)
        {
            if (IsUsableUnit(unit) && !unit.IsDead && unit.UnitId == _selectedCharacterUnitId)
            {
                return unit;
            }
        }

        return null;
    }

    private Unit GetSelectedCharacterUnit()
    {
        if (string.IsNullOrEmpty(_selectedCharacterUnitId))
        {
            return null;
        }

        foreach (var unit in _allUnits)
        {
            if (IsUsableUnit(unit) && !unit.IsDead && unit.UnitId == _selectedCharacterUnitId)
            {
                return unit;
            }
        }

        return null;
    }

    private bool TrySelectCharacterAtCell(Vector2I cell)
    {
        foreach (var unit in _allUnits)
        {
            if (!IsUsableUnit(unit) || unit.IsDead || unit.GridPos != cell)
            {
                continue;
            }

            _selectedCharacterUnitId = unit.UnitId;
            SyncHudFromGameState();
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
            _selectedCharacterUnitId = "";
            return;
        }

        var currentIndex = -1;
        for (var i = 0; i < party.Count; i++)
        {
            if (party[i].UnitId == _selectedCharacterUnitId)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex < 0)
        {
            _selectedCharacterUnitId = party[0].UnitId;
            return;
        }

        var next = (currentIndex + delta) % party.Count;
        if (next < 0)
        {
            next += party.Count;
        }

        _selectedCharacterUnitId = party[next].UnitId;
    }

    private void ApplyEquippedItemBonuses(Unit unit)
    {
        if (unit == null)
        {
            return;
        }

        unit.SetWeaponBonuses(0, 0);
        unit.SetArmorBonuses(0);

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
        var hasEquippedWeapon = false;
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
                hasEquippedWeapon = true;
            }
            else if (itemType == "armor")
            {
                var base_armor_class = GetInt(itemData, "base_armor_class", 0);
                var bonus_armor_class = GetInt(itemData, "bonus_armor_class", 0);
                total_armor_class += base_armor_class + bonus_armor_class;
            }
        }

        var weaponDamageBonus = hasEquippedWeapon
            ? totalWeaponDamage
            : 0;
        var weaponRangeBonus = hasEquippedWeapon
            ? bestWeaponRange
            : 0;

        unit.SetWeaponBonuses(weaponDamageBonus, weaponRangeBonus);
        unit.SetArmorBonuses(total_armor_class);
    }

    private void ApplyStartingEquipmentFromConfig(Unit unit, Dictionary config, bool addToPartyInventory)
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

            if (addToPartyInventory)
            {
                _partyInventoryItemIds.Add(itemId);
            }
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
            if (equippedBySlot.Remove("1-handed-a", out var removedMainHand))
            {
                EnsureSharedInventoryHasUnequippedCount(removedMainHand, 1);
            }

            if (equippedBySlot.Remove("1-handed-b", out var removedOffHand))
            {
                EnsureSharedInventoryHasUnequippedCount(removedOffHand, 1);
            }

            equippedBySlot["2-handed"] = itemId;
            return;
        }

        if (slot == "1-handed")
        {
            if (equippedBySlot.Remove("2-handed", out var removedTwoHanded))
            {
                EnsureSharedInventoryHasUnequippedCount(removedTwoHanded, 1);
            }

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

            if (equippedBySlot.TryGetValue("1-handed-a", out var replacedOneHanded))
            {
                equippedBySlot["1-handed-a"] = itemId;
                EnsureSharedInventoryHasUnequippedCount(replacedOneHanded, 1);
                return;
            }

            return;
        }

        if (equippedBySlot.TryGetValue(slot, out var replacedSlottedItem))
        {
            equippedBySlot[slot] = itemId;
            EnsureSharedInventoryHasUnequippedCount(replacedSlottedItem, 1);
            return;
        }

        equippedBySlot[slot] = itemId;
    }

    private void EnsureSharedInventoryHasUnequippedCount(string itemId, int minimumUnequippedCount)
    {
        if (string.IsNullOrEmpty(itemId) || minimumUnequippedCount <= 0)
        {
            return;
        }

        var sharedCount = 0;
        foreach (var sharedItemId in _partyInventoryItemIds)
        {
            if (sharedItemId == itemId)
            {
                sharedCount++;
            }
        }

        var equippedCount = 0;
        foreach (var player in _playerUnits)
        {
            if (!IsUsableUnit(player) || string.IsNullOrEmpty(player.UnitId))
            {
                continue;
            }

            if (!_equippedItemsByUnitId.TryGetValue(player.UnitId, out var equippedBySlot))
            {
                continue;
            }

            foreach (var equippedItemId in equippedBySlot.Values)
            {
                if (equippedItemId == itemId)
                {
                    equippedCount++;
                }
            }
        }

        var unequippedCount = sharedCount - equippedCount;
        while (unequippedCount < minimumUnequippedCount)
        {
            _partyInventoryItemIds.Add(itemId);
            unequippedCount++;
        }
    }

    private Array<Unit> BuildTurnOrderForHud()
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

    private void ClearPlayerUnitsFromScene()
    {
        for (var i = _allUnits.Count - 1; i >= 0; i--)
        {
            var unit = _allUnits[i];
            if (!IsUsableUnit(unit))
            {
                _allUnits.RemoveAt(i);
                continue;
            }

            if (unit.Team != "player")
            {
                continue;
            }

            unit.QueueFree();
            _allUnits.RemoveAt(i);
        }

        _playerUnits.Clear();
    }

    private Array<Dictionary> BuildPartyRosterSnapshot()
    {
        var roster = new Array<Dictionary>();
        foreach (var unit in _playerUnits)
        {
            if (!IsUsableUnit(unit))
            {
                continue;
            }

            roster.Add(BuildUnitRosterEntry(unit));
        }

        return roster;
    }

    private void RestorePartyRosterSnapshot(Array<Dictionary> roster)
    {
        if (roster == null || roster.Count == 0)
        {
            return;
        }

        ClearPlayerUnitsFromScene();
        _selectedCharacterUnitId = "";
        _explorerUnit = null;

        foreach (var config in roster)
        {
            if (config == null || config.Count == 0)
            {
                continue;
            }

            var entry = CopyDictionary(config);
            entry["team"] = "player";
            SpawnUnit(entry);
        }

        PruneInvalidUnitReferences();
    }

    private Array<string> BuildRecruitedNpcIdSnapshot()
    {
        var ids = new Array<string>();
        foreach (var npcId in _recruitedNpcIds)
        {
            if (!string.IsNullOrWhiteSpace(npcId))
            {
                ids.Add(npcId);
            }
        }

        return ids;
    }

    private Array<Dictionary> BuildReserveRosterSnapshot()
    {
        var snapshot = new Array<Dictionary>();
        foreach (var entry in _reservePartyRoster)
        {
            snapshot.Add(CopyDictionary(entry));
        }

        return snapshot;
    }

    private void RestoreRecruitedNpcIdSnapshot(Array<string> recruitedNpcIds)
    {
        _recruitedNpcIds.Clear();
        if (recruitedNpcIds == null)
        {
            return;
        }

        foreach (var npcId in recruitedNpcIds)
        {
            if (!string.IsNullOrWhiteSpace(npcId))
            {
                _recruitedNpcIds.Add(npcId);
            }
        }
    }

    private void RestoreReserveRosterSnapshot(Array<Dictionary> reserveRoster)
    {
        _reservePartyRoster.Clear();
        if (reserveRoster == null)
        {
            return;
        }

        foreach (var entry in reserveRoster)
        {
            if (entry == null || entry.Count == 0)
            {
                continue;
            }

            var unitId = GetString(entry, "id", "");
            if (string.IsNullOrWhiteSpace(unitId))
            {
                continue;
            }

            _reservePartyRoster.Add(CopyDictionary(entry));
        }
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
        _openedDoorIds.Clear();
        _openedPropIds.Clear();
        _defeatedEnemyIds.Clear();
        _lootedBagIds.Clear();

        if (_openedDoorIdsByMap.TryGetValue(_currentMapId, out var openedDoors))
        {
            foreach (var doorId in openedDoors)
            {
                _openedDoorIds.Add(doorId);
            }
        }

        if (_openedPropIdsByMap.TryGetValue(_currentMapId, out var opened))
        {
            foreach (var propId in opened)
            {
                _openedPropIds.Add(propId);
            }
        }

        if (_defeatedEnemyIdsByMap.TryGetValue(_currentMapId, out var defeatedEnemies))
        {
            foreach (var enemyId in defeatedEnemies)
            {
                _defeatedEnemyIds.Add(enemyId);
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

        var snapshot = new HashSet<string>();
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

        var openedDoorSnapshot = new HashSet<string>();
        foreach (var doorId in _openedDoorIds)
        {
            openedDoorSnapshot.Add(doorId);
        }

        var openedSnapshot = new HashSet<string>();
        foreach (var propId in _openedPropIds)
        {
            openedSnapshot.Add(propId);
        }

        var lootedSnapshot = new HashSet<string>();
        foreach (var bagId in _lootedBagIds)
        {
            lootedSnapshot.Add(bagId);
        }

        var defeatedEnemySnapshot = new HashSet<string>();
        foreach (var enemyId in _defeatedEnemyIds)
        {
            defeatedEnemySnapshot.Add(enemyId);
        }

        var lootBagSnapshot = new Array<Dictionary>();
        foreach (var bag in _lootBags)
        {
            lootBagSnapshot.Add(CopyDictionary(bag));
        }

        _openedDoorIdsByMap[_currentMapId] = openedDoorSnapshot;
        _openedPropIdsByMap[_currentMapId] = openedSnapshot;
        _defeatedEnemyIdsByMap[_currentMapId] = defeatedEnemySnapshot;
        _lootedBagIdsByMap[_currentMapId] = lootedSnapshot;
        _lootBagsByMap[_currentMapId] = lootBagSnapshot;
    }

    private Unit FindUnitById(string unitId)
    {
        if (string.IsNullOrEmpty(unitId))
        {
            return null;
        }

        foreach (var unit in _allUnits)
        {
            if (IsUsableUnit(unit) && unit.UnitId == unitId)
            {
                return unit;
            }
        }

        return null;
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

    private bool HasLivingActiveCombatEnemies()
    {
        foreach (var enemy in _enemyUnits)
        {
            if (IsEnemyInActiveCombat(enemy))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsEnemyInActiveCombat(Unit enemy)
    {
        return IsUsableUnit(enemy) && !enemy.IsDead && enemy.Team == "enemy" && _activeCombatEnemyUnitIds.Contains(enemy.UnitId);
    }

    private void MarkClearedEncounterIdsForActiveCombat()
    {
        foreach (var encounterId in _activeCombatEncounterIds)
        {
            if (!HasLivingEnemiesInEncounter(encounterId))
            {
                _clearedEncounterIds.Add(encounterId);
            }
        }
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

        return IsEnemyInActiveCombat(candidate);
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

        return IsEnemyInActiveCombat(candidate);
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

        if (!string.IsNullOrEmpty(_selectedCharacterUnitId) && GetSelectedCharacterUnit() == null)
        {
            _selectedCharacterUnitId = "";
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

        var orderedParty = new List<Unit>();
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

        var partySet = new HashSet<Unit>(orderedParty);
        var leaderNextCell = priorPositions[leader] + delta;
        if (!CanExplorationLeaderEnterCell(leaderNextCell, partySet))
        {
            return false;
        }

        leader.SetGridPos(leaderNextCell);

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

        ResolveExplorationTraps();
        CenterViewOnCurrentFocus();
        QueueRedraw();
        return true;
    }

    private async void BeginExplorationClickMove(Vector2I targetCell)
    {
        if (!CanBeginExplorationClickMove(targetCell))
        {
            return;
        }

        _isExplorationAutoMoving = true;
        try
        {
            var leader = GetExplorerUnit();
            if (!IsUsableUnit(leader) || leader.IsDead)
            {
                return;
            }

            var party = BuildExplorationPartyOrdered(leader);
            if (party.Count == 0)
            {
                return;
            }

            var path = FindExplorationPath(leader.GridPos, targetCell, party);
            if (path.Count == 0)
            {
                return;
            }

            foreach (var step in path)
            {
                if (_flowState != BattleFlowState.Exploration)
                {
                    break;
                }

                if (!await TryMoveExplorationPartyStepAnimated(step, party))
                {
                    break;
                }

                var transitionOutcome = await TryHandleMapTransitionAsync();
                if (transitionOutcome == MapTransitionOutcome.Transitioned)
                {
                    return;
                }

                if (transitionOutcome == MapTransitionOutcome.Stayed)
                {
                    SetStatusHelp();
                    TryStartCombatFromAggro();
                    return;
                }

                SetStatusHelp();
                TryStartCombatFromAggro();
                if (_flowState != BattleFlowState.Exploration)
                {
                    return;
                }
            }
        }
        finally
        {
            _isExplorationAutoMoving = false;
        }
    }

    private bool CanBeginExplorationClickMove(Vector2I targetCell)
    {
        if (_flowState != BattleFlowState.Exploration || _isExplorationAutoMoving)
        {
            return false;
        }

        if (!IsInBounds(targetCell))
        {
            return false;
        }

        var leader = GetExplorerUnit();
        if (!IsUsableUnit(leader) || leader.IsDead || leader.GridPos == targetCell)
        {
            return false;
        }

        return true;
    }

    private List<Unit> BuildExplorationPartyOrdered(Unit leader)
    {
        var party = new List<Unit>();
        if (!IsUsableUnit(leader) || leader.IsDead)
        {
            return party;
        }

        party.Add(leader);
        foreach (var player in _playerUnits)
        {
            if (!IsUsableUnit(player) || player.IsDead || player == leader)
            {
                continue;
            }

            party.Add(player);
        }

        return party;
    }

    private Array<Vector2I> FindExplorationPath(Vector2I start, Vector2I goal, List<Unit> party)
    {
        var path = new Array<Vector2I>();
        if (start == goal || party == null || party.Count == 0)
        {
            return path;
        }

        var partySet = new HashSet<Unit>(party);
        if (!CanExplorationLeaderEnterCell(goal, partySet))
        {
            return path;
        }

        var frontier = new Queue<Vector2I>();
        var cameFrom = new System.Collections.Generic.Dictionary<Vector2I, Vector2I>();
        var visited = new HashSet<Vector2I>();

        frontier.Enqueue(start);
        visited.Add(start);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current == goal)
            {
                break;
            }

            foreach (var dir in AttackDirections)
            {
                var next = current + dir;
                if (visited.Contains(next) || !CanExplorationLeaderEnterCell(next, partySet))
                {
                    continue;
                }

                visited.Add(next);
                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (!visited.Contains(goal))
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

    private async System.Threading.Tasks.Task<bool> TryMoveExplorationPartyStepAnimated(Vector2I leaderNextCell, List<Unit> orderedParty)
    {
        if (_flowState != BattleFlowState.Exploration || orderedParty == null || orderedParty.Count == 0)
        {
            return false;
        }

        var partySet = new HashSet<Unit>(orderedParty);
        var priorPositions = new System.Collections.Generic.Dictionary<Unit, Vector2I>();
        foreach (var member in orderedParty)
        {
            if (!IsUsableUnit(member) || member.IsDead)
            {
                continue;
            }

            priorPositions[member] = member.GridPos;
        }

        var leader = orderedParty[0];
        if (!priorPositions.ContainsKey(leader) || !CanExplorationLeaderEnterCell(leaderNextCell, partySet))
        {
            return false;
        }

        var visualFrom = new System.Collections.Generic.Dictionary<Unit, Vector2>();
        var visualTo = new System.Collections.Generic.Dictionary<Unit, Vector2>();

        visualFrom[leader] = leader.Position;
        leader.SetGridPos(leaderNextCell);
        visualTo[leader] = leader.Position;
        leader.Position = visualFrom[leader];

        for (var i = 1; i < orderedParty.Count; i++)
        {
            var follower = orderedParty[i];
            if (!priorPositions.ContainsKey(follower))
            {
                continue;
            }

            var nextCell = priorPositions[orderedParty[i - 1]];
            if (!CanExplorationFollowerEnterCell(nextCell, partySet))
            {
                continue;
            }

            visualFrom[follower] = follower.Position;
            follower.SetGridPos(nextCell);
            visualTo[follower] = follower.Position;
            follower.Position = visualFrom[follower];
        }

        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.Out);
        foreach (var pair in visualTo)
        {
            tween.Parallel().TweenProperty(pair.Key, "position", pair.Value, ExplorationStepSeconds);
        }

        var cameraTarget = GetClampedViewPosition(GetCenteredViewTargetForCell(leaderNextCell));
        tween.Parallel().TweenProperty(this, "position", cameraTarget, ExplorationStepSeconds);

        await ToSignal(tween, Tween.SignalName.Finished);
        ResolveExplorationTraps();
        QueueRedraw();
        return true;
    }

    private void ResolveExplorationTraps()
    {
        if (_flowState != BattleFlowState.Exploration)
        {
            return;
        }

        var triggeredAny = false;
        foreach (var prop in _mapProps)
        {
            if (GetString(prop, "type", "") != "trap")
            {
                continue;
            }

            var trapId = GetString(prop, "id", "");
            if (string.IsNullOrEmpty(trapId) || _openedPropIds.Contains(trapId))
            {
                continue;
            }

            var trapCell = GetVector2I(prop, "grid_pos", new Vector2I(-9999, -9999));
            Unit triggeringUnit = null;
            foreach (var unit in _playerUnits)
            {
                if (IsUsableUnit(unit) && !unit.IsDead && unit.GridPos == trapCell)
                {
                    triggeringUnit = unit;
                    break;
                }
            }

            if (triggeringUnit == null)
            {
                continue;
            }

            triggeredAny |= TryTriggerTrap(prop, triggeringUnit);
        }

        if (!triggeredAny)
        {
            return;
        }

        var livingLeader = GetExplorerUnit();
        if (!IsUsableUnit(livingLeader) || livingLeader.IsDead)
        {
            livingLeader = null;
            foreach (var unit in _playerUnits)
            {
                if (IsUsableUnit(unit) && !unit.IsDead)
                {
                    livingLeader = unit;
                    break;
                }
            }
            _explorerUnit = livingLeader;
        }

        if (livingLeader == null)
        {
            _flowState = BattleFlowState.Defeat;
            ClearCombatOnlyDebuffsForParty();
            _eventBus?.EmitSignal(EventBus.SignalName.CombatEnded);
        }

        SaveMapInteractionStateForCurrentMap();
        SyncHudFromGameState();
        _persistence.PersistSaveGame(false);
        QueueRedraw();
    }

    private bool ResolveCombatTraps(Unit triggeringUnit, out bool combatEnded)
    {
        combatEnded = false;
        if (_flowState != BattleFlowState.Combat || !IsUsableUnit(triggeringUnit) || triggeringUnit.IsDead)
        {
            return false;
        }

        var triggeredAny = false;
        foreach (var prop in _mapProps)
        {
            if (GetString(prop, "type", "") != "trap"
                || GetVector2I(prop, "grid_pos", new Vector2I(-9999, -9999)) != triggeringUnit.GridPos)
            {
                continue;
            }

            triggeredAny |= TryTriggerTrap(prop, triggeringUnit);
        }

        if (!triggeredAny)
        {
            return false;
        }

        SaveMapInteractionStateForCurrentMap();
        CleanupDefeatedUnits();
        combatEnded = CheckCombatResolved();
        if (!combatEnded)
        {
            SyncHudFromGameState();
        }
        _persistence.PersistSaveGame(false);
        QueueRedraw();
        return true;
    }

    private bool TryTriggerTrap(Dictionary trap, Unit triggeringUnit)
    {
        if (trap == null || !IsUsableUnit(triggeringUnit) || triggeringUnit.IsDead)
        {
            return false;
        }

        var trapId = GetString(trap, "id", "");
        if (string.IsNullOrEmpty(trapId) || _openedPropIds.Contains(trapId))
        {
            return false;
        }

        _openedPropIds.Add(trapId);
        var trapName = GetString(trap, "name", "Trap");
        var damage = Mathf.Max(0, GetInt(trap, "damage", 0));
        var targetScope = GetString(trap, "target_scope", "triggering_unit").ToLowerInvariant();
        var targets = new List<Unit>();
        if (targetScope == "party")
        {
            foreach (var unit in _allUnits)
            {
                if (IsUsableUnit(unit) && !unit.IsDead && unit.Team == triggeringUnit.Team)
                {
                    targets.Add(unit);
                }
            }
        }
        else
        {
            targets.Add(triggeringUnit);
        }

        foreach (var target in targets)
        {
            var appliedDamage = target.ApplyDamage(damage);
            var result = target.IsDead
                ? $"{target.UnitName} triggered {trapName}, took {appliedDamage} damage, and was defeated."
                : $"{target.UnitName} triggered {trapName} and took {appliedDamage} damage.";

            if (appliedDamage > 0)
            {
                var awakenedEffects = target.ClearWakeOnDamageStatusEffects();
                if (awakenedEffects.Count > 0)
                {
                    result += $" {target.UnitName} wakes up.";
                }
            }

            _hud?.AddCombatLogEntry(result);
        }

        return true;
    }

    private bool TryOpenDoorAtCell(Vector2I cell)
    {
        if (_flowState != BattleFlowState.Exploration)
        {
            return false;
        }

        if (!TryGetDoorAtCell(cell, out var door))
        {
            return false;
        }

        var explorer = GetExplorerUnit();
        if (!IsUsableUnit(explorer) || explorer.IsDead)
        {
            return false;
        }

        var doorCell = GetVector2I(door, "cell", new Vector2I(-9999, -9999));
        if (Manhattan(explorer.GridPos, doorCell) > 1)
        {
            return false;
        }

        var doorId = GetString(door, "id", "");
        var isOpen = IsDoorOpen(door);
        var shouldOpen = !isOpen;

        if (!string.IsNullOrEmpty(doorId))
        {
            if (shouldOpen)
            {
                _openedDoorIds.Add(doorId);
            }
            else
            {
                _openedDoorIds.Remove(doorId);
            }
        }

        door["is_open"] = shouldOpen;
        _mapLoader?.SetDoorVisual(_currentMapId, doorCell, shouldOpen);
        _hud?.AddCombatLogEntry(shouldOpen
            ? $"{explorer.UnitName} opened a door."
            : $"{explorer.UnitName} closed a door.");
        SaveMapInteractionStateForCurrentMap();
        _persistence.PersistSaveGame(false);
        SetStatusHelp();
        QueueRedraw();
        return true;
    }

    private void SyncDoorVisualStateForCurrentMap()
    {
        if (_mapLoader == null)
        {
            return;
        }

        foreach (var door in _mapDoors)
        {
            var cell = GetVector2I(door, "cell", new Vector2I(-9999, -9999));
            if (cell.X < 0 || cell.Y < 0)
            {
                continue;
            }

            _mapLoader.SetDoorVisual(_currentMapId, cell, IsDoorOpen(door));
        }
    }

    private bool CanExplorationLeaderEnterCell(Vector2I cell, HashSet<Unit> partyMembers)
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

    private bool CanExplorationFollowerEnterCell(Vector2I cell, HashSet<Unit> partyMembers)
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

    string IGamePersistenceHost.CurrentMapId
    {
        get => _currentMapId;
        set => _currentMapId = value;
    }

    string IGamePersistenceHost.ActiveEncounterId
    {
        get => _activeEncounterId;
        set => _activeEncounterId = value;
    }

    string IGamePersistenceHost.SelectedCharacterUnitId
    {
        get => _selectedCharacterUnitId;
        set => _selectedCharacterUnitId = value;
    }

    int IGamePersistenceHost.PartyGold
    {
        get => _partyGold;
        set => _partyGold = Mathf.Max(0, value);
    }

    System.Collections.Generic.Dictionary<string, string> IGamePersistenceHost.SelectedAbilityIdByUnitId => _selectedAbilityIdByUnitId;
    System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>> IGamePersistenceHost.EquippedItemsByUnitId => _equippedItemsByUnitId;
    List<string> IGamePersistenceHost.PartyInventoryItemIds => _partyInventoryItemIds;
    System.Collections.Generic.Dictionary<string, int> IGamePersistenceHost.VendorGoldById => _vendorGoldById;
    System.Collections.Generic.Dictionary<string, List<string>> IGamePersistenceHost.VendorInventoryItemIdsById => _vendorInventoryItemIdsById;
    System.Collections.Generic.Dictionary<string, HashSet<string>> IGamePersistenceHost.ClearedEncounterIdsByMap => _clearedEncounterIdsByMap;
    System.Collections.Generic.Dictionary<string, HashSet<string>> IGamePersistenceHost.OpenedDoorIdsByMap => _openedDoorIdsByMap;
    System.Collections.Generic.Dictionary<string, HashSet<string>> IGamePersistenceHost.OpenedPropIdsByMap => _openedPropIdsByMap;
    System.Collections.Generic.Dictionary<string, HashSet<string>> IGamePersistenceHost.DefeatedEnemyIdsByMap => _defeatedEnemyIdsByMap;
    System.Collections.Generic.Dictionary<string, HashSet<string>> IGamePersistenceHost.LootedBagIdsByMap => _lootedBagIdsByMap;
    System.Collections.Generic.Dictionary<string, Array<Dictionary>> IGamePersistenceHost.LootBagsByMap => _lootBagsByMap;

    string IGamePersistenceHost.GetFlowStateToken() => _flowState == BattleFlowState.Combat ? "combat" : (_flowState == BattleFlowState.Defeat ? "defeat" : "exploration");
    string IGamePersistenceHost.GetExplorerUnitId() => _explorerUnit?.UnitId ?? "";
    void IGamePersistenceHost.SetExplorerUnitById(string unitId) => _explorerUnit = FindUnitById(unitId);
    void IGamePersistenceHost.SaveClearedEncounterStateForCurrentMap() => SaveClearedEncounterStateForCurrentMap();
    void IGamePersistenceHost.SpawnMapEncounter(string mapId) => SpawnMapEncounter(mapId, preserveParty: false, leadSpawnCell: default, preserveCurrentMapState: false);
    Array<Dictionary> IGamePersistenceHost.BuildPartyRoster() => BuildPartyRosterSnapshot();
    void IGamePersistenceHost.RestorePartyRoster(Array<Dictionary> roster) => RestorePartyRosterSnapshot(roster);
    Array<Dictionary> IGamePersistenceHost.BuildReserveRoster() => BuildReserveRosterSnapshot();
    void IGamePersistenceHost.RestoreReserveRoster(Array<Dictionary> reserveRoster) => RestoreReserveRosterSnapshot(reserveRoster);
    Array<string> IGamePersistenceHost.BuildRecruitedNpcIds() => BuildRecruitedNpcIdSnapshot();
    void IGamePersistenceHost.RestoreRecruitedNpcIds(Array<string> recruitedNpcIds) => RestoreRecruitedNpcIdSnapshot(recruitedNpcIds);

    Array<Dictionary> IGamePersistenceHost.BuildUnitSnapshots()
    {
        var unitSnapshots = new Array<Dictionary>();
        foreach (var unit in _allUnits)
        {
            if (!IsUsableUnit(unit))
            {
                continue;
            }

            unitSnapshots.Add(unit.BuildRuntimeSnapshot());
        }

        return unitSnapshots;
    }

    void IGamePersistenceHost.ApplyUnitSnapshots(Array<Dictionary> snapshots)
    {
        var byId = new System.Collections.Generic.Dictionary<string, Dictionary>();
        foreach (var snapshot in snapshots)
        {
            var unitId = GetString(snapshot, "unit_id", "");
            if (!string.IsNullOrEmpty(unitId))
            {
                byId[unitId] = snapshot;
            }
        }

        foreach (var unit in _allUnits)
        {
            if (!IsUsableUnit(unit) || string.IsNullOrEmpty(unit.UnitId))
            {
                continue;
            }

            if (byId.TryGetValue(unit.UnitId, out var snapshot))
            {
                unit.ApplyRuntimeSnapshot(snapshot);
            }

            ApplyEquippedItemBonuses(unit);
        }

        CleanupDefeatedUnits();
        PruneInvalidUnitReferences();
    }

    void IGamePersistenceHost.RestoreFlowState(string flowStateToken)
    {
        _isEndingTurn = false;
        _isEnemyTurnProcessing = false;
        _activeCombatEnemyUnitIds.Clear();
        _activeCombatEncounterIds.Clear();

        if (flowStateToken == "combat" && !string.IsNullOrEmpty(_activeEncounterId) && HasLivingEnemiesInEncounter(_activeEncounterId))
        {
            _flowState = BattleFlowState.Exploration;
            StartCombat(_activeEncounterId);
            return;
        }

        EnterExplorationMode("Loaded save.");
    }

    void IGamePersistenceHost.SyncHudFromGameState() => SyncHudFromGameState();
    void IGamePersistenceHost.RequestRedraw() => QueueRedraw();
}
