using System.IO;

namespace NationalArchives.FindCaseLaw.Utils;

public interface IEmbeddedResourceHelper
{
    string GetEmbeddedResourceAsString(string resourceName);
}

public class EmbeddedResourceHelper : IEmbeddedResourceHelper
{
    public string GetEmbeddedResourceAsString(string resourceName)
    {
        Stream? manifestResourceStream = null;
        try
        {
            var assembly = typeof(EmbeddedResourceHelper).Assembly;
            manifestResourceStream = assembly.GetManifestResourceStream(resourceName);
            if (manifestResourceStream is null)
            {
                throw new FileNotFoundException("Embeddded resource not found", resourceName);
            }

            using StreamReader reader = new(manifestResourceStream);
            return reader.ReadToEnd();
        }
        finally
        {
            manifestResourceStream?.Dispose();
        }
    }
}
