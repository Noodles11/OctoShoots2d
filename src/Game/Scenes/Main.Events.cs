using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core;
using OctoShoots.Core.Items;
using OctoShoots.Core.Saves;
using OctoShoots.Core.Sim;
using OctoShoots.Game.Settings;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.Scenes;

/// <summary>Simulation events → sound, particles, camera, viewmodel and HUD.</summary>
public partial class Main
{
    static readonly Color Ink = new(0.06f, 0.05f, 0.12f, 0.65f);
    static readonly Color InkGone = new(0.06f, 0.05f, 0.12f, 0f);
    static readonly Color Foam = new(0.85f, 0.97f, 1f, 0.6f);
    static readonly Color FoamGone = new(0.85f, 0.97f, 1f, 0f);

    /// <summary>A bubble bursting: a ring of droplets and a few tiny bubbles drifting up.</summary>
    void Pop(Vector3 pos, Vector3 dir, float size = 1f)
    {
        _sparks.Burst(pos, dir, (int)(10 * size), 3f * size, 1f, 0.3f, 0.06f * size, 0.01f, new Color(0.8f, 0.95f, 1f, 0.9f), new Color(0.6f, 0.85f, 1f, 0f), 4f);
        _ink.Burst(pos, Vector3.Up, (int)(6 * size), 0.8f, 1f, 0.9f, 0.04f, 0.07f, Foam, FoamGone, 1.5f, 1f);
    }

    static Vector3 Audible(Vector3 pos, Vector3 listener)
    {
        Vector3 away = pos - listener;
        float d = away.Length();
        return d > 25f ? listener + away / d * 25f : pos;
    }

    static string NoticeSound(string? kind) => kind switch
    {
        "Barracuda" => "hiss",
        "Pufferling" => "squeak",
        "Crabby" or "SeaUrchin" or "Moray" => "click",
        _ => "gurgle",
    };

    void HandleEvents()
    {
        var vm = _camera.Viewmodel;
        Vector3 playerPos = _world.Player.Position.G();
        foreach (var ev in _world.Events)
        {
            Vector3 pos = ev.Position.G();
            Vector3 dir = ev.Direction.G();
            switch (ev.Type)
            {
                case SimEventType.ShotFired:
                    vm.OnThrow();
                    if (ev.Tag == "beam") _sfx.Play("beam", -2f, 0f);
                    else _sfx.Play("throw", ev.Tag == "pearl" ? 0f : -3f, ev.Tag == "pearl" ? 0f : 0.08f);
                    if (ev.Tag != "beam")
                        _ink.Burst(pos, dir, 4, 1.2f, 0.7f, 0.5f, 0.03f, 0.05f, Foam, FoamGone, 2f, 0.8f);
                    break;
                case SimEventType.BubblesEmpty:
                    _sfx.Play("nope", -14f, 0f);
                    break;
                case SimEventType.ShotHitEnemy:
                    _hud.OnHit();
                    _sfx.PlayAt("hit", pos, 2f);
                    _sfx.PlayAt("pop", pos, 0f);
                    _sparks.Burst(pos, -dir, 14, 5f, 0.8f, 0.35f, 0.12f, 0.02f, new Color(0.6f, 0.9f, 1f, 1f), new Color(0.3f, 0.5f, 1f, 0f));
                    Pop(pos, -dir);
                    break;
                case SimEventType.EnemyCrit:
                    _sparks.Burst(pos, Vector3.Up, 24, 6f, 1f, 0.4f, 0.18f, 0.02f, new Color(1f, 0.95f, 0.5f, 1f), new Color(1f, 0.6f, 0.2f, 0f));
                    _sfx.PlayAt("hit", pos, 5f, 0f);
                    break;
                case SimEventType.ShotHitTerrain:
                    bool fromPlayer = ev.Value > 0.5f;
                    if (fromPlayer)
                    {
                        // Bubbles just burst against rock; only creature orbs scorch it.
                        _sfx.PlayAt("pop", pos, -4f);
                        Pop(pos, dir);
                    }
                    else if (ev.Tag == "star")
                    {
                        // A starfish thunks into the rock: a few orange specks, no scorch.
                        _sfx.PlayAt("pop", pos, -6f);
                        _sparks.Burst(pos, dir, 6, 1.8f, 0.8f, 0.3f, 0.06f, 0.01f, new Color(1f, 0.6f, 0.3f, 1f), new Color(1f, 0.3f, 0.1f, 0f));
                    }
                    else
                    {
                        _stains.Add(pos, dir, false);
                        _sfx.PlayAt("splat", pos, -6f);
                        _sparks.Burst(pos, dir, 10, 2.5f, 0.8f, 0.4f, 0.1f, 0.02f, new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.5f, 0.2f, 0f));
                    }
                    break;
                case SimEventType.ShotExpired:
                    if (ev.Value > 0.5f)
                    {
                        _sfx.PlayAt("pop", pos, -10f);
                        Pop(pos, dir, 0.6f);
                    }
                    else
                        _sparks.Burst(pos, dir, 6, 1f, 1f, 0.3f, 0.12f, 0.02f, new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.5f, 0.2f, 0f));
                    break;
                case SimEventType.ShotBounced:
                    _sfx.PlayAt("bounce", pos, -4f);
                    _sparks.Burst(pos, dir, 6, 2f, 0.7f, 0.25f, 0.1f, 0.02f, new Color(0.7f, 0.9f, 1f, 1f), new Color(0.4f, 0.6f, 1f, 0f));
                    break;
                case SimEventType.Explosion:
                    float radius = ev.Value;
                    _sfx.PlayAt("boom", pos, ev.Tag == "steam" ? 2f : 0f);
                    Color hot = ev.Tag == "steam" ? new Color(0.9f, 0.95f, 1f, 1f) : new Color(0.75f, 0.55f, 1f, 1f);
                    _sparks.Burst(pos, Vector3.Up, 30, radius * 5f, 1f, 0.45f, 0.22f, 0.02f, hot, new Color(hot, 0f), 4f);
                    _ink.Burst(pos, Vector3.Up, 18, radius * 1.5f, 1f, 1.3f, radius * 0.4f, radius * 1.1f,
                        ev.Tag == "steam" ? new Color(0.8f, 0.85f, 0.9f, 0.5f) : new Color(0.05f, 0.04f, 0.11f, 0.75f),
                        ev.Tag == "steam" ? new Color(0.8f, 0.85f, 0.9f, 0f) : new Color(0.05f, 0.04f, 0.11f, 0f), 2.5f, 0.2f);
                    if (ev.Tag == "bomb")
                    {
                        _sfx.PlayAt("boom", pos, 6f, 0f);
                        _camera.AddTrauma(Mathf.Clamp(1f - pos.DistanceTo(playerPos) / 20f, 0f, 1f) * 0.8f);
                    }
                    else if (pos.DistanceTo(playerPos) < 8f)
                    {
                        _camera.AddTrauma(0.25f);
                    }
                    break;
                case SimEventType.TerrainCarved:
                    _sun.MarkDirty();
                    _terrain.Remesh(ev.Position, ev.Value);
                    if (ev.Value >= 0.5f)
                    {
                        // Rock dust and silt settling out of the new crater.
                        int n = (int)(ev.Value * 14f);
                        _ink.Burst(pos, Vector3.Up, n, ev.Value * 2.5f, 1f, 2.2f, ev.Value * 0.35f, ev.Value * 0.9f,
                            new Color(0.45f, 0.5f, 0.52f, 0.7f), new Color(0.45f, 0.5f, 0.52f, 0f), 2.5f, -0.25f);
                        _sparks.Burst(pos, Vector3.Up, n / 2, ev.Value * 4f, 1f, 0.5f, 0.08f, 0.02f,
                            new Color(0.9f, 0.85f, 0.7f, 1f), new Color(0.6f, 0.55f, 0.4f, 0f), 3f, -2f);
                    }
                    break;
                case SimEventType.BombDropped:
                    _sfx.Play("bounce", -4f, 0f);
                    _camera.Viewmodel.OnThrow();
                    break;
                case SimEventType.ChainArc:
                    _lines.ShowArc(pos, pos + dir);
                    _sfx.PlayAt("zap", pos + dir, -2f);
                    break;
                case SimEventType.BeamTick:
                    bool prism = ev.Tag == "prism";
                    _lines.ShowBeam(pos, dir, ev.Value, prism ? 0.15f : 0.05f, prism ? 0.6f : 1f);
                    if (_ink.RandRange(0f, 1f) < 0.5f)
                        _sparks.Emit(pos + dir * ev.Value, dir * -2f + _sparks.RandDir(), 0.3f, 0.15f, 0.02f, new Color(1f, 0.9f, 0.5f, 1f), new Color(1f, 0.6f, 0.2f, 0f));
                    break;
                case SimEventType.StatusApplied:
                    OnStatus(ev.Tag, pos);
                    break;
                case SimEventType.KrakenSlam:
                    _sfx.PlayAt("boom", pos, 4f, 0f);
                    _ink.Burst(pos + Vector3.Up * 2f, Vector3.Down, 30, 6f, 0.5f, 1.2f, 0.4f, 1.4f, new Color(0.08f, 0.04f, 0.12f, 0.8f), new Color(0.08f, 0.04f, 0.12f, 0f), 2f);
                    _camera.AddTrauma(0.3f);
                    break;
                case SimEventType.EnemyTelegraph:
                    _sfx.PlayTelegraph(Audible(pos, playerPos), ev.Value);
                    break;
                case SimEventType.CreatureNoticed:
                    // Heard from beyond the mist: the call comes from its direction, never fainter than 25 m.
                    _sfx.PlayAt(NoticeSound(ev.Tag), Audible(pos, playerPos), 2f);
                    break;
                case SimEventType.EnemyShotFired:
                    switch (ev.Tag)
                    {
                        case "star": _sfx.PlayAt("star", pos, 0f); break;
                        case "spore": _sfx.PlayAt("gurgle", pos, 3f); break;
                        case "burst":
                            _sfx.PlayAt("pop", pos, 6f);
                            _sparks.Burst(pos, Vector3.Up, 18, 4f, 1f, 0.4f, 0.1f, 0.02f, new Color(1f, 0.95f, 0.5f, 1f), new Color(1f, 0.7f, 0.2f, 0f), 3f, 0.4f);
                            break;
                        case "bloom": _sfx.PlayAt("click", pos, 3f); break;
                        case "charge": _sfx.PlayAt("hiss", pos, 3f); break;
                        case "leap": _sfx.PlayAt("bounce", pos, 2f); break;
                        case "lunge": _sfx.PlayAt("hiss", pos, 2f); break;
                        default: _sfx.PlayAt("enemy_shot", pos, 0f); break;
                    }
                    break;
                case SimEventType.NinjaEmerged:
                    _sfx.PlayAt("emerge", pos, -2f);
                    Pop(pos, dir, 1.6f);
                    _ink.Burst(pos, dir, 10, 1.4f, 0.8f, 1.1f, 0.05f, 0.09f, Foam, FoamGone, 1.5f, 1.2f);
                    break;
                case SimEventType.EnemyDied:
                    _sfx.PlayAt("die", pos, 3f, 0f);
                    bool shatter = ev.Tag == "shatter";
                    if (shatter) _sfx.PlayAt("freeze", pos, 4f, 0f);
                    Color bright = shatter ? new Color(0.8f, 0.95f, 1f, 1f) : new Color(0.5f, 1f, 1f, 1f);
                    _sparks.Burst(pos, Vector3.Up, 40, 4f, 1f, 0.8f, 0.15f, 0.02f, bright, new Color(0.2f, 0.4f, 1f, 0f), 2f, 0.4f);
                    _ink.Burst(pos, Vector3.Up, 16, 1.2f, 1f, 1.4f, 0.3f, 0.9f, new Color(0.1f, 0.12f, 0.18f, 0.6f), new Color(0.1f, 0.12f, 0.18f, 0f), 2f, 0.2f);
                    break;
                case SimEventType.PlayerHurt:
                    vm.OnHurt();
                    _hurtPulse = 1f;
                    _camera.AddTrauma(0.6f);
                    _sfx.Play("hurt", 0f, 0.03f);
                    break;
                case SimEventType.ShieldBlocked:
                    _sfx.Play("bounce", 0f, 0f);
                    _sparks.Burst(pos, -dir, 16, 3f, 0.6f, 0.35f, 0.14f, 0.02f, new Color(0.5f, 0.95f, 1f, 1f), new Color(0.3f, 0.7f, 1f, 0f));
                    break;
                case SimEventType.PlayerDied:
                    _camera.AddTrauma(1f);
                    _shots.Clear();
                    _save.Profile.Stats.Deaths++;
                    _save.Profile.Stats.DeathsByCause["lanternfish"] = _save.Profile.Stats.DeathsByCause.GetValueOrDefault("lanternfish") + 1;
                    SaveStore.Write(_save);
                    break;
                case SimEventType.DashStarted:
                    vm.OnDash();
                    _camera.AddTrauma(0.15f);
                    _sfx.Play("dash", -2f);
                    for (int i = 0; i < 26; i++)
                    {
                        Vector3 offset = _ink.RandDir() * _tuning.InkCloudRadius * _ink.RandRange(0.1f, 0.7f);
                        _ink.Emit(pos + offset, offset * 0.6f - dir * 1.2f, _tuning.InkCloudLife, 0.4f, 1.6f,
                            new Color(0.05f, 0.04f, 0.11f, 0.75f), new Color(0.05f, 0.04f, 0.11f, 0f), 1.8f, 0.25f);
                    }
                    _sparks.Burst(pos, -dir, 10, 3f, 0.7f, 0.25f, 0.2f, 0.02f, new Color(0.7f, 0.5f, 1f, 0.9f), new Color(0.5f, 0.3f, 1f, 0f));
                    break;
                case SimEventType.JetStarted:
                    vm.OnStroke();
                    _sfx.Play("jet", -6f);
                    break;
                case SimEventType.Landed:
                    if (_world.Player.Velocity.Length() < 3f)
                    {
                        _sfx.Play("land", -10f);
                        _ink.Burst(pos, Vector3.Up, 10, 0.8f, 0.9f, 1.1f, 0.15f, 0.5f, new Color(0.75f, 0.68f, 0.5f, 0.5f), new Color(0.75f, 0.68f, 0.5f, 0f), 2f, 0.05f);
                    }
                    break;
                case SimEventType.ShellOpened:
                    if (ev.Tag is not null) _sfx.PlayAt("chime", pos, -2f, 0f);
                    break;
                case SimEventType.TooPoor:
                    _hud.ShowBanner($"◎ {(int)ev.Value} sand dollars", $"You have {_world.Coins}. Shoot chests and creatures for more.", new Color(1f, 0.8f, 0.4f), 2f);
                    _sfx.Play("nope", -4f, 0f);
                    break;
                case SimEventType.ChestOpened:
                    _sfx.PlayAt("boom", pos, -6f, 0.1f);
                    _sfx.PlayAt("coin", pos, 0f, 0f);
                    _ink.Burst(pos + Vector3.Up * 0.3f, Vector3.Up, 14, 3f, 0.8f, 0.9f, 0.08f, 0.2f, new Color(0.45f, 0.3f, 0.15f, 0.9f), new Color(0.45f, 0.3f, 0.15f, 0f), 3f, -1.5f);
                    _sparks.Burst(pos + Vector3.Up * 0.4f, Vector3.Up, 20, 3f, 0.6f, 0.6f, 0.1f, 0.02f, new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.6f, 0.2f, 0f), 2f, 0.2f);
                    break;
                case SimEventType.PickupCollected:
                    _sfx.Play(ev.Value > 0 ? "coin" : "chime", -6f, 0.05f);
                    _sparks.Burst(pos, Vector3.Up, 6, 1.5f, 1f, 0.3f, 0.08f, 0.02f, new Color(1f, 0.9f, 0.6f, 1f), new Color(1f, 0.7f, 0.3f, 0f));
                    break;
                case SimEventType.CoinsDugUp:
                    _sfx.PlayAt("coin", pos, 2f, 0f);
                    _sparks.Burst(pos, Vector3.Up, 16, 3f, 0.6f, 0.6f, 0.1f, 0.02f, new Color(1f, 0.85f, 0.35f, 1f), new Color(1f, 0.6f, 0.2f, 0f), 2f, 0.3f);
                    break;
                case SimEventType.ItemGained when ev.Tag is not null:
                    OnItemGained(ev.Tag);
                    break;
                case SimEventType.SynergyActivated when ev.Tag is not null:
                    var synergy = _catalog.Synergies.First(s => s.Id == ev.Tag);
                    _hud.ShowBanner($"★ {synergy.Name} ★", synergy.Description, new Color(1f, 0.85f, 0.3f));
                    _sfx.Play("synergy", 0f, 0f);
                    if (_world.Loadout.Synergies.Count >= 3 && _save.Profile.Award(Achievements.ThreeSynergies, customSeed: false))
                    {
                        _hud.ShowBanner("Achievement: Three of a Kind", "Mitosis can now appear in treasure pools", new Color(0.6f, 1f, 0.7f));
                        SaveStore.Write(_save);
                    }
                    break;
                case SimEventType.TransformationActivated when ev.Tag is not null:
                    var form = _catalog.Transformations.First(t => t.Id == ev.Tag);
                    _hud.ShowBanner($"✦ {form.Name} ✦", form.Description, new Color(0.85f, 0.55f, 1f), 4.5f);
                    _sfx.Play("synergy", 3f, 0f);
                    break;
                case SimEventType.ActiveUsed:
                    _sfx.Play("active", 0f, 0f);
                    _camera.Viewmodel.OnDash();
                    if (ev.Value < 0.5f && ev.Tag is not null)
                        _hud.ShowBanner(_catalog[ev.Tag].Name, "Does nothing yet: its map, pedestal, pickup or terrain system arrives in a later step", new Color(0.7f, 0.7f, 0.7f), 3f);
                    break;
                case SimEventType.ActiveNotReady:
                    _sfx.Play("nope", -4f, 0f);
                    if (ev.Tag == "bomb") _hud.ShowBanner("No ink bombs", "F1 → +5 ink bombs (pickups arrive in step 5)", new Color(0.7f, 0.7f, 0.7f), 1.5f);
                    break;
                case SimEventType.ActiveCharged:
                    _hud.OnActiveCharged();
                    _sfx.Play("item", -8f, 0f);
                    break;
            }
        }
    }

    void OnStatus(string? status, Vector3 pos)
    {
        Color c = status switch
        {
            "freeze" => new Color(0.6f, 0.9f, 1f, 1f),
            "burn" => new Color(1f, 0.5f, 0.15f, 1f),
            "poison" => new Color(0.45f, 1f, 0.3f, 1f),
            "charm" => new Color(1f, 0.45f, 0.85f, 1f),
            _ => new Color(0.9f, 0.9f, 0.6f, 1f),
        };
        if (status == "freeze") _sfx.PlayAt("freeze", pos, 0f);
        _sparks.Burst(pos, Vector3.Up, 12, 2f, 1f, 0.5f, 0.12f, 0.02f, c, new Color(c, 0f), 2f, 0.3f);
    }

    void OnItemGained(string id)
    {
        var item = _catalog[id];
        _hud.ShowItem(item.Name, item.Tagline, ItemCaption.Describe(item));
        _sfx.Play("item", 0f, 0f);
        _sparks.Burst(_world.Player.Position.G() + MathUtil.Forward(_yaw, _pitch).G() * 1.2f, Vector3.Up, 24, 2.5f, 1f, 0.8f, 0.12f, 0.02f,
            new Color(1f, 0.85f, 0.5f, 1f), new Color(1f, 0.6f, 0.3f, 0f), 2f, 0.3f);

        var profile = _save.Profile;
        profile.SeenItems.Add(id);
        profile.Stats.ItemPickups[id] = profile.Stats.ItemPickups.GetValueOrDefault(id) + 1;
        SaveStore.Write(_save);
    }
}

