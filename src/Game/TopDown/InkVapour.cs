using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Corruption evaporating as it is cleared (docs/CORRUPTION.md). Where her light cleanses the floor, the ink lifts off
/// as dark curls of vapour that swell, thin to violet and let go a gold glint. When her own meter drains (a light
/// spring, or any healing), the ink steams off her the same way. A corrupted creature freed pops like a water balloon
/// (<see cref="Pop"/>): the ink it was made of showers out in droplets glowing in its colour, and evaporates the same
/// way. Cosmetic only; the work is capped per frame.
/// </summary>
public partial class InkVapour : Node3D
{
    /// <summary>At most this many puffs a frame from the floor, and how many per unit of density cleansed.</summary>
    const int FloorBudget = 48;
    const float PerDrop = 0.9f;

    static readonly Color Ink = new(0.05f, 0.02f, 0.09f, 0.75f);
    static readonly Color Thin = new(0.5f, 0.25f, 0.8f, 0f);
    static readonly Color Glint = new(1f, 0.86f, 0.5f, 1f);

    FxParticles _ink = null!, _glints = null!;
    float _herOwed;

    public override void _Ready()
    {
        _ink = new FxParticles(1536, additive: false);
        _glints = new FxParticles(1024, additive: true);
        AddChild(_ink);
        AddChild(_glints);
    }

    public void Clear()
    {
        _ink.Clear();
        _glints.Clear();
    }

    /// <summary>Lifts ink off the cells cleansed since the texture last uploaded.</summary>
    public void FromField(CorruptionTexture texture, LevelMap map)
    {
        int spent = 0;
        foreach (var (cell, drop) in texture.Drops)
        {
            if (spent >= FloorBudget) break;
            if (_ink.RandRange(0f, 1f) > drop * PerDrop) continue;
            spent++;
            var c = CorruptionField.CellCentre(cell % CorruptionField.N, cell / CorruptionField.N);
            float floor = Mathf.Min(map.HeightAt(c), LevelMap.BlockHeight);
            var at = new Vector3(c.X + _ink.RandRange(-0.25f, 0.25f), floor + 0.05f, c.Y + _ink.RandRange(-0.25f, 0.25f));
            Puff(at, _ink.RandRange(0.6f, 1.6f), 1f);
        }
        // Each upload's drops are lifted once.
        texture.Drops.Clear();
    }

    /// <summary>Her meter drained by amount (a share of the whole) this frame: the ink steams off her.</summary>
    public void FromHer(Vector3 at, float amount)
    {
        _herOwed += amount * 160f;
        while (_herOwed >= 1f)
        {
            _herOwed -= 1f;
            var off = _ink.RandDir() * 0.45f;
            Puff(at + off with { Y = off.Y * 0.5f }, _ink.RandRange(0.8f, 1.8f), 1.2f);
        }
    }

    /// <summary>
    /// A creature of ink freed: its skin pops like a water balloon. The ink inside showers out all round in droplets
    /// that glow in its colour as they thin (size: its body's radius), and the ink evaporates off the spray, as off the
    /// cleansed floor. No flash of light: only the ink, going.
    /// </summary>
    public void Pop(Vector3 at, float size, Color glow)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(40f * size), 14, 140);
        float speed = 0.6f + 0.4f * Mathf.Min(size, 2.5f);
        var lit = new Color(glow.R, glow.G, glow.B, 0.9f);
        var gone = new Color(glow.R, glow.G, glow.B, 0f);
        for (int i = 0; i < n; i++)
        {
            // The shower: droplets flung out of the skin, slowing fast in the water, glowing as they thin.
            var dir = _ink.RandDir();
            dir.Y = dir.Y * 0.6f + 0.15f;
            var from = at + dir * size * 0.7f;
            var v = dir * _ink.RandRange(2.5f, 6f) * speed;
            _ink.Emit(from, v, _ink.RandRange(0.5f, 0.9f), 0.07f + 0.03f * size, 0.02f, Ink with { A = 0.95f }, gone, drag: 4.5f, buoyancy: 0.3f);
            // Each drop glows in its colour as it flies, a little ahead of the dark ink.
            _glints.Emit(from, v * 1.05f, _glints.RandRange(0.5f, 0.9f), 0.09f + 0.03f * size, 0.015f, lit, gone, drag: 4.5f, buoyancy: 0.4f);
        }
        // And it evaporates: curls of ink lifting off where the spray slows, thinning to violet.
        for (int i = 0; i < n * 3 / 4; i++)
        {
            var dir = _ink.RandDir();
            dir.Y = Mathf.Abs(dir.Y) * 0.4f;
            var drift = dir * _ink.RandRange(0.6f, 1.8f) * speed + Vector3.Up * _ink.RandRange(0.4f, 1.2f);
            Puff(at + dir * size * _ink.RandRange(0.5f, 1.5f), drift, 0.8f + 0.25f * Mathf.Min(size, 2.5f));
        }
    }

    void Puff(Vector3 at, float rise, float scale) => Puff(at, new Vector3(_ink.RandRange(-0.35f, 0.35f), rise, _ink.RandRange(-0.35f, 0.35f)), scale);

    void Puff(Vector3 at, Vector3 drift, float scale)
    {
        float life = _ink.RandRange(0.9f, 1.6f);
        _ink.Emit(at, drift, life, _ink.RandRange(0.12f, 0.22f) * scale, _ink.RandRange(0.5f, 0.8f) * scale, Ink, Thin, drag: 1.1f, buoyancy: 0.7f);
        if (_glints.RandRange(0f, 1f) < 0.45f)
            _glints.Emit(at + Vector3.Up * 0.2f, drift * 1.3f, life * 0.8f, 0.07f * scale, 0.01f, Glint, Glint with { A = 0f }, drag: 0.8f, buoyancy: 0.5f);
    }

    public void Tick(float dt, Camera3D? camera)
    {
        if (camera is null) return;
        _ink.Tick(dt, camera.GlobalBasis);
        _glints.Tick(dt, camera.GlobalBasis);
    }
}
