using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Corruption evaporating as it is cleared (docs/CORRUPTION.md). Where her light cleanses the floor, the ink lifts off
/// as dark curls of vapour that swell, thin to violet and let go a gold glint. When her own meter drains (a light
/// spring, or any healing), the ink steams off her the same way. Cosmetic only; the work is capped per frame.
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
        _glints = new FxParticles(768, additive: true);
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

    void Puff(Vector3 at, float rise, float scale)
    {
        var drift = new Vector3(_ink.RandRange(-0.35f, 0.35f), rise, _ink.RandRange(-0.35f, 0.35f));
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
