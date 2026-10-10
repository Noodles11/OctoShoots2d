using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The reef director: a seeded schedule of world events, and the current surges it runs.</summary>
public class ReefDirectorTests
{
    static readonly Lazy<LevelMap> Level = new(TestLevels.OneTreasure);

    [Fact]
    public void TheScheduleIsSeededAndFollowsTheTimingRules()
    {
        var a = new ReefDirector(Level.Value).Schedule;
        var b = new ReefDirector(Level.Value).Schedule;
        Assert.True(a.Count >= 10, $"{a.Count} events");
        Assert.Equal(a.Select(e => (e.Start, e.Corridor, e.Reverse, e.Speed)), b.Select(e => (e.Start, e.Corridor, e.Reverse, e.Speed)));

        Assert.InRange(a[0].Start, ReefDirectorTuning.FirstMin, ReefDirectorTuning.FirstMax);
        for (int i = 0; i < a.Count; i++)
        {
            var e = a[i];
            Assert.Equal(ReefEventKind.CurrentSurge, e.Kind);
            Assert.Equal(ReefDirectorTuning.SurgeSeconds, e.Duration);
            Assert.InRange(e.Speed, ReefDirectorTuning.SurgeSpeedMin, ReefDirectorTuning.SurgeSpeedMax);
            Assert.Contains(Level.Value.Corridors[e.Corridor].Kind, new[] { CorridorKind.Main, CorridorKind.Side });
            if (i > 0) Assert.InRange(e.Start - a[i - 1].End, ReefDirectorTuning.GapMin, ReefDirectorTuning.GapMax);
        }
        // Another level schedules otherwise.
        var other = new ReefDirector(TestLevels.After(Level.Value)).Schedule;
        Assert.NotEqual(a.Select(e => e.Start), other.Select(e => e.Start));
    }

    [Fact]
    public void ASurgeStartsSlowAcceleratesHoldsAndEasesOff()
    {
        var e = new ReefEvent { Start = 10f, Duration = ReefDirectorTuning.SurgeSeconds };
        Assert.Equal(0f, e.Envelope(10f));
        float early = e.Envelope(10.5f), mid = e.Envelope(12.5f), up = e.Envelope(15f);
        Assert.True(early < 0.05f && early > 0f, $"slow start {early}");
        // Accelerating: the second second adds more than the first.
        Assert.True(e.Envelope(12f) - e.Envelope(11f) > e.Envelope(11f) - e.Envelope(10f));
        Assert.True(mid > early && up > mid);
        Assert.Equal(1f, e.Envelope(20f));
        Assert.Equal(1f, e.Envelope(25f));
        Assert.True(e.Envelope(28f) < 1f && e.Envelope(29.5f) < e.Envelope(28f));
        Assert.Equal(0f, e.Envelope(30f));
    }

    [Fact]
    public void TheReefActsWhetherSheIsThereOrNot()
    {
        // She idles at the start; the first surge still starts and ends on time.
        var w = new PlaneWorld(Level.Value, new Tuning());
        w.Mobs.Clear();
        var first = w.Director.Schedule[0];
        bool started = false, ended = false;
        int ticks = (int)MathF.Ceiling((first.End + 0.5f) / PlaneWorld.Dt);
        for (int i = 0; i < ticks; i++)
        {
            w.Player.Hp = 100f;
            w.Step(default);
            started |= w.Events.Any(e => e.Type == PlaneEventType.SurgeStarted);
            ended |= w.Events.Any(e => e.Type == PlaneEventType.SurgeEnded);
        }
        Assert.True(started && ended);
        Assert.Empty(w.Director.Active);
    }

    [Fact]
    public void ASurgeCarriesWhatIsInItsCanyonAndNothingFarOff()
    {
        var w = new PlaneWorld(Level.Value, new Tuning());
        w.Mobs.Clear();
        var surge = w.Director.Schedule[0];
        var canyon = w.Map.Corridors[surge.Corridor];
        // A point mid-canyon, and one far from it.
        var mid = canyon.Points[canyon.Points.Count / 2];
        Vector2 away = Vector2.Zero;
        for (float y = 20f; y < LevelMap.Size - 20f && away == Vector2.Zero; y += 3f)
        for (float x = 20f; x < LevelMap.Size - 20f; x += 3f)
        {
            var q = new Vector2(x, y);
            ReefDirector.Nearest(canyon, q, out float d, out _);
            if (d > canyon.HalfWidth + ReefDirectorTuning.ForceFeather + 4f && w.Map.IsOpen(q)) { away = q; break; }
        }
        Assert.NotEqual(Vector2.Zero, away);

        // Run up to the surge's full strength.
        while (w.Time < surge.Start + ReefDirectorTuning.SurgeRise + 1f)
        {
            w.Player.Position = w.Player.PrevPosition = mid;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = 100f;
            w.Step(default);
        }
        ReefDirector.Nearest(canyon, mid, out _, out Vector2 along);
        Vector2 flowDir = along * (surge.Reverse ? -1f : 1f);
        Vector2 flow = w.Director.FlowAt(mid, w.Time);
        Assert.InRange(flow.Length(), surge.Speed * 0.9f, surge.Speed * 1.01f);
        Assert.True(Vector2.Dot(Vector2.Normalize(flow), flowDir) > 0.99f);
        Assert.Equal(Vector2.Zero, w.Director.FlowAt(away, w.Time));

        // Idle in the canyon, she drifts with it; a shell there drifts too, one far off stays.
        w.Player.Position = w.Player.PrevPosition = mid;
        w.Player.Velocity = Vector2.Zero;
        w.Shells.Clear();
        var near = new PlaneShell { Position = mid + Geo.Perp(along) * 1.5f };
        var far = new PlaneShell { Position = away };
        w.Shells.Add(near);
        w.Shells.Add(far);
        Vector2 nearFrom = near.Position;
        for (int i = 0; i < 30; i++) w.Step(default);
        Assert.True(Vector2.Dot(w.Player.Position - mid, flowDir) > 1f, "she is carried along the canyon");
        if (!near.Taken) Assert.True(Vector2.Dot(near.Position - nearFrom, flowDir) > 0.5f, "the shell drifts");
        Assert.Equal(away, far.Position);
    }
}
