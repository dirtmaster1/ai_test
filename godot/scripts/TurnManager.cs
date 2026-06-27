using Godot;
using Godot.Collections;
using System.Collections.Generic;

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
        var ordered = new List<Unit>();
        foreach (var unit in units)
        {
            if (unit != null && !unit.IsDead)
            {
                ordered.Add(unit);
            }
        }

        ordered.Sort((a, b) =>
        {
            var byInitiative = b.Initiative.CompareTo(a.Initiative);
            if (byInitiative != 0)
            {
                return byInitiative;
            }

            return string.CompareOrdinal(a.UnitId, b.UnitId);
        });

        _turnOrder = new Array<Unit>();
        foreach (var unit in ordered)
        {
            _turnOrder.Add(unit);
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

        var endingUnit = GetActiveUnit();
        _eventBus?.EmitSignal(EventBus.SignalName.TurnEnded, endingUnit);

        var nextUnit = FindNextLivingAfter(_activeIndex);
        CompactTurnOrder();
        if (_turnOrder.Count == 0 || nextUnit == null)
        {
            return;
        }

        var nextIndex = _turnOrder.IndexOf(nextUnit);
        if (nextIndex < 0)
        {
            nextIndex = 0;
        }

        _activeIndex = nextIndex;
        EmitActive();
    }

    private Unit FindNextLivingAfter(int startIndex)
    {
        if (_turnOrder.Count == 0)
        {
            return null;
        }

        for (var step = 1; step <= _turnOrder.Count; step++)
        {
            var index = (startIndex + step) % _turnOrder.Count;
            var candidate = _turnOrder[index];
            if (candidate != null && !candidate.IsDead)
            {
                return candidate;
            }
        }

        return null;
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
