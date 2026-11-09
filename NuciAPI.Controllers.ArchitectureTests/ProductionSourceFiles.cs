using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuciAPI.Controllers.ArchitectureTests
{
    internal static class ProductionSourceFiles
    {
        private static readonly string[] ExcludedDirectories = ["bin", "obj"];

        public static IEnumerable<string> Enumerate(string projectDirectory)
        {
            if (!Directory.Exists(projectDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"Source project directory does not exist: '{projectDirectory}'.");
            }

            List<string> files = Directory
                .GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(file => !IsInExcludedDirectory(projectDirectory, file))
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToList();

            if (files.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No eligible C# source files were found in '{projectDirectory}'.");
            }

            return files;
        }

        private static bool IsInExcludedDirectory(string projectDirectory, string file)
        {
            string relativePath = Path.GetRelativePath(projectDirectory, file);
            string[] segments = relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

            return segments.Any(segment => ExcludedDirectories.Contains(segment, StringComparer.Ordinal));
        }
    }
}