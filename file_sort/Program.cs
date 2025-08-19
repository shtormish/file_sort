﻿﻿﻿using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

/// <summary>
/// Main entry point for the application.
/// Handles command-line argument parsing and orchestrates the overall process.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        // Check for verbose flag anywhere in the arguments
        bool isVerbose = args.Contains("--verbose");
        // Filter out the verbose flag so it doesn't interfere with path arguments
        var filteredArgs = args.Where(a => a != "--verbose").ToArray();

        var ui = new ConsoleUI(isVerbose);

        // Handle command-line flags
        if (filteredArgs.Length > 0)
        {
            if (filteredArgs[0] == "-h" || filteredArgs[0] == "--help")
            {
                ConsoleUI.ShowHelp();
                return 0;
            }

            if (filteredArgs[0] == "--setup-test-data")
            {
                try
                {
                    ui.LogInfo("Setting up test data...");
                    var fileSystem = new FileSystem();
                    TestDataGenerator.Setup(fileSystem, ui);
                    ui.LogSuccess("Test data setup complete. The 'temp1' and 'temp2' directories have been created.");
                    return 0;
                }
                catch (Exception ex)
                {
                    ui.LogError("Failed to set up test data.", ex);
                    return 1;
                }
            }
        }

        // Validate main arguments
        if (filteredArgs.Length < 2)
        {
            ui.LogError("Error: Please provide two directory paths as arguments.");
            Console.WriteLine("Usage: file_sort \"<target_folders_path>\" \"<source_files_path>\"");
            Console.WriteLine("Use 'file_sort -h' for more information.");
            return 1;
        }

        var targetDirectory = filteredArgs[0];
        var sourceDirectory = filteredArgs[1];

        var realFileSystem = new FileSystem();
        if (!realFileSystem.Directory.Exists(targetDirectory))
        {
            ui.LogError($"Error: Target directory not found at path '{targetDirectory}'.");
            return 1;
        }

        if (!realFileSystem.Directory.Exists(sourceDirectory))
        {
            ui.LogError($"Error: Source directory not found at path '{sourceDirectory}'.");
            return 1;
        }

        try
        {
            // Wire up dependencies for the main application run
            var sorter = new FileSorter(targetDirectory, sourceDirectory, realFileSystem, ui);
            sorter.Run();
        }
        catch (Exception ex)
        {
            ui.LogError("An unexpected error occurred.", ex);
            return 1;
        }

        return 0;
    }
}
