using System;
using System.IO;
using System.IO.Abstractions;
using Xunit;

/// <summary>
/// Contains end-to-end integration tests that run the application's Main method
/// and interact with a real file system.
/// </summary>
public class IntegrationTests : IDisposable
{
    private readonly IFileSystem _fileSystem;
    private readonly string _baseTestDir;
    private readonly string _targetDir;
    private readonly string _sourceDir;

    private readonly TextWriter _originalConsoleOut;
    private readonly TextReader _originalConsoleIn;

    public IntegrationTests()
    {
        _fileSystem = new FileSystem();
        // Create a unique temporary directory for each test run to ensure isolation.
        _baseTestDir = Path.Combine(Path.GetTempPath(), "file_sorter_integration", Guid.NewGuid().ToString());
        _targetDir = Path.Combine(_baseTestDir, "targets");
        _sourceDir = Path.Combine(_baseTestDir, "sources");

        _fileSystem.Directory.CreateDirectory(_targetDir);
        _fileSystem.Directory.CreateDirectory(_sourceDir);

        // Redirect Console I/O to control and capture it during tests.
        _originalConsoleOut = Console.Out;
        _originalConsoleIn = Console.In;
    }

    /// <summary>
    /// Helper method to run the application with specific arguments and simulated user input.
    /// </summary>
    /// <param name="args">Command-line arguments for Program.Main.</param>
    /// <param name="userInput">A string simulating user key presses.</param>
    /// <returns>The application's exit code and captured console output.</returns>
    private (int exitCode, string output) RunApp(string[] args, string userInput = "")
    {
        using var consoleOut = new StringWriter();
        Console.SetOut(consoleOut);

        // Simulate user input by providing a StringReader to Console.In
        using var consoleIn = new StringReader(userInput);
        Console.SetIn(consoleIn);

        var exitCode = Program.Main(args);
        var output = consoleOut.ToString();

        return (exitCode, output);
    }

    [Fact]
    public void Run_WithConflict_RenamesFileBasedOnUserInput()
    {
        // Arrange
        // 1. Create a specific file structure on the real disk for this test.
        var annaDir = _fileSystem.Path.Combine(_targetDir, "Anna");
        _fileSystem.Directory.CreateDirectory(annaDir);

        var sourceFile = _fileSystem.Path.Combine(_sourceDir, "Photo of Anna.jpg");
        _fileSystem.File.WriteAllText(sourceFile, "new photo");

        var existingFile = _fileSystem.Path.Combine(annaDir, "Photo of Anna.jpg");
        _fileSystem.File.WriteAllText(existingFile, "old photo");

        // 2. Prepare arguments and simulate user input ("1" for Rename).
        var args = new[] { _targetDir, _sourceDir };
        var userInput = "1";

        // Act
        var (exitCode, output) = RunApp(args, userInput);

        // Assert
        // 1. Check exit code and that the correct messages were printed to the console.
        Assert.Equal(0, exitCode);
        Assert.Contains("CONFLICT: A file named 'Photo of Anna.jpg' already exists", output);
        Assert.Contains("MOVED & RENAMED", output);

        // 2. Verify the final state of the file system.
        var expectedRenamedFile = _fileSystem.Path.Combine(annaDir, "Photo of Anna_duplicate_001.jpg");
        Assert.True(_fileSystem.File.Exists(expectedRenamedFile), "File should have been renamed and moved.");
        Assert.False(_fileSystem.File.Exists(sourceFile), "Source file should have been removed.");
    }

    [Fact]
    public void Run_WithAmbiguity_MovesFileToUserSelectedDirectory()
    {
        // Arrange
        var teamADir = _fileSystem.Path.Combine(_targetDir, "Team A");
        _fileSystem.Directory.CreateDirectory(teamADir);
        var teamBDir = _fileSystem.Path.Combine(_targetDir, "Team B");
        _fileSystem.Directory.CreateDirectory(teamBDir);

        var sourceFile = _fileSystem.Path.Combine(_sourceDir, "Report for Team A and B.pdf");
        _fileSystem.File.WriteAllText(sourceFile, "report data");

        // Prepare arguments and simulate user input ("1" to select the first option).
        var args = new[] { _targetDir, _sourceDir };
        var userInput = "1";

        // Act
        var (exitCode, output) = RunApp(args, userInput);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("AMBIGUOUS: File 'Report for Team A and B.pdf' matches multiple directories.", output);

        var expectedDestFile = _fileSystem.Path.Combine(teamADir, "Report for Team A and B.pdf");
        Assert.True(_fileSystem.File.Exists(expectedDestFile), "File should be in the user-selected directory.");
        Assert.False(_fileSystem.File.Exists(sourceFile), "Source file should be removed.");
    }

    public void Dispose()
    {
        // Restore original Console I/O and clean up the temporary directory.
        Console.SetOut(_originalConsoleOut);
        Console.SetIn(_originalConsoleIn);
        if (_fileSystem.Directory.Exists(_baseTestDir))
        {
            _fileSystem.Directory.Delete(_baseTestDir, true);
        }
    }
}