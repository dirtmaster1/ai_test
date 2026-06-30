using Godot;
using System.IO;

public partial class StartScreen : Control
{
    private const string SaveFilePath = "user://dark_dungeon_tactics_save.json";
    private const string MainScenePath = "res://scenes/Main.tscn";

    private Label _statusLabel;
    private Label _savePathLabel;
    private Label _saveExistsLabel;
    private Button _newGameButton;
    private Button _loadGameButton;
    private Button _deleteSaveButton;
    private Button _quitButton;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("CenterPanel/VBox/StatusLabel");
        _savePathLabel = GetNode<Label>("CenterPanel/VBox/SavePathLabel");
        _saveExistsLabel = GetNode<Label>("CenterPanel/VBox/SaveExistsLabel");
        _newGameButton = GetNode<Button>("CenterPanel/VBox/Buttons/NewGameButton");
        _loadGameButton = GetNode<Button>("CenterPanel/VBox/Buttons/LoadGameButton");
        _deleteSaveButton = GetNode<Button>("CenterPanel/VBox/Buttons/DeleteSaveButton");
        _quitButton = GetNode<Button>("CenterPanel/VBox/Buttons/QuitButton");

        _newGameButton.Pressed += OnNewGamePressed;
        _loadGameButton.Pressed += OnLoadGamePressed;
        _deleteSaveButton.Pressed += OnDeleteSavePressed;
        _quitButton.Pressed += OnQuitPressed;

        _savePathLabel.Text = $"Save path: {ProjectSettings.GlobalizePath(SaveFilePath)}";
        RefreshSaveState();
    }

    private void OnNewGamePressed()
    {
        TryDeleteSaveSilently();
        GameLaunchOptions.Mode = GameLaunchMode.NewGame;
        GetTree().ChangeSceneToFile(MainScenePath);
    }

    private void OnLoadGamePressed()
    {
        GameLaunchOptions.Mode = GameLaunchMode.LoadGame;
        GetTree().ChangeSceneToFile(MainScenePath);
    }

    private void OnDeleteSavePressed()
    {
        if (!Godot.FileAccess.FileExists(SaveFilePath))
        {
            _statusLabel.Text = "No save file found.";
            RefreshSaveState();
            return;
        }

        var absolutePath = ProjectSettings.GlobalizePath(SaveFilePath);
        try
        {
            File.Delete(absolutePath);
            _statusLabel.Text = "Save deleted.";
        }
        catch (IOException ex)
        {
            _statusLabel.Text = $"Failed to delete save: {ex.Message}";
        }

        RefreshSaveState();
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    private void TryDeleteSaveSilently()
    {
        if (!Godot.FileAccess.FileExists(SaveFilePath))
        {
            return;
        }

        var absolutePath = ProjectSettings.GlobalizePath(SaveFilePath);
        try
        {
            File.Delete(absolutePath);
        }
        catch
        {
            // Best effort; if delete fails, game still starts and can overwrite later.
        }
    }

    private void RefreshSaveState()
    {
        var hasSave = Godot.FileAccess.FileExists(SaveFilePath);
        _saveExistsLabel.Text = hasSave ? "Save status: found" : "Save status: none";
        _loadGameButton.Disabled = !hasSave;
        _deleteSaveButton.Disabled = !hasSave;
    }
}
