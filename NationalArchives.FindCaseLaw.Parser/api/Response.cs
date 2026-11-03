using System.Collections.Generic;

namespace UK.Gov.NationalArchives.Judgments.Api;

public class Response
{
    public string Xml { get; init; }

    public Meta Meta { get; init; }

    public IEnumerable<Image> Images { get; init; }
}

public class Image
{
    public string Name { get; init; }

    public string Type { get; init; }

    public byte[] Content { get; init; }
}
