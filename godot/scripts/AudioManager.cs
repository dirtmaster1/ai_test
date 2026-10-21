using Godot;

public partial class AudioManager : Node
{
    private const string ExplorationMusicPath = "res://assets/audio/forest_town_theme.wav";
    private const string CombatMusicPath = "res://assets/audio/combat_1.wav";
    private const string SettingsPath = "user://dark_dungeon_tactics_audio.cfg";
    private const string MusicBusName = "BackgroundMusic";
    private const float SilentVolumeDb = -80.0f;
    private const float MusicMakeupGainDb = 4.0f;
    private const float CombatToExplorationFadeOutSeconds = 1.25f;
    private const float CombatToExplorationFadeInSeconds = 1.75f;

    private AudioStreamPlayer _musicPlayer;
    private AudioStreamWav _explorationMusic;
    private AudioStreamWav _combatMusic;
    private Tween _musicTransitionTween;
    private float _backgroundMusicVolume = 1.0f;
    private float _musicFade = 1.0f;

    public float BackgroundMusicVolume => _backgroundMusicVolume;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _backgroundMusicVolume = LoadBackgroundMusicVolume();

        _explorationMusic = LoadLoopingMusic(ExplorationMusicPath);
        if (_explorationMusic == null)
        {
            return;
        }

        _combatMusic = LoadLoopingMusic(CombatMusicPath);

        var eventBus = GetNode<EventBus>("/root/EventBus");
        eventBus.CombatStarted += SwitchToCombatMusic;
        eventBus.CombatEnded += SwitchToExplorationMusic;

        ConfigureMusicBus();
        _musicPlayer = new AudioStreamPlayer
        {
            Name = "BackgroundMusicPlayer",
            Stream = _explorationMusic,
            Bus = MusicBusName,
            ProcessMode = ProcessModeEnum.Always
        };
        AddChild(_musicPlayer);
        ApplyBackgroundMusicVolume();
        _musicPlayer.Play();
    }

    private static AudioStreamWav LoadLoopingMusic(string path)
    {
        var music = GD.Load<AudioStreamWav>(path);
        if (music == null)
        {
            GD.PushError($"Could not load music at '{path}'.");
            return null;
        }

        music.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        music.LoopBegin = 0;
        music.LoopEnd = Mathf.RoundToInt((float)(music.GetLength() * music.MixRate));
        return music;
    }

    private void SwitchToCombatMusic()
    {
        _musicTransitionTween?.Kill();
        _musicTransitionTween = null;
        SetMusicFade(1.0f);
        SwitchMusic(_combatMusic);
    }

    private void SwitchToExplorationMusic()
    {
        if (_musicPlayer == null || _explorationMusic == null || _musicPlayer.Stream == _explorationMusic)
        {
            return;
        }

        _musicTransitionTween?.Kill();
        _musicTransitionTween = CreateTween();
        _musicTransitionTween.SetTrans(Tween.TransitionType.Sine);
        _musicTransitionTween.SetEase(Tween.EaseType.InOut);
        _musicTransitionTween.TweenMethod(
            Callable.From<float>(SetMusicFade),
            _musicFade,
            0.0f,
            CombatToExplorationFadeOutSeconds
        );
        _musicTransitionTween.TweenCallback(Callable.From(() => SwitchMusic(_explorationMusic)));
        _musicTransitionTween.TweenMethod(
            Callable.From<float>(SetMusicFade),
            0.0f,
            1.0f,
            CombatToExplorationFadeInSeconds
        );
        _musicTransitionTween.Finished += () => _musicTransitionTween = null;
    }

    private void SwitchMusic(AudioStreamWav music)
    {
        if (_musicPlayer == null || music == null)
        {
            return;
        }

        if (_musicPlayer.Stream == music && _musicPlayer.Playing)
        {
            return;
        }

        _musicPlayer.Stream = music;
        _musicPlayer.Play();
    }

    private void ConfigureMusicBus()
    {
        var busIndex = AudioServer.GetBusIndex(MusicBusName);
        if (busIndex < 0)
        {
            AudioServer.AddBus();
            busIndex = AudioServer.BusCount - 1;
            AudioServer.SetBusName(busIndex, MusicBusName);
            AudioServer.SetBusSend(busIndex, "Master");
        }

        for (var effectIndex = 0; effectIndex < AudioServer.GetBusEffectCount(busIndex); effectIndex++)
        {
            if (AudioServer.GetBusEffect(busIndex, effectIndex) is AudioEffectHardLimiter)
            {
                return;
            }
        }

        AudioServer.AddBusEffect(busIndex, new AudioEffectHardLimiter
        {
            CeilingDb = -1.0f,
            PreGainDb = 0.0f,
            Release = 0.1f
        });
    }

    public void SetBackgroundMusicVolume(float volume)
    {
        _backgroundMusicVolume = Mathf.Clamp(volume, 0.0f, 1.0f);
        ApplyBackgroundMusicVolume();

        var config = new ConfigFile();
        config.SetValue("audio", "background_music_volume", _backgroundMusicVolume);
        var error = config.Save(SettingsPath);
        if (error != Error.Ok)
        {
            GD.PushError($"Could not save audio settings to '{SettingsPath}': {error}.");
        }
    }

    private float LoadBackgroundMusicVolume()
    {
        if (!FileAccess.FileExists(SettingsPath))
        {
            return 1.0f;
        }

        var config = new ConfigFile();
        var error = config.Load(SettingsPath);
        if (error != Error.Ok)
        {
            GD.PushError($"Could not load audio settings from '{SettingsPath}': {error}.");
            return 1.0f;
        }

        var storedVolume = config.GetValue("audio", "background_music_volume", 1.0f);
        if (storedVolume.VariantType != Variant.Type.Float && storedVolume.VariantType != Variant.Type.Int)
        {
            GD.PushError($"Audio setting 'background_music_volume' in '{SettingsPath}' must be numeric.");
            return 1.0f;
        }

        return Mathf.Clamp((float)storedVolume.AsDouble(), 0.0f, 1.0f);
    }

    private void ApplyBackgroundMusicVolume()
    {
        if (_musicPlayer == null)
        {
            return;
        }

        var effectiveVolume = _backgroundMusicVolume * _musicFade;
        _musicPlayer.VolumeDb = effectiveVolume <= 0.0f
            ? SilentVolumeDb
            : Mathf.Max(SilentVolumeDb, Mathf.LinearToDb(effectiveVolume) + MusicMakeupGainDb);
        _musicPlayer.StreamPaused = _backgroundMusicVolume <= 0.0f;
    }

    private void SetMusicFade(float fade)
    {
        _musicFade = Mathf.Clamp(fade, 0.0f, 1.0f);
        ApplyBackgroundMusicVolume();
    }
}
