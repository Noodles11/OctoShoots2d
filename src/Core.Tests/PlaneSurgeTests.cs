using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Firefly Squid's continuous surge, and how her other pearls shape it.</summary>
public class PlaneSurgeTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly Lazy<LevelMap> Room = new(TestLevels.OneTreasure);

    static PlaneWorld World(params string[] pearls)
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        run.Add("firefly_squid");
        foreach (string id in pearls) run.Add(id);
        var w = new PlaneWorld(Room.Value, new Tuning(), run, new PlaneOptions { Corruption = false });
        w.Mobs.Clear();
        w.Pearls.Clear();
        return w;
    }

    static Vector2 OpenAim(PlaneWorld w)
    {
        for (int i = 0; i < 32; i++)
        {
            float a = i * MathF.Tau / 32f;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            if (Enumerable.Range(1, 15).All(k => w.Map.IsOpen(w.Player.Position + d * k))) return d;
        }
        return Vector2.UnitX;
    }

    static PlaneMob Mob(PlaneWorld w, Vector2 at)
    {
        var mob = new PlaneMob { Position = at, Home = at, Rng = new Rng(1), FireTimer = 99f, Hp = 1000f };
        w.Mobs.Add(mob);
        return mob;
    }

    static void Hold(PlaneWorld w, Vector2 aim, float seconds)
    {
        for (int i = 0; i < (int)(seconds * PlaneWorld.TickRate); i++)
        {
            w.Player.Hp = 100f;
            w.Step(new PlaneInput { Aim = aim, Fire = true });
        }
    }

    [Fact]
    public void HeldItPoursOutOneSurgeNoShotsAndItGoesWhenLetGo()
    {
        var w = World();
        var aim = OpenAim(w);
        Hold(w, aim, 0.5f);
        Assert.Empty(w.Shots);
        var surge = Assert.IsType<PlaneSurge>(w.LightSurge);
        var ray = Assert.Single(surge.Rays);
        Assert.InRange(ray.Length, PlaneCombatTuning.BubbleRange - 0.6f, PlaneCombatTuning.BubbleRange + 0.2f);
        w.Step(new PlaneInput { Aim = aim });
        Assert.Null(w.LightSurge);
    }

    [Fact]
    public void ItStrikesTheFirstCreatureAndStopsThereUnlessItPierces()
    {
        foreach (bool pierce in new[] { false, true })
        {
            var w = pierce ? World("swordfish_bill") : World();
            var aim = OpenAim(w);
            var near = Mob(w, w.Player.Position + aim * 4f);
            var far = Mob(w, w.Player.Position + aim * 8f);
            Hold(w, aim, 1f);
            Assert.True(near.Hp < 1000f);
            if (pierce) Assert.True(far.Hp < 1000f, "it runs through");
            else
            {
                Assert.Equal(1000f, far.Hp);
                Assert.InRange(w.LightSurge!.Rays[0].Length, 2.5f, 4.5f);
            }
        }
    }

    [Fact]
    public void ItsDamageMatchesHerShotsOverTime()
    {
        var w = World();
        var aim = OpenAim(w);
        var mob = Mob(w, w.Player.Position + aim * 5f);
        Hold(w, aim, 2f);
        float dps = (1000f - mob.Hp) / 2f;
        float shots = PlaneCombatTuning.ShotDamage / PlaneCombatTuning.ShotInterval;
        Assert.InRange(dps, shots * SurgeTuning.Dps * 0.85f, shots * SurgeTuning.Dps * 1.15f);
    }

    [Fact]
    public void ItDrainsACreatureEveryStep_AndTellsItsHitsSummedAFewTimesASecond()
    {
        var w = World();
        var aim = OpenAim(w);
        var mob = Mob(w, w.Player.Position + aim * 5f);
        Hold(w, aim, 0.1f);
        int hits = 0;
        for (int i = 0; i < 60; i++)
        {
            float before = mob.Hp;
            w.Step(new PlaneInput { Aim = aim, Fire = true });
            Assert.True(mob.Hp < before, "it drains every step");
            hits += w.Events.Count(e => e.Type == PlaneEventType.MobHit);
        }
        Assert.InRange(hits, 3, 5);
    }

    [Fact]
    public void MoreShotsOpenAConeWithNoGaps()
    {
        var w = World("hammerhead");
        var aim = OpenAim(w);
        Hold(w, aim, 0.1f);
        var rays = w.LightSurge!.Rays;
        Assert.True(rays.Count > 5);
        // Neighbouring rays touch at every distance: their widths cover the angle between them.
        var a = rays[0];
        var b = rays[1];
        float s = 5f;
        float apart = Vector2.Distance(a.Points[0] + Vector2.Normalize(a.End - a.Points[0]) * s, b.Points[0] + Vector2.Normalize(b.End - b.Points[0]) * s);
        Assert.True(a.WidthAt(s) + b.WidthAt(s) >= apart);
    }

    [Fact]
    public void DoubleHelixTwistsIt_AndStarburstBurstsIt_AndMitosisForksIt()
    {
        var helix = World("double_helix");
        Hold(helix, OpenAim(helix), 0.1f);
        Assert.True(helix.LightSurge!.Helix);
        Assert.Single(helix.LightSurge.Rays);

        var star = World("ink_sac");
        var aim = OpenAim(star);
        bool burst = false;
        for (int i = 0; i < 60; i++)
        {
            star.Step(new PlaneInput { Aim = aim, Fire = true });
            burst |= star.Events.Any(e => e.Type == PlaneEventType.LightBurst);
        }
        Assert.True(burst);

        var split = World("mitosis");
        aim = OpenAim(split);
        Mob(split, split.Player.Position + aim * 5f);
        Hold(split, aim, 0.1f);
        Assert.Equal(2, split.LightSurge!.Rays.Count(r => r.Fork));
    }

    [Fact]
    public void BoomerangShrimpLoopsItOutToItsReachAndBackToHer()
    {
        var w = World("boomerang_shrimp");
        // Somewhere the whole loop runs through open water (out her range, its widest 22% of that either side, and a margin).
        bool Free(Vector2 q, Vector2 d)
        {
            var side = new Vector2(-d.Y, d.X);
            for (int i = 0; i <= 40; i++)
            {
                float t = i / 40f;
                const float reach = PlaneCombatTuning.BubbleRange + 0.6f;
                var p = q + d * (reach * MathF.Sin(MathF.PI * t)) + side * (reach * SurgeTuning.LoopWidth * MathF.Sin(MathF.Tau * t));
                if (!w.Map.IsOpen(p) || !w.Map.IsOpen(p + side * 0.6f) || !w.Map.IsOpen(p - side * 0.6f)) return false;
            }
            return true;
        }
        var (spot, aim) = Enumerable.Range(0, 6000).Select(i => new Vector2(4f + (i % 75) * 2f, 4f + (i / 75) * 2f))
            .SelectMany(q => Enumerable.Range(0, 8).Select(k => (q, new Vector2(MathF.Cos(k * MathF.Tau / 8f), MathF.Sin(k * MathF.Tau / 8f)))))
            .First(t => Free(t.Item1, t.Item2));
        w.Player.Position = w.Player.PrevPosition = spot - aim * (w.Radius + 0.1f);
        Hold(w, aim, 0.1f);
        var ray = Assert.Single(w.LightSurge!.Rays);
        Assert.True(ray.Loop);
        Assert.False(ray.Blocked);
        var start = ray.Points[0];
        float far = ray.Points.Max(q => Vector2.Dot(q - start, aim));
        Assert.InRange(far, PlaneCombatTuning.BubbleRange - 0.5f, PlaneCombatTuning.BubbleRange + 0.2f);
        Assert.True(ray.Points.Max(q => MathF.Abs(Vector2.Dot(q - start, new Vector2(-aim.Y, aim.X)))) > 1f, "a loop, not a line");
        Assert.True(Vector2.Distance(ray.End, start) < 0.5f, "it comes back to her");
        Assert.InRange(Vector2.Dot(ray.Tip - start, aim), PlaneCombatTuning.BubbleRange - 0.5f, PlaneCombatTuning.BubbleRange + 0.2f);
    }

    [Fact]
    public void PearlDiverFocusesIt()
    {
        var w = World("pearl_diver");
        var aim = OpenAim(w);
        Hold(w, aim, 0.05f);
        float wide = w.LightSurge!.Rays[0].Width;
        Hold(w, aim, 1.2f);
        Assert.True(w.LightSurge!.Rays[0].Width < wide * 0.7f);
    }
}
