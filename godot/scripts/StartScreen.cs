using Godot;
using System.IO;

public partial class StartScreen : Control
{
    private const string SaveFilePath = "user://dark_dungeon_tactics_save.json";
    private const string MainScenePath = "res://scenes/Main.tscn";

    private PanelContainer _centerPanel;
    private Label _titleLabel;
    private Label _subtitleLabel;
    private Label _statusLabel;
    private Label _savePathLabel;
    private Label _saveExistsLabel;
    private Button _newGameButton;
    private Button _loadGameButton;
    private Button _deleteSaveButton;
    private Button _settingsButton;
    private Button _quitButton;
    private PanelContainer _settingsPanel;
    private Button _soundFxButton;
    private Button _settingsBackButton;
    private PanelContainer _soundFxPanel;
    private Label _backgroundVolumeValue;
    private HSlider _backgroundVolumeSlider;
    private Button _soundFxBackButton;
    private AudioManager _audioManager;

    public override void _Ready()
    {
        _centerPanel = GetNode<PanelContainer>("CenterPanel");
        _titleLabel = GetNode<Label>("CenterPanel/VBox/Title");
        _subtitleLabel = GetNode<Label>("CenterPanel/VBox/Subtitle");
        _statusLabel = GetNode<Label>("CenterPanel/VBox/StatusLabel");
        _savePathLabel = GetNode<Label>("CenterPanel/VBox/SavePathLabel");
        _saveExistsLabel = GetNode<Label>("CenterPanel/VBox/SaveExistsLabel");
        _newGameButton = GetNode<Button>("CenterPanel/VBox/Buttons/NewGameButton");
        _loadGameButton = GetNode<Button>("CenterPanel/VBox/Buttons/LoadGameButton");
        _deleteSaveButton = GetNode<Button>("CenterPanel/VBox/Buttons/DeleteSaveButton");
        _settingsButton = GetNode<Button>("CenterPanel/VBox/Buttons/SettingsButton");
        _quitButton = GetNode<Button>("CenterPanel/VBox/Buttons/QuitButton");
        _settingsPanel = GetNode<PanelContainer>("SettingsPanel");
        _soundFxButton = GetNode<Button>("SettingsPanel/SettingsVBox/SettingsButtons/SoundFxButton");
        _settingsBackButton = GetNode<Button>("SettingsPanel/SettingsVBox/SettingsButtons/SettingsBackButton");
        _soundFxPanel = GetNode<PanelContainer>("SoundFxPanel");
        _backgroundVolumeValue = GetNode<Label>("SoundFxPanel/SoundFxVBox/BackgroundVolumeRow/BackgroundVolumeValue");
        _backgroundVolumeSlider = GetNode<HSlider>("SoundFxPanel/SoundFxVBox/BackgroundVolumeSlider");
        _soundFxBackButton = GetNode<Button>("SoundFxPanel/SoundFxVBox/SoundFxBackButton");
        _audioManager = GetNode<AudioManager>("/root/AudioManager");

        ApplyFantasyMenuStyling();

        _newGameButton.Pressed += OnNewGamePressed;
        _loadGameButton.Pressed += OnLoadGamePressed;
        _deleteSaveButton.Pressed += OnDeleteSavePressed;
        _settingsButton.Pressed += OnSettingsPressed;
        _quitButton.Pressed += OnQuitPressed;
        _soundFxButton.Pressed += OnSoundFxPressed;
        _settingsBackButton.Pressed += OnSettingsBackPressed;
        _soundFxBackButton.Pressed += OnSoundFxBackPressed;
        _backgroundVolumeSlider.ValueChanged += OnBackgroundVolumeChanged;

        _savePathLabel.Text = $"Save path: {ProjectSettings.GlobalizePath(SaveFilePath)}";
        _backgroundVolumeSlider.Value = _audioManager.BackgroundMusicVolume * 100.0f;
        UpdateBackgroundVolumeLabel(_backgroundVolumeSlider.Value);
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

    private void OnSettingsPressed()
    {
        _centerPanel.Visible = false;
        _settingsPanel.Visible = true;
    }

    private void OnSoundFxPressed()
    {
        _settingsPanel.Visible = false;
        _soundFxPanel.Visible = true;
    }

    private void OnSettingsBackPressed()
    {
        _settingsPanel.Visible = false;
        _centerPanel.Visible = true;
    }

    private void OnSoundFxBackPressed()
    {
        _soundFxPanel.Visible = false;
        _settingsPanel.Visible = true;
    }

    private void OnBackgroundVolumeChanged(double value)
    {
        var volume = Mathf.Clamp((float)value / 100.0f, 0.0f, 1.0f);
        UpdateBackgroundVolumeLabel(value);
        _audioManager.SetBackgroundMusicVolume(volume);
    }

    private void UpdateBackgroundVolumeLabel(double value)
    {
        _backgroundVolumeValue.Text = $"{Mathf.RoundToInt((float)value)}%";
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

    private void ApplyFantasyMenuStyling()
    {
        _centerPanel.AddThemeStyleboxOverride("panel", TacticalTheme.CreateFramePanel(22));
        TacticalTheme.ApplyLabel(_titleLabel, TacticalTheme.BrassBright, 32);
        TacticalTheme.ApplyLabel(_subtitleLabel, TacticalTheme.Parchment, 16);
        TacticalTheme.ApplyLabel(_saveExistsLabel, TacticalTheme.Parchment, 14);
        TacticalTheme.ApplyLabel(_savePathLabel, TacticalTheme.ParchmentMuted, 12);
        TacticalTheme.ApplyLabel(_statusLabel, TacticalTheme.BrassBright, 14);

        TacticalTheme.ApplyButton(_newGameButton, true, 16);
        TacticalTheme.ApplyButton(_loadGameButton, true, 16);
        TacticalTheme.ApplyButton(_deleteSaveButton, false, 16);
        TacticalTheme.ApplyButton(_settingsButton, false, 16);
        TacticalTheme.ApplyButton(_quitButton, false, 16);
        _settingsPanel.AddThemeStyleboxOverride("panel", TacticalTheme.CreateFramePanel(22));
        _soundFxPanel.AddThemeStyleboxOverride("panel", TacticalTheme.CreateFramePanel(22));
        var settingsTitle = GetNode<Label>("SettingsPanel/SettingsVBox/SettingsTitle");
        var soundFxTitle = GetNode<Label>("SoundFxPanel/SoundFxVBox/SoundFxTitle");
        var backgroundVolumeLabel = GetNode<Label>("SoundFxPanel/SoundFxVBox/BackgroundVolumeRow/BackgroundVolumeLabel");
        TacticalTheme.ApplyLabel(settingsTitle, TacticalTheme.BrassBright, 24);
        TacticalTheme.ApplyLabel(soundFxTitle, TacticalTheme.BrassBright, 24);
        TacticalTheme.ApplyLabel(backgroundVolumeLabel, TacticalTheme.Parchment, 16);
        TacticalTheme.ApplyLabel(_backgroundVolumeValue, TacticalTheme.BrassBright, 16);
        TacticalTheme.ApplyButton(_soundFxButton, true, 16);
        TacticalTheme.ApplyButton(_settingsBackButton, false, 16);
        TacticalTheme.ApplyButton(_soundFxBackButton, false, 16);
        _backgroundVolumeSlider.AddThemeStyleboxOverride("slider", new StyleBoxFlat
        {
            BgColor = TacticalTheme.IronInset,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusBottomLeft = 3
        });
        _backgroundVolumeSlider.AddThemeStyleboxOverride("grabber_area", new StyleBoxFlat
        {
            BgColor = TacticalTheme.Brass,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusBottomLeft = 3
        });
        _backgroundVolumeSlider.AddThemeStyleboxOverride("grabber_area_highlight", new StyleBoxFlat
        {
            BgColor = TacticalTheme.BrassBright,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusBottomLeft = 3
        });
    }
}
