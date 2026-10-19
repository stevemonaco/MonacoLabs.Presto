namespace Tempo.Model;

/// <summary>Where <see cref="TempoStore"/> keeps its one JSON document and the backup of a corrupt one.</summary>
public interface ITempoStorage
{
    /// <summary>False until the first write; a corrupt document still counts.</summary>
    bool Exists { get; }

    /// <summary>Returns the document, or null when there is none.</summary>
    Task<string?> ReadAsync();

    Task WriteAsync(string json);

    /// <summary>Moves the document to the backup, replacing any earlier backup, and leaves no document.</summary>
    Task BackupAsync();
}
