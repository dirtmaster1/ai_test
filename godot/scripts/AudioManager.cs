using Godot;

public partial class AudioManager : Node
{
    private const string ExplorationMusicPath = "res://assets/audio/forest_town_theme.wav";
    private const string CombatMusicPath = "res://assets/audio/combat_1.wav";
    private const string SettingsPath = "user://dark_dungeon_tactics_audio.cfg";
    private const string MusicBusName = "BackgroundMusic";
    private const string SfxBusName = "SoundEffects";
    private const string CueDirectory = "res://assets/audio/sfx/";
    private readonly ConfigFile _settings = new();
    private readonly System.Collections.Generic.Dictionary<string, AudioStream> _cues = new();
    private readonly AudioStreamPlayer[] _voices = new AudioStreamPlayer[6];
    private EventBus _eventBus;
    private const float SilentVolumeDb = -80.0f;
    private const float MusicMakeupGainDb = 4.0f;
    private const float SoundEffectsMakeupGainDb = 6.0f;
    private const float CombatToExplorationFadeOutSeconds = 1.25f;
    private const float CombatToExplorationFadeInSeconds = 1.75f;

    private AudioStreamPlayer _musicPlayer;
    private AudioStreamWav _explorationMusic;
    private AudioStreamWav _combatMusic;
    private Tween _musicTransitionTween;
    private float _backgroundMusicVolume = 1.0f;
    private float _musicFade = 1.0f;

    public float BackgroundMusicVolume => _backgroundMusicVolume;
    public float SoundEffectsVolume { get; private set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        if (FileAccess.FileExists(SettingsPath))
        {
            var error = _settings.Load(SettingsPath);
            if (error != Error.Ok)
                GD.PushError($"Could not load audio settings from '{SettingsPath}': {error}.");
        }
        _backgroundMusicVolume = ReadVolume("background_music_volume", 1f);
        SoundEffectsVolume = ReadVolume("sound_effects_volume", 0.8f);
        ConfigureSoundEffects();
        _eventBus = GetNode<EventBus>("/root/EventBus");
        _eventBus.ActionUsed += OnActionUsed;

        _explorationMusic = LoadLoopingMusic(ExplorationMusicPath);
        if (_explorationMusic == null)
        {
            return;
        }

        _combatMusic = LoadLoopingMusic(CombatMusicPath);

        _eventBus.CombatStarted += SwitchToCombatMusic;
        _eventBus.CombatEnded += SwitchToExplorationMusic;

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

    public override void _ExitTree()
    {
        if (_eventBus != null)
        {
            _eventBus.ActionUsed -= OnActionUsed;
            _eventBus.CombatStarted -= SwitchToCombatMusic;
            _eventBus.CombatEnded -= SwitchToExplorationMusic;
        }
    }

    private void ConfigureSoundEffects()
    {
        var bus = AudioServer.GetBusIndex(SfxBusName);
        if (bus < 0)
        {
            AudioServer.AddBus();
            bus = AudioServer.BusCount - 1;
            AudioServer.SetBusName(bus, SfxBusName);
            AudioServer.SetBusSend(bus, "Master");
        }
        var hasLimiter = false;
        for (var effectIndex = 0; effectIndex < AudioServer.GetBusEffectCount(bus); effectIndex++)
        {
            hasLimiter |= AudioServer.GetBusEffect(bus, effectIndex) is AudioEffectHardLimiter;
        }
        if (!hasLimiter)
        {
            AudioServer.AddBusEffect(bus, new AudioEffectHardLimiter
            {
                CeilingDb = -1.0f,
                PreGainDb = 0.0f,
                Release = 0.1f
            });
        }
        ApplySoundEffectsVolume();
        foreach (var file in ResourceLoader.ListDirectory(CueDirectory))
        {
            if (file.EndsWith(".wav"))
                _cues[file[..^4]] = GD.Load<AudioStream>(CueDirectory + file);
        }
        for (var i = 0; i < _voices.Length; i++)
        {
            _voices[i] = new AudioStreamPlayer { Bus = SfxBusName };
            AddChild(_voices[i]);
        }
    }

    private void OnActionUsed(Variant actor, Variant actionId, Variant payload)
    {
        var id = actionId.AsString();
        PlaySoundEffect(id == "attack" ? "melee" : id);
    }

    public void PlaySoundEffect(string cue)
    {
        if (SoundEffectsVolume == 0f) return;
        if (!_cues.TryGetValue(cue, out var stream) || stream == null)
        {
            GD.PushWarning($"Missing action sound: {cue}");
            return;
        }
        foreach (var voice in _voices)
        {
            if (voice.Playing) continue;
            voice.Stream = stream;
            voice.Play();
            return;
        }
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
        SaveVolume("background_music_volume", _backgroundMusicVolume);
    }

    public void SetSoundEffectsVolume(float volume)
    {
        SoundEffectsVolume = Mathf.Clamp(volume, 0f, 1f);
        ApplySoundEffectsVolume();
        SaveVolume("sound_effects_volume", SoundEffectsVolume);
    }

    private void ApplySoundEffectsVolume()
    {
        var bus = AudioServer.GetBusIndex(SfxBusName);
        AudioServer.SetBusMute(bus, SoundEffectsVolume == 0f);
        AudioServer.SetBusVolumeDb(
            bus,
            SoundEffectsVolume > 0f
                ? Mathf.LinearToDb(SoundEffectsVolume) + SoundEffectsMakeupGainDb
                : SilentVolumeDb);
    }

    private void SaveVolume(string key, float value)
    {
        _settings.SetValue("audio", key, value);
        var error = _settings.Save(SettingsPath);
        if (error != Error.Ok)
        {
            GD.PushError($"Could not save audio settings to '{SettingsPath}': {error}.");
        }
    }

    private float ReadVolume(string key, float defaultValue)
    {
        var storedVolume = _settings.GetValue("audio", key, defaultValue);
        if (storedVolume.VariantType != Variant.Type.Float && storedVolume.VariantType != Variant.Type.Int)
        {
            GD.PushError($"Audio setting '{key}' in '{SettingsPath}' must be numeric.");
            return defaultValue;
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
