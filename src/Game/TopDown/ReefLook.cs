using Godot;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// How a depth looks (THEME-BIBLE §6.2 light budget, §6.3 palette, §7 the depths): its water, seabed and reef colours,
/// its sun, how sharp its caustics and how strong its god rays, and its Menace (0 at Depth 1 → 1 at the Tank), which
/// thickens the murk, darkens the vignette and drains the colour. One look drives every reef shader through the
/// reef_* shader globals, the environment and the sun, so the descent is felt everywhere at once.
/// Warmth belongs to Clementine and to healthy life: the sun is a pale gold, never her tangerine.
/// </summary>
public sealed record ReefLook
{
    public required string Name { get; init; }
    /// <summary>Water near the swim level, and the deep water below it; murk is the deepest, light-starved colour.</summary>
    public required Color WaterShallow { get; init; }
    public required Color WaterDeep { get; init; }
    public required Color Murk { get; init; }
    public required Color Sand { get; init; }
    /// <summary>The reef's bare rock, and the life encrusting it: coral, a second coral, algae turf, sponges, a pale butter.</summary>
    public required Color Rock { get; init; }
    public required Color Coral { get; init; }
    public required Color Coral2 { get; init; }
    public required Color Algae { get; init; }
    public required Color Sponge { get; init; }
    public required Color Butter { get; init; }
    public required Color Sun { get; init; }
    public required float SunEnergy { get; init; }
    public required Color Ambient { get; init; }
    public required float AmbientEnergy { get; init; }
    /// <summary>Caustic and god-ray strength (0 none).</summary>
    public required float Caustics { get; init; }
    public required float Rays { get; init; }
    public required float Menace { get; init; }
    public required float Saturation { get; init; }
    public required float Vignette { get; init; }
    /// <summary>How thick the veil over the deeps is (0 crystal, 1 murk).</summary>
    public required float Fog { get; init; }
    /// <summary>
    /// The beneath-layer (THEME-BIBLE §6.7): how strongly the next depth shows through the floor, as shadows about
    /// 13 m down. Almost nothing through the Shallows' bright sand, more in its deeps; it becomes the scenery in the
    /// Trench and the Abyss.
    /// </summary>
    public float Beneath { get; init; } = 0.5f;
    /// <summary>Contrast after tone mapping (1 neutral): the Shallows want punchy light and shade.</summary>
    public float Contrast { get; init; } = 1f;
    /// <summary>The flora's palette: low and high ends for each of two variants per species group.</summary>
    public required Color FanA { get; init; }
    public required Color FanB { get; init; }
    public required Color SoftCoral { get; init; }
    public required Color HardCoral { get; init; }
    public required Color Grass { get; init; }

    static Color C(string hex) => Color.FromHtml(hex);

    /// <summary>The seven depths (THEME-BIBLE §6.3). Depth 1 is tuned; the rest follow the palette table, to be tuned as they are built.</summary>
    public static ReefLook For(int depth) => depth switch
    {
        <= 1 => Shallows,
        2 => Kelp,
        3 => Galleon,
        4 => Carnival,
        5 => Trench,
        6 => Abyss,
        _ => Tank,
    };

    /// <summary>Depth 1 — Sunlit Shallows · The Bright Ache: saturated and hopeful, turquoise over sand, pink coral, sharp caustics.</summary>
    public static readonly ReefLook Shallows = new()
    {
        Name = "Sunlit Shallows",
        WaterShallow = C("#7FDCD2"), WaterDeep = C("#3FBFBE"), Murk = C("#17707A"),
        Sand = C("#E3D2A0"), Rock = C("#C9C2AC"),
        Coral = C("#F089A8"), Coral2 = C("#B79BE0"), Algae = C("#A9C46B"), Sponge = C("#5EC7BC"), Butter = C("#F3DE8C"),
        Sun = C("#FFF8EC"), SunEnergy = 0.78f, Ambient = C("#3FA6C0"), AmbientEnergy = 0.13f,
        Caustics = 1f, Rays = 1f, Menace = 0f, Saturation = 1.3f, Vignette = 0.36f, Fog = 0.3f, Contrast = 1.22f, Beneath = 0.55f,
        FanA = C("#B48CDC"), FanB = C("#F28BB0"), SoftCoral = C("#F6A5C0"), HardCoral = C("#F1DDA2"), Grass = C("#8FC85A"),
    };

    public static readonly ReefLook Kelp = Shallows with
    {
        Name = "Kelp Jungle", Beneath = 0.6f,
        WaterShallow = C("#8FBF6F"), WaterDeep = C("#2E7D5B"), Murk = C("#173F31"),
        Sand = C("#B9B07E"), Rock = C("#5C6E46"), Coral = C("#C88AA0"), Coral2 = C("#8C8FBF"), Algae = C("#7FA84A"),
        Sun = C("#E8D878"), SunEnergy = 0.95f, Ambient = C("#7FB88A"), AmbientEnergy = 0.7f,
        Caustics = 0.6f, Rays = 0.75f, Menace = 0.15f, Saturation = 1f, Vignette = 0.4f, Fog = 0.45f,
    };

    public static readonly ReefLook Galleon = Shallows with
    {
        Name = "Sunken Galleon", Beneath = 0.7f,
        WaterShallow = C("#6B7A84"), WaterDeep = C("#4A5A66"), Murk = C("#1F272E"),
        Sand = C("#9C8C6A"), Rock = C("#7A5230"), Coral = C("#B07A6A"), Coral2 = C("#8A4B2E"), Algae = C("#6E7A4A"),
        Butter = C("#D9A441"), Sun = C("#F2C14E"), SunEnergy = 0.7f, Ambient = C("#6A7680"), AmbientEnergy = 0.5f,
        Caustics = 0.3f, Rays = 0.5f, Menace = 0.35f, Saturation = 0.9f, Vignette = 0.5f, Fog = 0.55f,
    };

    public static readonly ReefLook Carnival = Shallows with
    {
        Name = "Coral Carnival", Beneath = 0.78f,
        WaterShallow = C("#5A3E8A"), WaterDeep = C("#3A2A5E"), Murk = C("#1A1230"),
        Sand = C("#6E5A8A"), Rock = C("#4A3A6A"), Coral = C("#B44FD0"), Coral2 = C("#FF4FA3"), Sponge = C("#2FF3E0"), Butter = C("#9B5CFF"),
        Sun = C("#C9A8FF"), SunEnergy = 0.35f, Ambient = C("#5A4A8A"), AmbientEnergy = 0.45f,
        Caustics = 0f, Rays = 0f, Menace = 0.5f, Saturation = 1.15f, Vignette = 0.6f, Fog = 0.6f,
    };

    public static readonly ReefLook Trench = Shallows with
    {
        Name = "Twilight Trench", Beneath = 0.95f,
        WaterShallow = C("#12284A"), WaterDeep = C("#0B1B33"), Murk = C("#050B16"),
        Sand = C("#2A3346"), Rock = C("#1A2438"), Coral = C("#2E4A6A"), Coral2 = C("#22385A"), Algae = C("#1E3A4A"), Sponge = C("#7FE7FF"),
        Sun = C("#7FA8D8"), SunEnergy = 0.08f, Ambient = C("#16284A"), AmbientEnergy = 0.25f,
        Caustics = 0f, Rays = 0f, Menace = 0.72f, Saturation = 0.8f, Vignette = 0.75f, Fog = 0.8f,
    };

    public static readonly ReefLook Abyss = Trench with
    {
        Name = "The Abyss", Beneath = 1f,
        WaterShallow = C("#0A0E1C"), WaterDeep = C("#05070F"), Murk = C("#020308"),
        Rock = C("#0A0D16"), Sand = C("#10141E"), Coral = C("#FF3FA4"), Sponge = C("#2FF3E0"), Butter = C("#FFD25E"),
        SunEnergy = 0f, AmbientEnergy = 0.12f, Menace = 0.92f, Saturation = 0.75f, Vignette = 0.85f, Fog = 0.9f,
    };

    public static readonly ReefLook Tank = Shallows with
    {
        Name = "The Tank", Beneath = 0f,
        WaterShallow = C("#F4F8FF"), WaterDeep = C("#DCE6F5"), Murk = C("#9AA8C0"),
        Sand = C("#3A7BFF"), Rock = C("#C8D0E0"), Coral = C("#FF6BCB"), Coral2 = C("#7CFC4D"), Algae = C("#7CFC4D"), Butter = C("#FF4D5E"),
        Sun = C("#F4F8FF"), SunEnergy = 1.4f, Ambient = C("#F4F8FF"), AmbientEnergy = 1.2f,
        Caustics = 0f, Rays = 0f, Menace = 1f, Saturation = 1.2f, Vignette = 0.1f, Fog = 0.05f,
    };

    /// <summary>The depth below this one (the Tank has none: the Abyss's light comes up from it instead).</summary>
    static ReefLook NextDown(ReefLook look) =>
        look == Shallows ? Kelp : look == Kelp ? Galleon : look == Galleon ? Carnival : look == Carnival ? Trench : look == Trench ? Abyss : Tank;

    /// <summary>Sets the reef globals the shaders read (colours converted to linear).</summary>
    public void Apply()
    {
        static void V(string name, Color c)
        {
            var l = c.SrgbToLinear();
            RenderingServer.GlobalShaderParameterSet(name, new Vector3(l.R, l.G, l.B));
        }
        V("reef_water_shallow", WaterShallow);
        V("reef_water_deep", WaterDeep);
        V("reef_murk", Murk);
        V("reef_sand", Sand);
        V("reef_rock", Rock);
        V("reef_coral", Coral);
        V("reef_coral2", Coral2);
        V("reef_algae", Algae);
        V("reef_sponge", Sponge);
        V("reef_butter", Butter);
        V("reef_sun", Sun);
        RenderingServer.GlobalShaderParameterSet("reef_caustics", Caustics);
        // What lies below: the next depth's murk, as a shadow tint.
        var below = NextDown(this).Murk.SrgbToLinear();
        RenderingServer.GlobalShaderParameterSet("reef_beneath", new Vector4(Beneath, -14f, Menace, 0f));
        RenderingServer.GlobalShaderParameterSet("reef_beneath_tint", new Vector3(below.R, below.G, below.B));
        RenderingServer.GlobalShaderParameterSet("reef_rays", Rays);
        RenderingServer.GlobalShaderParameterSet("reef_menace", Menace);
        RenderingServer.GlobalShaderParameterSet("reef_saturation", Saturation);
        RenderingServer.GlobalShaderParameterSet("reef_vignette", Vignette);
        RenderingServer.GlobalShaderParameterSet("reef_fog", Fog);
        // The sea surface a little above the highest reef tops (they reach about 14 m), for the caustics and the rays.
        RenderingServer.GlobalShaderParameterSet("sea_surface_y", 18f);
        RenderingServer.GlobalShaderParameterSet("reef_extent", new Vector2(150f, 150f));
        RenderingServer.GlobalShaderParameterSet("sun_has_shadow", 0f);
    }

    /// <summary>The water around her: ambient light, background, and glow, for this depth.</summary>
    public void ApplyTo(Godot.Environment env)
    {
        env.BackgroundColor = Murk;
        env.AmbientLightColor = Ambient;
        env.AmbientLightEnergy = AmbientEnergy;
        env.FogLightColor = WaterDeep;
        env.FogDensity = 0.0015f + 0.006f * Menace;
        env.AdjustmentEnabled = true;
        env.AdjustmentContrast = Contrast;
        env.AdjustmentSaturation = 1f;
        env.AdjustmentBrightness = 1f;
    }
}
