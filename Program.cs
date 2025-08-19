﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

/// <summary>
/// Main entry point for the application.
/// Handles command-line argument parsing and orchestrates the overall process.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        // Handle command-line flags
        if (args.Length > 0)
        {
            if (args[0] == "-h" || args[0] == "--help")
            {
                ConsoleUI.ShowHelp();
                return 0;
            }

            if (args[0] == "--setup-test-data")
            {
                ConsoleUI.LogInfo("Setting up test data...");
                TestDataGenerator.Setup();
                ConsoleUI.LogSuccess("Test data setup complete. The 'temp1' and 'temp2' directories have been created.");
                return 0;
            }
        }

        // Validate main arguments
        if (args.Length < 2)
        {
            ConsoleUI.LogError("Error: Please provide two directory paths as arguments.");
            Console.WriteLine("Usage: file_sort \"<target_folders_path>\" \"<source_files_path>\"");
            Console.WriteLine("Use 'file_sort --help' for more information.");
            return 1;
        }

        var targetDirectory = args[0];
        var sourceDirectory = args[1];

        if (!Directory.Exists(targetDirectory))
        {
            ConsoleUI.LogError($"Error: Target directory not found at path '{targetDirectory}'");
            return 1;
        }

        if (!Directory.Exists(sourceDirectory))
        {
            ConsoleUI.LogError($"Error: Source directory not found at path '{sourceDirectory}'");
            return 1;
        }

        try
        {
            var sorter = new FileSorter(targetDirectory, sourceDirectory);
            sorter.Run();
        }
        catch (Exception ex)
        {
            ConsoleUI.LogError($"An unexpected error occurred: {ex.Message}");
            return 1;
        }

        return 0;
    }
}

/// <summary>
/// Encapsulates the core logic for scanning, matching, and sorting files.
/// </summary>
public class FileSorter
{
    private readonly string _targetDirectory;
    private readonly string _sourceDirectory;
    private readonly Dictionary<string, List<string>> _directoryNamesMap = new();
    private readonly List<(string SourceFile, string FinalDestPath)> _movedFilesLog = new();

    private bool _renameAllConflicts = false;
    private bool _skipAllAmbiguous = false;

    public FileSorter(string targetDirectory, string sourceDirectory)
    {
        _targetDirectory = targetDirectory;
        _sourceDirectory = sourceDirectory;
    }

    /// <summary>
    /// Executes the entire file sorting process.
    /// </summary>
    public void Run()
    {
        ScanTargetDirectories();

        if (!CheckForDuplicateNames())
        {
            ConsoleUI.LogWarning("\nOperation aborted by user.");
            return;
        }

        ProcessSourceFiles();
        ConsoleUI.PrintReport(_movedFilesLog);
    }

    /// <summary>
    /// Scans target directories and builds a map of paths to associated names.
    /// </summary>
    private void ScanTargetDirectories()
    {
        ConsoleUI.LogInfo($"Scanning target directories in: {_targetDirectory}");
        foreach (var dirPath in Directory.EnumerateDirectories(_targetDirectory))
        {
            var dirName = Path.GetFileName(dirPath);
            var namesInDir = dirName.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);

            var processedNames = new HashSet<string>();
            foreach (var name in namesInDir)
            {
                var trimmedName = name.Trim();
                processedNames.Add(trimmedName); // Original name
                processedNames.Add(trimmedName.Replace(" ", "")); // Variation without spaces
            }

            if (processedNames.Any())
            {
                _directoryNamesMap[dirPath] = processedNames.ToList();
            }
        }
    }

    /// <summary>
    /// Finds and reports duplicate names among target folders and asks for user confirmation.
    /// </summary>
    /// <returns>True if the operation should continue, false if aborted by the user.</returns>
    private bool CheckForDuplicateNames()
    {
        var nameOccurrences = _directoryNamesMap
            .SelectMany(kvp => kvp.Value.Select(name => new NameOccurrence(name, kvp.Key)))
            .GroupBy(item => item.Name)
            .Where(group => group.Count() > 1)
            .ToList();

        if (nameOccurrences.Any())
        {
            return ConsoleUI.ConfirmDuplicates(nameOccurrences);
        }
        return true;
    }

    /// <summary>
    /// Scans the source directory for files and processes each one.
    /// </summary>
    private void ProcessSourceFiles()
    {
        ConsoleUI.LogInfo($"\nScanning source files in: {_sourceDirectory}");
        var sourceFiles = Directory.EnumerateFiles(_sourceDirectory, "*.*", SearchOption.AllDirectories);
        
        ConsoleUI.LogInfo($"Found {sourceFiles.Count()} files to process.");
        Console.WriteLine("\nStarting file processing...");

        foreach (var filePath in sourceFiles)
        {
            ProcessSingleFile(filePath);
        }
    }

    /// <summary>
    /// Finds matches for a single file and determines the correct action.
    /// </summary>
    private void ProcessSingleFile(string sourceFilePath)
    {
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFilePath);

        var matches = _directoryNamesMap
            .Select(dirEntry => new {
                Path = dirEntry.Key,
                BestMatch = dirEntry.Value
                    .Where(nameVariant => fileNameWithoutExt.Contains(nameVariant, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(nv => nv.Length)
                    .FirstOrDefault()
            })
            .Where(m => m.BestMatch != null)
            .Select(m => new MatchInfo(m.Path, m.BestMatch!))
            .ToList();

        if (matches.Count == 1)
        {
            MoveFileWithConflictResolution(sourceFilePath, matches.First().Path);
        }
        else if (matches.Count > 1)
        {
            HandleAmbiguousFile(sourceFilePath, matches);
        }
    }

    /// <summary>
    /// Handles an ambiguous file move by prompting the user for a choice.
    /// </summary>
    private void HandleAmbiguousFile(string sourcePath, List<MatchInfo> matches)
    {
        if (_skipAllAmbiguous)
        {
            ConsoleUI.LogAutoSkip(sourcePath);
            return;
        }

        var choice = ConsoleUI.ResolveAmbiguity(sourcePath, matches, _targetDirectory);

        switch (choice.Action)
        {
            case AmbiguityAction.Select:
                if (choice.SelectedPath != null)
                {
                    MoveFileWithConflictResolution(sourcePath, choice.SelectedPath);
                }
                break;
            case AmbiguityAction.Skip:
                ConsoleUI.LogUserSkip(sourcePath);
                break;
            case AmbiguityAction.SkipAll:
                _skipAllAmbiguous = true;
                ConsoleUI.LogUserSkip(sourcePath, isPermanent: true);
                break;
        }
    }

    /// <summary>
    /// Moves a file, handling potential naming conflicts by asking the user.
    /// </summary>
    private void MoveFileWithConflictResolution(string sourcePath, string destinationDir)
    {
        var fileName = Path.GetFileName(sourcePath);
        var destFilePath = Path.Combine(destinationDir, fileName);

        if (!File.Exists(destFilePath))
        {
            MoveAndLog(sourcePath, destFilePath);
            return;
        }

        if (_renameAllConflicts)
        {
            RenameAndMove(sourcePath, destinationDir);
            return;
        }

        var choice = ConsoleUI.ResolveConflict(fileName, destinationDir);
        switch (choice)
        {
            case ConflictAction.Rename:
                RenameAndMove(sourcePath, destinationDir);
                break;
            case ConflictAction.Skip:
                ConsoleUI.LogUserSkip(sourcePath);
                break;
            case ConflictAction.RenameAll:
                _renameAllConflicts = true;
                ConsoleUI.LogPermanentChoice("Automatic renaming for all future conflicts is now ENABLED.");
                RenameAndMove(sourcePath, destinationDir);
                break;
        }
    }

    /// <summary>
    /// Renames a file to avoid conflicts and moves it to the destination.
    /// </summary>
    private void RenameAndMove(string sourcePath, string destinationDir)
    {
        var newDestFilePath = GenerateUniqueFilePath(sourcePath, destinationDir);
        MoveAndLog(sourcePath, newDestFilePath, isRenamed: true);
    }

    /// <summary>
    /// Generates a unique file path in the destination directory to avoid overwriting.
    /// </summary>
    private static string GenerateUniqueFilePath(string sourcePath, string destinationDir)
    {
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);
        int copyCount = 1;
        string newDestFilePath;
        do
        {
            var newFileName = $"{fileNameWithoutExt}_duplicate_{copyCount:D3}{extension}";
            newDestFilePath = Path.Combine(destinationDir, newFileName);
            copyCount++;
        } while (File.Exists(newDestFilePath));
        return newDestFilePath;
    }

    /// <summary>
    /// Performs the actual file move and logs the operation.
    /// </summary>
    private void MoveAndLog(string sourcePath, string destPath, bool isRenamed = false)
    {
        try
        {
            File.Move(sourcePath, destPath);
            _movedFilesLog.Add((sourcePath, destPath));
            ConsoleUI.LogMove(sourcePath, destPath, isRenamed);
        }
        catch (Exception ex)
        {
            ConsoleUI.LogMoveFailure(sourcePath, ex.Message);
        }
    }
}

/// <summary>
/// Provides static methods for all console-based user interactions.
/// </summary>
public static class ConsoleUI
{
    public static void ShowHelp()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
        Console.WriteLine($"📂 File Sorter Utility v{version}");
        Console.WriteLine("---------------------------------");
        Console.WriteLine("A smart command-line tool to automatically organize your files.");
        Console.WriteLine();
        Console.WriteLine("USAGE:");
        Console.WriteLine("  file_sort <target_folders_path> <source_files_path>");
        Console.WriteLine();
        Console.WriteLine("OPTIONS:");
        Console.WriteLine("  -h, --help               Show this help screen.");
        Console.WriteLine("  --setup-test-data        Generate test directories ('temp1', 'temp2') and files.");
        Console.WriteLine();
        Console.WriteLine("License: MIT");
        Console.WriteLine("Source: https://github.com/shtormish/file_sort");
    }

    public static bool ConfirmDuplicates(IEnumerable<IGrouping<string, NameOccurrence>> nameOccurrences)
    {
        LogWarning("\nWarning: The following name duplicates were found in target directories:");
        foreach (var group in nameOccurrences)
        {
            Console.WriteLine($"\n- Name '{group.Key}' appears in the following folders:");
            foreach (var item in group)
            {
                Console.WriteLine($"  - {item.Path}");
            }
        }

        LogWarning("\nThis may lead to incorrect file placement during the process.");
        Console.Write("Do you want to continue? (C = Continue, A = Abort): ");

        while (true)
        {
            var keyInfo = Console.ReadKey(true);
            char choice = char.ToUpper(keyInfo.KeyChar);

            if (choice == 'C')
            {
                Console.WriteLine("Continue");
                return true;
            }
            if (choice == 'A')
            {
                Console.WriteLine("Abort");
                return false;
            }
        }
    }

    public static ConflictAction ResolveConflict(string fileName, string destinationDir)
    {
        LogWarning($"  - CONFLICT: A file named '{fileName}' already exists in '{destinationDir}'.");
        Console.WriteLine("    Please choose an action:");
        Console.WriteLine("    1: Rename and move (e.g., 'file_duplicate_001.txt')");
        Console.WriteLine("    2: Skip this file");
        Console.WriteLine("    3: Rename All - Automatically rename this and all future conflicts");

        while (true)
        {
            Console.Write("    Enter your choice (1-3): ");
            var keyInfo = Console.ReadKey(true);
            Console.WriteLine(keyInfo.KeyChar);

            switch (keyInfo.KeyChar)
            {
                case '1': return ConflictAction.Rename;
                case '2': return ConflictAction.Skip;
                case '3': return ConflictAction.RenameAll;
                default:
                    LogError("    Invalid choice. Please try again.");
                    break;
            }
        }
    }

    public static AmbiguityChoice ResolveAmbiguity(string sourcePath, List<MatchInfo> matches, string rootDir)
    {
        var sortedMatches = matches
            .OrderByDescending(m => m.BestMatch.Contains(' '))
            .ThenBy(m => Path.GetFileName(m.Path).Length)
            .ThenBy(m => m.Path)
            .ToList();

        LogWarning($"\n- AMBIGUOUS: File '{Path.GetFileName(sourcePath)}' matches multiple directories.");
        Console.WriteLine("  Please choose a destination:");
        for (int i = 0; i < sortedMatches.Count; i++)
        {
            var relativePath = Path.GetRelativePath(rootDir, sortedMatches[i].Path);
            Console.WriteLine($"    {i + 1}: {relativePath}");
        }
        Console.WriteLine("    S: Skip this file");
        Console.WriteLine("    A: Skip All - Skip this and all future ambiguous files");

        while (true)
        {
            Console.Write("  Enter your choice: ");
            var keyInfo = Console.ReadKey(true);
            Console.WriteLine(keyInfo.KeyChar);

            char choiceChar = char.ToUpper(keyInfo.KeyChar);

            if (choiceChar == 'S') return new AmbiguityChoice(AmbiguityAction.Skip);
            if (choiceChar == 'A') return new AmbiguityChoice(AmbiguityAction.SkipAll);

            if (char.IsDigit(choiceChar) && int.TryParse(choiceChar.ToString(), out int choice) && choice > 0 && choice <= sortedMatches.Count)
            {
                return new AmbiguityChoice(AmbiguityAction.Select, sortedMatches[choice - 1].Path);
            }
            
            LogError("  Invalid choice. Please try again.");
        }
    }

    public static void PrintReport(List<(string SourceFile, string FinalDestPath)> movedFiles)
    {
        Console.WriteLine("\n--- File Moving Report ---");
        if (movedFiles.Any())
        {
            Console.WriteLine($"{movedFiles.Count} file(s) were moved:");
            foreach (var (source, finalDestinationPath) in movedFiles)
            {
                LogSuccess($"  - MOVED: '{Path.GetFileName(source)}' to '{finalDestinationPath}'");
            }
        }
        else
        {
            LogInfo("No files were moved.");
        }
    }
    
    // Logging helpers
    public static void LogError(string message) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine(message); Console.ResetColor(); }
    public static void LogWarning(string message) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine(message); Console.ResetColor(); }
    public static void LogSuccess(string message) { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine(message); Console.ResetColor(); }
    public static void LogInfo(string message) { Console.WriteLine(message); }
    public static void LogPermanentChoice(string message) { Console.ForegroundColor = ConsoleColor.Cyan; Console.WriteLine($"    -> {message}"); Console.ResetColor(); }
    public static void LogUserSkip(string sourcePath, bool isPermanent = false)
    {
        var reason = isPermanent ? "User chose to skip all" : "User chose not to move";
        LogWarning($"  - SKIPPED: {reason} '{Path.GetFileName(sourcePath)}'.");
    }
    public static void LogAutoSkip(string sourcePath) => LogWarning($"\n- SKIPPED (auto): File '{Path.GetFileName(sourcePath)}' is ambiguous.");
    public static void LogMove(string sourcePath, string destPath, bool isRenamed)
    {
        var action = isRenamed ? "MOVED & RENAMED" : "MOVED";
        var destDir = Path.GetDirectoryName(destPath);
        LogSuccess($"  - {action}: '{Path.GetFileName(sourcePath)}' to '{Path.GetFileName(destPath)}' in '{destDir}'");
    }
    public static void LogMoveFailure(string sourcePath, string reason) => LogError($"  - FAILED to move '{sourcePath}': {reason}");
}

/// <summary>
/// Generates a set of test directories and files for testing the application.
/// </summary>
public static class TestDataGenerator
{
    public static void Setup()
    {
        var random = new Random();

        // Clean up previous runs
        if (Directory.Exists("temp1")) Directory.Delete("temp1", true);
        if (Directory.Exists("temp2")) Directory.Delete("temp2", true);

        // --- Data for generation ---
        var firstNames = new List<string> { "Иван", "Петр", "Сергей", "Анна", "Мария", "Елена", "Алексей", "Дмитрий", "Ольга", "Татьяна", "John", "Peter", "Michael", "Sarah", "Emily", "David" };
        var lastNames = new List<string> { "Иванов", "Петров", "Сидоров", "Смирнов", "Кузнецова", "Попова", "Васильев", "Зайцев", "Соколов", "Михайлов", "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia" };
        var fileWords = new List<string> { "Report", "Document", "Summary", "Analysis", "Contract", "Invoice", "Presentation", "Notes", "Data", "Archive" };
        var fileExtensions = new List<string> { ".txt", ".docx", ".pdf", ".xlsx", ".jpg", ".png" };

        string GetRandomFullName() => $"{firstNames[random.Next(firstNames.Count)]} {lastNames[random.Next(lastNames.Count)]}";

        var allGeneratedNames = new HashSet<string>();
        string GetRandomUniqueFullName()
        {
            string name;
            do { name = GetRandomFullName(); } while (allGeneratedNames.Contains(name));
            allGeneratedNames.Add(name);
            return name;
        }

        // --- 1. Create `temp1` with 100 folders ---
        Console.WriteLine("Creating 'temp1' directory with 100 folders...");
        Directory.CreateDirectory("temp1");
        var folderNamesForTemp1 = new List<string>();

        // 20% with multiple names (2-10)
        for (int i = 0; i < 20; i++)
        {
            int nameCount = random.Next(2, 11);
            var names = new List<string>();
            for (int j = 0; j < nameCount; j++)
            {
                names.Add(GetRandomUniqueFullName());
            }
            folderNamesForTemp1.Add(string.Join(", ", names));
        }

        // 5% with duplicated names
        for (int i = 0; i < 5; i++)
        {
            if (!allGeneratedNames.Any()) break;
            var existingName = allGeneratedNames.ElementAt(random.Next(allGeneratedNames.Count));
            var newName = GetRandomUniqueFullName();
            folderNamesForTemp1.Add($"{existingName}, {newName}");
        }

        // Remaining 75% with single names
        while (folderNamesForTemp1.Count < 100)
        {
            folderNamesForTemp1.Add(GetRandomUniqueFullName());
        }

        foreach (var folderName in folderNamesForTemp1)
        {
            Directory.CreateDirectory(Path.Combine("temp1", folderName));
        }

        // --- 2. Create `temp2` with 3000 files in a nested structure ---
        Console.WriteLine("Creating 'temp2' directory with a nested structure and 3000 files...");
        Directory.CreateDirectory("temp2");
        var allSourceDirs = new List<string> { "temp2" };

        // Create a random directory structure up to 3 levels deep
        for (int i = 0; i < 10; i++)
        {
            var level1 = Path.Combine("temp2", $"Department_{i + 1}");
            Directory.CreateDirectory(level1);
            allSourceDirs.Add(level1);

            if (random.Next(2) == 0) continue;
            var level2 = Path.Combine(level1, $"Group_{random.Next(100)}");
            Directory.CreateDirectory(level2);
            allSourceDirs.Add(level2);

            if (random.Next(2) == 0) continue;
            var level3 = Path.Combine(level2, $"Team_{random.Next(100)}");
            Directory.CreateDirectory(level3);
            allSourceDirs.Add(level3);
        }

        // Create 3000 files
        var namesForFiles = allGeneratedNames.ToList();
        for (int i = 0; i < 3000; i++)
        {
            string fileName;
            // 10% (300 files) with 2 or 3 names
            if (i < 300)
            {
                var name1 = namesForFiles[random.Next(namesForFiles.Count)];
                var name2 = namesForFiles[random.Next(namesForFiles.Count)];
                if (random.Next(2) == 0)
                {
                    var name3 = namesForFiles[random.Next(namesForFiles.Count)];
                    fileName = $"{name1} and {name2} vs {name3} meeting";
                }
                else
                {
                    fileName = $"Summary for {name1} and {name2}";
                }
            }
            else // 90% with a single name
            {
                var name = namesForFiles[random.Next(namesForFiles.Count)];
                var word = fileWords[random.Next(fileWords.Count)];
                fileName = $"{word} from {name} date {random.Next(1, 29)}-{random.Next(1, 13)}-2024";
            }

            var finalFileName = fileName + fileExtensions[random.Next(fileExtensions.Count)];
            var targetDir = allSourceDirs[random.Next(allSourceDirs.Count)];
            var fullPath = Path.Combine(targetDir, finalFileName);

            try
            {
                File.WriteAllText(fullPath, $"This is a test file: {finalFileName}");
            }
            catch (Exception ex)
            {
                // Handle cases where filename becomes too long or contains invalid chars
                ConsoleUI.LogError($"Could not create file '{fullPath}'. Reason: {ex.Message}. Skipping.");
            }
        }
    }
}

// Data structures and enums

/// <summary>
/// Holds information about a potential match between a file and a directory.
/// </summary>
public record MatchInfo(string Path, string BestMatch);

/// <summary>
/// Represents the user's choice for resolving an ambiguous match.
/// </summary>
public record AmbiguityChoice(AmbiguityAction Action, string? SelectedPath = null);

/// <summary>
/// Defines user actions for ambiguous file matches.
/// </summary>
public enum AmbiguityAction { Select, Skip, SkipAll }

/// <summary>
/// Defines user actions for file conflicts.
/// </summary>
public enum ConflictAction { Rename, Skip, RenameAll }

/// <summary>
/// Represents a single occurrence of a name in a directory path.
/// </summary>
public record NameOccurrence(string Name, string Path);
