using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Items;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The pearls she has absorbed, circling her bell (DESIGN-TOPDOWN §2.5): one small pearl for each, in its own colours and
/// pattern (its world pearl's material), spaced evenly round one ring a little out from the bell and turning slowly —
/// the build sheet worn on her, in place of a HUD list. Each new one slots in and the ring respaces itself smoothly.
/// </summary>
public partial class PearlOrbitView : Node3D
{
    /// <summary>The ring's radius round her middle, its pearls' size, how fast it turns (rad/s) and how high it rides over her plane.</summary>
    const float Ring = 0.8f, Size = 0.065f, Turn = 0.55f, Lift = 0.05f;

    readonly List<(MeshInstance3D Mesh, string Id, float Angle)> _pearls = new();
    SphereMesh _mesh = null!;
    float _spin;

    public override void _Ready() => _mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 20, Rings = 10 };

    /// <summary>Follows her (her bell's place), with the pearls she carries now.</summary>
    public void Sync(IReadOnlyList<string> items, ItemCatalog? catalog, Vector3 bell, float dt, bool visible)
    {
        Visible = visible;
        if (!visible || catalog is null) return;
        // The ring's pearls: one per pearl she holds (non-unique ones as many times as she took them), in order.
        int k = 0;
        foreach (string id in items)
        {
            if (!catalog.TryGet(id, out var item) || item.Pearl is null) continue;
            if (k < _pearls.Count && _pearls[k].Id == id)
            {
                k++;
                continue;
            }
            if (k < _pearls.Count) _pearls[k].Mesh.MaterialOverride = PearlMaterials.Get(item);
            else
            {
                var mesh = new MeshInstance3D { Mesh = _mesh, MaterialOverride = PearlMaterials.Get(item), Scale = Vector3.One * Size, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                AddChild(mesh);
                // A new pearl joins where the last one was, then glides to its place.
                float from = _pearls.Count > 0 ? _pearls[^1].Angle : 0f;
                _pearls.Add((mesh, id, from));
            }
            _pearls[k] = (_pearls[k].Mesh, id, _pearls[k].Angle);
            k++;
        }
        while (_pearls.Count > k)
        {
            _pearls[^1].Mesh.QueueFree();
            _pearls.RemoveAt(_pearls.Count - 1);
        }

        _spin += Turn * dt;
        Position = bell;
        int n = _pearls.Count;
        for (int i = 0; i < n; i++)
        {
            var (mesh, id, angle) = _pearls[i];
            // Evenly round the ring, easing to their places as the count changes.
            float target = Mathf.Tau * i / n;
            angle = Mathf.LerpAngle(angle, target, 1f - Mathf.Exp(-4f * dt));
            _pearls[i] = (mesh, id, angle);
            float a = angle + _spin;
            // A slight sway on the water, each pearl a little out of step.
            float bob = 0.04f * Mathf.Sin(_spin * 2.3f + i * 1.7f);
            mesh.Position = new Vector3(Mathf.Cos(a) * Ring, Lift + bob, Mathf.Sin(a) * Ring);
        }
    }
}
