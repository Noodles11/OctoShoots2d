using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>Blightroots, gloomvines, valves and murklings (docs/CORRUPTION.md). Provisional numbers.</summary>
public static class BlightTuning
{
    // Blightroot: the source of corruption in its reach.
    public const float Reach = 9f;
    /// <summary>It regrows its reach toward the field's starting density at this rate (density per second).</summary>
    public const float Regrow = 0.1f;
    /// <summary>It starves when its ring (from RingInner of its reach out) is cleansed below this mean density.</summary>
    public const float RingInner = 0.55f, StarveBelow = 0.12f;
    /// <summary>The ink sac: bubbles that reach it are drunk (no damage).</summary>
    public const float SacRadius = 0.9f;
    /// <summary>Its burst sends a wave of cleansing through its whole reach in this long.</summary>
    public const float BurstSeconds = 0.7f;
    /// <summary>Every PulseEvery seconds: the sac inflates (Telegraph), then a ring of ink runs out at PulseSpeed.</summary>
    public const float PulseEvery = 4f, Telegraph = 0.8f, PulseSpeed = 10f, PulseBand = 0.9f;
    /// <summary>The ring stains cleansed ground back to this density and dims the bubbles it crosses to this much.</summary>
    public const float PulseStain = 0.35f, PulseDim = 0.7f;
    /// <summary>Where they stand: never near safe places (beyond their radius), apart, more with Menace.</summary>
    public const float SafeDistance = 18f, Spacing = 22f;
    public const int Base = 3, Max = 6;
    public const float PerMenace = 1.5f;

    // Gloomvines: rooted at Blightroots, they creep at a lingering Clementine.
    public const int VinesPer = 2;
    public const float Segment = 0.4f, Rest = 2.6f, Creep = 0.8f, Regrow2 = 0.4f, MaxLength = Reach * 1.3f;
    /// <summary>She lingers when slower than LingerSpeed for Linger seconds.</summary>
    public const float Linger = 0.6f, LingerSpeed = 1f;
    /// <summary>A tip within CoilReach of her coils: rooted CoilSeconds, drinking her light; it then lets go and draws back.</summary>
    public const float CoilReach = 0.7f, CoilSeconds = 1.2f, CoilDrain = 5f, Retract = 2f;
    public const float DissolveSeconds = 0.9f;

    // Valves: walls of dense darkness across a neck or a stash's approach.
    public const float ValveThickness = 1.6f, ValveCost = 7f;
    /// <summary>Cleansing its edges (her light within EdgeReach) wears it down in about 3 s; a pop takes PopWear per bubble.</summary>
    public const float EdgeReach = 2.2f, EdgeWear = 0.32f, PopWear = 0.06f;
    public const float SeenRange = 14f;

    // Murklings: orbs of darkness budding from the corruption.
    public const float MurkRadius = 0.45f, MurkSpeed = 1.8f, Sense = 14f;
    public const float BudSeconds = 0.8f, BudInterval = 15f, BudDensity = 0.6f, BudClearance = 5f;
    /// <summary>An area stops budding for good once it is cleansed this far (below a fifth of its corruption left).</summary>
    public const float AreaSpent = 0.8f;
    public const int CapBase = 2;
    public const float CapPerMenace = 1.5f;
    public const float ContactDamage = 6f, ClingDrain = 2.5f;
    public const int MaxClingers = 3;
    public const float ThrowEvery = 2.4f, ThrowRange = 9f, ThrowSpeed = 6f, ThrowDamage = 7f, ThrowRadius = 0.2f, ThrowLife = 2.5f;
    /// <summary>A bubble that bursts a murkling or meets its throw keeps this much of its light.</summary>
    public const float BubbleDim = 0.5f;
    /// <summary>A drain shows as one hit this often.</summary>
    public const float DrainReport = 0.4f;
    /// <summary>The arena buds faster while the boss fights.</summary>
    public const float ArenaInterval = 6f;
    /// <summary>An ambush buds this many murklings round her.</summary>
    public const int AmbushBuds = 3;
}

/// <summary>A Blightroot: a blackened stump crowned with a pulsing ink sac.</summary>
public sealed class Blightroot
{
    public Vector2 Position;
    public bool Alive = true;
    /// <summary>Its area (index into Areas): the cells of its reach.</summary>
    public int Area;
    public List<int> Ring = new();
    /// <summary>Distance of each of its area's cells from it (same order as the area's cells).</summary>
    public float[] CellDistance = Array.Empty<float>();
    public float PulseTimer;
    /// <summary>The ring's radius while one runs out (negative: none); its number (each bubble is dimmed once per ring).</summary>
    public float PulseRadius = -1f;
    public int PulseId;
    /// <summary>The sac's inflation for the telegraph, 0..1.</summary>
    public float Inflate;
    /// <summary>Its ring's mean density (how close it is to starving), for the view.</summary>
    public float RingDensity = 1f;
    /// <summary>The burst's cleansing wave radius (negative: not bursting).</summary>
    public float BurstRadius = -1f;
    public float Dead;
}

/// <summary>A gloomvine: a thick ink vine rooted at a Blightroot, as points from the root to its tip.</summary>
public sealed class Gloomvine
{
    public Blightroot Root = null!;
    public List<Vector2> Points = new();
    public Vector2 RestDir;
    public float Curl;
    public bool Coiling;
    public float CoilTime;
    /// <summary>Its root died: it dissolves over DissolveSeconds (counting up).</summary>
    public float Dissolving = -1f;
    public float Length => Points.Count < 2 ? 0f : (Points.Count - 1) * BlightTuning.Segment;
}

/// <summary>A valve: a wall of dense darkness. Light passes through dark (crossing costs HP unless dashing).</summary>
public sealed class Valve
{
    public Vector2 Center;
    /// <summary>Through the wall (along the corridor) and along it (across the corridor).</summary>
    public Vector2 Normal, Across;
    public float HalfWidth, HalfThickness = BlightTuning.ValveThickness * 0.5f;
    public float Integrity = 1f;
    public bool Cleared, Seen;
    /// <summary>The side she entered from (−1, +1; 0 when she is not inside).</summary>
    public int Entered;
}

/// <summary>A murkling: a wobbling orb of darkness with a dim stolen ember inside.</summary>
public sealed class Murkling
{
    public Vector2 Position, Velocity;
    /// <summary>Budding out of the ground (counting down; above zero it is not yet out).</summary>
    public float Bud = BlightTuning.BudSeconds;
    public int Area = -1;
    public bool Alive = true, Clinging;
    public Vector2 ClingOffset;
    public float ThrowTimer, Seed;
    /// <summary>Drifting toward this light (her, or a hanging bubble of hers).</summary>
    public Vector2 Target;
    public bool Budding => Bud > 0f;
}

/// <summary>A murkling's throw: a small black orb that drains light.</summary>
public sealed class MurkShot
{
    public Vector2 Position, Velocity;
    public float Life = BlightTuning.ThrowLife;
}

/// <summary>Where murklings bud: a Blightroot's reach, or the arena.</summary>
public sealed class CorruptArea
{
    public Vector2 Center;
    public float Radius;
    public List<int> Cells = new();
    public bool Arena, Spent;
    public float Cleansed, BudTimer;
}

public sealed partial class PlaneWorld
{
    public PlaneOptions Options { get; private set; } = PlaneOptions.Default;
    /// <summary>The corruption field (null when the corruption is off).</summary>
    public CorruptionField? Corruption { get; private set; }
    public List<Blightroot> Blightroots { get; } = new();
    public List<Gloomvine> Vines { get; } = new();
    public List<Valve> Valves { get; } = new();
    public List<Murkling> Murklings { get; } = new();
    public List<MurkShot> MurkShots { get; } = new();
    public List<CorruptArea> Areas { get; } = new();
    /// <summary>The arena's area (boss levels), or null.</summary>
    public CorruptArea? ArenaArea { get; private set; }
    /// <summary>The level cleansed, 0..1.</summary>
    public float Cleansed => Corruption?.Cleansed ?? 1f;
    /// <summary>The share of the arena cleansed, 0..1.</summary>
    public float ArenaCleansed => ArenaArea?.Cleansed ?? 1f;
    /// <summary>Murklings clinging to her (each dims her glow).</summary>
    public int Clingers => Murklings.Count(m => m.Alive && m.Clinging);

    Rng _blightRng = null!;
    readonly List<(Vector2 At, int Bubbles, float Dim)> _pops = new();
    float _linger, _drained, _drainClock;
    DamageSource _drainSource;

    IEnumerable<(Vector2 Centre, float Radius)> SafePlaces() => CorruptionField.SafeOf(Map);

    void PlaceCorruption()
    {
        _blightRng = new Rng(Map.Seed ^ 0xB1167UL ^ ((ulong)Map.Level << 22) ^ ((ulong)Map.Depth << 30) ^ ((ulong)Map.Attempt << 46));
        var field = Corruption = CorruptionField.ForMap(Map);
        if (Map.HasBoss)
        {
            ArenaArea = new CorruptArea { Center = ArenaCenter, Radius = ArenaRadius, Arena = true, Cells = field.CellsWithin(ArenaCenter, ArenaRadius) };
            Areas.Add(ArenaArea);
        }
        PlaceBlightroots();
        PlaceValves();
        if (Boss is { } boss) boss.Crust = CorruptionTuning.CrustLayers;
        foreach (var area in Areas)
        {
            area.Cleansed = field.CleansedOf(area.Cells);
            area.BudTimer = _blightRng.Range(3f, BudInterval());
        }
    }

    void PlaceBlightroots()
    {
        var field = Corruption!;
        var reach = LevelValidator.Distances(Map, Map.Start.Position);
        var candidates = Map.Plazas.Select(p => p.Center).ToList();
        foreach (var c in Map.Corridors.Where(c => c.Kind is CorridorKind.Main or CorridorKind.Side))
            for (int i = 0; i < c.Points.Count; i += 3) candidates.Add(c.Points[i]);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = _blightRng.Int(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        var safes = SafePlaces().ToList();
        int want = Math.Min(BlightTuning.Max, BlightTuning.Base + (int)MathF.Round(Menace * BlightTuning.PerMenace));
        foreach (var at in candidates)
        {
            if (Blightroots.Count >= want) break;
            if (float.IsPositiveInfinity(LevelValidator.DistanceAt(reach, at)) || !Clear(at, 1.2f)) continue;
            if (safes.Any(s => Vector2.Distance(at, s.Centre) < s.Radius + BlightTuning.SafeDistance)) continue;
            if (Map.HasBoss && Vector2.Distance(at, ArenaCenter) < ArenaRadius + BlightTuning.Reach + 2f) continue;
            if (Map.Shaft.Contains(at, BlightTuning.Reach)) continue;
            if (Blightroots.Any(b => Vector2.Distance(b.Position, at) < BlightTuning.Spacing)) continue;
            var root = new Blightroot { Position = at, PulseTimer = _blightRng.Range(1.5f, BlightTuning.PulseEvery) };
            var area = new CorruptArea { Center = at, Radius = BlightTuning.Reach, Cells = field.CellsWithin(at, BlightTuning.Reach) };
            root.Area = Areas.Count;
            Areas.Add(area);
            root.Ring = field.CellsWithin(at, BlightTuning.Reach, BlightTuning.Reach * BlightTuning.RingInner);
            root.CellDistance = area.Cells.Select(i => Vector2.Distance(CorruptionField.CellCentre(i % CorruptionField.N, i / CorruptionField.N), at)).ToArray();
            Blightroots.Add(root);
            for (int v = 0; v < BlightTuning.VinesPer; v++)
            {
                float a = MathF.Tau * v / BlightTuning.VinesPer + _blightRng.Range(-0.6f, 0.6f);
                var vine = new Gloomvine { Root = root, RestDir = new Vector2(MathF.Cos(a), MathF.Sin(a)), Curl = _blightRng.Range(-1.2f, 1.2f) };
                vine.Points.Add(at);
                Vines.Add(vine);
                while (vine.Length < BlightTuning.Rest && Extend(vine, Rested(vine))) { }
            }
        }
    }

    /// <summary>Stash approaches (a shell cache's, a secret's, a curse den's spur), and on deeper levels a neck of the main way.</summary>
    void PlaceValves()
    {
        for (int i = 0; i < Map.Pois.Count; i++)
        {
            var poi = Map.Pois[i];
            if (poi.Kind is not (PoiKind.ShellCache or PoiKind.Secret or PoiKind.CurseDen)) continue;
            var spur = Map.Corridors.FirstOrDefault(c => c.Poi == i && c.Points.Count >= 2);
            if (spur is null) continue;
            // From the end at the place, walk back along the spur to just outside it.
            var pts = Vector2.Distance(spur.Points[^1], poi.Position) < Vector2.Distance(spur.Points[0], poi.Position) ? Enumerable.Reverse(spur.Points).ToList() : spur.Points;
            float want = poi.Radius + 2.5f, walked = 0f;
            for (int k = 1; k < pts.Count; k++)
            {
                float seg = Vector2.Distance(pts[k - 1], pts[k]);
                if (walked + seg < want)
                {
                    walked += seg;
                    continue;
                }
                var dir = SafeNormalize(pts[k] - pts[k - 1]);
                var at = pts[k - 1] + dir * (want - walked);
                if (Map.IsOpen(at)) AddValve(at, dir, spur.HalfWidth);
                break;
            }
        }
        if (Map.Level >= 2)
        {
            var neck = Map.Corridors.Where(c => c.Kind == CorridorKind.Main && c.Points.Count >= 4).OrderBy(c => c.Width).FirstOrDefault();
            if (neck is not null)
            {
                int mid = neck.Points.Count / 2;
                var at = neck.Points[mid];
                bool clear = Vector2.Distance(at, Map.Start.Position) > 25f && Vector2.Distance(at, Map.Exit.Position) > Map.Exit.Radius + 12f
                             && Valves.All(v => Vector2.Distance(v.Center, at) > 15f) && Map.IsOpen(at);
                if (clear) AddValve(at, SafeNormalize(neck.Points[mid + 1] - neck.Points[mid - 1]), neck.HalfWidth);
            }
        }
    }

    void AddValve(Vector2 at, Vector2 dir, float halfWidth)
    {
        if (dir == Vector2.Zero) return;
        Valves.Add(new Valve { Center = at, Normal = dir, Across = new Vector2(-dir.Y, dir.X), HalfWidth = halfWidth + 0.8f });
    }

    float BudInterval()
    {
        float interval = BlightTuning.BudInterval / (1f + 0.6f * Menace);
        return Cleansed >= CorruptionTuning.SlowBudding ? interval * 2f : interval;
    }

    int BudCap => BlightTuning.CapBase + (int)(Menace * BlightTuning.CapPerMenace);

    /// <summary>The war's turn, after combat: her light, pops, Blightroots, vines, valves, murklings, the arena.</summary>
    void StepCorruption()
    {
        var field = Corruption;
        if (field is null) return;
        var p = Player;

        // Her light cleanses the ground beneath her (Lantern Pearl's glow reaches farther).
        float glow = Run.Loadout.Stats[Stat.Glow];
        field.Cleanse(p.Position, CorruptionTuning.HerRadius * (0.8f + 0.2f * glow), CorruptionTuning.HerRate * Dt);
        // Her bubbles cleanse a burst where they pop, wider for a bigger bubble.
        foreach (var (at, bubbles, dim) in _pops)
            field.Cleanse(at, MathF.Min(CorruptionTuning.PopRadius + CorruptionTuning.PopPerBubble * bubbles, CorruptionTuning.PopMax), CorruptionTuning.PopStrength * dim);

        StepBlightroots(field);
        StepVines(field);
        StepValves();
        StepMurklings(field);
        _pops.Clear();

        if (Tick % 15 == 0)
            foreach (var area in Areas)
            {
                area.Cleansed = field.CleansedOf(area.Cells);
                if (!area.Arena && area.Cleansed >= BlightTuning.AreaSpent) area.Spent = true;
            }
        StepCondensation();

        // Drains (clinging, coiling, a valve) show as one hit every so often.
        _drainClock += Dt;
        if (_drained > 0f && _drainClock >= BlightTuning.DrainReport)
        {
            Events.Add(new PlaneEvent(PlaneEventType.PlayerDrained, p.Position, Vector2.Zero, _drained, _drainSource));
            _drained = 0f;
            _drainClock = 0f;
        }
    }

    /// <summary>Her light drunk away (no hit grace: a steady drain), unless shielded or dashing.</summary>
    void Drain(float amount, DamageSource source)
    {
        var p = Player;
        if (Defeated || amount <= 0f || p.ShieldTimer > 0f || p.DashInvulnerableTimer > 0f) return;
        p.Hp -= amount;
        Stats.DamageTaken += amount;
        LastHitSource = _drainSource = source;
        _drained += amount;
        if (p.Hp <= 0f)
        {
            p.Hp = 0f;
            p.Velocity = Vector2.Zero;
            Defeated = true;
            Events.Add(new PlaneEvent(PlaneEventType.PlayerDefeated, p.Position, Vector2.Zero, 0f, source));
        }
        Run.Hp = p.Hp;
    }

    /// <summary>One of her bubbles popped: its light cleanses (and cuts vines, wears valves) after combat.</summary>
    void LightReleased(PlaneShot shot)
    {
        if (Corruption is null || !shot.FromPlayer) return;
        _pops.Add((shot.Position, shot.Bubbles, shot.Dim));
    }

    /// <summary>A dash: clinging murklings are shaken off into the ink cloud, and a coiling vine lets go.</summary>
    void DashFreed()
    {
        if (Corruption is null) return;
        foreach (var m in Murklings)
            if (m.Alive && m.Clinging) BurstMurkling(m, ember: false);
        foreach (var vine in Vines)
            if (vine.Coiling) LetGo(vine);
        Player.RootTimer = 0f;
    }

    // ───────────────────────── Blightroots ─────────────────────────

    void StepBlightroots(CorruptionField field)
    {
        bool regrow = field.Cleansed < CorruptionTuning.StopRegrowth && Tick % 4 == 0;
        foreach (var root in Blightroots)
        {
            var area = Areas[root.Area];
            if (!root.Alive)
            {
                root.Dead += Dt;
                // The burst: a wave of cleansing through its whole reach.
                if (root.BurstRadius >= 0f && root.BurstRadius < BlightTuning.Reach)
                {
                    root.BurstRadius = MathF.Min(BlightTuning.Reach, root.BurstRadius + BlightTuning.Reach / BlightTuning.BurstSeconds * Dt);
                    field.Cleanse(root.Position, root.BurstRadius, 0.5f);
                }
                continue;
            }
            if (regrow)
                for (int k = 0; k < area.Cells.Count; k++)
                {
                    int i = area.Cells[k];
                    // Strongest at the stump, still creeping at the rim.
                    float near = 1f - 0.6f * root.CellDistance[k] / BlightTuning.Reach;
                    field.Raise(i, field.Initial[i], BlightTuning.Regrow * near * Dt * 4f);
                }

            if (Tick % 15 == 0)
            {
                root.RingDensity = field.Mean(root.Ring);
                if (root.RingDensity < BlightTuning.StarveBelow)
                {
                    // Starved: it shrivels and bursts into motes; its vines dissolve.
                    root.Alive = false;
                    root.BurstRadius = 0f;
                    root.PulseRadius = -1f;
                    foreach (var vine in Vines.Where(v => v.Root == root))
                    {
                        if (vine.Coiling) LetGo(vine);
                        vine.Dissolving = 0f;
                    }
                    Events.Add(new PlaneEvent(PlaneEventType.BlightrootBurst, root.Position, Vector2.Zero, BlightTuning.Reach));
                    continue;
                }
            }

            // The pulse: the sac inflates (the telegraph), then a ring of ink runs out over its reach.
            root.PulseTimer -= Dt;
            root.Inflate = root.PulseTimer < BlightTuning.Telegraph ? 1f - MathF.Max(root.PulseTimer, 0f) / BlightTuning.Telegraph : MathF.Max(0f, root.Inflate - Dt * 4f);
            if (root.PulseTimer <= BlightTuning.Telegraph && root.PulseTimer + Dt > BlightTuning.Telegraph)
                Events.Add(new PlaneEvent(PlaneEventType.BlightrootInflate, root.Position, Vector2.Zero));
            if (root.PulseTimer <= 0f)
            {
                root.PulseTimer = BlightTuning.PulseEvery;
                root.PulseRadius = 0.6f;
                root.PulseId = ++_pulses;
                Events.Add(new PlaneEvent(PlaneEventType.BlightrootPulse, root.Position, Vector2.Zero, BlightTuning.Reach));
            }
            if (root.PulseRadius >= 0f)
            {
                float r0 = root.PulseRadius;
                float r1 = root.PulseRadius = r0 + BlightTuning.PulseSpeed * Dt;
                // It stains what it crosses...
                for (int k = 0; k < area.Cells.Count; k++)
                {
                    float d = root.CellDistance[k];
                    if (d >= r0 && d < r1) field.Raise(area.Cells[k], MathF.Min(BlightTuning.PulseStain, field.Initial[area.Cells[k]]), 1f);
                }
                // ...and dims her bubbles it crosses (once per ring).
                foreach (var shot in Shots)
                {
                    if (!shot.FromPlayer || shot.Life <= 0f || shot.Pulse == root.PulseId) continue;
                    float d = Vector2.Distance(shot.Position, root.Position);
                    if (MathF.Abs(d - r1) > BlightTuning.PulseBand * 0.5f + shot.Radius) continue;
                    shot.Pulse = root.PulseId;
                    DimBubble(shot, BlightTuning.PulseDim);
                }
                if (r1 >= BlightTuning.Reach) root.PulseRadius = -1f;
            }

            // The sac drinks the light of bubbles that reach it: no damage, no pop.
            foreach (var shot in Shots)
            {
                if (!shot.FromPlayer || shot.Life <= 0f) continue;
                if (Vector2.Distance(shot.Position, root.Position) > BlightTuning.SacRadius + shot.Radius) continue;
                shot.Life = -1000f;
                Events.Add(new PlaneEvent(PlaneEventType.BlightrootDrank, root.Position, SafeNormalize(shot.Velocity), shot.Bubbles));
            }
        }
    }

    int _pulses;

    /// <summary>A bubble loses some of its light (and with it, damage); one too dim pops.</summary>
    void DimBubble(PlaneShot shot, float keep)
    {
        shot.Dim *= keep;
        shot.Damage *= keep;
        if (shot.Dim < 0.12f) Pop(shot);
    }

    // ───────────────────────── gloomvines ─────────────────────────

    /// <summary>The way a resting vine grows: out from its root, curling.</summary>
    static Vector2 Rested(Gloomvine vine)
    {
        float a = MathF.Atan2(vine.RestDir.Y, vine.RestDir.X) + vine.Curl * vine.Length / BlightTuning.Rest;
        return new Vector2(MathF.Cos(a), MathF.Sin(a));
    }

    /// <summary>One segment more toward dir (false where rock is in the way).</summary>
    bool Extend(Gloomvine vine, Vector2 dir)
    {
        var tip = vine.Points[^1];
        var next = tip + SafeNormalize(dir) * BlightTuning.Segment;
        if (!Map.IsOpen(next) || vine.Length + BlightTuning.Segment > BlightTuning.MaxLength) return false;
        vine.Points.Add(next);
        return true;
    }

    void LetGo(Gloomvine vine)
    {
        vine.Coiling = false;
        vine.CoilTime = 0f;
        int drop = (int)(BlightTuning.Retract / BlightTuning.Segment);
        int keep = Math.Max(1, vine.Points.Count - drop);
        vine.Points.RemoveRange(keep, vine.Points.Count - keep);
    }

    void StepVines(CorruptionField field)
    {
        var p = Player;
        _linger = p.Velocity.Length() < BlightTuning.LingerSpeed ? _linger + Dt : 0f;
        p.RootTimer = MathF.Max(0f, p.RootTimer - Dt);
        for (int i = Vines.Count - 1; i >= 0; i--)
        {
            var vine = Vines[i];
            if (vine.Dissolving >= 0f)
            {
                vine.Dissolving += Dt;
                if (vine.Dissolving >= BlightTuning.DissolveSeconds) Vines.RemoveAt(i);
                continue;
            }
            // Cut by a pop: everything past the cut dissolves.
            foreach (var (at, bubbles, _) in _pops)
            {
                float r = MathF.Min(CorruptionTuning.PopRadius + CorruptionTuning.PopPerBubble * bubbles, CorruptionTuning.PopMax) * 0.6f;
                int cut = vine.Points.FindIndex(1, q => Vector2.Distance(q, at) < r);
                if (cut < 0) continue;
                if (vine.Coiling) LetGo(vine);
                if (cut < vine.Points.Count) vine.Points.RemoveRange(cut, vine.Points.Count - cut);
                Events.Add(new PlaneEvent(PlaneEventType.VineCut, at, Vector2.Zero));
            }
            var tip = vine.Points[^1];
            if (vine.Coiling)
            {
                // Coiled: she is held while it drinks her light, then it lets go.
                vine.CoilTime += Dt;
                p.RootTimer = MathF.Max(p.RootTimer, Dt * 2f);
                Drain(BlightTuning.CoilDrain * Dt, DamageSource.Gloomvine);
                vine.Points[^1] = Vector2.Lerp(tip, p.Position, 0.3f);
                if (vine.CoilTime >= BlightTuning.CoilSeconds) LetGo(vine);
                continue;
            }
            bool reachable = Vector2.Distance(vine.Root.Position, p.Position) <= BlightTuning.MaxLength + 1f;
            if (_linger >= BlightTuning.Linger && reachable)
            {
                // She lingers: the tip creeps toward her, slower than she ever swims.
                var to = p.Position - tip;
                float step = BlightTuning.Creep * Dt;
                if (to.Length() > 0.05f)
                {
                    var moved = tip + SafeNormalize(to) * step;
                    if (Map.IsOpen(moved))
                    {
                        if (vine.Points.Count >= 2 && Vector2.Distance(vine.Points[^2], moved) >= BlightTuning.Segment)
                        {
                            if (vine.Length + BlightTuning.Segment <= BlightTuning.MaxLength) vine.Points.Add(moved);
                        }
                        else if (vine.Points.Count >= 2) vine.Points[^1] = moved;
                        else vine.Points.Add(moved);
                    }
                }
            }
            else if (vine.Length < BlightTuning.Rest && field.At(vine.Root.Position) > 0.3f && Tick % (int)(BlightTuning.Segment / BlightTuning.Regrow2 / Dt) == 0)
                Extend(vine, Rested(vine));

            if (Vector2.Distance(vine.Points[^1], p.Position) < BlightTuning.CoilReach + Radius && p.DashTimer <= 0f && p.DashInvulnerableTimer <= 0f && p.ShieldTimer <= 0f && !Defeated)
            {
                vine.Coiling = true;
                vine.CoilTime = 0f;
                Events.Add(new PlaneEvent(PlaneEventType.VineCoiled, p.Position, Vector2.Zero));
            }
        }
    }

    // ───────────────────────── valves ─────────────────────────

    void StepValves()
    {
        var p = Player;
        foreach (var v in Valves)
        {
            if (v.Cleared) continue;
            var rel = p.Position - v.Center;
            float along = Vector2.Dot(rel, v.Normal), side = Vector2.Dot(rel, v.Across);
            if (!v.Seen && rel.Length() < BlightTuning.SeenRange && LineOfSight(p.Position, v.Center))
            {
                v.Seen = true;
                Events.Add(new PlaneEvent(PlaneEventType.ValveSeen, v.Center, v.Normal));
            }
            bool inside = MathF.Abs(along) <= v.HalfThickness && MathF.Abs(side) <= v.HalfWidth;
            if (inside)
            {
                if (v.Entered == 0)
                {
                    float before = Vector2.Dot(p.PrevPosition - v.Center, v.Normal);
                    v.Entered = before < 0f ? -1 : 1;
                }
                // Light passes through dark, at a price (nothing while dashing).
                if (p.DashTimer <= 0f)
                    Drain(BlightTuning.ValveCost * v.Integrity * Vector2.Distance(p.Position, p.PrevPosition) / (v.HalfThickness * 2f) * 1.6f, DamageSource.Valve);
            }
            else
            {
                if (v.Entered != 0 && MathF.Abs(side) <= v.HalfWidth + 0.5f && MathF.Sign(along) == -v.Entered)
                {
                    Clear(v);
                    continue;
                }
                v.Entered = 0;
                // Her light at its edges wears it down.
                if (MathF.Abs(side) <= v.HalfWidth + 1.5f && MathF.Abs(along) <= v.HalfThickness + BlightTuning.EdgeReach)
                    v.Integrity -= BlightTuning.EdgeWear * Dt;
            }
            foreach (var (at, bubbles, _) in _pops)
                if (Vector2.Distance(at, v.Center) < v.HalfWidth + 1f) v.Integrity -= BlightTuning.PopWear * bubbles;
            if (v.Integrity <= 0f) Clear(v);
        }
    }

    void Clear(Valve v)
    {
        v.Cleared = true;
        v.Integrity = 0f;
        v.Entered = 0;
        Events.Add(new PlaneEvent(PlaneEventType.ValveCleared, v.Center, v.Normal));
    }

    // ───────────────────────── murklings ─────────────────────────

    void StepMurklings(CorruptionField field)
    {
        var p = Player;
        // Budding: from the dense ground of each living area (the arena only while its boss fights).
        foreach (var area in Areas)
        {
            if (area.Spent) continue;
            if (area.Arena ? Boss is not { Stage: BossStage.Fight } : !Blightroots.Any(b => b.Alive && Areas[b.Area] == area)) continue;
            area.BudTimer -= Dt;
            if (area.BudTimer > 0f) continue;
            area.BudTimer = area.Arena ? BlightTuning.ArenaInterval : BudInterval();
            int index = Areas.IndexOf(area);
            if (Murklings.Count(m => m.Alive && m.Area == index) >= BudCap + (area.Arena ? 1 : 0)) continue;
            for (int tries = 0; tries < 12 && area.Cells.Count > 0; tries++)
            {
                int cell = area.Cells[_blightRng.Int(area.Cells.Count)];
                var at = CorruptionField.CellCentre(cell % CorruptionField.N, cell / CorruptionField.N);
                if (field.Density[cell] < BlightTuning.BudDensity || Vector2.Distance(at, p.Position) < BlightTuning.BudClearance || !Clear(at, BlightTuning.MurkRadius)) continue;
                Bud(at, index);
                break;
            }
        }

        foreach (var m in Murklings)
        {
            if (!m.Alive) continue;
            if (m.Budding)
            {
                m.Bud -= Dt;
                if (!m.Budding) Events.Add(new PlaneEvent(PlaneEventType.MurklingEmerged, m.Position, Vector2.Zero));
                continue;
            }
            if (m.Clinging)
            {
                m.Position = p.Position + m.ClingOffset;
                Drain(BlightTuning.ClingDrain * Dt, DamageSource.Murkling);
                continue;
            }
            // Toward the brightest light: her, or a bubble of hers hanging at the end of its range (bait).
            Vector2 target = p.Position;
            float best = 1f / (1f + 0.15f * Vector2.Distance(m.Position, p.Position));
            bool her = true;
            foreach (var shot in Shots)
            {
                if (!shot.FromPlayer || shot.Life <= 0f || shot.Rest <= 0f) continue;
                float d = Vector2.Distance(m.Position, shot.Position);
                float bright = MathF.Min(0.4f + 0.06f * shot.Bubbles, 1.3f) * shot.Dim;
                float score = bright / (1f + 0.15f * d);
                if (d < BlightTuning.Sense && score > best)
                {
                    best = score;
                    target = shot.Position;
                    her = false;
                }
            }
            m.Target = target;
            var to = target - m.Position;
            float dist = to.Length();
            Vector2 wish = dist < BlightTuning.Sense ? SafeNormalize(to) : Vector2.Zero;
            // A wobble off the line, as if the dark were reluctant.
            wish += new Vector2(MathF.Sin(Time * 1.7f + m.Seed), MathF.Cos(Time * 1.3f + m.Seed * 2f)) * 0.35f;
            m.Velocity = MoveToward(m.Velocity, SafeNormalize(wish) * BlightTuning.MurkSpeed, 4f * Dt);
            Move(ref m.Position, ref m.Velocity, BlightTuning.MurkRadius, report: false);

            // It clings on touch.
            if (Vector2.Distance(m.Position, p.Position) < BlightTuning.MurkRadius + Radius)
            {
                if (HurtPlayer(BlightTuning.ContactDamage, SafeNormalize(p.Position - m.Position), DamageSource.Murkling) && Clingers < BlightTuning.MaxClingers)
                {
                    m.Clinging = true;
                    m.ClingOffset = SafeNormalize(m.Position - p.Position) * (Radius * 0.8f);
                    Events.Add(new PlaneEvent(PlaneEventType.MurklingCling, m.Position, Vector2.Zero));
                }
                continue;
            }
            // It throws at her when she is near and in sight.
            m.ThrowTimer -= Dt;
            if (her && dist < BlightTuning.ThrowRange && m.ThrowTimer <= 0f && LineOfSight(m.Position, p.Position))
            {
                m.ThrowTimer = BlightTuning.ThrowEvery * _blightRng.Range(0.8f, 1.2f);
                var dir = SafeNormalize(p.Position - m.Position);
                MurkShots.Add(new MurkShot { Position = m.Position + dir * BlightTuning.MurkRadius, Velocity = dir * BlightTuning.ThrowSpeed });
                Events.Add(new PlaneEvent(PlaneEventType.MurklingThrow, m.Position, dir));
            }
        }

        // Its throws: they drain light on hit; rock stops them.
        foreach (var s in MurkShots)
        {
            s.Position += s.Velocity * Dt;
            s.Life -= Dt;
            if (!Map.IsOpen(s.Position)) s.Life = 0f;
            else if (Vector2.Distance(s.Position, p.Position) < Radius + BlightTuning.ThrowRadius)
            {
                s.Life = 0f;
                HurtPlayer(BlightTuning.ThrowDamage, SafeNormalize(s.Velocity), DamageSource.MurkShot);
            }
        }

        // Her bubbles: one bubble's light bursts a murkling (or a bud), and a throw it meets; either way it loses half its light.
        foreach (var shot in Shots)
        {
            if (!shot.FromPlayer || shot.Life <= 0f) continue;
            foreach (var m in Murklings)
            {
                if (!m.Alive || m.Clinging || Vector2.Distance(m.Position, shot.Position) > BlightTuning.MurkRadius + shot.Radius) continue;
                BurstMurkling(m, ember: true, shot.Damage);
                DimBubble(shot, BlightTuning.BubbleDim);
                if (shot.Life <= 0f) break;
            }
            if (shot.Life <= 0f) continue;
            foreach (var s in MurkShots)
            {
                if (s.Life <= 0f || Vector2.Distance(s.Position, shot.Position) > BlightTuning.ThrowRadius + shot.Radius) continue;
                s.Life = 0f;
                DimBubble(shot, BlightTuning.BubbleDim);
                if (shot.Life <= 0f) break;
            }
        }
        MurkShots.RemoveAll(s => s.Life <= 0f);
        Murklings.RemoveAll(m => !m.Alive);
    }

    void Bud(Vector2 at, int area)
    {
        Murklings.Add(new Murkling { Position = at, Area = area, Seed = _blightRng.Range(0f, 100f), ThrowTimer = BlightTuning.ThrowEvery * _blightRng.Range(0.5f, 1f) });
        Events.Add(new PlaneEvent(PlaneEventType.MurklingBud, at, Vector2.Zero, BlightTuning.BudSeconds));
    }

    /// <summary>
    /// A murkling bursts. Over cleansed ground its stolen ember falls and blooms a patch of colour (not when shaken off
    /// into the ink cloud by a dash).
    /// </summary>
    void BurstMurkling(Murkling m, bool ember, float damage = 0f)
    {
        m.Alive = false;
        Stats.MobsDefeated++;
        bool blooms = ember && Corruption is { } field && field.At(m.Position) < CorruptionTuning.Clean;
        if (blooms) Corruption!.Cleanse(m.Position, CorruptionTuning.EmberRadius, 1f);
        Events.Add(new PlaneEvent(PlaneEventType.MurklingBurst, m.Position, Vector2.Zero, blooms ? CorruptionTuning.EmberRadius : 0f));
        if (damage > 0f) Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, m.Position, Vector2.Zero, damage));
    }

    /// <summary>An ambush clearing springs: murklings bud in a ring round her (the pufferlings are retracted).</summary>
    void AmbushBuds(PlaneAmbush ambush)
    {
        var p = Player;
        float turn = _blightRng.Range(0f, MathF.Tau);
        int spawned = 0;
        for (int i = 0; i < BlightTuning.AmbushBuds; i++)
        {
            float a = turn + MathF.Tau * i / BlightTuning.AmbushBuds;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            foreach (float r in new[] { 5f, 3.8f, 2.8f })
            {
                var at = p.Position + dir * r;
                if (!Clear(at, BlightTuning.MurkRadius)) continue;
                Bud(at, -1);
                spawned++;
                break;
            }
        }
        ambush.Spawned = spawned;
    }

    // ───────────────────────── the arena ─────────────────────────

    /// <summary>The condensation: the boss's crust follows the arena cleansed; the Crack opens once she is freed and it is clean enough.</summary>
    void StepCondensation()
    {
        if (Boss is not { } boss || ArenaArea is not { } arena) return;
        int crust = Math.Clamp(CorruptionTuning.CrustLayers - (int)(arena.Cleansed / (1f / CorruptionTuning.CrustLayers) + 1e-4f), 0, CorruptionTuning.CrustLayers);
        if (crust < boss.Crust)
            Events.Add(new PlaneEvent(PlaneEventType.CrustBroken, boss.Position, Vector2.Zero, crust));
        boss.Crust = crust;
        if (boss.Freed && !ExitOpen && arena.Cleansed >= CorruptionTuning.ArenaToOpen)
        {
            ExitOpen = true;
            Events.Add(new PlaneEvent(PlaneEventType.CrackOpened, ArenaCenter, Vector2.Zero));
        }
    }
}
