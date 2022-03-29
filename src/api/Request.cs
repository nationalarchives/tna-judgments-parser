
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UK.Gov.NationalArchives.Judgments.Api {

public class Request {

    public string Filename { get; set; }

    public byte[] Content { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Hint? Hint { get; set; }

    public Meta Meta { get; set; }

    public IEnumerable<Attachment> Attachments { get; set; }

    private static JsonSerializerOptions options = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };

    public static Request FromJson(string json) {
        return JsonSerializer.Deserialize<Request>(json, options);
    }

}

public enum AttachmentType { Order, Appendix }

public class Attachment {

    public string Filename { get; set; }

    public byte[] Content { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AttachmentType Type { get; set; }

}

}
