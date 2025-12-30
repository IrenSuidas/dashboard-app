using System.Text.Json.Serialization;

namespace EndingApp;

public record Song(
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("video_id")] string VideoId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("uploader")] string Uploader,
    [property: JsonPropertyName("added_by")] string AddedBy,
    [property: JsonPropertyName("created_at")] long CreatedAt,
    [property: JsonPropertyName("queue_position")] int? QueuePosition,
    [property: JsonPropertyName("duration")] int? Duration // Duration in seconds
);

[JsonSerializable(typeof(List<Song>))]
[JsonSerializable(typeof(SongManifest))]
internal sealed partial class SongContext : JsonSerializerContext { }

public record SongManifest(
    [property: JsonPropertyName("requested")] List<Song> Requested,
    [property: JsonPropertyName("recurrent")] List<Song> Recurrent
);
