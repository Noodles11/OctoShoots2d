using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.Fx;

/// <summary>Short-lived beams (Sunbeam, Prism Pearl) and jagged lightning arcs (Electric Eel Tail).</summary>
public partial class LineFx : Node3D
{
    sealed class Beam
    {
        public required MeshInstance3D Mesh;
        public float Life;
    }

    sealed class Arc
    {
        public Vector3 From;
        public Vector3 To;
        public float Life;
    }

    const int MaxBeams = 8;
    const float ArcLife = 0.16f;

    readonly List<Beam> _beams = new();
    readonly List<Arc> _arcs = new();
    readonly Random _random = new(11);
    ImmediateMesh _arcMesh = null!;
    StandardMaterial3D _arcMaterial = null!;

    public override void _Ready()
    {
        var beamMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            AlbedoColor = new Color(1f, 0.95f, 0.6f, 0.9f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.85f, 0.4f),
            EmissionEnergyMultiplier = 4f,
        };
        var cylinder = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.07f, Height = 1f, RadialSegments = 8, Rings = 1 };
        for (int i = 0; i < MaxBeams; i++)
        {
            var mesh = new MeshInstance3D { Mesh = cylinder, MaterialOverride = beamMaterial, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(mesh);
            _beams.Add(new Beam { Mesh = mesh });
        }

        _arcMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            VertexColorUseAsAlbedo = true,
        };
        _arcMesh = new ImmediateMesh();
        AddChild(new MeshInstance3D { Mesh = _arcMesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Shows a beam segment; a continuous laser calls this every tick, so a short life is enough.</summary>
    public void ShowBeam(Vector3 origin, Vector3 dir, float length, float life = 0.05f, float width = 1f)
    {
        var beam = _beams.Find(b => b.Life <= 0f) ?? _beams[0];
        beam.Life = life;
        beam.Mesh.Visible = true;
        // CylinderMesh runs along Y; align Y with the beam.
        Basis basis = Conv.AlignUp(dir, 0f).Scaled(new Vector3(width, length, width));
        beam.Mesh.GlobalTransform = new Transform3D(basis, origin + dir * (length * 0.5f));
    }

    public void ShowArc(Vector3 from, Vector3 to) => _arcs.Add(new Arc { From = from, To = to, Life = ArcLife });

    public void Clear()
    {
        foreach (var b in _beams)
        {
            b.Life = 0f;
            b.Mesh.Visible = false;
        }
        _arcs.Clear();
    }

    public void Tick(float dt)
    {
        foreach (var b in _beams)
        {
            if (b.Life <= 0f) continue;
            b.Life -= dt;
            if (b.Life <= 0f) b.Mesh.Visible = false;
        }

        _arcMesh.ClearSurfaces();
        _arcs.RemoveAll(a => (a.Life -= dt) <= 0f);
        if (_arcs.Count == 0) return;
        _arcMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, _arcMaterial);
        foreach (var arc in _arcs)
        {
            float alpha = arc.Life / ArcLife;
            Vector3 d = arc.To - arc.From;
            Vector3 side = d.Cross(Vector3.Up).Normalized();
            if (side.LengthSquared() < 1e-4f) side = Vector3.Right;
            Vector3 up = side.Cross(d).Normalized();
            Vector3 prev = arc.From;
            const int segments = 7;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float jitter = i == segments ? 0f : 0.35f;
                Vector3 next = arc.From + d * t + side * Rand(jitter) + up * Rand(jitter);
                _arcMesh.SurfaceSetColor(new Color(0.7f, 0.95f, 1f, alpha));
                _arcMesh.SurfaceAddVertex(prev);
                _arcMesh.SurfaceSetColor(new Color(0.7f, 0.95f, 1f, alpha));
                _arcMesh.SurfaceAddVertex(next);
                prev = next;
            }
        }
        _arcMesh.SurfaceEnd();
    }

    float Rand(float amount) => ((float)_random.NextDouble() * 2f - 1f) * amount;
}
