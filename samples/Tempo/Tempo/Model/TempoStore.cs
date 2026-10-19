using System.Text.Json;

namespace Tempo.Model;

/// <summary>Loads and saves <see cref="TempoData"/> as one JSON document in an <see cref="ITempoStorage"/>.</summary>
public sealed class TempoStore(ITempoStorage storage)
{
    /// <summary>Stores in <c>tempo.json</c> under <paramref name="root"/>, as <see cref="FileTempoStorage"/> does.</summary>
    public TempoStore(string? root = null)
        : this(new FileTempoStorage(root))
    {
    }

    /// <summary>False until the first save; a corrupt document still counts.</summary>
    public bool Exists => storage.Exists;

    /// <summary>Returns defaults when the document is missing, or when it is corrupt, after moving it to the backup.</summary>
    public async Task<TempoData> LoadAsync()
    {
        if (await storage.ReadAsync() is not { } json)
            return new TempoData();

        try
        {
            if (JsonSerializer.Deserialize(json, TempoJsonContext.Default.TempoData) is { } data)
                return data;
        }
        catch (JsonException)
        {
        }
        await storage.BackupAsync();
        return new TempoData();
    }

    public Task SaveAsync(TempoData data) =>
        storage.WriteAsync(JsonSerializer.Serialize(data, TempoJsonContext.Default.TempoData));
}
