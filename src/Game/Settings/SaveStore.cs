using Godot;
using OctoShoots.Core.Saves;

namespace OctoShoots.Game.Settings;

/// <summary>Reads and writes the save file in the user directory (format: Core <see cref="SaveCodec"/>).</summary>
public static class SaveStore
{
    const string Path = "user://save.json";

    public static string GlobalPath => ProjectSettings.GlobalizePath(Path);

    /// <summary>The save on disk, or a fresh one if there is none or it can't be read (the bad file is kept aside).</summary>
    public static SaveFile Load()
    {
        LastLoadFailed = false;
        if (!FileAccess.FileExists(Path)) return new SaveFile();
        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        string json = file?.GetAsText() ?? "";
        try
        {
            return SaveCodec.Deserialize(json);
        }
        catch (SaveException e)
        {
            GD.PushWarning($"Save could not be read ({e.Message}); starting a fresh profile. The old file is kept as save.bad.json");
            DirAccess.CopyAbsolute(GlobalPath, ProjectSettings.GlobalizePath("user://save.bad.json"));
            LastLoadFailed = true;
            return new SaveFile();
        }
    }

    /// <summary>Writes the save atomically: to a temporary file first, then renamed over the old one.</summary>
    public static void Write(SaveFile save)
    {
        const string temp = "user://save.tmp";
        using (var file = FileAccess.Open(temp, FileAccess.ModeFlags.Write))
        {
            if (file is null) return;
            file.StoreString(SaveCodec.Serialize(save));
        }
        DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(temp), GlobalPath);
    }

    /// <summary>Whether a save existed but could not be read at the last load (a fresh profile was started).</summary>
    public static bool LastLoadFailed { get; private set; }
}
