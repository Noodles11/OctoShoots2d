using System.Collections.Generic;
using System.Linq;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// What Clementine carries from room to room through the rift: her HP and the pearls she has collected (and the
/// loadout they add up to). A new run starts empty; going through the gateway keeps it.
/// </summary>
public sealed class PlaneRun
{
    /// <summary>
    /// The pearls ported to the plane so far (docs/PEARLS.md marks them): treasure rooms, shops and Queen Clam offer
    /// only these, and only once unlocked (a pearl with an `unlock` waits for its achievement). Passive ones first,
    /// then the active ones.
    /// </summary>
    public static readonly string[] PortedPearls =
    {
        "triple_tentacle", "hammerhead", "anglerfish_lure", "swordfish_bill", "mirror_scale", "boomerang_shrimp", "double_helix",
        "coral_crown", "moon_jelly_heart", "shark_tooth", "pearl_diver", "starfish_arm", "ink_sac", "remora_sucker",
        "captains_hook", "lantern_pearl", "bubble_coral", "plankton_swarm", "mitosis", "giant_squid_eye",
        "lucky_sea_glass", "pirates_doubloon",
        "bubble_shield", "whale_song",
    };

    public PlaneRun(ItemCatalog? catalog, Tuning tuning)
    {
        Catalog = catalog;
        Tuning = tuning;
        Rebuild();
        Hp = MaxHp;
    }

    public ItemCatalog? Catalog { get; }
    public Tuning Tuning { get; }
    public List<string> Items { get; } = new();
    public Loadout Loadout { get; private set; } = null!;
    public float Hp { get; set; }
    /// <summary>Small shells, the currency: picked up in places and from every mob, spent in shops.</summary>
    public int Shells { get; set; }
    public LevelId Level { get; set; } = LevelId.First;

    public float MaxHp => Loadout.Stats.MaxHp;

    /// <summary>The active pearl she holds (the last one taken; one at a time), used with F.</summary>
    public ItemDef? Active => Loadout.Active;

    /// <summary>How charged the active pearl is, 0 → 1 (ready). A new one comes charged.</summary>
    public float ActiveCharge { get; set; } = 1f;

    public void Add(string itemId)
    {
        Items.Add(itemId);
        Rebuild();
        if (Active?.Id == itemId) ActiveCharge = 1f;
    }

    /// <summary>Debug: takes a pearl away again (every copy). Her HP stays within the new maximum.</summary>
    public bool Remove(string itemId)
    {
        if (Items.RemoveAll(id => id == itemId) == 0) return false;
        Rebuild();
        Hp = System.MathF.Min(Hp, MaxHp);
        return true;
    }

    void Rebuild() => Loadout = Loadout.Build(Catalog, Items, Tuning);

    /// <summary>
    /// The achievements earned (the profile's, shared: one earned mid-run unlocks its pearl from then on). Locked pearls
    /// are never offered.
    /// </summary>
    public ISet<string> Unlocked { get; set; } = new HashSet<string>();

    /// <summary>A pearl a treasure room may hold: a ported one the catalog knows, unlocked, and she does not have yet.</summary>
    public bool CanOffer(string itemId) =>
        Catalog is not null && Catalog.TryGet(itemId, out var item) && ItemPools.IsUnlocked(item, Unlocked) && !Items.Contains(itemId);
}
