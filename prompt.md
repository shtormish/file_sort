# Technical Specification: File Sorter Utility

## 1. Introduction & Goals

### 1.1. Overview
The File Sorter Utility is a .NET command-line application designed to automate the organization of files. It scans a source directory, intelligently matches files to predefined target directories, and moves them accordingly, handling potential conflicts and ambiguities through interactive user prompts.

### 1.2. Core Objectives
- **Automation:** Reduce the manual effort required to sort large volumes of files.
- **Accuracy:** Implement a robust matching algorithm to ensure files are placed in the correct destination.
- **User-Friendliness:** Provide a clear, interactive CLI with helpful prompts and error messages.
- **Robustness:** Gracefully handle file system errors, invalid user input, and edge cases.
- **Testability:** Design the application with a decoupled architecture to allow for comprehensive unit and integration testing.
- **Performance:** Efficiently process large directories by parallelizing CPU-bound tasks.

---

## 2. System Architecture

The application follows the Dependency Inversion Principle to achieve a clean, decoupled, and testable design.

### 2.1. Components
- **`Program.cs` (Composition Root):**
  - The main entry point of the application.
  - Responsible for parsing command-line arguments (`--help`, `--verbose`, paths).
  - Instantiates and "wires up" all dependencies (the "Composition Root"). It creates concrete instances of `ConsoleUI` and `FileSystem` and injects them into the `FileSorter`.
  - Orchestrates the top-level application flow and handles fatal exceptions.

- **`FileSorter.cs` (Core Logic):**
  - Contains the primary business logic for the sorting process.
  - Is completely decoupled from the `Console` and the physical file system. It operates exclusively against the `IUserInterface` and `IFileSystem` abstractions.
  - Manages the state of the sorting session (e.g., `_renameAllConflicts`, `_skipAllAmbiguous` flags).

- **`IUserInterface` / `ConsoleUI.cs` (UI Abstraction):**
  - `IUserInterface` defines the contract for all user interactions (e.g., resolving conflicts, printing reports, logging messages).
  - `ConsoleUI` is the concrete implementation that interacts with the system console. It handles all text output, color-coding, and reading user input. It supports a `verbose` mode for detailed error logging.

- **`IFileSystem` / `System.IO.Abstractions` (Filesystem Abstraction):**
  - The application uses the `System.IO.Abstractions` library to decouple from `System.IO`.
  - All file operations (read, move, check existence) in `FileSorter` are performed against the `IFileSystem` interface.
  - This allows a `MockFileSystem` to be used in unit tests for fast, reliable, and in-memory testing.

---

## 3. Core Logic & Algorithms

### 3.1. Target Directory Scanning
1.  The system enumerates all top-level directories within the specified `targetDirectory`.
2.  For each directory, its name is treated as a source of keywords. The name is split by `, ` to extract multiple distinct names (e.g., `"Anna Karenina, Leo Tolstoy"` becomes `"Anna Karenina"` and `"Leo Tolstoy"`).
3.  For each extracted name, two variants are generated for matching:
    - The original trimmed name (e.g., `John Smith`).
    - A "spaceless" variant (e.g., `JohnSmith`).
4.  A `Dictionary<string, List<string>>` maps each target directory path to its list of name variants.

### 3.2. Source File Matching
1.  The system enumerates all files in the `sourceDirectory` recursively.
2.  To improve performance, this list of files is processed in parallel using Parallel LINQ (`AsParallel()`).
3.  For each file, the matching logic is executed:
    - The file's name (without extension) is compared against all name variants from the directory map.
    - The comparison is a case-insensitive `Contains` check.
    - All successful matches are collected.
    - The algorithm then finds the length of the **longest** match among all possibilities.
    - It filters the results, keeping only the matches that correspond to this maximum length. This ensures that for a file containing "Project X", a folder named "Project X" is prioritized over a folder named "Project".
4.  The result of the parallel analysis is a list of files, each associated with one or more "best-match" directories.

### 3.3. Sequential Processing
After the parallel analysis, the results are processed sequentially to ensure safe user interaction and state management.

- **Single Match:** If a file has exactly one best-match directory, it proceeds to the move/conflict resolution phase.
- **Multiple Matches (Ambiguity):** If a file has more than one best-match directory, the ambiguity resolution flow is triggered.

### 3.4. Conflict & Ambiguity Resolution

- **Conflict (File Exists):**
  - Triggered if the destination file path already exists.
  - The user is prompted to (1) **Rename**, (2) **Skip**, or (3) **Rename All**.
  - `RenameAll` sets a session-wide flag to automatically rename all subsequent conflicts.
  - The renaming scheme generates a new name: `[filename]_duplicate_[###].[ext]`, starting with `001` and incrementing until a free slot is found.

- **Ambiguity (Multiple Destinations):**
  - The user is presented with a numbered list of potential destination folders.
  - Options include selecting a folder by number, (S)kipping the current file, or (A)borting by skipping all future ambiguous files.
  - `SkipAll` sets a session-wide flag.

---

## 4. Command-Line Interface (CLI)

| Command / Flag | Alias | Description |
| :--- | :--- | :--- |
| `<target_path> <source_path>` | | The main arguments specifying the target and source directories. Must be enclosed in quotes if they contain spaces. |
| `--help` | `-h` | Displays the help screen and exits with code `0`. |
| `--setup-test-data` | | Generates a set of test folders and files in the current directory and exits with code `0`. |
| `--verbose` | | Enables detailed error logging, including full exception stack traces. |

**Exit Codes:**
- `0`: Success.
- `1`: Error (e.g., invalid arguments, unhandled exception, user abort).

---

## 5. Error Handling

- **Argument Validation:** The application validates that exactly two path arguments are provided. If not, it prints a usage error and exits with code `1`.
- **Path Validation:** It checks if the provided source and target directories exist. If not, it prints an error and exits with code `1`.
- **Filesystem Errors:**
  - **Fatal Errors:** Exceptions during the initial directory scanning (e.g., `UnauthorizedAccessException`) are considered fatal. The exception propagates to `Program.Main`, is logged, and the application exits with code `1`.
  - **Non-Fatal Errors:** Exceptions during individual file move operations (e.g., `IOException`, `PathTooLongException`) are caught within a `try-catch` block. The failure is logged via `IUserInterface.LogMoveFailure`, and the application continues to the next file.
- **Verbose Mode:** When the `--verbose` flag is present, the `LogError` method will print the full exception details (`ex.ToString()`) to the console, providing a complete stack trace for debugging.

---

## 6. Testing Strategy

The project employs a dual-layered testing strategy to ensure code quality and correctness.

### 6.1. Unit Tests (`FileSorterTests.cs`)
- **Framework:** xUnit.
- **Purpose:** To test the core logic of the `FileSorter` class in complete isolation.
- **Mocks:**
  - `Moq` is used to create mock implementations of `IUserInterface`.
  - `System.IO.Abstractions.TestingHelpers.MockFileSystem` is used to create an in-memory mock of `IFileSystem`.
- **Coverage:** Tests cover successful moves, conflict resolution paths, ambiguity choices, error handling (mocked exceptions), and various edge cases in name matching.

### 6.2. Integration Tests (`IntegrationTests.cs`)
- **Framework:** xUnit.
- **Purpose:** To test the application's end-to-end behavior, including CLI parsing and interaction with a real file system.
- **Methodology:**
  - Tests invoke `Program.Main(args)` directly.
  - `Console.SetIn` and `Console.SetOut` are used to redirect I/O, allowing tests to simulate user input and capture console output for assertions.
  - Tests run against a temporary directory on the actual file system, which is created before each test and deleted afterward.
- **Coverage:** Tests cover CLI flag handling (`--help`, invalid arguments), and full user interaction flows for conflicts and ambiguities.

---

## 7. Build & Release Process

The release process is fully automated using GitHub Actions.

### 7.1. Workflow (`.github/workflows/release.yml`)
- **Trigger:** The workflow is triggered automatically on a `git push` of a tag that matches the pattern `v*.*.*` (e.g., `v1.0.1`).
- **Permissions:** The workflow is granted `contents: write` permissions to allow it to create GitHub Releases.
- **Cross-Platform Builds:** The job runs on an `ubuntu-latest` runner and uses `dotnet publish` with specific Runtime Identifiers (RID) to create executables for:
  - `win-x64`
  - `linux-x64`
  - `osx-x64`
- **Versioning:** The version number is extracted from the git tag (e.g., `v1.0.1` -> `1.0.1`) and injected into the assembly's metadata during the publish step via the `/p:Version` MSBuild property. This ensures the version displayed by `--help` is always accurate.
- **Artifacts:** The output for each platform is packaged into an appropriate archive (`.zip` for Windows, `.tar.gz` for Linux/macOS).
- **Release Creation:** A new GitHub Release is created, titled with the tag name. The workflow automatically generates release notes based on the commit history since the last tag and attaches the platform-specific archives for users to download.

### 7.2. Release Script (`release.bat`)
A helper batch script is provided for Windows users to simplify the release process. It automates the following steps:
1. Stages all changes (`git add .`).
2. Commits with a standard message (`git commit -m "release"`).
3. Prompts the user to enter a version tag.
4. Creates the local git tag.
5. Pushes the commit and the new tag to GitHub, triggering the release workflow.