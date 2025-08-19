using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

/// <summary>
/// Contains end-to-end integration tests that run the application's Main method
/// and interact with a real file system.
/// </summary>
public class IntegrationTests : IDisposable
{
    private readonly string _baseTestDir;
    private readonly string _targetDir;
    private readonly string _sourceDir;

    private readonly TextWriter _originalConsoleOut;
    private readonly TextReader _originalConsoleIn;

    public IntegrationTests()
    {
        // Create a unique temporary directory for each test run to ensure isolation.
        _baseTestDir = Path.Combine(Path.GetTempPath(), "file_sorter_integration", Guid.NewGuid().ToString());
        _targetDir = Path.Combine(_baseTestDir, "targets");
        _sourceDir = Path.Combine(_baseTestDir, "sources");

        Directory.CreateDirectory(_targetDir);
        Directory.CreateDirectory(_sourceDir);

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
        var annaDir = Path.Combine(_targetDir, "Anna");
        Directory.CreateDirectory(annaDir);

        var sourceFile = Path.Combine(_sourceDir, "Photo of Anna.jpg");
        File.WriteAllText(sourceFile, "new photo");

        var existingFile = Path.Combine(annaDir, "Photo of Anna.jpg");
        File.WriteAllText(existingFile, "old photo");

        // 2. Prepare arguments and simulate user input ("1" for Rename).
        var args = new[] { _targetDir, _sourceDir };
        var userInput = "1" + Environment.NewLine;

        // Act
        var (exitCode, output) = RunApp(args, userInput);

        // Assert
        // 1. Check exit code and that the correct messages were printed to the console.
        Assert.Equal(0, exitCode);
        Assert.Contains("CONFLICT: A file named 'Photo of Anna.jpg' already exists", output);
        Assert.Contains("MOVED & RENAMED", output);

        // 2. Verify the final state of the file system.
        var expectedRenamedFile = Path.Combine(annaDir, "Photo of Anna_duplicate_001.jpg");
        Assert.True(File.Exists(expectedRenamedFile), "File should have been renamed and moved.");
        Assert.False(File.Exists(sourceFile), "Source file should have been removed.");
    }

    [Fact]
    public void Run_WithAmbiguity_MovesFileToUserSelectedDirectory()
    {
        // Arrange
        var dirA = Path.Combine(_targetDir, "A");
        Directory.CreateDirectory(dirA);
        var dirB = Path.Combine(_targetDir, "B");
        Directory.CreateDirectory(dirB);

        var sourceFile = Path.Combine(_sourceDir, "Report for A and B.pdf");
        File.WriteAllText(sourceFile, "report data");

        // Prepare arguments and simulate user input ("1" to select the first option).
        var args = new[] { _targetDir, _sourceDir };
        var userInput = "1" + Environment.NewLine;

        // Act
        var (exitCode, output) = RunApp(args, userInput);

        // Assert
        Assert.Equal(0, exitCode);
        // We assert against a simpler, uncolored string that is also part of the ambiguity resolution flow.
        // This makes the test more robust and less sensitive to formatting/coloring issues.
        Assert.Contains("Please choose a destination:", output);

        var expectedDestFile = Path.Combine(dirA, "Report for A and B.pdf");
        Assert.True(File.Exists(expectedDestFile), "File should be in the user-selected directory.");
        Assert.False(File.Exists(sourceFile), "Source file should be removed.");
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void Run_WithHelpFlag_ShowsHelpAndExitsWithZero(string helpFlag)
    {
        // Arrange
        var args = new[] { helpFlag };

        // Act
        var (exitCode, output) = RunApp(args);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("USAGE:", output);
        Assert.Contains("File Sorter Utility", output);
    }

    public static IEnumerable<object[]> GetInvalidArguments()
    {
        yield return new object[] { Array.Empty<string>() }; // No arguments
        yield return new object[] { new[] { "one_argument" } }; // One argument
    }

    [Theory]
    [MemberData(nameof(GetInvalidArguments))]
    public void Run_WithInvalidArguments_ShowsErrorAndExitsWithOne(string[] args)
    {
        // Arrange (args provided by MemberData)

        // Act
        var (exitCode, output) = RunApp(args);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("Error: Please provide two directory paths as arguments.", output);
    }

    [Fact]
    public void Run_WithSetupTestDataFlag_CreatesTestDirectoriesAndExitsWithZero()
    {
        // Arrange: Create a unique, isolated directory for this test run to avoid conflicts.
        var testRunDir = Path.Combine(Path.GetTempPath(), "file_sorter_setup_test", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testRunDir);
        var originalCurrentDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(testRunDir);

        var args = new[] { "--setup-test-data" };

        try
        {
            // Act
            var (exitCode, output) = RunApp(args);

            // Assert
            Assert.Equal(0, exitCode);
            Assert.Contains("Test data setup complete.", output);

            // Verify that the directories were actually created in the file system
            Assert.True(Directory.Exists("temp1"), "The 'temp1' directory should be created.");
            Assert.True(Directory.Exists("temp2"), "The 'temp2' directory should be created.");
        }
        finally
        {
            // Cleanup: Restore the original working directory and delete the temporary test folder.
            Directory.SetCurrentDirectory(originalCurrentDir);
            Directory.Delete(testRunDir, true);
        }
    }

    public void Dispose()
    {
        // Restore original Console I/O and clean up the temporary directory.
        Console.SetOut(_originalConsoleOut);
        Console.SetIn(_originalConsoleIn);
        if (Directory.Exists(_baseTestDir))
        {
            Directory.Delete(_baseTestDir, true);
        }
    }
}