using System.IO;

namespace UK.Gov.Legislation.Judgments {

public interface IImage {

    string Name { get; }

    string ContentType { get; }

    Stream Content();

}

interface IExternalImage : IInline {

    string URL { get; }

}

}