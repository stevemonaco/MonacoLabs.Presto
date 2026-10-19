namespace Tempo.Model;

/// <summary>Keeps the document in <c>tempo.json</c> under a root directory, <c>LocalApplicationData/Tempo</c> by default, and the backup in <c>tempo.json.bak</c>.</summary>
public sealed class FileTempoStorage(string? root = null) : ITempoStorage
{
    public string FilePath { get; } = Path.Combine(
        root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tempo"),
        "tempo.json");

    public bool Exists => File.Exists(FilePath);

    public async Task<string?> ReadAsync() => Exists ? await File.ReadAllTextAsync(FilePath) : null;

    /// <summary>Writes to a temporary file and moves it over the old one, so a crash never leaves a partial file.</summary>
    public async Task WriteAsync(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        await File.WriteAllTextAsync(temp, json);
        File.Move(temp, FilePath, overwrite: true);
    }

    public Task BackupAsync()
    {
        File.Move(FilePath, FilePath + ".bak", overwrite: true);
        return Task.CompletedTask;
    }
}
