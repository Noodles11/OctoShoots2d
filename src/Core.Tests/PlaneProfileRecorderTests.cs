using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The profile recorder: statistics, pearl and creature records, and what ended a run.</summary>
public class PlaneProfileRecorderTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly Lazy<LevelMap> Shared = new(() => TopDownGenerator.Generate(new RunStreams(SeedCode.Parse("KELP7Q2Z")), 1, 1));

    static (PlaneWorld World, PlaneProfileRecorder Recorder) Start(params string[] pearls)
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        foreach (string id in pearls) run.Add(id);
        var world = new PlaneWorld(Shared.Value, new Tuning(), run);
        var recorder = new PlaneProfileRecorder(new Profile());
        recorder.StartRun("KELP 7Q2Z", customSeed: false, run.Items, continuing: false);
        recorder.EnterRoom(world, 1, 1);
        return (world, recorder);
    }

    static void Step(PlaneWorld w, PlaneProfileRecorder r, PlaneInput input = default)
    {
        w.Step(input);
        r.Observe(w);
    }

    [Fact]
    public void ARunIsCountedAndItsSeedRemembered()
    {
        var (_, r) = Start();
        Assert.Equal(1, r.Profile.Stats.Runs);
        Assert.Equal(0, r.Profile.Stats.SeededRuns);
        Assert.Equal("KELP 7Q2Z", r.Profile.RecentSeeds[0]);
        Assert.Equal(1, r.Profile.Stats.BestRoom);
        r.StartRun("AAAA AAAA", customSeed: true, Array.Empty<string>(), continuing: false);
        r.StartRun("KELP 7Q2Z", customSeed: false, Array.Empty<string>(), continuing: false);
        Assert.Equal(new[] { "KELP 7Q2Z", "AAAA AAAA" }, r.Profile.RecentSeeds);
        Assert.Equal(1, r.Profile.Stats.SeededRuns);
    }

    [Fact]
    public void VolleysAndKillsAreCounted()
    {
        var (w, r) = Start("triple_tentacle");
        foreach (var m in w.Mobs.Skip(1)) m.Hp = 0f;
        var mob = w.Mobs[0];
        w.Player.Position = w.Player.PrevPosition = mob.Position + new Vector2(2.5f, 0f);
        if (!w.Clear(w.Player.Position, w.Radius)) w.Player.Position = w.Player.PrevPosition = mob.Position + new Vector2(-2.5f, 0f);
        var aim = Vector2.Normalize(mob.Position - w.Player.Position);
        for (int i = 0; i < 60 * 8 && mob.Alive; i++) Step(w, r, new PlaneInput { Aim = aim, Fire = true });
        Assert.False(mob.Alive);
        Assert.Equal(3, r.Profile.Stats.BiggestVolley);
        Assert.True(r.Profile.Stats.BubblesThrown >= 3);
        Assert.Equal(1, r.Profile.Stats.Kills);
        Assert.Equal(1, r.Profile.Creature(PlaneProfileRecorder.MobId).Defeated);
        Assert.True(r.Profile.Stats.DamageDealt >= PlaneCombatTuning.MobHp);
        Assert.Contains(PlaneProfileRecorder.MobId, r.Profile.SeenCreatures);
    }

    [Fact]
    public void ADeathNamesItsCauseAndTheKillerAndCountsAgainstHerPearls()
    {
        var (w, r) = Start("hammerhead");
        w.Player.Hp = 5f;
        var mob = w.Mobs[0];
        w.Shots.Add(new PlaneShot { Position = w.Player.Position, Velocity = Vector2.UnitX, Life = 1f });
        Step(w, r);
        Assert.True(w.Defeated);
        r.RunDied(w, 120);
        Assert.Equal(1, r.Profile.Stats.Deaths);
        Assert.Equal(1, r.Profile.Stats.DeathsByCause["mob_shot"]);
        Assert.Equal(1, r.Profile.Creature(PlaneProfileRecorder.MobId).DefeatedYou);
        Assert.Equal(1, r.Profile.Pearl("hammerhead").RunsLost);
        Assert.Equal(1, r.Profile.Pearl("hammerhead").Runs);
        Assert.Equal(120, r.Profile.Stats.LongestRunSeconds);
    }

    [Fact]
    public void AClearedRoomKeepsItsRecords()
    {
        var (w, r) = Start("mirror_scale");
        w.Stats.ShellsCollected = 40;
        r.RoomCleared(w, 95);
        w.Stats.ShellsCollected = 25;
        w.Stats.DamageTaken = 10f;
        r.RoomCleared(w, 130);
        var s = r.Profile.Stats;
        Assert.Equal(2, s.RoomsCleared);
        Assert.Equal(95, s.FastestRoomSeconds);
        Assert.Equal(40, s.MostShellsInRoom);
        Assert.Equal(65, s.ShellsCollected);
        Assert.Equal(1, s.UntouchableRooms);
        Assert.Equal(2, r.Profile.Pearl("mirror_scale").RoomsCleared);
    }

    [Fact]
    public void PearlsOfferedInARoomAreSeen()
    {
        var (w, r) = Start();
        foreach (var pearl in w.Pearls) Assert.Contains(pearl.ItemId, r.Profile.SeenItems);
        foreach (var stand in w.Stands.Where(s => s.Kind == StandKind.Pearl)) Assert.Contains(stand.ItemId, r.Profile.SeenItems);
    }
}
