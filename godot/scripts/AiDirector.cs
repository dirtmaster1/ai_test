using Godot;
using Godot.Collections;

public partial class AiDirector : Node
{
    private static readonly Vector2I[] CardinalDirections =
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1)
    };

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

    public Vector2I ChooseStepTowardTarget(Unit actor, Unit target)
    {
        if (actor == null || target == null)
        {
            return actor?.GridPos ?? Vector2I.Zero;
        }

        var bestStep = actor.GridPos;
        var bestDistance = Manhattan(actor.GridPos, target.GridPos);

        foreach (var direction in CardinalDirections)
        {
            var candidate = actor.GridPos + direction;
            var distance = Manhattan(candidate, target.GridPos);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestStep = candidate;
            }
        }

        return bestStep;
    }

    private static int Manhattan(Vector2I a, Vector2I b)
    {
        return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
    }
}
