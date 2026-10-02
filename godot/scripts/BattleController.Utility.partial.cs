using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BattleController
{
    private bool _utilityConfirmationPending;

    private static bool IsUtilityAction(string actionType) => actionType is "disarm_trap" or "pick_lock";

    private static bool IsExplorationAction(string actionType) => actionType == "heal" || IsUtilityAction(actionType);

    private void EnsureClassUtilityAbilities(Unit unit)
    {
        if (_gameData == null || unit.Team != "player")
        {
            return;
        }

        var template = _gameData.GetCharacterTemplate(unit.ClassId);
        foreach (var abilityId in TryGetStringArray(template, "ability_ids"))
        {
            if (IsUtilityAction(GetString(_gameData.GetAbility(abilityId), "type", "")))
            {
                unit.LearnAbility(abilityId);
            }
        }
    }

    private bool IsDoorLocked(Dictionary door)
    {
        if (door == null || !GetBool(door, "locked", false))
        {
            return false;
        }

        return !_unlockedDoorIdsByMap.TryGetValue(_currentMapId, out var unlockedDoors)
            || !unlockedDoors.Contains(GetString(door, "id", ""));
    }

    private static bool TryUnlockDoorWithKey(Unit actor, Dictionary door) => false;

    private bool TryGetUtilityTarget(Unit actor, ActionProfile profile, Vector2I cell, out Dictionary target)
    {
        target = null;
        if (!IsUsableUnit(actor) || actor.IsDead || actor.Team != "player"
            || !actor.HasAbility(profile.ActionId) || !IsUtilityAction(profile.ActionType)
            || Manhattan(actor.GridPos, cell) != 1 || !IsFogCellCurrentlyVisible(cell)
            || !HasClearLineOfSight(actor.GridPos, cell))
        {
            return false;
        }

        if (profile.ActionType == "pick_lock")
        {
            return TryGetDoorAtCell(cell, out target) && IsDoorLocked(target);
        }

        foreach (var prop in _mapProps)
        {
            var propId = GetString(prop, "id", "");
            if (GetString(prop, "type", "") == "trap"
                && GetVector2I(prop, "grid_pos", new Vector2I(-9999, -9999)) == cell
                && !string.IsNullOrEmpty(propId) && !_openedPropIds.Contains(propId))
            {
                target = prop;
                return true;
            }
        }

        return false;
    }

    private bool CanUseUtilityAction(Unit actor, ActionProfile profile)
    {
        if (!IsUsableUnit(actor) || actor.IsDead || !_playerUnits.Contains(actor)
            || actor.GetAbilityCooldownRemaining(profile.ActionId) > 0 || !CanCastAction(actor, profile))
        {
            return false;
        }

        return _flowState == BattleFlowState.Exploration
            ? !_isExplorationAutoMoving
            : _flowState == BattleFlowState.Combat && GetActivePlayerUnit() == actor && CanUseActionProfileNow(actor, profile);
    }

    private async void BeginUtilityAction(Unit actor, ActionProfile profile, Vector2I cell)
    {
        if (_utilityConfirmationPending || !CanUseUtilityAction(actor, profile)
            || !TryGetUtilityTarget(actor, profile, cell, out var target))
        {
            return;
        }

        var mapId = _currentMapId;
        var flowState = _flowState;
        _utilityConfirmationPending = true;
        CancelAttackMode(false);
        try
        {
            var confirmed = await ConfirmUtilityActionAsync(profile, target);
            if (confirmed && mapId == _currentMapId && flowState == _flowState)
            {
                TryApplyUtilityAction(actor, profile, cell);
            }
        }
        finally
        {
            _utilityConfirmationPending = false;
            BeginPostPlayerActionMouseMoveLock();
            SyncHudFromGameState();
            QueueRedraw();
        }
    }

    private bool TryApplyUtilityAction(Unit actor, ActionProfile profile, Vector2I cell)
    {
        if (!CanUseUtilityAction(actor, profile) || !TryGetUtilityTarget(actor, profile, cell, out var target))
        {
            return false;
        }

        string message;
        if (profile.ActionType == "disarm_trap")
        {
            _openedPropIds.Add(GetString(target, "id", ""));
            message = "Trap disarmed!";
        }
        else
        {
            if (!_unlockedDoorIdsByMap.TryGetValue(_currentMapId, out var unlockedDoors))
            {
                unlockedDoors = new HashSet<string>();
                _unlockedDoorIdsByMap[_currentMapId] = unlockedDoors;
            }

            unlockedDoors.Add(GetString(target, "id", ""));
            message = "Door unlocked!";
        }

        if (_flowState == BattleFlowState.Combat && !profile.IgnoresActionCost)
        {
            actor.MarkAbilityUsed(profile.ActionId, profile.CooldownTurns);
        }
        else
        {
            actor.MarkAbilityCooldownOnly(profile.ActionId, profile.CooldownTurns);
        }

        _hud?.ShowCombatBanner(message, new Color(0.3f, 1.0f, 0.45f));
        _hud?.AddCombatLogEntry($"{actor.UnitName}: {message}");
        SaveMapInteractionStateForCurrentMap();
        _persistence?.PersistSaveGame(false);
        SetStatusHelp();
        SyncHudFromGameState();
        QueueRedraw();
        return true;
    }

    private async Task<bool> ConfirmUtilityActionAsync(ActionProfile profile, Dictionary target)
    {
        var disarming = profile.ActionType == "disarm_trap";
        var dialog = new ConfirmationDialog
        {
            Title = profile.ActionName,
            DialogText = disarming ? $"Disarm {GetString(target, "name", "this trap")}?" : "Pick this door's lock?",
            Exclusive = true
        };
        dialog.GetOkButton().Text = disarming ? "Disarm" : "Pick Lock";
        dialog.GetCancelButton().Text = "Not now";
        AddChild(dialog);
        TacticalTheme.ApplyDialog(dialog);

        var completion = new TaskCompletionSource<bool>();
        void HandleConfirmed() => completion.TrySetResult(true);
        void HandleCanceled() => completion.TrySetResult(false);
        dialog.Confirmed += HandleConfirmed;
        dialog.Canceled += HandleCanceled;
        dialog.CloseRequested += HandleCanceled;
        dialog.TreeExiting += HandleCanceled;
        try
        {
            dialog.PopupCentered(new Vector2I(560, 220));
            return await completion.Task;
        }
        finally
        {
            dialog.Confirmed -= HandleConfirmed;
            dialog.Canceled -= HandleCanceled;
            dialog.CloseRequested -= HandleCanceled;
            dialog.TreeExiting -= HandleCanceled;
            dialog.Exclusive = false;
            dialog.QueueFree();
        }
    }
}