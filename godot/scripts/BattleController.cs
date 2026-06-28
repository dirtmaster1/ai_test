using Godot;
using Godot.Collections;

public partial class BattleController : Node2D
{
    private const int GridWidth = 15;
    private const int GridHeight = 10;
    private const int CellSize = 64;
    private const int DefaultAggroTriggerRange = 4;

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
    private readonly System.Collections.Generic.Dictionary<string, int> _encounterAggroRanges = new();
    private readonly System.Collections.Generic.HashSet<string> _clearedEncounterIds = new();
    private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>> _clearedEncounterIdsByMap = new();

    private BattleFlowState _flowState = BattleFlowState.Exploration;
    private bool _awaitingPlayerAttackDirection;
    private Unit _explorerUnit;
    private string _activeEncounterId = "";
    private string _currentMapId = "map-a";

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
        public static CombatActionResult AttackResolved => new(true, true, false);
        public static CombatActionResult CombatResolvedResult => new(true, false, true);
    }

    private readonly struct ActionProfile
    {
        public string ActionId { get; }
        public string ActionType { get; }
        public int Range { get; }
        public int Damage { get; }
        public int HealAmount { get; }

        public ActionProfile(string actionId, string actionType, int range, int damage, int healAmount)
        {
            ActionId = actionId;
            ActionType = actionType;
            Range = range;
            Damage = damage;
            HealAmount = healAmount;
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

        SpawnMapEncounter(_currentMapId);
        _turnManager.TurnChanged += OnTurnChanged;
        EnterExplorationMode();
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(
            new Rect2(Vector2.Zero, new Vector2(GridWidth * CellSize, GridHeight * CellSize)),
            new Color(0.08f, 0.08f, 0.1f),
            true
        );

        for (var x = 0; x <= GridWidth; x++)
        {
            DrawLine(
                new Vector2(x * CellSize, 0),
                new Vector2(x * CellSize, GridHeight * CellSize),
                new Color(0.2f, 0.2f, 0.24f),
                1.0f
            );
        }

        for (var y = 0; y <= GridHeight; y++)
        {
            DrawLine(
                new Vector2(0, y * CellSize),
                new Vector2(GridWidth * CellSize, y * CellSize),
                new Color(0.2f, 0.2f, 0.24f),
                1.0f
            );
        }

        _mapLoader?.DrawMapFeaturesOverlay(this, _blockedCells, _mapTransitions, GridWidth, GridHeight, CellSize);
        DrawAttackPreviewOverlay();
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
            HandleMouseAttackInput(mouseEvent);
            return;
        }

        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
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
            ApplyActionResult(CombatActionResult.PassResolved);
            return;
        }

        var active = GetActivePlayerUnit();
        if (active == null)
        {
            return;
        }

        if (keyEvent.Keycode == Key.F)
        {
            _awaitingPlayerAttackDirection = true;
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

        var clickedCell = WorldToCell(GetGlobalMousePosition());
        if (Manhattan(active.GridPos, clickedCell) != 1)
        {
            _hud?.SetStatusText("Click an adjacent target tile.");
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

        CancelAttackMode(false);
        TryResolvePlayerActionAtCell(active, active.GridPos + delta);
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

        if (activeUnit.Team == "enemy")
        {
            RunEnemyTurn(activeUnit);
        }
        else
        {
            SetStatusHelp();
        }
    }

    private void TryResolvePlayerActionAtCell(Unit active, Vector2I targetCell)
    {
        var actionProfile = ResolveActionProfile(active);
        if (actionProfile.ActionType == "heal")
        {
            var allyTarget = GetLivingAllyAtCell(active.Team, targetCell);
            if (allyTarget != null)
            {
                if (!TryHealTarget(active, allyTarget, actionProfile.HealAmount, actionProfile.Range, actionProfile.ActionId))
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
                if (!TryAttackTarget(active, attackTarget, actionProfile.Damage, actionProfile.Range, actionProfile.ActionId))
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

    private void RunEnemyTurn(Unit enemyUnit)
    {
        if (_flowState != BattleFlowState.Combat)
        {
            return;
        }

        var attackResult = TryUsePrimaryAction(enemyUnit);
        if (attackResult.Success)
        {
            ApplyActionResult(attackResult);
            return;
        }

        var target = _aiDirector.ChooseTarget(enemyUnit, _playerUnits);
        if (target == null)
        {
            ApplyActionResult(CombatActionResult.PassResolved);
            return;
        }

        var step = _aiDirector.ChooseStepTowardTarget(enemyUnit, target);
        var moveResult = ResolveMoveAction(enemyUnit, step, endTurnOnSuccess: true);
        ApplyActionResult(moveResult.Success ? moveResult : CombatActionResult.PassResolved);
    }

    private void ApplyActionResult(CombatActionResult result)
    {
        if (!result.Success || result.CombatEnded)
        {
            return;
        }

        if (result.ShouldEndTurn)
        {
            _turnManager.EndTurn();
        }
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
        }

        _blockedCells.Clear();
        _mapTransitions.Clear();
        _encounterAggroRanges.Clear();
        _activeEncounterId = "";
        _currentMapId = mapId;

        var mapData = _mapLoader?.LoadMapStub(mapId) ?? new MapLoader().LoadMapStub(mapId);
        _currentMapId = GetString(mapData, "id", mapId);
        LoadClearedEncounterStateForCurrentMap();

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
        }
        else
        {
            _enemyUnits.Add(unit);
        }
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
        var actionProfile = ResolveActionProfile(actor);
        Unit target = actionProfile.ActionType == "heal"
            ? FindMostInjuredAllyInRange(actor, actionProfile.Range)
            : FindNearestEnemyInRange(actor, actionProfile.Range);

        if (target == null)
        {
            _hud?.SetStatusText($"{actor.UnitName} has no {actionProfile.ActionType} target in range ({actionProfile.Range}).");
            return CombatActionResult.Failed;
        }

        var actionSuccess = actionProfile.ActionType == "heal"
            ? TryHealTarget(actor, target, actionProfile.HealAmount, actionProfile.Range, actionProfile.ActionId)
            : TryAttackTarget(actor, target, actionProfile.Damage, actionProfile.Range, actionProfile.ActionId);

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
        if (!TryMoveUnit(unit, targetCell))
        {
            return CombatActionResult.Failed;
        }

        return endTurnOnSuccess ? CombatActionResult.MoveAndEndTurnResolved : CombatActionResult.MoveResolved;
    }

    private bool TryAttackTarget(Unit attacker, Unit target, int damage, int range, string actionId = "attack")
    {
        if (!CanAttack(attacker, target, range))
        {
            _hud?.SetStatusText($"{attacker.UnitName} has no clear line to {target.UnitName}.");
            return false;
        }

        target.ApplyDamage(damage);
        _eventBus?.EmitSignal(EventBus.SignalName.ActionUsed, attacker, actionId, target.UnitId);

        var resultText = $"{attacker.UnitName} hits {target.UnitName} for {damage}.";
        if (target.IsDead)
        {
            resultText += $" {target.UnitName} is defeated.";
        }

        _hud?.SetStatusText(resultText);
        return true;
    }

    private bool TryHealTarget(Unit actor, Unit target, int healAmount, int range, string actionId)
    {
        if (!CanHeal(actor, target, range))
        {
            _hud?.SetStatusText($"{actor.UnitName} cannot heal {target.UnitName} from there.");
            return false;
        }

        var healed = target.ApplyHealing(healAmount);
        if (healed <= 0)
        {
            _hud?.SetStatusText($"{target.UnitName} is already at full health.");
            return false;
        }

        _eventBus?.EmitSignal(EventBus.SignalName.ActionUsed, actor, actionId, target.UnitId);
        _hud?.SetStatusText($"{actor.UnitName} heals {target.UnitName} for {healed}.");
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

            var distance = Manhattan(attacker.GridPos, unit.GridPos);
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

    // Math and data helpers
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
        return Manhattan(from, to) <= range;
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

    private ActionProfile ResolveActionProfile(Unit actor)
    {
        if (actor == null)
        {
            return new ActionProfile("attack", "attack", 1, 0, 0);
        }

        var fallback = new ActionProfile("attack", "attack", actor.AttackRange, actor.AttackDamage, 0);
        if (_gameData == null || string.IsNullOrEmpty(actor.PrimaryAbilityId))
        {
            return fallback;
        }

        var actionData = _gameData.GetAbility(actor.PrimaryAbilityId);
        if (actionData.Count == 0)
        {
            actionData = _gameData.GetSpell(actor.PrimaryAbilityId);
        }

        if (actionData.Count == 0)
        {
            return fallback;
        }

        var actionType = GetString(actionData, "type", "attack");
        var range = Mathf.Max(1, GetInt(actionData, "range", actor.AttackRange));
        var damage = Mathf.Max(0, GetInt(actionData, "damage", actor.AttackDamage));
        var healAmount = Mathf.Max(0, GetInt(actionData, "heal_amount", 0));

        return new ActionProfile(actor.PrimaryAbilityId, actionType, range, damage, healAmount);
    }

    private static int GetInt(Dictionary dict, string key, int fallback)
    {
        return dict.ContainsKey(key) ? (int)((Variant)dict[key]) : fallback;
    }

    // UI helpers
    private void CancelAttackMode(bool restoreHelpText = true)
    {
        _awaitingPlayerAttackDirection = false;
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

        var actionProfile = ResolveActionProfile(active);
        var center = CellCenter(active.GridPos);
        DrawArc(center, 28.0f, 0.0f, Mathf.Tau, 40, new Color(1.0f, 0.85f, 0.35f, 0.95f), 3.0f);

        foreach (var dir in AttackDirections)
        {
            var cell = active.GridPos + dir;
            if (!IsInBounds(cell))
            {
                continue;
            }

            var cellRect = new Rect2(new Vector2(cell.X * CellSize, cell.Y * CellSize), new Vector2(CellSize, CellSize));
            var target = actionProfile.ActionType == "heal"
                ? GetLivingAllyAtCell(active.Team, cell)
                : GetLivingEnemyAtCell(active.Team, cell);
            var valid = target != null && CanUseActionAtRange(active, target, actionProfile.Range);

            var fill = valid ? new Color(0.2f, 0.9f, 0.3f, 0.25f) : new Color(0.9f, 0.25f, 0.25f, 0.18f);
            var edge = valid ? new Color(0.3f, 1.0f, 0.45f, 0.9f) : new Color(1.0f, 0.4f, 0.4f, 0.8f);
            DrawRect(cellRect, fill, true);
            DrawRect(cellRect, edge, false, 2.0f);
        }
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

            _hud?.SetStatusText($"Map: {_currentMapId} | Exploration: {explorer.UnitName} | Move: WASD / Arrows | Step on edge markers to transition");
            return;
        }

        if (_flowState == BattleFlowState.Defeat)
        {
            _hud?.SetStatusText("Defeat. All player units were defeated.");
            return;
        }

        var active = _turnManager.GetActiveUnit();
        if (active == null)
        {
            _hud?.SetStatusText("No active unit");
            return;
        }

        _hud?.SetStatusText($"Turn: {active.UnitName} ({active.Team}) | Move: WASD / Arrows | Attack: F then direction | End turn: Space");
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
        for (var i = _enemyUnits.Count - 1; i >= 0; i--)
        {
            var enemy = _enemyUnits[i];
            if (!IsUsableUnit(enemy))
            {
                continue;
            }

            enemy.QueueFree();
        }

        _enemyUnits.Clear();

        for (var i = _allUnits.Count - 1; i >= 0; i--)
        {
            var unit = _allUnits[i];
            if (!IsUsableUnit(unit) || unit.Team == "enemy")
            {
                _allUnits.RemoveAt(i);
            }
        }
    }

    private int GetEncounterAggroRange(string encounterId)
    {
        return _encounterAggroRanges.TryGetValue(encounterId, out var range) ? range : DefaultAggroTriggerRange;
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

    private static string GetString(Dictionary dict, string key, string fallback)
    {
        return dict.ContainsKey(key) ? ((Variant)dict[key]).AsString() : fallback;
    }

    private static Vector2I GetVector2I(Dictionary dict, string key, Vector2I fallback)
    {
        return dict.ContainsKey(key) ? (Vector2I)((Variant)dict[key]) : fallback;
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
