using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Bosses;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The boss intro's cast, drawn as chibi anime characters (DESIGN-TOPDOWN §6.4, THEME-BIBLE §6.8): thick soft
/// outlines, flat cel colour with one highlight, big glossy eyes. Clementine, and a boss for each body form in its own
/// generated colours, goofy at Depth 1 and menacing by Depth 6 (round eyes to slit pupils, a smile to teeth, colour
/// drained, a cold rim). Everything is drawn round (0, 0) at a size s (about the body's radius); the caller places it.
/// </summary>
public static class BossIntroArt
{
    public enum Mood { Calm, Shock, Gulp, Determined, Taunt }

    static readonly Color Line = new(0.12f, 0.06f, 0.1f);
    static readonly Color Body = new(1f, 0.55f, 0.26f);
    static readonly Color Glow = new(1f, 0.77f, 0.42f);

    // ───────────────────────── shapes ─────────────────────────

    /// <summary>Points round an ellipse (or an arc of it); a whole one never repeats its first point (the polygon fill would refuse it).</summary>
    public static Vector2[] Ellipse(Vector2 c, float rx, float ry, int n = 28, float from = 0f, float to = Mathf.Tau)
    {
        bool whole = Mathf.Abs(to - from - Mathf.Tau) < 1e-4f;
        var pts = new Vector2[whole ? n : n + 1];
        for (int i = 0; i < pts.Length; i++)
        {
            float a = from + (to - from) * i / n;
            pts[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }
        return pts;
    }

    /// <summary>A filled shape with a soft dark outline (the cel look).</summary>
    static void Inked(CanvasItem ci, Vector2[] pts, Color fill, float s, float line = 0.06f)
    {
        ci.DrawColoredPolygon(pts, fill);
        var closed = new Vector2[pts.Length + 1];
        Array.Copy(pts, closed, pts.Length);
        closed[^1] = pts[0];
        ci.DrawPolyline(closed, Line, s * line, true);
    }

    static void Blob(CanvasItem ci, Vector2 c, float rx, float ry, Color fill, float s, float line = 0.06f) =>
        Inked(ci, Ellipse(c, rx, ry, 30), fill, s, line);

    /// <summary>A four-pointed sparkle.</summary>
    public static void Sparkle(CanvasItem ci, Vector2 c, float r, Color col)
    {
        if (r <= 0.5f) return;
        var pts = new Vector2[8];
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.Pi / 4f;
            float rr = i % 2 == 0 ? r : r * 0.28f;
            pts[i] = c + new Vector2(Mathf.Cos(a - Mathf.Pi / 2f), Mathf.Sin(a - Mathf.Pi / 2f)) * rr;
        }
        ci.DrawColoredPolygon(pts, col);
    }

    /// <summary>The anime sweat-drop: a pale blue teardrop with a glint.</summary>
    public static void SweatDrop(CanvasItem ci, Vector2 c, float s)
    {
        var pts = new List<Vector2> { c + new Vector2(0f, -s * 1.5f) };
        pts.AddRange(Ellipse(c, s, s, 18, -0.15f * Mathf.Pi, 1.15f * Mathf.Pi));
        Inked(ci, pts.ToArray(), new Color(0.72f, 0.9f, 1f), s * 6f, 0.035f);
        ci.DrawCircle(c + new Vector2(-s * 0.35f, -s * 0.2f), s * 0.25f, Colors.White);
    }

    // ───────────────────────── faces ─────────────────────────

    /// <summary>
    /// Big glossy eyes and a mouth, for a given mood; menace (0 goofy → 1 menacing) narrows the pupils to slits and
    /// gives the grin teeth. Look leans the irises (−1 left … 1 right). Blink 0..1 closes them.
    /// </summary>
    public static void Face(CanvasItem ci, Vector2 c, float s, Mood mood, float menace, float look, float blink, Color iris, float t)
    {
        float gap = s * 0.36f;
        bool shock = mood is Mood.Shock or Mood.Gulp;
        for (int side = -1; side <= 1; side += 2)
        {
            var e = c + new Vector2(side * gap, 0f);
            float rx = s * 0.21f * (shock ? 1.12f : 1f), ry = s * 0.27f * (shock ? 1.15f : 1f) * (1f - 0.92f * blink);
            // A wink while taunting (the left eye).
            if (mood == Mood.Taunt && menace < 0.5f && side < 0) ry = s * 0.02f;
            if (ry < s * 0.04f)
            {
                ci.DrawArc(e + new Vector2(0f, -s * 0.02f), rx, 0.15f * Mathf.Pi, 0.85f * Mathf.Pi, 12, Line, s * 0.06f, true);
                continue;
            }
            Blob(ci, e, rx, ry, Colors.White, s, 0.05f);
            if (menace >= 0.45f && !shock)
            {
                // Slit pupils in a hot eye.
                Inked(ci, Ellipse(e, rx * 0.85f, ry * 0.85f, 20), iris.Lerp(new Color(1f, 0.85f, 0.2f), 0.6f), s, 0.02f);
                ci.DrawColoredPolygon(Ellipse(e + new Vector2(look * rx * 0.2f, 0f), rx * 0.18f, ry * 0.78f, 16), Line);
            }
            else
            {
                float ir = shock ? 0.45f : 0.72f;
                var at = e + new Vector2(look * rx * 0.28f, ry * 0.08f);
                ci.DrawColoredPolygon(Ellipse(at, rx * ir, ry * ir, 20), iris);
                ci.DrawColoredPolygon(Ellipse(at + new Vector2(0f, ry * 0.12f), rx * ir * 0.55f, ry * ir * 0.55f, 16), iris.Darkened(0.55f));
                // Two highlights, the anime glint.
                ci.DrawCircle(at + new Vector2(-rx * 0.3f, -ry * 0.32f), rx * 0.28f, Colors.White);
                ci.DrawCircle(at + new Vector2(rx * 0.25f, ry * 0.25f), rx * 0.12f, Colors.White);
            }
            // Brows: knitted for a determined or taunting face (always, past half menace).
            if (mood is Mood.Determined || mood == Mood.Taunt && menace >= 0.3f || menace >= 0.6f)
            {
                var a = e + new Vector2(-side * rx * 1.1f, -ry * 1.25f);
                var b = e + new Vector2(side * rx * 0.9f, -ry * 1.55f);
                if (mood is Mood.Determined || menace >= 0.3f) (a, b) = (e + new Vector2(-side * rx * 0.9f, -ry * 1.15f) + new Vector2(side * 0f, 0f), e + new Vector2(side * rx * 1.1f, -ry * 1.6f));
                ci.DrawLine(a, b, Line, s * 0.07f, true);
            }
        }
        var m = c + new Vector2(0f, s * 0.34f);
        switch (mood)
        {
            case Mood.Gulp:
                Blob(ci, m, s * 0.07f, s * 0.09f, new Color(0.45f, 0.1f, 0.15f), s, 0.04f);
                // The gulp travels down her throat.
                ci.DrawArc(m + new Vector2(0f, s * 0.22f + Mathf.Sin(t * 9f) * s * 0.03f), s * 0.05f, 0f, Mathf.Pi, 8, Line, s * 0.03f, true);
                break;
            case Mood.Shock:
                Blob(ci, m, s * 0.1f, s * 0.13f, new Color(0.45f, 0.1f, 0.15f), s, 0.04f);
                break;
            case Mood.Determined:
                ci.DrawArc(m + new Vector2(0f, -s * 0.06f), s * 0.16f, 0.1f * Mathf.Pi, 0.9f * Mathf.Pi, 14, Line, s * 0.055f, true);
                break;
            default:
            {
                int teeth = Mathf.RoundToInt(menace * 6f);
                if (teeth > 0)
                {
                    // A grin with teeth, wider the deeper it is.
                    float w = s * (0.22f + 0.12f * menace);
                    var grin = Ellipse(m, w, s * 0.13f, 16, 0f, Mathf.Pi);
                    var mouth = new List<Vector2> { m + new Vector2(-w, 0f) };
                    mouth.AddRange(grin);
                    Inked(ci, mouth.ToArray(), new Color(0.3f, 0.05f, 0.1f), s, 0.04f);
                    for (int i = 0; i < teeth; i++)
                    {
                        float x0 = -w + 2f * w * i / teeth, x1 = -w + 2f * w * (i + 1) / teeth;
                        ci.DrawColoredPolygon(new[] { m + new Vector2(x0, 0f), m + new Vector2(x1, 0f), m + new Vector2((x0 + x1) * 0.5f, s * 0.08f) }, Colors.White);
                    }
                }
                else
                {
                    // The cat smile.
                    ci.DrawArc(m + new Vector2(-s * 0.07f, -s * 0.03f), s * 0.07f, 0f, Mathf.Pi, 10, Line, s * 0.045f, true);
                    ci.DrawArc(m + new Vector2(s * 0.07f, -s * 0.03f), s * 0.07f, 0f, Mathf.Pi, 10, Line, s * 0.045f, true);
                    if (mood == Mood.Taunt) Blob(ci, m + new Vector2(0f, s * 0.09f), s * 0.07f, s * 0.08f, new Color(1f, 0.45f, 0.55f), s, 0.035f);
                }
                break;
            }
        }
        if (menace < 0.6f)
            for (int side = -1; side <= 1; side += 2)
                ci.DrawColoredPolygon(Ellipse(c + new Vector2(side * gap * 1.25f, s * 0.24f), s * 0.11f, s * 0.05f, 14), new Color(1f, 0.45f, 0.5f, 0.45f * (1f - menace)));
    }

    // ───────────────────────── Clementine ─────────────────────────

    /// <summary>Clementine: her tangerine bell, warm glow, gold gonad rings, trailing tentacles and her face.</summary>
    public static void Clementine(CanvasItem ci, float s, float t, Mood mood, float look)
    {
        // Her glow.
        for (int i = 4; i >= 1; i--) ci.DrawCircle(new Vector2(0f, -s * 0.1f), s * (0.9f + 0.28f * i), Glow with { A = 0.07f });
        // Tentacles: soft, waving, tapering.
        for (int k = 0; k < 8; k++)
        {
            float bx = -s * 0.78f + s * 1.56f * k / 7f;
            var prev = new Vector2(bx, s * 0.28f);
            for (int j = 1; j <= 10; j++)
            {
                float u = j / 10f;
                var p = new Vector2(bx * (1f - 0.25f * u) + Mathf.Sin(t * 3.2f + k * 0.9f + u * 4f) * s * 0.16f * u, s * 0.28f + s * 1.25f * u);
                ci.DrawLine(prev, p, new Color(1f, 0.7f, 0.45f, 0.85f - 0.4f * u), s * (0.075f - 0.05f * u), true);
                prev = p;
            }
        }
        // Two frilly oral arms in the middle.
        for (int k = -1; k <= 1; k += 2)
        {
            var prev = new Vector2(k * s * 0.12f, s * 0.3f);
            for (int j = 1; j <= 8; j++)
            {
                float u = j / 8f;
                var p = new Vector2(k * s * 0.12f + Mathf.Sin(t * 2.4f + k + u * 5f) * s * 0.12f, s * 0.3f + s * 0.95f * u);
                ci.DrawLine(prev, p, Glow with { A = 0.9f }, s * (0.12f - 0.06f * u), true);
                prev = p;
            }
        }
        // The bell: a soft dome, a little scalloped at its rim, breathing.
        float breathe = 1f + 0.04f * Mathf.Sin(t * 4f);
        var bell = new List<Vector2>();
        bell.AddRange(Ellipse(new Vector2(0f, s * 0.28f), s * 1.05f * breathe, s * 1.15f / breathe, 30, Mathf.Pi, Mathf.Tau));
        for (int i = 1; i < 8; i++)
        {
            float x = s * 1.05f * breathe * (1f - 2f * i / 8f);
            bell.Add(new Vector2(x, s * (0.28f + (i % 2 == 0 ? 0.05f : 0.12f))));
        }
        Inked(ci, bell.ToArray(), Body with { A = 0.96f }, s);
        // Shine.
        ci.DrawColoredPolygon(Ellipse(new Vector2(-s * 0.45f, -s * 0.5f), s * 0.28f, s * 0.16f, 16), new Color(1f, 1f, 1f, 0.45f));
        // Her four gold gonad rings, up in the dome.
        for (int i = 0; i < 4; i++)
        {
            var at = new Vector2(-s * 0.42f + s * 0.28f * i, -s * 0.62f + (i is 1 or 2 ? -s * 0.08f : 0f));
            ci.DrawArc(at, s * 0.08f, 0.85f * Mathf.Pi, 2.15f * Mathf.Pi, 12, Glow with { A = 0.8f }, s * 0.04f, true);
        }
        float blink = BlinkAt(t, 2.7f);
        Face(ci, new Vector2(0f, -s * 0.12f), s, mood, 0f, look, blink, new Color(0.35f, 0.15f, 0.08f), t);
    }

    /// <summary>A quick blink now and then (0 open … 1 shut).</summary>
    public static float BlinkAt(float t, float every)
    {
        float k = t % every;
        return k < 0.12f ? Mathf.Sin(k / 0.12f * Mathf.Pi) : 0f;
    }

    // ───────────────────────── bosses ─────────────────────────

    /// <summary>The boss's colours: its own, drained and darkened with menace.</summary>
    static (Color Body, Color Accent, Color Rim) Colours(BossSpec spec, float menace)
    {
        var body = new Color(spec.TrueColor.X, spec.TrueColor.Y, spec.TrueColor.Z);
        var accent = new Color(spec.Accent.X, spec.Accent.Y, spec.Accent.Z);
        if (body.V < 0.25f) body = body.Lightened(0.3f);
        float drain = 0.55f * menace;
        body = body.Lerp(new Color(body.V, body.V, body.V), drain).Darkened(0.4f * menace);
        accent = accent.Lerp(new Color(accent.V, accent.V, accent.V), drain * 0.6f);
        var rim = new Color(0.55f, 0.85f, 1f).Lerp(accent, 0.3f);
        return (body, accent, rim);
    }

    /// <summary>
    /// The boss, by its body form, doing its gag (g: seconds since it slammed in), in its mood. The mutations that read
    /// at this size show: ink sacs as dark bumps; more arms, spines, bells, flaps and whorls come from its Count.
    /// </summary>
    public static void Boss(CanvasItem ci, BossSpec spec, float s, float g, float menace, Mood mood, float t)
    {
        var (body, accent, rim) = Colours(spec, menace);
        // A cold rim of light behind it as the depths darken.
        if (menace > 0.2f)
            for (int i = 3; i >= 1; i--) ci.DrawCircle(Vector2.Zero, s * (1.2f + 0.22f * i), rim with { A = 0.08f * menace });
        var iris = accent.Darkened(0.2f);
        float blink = BlinkAt(t + 1.3f, 3.1f);
        switch (spec.Form)
        {
            case BodyForm.GiantClam: Clam(ci, spec, s, g, menace, mood, t, body, accent, iris, blink); break;
            case BodyForm.SeaStar: Star(ci, spec, s, g, menace, mood, t, body, accent, iris, blink); break;
            case BodyForm.Urchin: Urchin(ci, spec, s, g, menace, mood, t, body, accent, iris, blink); break;
            case BodyForm.MoonJelly: Jelly(ci, s, g, menace, mood, t, body, accent, iris, blink); break;
            case BodyForm.Siphonophore: Siphonophore(ci, spec, s, g, menace, mood, t, body, accent, iris, blink); break;
            case BodyForm.SeaHare: SeaHare(ci, spec, s, g, menace, mood, t, body, accent, iris, blink); break;
            default: Snail(ci, spec, s, g, menace, mood, t, body, accent, iris, blink); break;
        }
        if (spec.Theme == MutationTheme.InkSacs || spec.Minor == MutationTheme.InkSacs)
            for (int i = 0; i < 3; i++)
                Blob(ci, new Vector2(-s * 0.55f + s * 0.5f * i, s * 0.55f + (i == 1 ? s * 0.12f : 0f)), s * 0.13f, s * 0.11f, new Color(0.12f, 0.03f, 0.2f), s, 0.04f);
    }

    /// <summary>The giant clam: snaps its shell twice, then blows a bubble raspberry; a pearl crown on top.</summary>
    static void Clam(CanvasItem ci, BossSpec spec, float s, float g, float menace, Mood mood, float t, Color body, Color accent, Color iris, float blink)
    {
        // Open, snap-snap, open wide.
        float open = g < 0.15f ? 1f : g < 0.5f ? Mathf.Abs(Mathf.Cos((g - 0.15f) / 0.35f * Mathf.Pi * 2f)) : 1f;
        open = 0.25f + 0.75f * open;
        var lower = new List<Vector2>();
        lower.AddRange(Ellipse(new Vector2(0f, s * 0.15f), s * 1.25f, s * 0.75f, 30, 0f, Mathf.Pi));
        Inked(ci, Scallop(lower, s * 0.06f), body, s);
        for (int i = 1; i < 7; i++)
        {
            float a = Mathf.Pi * i / 7f;
            ci.DrawLine(new Vector2(0f, s * 0.15f), new Vector2(Mathf.Cos(a) * s * 1.1f, s * 0.15f + Mathf.Sin(a) * s * 0.65f), body.Darkened(0.25f), s * 0.04f, true);
        }
        // The mantle inside, wavy, and the face peeking out of it.
        float lift = s * 1.0f * open;
        var mantle = new List<Vector2>();
        for (int i = 0; i <= 16; i++)
        {
            float x = -s * 1.1f + s * 2.2f * i / 16f;
            mantle.Add(new Vector2(x, s * 0.15f - lift * 0.15f + Mathf.Sin(i * 1.3f + t * 5f) * s * 0.05f));
        }
        for (int i = 16; i >= 0; i--) mantle.Add(new Vector2(-s * 1.1f + s * 2.2f * i / 16f, s * 0.2f));
        if (open > 0.4f) Inked(ci, Ellipse(new Vector2(0f, s * 0.15f), s * 1.05f, lift * 0.75f + s * 0.05f, 28, Mathf.Pi, Mathf.Tau), accent.Darkened(0.35f), s, 0.04f);
        Inked(ci, mantle.ToArray(), accent, s, 0.04f);
        // Its face, peeking out over the mantle.
        if (open > 0.4f) Face(ci, new Vector2(0f, s * 0.08f - lift * 0.45f), s * 0.72f, mood, menace, -0.6f, blink, iris, t);
        // The upper shell, lifted.
        var upper = new List<Vector2>();
        upper.AddRange(Ellipse(new Vector2(0f, s * 0.15f - lift), s * 1.25f, s * 0.7f, 30, Mathf.Pi, Mathf.Tau));
        Inked(ci, Scallop(upper, s * 0.06f), body.Lightened(0.08f), s);
        for (int i = 1; i < 7; i++)
        {
            float a = Mathf.Pi + Mathf.Pi * i / 7f;
            ci.DrawLine(new Vector2(0f, s * 0.15f - lift), new Vector2(Mathf.Cos(a) * s * 1.1f, s * 0.15f - lift + Mathf.Sin(a) * s * 0.6f), body.Darkened(0.2f), s * 0.04f, true);
        }
        ci.DrawColoredPolygon(Ellipse(new Vector2(-s * 0.5f, -s * 0.35f - lift), s * 0.3f, s * 0.1f, 14), new Color(1f, 1f, 1f, 0.3f));
        // The pearl crown.
        var top = new Vector2(0f, s * 0.15f - lift - s * 0.7f);
        var crown = new[] { top + new Vector2(-s * 0.32f, s * 0.05f), top + new Vector2(-s * 0.34f, -s * 0.28f), top + new Vector2(-s * 0.16f, -s * 0.1f), top + new Vector2(0f, -s * 0.34f), top + new Vector2(s * 0.16f, -s * 0.1f), top + new Vector2(s * 0.34f, -s * 0.28f), top + new Vector2(s * 0.32f, s * 0.05f) };
        Inked(ci, crown, new Color(1f, 0.8f, 0.3f), s, 0.04f);
        foreach (var tip in new[] { crown[1], crown[3], crown[5] }) Blob(ci, tip, s * 0.06f, s * 0.06f, new Color(0.98f, 0.96f, 0.92f), s, 0.025f);
        // The raspberry: a bubble swelling out of its mouth, then popping.
        if (g > 0.6f && g < 1.35f && open > 0.4f)
        {
            float k = (g - 0.6f) / 0.75f;
            var at = new Vector2(s * 0.1f, s * 0.3f - lift * 0.35f + s * 0.25f);
            if (k < 0.85f) Inked(ci, Ellipse(at + new Vector2(s * 0.3f * k, 0f), s * 0.45f * k, s * 0.4f * k, 20), new Color(1f, 0.7f, 0.8f, 0.55f), s, 0.025f);
            else for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.Tau / 6f;
                    var from = at + new Vector2(s * 0.3f, 0f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * 0.3f;
                    ci.DrawLine(from, from + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * 0.18f, new Color(1f, 0.7f, 0.8f), s * 0.04f, true);
                }
        }
    }

    static Vector2[] Scallop(List<Vector2> arc, float depth)
    {
        // A wavy edge: every other point pushed out.
        for (int i = 1; i < arc.Count - 1; i += 2) arc[i] *= 1f + depth / Mathf.Max(arc[i].Length(), 1f);
        return arc.ToArray();
    }

    /// <summary>The sea star: spins in, stops in a pose with a wink and a sparkle.</summary>
    static void Star(CanvasItem ci, BossSpec spec, float s, float g, float menace, Mood mood, float t, Color body, Color accent, Color iris, float blink)
    {
        int arms = Math.Clamp(spec.Count, 5, 8);
        float spin = g < 0.8f ? (1f - Mathf.SmoothStep(0f, 1f, g / 0.8f)) * Mathf.Tau * 2f : Mathf.Sin(t * 2f) * 0.08f;
        var pts = new Vector2[arms * 2];
        for (int i = 0; i < arms * 2; i++)
        {
            float a = spin - Mathf.Pi / 2f + i * Mathf.Pi / arms;
            float r = i % 2 == 0 ? s * 1.25f : s * 0.55f;
            // The raised arm (the pose).
            if (i == 2 && g > 0.8f) r *= 1.12f;
            pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        Inked(ci, Smooth(pts, 3), body, s);
        for (int i = 0; i < arms; i++)
        {
            float a = spin - Mathf.Pi / 2f + i * Mathf.Tau / arms;
            for (int j = 1; j <= 3; j++) ci.DrawCircle(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * (0.35f + 0.25f * j), s * 0.05f, accent);
        }
        ci.DrawColoredPolygon(Ellipse(new Vector2(-s * 0.3f, -s * 0.45f), s * 0.2f, s * 0.08f, 12), new Color(1f, 1f, 1f, 0.35f));
        Face(ci, new Vector2(0f, s * 0.02f), s * 0.75f, g > 0.8f ? mood : Mood.Calm, menace, 0f, blink, iris, t);
        if (g > 0.8f && g < 1.6f) Sparkle(ci, new Vector2(-s * 0.55f, -s * 0.35f), s * 0.25f * Mathf.Sin((g - 0.8f) / 0.8f * Mathf.Pi), Colors.White);
    }

    /// <summary>Corner-rounding: each corner replaced by points along a short curve.</summary>
    static Vector2[] Smooth(Vector2[] pts, int steps)
    {
        var o = new List<Vector2>();
        for (int i = 0; i < pts.Length; i++)
        {
            var a = (pts[(i - 1 + pts.Length) % pts.Length] + pts[i]) * 0.5f;
            var b = pts[i];
            var c = (pts[i] + pts[(i + 1) % pts.Length]) * 0.5f;
            for (int k = 0; k < steps; k++)
            {
                float u = k / (float)steps;
                o.Add(a.Lerp(b, u).Lerp(b.Lerp(c, u), u));
            }
        }
        return o.ToArray();
    }

    /// <summary>The urchin: puffs its spines out in angry huffs.</summary>
    static void Urchin(CanvasItem ci, BossSpec spec, float s, float g, float menace, Mood mood, float t, Color body, Color accent, Color iris, float blink)
    {
        float puff = Mathf.Max(0f, Mathf.Sin(g * 7f)) * Mathf.Exp(-g * 0.8f) + 0.15f * Mathf.Max(0f, Mathf.Sin(t * 3f));
        int spines = 22 + 2 * Math.Clamp(spec.Count, 0, 6);
        for (int i = 0; i < spines; i++)
        {
            float a = i * Mathf.Tau / spines + 0.1f * Mathf.Sin(t * 2f + i);
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var side = new Vector2(-dir.Y, dir.X) * s * 0.06f;
            float len = s * (0.55f + 0.45f * puff + 0.08f * (i % 3));
            var root = dir * s * 0.7f;
            Inked(ci, new[] { root + side, root + dir * len, root - side }, accent.Darkened(0.2f), s, 0.03f);
        }
        Blob(ci, Vector2.Zero, s * 0.82f * (1f + 0.08f * puff), s * 0.78f * (1f + 0.08f * puff), body, s);
        ci.DrawColoredPolygon(Ellipse(new Vector2(-s * 0.3f, -s * 0.4f), s * 0.22f, s * 0.1f, 12), new Color(1f, 1f, 1f, 0.35f));
        Face(ci, new Vector2(0f, s * 0.02f), s * 0.8f, mood == Mood.Calm ? Mood.Taunt : mood, Mathf.Max(menace, 0.35f), 0f, blink, iris, t);
    }

    /// <summary>The moon jelly: a wobbling, sparkly bell with four horseshoes and a fringe.</summary>
    static void Jelly(CanvasItem ci, float s, float g, float menace, Mood mood, float t, Color body, Color accent, Color iris, float blink)
    {
        float w = Mathf.Sin(t * 5f) * 0.08f;
        for (int k = 0; k < 14; k++)
        {
            float bx = -s * 1.1f + s * 2.2f * k / 13f;
            ci.DrawLine(new Vector2(bx, s * 0.25f), new Vector2(bx + Mathf.Sin(t * 3f + k) * s * 0.12f, s * 0.95f), body with { A = 0.6f }, s * 0.03f, true);
        }
        var bell = Ellipse(new Vector2(0f, s * 0.25f), s * 1.2f * (1f + w), s * 1.1f * (1f - w), 30, Mathf.Pi, Mathf.Tau);
        Inked(ci, bell, body with { A = 0.85f }, s);
        for (int i = 0; i < 4; i++)
            ci.DrawArc(new Vector2(-s * 0.54f + s * 0.36f * i, -s * 0.55f), s * 0.11f, 0.85f * Mathf.Pi, 2.15f * Mathf.Pi, 12, accent with { A = 0.75f }, s * 0.05f, true);
        ci.DrawColoredPolygon(Ellipse(new Vector2(-s * 0.5f, -s * 0.55f), s * 0.25f, s * 0.1f, 14), new Color(1f, 1f, 1f, 0.45f));
        Face(ci, new Vector2(0f, -s * 0.05f), s * 0.8f, mood, menace, 0f, blink, iris, t);
        for (int i = 0; i < 4; i++)
        {
            float a = t * 1.5f + i * Mathf.Tau / 4f;
            Sparkle(ci, new Vector2(Mathf.Cos(a) * s * 1.45f, Mathf.Sin(a) * s * 0.9f - s * 0.2f), s * 0.12f * (0.6f + 0.4f * Mathf.Sin(t * 6f + i)), Colors.White);
        }
    }

    /// <summary>The siphonophore: a conga line of little bells, bumping along behind the one with the face.</summary>
    static void Siphonophore(CanvasItem ci, BossSpec spec, float s, float g, float menace, Mood mood, float t, Color body, Color accent, Color iris, float blink)
    {
        int bells = Math.Clamp(spec.Count, 3, 6);
        for (int i = bells - 1; i >= 0; i--)
        {
            float size = s * (0.62f - 0.07f * i);
            // The conga step: each bell hops a beat after the one before it.
            float hop = Mathf.Max(0f, Mathf.Sin(t * 6f - i * 0.9f)) * s * 0.15f;
            float sway = Mathf.Sin(t * 3f - i * 0.9f) * s * 0.1f;
            var at = new Vector2(s * 0.85f * i - s * 0.6f + sway, s * 0.25f * i - hop);
            ci.DrawLine(at + new Vector2(0f, size * 0.3f), at + new Vector2(Mathf.Sin(t * 4f + i) * size * 0.3f, size * 1.3f), accent, s * 0.035f, true);
            Inked(ci, Ellipse(at + new Vector2(0f, size * 0.3f), size, size * 0.95f, 24, Mathf.Pi, Mathf.Tau), (i % 2 == 0 ? body : body.Lerp(accent, 0.3f)) with { A = 0.9f }, s, 0.05f);
            ci.DrawColoredPolygon(Ellipse(at + new Vector2(-size * 0.4f, -size * 0.35f), size * 0.25f, size * 0.1f, 12), new Color(1f, 1f, 1f, 0.4f));
            if (i == 0) Face(ci, at + new Vector2(0f, size * 0.02f), size * 0.95f, mood, menace, -0.5f, blink, iris, t);
            else
            {
                // Little closed eyes, happy in the line.
                ci.DrawArc(at + new Vector2(-size * 0.25f, 0f), size * 0.12f, Mathf.Pi * 1.1f, Mathf.Pi * 1.9f, 8, Line, s * 0.03f, true);
                ci.DrawArc(at + new Vector2(size * 0.25f, 0f), size * 0.12f, Mathf.Pi * 1.1f, Mathf.Pi * 1.9f, 8, Line, s * 0.03f, true);
            }
        }
    }

    /// <summary>The sea hare: a soft blob chewing a strand of weed, its ear-like feelers twitching.</summary>
    static void SeaHare(CanvasItem ci, BossSpec spec, float s, float g, float menace, Mood mood, float t, Color body, Color accent, Color iris, float blink)
    {
        // The flaps along its back.
        int flaps = Math.Clamp(spec.Count, 2, 5);
        for (int i = 0; i < flaps; i++)
        {
            float x = -s * 0.7f + s * 1.0f * i / Math.Max(flaps - 1, 1);
            Inked(ci, Ellipse(new Vector2(x, -s * 0.38f), s * 0.32f, s * (0.3f + 0.05f * Mathf.Sin(t * 3f + i)), 18, Mathf.Pi, Mathf.Tau), accent, s, 0.045f);
        }
        Blob(ci, new Vector2(0f, 0f), s * 1.3f, s * 0.62f, body, s);
        // Ears (rhinophores), twitching.
        for (int k = -1; k <= 1; k += 2)
        {
            float twitch = Mathf.Sin(t * 9f + k) * (Mathf.Sin(t * 1.3f) > 0.6f ? 0.35f : 0.05f);
            var root = new Vector2(-s * 0.72f + k * s * 0.18f, -s * 0.42f);
            float a = -Mathf.Pi / 2f + k * 0.35f + twitch - 0.25f;
            // A soft rabbit-ear: an ellipse along its own line, rolled at the tip.
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var across = new Vector2(-dir.Y, dir.X);
            var mid = root + dir * s * 0.3f;
            var ear = new Vector2[24];
            for (int i = 0; i < ear.Length; i++)
            {
                float u = i * Mathf.Tau / ear.Length;
                ear[i] = mid + dir * Mathf.Cos(u) * s * 0.32f + across * Mathf.Sin(u) * s * 0.11f;
            }
            Inked(ci, ear, body.Lightened(0.1f), s, 0.045f);
            ci.DrawLine(mid - dir * s * 0.12f, mid + dir * s * 0.2f, accent with { A = 0.7f }, s * 0.05f, true);
        }
        ci.DrawColoredPolygon(Ellipse(new Vector2(-s * 0.3f, -s * 0.25f), s * 0.35f, s * 0.1f, 14), new Color(1f, 1f, 1f, 0.3f));
        Face(ci, new Vector2(-s * 0.6f, -s * 0.05f), s * 0.7f, mood == Mood.Calm ? Mood.Calm : mood, menace, -0.6f, blink, iris, t);
        // Chewing: a strand of weed going in, the mouth working.
        float chew = Mathf.Abs(Mathf.Sin(t * 8f));
        float left = Mathf.Max(0f, 1f - g / 2.4f);
        var mouth = new Vector2(-s * 0.6f, s * 0.22f);
        if (left > 0f) ci.DrawLine(mouth, mouth + new Vector2(-s * 0.7f * left, s * 0.2f * Mathf.Sin(t * 5f)), new Color(0.35f, 0.75f, 0.3f), s * 0.07f, true);
        Blob(ci, mouth, s * 0.08f, s * (0.03f + 0.05f * chew), new Color(0.35f, 0.08f, 0.12f), s, 0.03f);
    }

    /// <summary>The snail: slides in on its foot, eyestalks boinging on springs, its shell a spiral of whorls.</summary>
    static void Snail(CanvasItem ci, BossSpec spec, float s, float g, float menace, Mood mood, float t, Color body, Color accent, Color iris, float blink)
    {
        // The foot.
        Blob(ci, new Vector2(-s * 0.1f, s * 0.45f), s * 1.35f, s * 0.32f, body.Lightened(0.15f), s);
        // The shell, a spiral of whorls.
        var shellAt = new Vector2(s * 0.25f, -s * 0.15f);
        Blob(ci, shellAt, s * 0.85f, s * 0.8f, accent, s);
        int whorls = Math.Clamp(spec.Count, 2, 5);
        var spiral = new List<Vector2>();
        for (int i = 0; i <= 60; i++)
        {
            float u = i / 60f, a = u * Mathf.Tau * whorls * 0.6f;
            spiral.Add(shellAt + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * 0.78f * (1f - u * 0.92f));
        }
        ci.DrawPolyline(spiral.ToArray(), Line, s * 0.05f, true);
        ci.DrawColoredPolygon(Ellipse(shellAt + new Vector2(-s * 0.3f, -s * 0.45f), s * 0.25f, s * 0.1f, 12), new Color(1f, 1f, 1f, 0.35f));
        // The head and its eyestalks, boinging (a spring settling after each slide).
        var head = new Vector2(-s * 0.85f, s * 0.15f);
        Blob(ci, head, s * 0.45f, s * 0.4f, body.Lightened(0.15f), s);
        float boing = Mathf.Sin(g * 14f) * Mathf.Exp(-g * 2.5f) + 0.15f * Mathf.Sin(t * 5f);
        for (int k = -1; k <= 1; k += 2)
        {
            var root = head + new Vector2(k * s * 0.15f, -s * 0.3f);
            var tip = root + new Vector2(k * s * 0.25f, -s * (0.6f + 0.3f * boing));
            ci.DrawLine(root, tip, Line, s * 0.11f, true);
            ci.DrawLine(root, tip, body.Lightened(0.15f), s * 0.06f, true);
            Blob(ci, tip, s * 0.16f, s * 0.16f, Colors.White, s, 0.04f);
            if (menace >= 0.45f) ci.DrawColoredPolygon(Ellipse(tip, s * 0.03f, s * 0.12f, 10), Line);
            else ci.DrawCircle(tip + new Vector2(-k * s * 0.03f, s * 0.02f), s * 0.08f, iris);
        }
        // Its mouth (the eyes are on the stalks).
        var m = head + new Vector2(0f, s * 0.15f);
        if (mood == Mood.Taunt && menace < 0.5f) Blob(ci, m + new Vector2(0f, s * 0.05f), s * 0.06f, s * 0.07f, new Color(1f, 0.45f, 0.55f), s, 0.03f);
        ci.DrawArc(m, s * 0.12f, 0.1f * Mathf.Pi, 0.9f * Mathf.Pi, 10, Line, s * 0.04f, true);
    }
}
