using System.Text.Json.Serialization;

namespace Tempo.Model;

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(TempoData))]
internal sealed partial class TempoJsonContext : JsonSerializerContext;
