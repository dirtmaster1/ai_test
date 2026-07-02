using Godot;
using System.IO;

public partial class StartScreen : Control
{
    private const string SaveFilePath = "user://dark_dungeon_tactics_save.json";
    private const string MainScenePath = "res://scenes/Main.tscn";

    private ColorRect _backdrop;
    private PanelContainer _centerPanel;
    private Label _titleLabel;
    private Label _subtitleLabel;
    private Label _statusLabel;
    private Label _savePathLabel;
    private Label _saveExistsLabel;
    private Button _newGameButton;
    private Button _loadGameButton;
    private Button _deleteSaveButton;
    private Button _quitButton;

    public override void _Ready()
    {
        _backdrop = GetNode<ColorRect>("Backdrop");
        _centerPanel = GetNode<PanelContainer>("CenterPanel");
        _titleLabel = GetNode<Label>("CenterPanel/VBox/Title");
        _subtitleLabel = GetNode<Label>("CenterPanel/VBox/Subtitle");
        _statusLabel = GetNode<Label>("CenterPanel/VBox/StatusLabel");
        _savePathLabel = GetNode<Label>("CenterPanel/VBox/SavePathLabel");
        _saveExistsLabel = GetNode<Label>("CenterPanel/VBox/SaveExistsLabel");
        _newGameButton = GetNode<Button>("CenterPanel/VBox/Buttons/NewGameButton");
        _loadGameButton = GetNode<Button>("CenterPanel/VBox/Buttons/LoadGameButton");
        _deleteSaveButton = GetNode<Button>("CenterPanel/VBox/Buttons/DeleteSaveButton");
        _quitButton = GetNode<Button>("CenterPanel/VBox/Buttons/QuitButton");

        ApplyFantasyMenuStyling();

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

    private void ApplyFantasyMenuStyling()
    {
        if (_backdrop != null)
        {
            _backdrop.Color = new Color(0.05f, 0.08f, 0.11f, 1.0f);
        }

        if (_centerPanel != null)
        {
            _centerPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.1f, 0.13f, 0.16f, 0.95f),
                BorderColor = new Color(0.34f, 0.47f, 0.58f, 0.95f),
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomRight = 4,
                CornerRadiusBottomLeft = 4,
                ContentMarginTop = 14,
                ContentMarginRight = 16,
                ContentMarginBottom = 14,
                ContentMarginLeft = 16,
                ShadowColor = new Color(0.01f, 0.02f, 0.03f, 0.35f),
                ShadowSize = 2,
                AntiAliasing = true
            });
        }

        StyleLabel(_titleLabel, new Color(0.9f, 0.96f, 1.0f, 1.0f), 30, false);
        StyleLabel(_subtitleLabel, new Color(0.77f, 0.85f, 0.92f, 1.0f), 16, false);
        StyleLabel(_saveExistsLabel, new Color(0.82f, 0.88f, 0.93f, 1.0f), 14, false);
        StyleLabel(_savePathLabel, new Color(0.64f, 0.72f, 0.79f, 1.0f), 13, false);
        StyleLabel(_statusLabel, new Color(0.86f, 0.93f, 0.99f, 1.0f), 14, false);

        StyleMenuButton(_newGameButton, true);
        StyleMenuButton(_loadGameButton, true);
        StyleMenuButton(_deleteSaveButton, false);
        StyleMenuButton(_quitButton, false);
    }

    private static void StyleLabel(Label label, Color color, int fontSize, bool centeredShadow)
    {
        if (label == null)
        {
            return;
        }

        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.35f));
        label.AddThemeConstantOverride("shadow_offset_x", centeredShadow ? 1 : 1);
        label.AddThemeConstantOverride("shadow_offset_y", centeredShadow ? 1 : 1);
        label.AddThemeFontSizeOverride("font_size", fontSize);
    }

    private static void StyleMenuButton(Button button, bool emphasized)
    {
        if (button == null)
        {
            return;
        }

        var borderColor = emphasized ? new Color(0.45f, 0.66f, 0.8f, 0.95f) : new Color(0.34f, 0.47f, 0.58f, 0.9f);
        var normalBg = emphasized ? new Color(0.17f, 0.27f, 0.34f, 0.98f) : new Color(0.14f, 0.17f, 0.21f, 0.96f);
        var hoverBg = emphasized ? new Color(0.21f, 0.33f, 0.42f, 1.0f) : new Color(0.18f, 0.22f, 0.27f, 1.0f);
        var pressedBg = emphasized ? new Color(0.13f, 0.21f, 0.27f, 1.0f) : new Color(0.11f, 0.14f, 0.17f, 1.0f);

        button.AddThemeStyleboxOverride("normal", CreateMenuButtonStyle(normalBg, borderColor));
        button.AddThemeStyleboxOverride("hover", CreateMenuButtonStyle(hoverBg, borderColor));
        button.AddThemeStyleboxOverride("pressed", CreateMenuButtonStyle(pressedBg, borderColor));
        button.AddThemeStyleboxOverride("focus", CreateMenuButtonStyle(hoverBg, new Color(0.62f, 0.82f, 0.97f, 1.0f)));
        button.AddThemeStyleboxOverride("disabled", CreateMenuButtonStyle(new Color(0.1f, 0.12f, 0.15f, 0.82f), new Color(0.26f, 0.32f, 0.38f, 0.85f)));

        button.AddThemeColorOverride("font_color", new Color(0.89f, 0.94f, 0.98f, 1.0f));
        button.AddThemeColorOverride("font_hover_color", new Color(0.94f, 0.98f, 1.0f, 1.0f));
        button.AddThemeColorOverride("font_pressed_color", new Color(0.85f, 0.92f, 0.97f, 1.0f));
        button.AddThemeColorOverride("font_disabled_color", new Color(0.48f, 0.54f, 0.6f, 1.0f));
        button.AddThemeFontSizeOverride("font_size", 16);
    }

    private static StyleBoxFlat CreateMenuButtonStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusBottomLeft = 3,
            ContentMarginTop = 6,
            ContentMarginRight = 10,
            ContentMarginBottom = 6,
            ContentMarginLeft = 10,
            AntiAliasing = true
        };
    }
}
