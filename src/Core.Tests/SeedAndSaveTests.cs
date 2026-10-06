using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Nodes;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using Xunit;

namespace OctoShoots.Core.Tests;

public class SeedTests
{
    [Theory]
    [InlineData("KELP 7Q2Z")]
    [InlineData("AAAA AAAA")]
    [InlineData("9999 9999")]
    public void SeedCodesRoundTrip(string code)
    {
        var seed = SeedCode.Parse(code);
        Assert.Equal(code, seed.ToString());
        Assert.Equal(seed, SeedCode.Parse(code.ToLowerInvariant().Replace(" ", "")));
    }

    [Theory]
    [InlineData("KELP 7Q2")]
    [InlineData("KELP 7Q2ZZ")]
    [InlineData("KELP 0Q2Z")]
    [InlineData("KELO 7Q2Z")]
    [InlineData("KEL1 7Q2Z")]
    [InlineData("")]
    public void BadSeedsAreRejected(string code)
    {
        Assert.False(SeedCode.TryParse(code, out _));
    }

    [Fact]
    public void SpecialSeedsAreRecognised()
    {
        Assert.Equal(SpecialSeed.Debug, SeedCode.Parse("debug").Special);
        Assert.Equal("DEBUG", SeedCode.Parse("DEBUG").ToString());
        Assert.Equal(SpecialSeed.BigJelly, SeedCode.Parse("HUGE JELL").Special);
        Assert.Equal(SpecialSeed.None, SeedCode.Parse("KELP 7Q2Z").Special);
    }

    [Fact]
    public void ValuesAreFortyBits()
    {
        var seed = SeedCode.FromValue(ulong.MaxValue);
        Assert.Equal((1UL << 40) - 1, seed.Value);
        Assert.Equal("9999 9999", seed.ToString());
    }

    [Fact]
    public void StreamsDoNotDisturbEachOther()
    {
        var a = new RunStreams(SeedCode.Parse("KELP 7Q2Z"));
        var b = new RunStreams(SeedCode.Parse("KELP 7Q2Z"));
        // Burn lots of numbers on one stream of b; the item stream must not care.
        var ai = b.EnemyAi();
        for (int i = 0; i < 1000; i++) ai.NextU64();
        Assert.Equal(a.Items(2, 1, 5).NextU64(), b.Items(2, 1, 5).NextU64());
        Assert.NotEqual(a.Items(2, 1, 5).NextU64(), a.Items(2, 1, 6).NextU64());
    }
}

public class SaveTests
{
    static SaveFile Sample() => new()
    {
        Profile = new Profile
        {
            Achievements = new HashSet<string> { "untouchable" },
            SeenItems = new HashSet<string> { "coral_crown", "mitosis" },
            Stats = new ProfileStats { Runs = 3, Deaths = 2, DeathsByCause = new() { ["lanternfish"] = 2 } },
        },
        Run = new SuspendedRun
        {
            Seed = "KELP 7Q2Z",
            Items = new List<string> { "coral_crown", "conch_horn" },
            Hp = 87.5f,
            Foam = 15f,
            Coins = 12,
            Position = new[] { 1f, 2f, 3f },
            Ticks = 4321,
        },
    };

    [Fact]
    public void JsonRoundTrip()
    {
        var back = SaveCodec.Deserialize(SaveCodec.Serialize(Sample()));
        Assert.Equal(SaveCodec.CurrentVersion, back.Version);
        Assert.Contains("untouchable", back.Profile.Achievements);
        Assert.Equal(2, back.Profile.Stats.DeathsByCause["lanternfish"]);
        Assert.Equal(87.5f, back.Run!.Hp);
        Assert.Equal(new[] { "coral_crown", "conch_horn" }, back.Run.Items);
        Assert.Equal(new[] { 1f, 2f, 3f }, back.Run.Position);
    }

    [Fact]
    public void ExportCodeRoundTrip()
    {
        string code = SaveCodec.Export(Sample());
        Assert.StartsWith(SaveCodec.ExportPrefix, code);
        Assert.DoesNotContain("+", code);
        Assert.DoesNotContain("/", code);
        var back = SaveCodec.Import("  " + code + "\n");
        Assert.Equal(12, back.Run!.Coins);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("CQ3D1:not*base64")]
    [InlineData("CQ3D1:aGVsbG8")]
    public void DamagedCodesAreRejected(string code)
    {
        Assert.Throws<SaveException>(() => SaveCodec.Import(code));
    }

    [Fact]
    public void NewerSavesAreRejected()
    {
        var e = Assert.Throws<SaveException>(() => SaveCodec.Deserialize("""{ "version": 99, "profile": {} }"""));
        Assert.Contains("newer", e.Message);
    }

    [Fact]
    public void OldSavesMigrateStepByStep()
    {
        // Pretend the format is at version 3: v1 kept coins as "sandDollars", v2 had no foam.
        var migrations = new Dictionary<int, Func<JsonObject, JsonObject>>
        {
            [1] = root =>
            {
                var run = root["run"]!.AsObject();
                run["coins"] = run["sandDollars"]!.GetValue<int>();
                run.Remove("sandDollars");
                return root;
            },
            [2] = root =>
            {
                root["run"]!["foam"] = 5f;
                return root;
            },
        };
        string v1 = """{ "version": 1, "profile": {}, "run": { "seed": "AAAA AAAA", "sandDollars": 7 } }""";
        var save = SaveCodec.Deserialize(v1, migrations, currentVersion: 3);
        Assert.Equal(7, save.Run!.Coins);
        Assert.Equal(5f, save.Run.Foam);
    }

    [Fact]
    public void CustomSeedRunsDontEarnAchievements()
    {
        var profile = new Profile();
        Assert.False(profile.Award("untouchable", customSeed: true));
        Assert.True(profile.Award("untouchable", customSeed: false));
        Assert.False(profile.Award("untouchable", customSeed: false));
    }

    [Fact]
    public void WorldSnapshotRestores()
    {
        var world = ItemWorlds.Create(0);
        world.GiveItem("coral_crown");
        world.GiveItem("barnacle_armor");
        world.GiveItem("pirates_doubloon");
        world.Player.Hp = 61f;
        world.Player.Position = new Vector3(20f, 13f, 18f);
        var snapshot = world.Snapshot("KELP 7Q2Z", customSeed: false);

        var restored = ItemWorlds.Create(0);
        restored.Restore(SaveCodec.Deserialize(SaveCodec.Serialize(new SaveFile { Run = snapshot })).Run!);
        Assert.Equal(world.Items, restored.Items);
        Assert.Equal(61f, restored.Player.Hp);
        Assert.Equal(30f, restored.Player.Foam);
        Assert.Equal(15, restored.Coins);
        Assert.Equal(new Vector3(20f, 13f, 18f), restored.Player.Position);
        Assert.Equal(world.Loadout.Stats.MaxHp, restored.Loadout.Stats.MaxHp);
    }
}
