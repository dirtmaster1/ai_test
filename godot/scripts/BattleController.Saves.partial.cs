using Godot;

public partial class BattleController
{
    private const string MainScenePath = "res://scenes/Main.tscn";
    private SaveLoadMenu _saveLoadMenu;
    private bool _skipExitSave;

    private void OnHudSaveRequested() => OpenSaveLoadMenu(true);
    private void OnHudLoadRequested() => OpenSaveLoadMenu(false);

    private void OnHudMainMenuRequested()
    {
        var error = GetTree().ChangeSceneToFile(StartScreenPath);
        if (error != Error.Ok)
        {
            GD.PushError($"Could not return to the main menu: {error}.");
            RejectPlayerAction("Could not return to the main menu.");
        }
    }

    private void OpenSaveLoadMenu(bool saveMode)
    {
        if (IsInstanceValid(_saveLoadMenu) && !_saveLoadMenu.IsQueuedForDeletion())
            return;
        if (_isExplorationAutoMoving || _isPlayerMovementAnimating || _isEnemyTurnProcessing
            || _isEndingTurn || _utilityConfirmationPending || _flowState == BattleFlowState.CombatIntro)
        {
            RejectPlayerAction("Wait for the current action to finish before saving or loading.");
            return;
        }

        var slots = new SaveGameSlots();
        _saveLoadMenu = new SaveLoadMenu();
        _saveLoadMenu.Configure(saveMode, slots);
        _saveLoadMenu.SaveSelected += (slot, name, overwrite) =>
        {
            if (!slots.TrySave(this, slot, name, overwrite, out var error))
            {
                _saveLoadMenu.ShowError(error);
                return;
            }
            _saveLoadMenu.Close();
            _hud?.ShowCombatBanner("GAME SAVED", TacticalTheme.BrassBright);
        };
        _saveLoadMenu.LoadSelected += path =>
        {
            if (!SaveGameSlots.TryRead(path, out _, out var readError))
            {
                _saveLoadMenu.ShowError(readError);
                return;
            }
            GameLaunchOptions.Mode = GameLaunchMode.LoadGame;
            GameLaunchOptions.SavePath = path;
            // Loading an autosave must not overwrite it with the outgoing adventure.
            _skipExitSave = true;
            var error = GetTree().ChangeSceneToFile(MainScenePath);
            if (error != Error.Ok)
            {
                _skipExitSave = false;
                GameLaunchOptions.Mode = GameLaunchMode.Auto;
                _saveLoadMenu.ShowError($"Could not load the game scene: {error}.");
                GD.PushError($"Could not load the game scene: {error}.");
                return;
            }
            _saveLoadMenu.Close();
        };
        AddChild(_saveLoadMenu);
    }
}
