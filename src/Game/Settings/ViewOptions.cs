using System.Text.Json;
using Godot;
using OctoShoots.Core;

namespace OctoShoots.Game.Settings;

/// <summary>Presentation and comfort options (§3, §11). Not part of the simulation.</summary>
public sealed class ViewOptions
{
    [Tune("View", 70f, 110f)] public float Fov = 90f;
    [Tune("View", 0.01f, 0.4f)] public float MouseSensitivity = 0.08f;
    [Tune("View")] public bool InvertY = false;
    [Tune("View")] public bool CameraShake = true;
    [Tune("View", 0f, 1f)] public float ShakeAmount = 0.5f;
    [Tune("View", 2f, 40f)] public float MistStart = 14f;
    [Tune("View", 8f, 80f)] public float MistEnd = 40f;
    [Tune("View", 10f, 80f)] public float MinimapRange = 32f;
    /// <summary>Frame-rate cap (0 = uncapped). Keeps the GPU cool; vsync caps it further on slower screens.</summary>
    [Tune("View", 0f, 240f)] public int MaxFps = 60;
    /// <summary>Strength of sunlight under water: god rays, caustics and dappled light (0 turns them off).</summary>
    [Tune("Light", 0f, 2f)] public float SunLight = 1f;
}

/// <summary>Loads and saves tuning and options as JSON in the user directory.</summary>
public static class SettingsStore
{
    const string TuningPath = "user://m0_tuning.json";
    const string ViewPath = "user://m0_view.json";

    static readonly JsonSerializerOptions ViewJson = new() { IncludeFields = true, WriteIndented = true };

    public static Tuning LoadTuning()
    {
        string? json = Read(TuningPath);
        if (json is null) return new Tuning();
        try
        {
            return Tuning.FromJson(json);
        }
        catch (JsonException e)
        {
            GD.PushWarning($"Ignoring unreadable {TuningPath}: {e.Message}");
            return new Tuning();
        }
    }

    public static ViewOptions LoadView()
    {
        string? json = Read(ViewPath);
        if (json is null) return new ViewOptions();
        try
        {
            return JsonSerializer.Deserialize<ViewOptions>(json, ViewJson) ?? new ViewOptions();
        }
        catch (JsonException e)
        {
            GD.PushWarning($"Ignoring unreadable {ViewPath}: {e.Message}");
            return new ViewOptions();
        }
    }

    public static void Save(Tuning tuning, ViewOptions view)
    {
        Write(TuningPath, tuning.ToJson());
        Write(ViewPath, JsonSerializer.Serialize(view, ViewJson));
    }

    public static string GlobalTuningPath => ProjectSettings.GlobalizePath(TuningPath);

    static string? Read(string path)
    {
        if (!FileAccess.FileExists(path)) return null;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return file?.GetAsText();
    }

    static void Write(string path, string text)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file?.StoreString(text);
    }
}
