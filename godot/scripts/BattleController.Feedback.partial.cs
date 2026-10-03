using Godot;

public partial class BattleController
{
    private bool RejectPlayerAction(string message)
    {
        _hud?.ShowActionFailureBanner(message);
        return false;
    }

    private string ReportInteractionFailure(string message)
    {
        RejectPlayerAction(message);
        return message;
    }

    private string GetActionFailureReason(Unit actor, ActionProfile profile)
    {
        if (!actor.HasAbility(profile.ActionId))
        {
            return $"{actor.UnitName} has not learned {profile.ActionName}.";
        }

        if (actor.TryGetActionPreventingStatusName(out var status))
        {
            return $"{actor.UnitName} cannot act while {status.ToLowerInvariant()}.";
        }

        if (_flowState == BattleFlowState.Combat && !profile.IgnoresActionCost && actor.HasUsedAbilityThisTurn)
        {
            return $"No action points left to use {profile.ActionName}.";
        }

        var cooldown = actor.GetAbilityCooldownRemaining(profile.ActionId);
        if (cooldown > 0)
        {
            return $"{profile.ActionName} is on cooldown - {cooldown} turn{(cooldown == 1 ? "" : "s")} remaining.";
        }

        if (profile.RequiresRangedWeapon && !CanUseRangedWeaponAbility(actor))
        {
            return $"Equip a ranged weapon to use {profile.ActionName}.";
        }

        if (profile.ActionId == "melee" && !CanUseMeleeAbility(actor))
        {
            return "Melee Attack requires a melee weapon or empty hands.";
        }

        if (!actor.HasEnoughMagicPoints(profile.MagicPointCost))
        {
            var verb = profile.IsMagical ? "cast" : "use";
            return actor.MagicPoints == 0
                ? $"Out of mana - cannot {verb} {profile.ActionName}."
                : $"Not enough mana to {verb} {profile.ActionName} - requires {profile.MagicPointCost} MP, have {actor.MagicPoints}.";
        }

        return "";
    }

    private bool ValidatePlayerAction(Unit actor, ActionProfile profile)
    {
        var reason = GetActionFailureReason(actor, profile);
        return string.IsNullOrEmpty(reason) || RejectPlayerAction(reason);
    }

    private bool ValidatePlayerTarget(Unit actor, ActionProfile profile, Vector2I cell)
    {
        if (!IsInBounds(cell))
        {
            return RejectPlayerAction("Target is unreachable.");
        }

        var target = GetLivingUnitAtCell(cell);
        var areaAction = profile.ActionType is "sleep" or "area_attack";
        var distance = target != null && !areaAction && !IsUtilityAction(profile.ActionType)
            ? actor.DistanceToUnitAt(actor.GridPos, target)
            : Unit.RangeDistance(actor.GridPos, cell);
        if (IsUtilityAction(profile.ActionType) ? distance != 1 : distance > profile.Range)
        {
            return RejectPlayerAction(IsUtilityAction(profile.ActionType)
                ? $"Move nearby to use {profile.ActionName}."
                : "Out of range.");
        }

        var clearSight = target != null && !areaAction && !IsUtilityAction(profile.ActionType)
            ? actor.HasLineOfSightTo(target, _allUnits) && HasClearUnitLineOfSight(actor, target)
            : HasClearLineOfSight(actor.GridPos, cell);
        if (profile.ActionType != "charge" && !clearSight)
        {
            return RejectPlayerAction("No line of sight.");
        }

        if (!IsFogCellCurrentlyVisible(cell))
        {
            return RejectPlayerAction("Target area is not visible.");
        }

        if (IsUtilityAction(profile.ActionType))
        {
            if (profile.ActionType == "pick_lock")
            {
                if (!TryGetDoorAtCell(cell, out var door))
                {
                    return RejectPlayerAction("Select a locked door.");
                }

                return IsDoorLocked(door) || RejectPlayerAction("This door is already unlocked.");
            }

            foreach (var prop in _mapProps)
            {
                if (GetString(prop, "type", "") == "trap"
                    && GetVector2I(prop, "grid_pos", new Vector2I(-9999, -9999)) == cell)
                {
                    return !_openedPropIds.Contains(GetString(prop, "id", ""))
                        || RejectPlayerAction("This trap is already disarmed.");
                }
            }

            return RejectPlayerAction("No trap at that location.");
        }

        if (profile.ActionType == "sleep")
        {
            var hasImmuneTarget = false;
            foreach (var unit in _allUnits)
            {
                if (!IsUsableUnit(unit) || unit.IsDead
                    || !Unit.IsWithinRange(cell, unit.GetClosestCell(cell), profile.AreaRadius))
                {
                    continue;
                }

                if (!IsUndead(unit))
                {
                    return true;
                }

                hasImmuneTarget = true;
            }

            return RejectPlayerAction(hasImmuneTarget
                ? "Undead are immune to Sleep."
                : "No living targets in the Sleep area.");
        }

        if (areaAction)
        {
            return true;
        }

        if (profile.ActionType == "heal")
        {
            if (target == null || target.Team != actor.Team)
            {
                return RejectPlayerAction("Select a living ally to heal.");
            }

            return target.HitPoints < target.MaxHitPoints
                || RejectPlayerAction($"{target.UnitName} is already at full health.");
        }

        if (target == null)
        {
            // Basic melee targeting on empty floor currently doubles as a move.
            return profile.ActionType == "attack" && profile.Range == 1
                ? ValidatePlayerMovement(actor, cell, pathing: false)
                : RejectPlayerAction("No enemy at that location.");
        }

        if (target.Team == actor.Team)
        {
            return RejectPlayerAction($"{profile.ActionName} must target an enemy.");
        }

        if (profile.ActionType == "charge")
        {
            if (!TryFindChargeDestination(actor, target, profile.Range, out _, out var cellsUsed))
            {
                return RejectPlayerAction("Cannot charge - no reachable space beside the target.");
            }

            if (cellsUsed <= 0)
            {
                return RejectPlayerAction("Cannot charge an adjacent target.");
            }
        }

        return true;
    }

    private bool ValidatePlayerMovement(Unit actor, Vector2I cell, bool pathing)
    {
        if (_flowState == BattleFlowState.Combat)
        {
            if (actor.TryGetMovementPreventingStatusName(out var status))
            {
                return RejectPlayerAction($"{actor.UnitName} cannot move while {status.ToLowerInvariant()}.");
            }

            if (actor.RemainingMovement <= 0)
            {
                return RejectPlayerAction("No movement points remaining.");
            }
        }

        if (!IsInBounds(cell))
        {
            return RejectPlayerAction("Destination is unreachable.");
        }

        foreach (var occupiedCell in actor.GetOccupiedCellsAt(cell))
        {
            if (!IsInBounds(occupiedCell) || IsBlockedCell(occupiedCell))
            {
                return RejectPlayerAction("Path blocked.");
            }

            if (IsOccupied(occupiedCell, actor))
            {
                return RejectPlayerAction("That space is occupied.");
            }
        }

        if (pathing && _flowState == BattleFlowState.Combat
            && FindPath(actor, actor.GridPos, cell, actor.RemainingMovement).Count == 0)
        {
            var hasRoute = FindPath(actor, actor.GridPos, cell, Mathf.Max(_walkableCells.Count, _gridWidth * _gridHeight)).Count > 0;
            return RejectPlayerAction(hasRoute
                ? "Not enough movement points to reach that location."
                : "No path to that location.");
        }

        return true;
    }
}
