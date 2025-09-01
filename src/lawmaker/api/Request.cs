
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UK.Gov.Legislation.Lawmaker.Api {

    public class Request {

        public string Filename { get; set; }

        public byte[] Content { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DocName? DocName { get; set; }

        private static JsonSerializerOptions options = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };

        public static Request FromJson(string json) {
            return JsonSerializer.Deserialize<Request>(json, options);
        }

    }

}
