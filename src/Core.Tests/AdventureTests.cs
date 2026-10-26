using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The adventure (docs/ADVENTURE.md): vents, ink dots, fish that turn, the three murklings, her inner light.</summary>
public class AdventureTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly PlaneOptions Adventure = new() { Adventure = true };

    static PlaneWorld World(bool boss = false)
    {
        var plan = TestLevels.Find(p => p.Id.Depth == 2 && p.HasBoss == boss);
        return new PlaneWorld(TestLevels.Get(plan), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), Adventure);
    }

    static void Run(PlaneWorld w, int ticks, PlaneInput input = default)
    {
        for (int i = 0; i < ticks; i++)
        {
            w.Player.Hp = 100f;
            w.Step(input);
        }
    }

    /// <summary>A world with its fish, anemones and vents taken away (each test brings what it needs).</summary>
    static PlaneWorld Bare()
    {
        var w = World();
        w.ReefFish.Clear();
        w.Anemones.Clear();
        w.Schools.Clear();
        foreach (var v in w.Vents) v.Sealed = true;
        return w;
    }

    /// <summary>Open water round a point: every cell of a ring of dots there may hold one.</summary>
    static Vector2 OpenSpot(PlaneWorld w, float awayFromHer = 15f)
    {
        var dots = w.Dots!;
        for (int i = 0; i < InkDots.N * InkDots.N; i += 37)
        {
            var c = InkDots.Centre(i);
            if (Vector2.Distance(c, w.Player.Position) < awayFromHer || !w.Clear(c, 2.5f)) continue;
            bool all = true;
            for (int dy = -2; dy <= 2 && all; dy++)
            for (int dx = -2; dx <= 2 && all; dx++)
                all = dots.Open[i + dx + dy * InkDots.N];
            if (all) return c;
        }
        throw new InvalidOperationException("no open water");
    }

    [Fact]
    public void A_level_starts_with_vents_fish_and_no_creatures()
    {
        foreach (bool boss in new[] { false, true })
        {
            var w = World(boss);
            Assert.InRange(w.Vents.Count, AdventureTuning.VentsMin, AdventureTuning.VentsMax);
            Assert.False(w.ExitOpen);
            Assert.Empty(w.Murklings);
            Assert.Empty(w.Blightroots);
            Assert.Empty(w.Blotters);
            Assert.Empty(w.Lashjaws);
            Assert.Empty(w.Thumpsquirts);
            Assert.Empty(w.Inkwardens);
            Assert.Null(w.Boss);
            Assert.True(w.ReefFish.Count > 60, $"only {w.ReefFish.Count} fish");
            Assert.Equal(0, w.Dots!.Count);
            // Every vent's mouth is in open water before a wall, apart from the others.
            foreach (var v in w.Vents)
            {
                Assert.True(w.Clear(v.Position, 0.3f));
                Assert.False(w.Map.IsOpen(v.Wall));
                Assert.True(Vector2.Distance(v.Position, w.Map.Start.Position) >= AdventureTuning.VentFromStart);
            }
        }
    }

    [Fact]
    public void An_abyss_reef_drops_its_floor_and_its_anemones_grow_on_shelves()
    {
        var id = TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss);
        // (Its own copy: the abyss changes the map.)
        var map = AbyssReef.Apply(OctoShoots.Core.Gen.TopDown.TopDownGenerator.Generate(new OctoShoots.Core.Run.RunStreams(TestLevels.Seed), id));
        Assert.True(map.Abyss);
        Assert.NotEmpty(map.Shelves);
        Assert.All(map.Shelves, s => Assert.True(map.HeightAt(s) >= AbyssReef.ShelfDepth - 0.05f && map.IsOpen(s)));
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), Adventure);
        Assert.NotEmpty(w.Anemones);
        Assert.All(w.Anemones, a =>
        {
            Assert.True(map.HeightAt(a.Position) >= AbyssReef.ShelfDepth - 0.5f, $"an anemone over the abyss at {a.Position}");
            Assert.Contains(map.Shelves, s => Vector2.Distance(s, a.Position) < 1.5f);
        });
        // Away from the walls, shelves, caves and tunnels, there is no seabed.
        Assert.Contains(Enumerable.Range(0, 140 * 140), i => map.IsOpen(new Vector2(i % 140, i / 140)) && map.HeightAt(new Vector2(i % 140, i / 140)) < -20f);
    }

    [Fact]
    public void Open_vents_pour_ink_and_sealed_ones_stop()
    {
        var w = World();
        w.ReefFish.Clear();
        w.Anemones.Clear();
        Run(w, 5 * PlaneWorld.TickRate);
        int poured = w.Dots!.Count;
        Assert.True(poured >= 3 * w.Vents.Count, $"only {poured} dots after 5 s");
        // Each lies near a vent, in a cell the ink may hold.
        foreach (int i in w.Dots.Live)
        {
            Assert.True(w.Dots.Open[i]);
            Assert.Contains(w.Vents, v => Vector2.Distance(v.Position, InkDots.Centre(i)) < 12f);
        }
        foreach (var v in w.Vents) w.DebugSealVent(v);
        Assert.True(w.ExitOpen);
        // Sealed, they pour no more, and each one's ring of light has cleared the ink at its mouth (the rest stays).
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        Run(w, (int)(1.5f * PlaneWorld.TickRate));
        foreach (var v in w.Vents)
            Assert.DoesNotContain(w.Dots.Live, i => Vector2.Distance(InkDots.Centre(i), v.Position) < 2f);
    }

    [Fact]
    public void A_vent_seals_with_hits_in_a_row_and_its_light_drains_if_they_stop()
    {
        var w = World();
        var v = w.Vents[0];
        for (int i = 0; i < v.Needed - 2; i++) w.DebugStrikeVent(v);
        Assert.False(v.Sealed);
        int lit = v.Hits;
        Run(w, 3 * PlaneWorld.TickRate);
        Assert.True(v.Hits < lit, "its light should drain once her hits stop");
        for (int i = 0; i < v.Needed && !v.Sealed; i++) w.DebugStrikeVent(v);
        Assert.True(v.Sealed);
        Assert.False(w.ExitOpen);
        foreach (var o in w.Vents.Skip(1)) w.DebugSealVent(o);
        Assert.True(w.ExitOpen);
    }

    [Fact]
    public void Her_shots_at_a_vent_light_it()
    {
        var w = World();
        w.ReefFish.Clear();
        var v = w.Vents[0];
        // Before it, facing it.
        var at = v.Position + v.Facing * 2.5f;
        Assert.True(w.Clear(at, w.Radius), "no room before the vent");
        w.Player.Position = w.Player.PrevPosition = at;
        var aim = Vector2.Normalize(v.Wall - at);
        int lit = 0;
        for (int i = 0; i < 2 * PlaneWorld.TickRate; i++)
        {
            w.Player.Position = at;
            w.Player.Velocity = Vector2.Zero;
            w.Step(new PlaneInput { Aim = aim, Fire = true });
            lit += w.Events.Count(e => e.Type == PlaneEventType.VentLit);
        }
        Assert.True(lit >= 3, $"only {lit} shots lit it");
    }

    [Fact]
    public void Ink_fed_by_a_vent_divides_only_into_open_cells_and_never_through_rock()
    {
        var w = World();
        w.ReefFish.Clear();
        w.Anemones.Clear();
        foreach (var v in w.Vents.Skip(1)) v.Sealed = true;
        Run(w, 40 * PlaneWorld.TickRate);
        var dots = w.Dots!;
        // More than the vent alone pours: it has spread.
        Assert.True(dots.Count > 40 / AdventureTuning.VentEvery + 4, $"only {dots.Count} dots");
        // The patch is one piece, joined only across links (no hop through rock).
        var seen = new System.Collections.Generic.HashSet<int> { dots.Live[0] };
        var todo = new System.Collections.Generic.Stack<int>(seen);
        while (todo.Count > 0)
        {
            int c = todo.Pop();
            for (int k = 0; k < 4; k++)
            {
                int n = dots.Neighbour(c, k);
                if (n >= 0 && dots.Has(n) && seen.Add(n)) todo.Push(n);
            }
        }
        Assert.Equal(dots.Count, seen.Count);
        Assert.All(dots.Live, i => Assert.True(dots.Open[i]));
    }

    [Fact]
    public void Ink_cut_off_from_every_working_vent_stops_spreading()
    {
        // A patch nowhere near a vent never grows.
        var w = Bare();
        var at = OpenSpot(w);
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
            w.DebugDot(at + new Vector2(dx, dy) * AdventureTuning.Spacing);
        Run(w, 25 * PlaneWorld.TickRate);
        Assert.Equal(9, w.Dots!.Count);

        // A vent's patch grows while it pours; sealed, what it left behind stops.
        w = World();
        w.ReefFish.Clear();
        w.Anemones.Clear();
        foreach (var v in w.Vents.Skip(1)) v.Sealed = true;
        Run(w, 30 * PlaneWorld.TickRate);
        w.DebugSealVent(w.Vents[0]);
        Run(w, PlaneWorld.TickRate);
        int left = w.Dots!.Count;
        Run(w, 25 * PlaneWorld.TickRate);
        Assert.Equal(left, w.Dots.Count);
    }

    [Fact]
    public void Her_light_clears_the_dots_she_swims_through()
    {
        var w = Bare();
        var at = OpenSpot(w);
        foreach (var d in new[] { Vector2.Zero, Vector2.UnitX * AdventureTuning.Spacing, -Vector2.UnitX * AdventureTuning.Spacing, Vector2.UnitY * AdventureTuning.Spacing })
            Assert.True(w.DebugDot(at + d));
        w.Player.Position = w.Player.PrevPosition = at;
        int cleared = 0;
        for (int i = 0; i < PlaneWorld.TickRate; i++)
        {
            w.Step(default);
            cleared += w.Events.Count(e => e.Type == PlaneEventType.DotCleared);
        }
        Assert.Equal(4, cleared);
        Assert.Equal(0, w.Dots!.Count);
    }

    [Fact]
    public void Her_shots_clear_every_dot_along_their_flight()
    {
        var w = Bare();
        var her = OpenSpot(w);
        w.Player.Position = w.Player.PrevPosition = her;
        // A line of dots ahead of her, out past her light's reach; she fires one shot along it.
        int laid = 0;
        for (float d = 3.2f; d < 7f; d += AdventureTuning.Spacing)
            if (w.Clear(her + Vector2.UnitX * d, 0.3f) && w.DebugDot(her + Vector2.UnitX * d)) laid++;
        Assert.True(laid >= 3, $"only {laid} dots laid");
        int cleared = 0;
        w.Step(new PlaneInput { Aim = Vector2.UnitX, Fire = true });
        cleared += w.Events.Count(e => e.Type == PlaneEventType.DotCleared);
        for (int i = 0; i < PlaneWorld.TickRate; i++)
        {
            w.Player.Position = her;
            w.Player.Velocity = Vector2.Zero;
            w.Step(new PlaneInput { Aim = Vector2.UnitX });
            cleared += w.Events.Count(e => e.Type == PlaneEventType.DotCleared);
        }
        Assert.Equal(laid, cleared);
        Assert.Equal(0, w.Dots!.Count);
    }

    [Fact]
    public void A_fish_in_the_ink_fills_and_turns_into_its_murkling()
    {
        foreach (var kind in new[] { ReefFishKind.BlueTang, ReefFishKind.Clownfish })
        {
            var w = World();
            var fish = w.ReefFish.First(f => f.Kind == kind);
            foreach (var o in w.ReefFish.Where(o => o != fish)) o.Freed = true;
            foreach (var v in w.Vents) v.Sealed = true;
            // Ink all round it, kept up as it eats.
            for (int t = 0; t < 30 * PlaneWorld.TickRate && !fish.Corrupted; t++)
            {
                for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    w.DebugDot(fish.Position + new Vector2(dx, dy) * AdventureTuning.Spacing);
                w.Player.Hp = 100f;
                w.Step(default);
            }
            Assert.True(fish.Corrupted, $"the {kind} never turned");
            var m = w.Murklings.Single(x => x.Fish == fish);
            Assert.Equal(kind == ReefFishKind.BlueTang ? AdventureTuning.SpikedRadius : AdventureTuning.TadpoleRadius, m.Radius);
        }
    }

    [Fact]
    public void A_fish_eats_the_dots_it_touches()
    {
        var w = World();
        foreach (var v in w.Vents) v.Sealed = true;
        var fish = w.ReefFish.First(f => f.Kind == ReefFishKind.BlueTang);
        fish.Freed = true;
        w.Player.Position = w.Map.Start.Position;
        w.DebugDot(fish.Position);
        int eaten = 0;
        for (int i = 0; i < 4 && eaten == 0; i++)
        {
            w.Step(default);
            eaten += w.Events.Count(e => e.Type == PlaneEventType.DotEaten);
        }
        Assert.Equal(1, eaten);
    }

    [Fact]
    public void A_school_turns_together_and_its_shoal_circles_her_taking_her_shots()
    {
        var w = World();
        foreach (var v in w.Vents) v.Sealed = true;
        var school = w.ReefFish.First(f => f.Kind == ReefFishKind.Chromis).Host;
        var members = w.ReefFish.Where(f => f.Kind == ReefFishKind.Chromis && f.Host == school).ToList();
        members[0].Ink = 1f;
        // (Full: the next step sets it turning, with all its school.)
        Run(w, 2);
        Assert.All(members, f => Assert.True(f.Turning > 0f || f.Corrupted));
        Run(w, PlaneWorld.TickRate);
        var shoal = w.Murklings.Where(m => m.Swarm == school).ToList();
        Assert.Equal(members.Count, shoal.Count);

        // She stops beside them; they ring her all round, and the ring circles her (whichever way she aims).
        w.Player.Position = w.Player.PrevPosition = shoal[0].Position;
        var her = w.Player.Position;
        float[] Angles() => shoal.Select(m => MathF.Atan2(m.Position.Y - her.Y, m.Position.X - her.X)).ToArray();
        for (int i = 0; i < 2 * PlaneWorld.TickRate; i++)
        {
            w.Player.Position = her;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = 100f;
            w.Step(new PlaneInput { Aim = Vector2.UnitX });
        }
        var before = Angles();
        Assert.All(shoal, m => Assert.InRange(Vector2.Distance(m.Position, her), 0.5f, 4f));
        Assert.All(new[] { 0, 1, 2, 3 }, q => Assert.Contains(before, ang => (int)MathF.Floor((ang + MathF.PI) / (MathF.PI / 2f)) % 4 == q));
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++)
        {
            w.Player.Position = her;
            w.Player.Velocity = Vector2.Zero;
            w.Step(new PlaneInput { Aim = Vector2.UnitX });
        }
        var after = Angles();
        float moved = 0f;
        for (int i = 0; i < shoal.Count; i++) moved += MathF.Abs(MathF.IEEERemainder(after[i] - before[i], MathF.Tau));
        Assert.True(moved / shoal.Count > 0.3f, $"the ring barely turned ({moved / shoal.Count:0.00} rad each in half a second)");
        Assert.Equal(0, w.LesserClingers);

        // Her shot meets one of them.
        int struck = 0;
        for (int i = 0; i < 3 * PlaneWorld.TickRate && struck == 0; i++)
        {
            w.Player.Position = her;
            w.Step(new PlaneInput { Aim = Vector2.UnitX, Fire = true });
            struck += w.Events.Count(e => e.Type is PlaneEventType.MobHit or PlaneEventType.MobDefeated);
        }
        Assert.True(struck > 0, "the shoal should take her shots");
    }

    [Fact]
    public void The_tadpole_chases_her_faster_than_she_cruises_and_clings()
    {
        var w = World();
        foreach (var v in w.Vents) v.Sealed = true;
        var fish = w.ReefFish.First(f => f.Kind == ReefFishKind.Clownfish);
        fish.Ink = 1f;
        Run(w, PlaneWorld.TickRate);
        var m = w.Murklings.Single(x => x.Fish == fish);
        w.Player.Position = w.Player.PrevPosition = m.Position + Vector2.Normalize(new Vector2(1f, 0.3f)) * 6f;
        if (!w.Clear(w.Player.Position, w.Radius)) w.Player.Position = w.Player.PrevPosition = m.Position + new Vector2(0f, 0.01f) + Vector2.UnitY * 3f;
        for (int i = 0; i < 4 * PlaneWorld.TickRate && !m.Clinging; i++)
        {
            w.Player.Hp = 100f;
            w.Player.Velocity = Vector2.Zero;
            w.Step(default);
        }
        Assert.True(m.Clinging, "it should have caught her and clung");
    }

    [Fact]
    public void An_anemone_in_the_ink_becomes_an_inkwarden_and_freed_is_an_anemone_again()
    {
        var w = World();
        foreach (var v in w.Vents) v.Sealed = true;
        Assert.NotEmpty(w.Anemones);
        var an = w.Anemones[0];
        an.Ink = 1f;
        w.DebugDot(an.Position + Vector2.UnitX * AdventureTuning.Spacing);
        if (w.Dots!.Count == 0) w.DebugDot(an.Position - Vector2.UnitX * AdventureTuning.Spacing);
        Run(w, 1);
        Assert.True(an.Turned);
        var warden = w.Inkwardens.Single();
        Assert.Equal(0, warden.Anemone);
        w.Player.Position = w.Player.PrevPosition = warden.Position + Vector2.UnitX * 4f;
        warden.Corruption.Value = 0.5f;
        var aim = Vector2.Normalize(warden.Position - w.Player.Position);
        for (int i = 0; i < PlaneWorld.TickRate && warden.Alive; i++)
        {
            w.Player.Hp = 100f;
            w.Step(new PlaneInput { Aim = aim, Fire = true });
        }
        Assert.False(warden.Alive);
        Assert.False(an.Turned);
        Assert.True(an.Freed);
    }

    [Fact]
    public void Her_inner_light_runs_out_with_shooting_and_comes_back()
    {
        var w = Bare();
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        int shots = 0;
        for (int i = 0; i < 6 * PlaneWorld.TickRate && !w.Player.LightOut; i++)
        {
            w.Step(new PlaneInput { Aim = Vector2.UnitX, Fire = true });
            shots += w.Events.Count(e => e.Type == PlaneEventType.Shot);
        }
        Assert.True(w.Player.LightOut);
        Assert.InRange(shots, 10, 20);
        // Out, she cannot shoot (or dash) until it is back.
        w.Step(new PlaneInput { Aim = Vector2.UnitX, Fire = true, Dash = true });
        Assert.DoesNotContain(w.Events, e => e.Type is PlaneEventType.Shot or PlaneEventType.DashStarted);
        Run(w, (int)(2.5f * PlaneWorld.TickRate));
        Assert.False(w.Player.LightOut);
        Assert.True(w.Player.Light > 0.95f);
    }

    [Fact]
    public void The_adventure_is_deterministic()
    {
        ulong Hash()
        {
            var w = World();
            for (int i = 0; i < 20 * PlaneWorld.TickRate; i++)
                w.Step(new PlaneInput { Move = new Vector2(MathF.Sin(i * 0.01f), 1f), Aim = Vector2.UnitX, Fire = i % 90 < 40 });
            ulong h = w.StateHash();
            foreach (int c in w.Dots!.Live) h = h * 31 + (ulong)c;
            return h ^ (ulong)w.Murklings.Count;
        }
        Assert.Equal(Hash(), Hash());
    }
}
