using Godot;
using Godot.Collections;

public partial class TurnManager : Node
{
    [Signal]
    public delegate void TurnChangedEventHandler(Unit activeUnit);

    private Array<Unit> _turnOrder = new();
    private int _activeIndex;
    private EventBus _eventBus;

    public override void _Ready()
    {
        _eventBus = GetNodeOrNull<EventBus>("/root/EventBus");
    }

    public void SetupTurnOrder(Array<Unit> units)
    {
        _turnOrder.Clear();
        foreach (var unit in units)
        {
            if (unit != null && !unit.IsDead)
            {
                _turnOrder.Add(unit);
            }
        }

        _activeIndex = 0;
        EmitActive();
    }

    public Unit GetActiveUnit()
    {
        if (_turnOrder.Count == 0)
        {
            return null;
        }

        return _turnOrder[_activeIndex];
    }

    public void EndTurn()
    {
        if (_turnOrder.Count == 0)
        {
            return;
        }

        CompactTurnOrder();
        if (_turnOrder.Count == 0)
        {
            return;
        }

        _activeIndex = (_activeIndex + 1) % _turnOrder.Count;
        EmitActive();
    }

    private void CompactTurnOrder()
    {
        var compacted = new Array<Unit>();
        foreach (var unit in _turnOrder)
        {
            if (unit != null && !unit.IsDead)
            {
                compacted.Add(unit);
            }
        }

        _turnOrder = compacted;
        if (_turnOrder.Count == 0)
        {
            _activeIndex = 0;
            return;
        }

        _activeIndex = Mathf.Clamp(_activeIndex, 0, _turnOrder.Count - 1);
    }

    private void EmitActive()
    {
        var active = GetActiveUnit();
        EmitSignal(SignalName.TurnChanged, active);
        _eventBus?.EmitSignal(EventBus.SignalName.TurnStarted, active);
    }
}
