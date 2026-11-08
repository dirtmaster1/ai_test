using Godot;

public partial class GameOptionsManager : Node
{
    private const string SettingsPath = "user://dark_dungeon_tactics_game_options.cfg";
    private const string SettingsSection = "game_options";
    private const float MinimumMapBrightness = 0.5f;
    private const float MaximumMapBrightness = 1.5f;
    private bool _healthBarsEnabled;
    private int _combatMessageDurationMultiplier = 1;
    private float _mapBrightness = 1.0f;

    [Signal]
    public delegate void HealthBarsEnabledChangedEventHandler(bool enabled);

    [Signal]
    public delegate void MapBrightnessChangedEventHandler(float brightness);

    public bool HealthBarsEnabled => _healthBarsEnabled;
    public int CombatMessageDurationMultiplier => _combatMessageDurationMultiplier;
    public float MapBrightness => _mapBrightness;

    public override void _Ready()
    {
        LoadSettings();
    }

    public void SetHealthBarsEnabled(bool enabled)
    {
        if (_healthBarsEnabled == enabled)
        {
            return;
        }

        _healthBarsEnabled = enabled;
        SaveSettings();
        EmitSignal(SignalName.HealthBarsEnabledChanged, enabled);
    }

    public void SetCombatMessageDurationMultiplier(int multiplier)
    {
        var clampedMultiplier = Mathf.Clamp(multiplier, 1, 5);
        if (_combatMessageDurationMultiplier == clampedMultiplier)
        {
            return;
        }

        _combatMessageDurationMultiplier = clampedMultiplier;
        SaveSettings();
    }

    public float ScaleCombatDuration(float durationSeconds)
    {
        return durationSeconds * _combatMessageDurationMultiplier;
    }

    public void SetMapBrightness(float brightness)
    {
        var clampedBrightness = Mathf.Clamp(brightness, MinimumMapBrightness, MaximumMapBrightness);
        if (Mathf.IsEqualApprox(_mapBrightness, clampedBrightness))
        {
            return;
        }

        _mapBrightness = clampedBrightness;
        SaveSettings();
        EmitSignal(SignalName.MapBrightnessChanged, _mapBrightness);
    }

    private void LoadSettings()
    {
        if (!FileAccess.FileExists(SettingsPath))
        {
            return;
        }

        var config = new ConfigFile();
        var error = config.Load(SettingsPath);
        if (error != Error.Ok)
        {
            GD.PushError($"Could not load game options from '{SettingsPath}': {error}.");
            return;
        }

        var healthBarsValue = config.GetValue(SettingsSection, "health_bars_enabled", false);
        if (healthBarsValue.VariantType == Variant.Type.Bool)
        {
            _healthBarsEnabled = healthBarsValue.AsBool();
        }
        else
        {
            GD.PushError($"Game option 'health_bars_enabled' in '{SettingsPath}' must be a boolean.");
        }

        var durationValue = config.GetValue(SettingsSection, "combat_message_duration_multiplier", 1);
        if (durationValue.VariantType == Variant.Type.Int)
        {
            var storedMultiplier = durationValue.AsInt32();
            if (storedMultiplier is >= 1 and <= 5)
            {
                _combatMessageDurationMultiplier = storedMultiplier;
            }
            else
            {
                GD.PushError($"Game option 'combat_message_duration_multiplier' in '{SettingsPath}' must be between 1 and 5.");
            }
        }
        else
        {
            GD.PushError($"Game option 'combat_message_duration_multiplier' in '{SettingsPath}' must be an integer.");
        }

        var mapBrightnessValue = config.GetValue(SettingsSection, "map_brightness", 1.0f);
        if (mapBrightnessValue.VariantType == Variant.Type.Float)
        {
            var storedBrightness = mapBrightnessValue.AsSingle();
            if (storedBrightness >= MinimumMapBrightness && storedBrightness <= MaximumMapBrightness)
            {
                _mapBrightness = storedBrightness;
            }
            else
            {
                GD.PushError($"Game option 'map_brightness' in '{SettingsPath}' must be between {MinimumMapBrightness} and {MaximumMapBrightness}.");
            }
        }
        else
        {
            GD.PushError($"Game option 'map_brightness' in '{SettingsPath}' must be a number.");
        }
    }

    private void SaveSettings()
    {
        var config = new ConfigFile();
        config.SetValue(SettingsSection, "health_bars_enabled", _healthBarsEnabled);
        config.SetValue(SettingsSection, "combat_message_duration_multiplier", _combatMessageDurationMultiplier);
        config.SetValue(SettingsSection, "map_brightness", _mapBrightness);
        var error = config.Save(SettingsPath);
        if (error != Error.Ok)
        {
            GD.PushError($"Could not save game options to '{SettingsPath}': {error}.");
        }
    }
}
