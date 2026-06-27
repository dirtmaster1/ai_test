using Godot;
using Godot.Collections;

public partial class BattleController : Node2D
{
    private const int GridWidth = 15;
    private const int GridHeight = 10;
    private const int CellSize = 64;

    private Node2D _unitsRoot;
    private TurnManager _turnManager;
    private AiDirector _aiDirector;
    private HudController _hud;
    private EventBus _eventBus;
    private GameData _gameData;

    private readonly PackedScene _unitScene = GD.Load<PackedScene>("res://scenes/Unit.tscn");
    private readonly Array<Unit> _allUnits = new();
    private readonly Array<Unit> _playerUnits = new();
    private readonly Array<Unit> _enemyUnits = new();

    private bool _combatResolved;
    private bool _awaitingPlayerAttackDirection;

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

    // Lifecycle and rendering
    public override void _Ready()
    {
        _unitsRoot = GetNode<Node2D>("Units");
        _turnManager = GetNode<TurnManager>("TurnManager");
        _aiDirector = GetNode<AiDirector>("AiDirector");
        _hud = GetNodeOrNull<HudController>("HUD");
        _eventBus = GetNodeOrNull<EventBus>("/root/EventBus");
        _gameData = GetNodeOrNull<GameData>("/root/GameData");

        SpawnTestEncounter();
        _eventBus?.EmitSignal(EventBus.SignalName.CombatStarted);
        _turnManager.TurnChanged += OnTurnChanged;
        _turnManager.SetupTurnOrder(_allUnits);
        SetStatusHelp();
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

        DrawAttackPreviewOverlay();
    }

    // Input and player control
    public override void _Input(InputEvent @event)
    {
        if (_combatResolved)
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
        if (_combatResolved)
        {
            return;
        }

        _awaitingPlayerAttackDirection = false;
        QueueRedraw();

        foreach (var unit in _allUnits)
        {
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
        var target = GetLivingEnemyAtCell(active.Team, targetCell);
        if (target == null)
        {
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

            return;
        }

        var attackProfile = ResolveAttackProfile(active);
        if (!TryAttackTarget(active, target, attackProfile.damage, attackProfile.range, attackProfile.actionId))
        {
            return;
        }

        var result = ResolveSuccessfulAttack();
        ApplyActionResult(result);
    }

    private void RunEnemyTurn(Unit enemyUnit)
    {
        if (_combatResolved)
        {
            return;
        }

        var attackResult = TryAttackNearestEnemy(enemyUnit);
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
    private void SpawnTestEncounter()
    {
        _allUnits.Clear();
        _playerUnits.Clear();
        _enemyUnits.Clear();

        var configs = new Array<Dictionary>
        {
            new Dictionary { { "id", "wizard" }, { "name", "Wizard" }, { "team", "player" }, { "grid_pos", new Vector2I(2, 2) }, { "primary_ability_id", "melee" }, { "initiative", 15 }, { "hit_points", 10 }, { "max_hit_points", 10 } },
            new Dictionary { { "id", "warrior" }, { "name", "Warrior" }, { "team", "player" }, { "grid_pos", new Vector2I(2, 4) }, { "primary_ability_id", "melee" }, { "initiative", 11 }, { "hit_points", 14 }, { "max_hit_points", 14 } },
            new Dictionary { { "id", "goblin-warrior" }, { "name", "Goblin" }, { "team", "enemy" }, { "grid_pos", new Vector2I(11, 2) }, { "primary_ability_id", "melee" }, { "initiative", 9 }, { "hit_points", 8 }, { "max_hit_points", 8 } },
            new Dictionary { { "id", "goblin-archer" }, { "name", "Goblin Archer" }, { "team", "enemy" }, { "grid_pos", new Vector2I(11, 4) }, { "primary_ability_id", "ranged" }, { "initiative", 13 }, { "hit_points", 7 }, { "max_hit_points", 7 } }
        };

        foreach (var config in configs)
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
    }

    // Combat and grid rules
    private bool TryMoveUnit(Unit unit, Vector2I targetCell)
    {
        if (!IsInBounds(targetCell))
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

    private CombatActionResult TryAttackNearestEnemy(Unit attacker)
    {
        var attackProfile = ResolveAttackProfile(attacker);
        var target = FindNearestEnemyInRange(attacker, attackProfile.range);
        if (target == null)
        {
            _hud?.SetStatusText($"{attacker.UnitName} has no target in range ({attackProfile.range}).");
            return CombatActionResult.Failed;
        }

        if (!TryAttackTarget(attacker, target, attackProfile.damage, attackProfile.range, attackProfile.actionId))
        {
            return CombatActionResult.Failed;
        }

        return ResolveSuccessfulAttack();
    }

    private CombatActionResult ResolveSuccessfulAttack()
    {
        CleanupDefeatedUnits();
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

    private Unit FindNearestEnemyInRange(Unit attacker, int range)
    {
        Unit nearest = null;
        var nearestDistance = int.MaxValue;

        foreach (var unit in _allUnits)
        {
            if (unit == null || unit.IsDead || unit.Team == attacker.Team)
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

    private void CleanupDefeatedUnits()
    {
        RemoveDeadFromTeam(_playerUnits);
        RemoveDeadFromTeam(_enemyUnits);
    }

    private static void RemoveDeadFromTeam(Array<Unit> units)
    {
        for (var i = units.Count - 1; i >= 0; i--)
        {
            if (units[i] == null || units[i].IsDead)
            {
                units.RemoveAt(i);
            }
        }
    }

    private bool CheckCombatResolved()
    {
        if (_playerUnits.Count == 0)
        {
            _combatResolved = true;
            _eventBus?.EmitSignal(EventBus.SignalName.CombatEnded);
            _hud?.SetStatusText("Defeat. All player units were defeated.");
            return true;
        }

        if (_enemyUnits.Count == 0)
        {
            _combatResolved = true;
            _eventBus?.EmitSignal(EventBus.SignalName.CombatEnded);
            _hud?.SetStatusText("Victory! All enemies were defeated.");
            return true;
        }

        return false;
    }

    private Unit GetActivePlayerUnit()
    {
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

    private bool IsOccupied(Vector2I cell, Unit ignoreUnit = null)
    {
        foreach (var unit in _allUnits)
        {
            if (unit == ignoreUnit || unit.IsDead)
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

        return IsWithinRange(attacker.GridPos, target.GridPos, range) && HasLineOfSight(attacker, target);
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
            if (unit == null || unit.IsDead || unit == attacker || unit == target)
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

    private (int damage, int range, string actionId) ResolveAttackProfile(Unit attacker)
    {
        if (attacker == null)
        {
            return (0, 1, "attack");
        }

        var fallback = (attacker.AttackDamage, attacker.AttackRange, "attack");
        if (_gameData == null || string.IsNullOrEmpty(attacker.PrimaryAbilityId))
        {
            return fallback;
        }

        var ability = _gameData.GetAbility(attacker.PrimaryAbilityId);
        if (ability.Count == 0)
        {
            return fallback;
        }

        var damage = Mathf.Max(0, GetInt(ability, "damage", attacker.AttackDamage));
        var range = Mathf.Max(1, GetInt(ability, "range", attacker.AttackRange));
        return (damage, range, attacker.PrimaryAbilityId);
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
        if (active == null || active.Team != "player" || active.IsDead)
        {
            return;
        }

        var attackProfile = ResolveAttackProfile(active);
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
            var target = GetLivingEnemyAtCell(active.Team, cell);
            var valid = target != null && CanAttack(active, target, attackProfile.range);

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
        var active = _turnManager.GetActiveUnit();
        if (active == null)
        {
            _hud?.SetStatusText("No active unit");
            return;
        }

        _hud?.SetStatusText($"Turn: {active.UnitName} ({active.Team}) | Move: WASD / Arrows | Attack: F then direction | End turn: Space");
    }
}
