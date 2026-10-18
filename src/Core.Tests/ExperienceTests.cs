using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Experience and skill points (DESIGN-TOPDOWN §2.6).</summary>
public class ExperienceTests
{
    static readonly PlaneOptions War = new() { Corruption = true, Pufferlings = false };

    static PlaneWorld Level() =>
        new(TestLevels.Get(TestLevels.Find(p => !p.HasBoss && p.Id.Level > 1)), new Tuning(), new PlaneRun(null, new Tuning()), War);

    [Fact]
    public void LevelsTakeMoreEachTimeAndEachIsASkillPoint()
    {
        var run = new PlaneRun(null, new Tuning());
        Assert.Equal(ExperienceTuning.FirstLevel, run.XpNeeded);
        Assert.Equal(0, run.Gain(ExperienceTuning.FirstLevel - 1));
        Assert.Equal(1, run.Gain(1));
        Assert.Equal(2, run.XpLevel);
        Assert.Equal(1, run.Points);
        Assert.Equal(ExperienceTuning.FirstLevel + ExperienceTuning.PerLevel, run.XpNeeded);
        // Enough for two levels at once.
        Assert.Equal(2, run.Gain(ExperienceTuning.Need(2) + ExperienceTuning.Need(3) + 5));
        Assert.Equal(4, run.XpLevel);
        Assert.Equal(3, run.Points);
        Assert.Equal(5, run.Xp);
    }

    [Fact]
    public void ASkillPointAddsItsStepAndMoreMaxHpComesFilled()
    {
        var t = new Tuning();
        var run = new PlaneRun(null, t);
        Assert.False(run.Spend(Stat.MaxHp), "no point to spend");
        run.Gain(ExperienceTuning.FirstLevel + ExperienceTuning.Need(2));
        run.Hp = 50f;
        float max = run.MaxHp, damage = run.Loadout.Stats.Damage;
        Assert.True(run.Spend(Stat.MaxHp));
        Assert.Equal(max + 10f, run.MaxHp, 3);
        Assert.Equal(60f, run.Hp, 3);
        Assert.True(run.Spend(Stat.Damage));
        Assert.Equal(damage + t.Damage * 0.06f, run.Loadout.Stats.Damage, 4);
        Assert.Equal(0, run.Points);
        Assert.Equal(1, run.Skills[Stat.MaxHp]);
        // Not a modifier a point can go to.
        run.Gain(ExperienceTuning.Need(run.XpLevel));
        Assert.False(run.Spend(Stat.Luck));
    }

    [Fact]
    public void AMaxHpPointFillsHerHealthInPlay()
    {
        var w = Level();
        w.Run.Gain(ExperienceTuning.FirstLevel);
        w.Player.Hp = 40f;
        Assert.True(w.SpendSkill(Stat.MaxHp));
        Assert.Equal(50f, w.Player.Hp, 3);
        Assert.Equal(110f, w.Run.MaxHp, 3);
    }

    [Fact]
    public void CleansingPaysAtEveryTenthOnlyOnce()
    {
        var w = Level();
        var field = w.Corruption!;
        // Clear the level's ground a bit past three tenths of its corruption.
        var order = Enumerable.Range(0, field.Density.Length).Where(i => field.Density[i] > 0f).ToList();
        float want = field.InitialTotal * 0.33f, cleared = 0f;
        var cells = new System.Collections.Generic.List<int>();
        foreach (int i in order)
        {
            if (cleared >= want) break;
            cleared += field.Density[i];
            cells.Add(i);
        }
        field.Clear(cells);
        w.Step(default);
        Assert.Equal(3 * ExperienceTuning.CleanseXp, w.Run.Xp);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.XpGained && (int)e.Size == 3 * ExperienceTuning.CleanseXp);
        // The same ground regrown and cleansed again pays nothing more; the next tenth does.
        foreach (int i in cells) field.Raise(i, field.Initial[i], 1f);
        w.Step(default);
        field.Clear(cells);
        w.Step(default);
        Assert.Equal(3 * ExperienceTuning.CleanseXp, w.Run.Xp);
    }

    [Fact]
    public void ItIsCalmOnlyAwayFromTheCorrupted()
    {
        var w = Level();
        foreach (var m in w.Murklings) m.Alive = false;
        foreach (var b in w.Blotters) b.Alive = false;
        foreach (var t in w.Thumpsquirts) t.Alive = false;
        foreach (var x in w.Inkwardens) x.Alive = false;
        foreach (var l in w.Lashjaws) l.Alive = false;
        w.Blightroots.ForEach(r => r.Alive = false);
        for (int i = 0; i < (int)(ExperienceTuning.CalmSeconds * 60f) + 2; i++) w.Step(default);
        Assert.True(w.Calm);
        // A murkling close by: a fight again.
        w.Murklings.Add(new Murkling { Position = w.Player.Position + new Vector2(3f, 0f), Bud = 0f });
        w.Step(default);
        Assert.False(w.Calm);
    }
}
