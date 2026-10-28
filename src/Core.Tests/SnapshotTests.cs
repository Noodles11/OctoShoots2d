using System;
using System.IO;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Saves;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>A level saved in play comes back exactly: restored, it carries on step for step as the one that was saved.</summary>
public class SnapshotTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(LevelMap map) =>
        new(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true, Corruption = true });

    /// <summary>Swimming about and shooting, the same way on both sides.</summary>
    static void Play(PlaneWorld w, int from, int steps)
    {
        for (int i = from; i < from + steps; i++)
        {
            w.Player.Hp = MathF.Max(w.Player.Hp, 50f);
            float a = i * 0.004f;
            w.Step(new PlaneInput
            {
                Move = new Vector2(MathF.Cos(a), MathF.Sin(a * 1.3f)),
                Aim = Vector2.Normalize(new Vector2(MathF.Cos(a * 3f), MathF.Sin(a * 3f))),
                Fire = i % 150 < 100,
                Dash = i % 400 == 0,
            });
        }
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(4, false)]
    [InlineData(3, true)]
    public void A_level_saved_in_play_carries_on_exactly_as_it_would_have(int depth, bool boss)
    {
        var plan = TestLevels.Find(p => p.Id.Depth == depth && p.HasBoss == boss);
        var map = TestLevels.Get(plan);
        var played = World(map);
        var playedAnchors = played.SaveAnchors();
        Play(played, 0, 1800);
        byte[] state = played.SaveState(playedAnchors);

        // The same level made again (from its seed), and put back as it was.
        var restored = World(TestLevels.Get(plan));
        var restoredAnchors = restored.SaveAnchors();
        restored.LoadState(restoredAnchors, state);
        Assert.Equal(played.Tick, restored.Tick);
        Assert.Equal(played.Player.Position, restored.Player.Position);
        // (The last step's events have been seen: a restored level starts without them.)
        played.Events.Clear();
        Same(Snapshot.Describe(playedAnchors), Snapshot.Describe(restoredAnchors));

        // Ten seconds on, the same.
        Play(played, 1800, 600);
        Play(restored, 1800, 600);
        Assert.Equal(played.Player.Position, restored.Player.Position);
        Assert.Equal(played.Murklings.Count, restored.Murklings.Count);
        Assert.Equal(played.Events.Count, restored.Events.Count);
        Same(Snapshot.Describe(playedAnchors), Snapshot.Describe(restoredAnchors));
    }

    /// <summary>Two states the same, line for line (the first that differs, if not).</summary>
    static void Same(string expected, string actual)
    {
        var a = expected.Split('\n');
        var b = actual.Split('\n');
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            Assert.True(a[i] == b[i], $"line {i}: expected «{a[i].Trim()}», got «{b[i].Trim()}»");
        Assert.Equal(a.Length, b.Length);
    }

    [Fact]
    public void A_state_from_another_level_is_refused()
    {
        var a = World(TestLevels.Get(TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss)));
        byte[] state = a.SaveState(a.SaveAnchors());
        var b = World(TestLevels.Get(TestLevels.Find(p => p.Id.Depth == 4 && !p.HasBoss)));
        Assert.Throws<SaveException>(() => b.LoadState(b.SaveAnchors(), state));
    }
}
