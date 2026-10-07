public partial class BattleController
{
    // Architecture: HUD synchronization only (game state -> UI projection).
    private void SyncHudFromGameState()
    {
        if (_hud == null)
        {
            return;
        }

        _hud.SetHelpText(_hud.BuildHelpText(_flowState.ToString()));

        var active = _turnManager?.GetActiveUnit();
        _hud.SetTurnOrder(BuildTurnOrderForHud(), active);
        _hud.SetPartyList(_playerUnits, _selectedCharacterUnitId, _flowState == BattleFlowState.Exploration, BuildPendingLevelUpUnitIds());

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

    private Godot.Collections.Array<string> BuildPendingLevelUpUnitIds()
    {
        var unitIds = new Godot.Collections.Array<string>();
        foreach (var unitId in _pendingLevelUpNoticesByUnitId.Keys)
        {
            unitIds.Add(unitId);
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
