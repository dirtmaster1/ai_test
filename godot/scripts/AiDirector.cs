using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

public partial class AiDirector : Node
{
    private static readonly Vector2I[] CardinalDirections =
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1)
    };

    public readonly struct ActionOption
    {
        public string AbilityId { get; }
        public string ActionType { get; }
        public int Range { get; }
        public bool CanUseNow { get; }
        public bool CanPlanFromMovement { get; }

        public ActionOption(string abilityId, string actionType, int range, bool canUseNow, bool canPlanFromMovement)
        {
            AbilityId = abilityId;
            ActionType = actionType;
            Range = Mathf.Max(0, range);
            CanUseNow = canUseNow;
            CanPlanFromMovement = canPlanFromMovement;
        }
    }

    public readonly struct ActionChoice
    {
        public bool HasChoice { get; }
        public string AbilityId { get; }
        public string ActionType { get; }
        public Unit Target { get; }

        private ActionChoice(bool hasChoice, string abilityId, string actionType, Unit target)
        {
            HasChoice = hasChoice;
            AbilityId = abilityId;
            ActionType = actionType;
            Target = target;
        }

        public static ActionChoice None => new(false, "", "", null);

        public static ActionChoice Create(ActionOption option, Unit target)
        {
            return new ActionChoice(true, option.AbilityId, option.ActionType, target);
        }
    }

    public ActionChoice ChooseAction(Unit actor, IEnumerable<ActionOption> options, Array<Unit> candidates, Func<Unit, Unit, ActionOption, bool> canTarget)
    {
        if (actor == null || options == null || candidates == null || canTarget == null)
        {
            return ActionChoice.None;
        }

        foreach (var option in options)
        {
            if (!option.CanUseNow || option.ActionType == "defend")
            {
                continue;
            }

            var target = option.ActionType == "heal"
                ? ChooseMostInjuredTarget(actor, option, candidates, canTarget)
                : ChooseNearestTarget(actor, option, candidates, canTarget);

            if (target != null)
            {
                return ActionChoice.Create(option, target);
            }
        }

        return ActionChoice.None;
    }

    public bool TryChooseStepTowardActionRange(
        Unit actor,
        Array<Unit> targets,
        IEnumerable<ActionOption> options,
        Func<Unit, bool> isValidTarget,
        Func<Vector2I, bool> canStandAtCell,
        Func<Vector2I, Vector2I, bool> hasLineOfSight,
        Func<Vector2I, Array<Vector2I>> findPath,
        out Vector2I step)
    {
        step = actor?.GridPos ?? Vector2I.Zero;
        if (actor == null || actor.IsDead || actor.RemainingMovement <= 0 || targets == null || options == null || isValidTarget == null || canStandAtCell == null || hasLineOfSight == null || findPath == null)
        {
            return false;
        }

        var bestPath = new Array<Vector2I>();
        var bestDistanceToTarget = int.MaxValue;

        foreach (var target in targets)
        {
            if (!isValidTarget(target))
            {
                continue;
            }

            foreach (var candidate in GetCandidateActionCellsForTarget(actor, target, options, canStandAtCell, hasLineOfSight))
            {
                var path = findPath(candidate);
                if (path.Count == 0)
                {
                    continue;
                }

                var distanceToTarget = Manhattan(candidate, target.GridPos);
                if (bestPath.Count == 0 || path.Count < bestPath.Count || (path.Count == bestPath.Count && distanceToTarget < bestDistanceToTarget))
                {
                    bestPath = path;
                    bestDistanceToTarget = distanceToTarget;
                }
            }
        }

        if (bestPath.Count == 0)
        {
            return false;
        }

        step = bestPath[0];
        return true;
    }

    private static int Manhattan(Vector2I a, Vector2I b)
    {
        return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
    }

    private static Unit ChooseNearestTarget(Unit actor, ActionOption option, Array<Unit> candidates, Func<Unit, Unit, ActionOption, bool> canTarget)
    {
        Unit best = null;
        var bestDistance = int.MaxValue;

        foreach (var unit in candidates)
        {
            if (!canTarget(actor, unit, option))
            {
                continue;
            }

            var distance = Manhattan(actor.GridPos, unit.GridPos);
            if (distance < bestDistance)
            {
                best = unit;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static Unit ChooseMostInjuredTarget(Unit actor, ActionOption option, Array<Unit> candidates, Func<Unit, Unit, ActionOption, bool> canTarget)
    {
        Unit best = null;
        var bestMissingHp = 0;

        foreach (var unit in candidates)
        {
            if (!canTarget(actor, unit, option))
            {
                continue;
            }

            var missingHp = unit.MaxHitPoints - unit.HitPoints;
            if (missingHp > bestMissingHp)
            {
                best = unit;
                bestMissingHp = missingHp;
            }
        }

        return best;
    }

    private static Array<Vector2I> GetCandidateActionCellsForTarget(Unit actor, Unit target, IEnumerable<ActionOption> options, Func<Vector2I, bool> canStandAtCell, Func<Vector2I, Vector2I, bool> hasLineOfSight)
    {
        var candidates = new Array<Vector2I>();
        if (actor == null || target == null || options == null)
        {
            return candidates;
        }

        foreach (var option in options)
        {
            if (!option.CanPlanFromMovement || option.ActionType != "attack")
            {
                continue;
            }

            for (var dx = -option.Range; dx <= option.Range; dx++)
            {
                for (var dy = -option.Range; dy <= option.Range; dy++)
                {
                    var candidate = target.GridPos + new Vector2I(dx, dy);
                    if (candidate == actor.GridPos || candidates.Contains(candidate))
                    {
                        continue;
                    }

                    if (!canStandAtCell(candidate))
                    {
                        continue;
                    }

                    if (!Unit.IsWithinRange(candidate, target.GridPos, option.Range) || !hasLineOfSight(candidate, target.GridPos))
                    {
                        continue;
                    }

                    candidates.Add(candidate);
                }
            }
        }

        if (candidates.Count > 0)
        {
            return candidates;
        }

        foreach (var direction in CardinalDirections)
        {
            var candidate = target.GridPos + direction;
            if (canStandAtCell(candidate))
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }
}
