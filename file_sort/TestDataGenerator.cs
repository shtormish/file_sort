using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

/// <summary>
/// Generates a set of test directories and files for testing the application.
/// </summary>
public static class TestDataGenerator
{
    public static void Setup(IFileSystem fileSystem)
    {
        var random = new Random();

        // Clean up previous runs
        if (fileSystem.Directory.Exists("temp1")) fileSystem.Directory.Delete("temp1", true);
        if (fileSystem.Directory.Exists("temp2")) fileSystem.Directory.Delete("temp2", true);

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
        fileSystem.Directory.CreateDirectory("temp1");
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
            fileSystem.Directory.CreateDirectory(fileSystem.Path.Combine("temp1", folderName));
        }

        // --- 2. Create `temp2` with 3000 files in a nested structure ---
        Console.WriteLine("Creating 'temp2' directory with a nested structure and 3000 files...");
        fileSystem.Directory.CreateDirectory("temp2");
        var allSourceDirs = new List<string> { "temp2" };

        // Create a random directory structure up to 3 levels deep
        for (int i = 0; i < 10; i++)
        {
            var level1 = fileSystem.Path.Combine("temp2", $"Department_{i + 1}");
            fileSystem.Directory.CreateDirectory(level1);
            allSourceDirs.Add(level1);

            if (random.Next(2) == 0) continue;
            var level2 = fileSystem.Path.Combine(level1, $"Group_{random.Next(100)}");
            fileSystem.Directory.CreateDirectory(level2);
            allSourceDirs.Add(level2);

            if (random.Next(2) == 0) continue;
            var level3 = fileSystem.Path.Combine(level2, $"Team_{random.Next(100)}");
            fileSystem.Directory.CreateDirectory(level3);
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
                fileName = random.Next(2) == 0
                    ? $"{name1} and {name2} vs {namesForFiles[random.Next(namesForFiles.Count)]} meeting"
                    : $"Summary for {name1} and {name2}";
            }
            else // 90% with a single name
            {
                var name = namesForFiles[random.Next(namesForFiles.Count)];
                var word = fileWords[random.Next(fileWords.Count)];
                fileName = $"{word} from {name} date {random.Next(1, 29)}-{random.Next(1, 13)}-2024";
            }

            var finalFileName = fileName + fileExtensions[random.Next(fileExtensions.Count)];
            var targetDir = allSourceDirs[random.Next(allSourceDirs.Count)];
            var fullPath = fileSystem.Path.Combine(targetDir, finalFileName);

            try
            {
                fileSystem.File.WriteAllText(fullPath, $"This is a test file: {finalFileName}");
            }
            catch (Exception ex)
            {
                // Handle cases where filename becomes too long or contains invalid chars
                ConsoleUI.LogError($"Could not create file '{fullPath}'. Reason: {ex.Message}. Skipping.");
            }
        }
    }
}