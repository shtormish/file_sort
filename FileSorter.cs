using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

/// <summary>
/// Encapsulates the core logic for scanning, matching, and sorting files.
/// </summary>
public class FileSorter
{
    private readonly string _targetDirectory;
    private readonly string _sourceDirectory;
    private readonly IFileSystem _fileSystem;
    private readonly IUserInterface _ui;
    private readonly Dictionary<string, List<string>> _directoryNamesMap = new();
    private readonly List<(string SourceFile, string FinalDestPath)> _movedFilesLog = new();

    private bool _renameAllConflicts = false;
    private bool _skipAllAmbiguous = false;

    public FileSorter(string targetDirectory, string sourceDirectory, IFileSystem fileSystem, IUserInterface ui)
    {
        _targetDirectory = targetDirectory;
        _sourceDirectory = sourceDirectory;
        _fileSystem = fileSystem;
        _ui = ui;
    }

    /// <summary>
    /// Executes the entire file sorting process.
    /// </summary>
    public void Run()
    {
        ScanTargetDirectories();

        if (!CheckForDuplicateNames())
        {
            _ui.LogWarning("\nOperation aborted by user.");
            return;
        }

        ProcessSourceFiles();
        _ui.PrintReport(_movedFilesLog);
    }

    /// <summary>
    /// Scans target directories and builds a map of paths to associated names.
    /// </summary>
    private void ScanTargetDirectories()
    {
        _ui.LogInfo($"Scanning target directories in: {_targetDirectory}");
        foreach (var dirPath in _fileSystem.Directory.EnumerateDirectories(_targetDirectory))
        {
            var dirName = _fileSystem.Path.GetFileName(dirPath);
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
            return _ui.ConfirmDuplicates(nameOccurrences);
        }
        return true;
    }

    /// <summary>
    /// Scans the source directory for files and processes each one.
    /// </summary>
    private void ProcessSourceFiles()
    {
        _ui.LogInfo($"\nScanning source files in: {_sourceDirectory}");
        var sourceFiles = _fileSystem.Directory.EnumerateFiles(_sourceDirectory, "*.*", SearchOption.AllDirectories);

        _ui.LogInfo($"Found {sourceFiles.Count()} files to process.");
        _ui.LogInfo("\nStarting file processing...");

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
        var fileNameWithoutExt = _fileSystem.Path.GetFileNameWithoutExtension(sourceFilePath);

        var matches = _directoryNamesMap
            .Select(dirEntry => new
            {
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
            _ui.LogAutoSkip(sourcePath);
            return;
        }

        var choice = _ui.ResolveAmbiguity(sourcePath, matches, _targetDirectory);

        switch (choice.Action)
        {
            case AmbiguityAction.Select:
                if (choice.SelectedPath != null)
                {
                    MoveFileWithConflictResolution(sourcePath, choice.SelectedPath);
                }
                break;
            case AmbiguityAction.Skip:
                _ui.LogUserSkip(sourcePath);
                break;
            case AmbiguityAction.SkipAll:
                _skipAllAmbiguous = true;
                _ui.LogUserSkip(sourcePath, isPermanent: true);
                break;
        }
    }

    /// <summary>
    /// Moves a file, handling potential naming conflicts by asking the user.
    /// </summary>
    private void MoveFileWithConflictResolution(string sourcePath, string destinationDir)
    {
        var fileName = _fileSystem.Path.GetFileName(sourcePath);
        var destFilePath = _fileSystem.Path.Combine(destinationDir, fileName);

        if (!_fileSystem.File.Exists(destFilePath))
        {
            MoveAndLog(sourcePath, destFilePath);
            return;
        }

        if (_renameAllConflicts)
        {
            RenameAndMove(sourcePath, destinationDir);
            return;
        }

        var choice = _ui.ResolveConflict(fileName, destinationDir);
        switch (choice)
        {
            case ConflictAction.Rename:
                RenameAndMove(sourcePath, destinationDir);
                break;
            case ConflictAction.Skip:
                _ui.LogUserSkip(sourcePath);
                break;
            case ConflictAction.RenameAll:
                _renameAllConflicts = true;
                _ui.LogPermanentChoice("Automatic renaming for all future conflicts is now ENABLED.");
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
    private string GenerateUniqueFilePath(string sourcePath, string destinationDir)
    {
        var fileNameWithoutExt = _fileSystem.Path.GetFileNameWithoutExtension(sourcePath);
        var extension = _fileSystem.Path.GetExtension(sourcePath);
        int copyCount = 1;
        string newDestFilePath;
        do
        {
            var newFileName = $"{fileNameWithoutExt}_duplicate_{copyCount:D3}{extension}";
            newDestFilePath = _fileSystem.Path.Combine(destinationDir, newFileName);
            copyCount++;
        } while (_fileSystem.File.Exists(newDestFilePath));
        return newDestFilePath;
    }

    /// <summary>
    /// Performs the actual file move and logs the operation.
    /// </summary>
    private void MoveAndLog(string sourcePath, string destPath, bool isRenamed = false)
    {
        try
        {
            _fileSystem.File.Move(sourcePath, destPath);
            _movedFilesLog.Add((sourcePath, destPath));
            _ui.LogMove(sourcePath, destPath, isRenamed);
        }
        catch (Exception ex)
        {
            _ui.LogMoveFailure(sourcePath, ex.Message);
        }
    }
}