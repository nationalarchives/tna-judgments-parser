
using System.Collections.Generic;

namespace UK.Gov.NationalArchives.Judgments.Api {

public class Meta {

    public string DocumentType { get; set; }

    public string Uri { get; set; }

    public string Court { get; set; }

    public string Cite { get; set; }

    public string Date { get; set; }

    public string Name { get; set; }

    public IEnumerable<ExternalAttachment> Attachments { get; set; }

}

public class ExternalAttachment {

    public string Name { get; set; }

    public string Link { get; set; }

}

}
