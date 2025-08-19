using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Defines the contract for user interface interactions, allowing for mock implementations in tests.
/// </summary>
public interface IUserInterface
{
    bool ConfirmDuplicates(IEnumerable<IGrouping<string, NameOccurrence>> nameOccurrences);
    ConflictAction ResolveConflict(string fileName, string destinationDir);
    AmbiguityChoice ResolveAmbiguity(string sourcePath, List<MatchInfo> matches, string rootDir);
    void PrintReport(List<(string SourceFile, string FinalDestPath)> movedFiles);
    void LogError(string message);
    void LogWarning(string message);
    void LogSuccess(string message);
    void LogInfo(string message);
    void LogPermanentChoice(string message);
    void LogUserSkip(string sourcePath, bool isPermanent = false);
    void LogAutoSkip(string sourcePath);
    void LogMove(string sourcePath, string destPath, bool isRenamed);
    void LogMoveFailure(string sourcePath, string reason);
}