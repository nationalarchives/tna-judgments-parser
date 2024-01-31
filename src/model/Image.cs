using System.IO;

namespace UK.Gov.Legislation.Judgments {

public interface IImage {

    string Name { get; }

    string ContentType { get; }

    Stream Content();

    byte[] Read();

}

interface IExternalImage : IInline {

    string URL { get; }

}

}