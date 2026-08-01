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
        _hud.SetActiveUnit(_flowState == BattleFlowState.Combat ? active : null);
        _hud.SetPartyList(_playerUnits, _selectedCharacterUnitId, _flowState == BattleFlowState.Exploration);

        var characterUnit = GetSelectedCharacterUnit();
        if (characterUnit == null)
        {
            characterUnit = _flowState == BattleFlowState.Combat
                ? active
                : GetExplorerUnit();

            if (characterUnit != null)
            {
                _selectedCharacterUnitId = characterUnit.UnitId;
            }
        }

        _hud.SetCharacterSummary(
            _hud.BuildCharacterSummary(
                characterUnit,
                characterUnit == null ? "" : GetActionDisplayName(GetSelectedAbilityId(characterUnit)),
                characterUnit == null ? "" : GetActionDisplayName(characterUnit.PrimaryAbilityId)
            )
        );
        _hud.SetCharacterStatusSummary(_hud.BuildCharacterStatusSummary(characterUnit));

        var activePlayer = GetActivePlayerUnit();
        var mainActionEnabled = _flowState == BattleFlowState.Combat && activePlayer != null && activePlayer.CanUseAbilityThisTurn();
        var abilityPanelEnabled = _flowState == BattleFlowState.Combat && activePlayer != null;
        _hud.SetActionButtonsEnabled(mainActionEnabled, _flowState == BattleFlowState.Combat);
        _hud.SetAbilityButtons(BuildAbilityEntriesForHud(activePlayer), abilityPanelEnabled);
        _hud.SetInventoryGold(_partyGold);

        var inventoryTarget = GetInventoryTargetUnit();
        if (inventoryTarget != null)
        {
            _hud.SetInventoryUnitName(inventoryTarget.UnitName);
            _hud.SetInventoryEquippedSummary(BuildInventoryEquippedSummary(inventoryTarget));
            _hud.SetInventoryEquippedItems(BuildInventoryEquippedEntries(inventoryTarget));
            _hud.SetInventoryItems(BuildInventoryItemsForHud(), GetEquippedItemIds(inventoryTarget));
        }
        else
        {
            _hud.SetInventoryEquippedSummary("Equipped: none");
            _hud.SetInventoryEquippedItems(new Godot.Collections.Array<Godot.Collections.Dictionary>());
        }

        if (_flowState == BattleFlowState.Exploration)
        {
            var explorer = GetExplorerUnit();
            _hud.SetLootEntries(BuildNearbyLootEntries(explorer));
        }
        else
        {
            _hud.SetLootPanelVisible(false);
        }
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
