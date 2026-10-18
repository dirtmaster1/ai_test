using Godot;
using Godot.Collections;

public partial class AudioSettingsChecks : Node
{
    public Array<string> Run()
    {
        var failures = new Array<string>();
        var audioManager = GetNode<AudioManager>("/root/AudioManager");
        var musicPlayer = audioManager.GetNode<AudioStreamPlayer>("BackgroundMusicPlayer");
        var musicBusIndex = AudioServer.GetBusIndex("BackgroundMusic");
        var hasMusicLimiter = false;
        if (musicBusIndex >= 0)
        {
            for (var effectIndex = 0; effectIndex < AudioServer.GetBusEffectCount(musicBusIndex); effectIndex++)
            {
                hasMusicLimiter |= AudioServer.GetBusEffect(musicBusIndex, effectIndex) is AudioEffectHardLimiter;
            }
        }
        if (musicPlayer.Bus != "BackgroundMusic" || !hasMusicLimiter)
        {
            failures.Add("Background music must use an amplified, limited bus to maintain loudness without clipping.");
        }

        var explorationMusic = musicPlayer.Stream as AudioStreamWav;
        if (explorationMusic == null
            || explorationMusic.LoopMode != AudioStreamWav.LoopModeEnum.Forward
            || explorationMusic.GetLength() <= 0.0
            || explorationMusic.LoopBegin != 0
            || explorationMusic.LoopEnd != Mathf.RoundToInt((float)(explorationMusic.GetLength() * explorationMusic.MixRate)))
        {
            failures.Add("The forest town music must loop through the full track duration.");
        }

        var combatMusic = GD.Load<AudioStreamWav>("res://assets/audio/combat_1.wav");
        if (combatMusic == null
            || combatMusic.LoopMode != AudioStreamWav.LoopModeEnum.Forward
            || combatMusic.LoopBegin != 0
            || combatMusic.LoopEnd != Mathf.RoundToInt((float)(combatMusic.GetLength() * combatMusic.MixRate)))
        {
            failures.Add("The combat music must loop through the full track duration.");
        }

        var eventBus = GetNode<EventBus>("/root/EventBus");
        eventBus.EmitSignal(EventBus.SignalName.CombatStarted);
        if (musicPlayer.Stream != combatMusic || !musicPlayer.Playing)
        {
            failures.Add("Combat start must switch the music player to the looping combat soundtrack.");
        }

        eventBus.EmitSignal(EventBus.SignalName.CombatEnded);
        if (musicPlayer.Stream != explorationMusic || !musicPlayer.Playing)
        {
            failures.Add("Combat end must restore the exploration soundtrack.");
        }

        var originalVolume = audioManager.BackgroundMusicVolume;
        audioManager.SetBackgroundMusicVolume(1.0f);
        if (Mathf.Abs(musicPlayer.VolumeDb - 4.0f) > 0.001f)
        {
            failures.Add("A 100% volume setting must apply the music makeup gain.");
        }

        var startScreen = GD.Load<PackedScene>("res://scenes/StartScreen.tscn").Instantiate<StartScreen>();
        AddChild(startScreen);

        var backdrop = startScreen.GetNode<TextureRect>("Backdrop");
        if (backdrop.StretchMode != TextureRect.StretchModeEnum.KeepAspectCentered)
        {
            failures.Add("Launch screen backdrop must fit the full image within the viewport.");
        }

        var centerPanel = startScreen.GetNode<PanelContainer>("CenterPanel");
        var settingsPanel = startScreen.GetNode<PanelContainer>("SettingsPanel");
        var soundFxPanel = startScreen.GetNode<PanelContainer>("SoundFxPanel");
        var centerPanelStyle = centerPanel.GetThemeStylebox("panel", "PanelContainer");
        var menuButtons = new[]
        {
            startScreen.GetNode<Button>("CenterPanel/VBox/Buttons/NewGameButton"),
            startScreen.GetNode<Button>("CenterPanel/VBox/Buttons/LoadGameButton"),
            startScreen.GetNode<Button>("CenterPanel/VBox/Buttons/DeleteSaveButton"),
            startScreen.GetNode<Button>("CenterPanel/VBox/Buttons/SettingsButton"),
            startScreen.GetNode<Button>("CenterPanel/VBox/Buttons/QuitButton"),
            startScreen.GetNode<Button>("CenterPanel/VBox/SavePathToggleButton")
        };
        var newGameButtonStyle = startScreen.GetNode<Button>("CenterPanel/VBox/Buttons/NewGameButton")
            .GetThemeStylebox("normal", "Button") as StyleBoxFlat;
        if (centerPanelStyle is not StyleBoxEmpty)
        {
            failures.Add("The launch menu background must remain transparent.");
        }

        if (centerPanel.AnchorLeft != 1.0f
            || centerPanel.AnchorTop != 1.0f
            || centerPanel.AnchorRight != 1.0f
            || centerPanel.AnchorBottom != 1.0f
            || centerPanel.OffsetRight != -24.0f
            || centerPanel.OffsetBottom != -24.0f)
        {
            failures.Add("The launch menu must be anchored at the bottom-right with a small inset.");
        }

        if (startScreen.GetNodeOrNull<Label>("CenterPanel/VBox/Subtitle") != null)
        {
            failures.Add("The launch menu subtitle must be removed.");
        }

        var expectedButtonWidth = 0.0f;
        foreach (var button in menuButtons)
        {
            expectedButtonWidth = Mathf.Max(expectedButtonWidth, button.GetCombinedMinimumSize().X);
        }

        foreach (var button in menuButtons)
        {
            if (!Mathf.IsEqualApprox(button.CustomMinimumSize.X, expectedButtonWidth)
                || button.SizeFlagsHorizontal != Control.SizeFlags.ShrinkCenter)
            {
                failures.Add("Launch menu buttons must share the width of the longest button text.");
                break;
            }
        }

        if (newGameButtonStyle == null || newGameButtonStyle.BgColor.A <= 0.0f)
        {
            failures.Add("Launch menu buttons must retain their styled backgrounds.");
        }

        var versionLabel = startScreen.GetNode<Label>("CenterPanel/VBox/Buttons/VersionLabel");
        if (versionLabel.Text != "Version 0.1.0" || versionLabel.GetThemeFontSize("font_size") > 12)
        {
            failures.Add("The launch menu must show the current version beneath Quit in small text.");
        }

        var savePathLabel = startScreen.GetNode<Label>("CenterPanel/VBox/SavePathLabel");
        var savePathToggleButton = startScreen.GetNode<Button>("CenterPanel/VBox/SavePathToggleButton");
        if (savePathLabel.Visible || savePathToggleButton.Text != "Show Save Path")
        {
            failures.Add("The full save path must be hidden initially with an option to show it.");
        }

        savePathToggleButton.EmitSignal(Button.SignalName.Pressed);
        if (!savePathLabel.Visible || savePathToggleButton.Text != "Hide Save Path")
        {
            failures.Add("The save path toggle must show the full path when requested.");
        }

        savePathToggleButton.EmitSignal(Button.SignalName.Pressed);
        if (savePathLabel.Visible || savePathToggleButton.Text != "Show Save Path")
        {
            failures.Add("The save path toggle must hide the full path again when requested.");
        }

        startScreen.GetNode<Button>("CenterPanel/VBox/Buttons/SettingsButton").EmitSignal(Button.SignalName.Pressed);
        if (centerPanel.Visible || !settingsPanel.Visible)
        {
            failures.Add("The Settings button must open the settings menu.");
        }

        settingsPanel.GetNode<Button>("SettingsVBox/SettingsButtons/SoundFxButton").EmitSignal(Button.SignalName.Pressed);
        if (settingsPanel.Visible || !soundFxPanel.Visible)
        {
            failures.Add("Sound FX must be the first settings option and open its submenu.");
        }

        var slider = soundFxPanel.GetNode<HSlider>("SoundFxVBox/BackgroundVolumeSlider");
        var valueLabel = soundFxPanel.GetNode<Label>("SoundFxVBox/BackgroundVolumeRow/BackgroundVolumeValue");
        slider.Value = 0.0;
        if (!musicPlayer.StreamPaused
            || audioManager.BackgroundMusicVolume != 0.0f
            || valueLabel.Text != "0%")
        {
            failures.Add("A zero background volume setting must silence the music and display 0%.");
        }

        slider.Value = 37.0;
        if (musicPlayer.StreamPaused
            || Mathf.Abs(audioManager.BackgroundMusicVolume - 0.37f) > 0.001f
            || valueLabel.Text != "37%")
        {
            failures.Add("The background volume slider must update the music level and percentage live.");
        }

        audioManager.SetBackgroundMusicVolume(originalVolume);
        startScreen.Free();
        return failures;
    }
}
