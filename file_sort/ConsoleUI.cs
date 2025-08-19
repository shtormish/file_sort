using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

/// <summary>
/// Provides static methods for all console-based user interactions.
/// </summary>
public class ConsoleUI : IUserInterface
{
    private readonly bool _isVerbose;

    public ConsoleUI(bool isVerbose = false)
    {
        _isVerbose = isVerbose;
    }

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

    public bool ConfirmDuplicates(IEnumerable<IGrouping<string, NameOccurrence>> nameOccurrences)
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
            var input = Console.ReadLine()?.Trim().ToUpper();

            if (input == "C")
            {
                Console.WriteLine("Continue");
                return true;
            }
            if (input == "A")
            {
                Console.WriteLine("Abort");
                return false;
            }
        }
    }

    public ConflictAction ResolveConflict(string fileName, string destinationDir)
    {
        LogWarning($"  - CONFLICT: A file named '{fileName}' already exists in '{destinationDir}'.");
        Console.WriteLine("    Please choose an action:");
        Console.WriteLine("    1: Rename and move (e.g., 'file_duplicate_001.txt')");
        Console.WriteLine("    2: Skip this file");
        Console.WriteLine("    3: Rename All - Automatically rename this and all future conflicts");

        while (true)
        {
            Console.Write("    Enter your choice (1-3): ");
            var input = Console.ReadLine();

            if (!string.IsNullOrEmpty(input))
            {
                switch (input.Trim())
                {
                    case "1": return ConflictAction.Rename;
                    case "2": return ConflictAction.Skip;
                    case "3": return ConflictAction.RenameAll;
                }
            }
            LogError("    Invalid choice. Please try again.");
        }
    }

    public AmbiguityChoice ResolveAmbiguity(string sourcePath, List<MatchInfo> matches, string rootDir)
    {
        var sortedMatches = matches
            .OrderByDescending(m => m.BestMatch.Contains(' '))
            .ThenBy(m => Path.GetFileName(m.Path).Length)
            .ThenBy(m => m.Path)
            .ToList();

        LogWarning($"  - AMBIGUOUS: File '{Path.GetFileName(sourcePath)}' matches multiple directories.");
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
            var input = Console.ReadLine();

            if (string.IsNullOrEmpty(input))
            {
                LogError("  Invalid choice. Please try again.");
                continue;
            }

            string choice = input.Trim().ToUpper();

            if (choice == "S") return new AmbiguityChoice(AmbiguityAction.Skip);
            if (choice == "A") return new AmbiguityChoice(AmbiguityAction.SkipAll);

            if (int.TryParse(choice, out int choiceNum) && choiceNum > 0 && choiceNum <= sortedMatches.Count)
            {
                return new AmbiguityChoice(AmbiguityAction.Select, sortedMatches[choiceNum - 1].Path);
            }
            LogError("  Invalid choice. Please try again.");
        }
    }

    public void PrintReport(List<(string SourceFile, string FinalDestPath)> movedFiles)
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

    // Implementation of IUserInterface logging methods
    public void LogError(string message, Exception? ex = null)
    {
        if (!Console.IsOutputRedirected) Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        if (!Console.IsOutputRedirected) Console.ResetColor();

        if (_isVerbose && ex != null)
        {
            if (!Console.IsOutputRedirected) Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"\n--- Verbose Error Details ---\n{ex}\n-----------------------------");
            if (!Console.IsOutputRedirected) Console.ResetColor();
        }
    }
    public void LogWarning(string message)
    {
        if (!Console.IsOutputRedirected) Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        if (!Console.IsOutputRedirected) Console.ResetColor();
    }
    public void LogSuccess(string message)
    {
        if (!Console.IsOutputRedirected) Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        if (!Console.IsOutputRedirected) Console.ResetColor();
    }
    public void LogInfo(string message) { Console.WriteLine(message); }
    public void LogPermanentChoice(string message)
    {
        if (!Console.IsOutputRedirected) Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"    -> {message}");
        if (!Console.IsOutputRedirected) Console.ResetColor();
    }
    public void LogUserSkip(string sourcePath, bool isPermanent = false)
    {
        var reason = isPermanent ? "User chose to skip all" : "User chose not to move";
        LogWarning($"  - SKIPPED: {reason} '{Path.GetFileName(sourcePath)}'.");
    }
    public void LogAutoSkip(string sourcePath) => LogWarning($"  - SKIPPED (auto): File '{Path.GetFileName(sourcePath)}' is ambiguous.");
    public void LogMove(string sourcePath, string destPath, bool isRenamed)
    {
        var action = isRenamed ? "MOVED & RENAMED" : "MOVED";
        var destDir = Path.GetDirectoryName(destPath);
        LogSuccess($"  - {action}: '{Path.GetFileName(sourcePath)}' to '{Path.GetFileName(destPath)}' in '{destDir}'");
    }
    public void LogMoveFailure(string sourcePath, Exception ex) => LogError($"  - FAILED to move '{Path.GetFileName(sourcePath)}'. Reason: {ex.Message}", ex);
}