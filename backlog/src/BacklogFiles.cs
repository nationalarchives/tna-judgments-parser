#nullable enable

using System.IO;
using System.IO.Abstractions;
using System.Linq;

using Backlog.Options;

using Microsoft.Extensions.Options;

namespace Backlog;

internal interface IBacklogFiles
{
    /// <summary>
    ///     Returns the contents of a file using its metadata information by resolving the UUID
    ///     and locating the corresponding document in the court documents directory.
    /// </summary>
    /// <param name="uuid">TDR UUID used to locate file</param>
    /// <returns>File contents as a byte array</returns>
    byte[] ReadFile(string uuid);
}

/// <summary>
///     Provides file operations for processing backlog documents.
/// </summary>
internal class BacklogFiles : IBacklogFiles
{
    private readonly IFileSystem fileSystem;
    private readonly IDirectoryInfo courtDocumentsDirectory;

    public BacklogFiles(IOptions<BacklogParserOptions> backlogParserOptions, IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
        var pathToDataFolder = backlogParserOptions.Value.DataFolderPath;

        var courtDocumentsDirectoryPath = fileSystem.Path.Combine(pathToDataFolder, "court_documents");
        courtDocumentsDirectory = fileSystem.DirectoryInfo.New(courtDocumentsDirectoryPath);
        if (!courtDocumentsDirectory.Exists)
        {
            throw new DirectoryNotFoundException($"Couldn't find {courtDocumentsDirectory}");
        }
    }

    public byte[] ReadFile(string uuid)
    {
        var filesWithUuid = courtDocumentsDirectory.GetFiles($"{uuid}*", SearchOption.AllDirectories);
        if (filesWithUuid.Length == 0)
        {
            throw new FileNotFoundException(
                $"Couldn't find file with UUID: {uuid}. It must have been received through TDR in order to have been assigned a UUID so check the original TDR bucket and check any file conversion folders");
        }

        if (filesWithUuid.Length > 1)
        {
            throw new MoreThanOneFileFoundException(
                $"There should only be one file in {courtDocumentsDirectory} matching UUID {uuid} but found {filesWithUuid.Length}: [{string.Join(", ", filesWithUuid.OrderBy(f => f.Name).Select(f => $"\"{f.Name}\""))}]");
        }

        return fileSystem.File.ReadAllBytes(filesWithUuid.Single().FullName);
    }
}
