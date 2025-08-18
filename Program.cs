﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// Check for help request
if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help"))
{
    ShowHelp();
    return 0;
}

// Check for test data setup mode
if (args.Length == 1 && args[0] == "--setup-test-data")
{
    Console.WriteLine("Setting up test data...");
    SetupTestData();
    Console.WriteLine("Test data setup complete. The 'temp1' and 'temp2' directories have been created.");
    return 0;
}
// 1. Get directory paths from command-line arguments
if (args.Length < 2)
{
    Console.WriteLine("Error: Please provide two directory paths as arguments.");
    Console.WriteLine("Usage: dotnet run \"<target_folders_path>\" \"<source_files_path>\"");
    return 1; // Return an error code
}

var targetDirectory = args[0];
var sourceDirectory = args[1];

if (!Directory.Exists(targetDirectory))
{
    Console.WriteLine($"Error: Target directory not found at path '{targetDirectory}'");
    return 1;
}

if (!Directory.Exists(sourceDirectory))
{
    Console.WriteLine($"Error: Source directory not found at path '{sourceDirectory}'");
    return 1;
}

Console.WriteLine($"Scanning target directories in: {targetDirectory}");

// 3. Create a dictionary for target folders: key is the folder path, value is a list of names
var directoryNamesMap = new Dictionary<string, List<string>>();

try
{
    // 2. Scan subdirectories efficiently
    foreach (var dirPath in Directory.EnumerateDirectories(targetDirectory))
    {
        var dirName = Path.GetFileName(dirPath);
        var namesInDir = dirName.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);

        var processedNames = new HashSet<string>(); // Using a HashSet to store unique names and their variations
        foreach (var name in namesInDir)
        {
            var trimmedName = name.Trim();
            processedNames.Add(trimmedName); // Original name
            processedNames.Add(trimmedName.Replace(" ", "")); // Variation without spaces
        }
        
        if (processedNames.Any())
        {
            directoryNamesMap[dirPath] = processedNames.ToList();
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred while accessing the file system: {ex.Message}");
    return 1;
}

// 4. Find and report duplicate names among target folders
if (!FindAndReportDuplicateNames(directoryNamesMap))
{
    Console.WriteLine("\nOperation aborted by user.");
    return 0;
}

// 5. Scan source directory for files recursively and create a map
Console.WriteLine($"\nScanning source files in: {sourceDirectory}");
var fileMap = new Dictionary<string, string>();
try
{
    foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*.*", SearchOption.AllDirectories))
    {
        fileMap[filePath] = Path.GetFileNameWithoutExtension(filePath);
    }
    Console.WriteLine($"Found {fileMap.Count} files to process.");
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred while scanning source files: {ex.Message}");
    return 1;
}

// 6, 7, 8. Match files to directories and move them
var movedFiles = new List<(string SourceFile, string FinalDestPath)>();
bool skipAllAmbiguous = false;
bool renameAllConflicts = false;
Console.WriteLine("\nStarting file processing...");

foreach (var fileEntry in fileMap)
{
    var sourceFilePath = fileEntry.Key;
    var fileNameWithoutExt = fileEntry.Value;

    var matches = directoryNamesMap
        .Select(dirEntry => new {
            Path = dirEntry.Key,
            // Find the best matching variant within this directory's names
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
        // Unique match: move the file
        MoveFile(sourceFilePath, matches.First().Path, movedFiles, ref renameAllConflicts);
    }
    else if (matches.Count > 1)
    {
        // Ambiguous match: ask the user for input
        HandleAmbiguousMove(sourceFilePath, matches, targetDirectory, movedFiles, ref skipAllAmbiguous, ref renameAllConflicts);
    }
}

// 9. List all moved files
Console.WriteLine("\n--- File Moving Report ---");
if (movedFiles.Any())
{
    Console.WriteLine($"{movedFiles.Count} file(s) were moved:");
    foreach (var (source, finalDestinationPath) in movedFiles)
    {
        Console.WriteLine($"  - MOVED: '{Path.GetFileName(source)}' to '{finalDestinationPath}'");
    }
}
else
{
    Console.WriteLine("No files were moved.");
}

return 0;


/// <summary>
/// Displays the help screen with usage information.
/// </summary>
static void ShowHelp()
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
/// <summary>
/// Finds and prints any duplicate names found among the target directories.
/// </summary>
static bool FindAndReportDuplicateNames(Dictionary<string, List<string>> directoryNamesMap)
{
    // Find duplicate names across all folders using LINQ
var nameOccurrences = directoryNamesMap
    .SelectMany(kvp => kvp.Value.Select(name => new { Name = name, Path = kvp.Key }))
    .GroupBy(item => item.Name)
    .Where(group => group.Count() > 1);

    if (nameOccurrences.Any())
    {
        Console.WriteLine("\nWarning: The following name duplicates were found in target directories:");
        foreach (var group in nameOccurrences)
        {
            Console.WriteLine($"\n- Name '{group.Key}' appears in the following folders:");
            foreach (var item in group)
            {
                Console.WriteLine($"  - {item.Path}");
            }
        }

        Console.WriteLine("\nThis may lead to incorrect file placement during the process.");
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
    return true; // No duplicates found, continue by default
}

/// <summary>
/// Moves a file from a source path to a destination directory, handling conflicts.
/// </summary>
static void MoveFile(string sourcePath, string destinationDir, List<(string, string)> movedFilesLog, ref bool renameAllConflicts)
{
    try
    {
        var fileName = Path.GetFileName(sourcePath);
        var destFilePath = Path.Combine(destinationDir, fileName);

        if (File.Exists(destFilePath))
        {
            if (renameAllConflicts)
            {
                RenameAndMove(sourcePath, destinationDir, movedFilesLog);
                return;
            }

            Console.WriteLine($"  - CONFLICT: A file named '{fileName}' already exists in '{destinationDir}'.");
            Console.WriteLine("    Please choose an action:");
            Console.WriteLine("    1: Rename and move (e.g., 'file_duplicate_001.txt')");
            Console.WriteLine("    2: Skip this file");
            Console.WriteLine("    3: Rename All - Automatically rename this and all future conflicts");

            while (true)
            {
                Console.Write("    Enter your choice (1-3): ");
                var keyInfo = Console.ReadKey(true);
                Console.WriteLine(keyInfo.KeyChar);
                char choiceChar = keyInfo.KeyChar;

                switch (choiceChar)
                {
                    case '1': // Rename
                    {
                        RenameAndMove(sourcePath, destinationDir, movedFilesLog);
                        return;
                    }
                    case '2': // Skip
                    {
                        Console.WriteLine($"  - SKIPPED: User chose not to move '{fileName}'.");
                        return;
                    }
                    case '3': // Rename All
                    {
                        renameAllConflicts = true;
                        Console.WriteLine("    -> Automatic renaming for all future conflicts is now ENABLED.");
                        RenameAndMove(sourcePath, destinationDir, movedFilesLog);
                        return;
                    }
                    default:
                        Console.WriteLine("    Invalid choice. Please try again.");
                        break;
                }
            }
        }

        File.Move(sourcePath, destFilePath);
        movedFilesLog.Add((sourcePath, destFilePath));
        Console.WriteLine($"  - MOVED: '{fileName}' to '{destinationDir}'");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  - FAILED to move '{sourcePath}': {ex.Message}");
    }
}

/// <summary>
/// Renames a file to avoid conflicts and moves it to the destination.
/// </summary>
static void RenameAndMove(string sourcePath, string destinationDir, List<(string, string)> movedFilesLog)
{
    var fileName = Path.GetFileName(sourcePath);
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

    File.Move(sourcePath, newDestFilePath);
    movedFilesLog.Add((sourcePath, newDestFilePath));
    Console.WriteLine($"  - MOVED & RENAMED: '{fileName}' to '{Path.GetFileName(newDestFilePath)}' in '{destinationDir}'");
}

/// <summary>
/// Handles an ambiguous file move by prompting the user for a choice.
/// </summary>
static void HandleAmbiguousMove(string sourcePath, List<MatchInfo> matches, string rootDir, List<(string, string)> movedFilesLog, ref bool skipAllAmbiguous, ref bool renameAllConflicts)
{
    if (skipAllAmbiguous)
    {
        Console.WriteLine($"\n- SKIPPED (auto): File '{Path.GetFileName(sourcePath)}' is ambiguous.");
        return;
    }

    // Sort the matches based on the requested criteria
    var sortedMatches = matches
        .OrderByDescending(m => m.BestMatch.Contains(' ')) // 1. Full name match (name + surname)
        .ThenBy(m => Path.GetFileName(m.Path).Length)      // 2. Directory name length
        .ThenBy(m => m.Path)                               // 3. Alphabetical
        .ToList();

    Console.WriteLine($"\n- AMBIGUOUS: File '{Path.GetFileName(sourcePath)}' matches multiple directories.");
    Console.WriteLine("  Please choose a destination:");
    for (int i = 0; i < sortedMatches.Count; i++)
    {
        // Display path relative to the root target directory
        var relativePath = Path.GetRelativePath(rootDir, sortedMatches[i].Path);
        Console.WriteLine($"    {i + 1}: {relativePath}");
    }
    Console.WriteLine("    S: Skip this file");
    Console.WriteLine("    A: Skip All - Skip this and all future ambiguous files");

    while (true)
    {
        Console.Write("  Enter your choice: ");
        var keyInfo = Console.ReadKey(true); // Read key without displaying it or waiting for Enter
        Console.WriteLine(keyInfo.KeyChar); // Echo the character so the user sees their choice

        char choiceChar = char.ToUpper(keyInfo.KeyChar);

        if (choiceChar == 'S')
        {
                Console.WriteLine($"  - SKIPPED: User chose not to move '{Path.GetFileName(sourcePath)}'.");
                return;
        }
        if (choiceChar == 'A')
        {
                skipAllAmbiguous = true;
                Console.WriteLine("  -> Automatic skipping for all future ambiguous files is now ENABLED.");
                Console.WriteLine($"  - SKIPPED: User chose not to move '{Path.GetFileName(sourcePath)}'.");
                return;
        }

        if (char.IsDigit(choiceChar) && choiceChar != '0')
        {
            int choice = choiceChar - '0'; // Convert char '1' to int 1
            if (choice > 0 && choice <= sortedMatches.Count)
            {
                var selectedDir = sortedMatches[choice - 1].Path;
                MoveFile(sourcePath, selectedDir, movedFilesLog, ref renameAllConflicts);
                return;
            }
        }
        Console.WriteLine("  Invalid choice. Please try again.");
    }
}

/// <summary>
/// Generates a set of test directories and files for testing the application.
/// </summary>
static void SetupTestData()
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
            // Handle cases where filename becomes too long or contains invalid chars, though unlikely with this generator
            Console.WriteLine($"Could not create file '{fullPath}'. Reason: {ex.Message}. Skipping.");
        }
    }
}

/// <summary>
/// Holds information about a potential match between a file and a directory.
/// </summary>
record MatchInfo(string Path, string BestMatch);
