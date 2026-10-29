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
            // A boss level holds its boss in its arena (Drippus Prime), and nothing else of the war.
            Assert.Equal(boss, w.Boss is not null);
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
        // Away from the walls, shelves, caves and tunnels, the floor lies at the menace's depth (give or take its dunes).
        float depth = AbyssReef.DepthOf(map.Menace);
        Assert.Contains(Enumerable.Range(0, 140 * 140), i => map.IsOpen(new Vector2(i % 140, i / 140)) && MathF.Abs(map.HeightAt(new Vector2(i % 140, i / 140)) - depth) < AbyssReef.DuneHeight);
    }

    [Fact]
    public void The_dropped_floor_sinks_with_the_menace_and_its_shallows_have_dunes()
    {
        Assert.Equal(AbyssReef.ShallowDepth, AbyssReef.DepthOf(0f));
        Assert.Equal(AbyssReef.DeepDepth, AbyssReef.DepthOf(1f));
        Assert.Equal(AbyssReef.DeepDepth, AbyssReef.DepthOf(2.4f));
        Assert.True(AbyssReef.DepthOf(0.4f) < AbyssReef.DepthOf(0.2f));
        var id = TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss);
        var map = AbyssReef.Apply(OctoShoots.Core.Gen.TopDown.TopDownGenerator.Generate(new OctoShoots.Core.Run.RunStreams(TestLevels.Seed), id));
        // The open floor rolls: its heights spread across most of the dunes' height, and never rise to her plane.
        var floor = Enumerable.Range(0, 140 * 140).Select(i => new Vector2(i % 140, i / 140))
            .Where(p => map.IsOpen(p) && map.HeightAt(p) < AbyssReef.ShallowDepth + AbyssReef.DuneHeight).Select(map.HeightAt).ToList();
        Assert.NotEmpty(floor);
        Assert.True(floor.Max() - floor.Min() > AbyssReef.DuneHeight * 0.6f, $"the dunes span {floor.Max() - floor.Min():0.00} m");
        Assert.True(floor.Max() < -2f);
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
    public void Deeper_levels_start_with_puddles_of_ink_round_their_vents()
    {
        PlaneWorld At(int depth) => new(TestLevels.Get(TestLevels.Find(p => p.Id.Depth == depth && !p.HasBoss)), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), Adventure);
        // Depth 1 (the first real depth) starts clean; deeper, each vent's puddle grows with Menace.
        Assert.Equal(0, At(2).Dots!.Count);
        var mid = At(4);
        var deep = At(7);
        Assert.True(deep.Menace > mid.Menace);
        foreach (var w in new[] { mid, deep })
        {
            int each = (int)MathF.Round(AdventureTuning.PuddlePerMenace * w.Menace);
            Assert.InRange(w.Dots!.Count, each * w.Vents.Count * 0.8f, each * w.Vents.Count);
            // Every dot lies in a puddle joined to its vent (fed, so it spreads), close round its mouth.
            foreach (int i in w.Dots.Live)
            {
                Assert.True(w.Dots.Fed[i]);
                Assert.Contains(w.Vents, v => Vector2.Distance(v.Position, InkDots.Centre(i)) < 12f);
            }
        }
        Assert.True(deep.Dots!.Count / deep.Vents.Count > mid.Dots!.Count / mid.Vents.Count);
    }

    [Fact]
    public void A_boss_levels_way_down_opens_once_its_vents_are_sealed_and_its_boss_is_freed()
    {
        foreach (bool ventsFirst in new[] { true, false })
        {
            var w = World(boss: true);
            var boss = w.Boss!;
            Assert.Equal(OctoShoots.Core.Bosses.BossConcept.TheBrood, boss.Spec.Concept);
            if (ventsFirst)
            {
                foreach (var v in w.Vents) w.DebugSealVent(v);
                Assert.False(w.ExitOpen);
            }
            // In she goes; it rises out of the ink and fights; it is freed.
            w.Player.Position = w.Player.PrevPosition = w.ArenaCenter + new Vector2(0f, w.ArenaRadius * 0.55f);
            for (int i = 0; i < 6 * PlaneWorld.TickRate && boss.Stage != BossStage.Fight; i++) Run(w, 1);
            Assert.Equal(BossStage.Fight, boss.Stage);
            w.DebugFreeBoss();
            Run(w, PlaneWorld.TickRate);
            Assert.False(w.BossBattle);
            Assert.Equal(ventsFirst, w.ExitOpen);
            if (ventsFirst) continue;
            foreach (var v in w.Vents) w.DebugSealVent(v);
            Assert.True(w.ExitOpen);
        }
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
    public void A_pufferfish_in_the_ink_becomes_a_spiky_inkwarden_that_fires_back_wards_and_is_freed_a_pufferfish()
    {
        var w = World();
        foreach (var v in w.Vents) v.Sealed = true;
        w.ReefFish.Clear();
        Assert.InRange(w.Fish.Count, AdventureTuning.PufferMin / 2, AdventureTuning.PufferMax);
        var puffer = w.Fish[0];
        puffer.Ink = 1f;
        Run(w, 1);
        var warden = w.Inkwardens.Single();
        Assert.True(warden.Puffer);
        Assert.DoesNotContain(puffer, w.Fish);
        Assert.Equal(WardenStage.Calm, warden.Stage);

        // Anemones never turn now.
        foreach (var an in w.Anemones) an.Ink = 1f;
        Run(w, 2);
        Assert.Single(w.Inkwardens);

        // It still wards the corrupted creatures round it.
        var kin = w.DebugBud(warden.Position + Vector2.UnitX * 2f, prime: false);
        kin.Bud = 0f;

        // Struck: it rolls out, spins up, fires all its spikes one by one, then settles and grows them back.
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        warden.Corruption.Hit(1f);
        int spikes = 0;
        bool rolled = false, spun = false;
        for (int i = 0; i < 6 * PlaneWorld.TickRate; i++)
        {
            w.Player.Hp = 100f;
            kin.Position = warden.Position + Vector2.UnitX * 2f;
            w.Step(default);
            spikes += w.Events.Count(e => e.Type == PlaneEventType.WardenSpike);
            rolled |= warden.Stage == WardenStage.Rolling;
            spun |= warden.SpinRate > WardenTuning.SpinFast * 0.9f;
        }
        Assert.True(rolled && spun);
        Assert.Equal(WardenTuning.Spikes, spikes);
        Assert.Equal(WardenStage.Calm, warden.Stage);
        Assert.Equal(WardenTuning.Spikes, warden.SpikesLeft);

        var light = kin.Corruption.Value;
        w.DebugBurst(kin.Position, 0.5f, 10f);
        Assert.Equal(light, kin.Corruption.Value);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.Warded);

        // Freed: a pufferfish again, swimming off (she is near: a freed one is gone once out of her sight).
        w.Player.Position = w.Player.PrevPosition = warden.Position + Vector2.UnitY * 3f;
        warden.Corruption.Value = 0.5f;
        w.DebugBurst(warden.Position, 0.5f, 10f);
        Run(w, 1);
        Assert.False(warden.Alive);
        Assert.Contains(w.Fish, f => f.Freed && Vector2.Distance(f.Position, warden.Position) < 1f);
    }

    [Fact]
    public void Each_fish_lives_fully_at_its_own_depth_and_fewer_below()
    {
        Assert.Equal(0f, AdventureTuning.Rate(FlounderTuning.Home, 1));
        Assert.Equal(1f, AdventureTuning.Rate(FlounderTuning.Home, FlounderTuning.Home));
        Assert.Equal(AdventureTuning.EarlierRate, AdventureTuning.Rate(FlounderTuning.Home, 5));
        Assert.True(AdventureTuning.EarlierRate is > 0f and < 1f);
        // The Shallows' fish live fully in the Kelp Jungle too, and fewer deeper.
        var kelp = World();
        var deep = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => p.Id.Depth == 4 && !p.HasBoss)), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), Adventure);
        Assert.InRange(kelp.Fish.Count, AdventureTuning.PufferMin, AdventureTuning.PufferMax);
        Assert.InRange(deep.Fish.Count, 1, (int)MathF.Round(AdventureTuning.PufferMax * AdventureTuning.EarlierRate));
        Assert.InRange(kelp.Flounders.Count, 1, FlounderTuning.Max);
        Assert.InRange(deep.Flounders.Count, 0, (int)MathF.Round(FlounderTuning.Max * AdventureTuning.EarlierRate));
    }

    [Fact]
    public void Flounders_lie_on_their_own_shelves()
    {
        var id = TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss);
        var map = AbyssReef.Apply(OctoShoots.Core.Gen.TopDown.TopDownGenerator.Generate(new OctoShoots.Core.Run.RunStreams(TestLevels.Seed), id));
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), Adventure);
        Assert.InRange(w.Flounders.Count, FlounderTuning.Min, FlounderTuning.Max);
        Assert.All(w.Flounders, f =>
        {
            Assert.False(f.Corrupted);
            Assert.Contains(map.Shelves, s => Vector2.Distance(s, f.Home) < 1.5f);
            Assert.DoesNotContain(w.Anemones, a => Vector2.Distance(a.Position, f.Home) < 6f);
        });
    }

    [Fact]
    public void A_flounder_in_the_ink_turns_into_a_hearthguard_that_holds_its_patch_flings_ink_and_stabs_her_close_by()
    {
        var w = Bare();
        var f = w.Flounders[0];
        w.Flounders.RemoveAll(o => o != f);
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        // Healthy, it lies still; it eats the dots it touches, fills and turns.
        for (int i = 0; i < 2 * PlaneWorld.TickRate && !f.Corrupted; i++)
        {
            w.DebugDot(f.Home);
            Run(w, 1);
        }
        Assert.True(f.Corrupted);
        Assert.Equal(f.Home, f.Position);

        // Left alone, it shimmies now and then, flinging ink drops round it that fall close and leave dots.
        int drops = 0;
        float far = 0f;
        for (int i = 0; i < 10 * PlaneWorld.TickRate; i++)
        {
            int before = w.MurkShots.Count;
            Run(w, 1);
            drops += Math.Max(0, w.MurkShots.Count - before);
            foreach (var s in w.MurkShots) far = MathF.Max(far, Vector2.Distance(s.Position, f.Home));
        }
        Assert.True(drops >= 8, $"only {drops} drops");
        Assert.InRange(far, 1f, FlounderTuning.InkReach + 1.5f);
        Assert.Equal(f.Home, f.Position);

        // She comes close: its disc draws into a needle at her, and it stabs.
        var near = f.Home + Vector2.Normalize(w.Map.Start.Position - f.Home) * 2f;
        for (int k = 0; k < 16 && !(w.Clear(near, w.Radius) && w.LineOfSight(f.Home, near)); k++)
            near = f.Home + new Vector2(MathF.Cos(k * 0.4f), MathF.Sin(k * 0.4f)) * 2f;
        float hp = 100f;
        bool wound = false, bit = false;
        for (int i = 0; i < 3 * PlaneWorld.TickRate && !bit; i++)
        {
            w.Player.Position = w.Player.PrevPosition = near;
            w.Player.Velocity = Vector2.Zero;
            w.Player.DashInvulnerableTimer = 0f;
            w.MurkShots.Clear();
            w.Player.Hp = hp;
            w.Step(default);
            wound |= f.Stage == FlounderStage.Windup && f.Needle > 0f;
            bit |= w.Events.Any(e => e.Type == PlaneEventType.FlounderBite);
        }
        Assert.True(wound && bit);
        Assert.True(w.Player.Hp < hp);
        Assert.Equal(f.Home, f.Position);

        // Far off, it never comes after her.
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        Run(w, 3 * PlaneWorld.TickRate);
        Assert.NotEqual(FlounderStage.Windup, f.Stage);
        Assert.Equal(f.Home, f.Position);

        // Freed: a flounder again, swimming off and gone.
        f.Corruption.Value = 0.5f;
        w.DebugBurst(f.Position, 0.5f, 10f);
        Run(w, 1);
        Assert.True(f.Freed);
        Assert.False(f.Corrupted);
        Run(w, PlaneWorld.TickRate * (int)ReefFishTuning.LeaveMax + 60);
        Assert.DoesNotContain(f, w.Flounders);
    }

    [Fact]
    public void An_inkwarden_guards_the_vents_in_its_reach()
    {
        var w = World();
        var v = w.Vents[0];
        var puffer = w.Fish[0];
        puffer.Position = v.Position + v.Facing * 3f;
        puffer.Ink = 1f;
        Run(w, 1);
        var warden = w.Inkwardens.Single();
        Assert.Contains(w.GuardedBy(warden), at => at == v.Position);
        for (int i = 0; i < v.Needed + 2; i++) w.DebugStrikeVent(v);
        Assert.False(v.Sealed);
        Assert.Equal(0, v.Hits);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.Warded);
        // Freed, it guards no more.
        warden.Corruption.Value = 0.5f;
        w.DebugBurst(warden.Position, 0.5f, 10f);
        Run(w, 1);
        for (int i = 0; i < v.Needed; i++) w.DebugStrikeVent(v);
        Assert.True(v.Sealed);
    }

    [Fact]
    public void Her_oral_arm_trails_her_stings_what_it_touches_and_grows_with_her_skills()
    {
        var w = Bare();
        var her = OpenSpot(w);
        w.Player.Position = w.Player.PrevPosition = her;
        var behind = her - Vector2.UnitX * 1.4f;
        var m = w.DebugBud(behind, prime: false);
        m.Bud = 0f;
        float full = m.Corruption.Value;
        int stung = 0;
        for (int i = 0; i < PlaneWorld.TickRate; i++)
        {
            // She has just swum in from the west; the murkling lies on her way, by her arm.
            w.DebugTrail(new[] { her - Vector2.UnitX * 3f, her - Vector2.UnitX * 1.5f, her });
            m.Position = behind;
            m.Velocity = Vector2.Zero;
            w.Player.Position = her;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = 100f;
            w.Step(default);
            stung += w.Events.Count(e => e.Type is PlaneEventType.MobHit or PlaneEventType.MobDefeated);
        }
        // A share of a shot each ArmEvery: about two or three stings in a second.
        Assert.InRange(stung, 2, 3);
        Assert.True(m.Corruption.Value < full);
        Assert.True(w.Arm.Count >= 2);

        // Each skill point spent grows it a little, up to its most.
        Assert.Equal(AdventureTuning.ArmBase, w.ArmLength, 3);
        w.Run.Points = 3;
        for (int i = 0; i < 3; i++) Assert.True(w.SpendSkill(OctoShoots.Core.Items.Stat.Damage));
        Assert.Equal(AdventureTuning.ArmBase + 3 * AdventureTuning.ArmPerUpgrade, w.ArmLength, 3);
        Assert.Equal(AdventureTuning.ArmMax, AdventureTuning.ArmLength(1000), 3);
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
