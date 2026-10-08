using OctoShoots.Core.Saves;

namespace OctoShoots.Game.Settings;

/// <summary>
/// The one save the title screen and the game share: loaded once from disk, written back after changes. A review run
/// (<c>--title-sample</c>) works on an in-memory copy that is never written.
/// </summary>
public static class GameSave
{
    static SaveFile? _save;

    public static SaveFile Current => _save ??= SaveStore.Load();

    /// <summary>When true, nothing is written to disk.</summary>
    public static bool ReadOnly { get; set; }

    public static void Write()
    {
        if (!ReadOnly) SaveStore.Write(Current);
    }

    /// <summary>Replaces the whole save (an imported code) and writes it.</summary>
    public static void Replace(SaveFile save)
    {
        _save = save;
        Write();
    }

    /// <summary>Clears the profile and the run.</summary>
    public static void Erase() => Replace(new SaveFile());

    /// <summary>Uses this save in memory only (review captures).</summary>
    public static void UseInMemory(SaveFile save)
    {
        _save = save;
        ReadOnly = true;
    }
}
