using Xunit;
using Moq;
using System.IO.Abstractions.TestingHelpers;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;

public class FileSorterTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly Mock<IUserInterface> _uiMock;
    private readonly FileSorter _sorter;

    private const string TargetDir = @"C:\targets";
    private const string SourceDir = @"C:\sources";

    public FileSorterTests()
    {
        _fileSystem = new MockFileSystem();
        _uiMock = new Mock<IUserInterface>();

        // Setup default directories
        _fileSystem.AddDirectory(TargetDir);
        _fileSystem.AddDirectory(SourceDir);

        _sorter = new FileSorter(TargetDir, SourceDir, _fileSystem, _uiMock.Object);
    }

    [Fact]
    public void Run_WithUniqueMatch_MovesFileCorrectly()
    {
        // Arrange
        var johnSmithDir = _fileSystem.Path.Combine(TargetDir, "John Smith");
        _fileSystem.AddDirectory(johnSmithDir);
        var sourceFile = _fileSystem.Path.Combine(SourceDir, "Report for John Smith.txt");
        _fileSystem.AddFile(sourceFile, new MockFileData("test content"));

        // Act
        _sorter.Run();

        // Assert
        var expectedDestFile = _fileSystem.Path.Combine(johnSmithDir, "Report for John Smith.txt");
        Assert.True(_fileSystem.File.Exists(expectedDestFile), "File should be moved to the target directory.");
        Assert.False(_fileSystem.File.Exists(sourceFile), "Source file should be removed.");
        _uiMock.Verify(ui => ui.LogMove(sourceFile, expectedDestFile, false), Times.Once);
    }

    [Fact]
    public void Run_WithExistingFile_ResolvesConflictByRenaming()
    {
        // Arrange
        var annaDir = _fileSystem.Path.Combine(TargetDir, "Anna");
        _fileSystem.AddDirectory(annaDir);

        var sourceFile = _fileSystem.Path.Combine(SourceDir, "Photo of Anna.jpg");
        _fileSystem.AddFile(sourceFile, new MockFileData("new photo"));

        var existingFile = _fileSystem.Path.Combine(annaDir, "Photo of Anna.jpg");
        _fileSystem.AddFile(existingFile, new MockFileData("old photo"));

        // Mock user choosing to rename the file
        _uiMock.Setup(ui => ui.ResolveConflict(It.IsAny<string>(), It.IsAny<string>()))
               .Returns(ConflictAction.Rename);

        // Act
        _sorter.Run();

        // Assert
        var expectedRenamedFile = _fileSystem.Path.Combine(annaDir, "Photo of Anna_duplicate_001.jpg");
        Assert.True(_fileSystem.File.Exists(expectedRenamedFile), "File should be renamed and moved.");
        Assert.Equal("new photo", _fileSystem.File.ReadAllText(expectedRenamedFile));
        Assert.False(_fileSystem.File.Exists(sourceFile), "Source file should be removed.");
    }

    [Fact]
    public void Run_WithAmbiguousMatch_ResolvesWithUserInput()
    {
        // Arrange
        var teamADir = _fileSystem.Path.Combine(TargetDir, "Team A");
        var teamBDir = _fileSystem.Path.Combine(TargetDir, "Team B");
        _fileSystem.AddDirectory(teamADir);
        _fileSystem.AddDirectory(teamBDir);

        var sourceFile = _fileSystem.Path.Combine(SourceDir, "Report for Team A and Team B.pdf");
        _fileSystem.AddFile(sourceFile, new MockFileData("report data"));

        // Mock user choosing "Team A"
        _uiMock.Setup(ui => ui.ResolveAmbiguity(It.IsAny<string>(), It.IsAny<List<MatchInfo>>(), It.IsAny<string>()))
               .Returns(new AmbiguityChoice(AmbiguityAction.Select, teamADir));

        // Act
        _sorter.Run();

        // Assert
        var expectedDestFile = _fileSystem.Path.Combine(teamADir, "Report for Team A and Team B.pdf");
        Assert.True(_fileSystem.File.Exists(expectedDestFile), "File should be in Team A's directory.");
        Assert.False(_fileSystem.File.Exists(_fileSystem.Path.Combine(teamBDir, "Report for Team A and Team B.pdf")), "File should not be in Team B's directory.");
    }

    [Fact]
    public void Run_WithDuplicateTargetNames_WarnsAndAbortsIfUserChooses()
    {
        // Arrange
        _fileSystem.AddDirectory(_fileSystem.Path.Combine(TargetDir, "Client X"));
        _fileSystem.AddDirectory(_fileSystem.Path.Combine(TargetDir, "Old Projects, Client X"));

        var sourceFile = _fileSystem.Path.Combine(SourceDir, "Invoice for Client X.pdf");
        _fileSystem.AddFile(sourceFile, new MockFileData("invoice"));

        // Mock user choosing to abort
        _uiMock.Setup(ui => ui.ConfirmDuplicates(It.IsAny<IEnumerable<IGrouping<string, NameOccurrence>>>()))
               .Returns(false);

        // Act
        _sorter.Run();

        // Assert
        _uiMock.Verify(ui => ui.ConfirmDuplicates(It.IsAny<IEnumerable<IGrouping<string, NameOccurrence>>>()), Times.Once, "User should be warned about duplicate names.");
        Assert.True(_fileSystem.File.Exists(sourceFile), "File should not be moved if user aborts.");
    }

    [Fact]
    public void Run_WithNoMatches_MovesNoFiles()
    {
        // Arrange
        _fileSystem.AddDirectory(_fileSystem.Path.Combine(TargetDir, "Project Z"));
        var sourceFile = _fileSystem.Path.Combine(SourceDir, "Random Document.txt");
        _fileSystem.AddFile(sourceFile, new MockFileData("random"));

        // Act
        _sorter.Run();

        // Assert
        Assert.True(_fileSystem.File.Exists(sourceFile), "File with no matches should not be moved.");
        _uiMock.Verify(ui => ui.LogMove(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never, "No move operations should be logged.");
        _uiMock.Verify(ui => ui.PrintReport(It.Is<List<(string, string)>>(l => l.Count == 0)), Times.Once, "Report should show zero moved files.");
    }

    [Fact]
    public void Run_WithExistingFile_ResolvesConflictBySkipping()
    {
        // Arrange
        var annaDir = _fileSystem.Path.Combine(TargetDir, "Anna");
        _fileSystem.AddDirectory(annaDir);

        var sourceFile = _fileSystem.Path.Combine(SourceDir, "Photo of Anna.jpg");
        _fileSystem.AddFile(sourceFile, new MockFileData("new photo"));

        var existingFile = _fileSystem.Path.Combine(annaDir, "Photo of Anna.jpg");
        _fileSystem.AddFile(existingFile, new MockFileData("old photo"));

        // Mock user choosing to skip the file
        _uiMock.Setup(ui => ui.ResolveConflict(It.IsAny<string>(), It.IsAny<string>()))
               .Returns(ConflictAction.Skip);

        // Act
        _sorter.Run();

        // Assert
        Assert.True(_fileSystem.File.Exists(sourceFile), "Source file should not be moved.");
        Assert.True(_fileSystem.File.Exists(existingFile), "Existing file should remain untouched.");
        Assert.Equal("old photo", _fileSystem.File.ReadAllText(existingFile));
        _uiMock.Verify(ui => ui.LogUserSkip(sourceFile, false), Times.Once);
    }

    [Fact]
    public void Run_WithMultipleConflicts_ResolvesUsingRenameAll()
    {
        // Arrange
        var dir1 = _fileSystem.Path.Combine(TargetDir, "Dir1");
        _fileSystem.AddDirectory(dir1);
        var dir2 = _fileSystem.Path.Combine(TargetDir, "Dir2");
        _fileSystem.AddDirectory(dir2);

        // First conflict
        _fileSystem.AddFile(_fileSystem.Path.Combine(SourceDir, "File for Dir1.txt"), new MockFileData("new 1"));
        _fileSystem.AddFile(_fileSystem.Path.Combine(dir1, "File for Dir1.txt"), new MockFileData("old 1"));
        // Second conflict
        _fileSystem.AddFile(_fileSystem.Path.Combine(SourceDir, "File for Dir2.txt"), new MockFileData("new 2"));
        _fileSystem.AddFile(_fileSystem.Path.Combine(dir2, "File for Dir2.txt"), new MockFileData("old 2"));

        // Mock user choosing "Rename All" on the first conflict
        _uiMock.SetupSequence(ui => ui.ResolveConflict(It.IsAny<string>(), It.IsAny<string>()))
               .Returns(ConflictAction.RenameAll);

        // Act
        _sorter.Run();

        // Assert
        // First file is renamed and moved
        Assert.True(_fileSystem.File.Exists(_fileSystem.Path.Combine(dir1, "File for Dir1_duplicate_001.txt")));
        // Second file is also renamed and moved, without asking again
        Assert.True(_fileSystem.File.Exists(_fileSystem.Path.Combine(dir2, "File for Dir2_duplicate_001.txt")));
        // Verify ResolveConflict was only called once
        _uiMock.Verify(ui => ui.ResolveConflict(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Run_WithMultipleAmbiguities_ResolvesUsingSkipAll()
    {
        // Arrange
        var dirA = _fileSystem.Path.Combine(TargetDir, "A");
        var dirB = _fileSystem.Path.Combine(TargetDir, "B");
        _fileSystem.AddDirectory(dirA);
        _fileSystem.AddDirectory(dirB);

        var sourceFile1 = _fileSystem.Path.Combine(SourceDir, "Report for A and B.txt");
        _fileSystem.AddFile(sourceFile1, new MockFileData("ambiguous 1"));
        var sourceFile2 = _fileSystem.Path.Combine(SourceDir, "Summary for A and B.txt");
        _fileSystem.AddFile(sourceFile2, new MockFileData("ambiguous 2"));

        // Mock user choosing "Skip All" on the first ambiguity
        _uiMock.SetupSequence(ui => ui.ResolveAmbiguity(It.IsAny<string>(), It.IsAny<List<MatchInfo>>(), It.IsAny<string>()))
               .Returns(new AmbiguityChoice(AmbiguityAction.SkipAll));

        // Act
        _sorter.Run();

        // Assert
        Assert.True(_fileSystem.File.Exists(sourceFile1), "First ambiguous file should not be moved.");
        Assert.True(_fileSystem.File.Exists(sourceFile2), "Second ambiguous file should not be moved.");
        // Verify ResolveAmbiguity was only called once
        _uiMock.Verify(ui => ui.ResolveAmbiguity(It.IsAny<string>(), It.IsAny<List<MatchInfo>>(), It.IsAny<string>()), Times.Once);
        _uiMock.Verify(ui => ui.LogAutoSkip(sourceFile2), Times.Once);
    }

    [Fact]
    public void Run_WithPartialAndFullNameMatches_SelectsBestMatch()
    {
        // Arrange
        var johnDir = _fileSystem.Path.Combine(TargetDir, "John");
        var johnSmithDir = _fileSystem.Path.Combine(TargetDir, "John Smith");
        _fileSystem.AddDirectory(johnDir);
        _fileSystem.AddDirectory(johnSmithDir);

        var sourceFile = _fileSystem.Path.Combine(SourceDir, "Document for John Smith.docx");
        _fileSystem.AddFile(sourceFile, new MockFileData("content"));

        // Act
        _sorter.Run();

        // Assert
        var expectedDestFile = _fileSystem.Path.Combine(johnSmithDir, "Document for John Smith.docx");
        Assert.True(_fileSystem.File.Exists(expectedDestFile), "File should be moved to the most specific folder (John Smith).");
        Assert.False(_fileSystem.File.Exists(_fileSystem.Path.Combine(johnDir, "Document for John Smith.docx")), "File should not be moved to the less specific folder (John).");
    }

    [Fact]
    public void Run_WithSpacelessVariant_MatchesCorrectly()
    {
        // Arrange
        var johnSmithDir = _fileSystem.Path.Combine(TargetDir, "John Smith");
        _fileSystem.AddDirectory(johnSmithDir);
        var sourceFile = _fileSystem.Path.Combine(SourceDir, "Report for JohnSmith.txt");
        _fileSystem.AddFile(sourceFile, new MockFileData("test content"));

        // Act
        _sorter.Run();

        // Assert
        var expectedDestFile = _fileSystem.Path.Combine(johnSmithDir, "Report for JohnSmith.txt");
        Assert.True(_fileSystem.File.Exists(expectedDestFile), "File with spaceless name should match.");
        Assert.False(_fileSystem.File.Exists(sourceFile));
    }

    [Fact]
    public void Run_WithCaseInsensitiveMatch_MovesFileCorrectly()
    {
        // Arrange
        var johnSmithDir = _fileSystem.Path.Combine(TargetDir, "John Smith");
        _fileSystem.AddDirectory(johnSmithDir);
        var sourceFile = _fileSystem.Path.Combine(SourceDir, "report for john smith.txt");
        _fileSystem.AddFile(sourceFile, new MockFileData("test content"));

        // Act
        _sorter.Run();

        // Assert
        var expectedDestFile = _fileSystem.Path.Combine(johnSmithDir, "report for john smith.txt");
        Assert.True(_fileSystem.File.Exists(expectedDestFile), "File should be moved regardless of case.");
        Assert.False(_fileSystem.File.Exists(sourceFile));
    }

    [Fact]
    public void Run_WithEmptySourceDirectory_CompletesWithoutErrors()
    {
        // Arrange
        _fileSystem.AddDirectory(_fileSystem.Path.Combine(TargetDir, "Some Folder"));
        // Source directory is already empty

        // Act
        _sorter.Run();

        // Assert
        _uiMock.Verify(ui => ui.LogMove(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never, "No move operations should occur.");
        _uiMock.Verify(ui => ui.PrintReport(It.Is<List<(string, string)>>(l => l.Count == 0)), Times.Once, "Report should show zero moved files.");
    }

    [Fact]
    public void Run_WithMultipleExistingDuplicates_GeneratesCorrectIncrementedName()
    {
        // Arrange
        var annaDir = _fileSystem.Path.Combine(TargetDir, "Anna");
        _fileSystem.AddDirectory(annaDir);

        var sourceFile = _fileSystem.Path.Combine(SourceDir, "Photo of Anna.jpg");
        _fileSystem.AddFile(sourceFile, new MockFileData("newest photo"));

        // Create a chain of existing duplicates
        _fileSystem.AddFile(_fileSystem.Path.Combine(annaDir, "Photo of Anna.jpg"), new MockFileData("original photo"));
        _fileSystem.AddFile(_fileSystem.Path.Combine(annaDir, "Photo of Anna_duplicate_001.jpg"), new MockFileData("first duplicate"));

        // Mock user choosing to rename the file
        _uiMock.Setup(ui => ui.ResolveConflict(It.IsAny<string>(), It.IsAny<string>()))
               .Returns(ConflictAction.Rename);

        // Act
        _sorter.Run();

        // Assert
        // The system should find the next available slot, which is "duplicate_002".
        var expectedRenamedFile = _fileSystem.Path.Combine(annaDir, "Photo of Anna_duplicate_002.jpg");
        Assert.True(_fileSystem.File.Exists(expectedRenamedFile), "File should be renamed to the next available increment.");
    }

    [Fact]
    public void Run_WhenFileMoveFails_LogsFailureAndDoesNotCrash()
    {
        // Arrange
        // For this test, we need to mock IFileSystem to throw an exception,
        // so we create a new set of mocks instead of using the class-level ones.
        var uiMock = new Mock<IUserInterface>();
        var fileSystemMock = new Mock<IFileSystem>();

        const string annaDir = @"C:\targets\Anna";
        const string sourceFile = @"C:\sources\Photo of Anna.jpg";
        const string destFile = @"C:\targets\Anna\Photo of Anna.jpg";
        const string exceptionMessage = "Disk is full.";

        // Setup the mock file system to simulate a simple scenario
        fileSystemMock.Setup(fs => fs.Directory.EnumerateDirectories(TargetDir)).Returns(new[] { annaDir });
        fileSystemMock.Setup(fs => fs.Path.GetFileName(annaDir)).Returns("Anna");
        fileSystemMock.Setup(fs => fs.Directory.EnumerateFiles(SourceDir, "*.*", SearchOption.AllDirectories)).Returns(new[] { sourceFile });
        fileSystemMock.Setup(fs => fs.Path.GetFileNameWithoutExtension(sourceFile)).Returns("Photo of Anna");
        fileSystemMock.Setup(fs => fs.Path.GetFileName(sourceFile)).Returns("Photo of Anna.jpg");
        fileSystemMock.Setup(fs => fs.Path.Combine(annaDir, "Photo of Anna.jpg")).Returns(destFile);
        fileSystemMock.Setup(fs => fs.File.Exists(destFile)).Returns(false); // No conflict

        // ** Key step: Configure the Move method to throw an IOException **
        fileSystemMock.Setup(fs => fs.File.Move(sourceFile, destFile)).Throws(new IOException(exceptionMessage));

        var sorter = new FileSorter(TargetDir, SourceDir, fileSystemMock.Object, uiMock.Object);

        // Act
        sorter.Run(); // This should not throw an exception due to the try-catch block

        // Assert
        uiMock.Verify(ui => ui.LogMoveFailure(sourceFile, exceptionMessage), Times.Once, "The move failure should be logged.");
        uiMock.Verify(ui => ui.LogMove(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never, "A successful move should not be logged.");
        uiMock.Verify(ui => ui.PrintReport(It.Is<List<(string, string)>>(l => l.Count == 0)), Times.Once, "The final report should show zero moved files.");
    }

    [Fact]
    public void Run_WhenSourceDirectoryIsUnreadable_ThrowsAndDoesNotProcessFiles()
    {
        // Arrange
        var fileSystemMock = new Mock<IFileSystem>();
        var uiMock = new Mock<IUserInterface>();
        var exceptionMessage = "Access to the path is denied.";

        // Setup target dirs to be scannable
        fileSystemMock.Setup(fs => fs.Directory.EnumerateDirectories(TargetDir)).Returns(new string[0]);

        // ** Key step: Configure EnumerateFiles to throw an exception **
        fileSystemMock.Setup(fs => fs.Directory.EnumerateFiles(SourceDir, "*.*", SearchOption.AllDirectories))
                      .Throws(new UnauthorizedAccessException(exceptionMessage));

        var sorter = new FileSorter(TargetDir, SourceDir, fileSystemMock.Object, uiMock.Object);

        // Act & Assert
        // The exception should propagate up from Run() because it's a fatal error for the process.
        var ex = Assert.Throws<UnauthorizedAccessException>(() => sorter.Run());
        Assert.Equal(exceptionMessage, ex.Message);

        // Verify that no processing or reporting happened after the crash.
        uiMock.Verify(ui => ui.PrintReport(It.IsAny<List<(string, string)>>()), Times.Never, "The report should not be printed if scanning fails.");
    }
}