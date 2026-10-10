using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Sunken amphorae: where they stand, what breaks them, and what they leave.</summary>
public class PlaneVaseTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly Lazy<LevelMap> Level = new(TestLevels.OneTreasure);

    static PlaneWorld World(params string[] pearls)
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        foreach (string id in pearls) run.Add(id);
        var w = new PlaneWorld(Level.Value, new Tuning(), run);
        w.Mobs.Clear();
        return w;
    }

    /// <summary>A bubble just short of a pot, flying into it.</summary>
    static PlaneShot BubbleAt(PlaneVase vase, float damage, int volley = 1)
    {
        var at = vase.Position - Vector2.UnitX * (vase.Radius + 0.3f);
        return new PlaneShot { Position = at, Line = at, Velocity = Vector2.UnitX * 8f, Speed0 = 8f, Range = 10f, Life = 2f, FromPlayer = true, Damage = damage, Radius = 0.15f, BaseRadius = 0.15f, Volley = volley };
    }

    [Fact]
    public void PotsStandInGroupsOnShallowSeabedClearOfTheStartAndThePlaces()
    {
        var w = World();
        var map = w.Map;
        Assert.InRange(w.Vases.Count, VaseTuning.GroupsMin, VaseTuning.GroupsMax * VaseTuning.PerGroupMax);
        foreach (var v in w.Vases)
        {
            Assert.InRange(map.HeightAt(v.Position), VaseTuning.FloorMin, VaseTuning.FloorMax);
            Assert.True(Vector2.Distance(v.Position, map.Start.Position) >= VaseTuning.StartClear);
            Assert.False(map.Shaft.Contains(v.Position));
            Assert.All(w.Vases.Where(o => o != v), o => Assert.True(Vector2.Distance(o.Position, v.Position) >= o.Radius + v.Radius));
        }
        // Seeded: the same level, the same pots.
        Assert.Equal(w.Vases.Select(v => v.Position), World().Vases.Select(v => v.Position));
    }

    [Fact]
    public void APlainBubbleOnlyRocksAPot()
    {
        var w = World();
        var vase = w.Vases[0];
        w.Shots.Add(BubbleAt(vase, PlaneCombatTuning.ShotDamage));
        for (int i = 0; i < 10; i++) w.Step(default);
        Assert.False(vase.Broken);
    }

    [Fact]
    public void ABigBubbleBreaksAPotAndItMayLeaveSomething()
    {
        // Break every pot of the level, one bubble each: all break, and some leave shells or a heart.
        var w = World();
        int left = 0;
        for (int n = 0; n < w.Vases.Count; n++)
        {
            var vase = w.Vases[n];
            int shells = w.Shells.Count, hearts = w.Hearts.Count;
            w.Shots.Clear();
            w.Shots.Add(BubbleAt(vase, 20f, volley: 100 + n));
            for (int i = 0; i < 10 && !vase.Broken; i++) w.Step(default);
            Assert.True(vase.Broken, $"pot {n}");
            if (w.Shells.Count > shells || w.Hearts.Count > hearts) left++;
        }
        Assert.True(left > 0 && left < w.Vases.Count + 1, $"{left} of {w.Vases.Count} left something");
    }

    [Fact]
    public void TheHooksKnockbackBreaksAPotWithAPlainBubble()
    {
        var w = World("captains_hook");
        var vase = w.Vases[0];
        w.Shots.Add(BubbleAt(vase, PlaneCombatTuning.ShotDamage));
        for (int i = 0; i < 10; i++) w.Step(default);
        Assert.True(vase.Broken);
    }

    [Fact]
    public void AnInkDashRamsAPotToPieces()
    {
        var w = World();
        var vase = w.Vases[0];
        var from = vase.Position - Vector2.UnitX * (vase.Radius + w.Radius + 1.2f);
        if (!w.Clear(from, w.Radius)) from = vase.Position + Vector2.UnitX * (vase.Radius + w.Radius + 1.2f);
        w.Player.Position = w.Player.PrevPosition = from;
        Vector2 dir = Vector2.Normalize(vase.Position - from);
        w.Step(new PlaneInput { Move = dir, Dash = true });
        for (int i = 0; i < 20; i++) w.Step(new PlaneInput { Move = dir });
        Assert.True(vase.Broken);
    }

    [Fact]
    public void PotsBlockSwimmers()
    {
        var w = World();
        var vase = w.Vases[0];
        Assert.False(w.Clear(vase.Position, 0.3f));
    }

    [Fact]
    public void AStrongCurrentCanTopplePots()
    {
        // A pot in a canyon, and a full-strength surge running through it for a long while: sooner or later it breaks.
        var w = World();
        var vase = w.Vases[0];
        int canyon = -1;
        float best = float.MaxValue;
        for (int i = 0; i < w.Map.Corridors.Count; i++)
        {
            ReefDirector.Nearest(w.Map.Corridors[i], vase.Position, out float d, out _);
            if (d - w.Map.Corridors[i].HalfWidth < best) (best, canyon) = (d - w.Map.Corridors[i].HalfWidth, i);
        }
        // Moved into the canyon's channel for the test.
        vase.Position = w.Map.Corridors[canyon].Points[w.Map.Corridors[canyon].Points.Count / 2];
        w.Director.Inject(new ReefEvent { Kind = ReefEventKind.CurrentSurge, Start = 0f, Duration = 200f, Corridor = canyon, Speed = ReefDirectorTuning.SurgeSpeedMax });
        for (int i = 0; i < 160 * PlaneWorld.TickRate && !vase.Broken; i++)
        {
            w.Player.Hp = w.Run.MaxHp;
            w.Step(default);
        }
        Assert.True(vase.Broken);
    }
}
