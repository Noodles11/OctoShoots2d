using System.Collections.Generic;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// What Clementine carries from room to room through the rift: her HP and the pearls she has collected (and the
/// loadout they add up to). A new run starts empty; going through the gateway keeps it.
/// </summary>
public sealed class PlaneRun
{
    /// <summary>
    /// The pearls ported to the plane so far — the ones that change her shots the most. Treasure rooms hold these.
    /// </summary>
    public static readonly string[] ShotPearls =
    {
        "triple_tentacle", "hammerhead", "anglerfish_lure", "swordfish_bill", "mirror_scale", "boomerang_shrimp", "double_helix",
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
    public int Room { get; set; } = 1;

    public float MaxHp => Loadout.Stats.MaxHp;

    public void Add(string itemId)
    {
        Items.Add(itemId);
        Rebuild();
    }

    void Rebuild() => Loadout = Loadout.Build(Catalog, Items, Tuning);

    /// <summary>A pearl a treasure room may hold: a ported one the catalog knows and she does not have yet.</summary>
    public bool CanOffer(string itemId) => Catalog is not null && Catalog.Contains(itemId) && !Items.Contains(itemId);
}
