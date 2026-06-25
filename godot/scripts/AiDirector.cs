using Godot;
using Godot.Collections;

public partial class AiDirector : Node
{
    public Unit ChooseTarget(Unit actor, Array<Unit> candidates)
    {
        if (actor == null || candidates == null || candidates.Count == 0)
        {
            return null;
        }

        Unit best = null;
        var bestDistance = int.MaxValue;

        foreach (var unit in candidates)
        {
            if (unit == null || unit.IsDead)
            {
                continue;
            }

            var distance = Mathf.Abs(unit.GridPos.X - actor.GridPos.X) + Mathf.Abs(unit.GridPos.Y - actor.GridPos.Y);
            if (distance < bestDistance)
            {
                best = unit;
                bestDistance = distance;
            }
        }

        return best;
    }
}
