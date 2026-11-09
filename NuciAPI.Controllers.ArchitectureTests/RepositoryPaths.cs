using System;
using System.Collections.Generic;
using System.IO;

namespace NuciAPI.Controllers.ArchitectureTests
{
    internal static class RepositoryPaths
    {
        public static string RepositoryRoot => FindRepositoryRoot();

        public static string ProductionProject
            => Path.Combine(RepositoryRoot, "NuciAPI.Controllers");

        public static string UnitTestsProject
            => Path.Combine(RepositoryRoot, "NuciAPI.Controllers.UnitTests");

        public static string ArchitectureTestsProject
            => Path.Combine(RepositoryRoot, "NuciAPI.Controllers.ArchitectureTests");

        public static string SolutionFile
            => Path.Combine(RepositoryRoot, "NuciAPI.Controllers.slnx");

        public static IEnumerable<string> CheckedSourceProjects
            => [ProductionProject, UnitTestsProject, ArchitectureTestsProject];

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? current = new(AppContext.BaseDirectory);

            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "NuciAPI.Controllers.slnx")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the repository root containing NuciAPI.Controllers.slnx.");
        }
    }
}
