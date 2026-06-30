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
        _hud.SetTurnQueue(BuildTurnQueueForHud(), active);
        _hud.SetActiveUnit(_flowState == BattleFlowState.Combat ? active : null);

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

        var activePlayer = GetActivePlayerUnit();
        var abilityEnabled = _flowState == BattleFlowState.Combat && activePlayer != null && activePlayer.CanUseAbilityThisTurn();
        _hud.SetActionButtonsEnabled(abilityEnabled, _flowState == BattleFlowState.Combat);
        _hud.SetAbilityButtons(BuildAbilityEntriesForHud(activePlayer), abilityEnabled);

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
                _hud?.SetStatusText("Exploration: no living player units.");
                return;
            }

            _hud?.SetStatusText($"Map: {_currentMapId} | Exploration: {explorer.UnitName}");
            _hud?.SetActionDetails($"Exploration party leader: {explorer.UnitName}");
            _hud?.SetSelectedAction("None");
            SyncHudFromGameState();
            return;
        }

        if (_flowState == BattleFlowState.Defeat)
        {
            _hud?.SetStatusText("Defeat. All player units were defeated.");
            _hud?.SetActionDetails("Defeat state. Restart the encounter to continue.");
            _hud?.SetSelectedAction("None");
            SyncHudFromGameState();
            return;
        }

        var active = _turnManager.GetActiveUnit();
        if (active == null)
        {
            _hud?.SetStatusText("No active unit");
            return;
        }

        var selectedAbilityId = GetSelectedAbilityId(active);
        var selectedProfile = ResolveActionProfile(active, selectedAbilityId);
        var cooldownRemaining = active.GetAbilityCooldownRemaining(selectedProfile.ActionId);
        var abilityState = !active.CanUseAbilityThisTurn()
            ? "used"
            : cooldownRemaining > 0
                ? $"cooldown ({cooldownRemaining})"
                : "ready";
        var combatPrefix = string.IsNullOrEmpty(_lastActionSummary) ? "" : $"Last action: {_lastActionSummary} | ";
        _hud?.SetStatusText(
            $"{combatPrefix}Turn: {active.UnitName} ({active.Team}) | HP: {active.HitPoints}/{active.MaxHitPoints} | MP: {active.MagicPoints}/{active.MaxMagicPoints} | Move: {active.RemainingMovement}/{Unit.MaxMovementPerTurn} | Ability: {abilityState}"
        );

        _hud?.SetSelectedAction(_awaitingPlayerAttackDirection ? $"{selectedProfile.ActionId} (targeting)" : selectedProfile.ActionId);
        SyncHudFromGameState();
    }
}
