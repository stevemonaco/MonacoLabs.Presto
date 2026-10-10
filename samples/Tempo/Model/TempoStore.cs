using System.Text.Json;

namespace Tempo.Model;

/// <summary>Loads and saves <see cref="TempoData"/> as one JSON file under a root directory.</summary>
public sealed class TempoStore(string? root = null)
{
    public string FilePath { get; } = Path.Combine(
        root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tempo"),
        "tempo.json");

    /// <summary>Returns defaults when the file is missing, or when it is corrupt, after moving it to <c>tempo.json.bak</c>.</summary>
    public async Task<TempoData> LoadAsync()
    {
        if (!File.Exists(FilePath))
            return new TempoData();

        try
        {
            await using var stream = File.OpenRead(FilePath);
            if (await JsonSerializer.DeserializeAsync(stream, TempoJsonContext.Default.TempoData) is { } data)
                return data;
        }
        catch (JsonException)
        {
        }
        File.Move(FilePath, FilePath + ".bak", overwrite: true);
        return new TempoData();
    }

    /// <summary>Writes to a temporary file and moves it over the old one, so a crash never leaves a partial file.</summary>
    public async Task SaveAsync(TempoData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        await using (var stream = File.Create(temp))
            await JsonSerializer.SerializeAsync(stream, data, TempoJsonContext.Default.TempoData);
        File.Move(temp, FilePath, overwrite: true);
    }
}
