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

        if (musicPlayer.Stream is not AudioStreamWav music
            || music.LoopMode != AudioStreamWav.LoopModeEnum.Forward
            || Mathf.Abs((float)music.GetLength() - 180.0f) > 5.0f
            || music.LoopEnd != Mathf.RoundToInt((float)(music.GetLength() * music.MixRate)))
        {
            failures.Add("The forest town music must loop through the full track duration.");
        }

        var originalVolume = audioManager.BackgroundMusicVolume;
        audioManager.SetBackgroundMusicVolume(1.0f);
        if (Mathf.Abs(musicPlayer.VolumeDb - 4.0f) > 0.001f)
        {
            failures.Add("A 100% volume setting must apply the music makeup gain.");
        }

        var startScreen = GD.Load<PackedScene>("res://scenes/StartScreen.tscn").Instantiate<StartScreen>();
        AddChild(startScreen);

        var centerPanel = startScreen.GetNode<PanelContainer>("CenterPanel");
        var settingsPanel = startScreen.GetNode<PanelContainer>("SettingsPanel");
        var soundFxPanel = startScreen.GetNode<PanelContainer>("SoundFxPanel");
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
