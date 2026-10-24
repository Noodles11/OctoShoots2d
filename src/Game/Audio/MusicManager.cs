using System;
using Godot;

namespace OctoShoots.Game.Audio;

/// <summary>
/// The adaptive soundtrack (audio/godot_adaptive_music_setup.md; a C# port of audio/music_manager.gd), an autoload so
/// the music carries on from the title into the game. Three channels play in sync, mixed by the game's state:
///   1. THEME      — the main ambient theme, always playing.
///   2. CORRUPTION — two variants of the theme: a bass-heavy "dark" one and a treble-harsh "eerie" one, blended by a
///                   corruption value in [-1, 1] (the sign picks the flavour, the size the intensity; 0 = clean).
///   3. COMBAT     — a danger layer that fades in on encounters (on the next beat) and ducks the others.
/// The stems are read from res://audio/: theme, corruption_dark, corruption_eerie and combat, as .ogg, .mp3 or .wav,
/// all looping; a missing one simply stays silent. They must share BPM, key and length.
/// </summary>
public partial class MusicManager : Node
{
    public static MusicManager? Instance { get; private set; }

    public event Action? CombatStarted;
    public event Action? CombatEnded;

    /// <summary>The stems' shared tempo (combat starts on the theme's next beat).</summary>
    [Export] public float Bpm = 88f;
    [Export] public bool CombatQuantizeToBeat = true;
    /// <summary>Seconds for the corruption blend to travel the full 0 → 1 range.</summary>
    [Export] public float CorruptionSmoothing = 1.5f;
    [Export] public float CombatFadeIn = 0.8f, CombatFadeOut = 1.2f;
    /// <summary>How far the theme and corruption channels are ducked during combat (dB).</summary>
    [Export] public float DuckDb = -6f;
    /// <summary>Extra pitch on the dark corruption bus, in semitones (e.g. -2 detunes it a little; 0 = none).</summary>
    [Export] public float DarkBusPitchSemitones;
    /// <summary>The soundtrack is off for now: nothing plays (the game's sounds do).</summary>
    public const bool SoundtrackOn = false;
        /// <summary>The music bus's level (dB), under the game's sounds.</summary>
    [Export] public float MusicDb = -6f;

    const string BusMusic = "Music", BusTheme = "MTheme", BusDark = "MDark", BusEerie = "MEerie", BusCombat = "MCombat";
    const float SilenceDb = -60f;
    const string Folder = "res://audio/";

    AudioStreamPlayer _theme = null!, _dark = null!, _eerie = null!, _combat = null!;
    float _corruptionTarget, _corruptionCurrent;
    bool _inCombat;
    Tween? _combatTween, _duckTween;
    ulong _combatToken;

    public override void _Ready()
    {
        Instance = this;
        // Music keeps playing under the pause menu.
        ProcessMode = ProcessModeEnum.Always;
        EnsureBuses();
        _theme = MakePlayer(Load("theme"), BusTheme);
        _dark = MakePlayer(Load("corruption_dark"), BusDark);
        _eerie = MakePlayer(Load("corruption_eerie"), BusEerie);
        _combat = MakePlayer(Load("combat"), BusCombat);
        // The music is off for now (audio/theme.mp3 is a placeholder, not ours to ship): no stem starts, and the music bus
        // is muted, so the combat layer (started on encounters) stays silent too. Set SoundtrackOn to hear it again.
        if (SoundtrackOn) StartAllStems();
        AudioServer.SetBusMute(AudioServer.GetBusIndex(BusMusic), !SoundtrackOn);
        static string Of(AudioStreamPlayer p) => p.Stream is null ? "none" : $"{p.Stream.ResourcePath.GetFile()} ({p.Stream.GetLength():0} s)";
        GD.Print($"Music: theme {Of(_theme)}, dark {Of(_dark)}, eerie {Of(_eerie)}, combat {Of(_combat)}; playing {_theme.Playing}");
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------ API ---

    /// <summary>
    /// Corruption in [-1, 1]: negative is the bass/dark flavour, positive the treble/eerie one, the size its intensity.
    /// Smoothed over CorruptionSmoothing seconds, so it is safe to call every frame.
    /// </summary>
    public void SetCorruption(float value) => _corruptionTarget = Math.Clamp(value, -1f, 1f);

    public void EnterCombat()
    {
        if (_inCombat) return;
        _inCombat = true;
        ApplyDuck(true);
        if (CombatQuantizeToBeat) StartCombatOnNextBeat();
        else FadeCombatTo(1f, CombatFadeIn);
        CombatStarted?.Invoke();
    }

    public void ExitCombat()
    {
        if (!_inCombat) return;
        _inCombat = false;
        _combatToken++;
        ApplyDuck(false);
        FadeCombatTo(0f, CombatFadeOut);
        CombatEnded?.Invoke();
    }

    public bool InCombat => _inCombat;

    // -------------------------------------------------------------- process ---

    public override void _Process(double delta)
    {
        if (Mathf.IsEqualApprox(_corruptionCurrent, _corruptionTarget)) return;
        float rate = (float)delta / MathF.Max(CorruptionSmoothing, 0.01f);
        _corruptionCurrent = Mathf.MoveToward(_corruptionCurrent, _corruptionTarget, rate);
        ApplyCorruptionBlend(_corruptionCurrent);
    }

    // ----------------------------------------------------------- corruption ---

    /// <summary>An equal-power-ish blend of the three stems: their gains add up to at most 1.</summary>
    void ApplyCorruptionBlend(float c)
    {
        float intensity = MathF.Abs(c);
        SetGain(_theme, 1f - intensity);
        SetGain(_dark, MathF.Max(-c, 0f));
        SetGain(_eerie, MathF.Max(c, 0f));
    }

    static void SetGain(AudioStreamPlayer player, float linear)
    {
        // sqrt softens the crossfade's midpoint so the mix does not dip.
        float shaped = MathF.Sqrt(Math.Clamp(linear, 0f, 1f));
        player.VolumeDb = shaped <= 0.0001f ? SilenceDb : Mathf.LinearToDb(shaped);
    }

    // --------------------------------------------------------------- combat ---

    async void StartCombatOnNextBeat()
    {
        float beat = 60f / Bpm;
        float pos = _theme.Playing ? _theme.GetPlaybackPosition() : 0f;
        float wait = beat - Mathf.PosMod(pos, beat);
        if (wait < 0.02f)
        {
            FadeCombatTo(1f, CombatFadeIn);
            return;
        }
        ulong token = ++_combatToken;
        await ToSignal(GetTree().CreateTimer(wait, processAlways: true), SceneTreeTimer.SignalName.Timeout);
        // Combat may have ended (or started again) while it waited.
        if (_inCombat && token == _combatToken) FadeCombatTo(1f, CombatFadeIn);
    }

    void FadeCombatTo(float gain, float seconds)
    {
        if (_combatTween is not null && _combatTween.IsValid()) _combatTween.Kill();
        if (_combat.Stream is null) return;
        if (gain > 0f && !_combat.Playing)
        {
            _combat.VolumeDb = SilenceDb;
            _combat.Play();
        }
        _combatTween = CreateTween();
        _combatTween.TweenProperty(_combat, "volume_db", gain <= 0f ? SilenceDb : Mathf.LinearToDb(gain), seconds);
        if (gain <= 0f) _combatTween.TweenCallback(Callable.From(_combat.Stop));
    }

    /// <summary>Ducks the theme and corruption buses while combat is on.</summary>
    void ApplyDuck(bool active)
    {
        if (_duckTween is not null && _duckTween.IsValid()) _duckTween.Kill();
        _duckTween = CreateTween().SetParallel();
        foreach (string name in new[] { BusTheme, BusDark, BusEerie })
        {
            int idx = AudioServer.GetBusIndex(name);
            _duckTween.TweenMethod(Callable.From<float>(v => AudioServer.SetBusVolumeDb(idx, v)), AudioServer.GetBusVolumeDb(idx), active ? DuckDb : 0f, CombatFadeIn);
        }
    }

    // ---------------------------------------------------------------- setup ---

    void EnsureBuses()
    {
        AddBusIfMissing(BusMusic, "Master");
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(BusMusic), MusicDb);
        foreach (string b in new[] { BusTheme, BusDark, BusEerie, BusCombat }) AddBusIfMissing(b, BusMusic);
        if (DarkBusPitchSemitones != 0f)
            AudioServer.AddBusEffect(AudioServer.GetBusIndex(BusDark), new AudioEffectPitchShift { PitchScale = MathF.Pow(2f, DarkBusPitchSemitones / 12f) });
    }

    static void AddBusIfMissing(string name, string sendTo)
    {
        if (AudioServer.GetBusIndex(name) != -1) return;
        AudioServer.AddBus();
        int idx = AudioServer.BusCount - 1;
        AudioServer.SetBusName(idx, name);
        AudioServer.SetBusSend(idx, sendTo);
    }

    AudioStreamPlayer MakePlayer(AudioStream? stream, string bus)
    {
        var p = new AudioStreamPlayer { Stream = stream, Bus = bus };
        AddChild(p);
        return p;
    }

    /// <summary>A stem from res://audio/ (ogg, then mp3, then wav), set to loop; null when there is none.</summary>
    static AudioStream? Load(string name)
    {
        foreach (string ext in new[] { ".ogg", ".mp3", ".wav" })
        {
            string path = Folder + name + ext;
            if (!ResourceLoader.Exists(path)) continue;
            var stream = GD.Load<AudioStream>(path);
            switch (stream)
            {
                case AudioStreamOggVorbis ogg: ogg.Loop = true; break;
                case AudioStreamMP3 mp3: mp3.Loop = true; break;
                case AudioStreamWav wav when wav.LoopMode == AudioStreamWav.LoopModeEnum.Disabled:
                    wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                    wav.LoopBegin = 0;
                    wav.LoopEnd = (int)(wav.GetLength() * wav.MixRate);
                    break;
            }
            return stream;
        }
        return null;
    }

    /// <summary>Every stem starts on the same frame so they stay sample-aligned; the variants start silent, combat stopped.</summary>
    void StartAllStems()
    {
        ApplyCorruptionBlend(0f);
        _combat.VolumeDb = SilenceDb;
        foreach (var p in new[] { _theme, _dark, _eerie })
            if (p.Stream is not null) p.Play();
    }
}
