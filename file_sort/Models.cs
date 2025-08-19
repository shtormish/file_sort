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