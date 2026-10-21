using System.Runtime.InteropServices.JavaScript;
using Tempo.Model;

namespace Tempo.Browser;

/// <summary>Keeps the document in the <c>tempo</c> localStorage key and the backup in <c>tempo.bak</c>, since the WebAssembly file system is lost on reload.</summary>
internal sealed partial class BrowserTempoStorage : ITempoStorage
{
    private const string Key = "tempo";
    private const string BackupKey = "tempo.bak";

    public bool Exists => GetItem(Key) is not null;

    public Task<string?> ReadAsync() => Task.FromResult(GetItem(Key));

    public Task WriteAsync(string json)
    {
        SetItem(Key, json);
        return Task.CompletedTask;
    }

    public Task BackupAsync()
    {
        if (GetItem(Key) is { } json)
            SetItem(BackupKey, json);
        RemoveItem(Key);
        return Task.CompletedTask;
    }

    [JSImport("globalThis.localStorage.getItem")]
    private static partial string? GetItem(string key);

    [JSImport("globalThis.localStorage.setItem")]
    private static partial void SetItem(string key, string value);

    [JSImport("globalThis.localStorage.removeItem")]
    private static partial void RemoveItem(string key);
}
