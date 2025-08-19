<div align="center">

# 📂 File Sorter Utility

**A smart command-line tool to automatically organize your files into categorized folders.**

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET Version](https://img.shields.io/badge/.NET-9.0-purple.svg)

</div>

---

## ✨ Features

*   🎯 **Intelligent Name Parsing**: Automatically extracts one or more names from target folder names (e.g., "John Smith" or "Anna Karenina, Leo Tolstoy").
*   🔍 **Recursive File Scanning**: Deep-scans a source directory and all its subdirectories to find every file that needs sorting.
*   🤖 **Smart Matching Logic**: Matches files to target folders by checking if the filename contains any associated name variants.
*   🛡️ **Pre-run Safety Checks**: Detects duplicate names across target folders and asks for user confirmation before proceeding, preventing incorrect sorting.
*   🤝 **Interactive Conflict Resolution**: If a file already exists at the destination, it provides clear choices:
    *   **Rename**: Move the new file with a suffix (e.g., `file_duplicate_001.txt`).
    *   **Skip**: Do not move the file.
    *   **Rename All**: Automate renaming for all future conflicts in the session.
*   🤔 **Ambiguity Resolution**: If a file matches multiple folders, it helps you decide:
    *   Choose the correct destination from a smartly sorted list.
    *   Skip the current file or all future ambiguous files.
*   ℹ️ **Built-in Help**: Get a quick overview of commands and options right from the terminal with the `-h` flag.
*   🧪 **Built-in Test Data Generator**: Quickly set up a test environment with a single command.

## 🚀 How It Works

The utility follows a simple and powerful workflow to organize your digital clutter.

```mermaid
graph TD
    A[Source Directory 📚] --> B{File Sorter Utility};
    C[Target Directories 🗂️] --> B;
    B -- Scans & Matches --> D{File: 'Report for John Smith.pdf'};
    D -- Moves to --> E[Target Folder: 'John Smith'];
```

## 💡 Use Cases

| **For...** | **Use Case** | **Example** |
| :--- | :--- | :--- |
| **Accountants & Lawyers** | Sort client documents into individual folders. | `Invoice - John Doe.pdf` → `\John Doe\` |
| **Photographers** | Organize photoshoots by client or event name. | `Emily & David Wedding-001.jpg` → `\Emily & David Wedding\` |
| **Researchers** | Arrange papers and data by author or study. | `Analysis by Dr. Smith.xlsx` → `\Dr. Smith\` |
| **Personal Use** | Clean up a messy "Downloads" folder. | `Vacation Photo (Anna).png` → `\Anna\` |

## 📋 Prerequisites

*   .NET 9 SDK or later.

## 🔗 Dependencies

This project has **no external third-party dependencies**. It relies exclusively on the built-in libraries provided with the .NET 9 SDK.

## 🛠️ Usage

This section covers two ways to use the utility: running a pre-compiled release and building from source.

### ⌨️ Command-line Options

| Option | Alias | Description |
| :--- | :--- | :--- |
| `<target_path> <source_path>` | | The main arguments specifying the target and source directories. |
| `--setup-test-data` | | Generates a set of test folders and files. |
| `--help` | `-h` | Displays the help screen with usage information. |

### ✅ 1. Using a Pre-compiled Release (Recommended)

This is the easiest way for most users.

1.  Go to the project's **Releases** page.
2.  Download the latest version for your operating system (e.g., `file-sorter-win-x64.zip`).
3.  Extract the archive to a convenient location.
4.  Open a terminal in that folder and run the executable, passing the required arguments.

**On Windows:**
```bash
.\file_sort.exe "<path_to_target_folders>" "<path_to_source_files>"
```

**On Linux/macOS:**
```bash
./file_sort "<path_to_target_folders>" "<path_to_source_files>"
```

### 🏗️ 2. Building from Source

This method is for developers who want to modify the code.

1.  Clone the repository:
    ```bash
    git clone https://github.com/shtormish/file_sort.git
    cd file_sort
    ```
2.  Build the application. This command will create an executable in the `bin/Release/net9.0/` directory:
    ```bash
    dotnet build -c Release
    ```
3.  Run the compiled application:

    **On Windows:**
    ```bash
    .\bin\Release\net9.0\file_sort.exe "<path_to_target_folders>" "<path_to_source_files>"
    ```

    **On Linux/macOS:**
    ```bash
    ./bin/Release/net9.0/file_sort "<path_to_target_folders>" "<path_to_source_files>"
    ```

    Alternatively, for quick development and testing, you can use the `dotnet run` command:
    ```bash
    dotnet run -- "<path_to_target_folders>" "<path_to_source_files>"
    ```

### 🎲 Generating Test Data

To create the `temp1` (targets) and `temp2` (sources) directories for testing, run the application with the `--setup-test-data` flag.

> **Note:** The double dash `--` is necessary to ensure the argument is passed to the application and not interpreted by the `dotnet` command itself.

```bash
dotnet run -- --setup-test-data
```

## 🐛 Debugging

*   **Paths**: Ensure that all directory paths are correct. If a path contains spaces, it must be enclosed in double quotes (`"`).
*   **Console Output**: The application provides detailed, real-time feedback in the console for every operation: scanning, moving, skipping, and any conflicts or ambiguities encountered.
*   **Error Messages**: Clear error messages are printed for common issues such as missing directories or file system access errors (e.g., insufficient permissions).

## ⚠️ Disclaimer

Please note that this project was brought to life with the help of Gemini Code Assist, which served as a valuable partner in brainstorming solutions and writing code. While this tool is functional, it should be considered a proof-of-concept and has not been subjected to exhaustive testing across all possible scenarios and edge cases. Therefore, it is strongly recommended that you do not use this utility for sorting critical, sensitive, or irreplaceable data without first thoroughly reviewing the code and conducting your own tests. Think of it as a helpful assistant for casual organization, not a robust archival system for mission-critical files.
