# Technical Specification: Intelligent File Sorter Utility

## 1. Introduction and Goals

### 1.1. Overview

Develop a .NET console utility designed to automate the file organization process. The application must scan a source directory for files, find the most suitable target directory for each file based on name analysis, and move the files, interactively resolving potential conflicts and ambiguities with the user.

### 1.2. Key Objectives

- **Automation:** Minimize manual labor when sorting large volumes of files.
- **Accuracy:** Implement a reliable matching algorithm to ensure correct file placement.
- **Usability:** Create a clear and interactive command-line interface (CLI) with helpful prompts and error messages.
- **Reliability:** Ensure proper handling of file system errors, invalid user input, and edge cases.
- **Testability:** Design the application with a clearly decoupled architecture that allows for comprehensive unit and integration testing.

---

## 2. Architecture and Design Principles

### 2.1. Architectural Approach

The application must be built on the principles of **layered architecture** and **Dependency Injection (DI)** to achieve loose coupling between components and high testability.

### 2.2. Logical Layers

- **Core Logic Layer:**
  - Contains all business logic: scanning, matching, and conflict resolution algorithms.
  - This layer must be completely independent of the implementation details of the user interface and the file system. It should not contain code that interacts directly with `Console` or `System.IO`.

- **User Interface (UI) Layer:**
  - Responsible for all user interactions: displaying information, prompting for input, and formatting output.
  - Implements an interface defined by the core layer.

- **Infrastructure Layer:**
  - Responsible for interaction with external systems, primarily the file system.
  - Implements an interface defined by the core layer.

### 2.3. Abstractions

To ensure loose coupling, interfaces (abstractions) must be used for all external dependencies of the core logic.

- **UI Abstraction (`IUserInterface`):** Defines the contract for all user interaction operations (logging, selection prompts, confirmations).
- **File System Abstraction (`IFileSystem`):** Defines the contract for all file operations (reading directories, moving files, checking existence). Using a ready-made library like `System.IO.Abstractions` is recommended.

### 2.4. Key Technical Approaches

-   **Parallel Processing (PLINQ):** The file analysis phase, which is CPU-intensive, must be parallelized to significantly improve performance when processing thousands of files.
-   **Defensive Programming:** The system must implement pre-run checks (e.g., for duplicate names in target folders) and validate all user inputs to prevent errors. It should also gracefully handle non-fatal exceptions (like file access errors) without crashing.
-   **Stateful Session Management:** The application must maintain state within a single run to remember user decisions. For example, if a user selects "Rename All" for a file conflict, this choice should be automatically applied to all subsequent conflicts in that session.
-   **Command-Line Argument Parsing:** A robust argument parser should be implemented to handle flags (`--help`, `--verbose`) and positional arguments (paths), including support for paths with spaces.

---

## 3. Core Functionality and Algorithms

### 3.1. Target Directory Analysis

1.  The system scans the top-level target directory.
2.  For each subdirectory, its name is parsed to extract one or more entities. Names can be separated by a comma (e.g., `"Company A, Company B"`).
3.  For each extracted entity, matching variants are created: the original name and a "spaceless" variant (e.g., for `"John Smith"`, `"JohnSmith"` is also created).
4.  The result is an internal data structure (e.g., a dictionary) that maps each target folder path to its list of name variants.

### 3.2. Source File Matching

1.  The system recursively scans the source directory to get a complete list of files.
2.  To improve performance, file analysis should be performed in parallel (e.g., using PLINQ).
3.  For each file, the following matching algorithm is executed:
    - The filename (without extension) is compared against all name variants from all target folders.
    - The comparison must be case-insensitive and check for **containment** of one string within another.
    - From all found matches, the **longest** (most specific) one is identified.
    - The result includes only those matches whose length is equal to the maximum length. This ensures that for a file `"Report for Project X"`, the folder `"Project X"` will have priority over the folder `"Project"`.

### 3.3. Result Processing

After parallel analysis, the results are processed sequentially to ensure safe user interaction.

- **Unambiguous Match:** If only one best-match candidate folder is found for a file, it proceeds to the move stage.
- **Ambiguous Match:** If multiple candidate folders are found, the ambiguity resolution process is triggered.

### 3.4. Conflict and Ambiguity Resolution

- **Conflict (File Already Exists):**
  - Occurs if a file with the same name already exists in the target folder.
  - The user must be offered options: **Rename**, **Skip**, **Rename All**.
  - Selecting "Rename All" must set a session-wide flag to automatically resolve subsequent conflicts.
  - Renaming scheme: `[filename]_duplicate_[###].[extension]`.

- **Ambiguity (Multiple Possible Folders):**
  - The user must be presented with a numbered list of candidate folders.
  - Choice options: folder number, **Skip** the current file, **Skip All** ambiguous files.
  - Selecting "Skip All" also sets a session-wide flag.

---

## 4. Command-Line Interface (CLI)

| Command / Flag | Description |
| :--- | :--- |
| `<target_path> <source_path>` | The main arguments. Paths with spaces must be supported (via quotes). |
| `--setup-test-data` | Generates a test dataset (folders and files) for demonstration purposes. |
| `--help` / `-h` | Displays the help screen with command descriptions and the application version. |
| `--verbose` | Enables verbose error logging mode with full stack traces. |

**Exit Codes:**
- `0`: Successful execution.
- `1`: Error (invalid arguments, operation failure, user cancellation).

---

## 5. Error Handling Strategy

- **Fatal Errors:** Errors that prevent the core logic from executing (invalid arguments, inaccessible source/target directory) must lead to immediate program termination with exit code `1`.
- **Non-Fatal Errors:** Errors that occur while processing an individual file (e.g., no permission to move) must be logged, after which the program should continue processing the next file.
- **Verbose Mode:** When the `--verbose` flag is present, all caught exceptions must be fully printed to the console.

---

## 6. Testing Strategy

- **Unit Tests:**
  - The core business logic must be covered by tests in complete isolation from the outside world.
  - All dependencies (UI, file system) must be replaced with mocks/stubs.

- **Integration Tests:**
  - There must be end-to-end tests that run the application with different command-line arguments.
  - These tests must emulate user input, check console output, and perform real file operations in a temporary, isolated directory.

---

## 7. CI/CD and Release Process

The build and publication process must be fully automated using a CI/CD pipeline (e.g., GitHub Actions).

### 7.1. Pipeline Specification

- **Trigger:** Runs upon the creation and push of a git tag that corresponds to semantic versioning (e.g., `v1.0.0`, `v1.2.3`).
- **Key Stages:**
  1.  Clone the code.
  2.  Set up the build environment (install .NET SDK).
  3.  Run all tests (unit and integration). The build is aborted if tests fail.
  4.  Build and publish cross-platform executables (Windows, Linux, macOS). Debug symbols (`.pdb`) must be excluded from the release build.
  5.  Package artifacts into archives (`.zip` for Windows, `.tar.gz` for others). Archive names must include the platform and version (e.g., `app-win-x64-v1.0.0.zip`).
  6.  Create a public release on the hosting platform (e.g., GitHub Releases).
  7.  Automatically generate release notes based on the commit history.
  8.  Attach the created archives to the release.

### 7.2. Helper Utilities

- For convenience in launching the release process locally, a helper script can be provided to automate the steps of creating a commit, tagging, and pushing to the remote repository.