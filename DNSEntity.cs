using System.Text.Json.Serialization;

namespace ArashiDNS.Aha
{
    public class Answer
    {
        [JsonPropertyName("name")] public string Name { get; set; }

        [JsonPropertyName("TTL")] public int TTL { get; set; }

        [JsonPropertyName("type")] public int Type { get; set; }

        [JsonPropertyName("data")] public string Data { get; set; }
    }

    public class Question
    {
        [JsonPropertyName("name")] public string Name { get; set; }

        [JsonPropertyName("type")] public int Type { get; set; }
    }

    public class DNSEntity
    {
        [JsonPropertyName("Status")] public int? Status { get; set; }

        [JsonPropertyName("TC")] public bool? TC { get; set; }

        [JsonPropertyName("RD")] public bool? RD { get; set; }

        [JsonPropertyName("RA")] public bool? RA { get; set; }

        [JsonPropertyName("AD")] public bool? AD { get; set; }

        [JsonPropertyName("CD")] public bool? CD { get; set; }

        [JsonPropertyName("Question")] public Question? Question { get; set; }

        [JsonPropertyName("Answer")] public List<Answer>? Answer { get; set; }

    }

    [JsonSerializable(typeof(DNSEntity))]
    [JsonSerializable(typeof(Answer))]
    [JsonSerializable(typeof(Question))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
