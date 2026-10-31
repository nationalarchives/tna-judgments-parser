using System.Text.Json;
using System.Text.Json.Serialization;

using TRE.FindCaseLaw.Inputs;

using Xunit;

namespace UK.Gov.NationalArchives.CaseLaw.TRE.Test;

public class TestInputSerialization
{
    private static readonly string Json = """
                                          {
                                            "parser-inputs": {
                                              "document-type": "judgment",
                                              "metadata": {
                                                "uri": "one",
                                                "cite": "two",
                                                "court": "three",
                                                "date": "four",
                                                "name": "five"
                                              }
                                            }
                                          }
                                          """;

    private static readonly Request Request = new()
    {
        Inputs = new ParserInputs
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata
            {
                URI = "one",
                Cite = "two",
                Court = "three",
                Date = "four",
                Name = "five"
            }
        }
    };

    [Fact]
    public static void One()
    {
        var request = JsonSerializer.Deserialize<Request>(Json);
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json2 = JsonSerializer.Serialize(request, options);
        Assert.Equal(Json, json2);
    }

    [Fact]
    public static void Two()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json2 = JsonSerializer.Serialize(Request, options);
        Assert.Equal(Json, json2);
    }
}
