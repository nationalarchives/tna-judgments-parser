using System.IO;
using System.IO.Abstractions.TestingHelpers;

using Backlog;

using Xunit;

namespace test.backlog;

public sealed class TestBacklogFiles
{
    private const string DataFolderPath = "batch-x";
    private readonly string courtDocumentsDir = Path.Combine(DataFolderPath, "court_documents");

    private readonly BacklogFiles backlogFiles;
    private readonly MockFileSystem mockFileSystem = new();

    public TestBacklogFiles()
    {
        mockFileSystem.AddDirectory(DataFolderPath);
        mockFileSystem.AddDirectory(courtDocumentsDir);

        var backlogParserOptions = BacklogParserOptionsHelper.Create(DataFolderPath);
        backlogFiles = new BacklogFiles(backlogParserOptions, mockFileSystem);
    }

    [Fact]
    public void BacklogFilesConstructor_WhenNoCourtDocumentsFolder_ThrowsDirectoryNotFoundException()
    {
        var backlogParserOptions = BacklogParserOptionsHelper.Create("this-folder-does-not-exist");

        var exception =
            Assert.Throws<DirectoryNotFoundException>(() =>
                new BacklogFiles(backlogParserOptions, new MockFileSystem()));

        var expectedNotFoundPath = mockFileSystem.Path.Combine("this-folder-does-not-exist", "court_documents");
        Assert.Equal($"Couldn't find {expectedNotFoundPath}", exception.Message);
    }

    [Fact]
    public void ReadFile_WithUuidForFileThatDoesntExist_ThrowsFileNotFoundException()
    {
        var thisUuidDoesnTHaveACorrespondingFile = "this uuid doesn't have a corresponding file";

        var exception =
            Assert.Throws<FileNotFoundException>(() => backlogFiles.ReadFile(thisUuidDoesnTHaveACorrespondingFile));

        Assert.Equal(
            $"Couldn't find file with UUID: {thisUuidDoesnTHaveACorrespondingFile}. It must have been received through TDR in order to have been assigned a UUID so check the original TDR bucket and check any file conversion folders",
            exception.Message);
    }

    [Theory]
    [InlineData("a80ed36d-7a5c-4956-894d-51b14c89aa79", ".doc")]
    [InlineData("b80ed36d-7a5c-4956-894d-51b14c89aa79", ".docx")]
    [InlineData("b80ed36d-7a5c-4956-894d-51b14c89aa79", "....docx")]
    [InlineData("c80ed36d-7a5c-4956-894d-51b14c89aa79", ".pdf")]
    [InlineData("d80ed36d-7a5c-4956-894d-51b14c89aa79", ".mystery")]
    [InlineData("d80ed36d-7a5c-4956-894d-51b14c89aa79", "")]
    public void ReadFile_WithUuidForFileThatDoesExist_ReturnsFileContentsRegardlessOfExtension(string uuid,
        string fileExtension)
    {
        byte[] expectedContent = [1, 2, 3, 4, 5];
        var filePath = mockFileSystem.Path.Combine(courtDocumentsDir, $"{uuid}{fileExtension}");
        mockFileSystem.AddFile(filePath, new MockFileData(expectedContent));

        var result = backlogFiles.ReadFile(uuid);

        Assert.Equal(expectedContent, result);
    }

    [Fact]
    public void ReadFile_WithUuidThatMatchesMultipleFiles_Throws()
    {
        const string uuid = "a80ed36d-7a5c-4956-894d-51b14c89aa79";
        var duplicateUuidFile1 = mockFileSystem.Path.Combine(courtDocumentsDir, uuid);
        var duplicateUuidFile2 = mockFileSystem.Path.Combine(courtDocumentsDir, $"{uuid}.docx");
        mockFileSystem.AddFile(duplicateUuidFile1, new MockFileData([1, 2, 3, 4, 5]));
        mockFileSystem.AddFile(duplicateUuidFile2, new MockFileData([6, 7, 8, 9, 10]));

        var exception = Assert.Throws<MoreThanOneFileFoundException>(() => backlogFiles.ReadFile(uuid));

        var expectedCourtDocumentsPath = mockFileSystem.Path.Combine("batch-x","court_documents");
        Assert.Equal(
            $"There should only be one file in {expectedCourtDocumentsPath} matching UUID a80ed36d-7a5c-4956-894d-51b14c89aa79 but found 2: [\"a80ed36d-7a5c-4956-894d-51b14c89aa79\", \"a80ed36d-7a5c-4956-894d-51b14c89aa79.docx\"]",
            exception.Message);
    }
}
