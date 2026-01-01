using System.Text.Json.Serialization;

namespace EndingApp;

public record Clip(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("creator_name")] string CreatorName,
    [property: JsonPropertyName("posted_by")] string PostedBy,
    [property: JsonPropertyName("created_at")] long CreatedAt,
    [property: JsonPropertyName("path")] string Path
);

[JsonSerializable(typeof(List<Clip>))]
internal sealed partial class ClipContext : JsonSerializerContext { }
