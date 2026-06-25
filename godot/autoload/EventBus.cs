using Godot;

public partial class EventBus : Node
{
    [Signal]
    public delegate void CombatStartedEventHandler();

    [Signal]
    public delegate void CombatEndedEventHandler();

    [Signal]
    public delegate void TurnStartedEventHandler(Variant activeUnit);

    [Signal]
    public delegate void TurnEndedEventHandler(Variant activeUnit);

    [Signal]
    public delegate void UnitMovedEventHandler(Variant unit, Variant fromCell, Variant toCell);

    [Signal]
    public delegate void ActionUsedEventHandler(Variant unit, Variant actionId, Variant payload);
}