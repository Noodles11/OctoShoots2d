using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace OctoShoots.Core.Saves;

public sealed class SaveException : Exception
{
    public SaveException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// JSON save format with a version number and step-by-step migrations, plus a copyable export code
/// (gzip + base64url) for backup and transfer (2D §13.1).
/// </summary>
public static class SaveCodec
{
    public const int CurrentVersion = 2;
    public const string ExportPrefix = "INKD2:";

    /// <summary>Migration from version N to N+1, keyed by N. Add one whenever the format changes.</summary>
    public static readonly IReadOnlyDictionary<int, Func<JsonObject, JsonObject>> Migrations =
        new Dictionary<int, Func<JsonObject, JsonObject>>
        {
            // 1 → 2: the first-person game's suspended run (position, craters, bombs) has no meaning on the plane; the
            // profile carries over.
            [1] = root =>
            {
                root.Remove("run");
                return root;
            },
        };

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(SaveFile save)
    {
        save.Version = CurrentVersion;
        return JsonSerializer.Serialize(save, Json);
    }

    public static SaveFile Deserialize(string json) => Deserialize(json, Migrations, CurrentVersion);

    /// <summary>Migrates older versions forward one step at a time; rejects saves from newer builds.</summary>
    public static SaveFile Deserialize(string json, IReadOnlyDictionary<int, Func<JsonObject, JsonObject>> migrations, int currentVersion)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject ?? throw new SaveException("Save is not a JSON object");
        }
        catch (JsonException e)
        {
            throw new SaveException("Save is not valid JSON", e);
        }

        int version = root["version"]?.GetValue<int>() ?? throw new SaveException("Save has no version");
        if (version > currentVersion) throw new SaveException($"Save is from a newer version ({version} > {currentVersion})");
        while (version < currentVersion)
        {
            if (!migrations.TryGetValue(version, out var migrate)) throw new SaveException($"No migration from save version {version}");
            root = migrate(root);
            version++;
            root["version"] = version;
        }

        try
        {
            return root.Deserialize<SaveFile>(Json) ?? throw new SaveException("Save is empty");
        }
        catch (JsonException e)
        {
            throw new SaveException("Save has an unexpected shape", e);
        }
    }

    public static string Export(SaveFile save)
    {
        byte[] raw = Encoding.UTF8.GetBytes(Serialize(save));
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.SmallestSize)) gzip.Write(raw);
        string b64 = Convert.ToBase64String(buffer.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return ExportPrefix + b64;
    }

    public static SaveFile Import(string code)
    {
        code = code.Trim();
        if (!code.StartsWith(ExportPrefix, StringComparison.Ordinal)) throw new SaveException("Not an Ink Deep save code");
        string b64 = code[ExportPrefix.Length..].Replace('-', '+').Replace('_', '/');
        b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
        try
        {
            using var input = new MemoryStream(Convert.FromBase64String(b64));
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            return Deserialize(reader.ReadToEnd());
        }
        catch (Exception e) when (e is FormatException or InvalidDataException)
        {
            throw new SaveException("Save code is damaged", e);
        }
    }
}
