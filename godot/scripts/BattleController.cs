using System;
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

    private readonly PackedScene _unitScene = GD.Load<PackedScene>("res://scenes/Unit.tscn");
    private readonly Array<Unit> _allUnits = new();
    private readonly Array<Unit> _playerUnits = new();
    private readonly Array<Unit> _enemyUnits = new();

    public override void _Ready()
    {
        _unitsRoot = GetNode<Node2D>("Units");
        _turnManager = GetNode<TurnManager>("TurnManager");
        _aiDirector = GetNode<AiDirector>("AiDirector");
        _hud = GetNodeOrNull<HudController>("HUD");
        _eventBus = GetNodeOrNull<EventBus>("/root/EventBus");

        SpawnTestEncounter();
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
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        if (keyEvent.Keycode == Key.Space)
        {
            _turnManager.EndTurn();
            return;
        }

        var active = _turnManager.GetActiveUnit();
        if (active == null || active.Team != "player" || active.IsDead)
        {
            return;
        }

        var delta = Vector2I.Zero;
        switch (keyEvent.Keycode)
        {
            case Key.W:
            case Key.Up:
                delta = new Vector2I(0, -1);
                break;
            case Key.S:
            case Key.Down:
                delta = new Vector2I(0, 1);
                break;
            case Key.A:
            case Key.Left:
                delta = new Vector2I(-1, 0);
                break;
            case Key.D:
            case Key.Right:
                delta = new Vector2I(1, 0);
                break;
            default:
                return;
        }

        TryMoveUnit(active, active.GridPos + delta);
    }

    private void SpawnTestEncounter()
    {
        _allUnits.Clear();
        _playerUnits.Clear();
        _enemyUnits.Clear();

        var configs = new Array<Dictionary>
        {
            new Dictionary { { "id", "wizard" }, { "name", "Wizard" }, { "team", "player" }, { "grid_pos", new Vector2I(2, 2) }, { "hit_points", 10 }, { "max_hit_points", 10 } },
            new Dictionary { { "id", "warrior" }, { "name", "Warrior" }, { "team", "player" }, { "grid_pos", new Vector2I(2, 4) }, { "hit_points", 14 }, { "max_hit_points", 14 } },
            new Dictionary { { "id", "goblin-warrior" }, { "name", "Goblin" }, { "team", "enemy" }, { "grid_pos", new Vector2I(11, 2) }, { "hit_points", 8 }, { "max_hit_points", 8 } },
            new Dictionary { { "id", "goblin-archer" }, { "name", "Goblin Archer" }, { "team", "enemy" }, { "grid_pos", new Vector2I(11, 4) }, { "hit_points", 7 }, { "max_hit_points", 7 } }
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

    private void OnTurnChanged(Unit activeUnit)
    {
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

    private void RunEnemyTurn(Unit enemyUnit)
    {
        var target = _aiDirector.ChooseTarget(enemyUnit, _playerUnits);
        if (target == null)
        {
            _turnManager.EndTurn();
            return;
        }

        var dx = Math.Sign(target.GridPos.X - enemyUnit.GridPos.X);
        var dy = Math.Sign(target.GridPos.Y - enemyUnit.GridPos.Y);
        var step = enemyUnit.GridPos;

        if (Mathf.Abs(target.GridPos.X - enemyUnit.GridPos.X) >= Mathf.Abs(target.GridPos.Y - enemyUnit.GridPos.Y))
        {
            step += new Vector2I(dx, 0);
        }
        else
        {
            step += new Vector2I(0, dy);
        }

        TryMoveUnit(enemyUnit, step);
        _turnManager.EndTurn();
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

    private void SetStatusHelp()
    {
        var active = _turnManager.GetActiveUnit();
        if (active == null)
        {
            _hud?.SetStatusText("No active unit");
            return;
        }

        _hud?.SetStatusText($"Turn: {active.UnitName} ({active.Team}) | Move: WASD / Arrows | End turn: Space");
    }
}
