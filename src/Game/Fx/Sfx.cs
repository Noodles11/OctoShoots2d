using System;
using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>Synth-style placeholder sounds generated at startup, played flat or positionally.</summary>
public partial class Sfx : Node
{
    const int Rate = 44100;

    readonly Dictionary<string, AudioStreamWav> _sounds = new();
    readonly List<AudioStreamPlayer> _flat = new();
    readonly List<AudioStreamPlayer3D> _spatial = new();
    readonly Random _random = new(3);
    int _nextFlat, _nextSpatial;

    public override void _Ready()
    {
        _sounds["shot"] = Make(0.11f, (t, n) => Sweep(t, 0.11f, 620f, 170f) * Env(t, 0.004f, 0.11f) * 0.5f + n * Env(t, 0.002f, 0.05f) * 0.25f);
        _sounds["hit"] = Make(0.16f, (t, n) => Sweep(t, 0.16f, 320f, 80f) * Env(t, 0.002f, 0.16f) * 0.7f + n * Env(t, 0.001f, 0.06f) * 0.5f);
        _sounds["splat"] = Make(0.12f, (t, n) => n * Env(t, 0.002f, 0.12f) * 0.3f);
        _sounds["dash"] = Make(0.38f, (t, n) => n * Env(t, 0.03f, 0.38f) * 0.45f + Sweep(t, 0.38f, 140f, 60f) * Env(t, 0.01f, 0.3f) * 0.3f);
        _sounds["jet"] = Make(0.24f, (t, n) => n * Env(t, 0.02f, 0.24f) * 0.18f + Sweep(t, 0.24f, 110f, 70f) * Env(t, 0.01f, 0.2f) * 0.15f);
        _sounds["enemy_shot"] = Make(0.22f, (t, n) => MathF.Sign(Sweep(t, 0.22f, 950f, 300f)) * Env(t, 0.003f, 0.22f) * 0.22f);
        _sounds["hurt"] = Make(0.3f, (t, n) => Sweep(t, 0.3f, 140f, 45f) * Env(t, 0.002f, 0.3f) * 0.9f + n * Env(t, 0.001f, 0.08f) * 0.4f);
        _sounds["die"] = Make(0.45f, (t, n) => MathF.Sin(MathF.Tau * (300f + 400f * MathF.Floor(t * 14f % 3f)) * t) * Env(t, 0.003f, 0.45f) * 0.4f);
        _sounds["land"] = Make(0.14f, (t, n) => (Sweep(t, 0.14f, 90f, 50f) * 0.6f + n * 0.3f) * Env(t, 0.002f, 0.14f) * 0.5f);
        _sounds["boom"] = Make(0.5f, (t, n) => (Sweep(t, 0.5f, 120f, 30f) * 0.8f + n * 0.7f) * Env(t, 0.002f, 0.5f) * 0.8f);
        _sounds["zap"] = Make(0.14f, (t, n) => MathF.Sign(MathF.Sin(MathF.Tau * (900f + 600f * n) * t)) * Env(t, 0.001f, 0.14f) * 0.2f);
        _sounds["bounce"] = Make(0.08f, (t, n) => Sweep(t, 0.08f, 500f, 900f) * Env(t, 0.002f, 0.08f) * 0.3f);
        _sounds["freeze"] = Make(0.3f, (t, n) => MathF.Sin(MathF.Tau * 2200f * t) * MathF.Sin(MathF.Tau * 31f * t) * Env(t, 0.003f, 0.3f) * 0.25f);
        _sounds["beam"] = Make(0.42f, (t, n) => (MathF.Sin(MathF.Tau * 330f * t) + 0.5f * MathF.Sin(MathF.Tau * 661f * t)) * Env(t, 0.01f, 0.42f) * 0.3f);
        _sounds["item"] = Make(0.6f, (t, n) => MathF.Sin(MathF.Tau * (523f * (t < 0.15f ? 1f : t < 0.3f ? 1.26f : 1.5f)) * t) * Env(t, 0.005f, 0.6f) * 0.35f);
        _sounds["synergy"] = Make(0.9f, (t, n) => (MathF.Sin(MathF.Tau * 523f * t) + MathF.Sin(MathF.Tau * 659f * t) + MathF.Sin(MathF.Tau * 784f * t)) * Env(t, 0.01f, 0.9f) * 0.2f);
        _sounds["active"] = Make(0.35f, (t, n) => (Sweep(t, 0.35f, 200f, 600f) * 0.5f + n * 0.3f) * Env(t, 0.005f, 0.35f) * 0.5f);
        // A soft "bloop" as a bubble leaves the tentacle, and a bright pop when one bursts.
        _sounds["throw"] = Make(0.13f, (t, n) => Sweep(t, 0.13f, 260f, 620f) * Env(t, 0.006f, 0.13f) * 0.5f + n * Env(t, 0.002f, 0.03f) * 0.08f);
        _sounds["pop"] = Make(0.07f, (t, n) => (Sweep(t, 0.07f, 1500f, 700f) * 0.5f + n * 0.5f) * Env(t, 0.001f, 0.07f) * 0.45f);
        // A tiny starfish whizzing off, and a ninja popping out of its anemone.
        _sounds["star"] = Make(0.18f, (t, n) => (Sweep(t, 0.18f, 2400f, 900f) * 0.35f + n * 0.3f) * Env(t, 0.003f, 0.18f) * 0.5f);
        _sounds["emerge"] = Make(0.22f, (t, n) => (Sweep(t, 0.22f, 220f, 520f) * 0.5f + n * 0.25f) * Env(t, 0.01f, 0.22f) * 0.55f);
        _sounds["coin"] = Make(0.16f, (t, n) => (MathF.Sin(MathF.Tau * 1320f * t) + 0.6f * MathF.Sin(MathF.Tau * (t < 0.05f ? 990f : 1760f) * t)) * Env(t, 0.002f, 0.16f) * 0.22f);
        _sounds["chime"] = Make(0.5f, (t, n) => (MathF.Sin(MathF.Tau * 880f * t) + MathF.Sin(MathF.Tau * 1318f * t) * 0.6f) * Env(t, 0.01f, 0.5f) * 0.18f);
        // Creature calls: a barracuda's hiss, a pufferling's squeak, clicks for the hard-shelled, a gurgle for the soft.
        _sounds["hiss"] = Make(0.5f, (t, n) => n * Env(t, 0.05f, 0.5f) * 0.55f + Sweep(t, 0.5f, 1800f, 3200f) * Env(t, 0.05f, 0.5f) * 0.12f);
        _sounds["squeak"] = Make(0.2f, (t, n) => Sweep(t, 0.2f, 900f, 2200f) * Env(t, 0.004f, 0.2f) * 0.4f);
        _sounds["click"] = Make(0.2f, (t, n) => MathF.Sign(MathF.Sin(MathF.Tau * 260f * t)) * Env(t, 0.001f, 0.03f) * 0.3f + MathF.Sign(MathF.Sin(MathF.Tau * 330f * (t - 0.09f))) * (t > 0.09f ? Env(t - 0.09f, 0.001f, 0.03f) : 0f) * 0.3f);
        _sounds["gurgle"] = Make(0.35f, (t, n) => MathF.Sin(MathF.Tau * (180f + 60f * MathF.Sin(t * 40f)) * t) * Env(t, 0.01f, 0.35f) * 0.4f + n * Env(t, 0.005f, 0.1f) * 0.1f);
        _sounds["nope"] = Make(0.12f, (t, n) => MathF.Sign(MathF.Sin(MathF.Tau * 140f * t)) * Env(t, 0.002f, 0.12f) * 0.15f);

        for (int i = 0; i < 10; i++)
        {
            var p = new AudioStreamPlayer();
            AddChild(p);
            _flat.Add(p);
        }
        for (int i = 0; i < 14; i++)
        {
            var p = new AudioStreamPlayer3D { MaxDistance = 45f, UnitSize = 6f, AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance };
            AddChild(p);
            _spatial.Add(p);
        }
    }

    public void Play(string name, float volumeDb = 0f, float pitchJitter = 0.06f)
    {
        var p = _flat[_nextFlat];
        _nextFlat = (_nextFlat + 1) % _flat.Count;
        p.Stream = _sounds[name];
        p.VolumeDb = volumeDb;
        p.PitchScale = 1f + ((float)_random.NextDouble() * 2f - 1f) * pitchJitter;
        p.Play();
    }

    public void PlayAt(string name, Vector3 position, float volumeDb = 0f, float pitchJitter = 0.06f)
    {
        var p = _spatial[_nextSpatial];
        _nextSpatial = (_nextSpatial + 1) % _spatial.Count;
        p.Stream = _sounds[name];
        p.VolumeDb = volumeDb;
        p.PitchScale = 1f + ((float)_random.NextDouble() * 2f - 1f) * pitchJitter;
        p.GlobalPosition = position;
        p.Play();
    }

    /// <summary>The telegraph tone rises over exactly the telegraph time, so the ear can time the dodge.</summary>
    public void PlayTelegraph(Vector3 position, float duration)
    {
        string key = $"telegraph_{Mathf.RoundToInt(duration * 100f)}";
        if (!_sounds.ContainsKey(key))
        {
            float d = Mathf.Max(duration, 0.05f);
            _sounds[key] = Make(d, (t, n) =>
                MathF.Sin(MathF.Tau * (420f * t + 0.5f * (1300f - 420f) / d * t * t)) * (0.6f + 0.4f * MathF.Sin(t * 70f)) * MathF.Min(1f, t / 0.03f) * 0.45f);
        }
        PlayAt(key, position, 2f, 0f);
    }

    AudioStreamWav Make(float seconds, Func<float, float, float> sample)
    {
        int count = (int)(seconds * Rate);
        var data = new byte[count * 2];
        float lowpassed = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate;
            lowpassed += ((float)_random.NextDouble() * 2f - 1f - lowpassed) * 0.25f;
            float v = Math.Clamp(sample(t, lowpassed), -1f, 1f);
            short s = (short)(v * 32000f);
            data[i * 2] = (byte)(s & 0xff);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
    }

    static float Sweep(float t, float duration, float from, float to)
    {
        float k = (to - from) / duration;
        return MathF.Sin(MathF.Tau * (from * t + 0.5f * k * t * t));
    }

    static float Env(float t, float attack, float duration) =>
        MathF.Min(1f, t / attack) * MathF.Max(0f, 1f - t / duration) * MathF.Max(0f, 1f - t / duration);
}
