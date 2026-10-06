using Godot;
using OctoShoots.Core.Sim;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.Enemies;

/// <summary>
/// Grey-box lanternfish: a bold dark body, glowing photophores and a lure. The photophores and lure
/// ramp to white-gold over the telegraph so the attack reads in the dark (§7.2).
/// </summary>
public partial class LanternfishView : Node3D
{
    StandardMaterial3D _body = null!;
    StandardMaterial3D _glow = null!;
    OmniLight3D _lure = null!;
    Node3D _model = null!;
    Node3D _tail = null!;
    float _swim;
    float _time;

    public override void _Ready()
    {
        _body = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.16f, 0.2f, 0.3f),
            Roughness = 0.4f,
            RimEnabled = true,
            Rim = 0.8f,
            RimTint = 0.4f,
            EmissionEnabled = true,
            Emission = Colors.Black,
        };
        _glow = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.4f, 0.95f, 1f),
            EmissionEnabled = true,
            Emission = new Color(0.4f, 0.95f, 1f),
            EmissionEnergyMultiplier = 2f,
        };
        var eyeWhite = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.85f, 0.6f), Roughness = 0.2f };
        var pupil = new StandardMaterial3D { AlbedoColor = Colors.Black, Roughness = 0.1f };

        _model = new Node3D();
        AddChild(_model);

        // Body: head toward -Z.
        _model.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.5f, Height = 1f },
            MaterialOverride = _body,
            Scale = new Vector3(0.62f, 0.72f, 1.15f),
        });
        _tail = new Node3D { Position = new Vector3(0, 0, 0.5f) };
        _model.AddChild(_tail);
        _tail.AddChild(new MeshInstance3D
        {
            Mesh = new PrismMesh { Size = new Vector3(0.5f, 0.45f, 0.05f) },
            MaterialOverride = _body,
            Position = new Vector3(0, 0, 0.2f),
            RotationDegrees = new Vector3(90f, 0f, 0f),
        });

        foreach (float side in new[] { -1f, 1f })
        {
            _model.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.09f, Height = 0.18f },
                MaterialOverride = eyeWhite,
                Position = new Vector3(side * 0.22f, 0.1f, -0.38f),
            });
            _model.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.05f, Height = 0.1f },
                MaterialOverride = pupil,
                Position = new Vector3(side * 0.28f, 0.11f, -0.43f),
            });
            for (int i = 0; i < 4; i++)
            {
                _model.AddChild(new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = 0.035f, Height = 0.07f },
                    MaterialOverride = _glow,
                    Position = new Vector3(side * 0.27f, -0.14f, -0.2f + i * 0.17f),
                });
            }
        }

        // Lure on a stalk above the head.
        _model.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.02f, Height = 0.45f },
            MaterialOverride = _body,
            Position = new Vector3(0, 0.45f, -0.45f),
            RotationDegrees = new Vector3(-35f, 0, 0),
        });
        _model.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f },
            MaterialOverride = _glow,
            Position = new Vector3(0, 0.62f, -0.6f),
        });
        _lure = new OmniLight3D
        {
            LightColor = new Color(0.4f, 0.95f, 1f),
            LightEnergy = 0.8f,
            OmniRange = 4f,
            Position = new Vector3(0, 0.62f, -0.6f),
        };
        _model.AddChild(_lure);
    }

    public void Sync(Enemy enemy, float alpha, float telegraphTime, float dt)
    {
        Visible = enemy.Alive;
        if (!enemy.Alive) return;

        Position = Conv.Lerp(enemy.PrevPosition, enemy.Position, alpha);
        Basis = Conv.LookAlong(enemy.Facing.G());

        if (!enemy.Disabled) _swim += dt * (4f + enemy.Velocity.Length() * 3f); // frozen and stunned fish stop wagging
        _tail.Rotation = new Vector3(0, Mathf.Sin(_swim) * 0.5f, 0);

        float charge = 0f;
        if (enemy.State == EnemyState.Telegraph && telegraphTime > 0f)
            charge = 1f - Mathf.Clamp(enemy.StateTimer / telegraphTime, 0f, 1f);

        Color calm = new(0.4f, 0.95f, 1f);
        Color hot = new(1f, 0.95f, 0.6f);
        float pulse = charge > 0f ? 0.5f + 0.5f * Mathf.Sin(charge * 30f) : 0f;
        _glow.Emission = calm.Lerp(hot, charge);
        _glow.AlbedoColor = _glow.Emission;
        _glow.EmissionEnergyMultiplier = 2f + charge * 8f + pulse * 3f;
        _lure.LightColor = _glow.Emission;
        _lure.LightEnergy = 0.8f + charge * 4f;
        _model.Scale = Vector3.One * (1f + charge * 0.15f);

        // Status tints; a hit flash wins over all of them.
        _time += dt;
        Color albedo = new(0.16f, 0.2f, 0.3f);
        Color emission = Colors.Black;
        float energy = 0f;
        if (enemy.FrozenTimer > 0f)
        {
            albedo = new Color(0.6f, 0.85f, 1f);
            emission = new Color(0.4f, 0.8f, 1f);
            energy = 0.6f;
        }
        else if (enemy.StunTimer > 0f)
        {
            albedo = new Color(0.35f, 0.35f, 0.4f);
            emission = new Color(1f, 1f, 0.8f);
            energy = 0.2f + 0.2f * Mathf.Sin(_time * 20f);
        }
        else if (enemy.CharmTimer > 0f)
        {
            albedo = new Color(0.6f, 0.25f, 0.45f);
            emission = new Color(1f, 0.4f, 0.8f);
            energy = 0.6f;
        }
        if (enemy.BurnTimer > 0f)
        {
            emission = new Color(1f, 0.45f, 0.1f);
            energy = Mathf.Max(energy, 0.8f + 0.6f * Mathf.Sin(_time * 25f));
        }
        else if (enemy.PoisonTimer > 0f)
        {
            albedo = albedo.Lerp(new Color(0.3f, 0.7f, 0.2f), 0.6f);
            emission = new Color(0.4f, 1f, 0.3f);
            energy = Mathf.Max(energy, 0.35f);
        }
        if (enemy.HurtTimer > 0f)
        {
            emission = Colors.White;
            energy = 1.5f;
        }
        _body.AlbedoColor = albedo;
        _body.Emission = emission;
        _body.EmissionEnergyMultiplier = energy;
    }
}
