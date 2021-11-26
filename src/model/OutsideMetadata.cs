
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface IExternalAttachment {

    string Type { get; }

}

interface IOutsideMetadata {

    string Id { get; }

    bool IdTrumps { get; }

    Court Court { get; }

    int Year { get; }

    int Number { get; }

    string Cite { get; }

    string Date { get; }

    string Name { get; }

    IEnumerable<IExternalAttachment> Attachments { get; }

}

}
