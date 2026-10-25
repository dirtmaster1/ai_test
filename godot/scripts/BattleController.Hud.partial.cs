public partial class BattleController
{
    // Architecture: HUD synchronization only (game state -> UI projection).
    private void SyncHudFromGameState()
    {
        if (_hud == null)
        {
            return;
        }

        _hud.SetHelpText(BuildCurrentTargetingHelpText());

        var active = _turnManager?.GetActiveUnit();
        _hud.SetTurnOrder(BuildTurnOrderForHud(), active);
        _hud.SetPartyList(
            _playerUnits,
            _selectedCharacterUnitId,
            _flowState == BattleFlowState.Exploration,
            BuildPendingLevelUpUnitIds(),
            BuildPartyTargetableUnitIds());

        if (GetSelectedCharacterUnit() == null)
        {
            var fallbackCharacterUnit = _flowState == BattleFlowState.Combat
                ? active
                : GetExplorerUnit();

            if (fallbackCharacterUnit != null)
            {
                _selectedCharacterUnitId = fallbackCharacterUnit.UnitId;
            }
        }

        var activePlayer = GetActivePlayerUnit();
        var mainActionEnabled = _flowState == BattleFlowState.Combat && activePlayer != null && activePlayer.CanUseAbilityThisTurn();
        var actionBarUnit = _flowState == BattleFlowState.Combat
            ? activePlayer
            : _flowState == BattleFlowState.Exploration
                ? GetSelectedCharacterPartyUnit() ?? GetExplorerUnit()
                : null;
        _hud.SetMovementCounter(actionBarUnit);
        var explorationOnly = _flowState == BattleFlowState.Exploration;
        var actionBarAbilities = BuildAbilityEntriesForHud(actionBarUnit, explorationOnly);
        var abilityPanelEnabled = _flowState == BattleFlowState.Combat
            ? activePlayer != null
            : explorationOnly && actionBarAbilities.Count > 0;
        _hud.SetActionButtonsEnabled(abilityPanelEnabled, _flowState == BattleFlowState.Combat);
        _hud.SetAbilityButtons(actionBarAbilities, abilityPanelEnabled);
        var canUseConsumables = actionBarUnit != null
            && !actionBarUnit.IsDead
            && actionBarUnit.Team == "player"
            && (_flowState == BattleFlowState.Exploration
                || _flowState == BattleFlowState.Combat
                    && IsCurrentActiveUnit(actionBarUnit)
                    && actionBarUnit.CanUseAbilityThisTurn());
        _hud.SetConsumableButtons(BuildConsumableEntriesForHud(actionBarUnit), canUseConsumables);
        _hud.SetInventoryGold(_partyGold);

        var inventoryTarget = GetInventoryTargetUnit();
        if (inventoryTarget != null)
        {
            _hud.SetInventoryUnitName(inventoryTarget.UnitName);
            _hud.SetInventoryCharacterSummary(
                _hud.BuildCharacterSummary(
                    inventoryTarget,
                    GetActionDisplayName(GetSelectedAbilityId(inventoryTarget)),
                    GetActionDisplayName(inventoryTarget.PrimaryAbilityId),
                    includeActionNames: false
                )
            );
            _hud.SetInventoryAbilities(BuildAbilityEntriesForHud(inventoryTarget));
            _hud.SetInventoryEquippedItems(BuildInventoryEquippedEntries(inventoryTarget));
            _hud.SetInventoryItems(BuildInventoryItemsForHud(), GetEquippedItemIds(inventoryTarget));
        }
        else
        {
            _hud.SetInventoryEquippedItems(new Godot.Collections.Array<Godot.Collections.Dictionary>());
        }

        if (_flowState == BattleFlowState.Exploration)
        {
            var explorer = GetExplorerUnit();
            _hud.SetLootEntries(BuildNearbyLootEntries(explorer));
            _hud.SetReserveEntries(BuildActivePartyReserveEntriesForHud(), BuildReserveRosterEntriesForHud());
        }
        else
        {
            _hud.SetLootPanelVisible(false);
            _hud.SetReservePanelVisible(false);
        }
    }

    private string BuildCurrentTargetingHelpText()
    {
        if (_awaitingHealerServiceTarget)
        {
            var service = _gameData?.GetHealingService(_selectedHealerServiceId) ?? new Godot.Collections.Dictionary();
            if (GetString(service, "type", "") == "resurrect")
            {
                return "Resurrection: select a highlighted fallen party portrait to revive them. No range limit. Press Esc or right-click to cancel.";
            }

            var serviceRange = GetInt(service, "range", 5);
            return serviceRange < 0
                ? "Choose a fallen party member from the party list. Press Esc or right-click to cancel."
                : $"Choose a party member within {serviceRange} tiles of Eldius. Press Esc or right-click to cancel.";
        }

        if (_flowState == BattleFlowState.Combat && _awaitingPlayerAttackDirection)
        {
            var activePlayer = GetActivePlayerUnit();
            if (activePlayer != null)
            {
                var profile = ResolveActionProfile(activePlayer, GetSelectedAbilityId(activePlayer));
                if (IsHealingSpellAction(profile.ActionType))
                {
                    return BuildHealingSpellTargetHelp(profile);
                }
            }
        }

        return _hud.BuildHelpText(_flowState.ToString());
    }

    private Godot.Collections.Array<string> BuildPartyTargetableUnitIds()
    {
        var actionType = "";
        if (_awaitingHealerServiceTarget)
        {
            var service = _gameData?.GetHealingService(_selectedHealerServiceId) ?? new Godot.Collections.Dictionary();
            actionType = GetString(service, "type", "");
        }
        else if (_flowState == BattleFlowState.Combat && _awaitingPlayerAttackDirection)
        {
            var activePlayer = GetActivePlayerUnit();
            if (activePlayer != null)
            {
                actionType = ResolveActionProfile(activePlayer, GetSelectedAbilityId(activePlayer)).ActionType;
            }
        }

        var targetableUnitIds = new Godot.Collections.Array<string>();
        if (actionType != "resurrect")
        {
            return targetableUnitIds;
        }

        foreach (var unit in _playerUnits)
        {
            if (IsUsableUnit(unit) && unit.IsDead)
            {
                targetableUnitIds.Add(unit.UnitId);
            }
        }

        return targetableUnitIds;
    }

    private Godot.Collections.Array<string> BuildPendingLevelUpUnitIds()
    {
        var unitIds = new Godot.Collections.Array<string>();
        foreach (var unit in _playerUnits)
        {
            if (IsUsableUnit(unit) && unit.UnallocatedStatPoints > 0)
            {
                unitIds.Add(unit.UnitId);
            }
        }

        return unitIds;
    }

    private void SetStatusHelp()
    {
        if (_flowState == BattleFlowState.Exploration)
        {
            var explorer = GetExplorerUnit();
            if (explorer == null)
            {
                return;
            }
            SyncHudFromGameState();
            return;
        }

        if (_flowState == BattleFlowState.Defeat)
        {
            SyncHudFromGameState();
            return;
        }

        var active = _turnManager.GetActiveUnit();
        if (active == null)
        {
            return;
        }

        SyncHudFromGameState();
    }
}
