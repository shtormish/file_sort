﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
FindAndReportDuplicateNames(directoryNamesMap);

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
Console.WriteLine("\nStarting file processing...");

foreach (var fileEntry in fileMap)
{
    var sourceFilePath = fileEntry.Key;
    var fileNameWithoutExt = fileEntry.Value;

    var matchingTargetDirs = directoryNamesMap
        .Where(dirEntry => dirEntry.Value.Any(nameVariant =>
            fileNameWithoutExt.Contains(nameVariant, StringComparison.OrdinalIgnoreCase)))
        .Select(dirEntry => dirEntry.Key)
        .ToList();

    if (matchingTargetDirs.Count == 1)
    {
        // Unique match: move the file
        MoveFile(sourceFilePath, matchingTargetDirs.First(), movedFiles);
    }
    else if (matchingTargetDirs.Count > 1)
    {
        // Ambiguous match: ask the user for input
        HandleAmbiguousMove(sourceFilePath, matchingTargetDirs, movedFiles);
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
/// Finds and prints any duplicate names found among the target directories.
/// </summary>
static void FindAndReportDuplicateNames(Dictionary<string, List<string>> directoryNamesMap)
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
    }
}

/// <summary>
/// Moves a file from a source path to a destination directory.
/// </summary>
static void MoveFile(string sourcePath, string destinationDir, List<(string, string)> movedFilesLog)
{
    try
    {
        var fileName = Path.GetFileName(sourcePath);
        var destFilePath = Path.Combine(destinationDir, fileName);

        if (File.Exists(destFilePath))
        {
            Console.WriteLine($"  - CONFLICT: A file named '{fileName}' already exists in '{destinationDir}'.");
            Console.WriteLine("    Please choose an action:");
            Console.WriteLine("    1: Rename and move (e.g., 'file (1).txt')");
            Console.WriteLine("    2: Skip this file");

            while (true)
            {
                Console.Write("    Enter your choice (1-2): ");
                var input = Console.ReadLine();
                if (int.TryParse(input, out int choice))
                {
                    if (choice == 1) // Rename
                    {
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourcePath);
                        var extension = Path.GetExtension(sourcePath);
                        int copyCount = 1;
                        string newDestFilePath;
                        do
                        {
                            var newFileName = $"{fileNameWithoutExt} ({copyCount}){extension}";
                            newDestFilePath = Path.Combine(destinationDir, newFileName);
                            copyCount++;
                        } while (File.Exists(newDestFilePath));
                        
                        File.Move(sourcePath, newDestFilePath);
                        movedFilesLog.Add((sourcePath, newDestFilePath));
                        Console.WriteLine($"  - MOVED & RENAMED: '{fileName}' to '{Path.GetFileName(newDestFilePath)}' in '{destinationDir}'");
                        return;
                    }
                    if (choice == 2) // Skip
                    {
                        Console.WriteLine($"  - SKIPPED: User chose not to move '{fileName}'.");
                        return;
                    }
                }
                Console.WriteLine("    Invalid choice. Please try again.");
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
/// Handles an ambiguous file move by prompting the user for a choice.
/// </summary>
static void HandleAmbiguousMove(string sourcePath, List<string> possibleDirs, List<(string, string)> movedFilesLog)
{
    Console.WriteLine($"\n- AMBIGUOUS: File '{Path.GetFileName(sourcePath)}' matches multiple directories.");
    Console.WriteLine("  Please choose a destination:");
    for (int i = 0; i < possibleDirs.Count; i++)
    {
        Console.WriteLine($"    {i + 1}: {possibleDirs[i]}");
    }
    Console.WriteLine("    0: Skip (Do not move)");

    while (true)
    {
        Console.Write("  Enter your choice: ");
        var input = Console.ReadLine();
        if (int.TryParse(input, out int choice))
        {
            if (choice == 0)
            {
                Console.WriteLine($"  - SKIPPED: User chose not to move '{Path.GetFileName(sourcePath)}'.");
                return;
            }
            if (choice > 0 && choice <= possibleDirs.Count)
            {
                var selectedDir = possibleDirs[choice - 1];
                MoveFile(sourcePath, selectedDir, movedFilesLog);
                return;
            }
        }
        Console.WriteLine("  Invalid choice. Please try again.");
    }
}
