using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// F3: the whole <see cref="LevelMap"/> drawn top-down: height bands, corridors, plazas, canopy, POIs in their landmark
/// colours, loot under the floor, the spawn table, and Clementine. A debugging view, not the game's Tab map.
/// </summary>
public partial class DebugMapOverlay : Control
{
    const float Scale = 4f;

    LevelMap? _map;
    ImageTexture? _texture;
    System.Numerics.Vector2 _player;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public void SetMap(LevelMap map)
    {
        _map = map;
        var image = Image.CreateEmpty(LevelMap.Size, LevelMap.Size, false, Image.Format.Rgba8);
        for (int y = 0; y < LevelMap.Size; y++)
        for (int x = 0; x < LevelMap.Size; x++)
        {
            float h = map.HeightAt(new System.Numerics.Vector2(x + 0.5f, y + 0.5f));
            Color c = h < -0.5f ? new Color(0.1f, 0.2f, 0.35f).Lerp(new Color(0.02f, 0.05f, 0.15f), Mathf.Clamp(-h / 8f, 0f, 1f))
                : h <= LevelMap.BlockHeight ? new Color(0.85f, 0.78f, 0.6f)
                : new Color(0.45f, 0.5f, 0.38f).Lerp(new Color(0.25f, 0.2f, 0.18f), Mathf.Clamp(h / 16f, 0f, 1f));
            image.SetPixel(x, y, c);
        }
        _texture = ImageTexture.CreateFromImage(image);
        QueueRedraw();
    }

    public void SetPlayer(System.Numerics.Vector2 p)
    {
        _player = p;
        if (Visible) QueueRedraw();
    }

    Vector2 Origin => new((Size.X - LevelMap.Size * Scale) * 0.5f, (Size.Y - LevelMap.Size * Scale) * 0.5f);

    Vector2 P(System.Numerics.Vector2 p) => Origin + new Vector2(p.X, p.Y) * Scale;

    public override void _Draw()
    {
        if (_map is null || _texture is null) return;
        var map = _map;
        var o = Origin;
        DrawRect(new Rect2(o - Vector2.One * 6f, Vector2.One * (LevelMap.Size * Scale + 12f)), new Color(0f, 0f, 0f, 0.75f));
        DrawTextureRect(_texture, new Rect2(o, Vector2.One * LevelMap.Size * Scale), false);

        foreach (var c in map.Corridors)
        {
            var col = c.Kind switch { CorridorKind.Main => new Color(0.6f, 0.45f, 0.2f, 0.9f), CorridorKind.Spur => new Color(0.5f, 0.5f, 0.5f, 0.9f), _ => new Color(0.9f, 0.3f, 0.9f, 0.9f) };
            for (int i = 1; i < c.Points.Count; i++) DrawLine(P(c.Points[i - 1]), P(c.Points[i]), col, 1.5f);
        }
        DrawCircle(P(map.Shaft.Center), map.Shaft.HalfLength * Scale, new Color(0.3f, 0.6f, 1f, 0.6f));
        foreach (var z in map.Plazas) DrawArc(P(z.Center), z.Radius * Scale, 0f, Mathf.Tau, 32, new Color(1f, 1f, 1f, 0.8f), 1.5f);
        foreach (var c in map.Canopies)
        {
            var col = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            if (c.Radius > 0f) DrawArc(P(c.Center), c.Radius * Scale, 0f, Mathf.Tau, 24, col, 2f);
            else DrawLine(P(c.EndA), P(c.EndB), c.Kind == CanopyKind.Arch ? new Color(0.1f, 0.1f, 0.1f) : col, c.HalfWidth * 2f * Scale * 0.5f);
        }
        foreach (var r in map.WeakRocks) DrawCircle(P(r.Center), r.Radius * Scale, new Color(0.6f, 0.45f, 0.4f));
        foreach (var c in map.Coins) DrawString(ThemeDB.FallbackFont, P(c.Position) + new Vector2(-3f, 4f), "x", HorizontalAlignment.Left, -1, 11, new Color(1f, 0.85f, 0.3f));
        foreach (var p in map.Pockets) DrawArc(P(p.Position), p.Radius * Scale, 0f, Mathf.Tau, 12, new Color(0.4f, 0.9f, 1f), 1.5f);
        foreach (var s in map.Spawns)
        {
            var col = s.Role switch
            {
                SpawnRole.Den => new Color(1f, 0.3f, 0.3f),
                SpawnRole.Ambush => new Color(0.75f, 0.35f, 1f),
                SpawnRole.Patrol => new Color(1f, 1f, 0.3f),
                SpawnRole.Guardian => new Color(1f, 0.6f, 0.2f),
                _ => new Color(1f, 0.55f, 0.75f),
            };
            if (s.Path is { } path)
                for (int i = 1; i < path.Count; i++) DrawLine(P(path[i - 1]), P(path[i]), new Color(col, 0.6f), 2f);
            DrawRect(new Rect2(P(s.Position) - Vector2.One * 3f, Vector2.One * 6f), col);
        }
        foreach (var poi in map.Pois)
        {
            DrawCircle(P(poi.Position), 6f, LevelView.PoiColor(poi.Kind));
            DrawArc(P(poi.Position), poi.Radius * Scale, 0f, Mathf.Tau, 32, LevelView.PoiColor(poi.Kind), 2f);
        }
        DrawCircle(P(_player), 5f, Colors.White);
        DrawArc(P(_player), 8f, 0f, Mathf.Tau, 16, Colors.Black, 2f);

        var font = ThemeDB.FallbackFont;
        string legend = $"F3 map · seed {map.Seed:X} attempt {map.Attempt + 1} · {map.Corridors.Count(c => c.Kind == CorridorKind.Main)} corridors · {map.Plazas.Count} plazas · " +
                        $"{map.Canopies.Count(c => c.Kind == CanopyKind.Arch)} arches · {map.Caves.Count} caves · {(map.HasBoss ? "boss" : "blue hole")} · {map.Spawns.Count} spawn rows";
        DrawString(font, o + new Vector2(0f, -12f), legend, HorizontalAlignment.Left, -1, 14, Colors.White);
    }
}
