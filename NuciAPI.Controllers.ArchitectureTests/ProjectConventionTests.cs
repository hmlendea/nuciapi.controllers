using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;

namespace NuciAPI.Controllers.ArchitectureTests
{
    [TestFixture]
    public class ProjectConventionTests
    {
        private const string TargetFramework = "net10.0";
        private const string DisabledImplicitUsings = "disable";
        private const string EnabledNullable = "enable";
        private const string LatestLanguageVersion = "latest";
        private const string PreviewLanguageVersion = "preview";

        private static readonly string[] ProhibitedPackageReferences =
        [
            "Microsoft.EntityFrameworkCore",
            "Microsoft.EntityFrameworkCore.SqlServer",
            "AWSSDK.DynamoDBv2",
            "Dapper",
            "MediatR"
        ];

        [Test]
        public void AllProjectsMustTargetTheRequiredFramework()
        {
            List<string> failures = GetProjectFiles()
                .Where(file => !string.Equals(ReadProperty(file, "TargetFramework"), TargetFramework, StringComparison.Ordinal))
                .Select(file => $"{Path.GetFileName(Path.GetDirectoryName(file))} must target {TargetFramework}.")
                .ToList();

            AssertNoFailures(failures, "target framework");
        }

        [Test]
        public void AllProjectsMustDisableImplicitUsings()
        {
            List<string> failures = GetProjectFiles()
                .Where(file => !string.Equals(ReadProperty(file, "ImplicitUsings"), DisabledImplicitUsings, StringComparison.Ordinal))
                .Select(file => $"{Path.GetFileName(Path.GetDirectoryName(file))} must disable implicit usings.")
                .ToList();

            AssertNoFailures(failures, "implicit usings");
        }

        [Test]
        public void AllProjectsMustEnableNullableReferenceTypes()
        {
            List<string> failures = GetProjectFiles()
                .Where(file => !string.Equals(ReadProperty(file, "Nullable"), EnabledNullable, StringComparison.Ordinal))
                .Select(file => $"{Path.GetFileName(Path.GetDirectoryName(file))} must enable nullable reference types.")
                .ToList();

            AssertNoFailures(failures, "nullable reference types");
        }

        [Test]
        public void AllProjectsMustUseTheLatestStableLanguageVersion()
        {
            List<string> failures = [];

            foreach (string file in GetProjectFiles())
            {
                string? languageVersion = ReadProperty(file, "LangVersion");
                string projectName = Path.GetFileName(Path.GetDirectoryName(file));

                if (string.Equals(languageVersion, PreviewLanguageVersion, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"{projectName} must not use preview language features.");
                }

                if (!string.Equals(languageVersion, LatestLanguageVersion, StringComparison.Ordinal))
                {
                    failures.Add($"{projectName} must declare the latest language version.");
                }
            }

            AssertNoFailures(failures, "stable language version");
        }

        [Test]
        public void PackageReferencesMustBeAlphabetised()
        {
            List<string> failures = [];

            foreach (string file in GetProjectFiles())
            {
                List<string> packageNames = ReadElements(file, "PackageReference")
                    .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                    .ToList();

                if (!packageNames.SequenceEqual(packageNames.OrderBy(name => name, StringComparer.Ordinal)))
                {
                    failures.Add($"{Path.GetFileName(Path.GetDirectoryName(file))} must alphabetise package references.");
                }
            }

            AssertNoFailures(failures, "alphabetised package references");
        }

        [Test]
        public void ProductionProjectMustBePackable()
            => Assert.That(
                ReadProperty(Path.Combine(RepositoryPaths.ProductionProject, "NuciAPI.Controllers.csproj"), "IsPackable"),
                Is.Not.EqualTo("false"),
                "The production library is published as a NuGet package and must remain packable.");

        [Test]
        public void TestProjectsMustNotBePackable()
        {
            List<string> failures = [];

            foreach (string project in new[] { RepositoryPaths.UnitTestsProject, RepositoryPaths.ArchitectureTestsProject })
            {
                string file = Path.Combine(project, Path.GetFileName(project) + ".csproj");

                if (!string.Equals(ReadProperty(file, "IsPackable"), "false", StringComparison.Ordinal))
                {
                    failures.Add($"{Path.GetFileName(project)} must not be packable.");
                }
            }

            AssertNoFailures(failures, "packable test projects");
        }

        [Test]
        public void TestProjectsMustDeclareTestProjectFlags()
        {
            List<string> failures = [];

            foreach (string project in new[] { RepositoryPaths.UnitTestsProject, RepositoryPaths.ArchitectureTestsProject })
            {
                string file = Path.Combine(project, Path.GetFileName(project) + ".csproj");
                string projectName = Path.GetFileName(project);

                if (!string.Equals(ReadProperty(file, "IsTestProject"), "true", StringComparison.Ordinal))
                {
                    failures.Add($"{projectName} must declare IsTestProject.");
                }

                if (!string.Equals(ReadProperty(file, "ExcludeFromCodeCoverage"), "true", StringComparison.Ordinal))
                {
                    failures.Add($"{projectName} must declare ExcludeFromCodeCoverage.");
                }
            }

            AssertNoFailures(failures, "test project flags");
        }

        [Test]
        public void ProductionProjectsMustNotDeclareInternalsVisibleTo()
        {
            string projectFile = Path.Combine(RepositoryPaths.ProductionProject, "NuciAPI.Controllers.csproj");
            string content = File.ReadAllText(projectFile);

            Assert.That(
                content.Contains("InternalsVisibleTo", StringComparison.OrdinalIgnoreCase),
                Is.False,
                "The production project must not expose internals to test projects.");

            List<string> assemblyAttributes = ProductionSourceFiles
                .Enumerate(RepositoryPaths.ProductionProject)
                .Where(file => File.ReadAllText(file).Contains("InternalsVisibleTo", StringComparison.Ordinal))
                .Select(Path.GetFileName)
                .ToList();

            AssertNoFailures(assemblyAttributes, "no InternalsVisibleTo attributes");
        }

        [Test]
        public void ProductionProjectMustNotReferenceTestProjects()
        {
            List<string> projectReferences = ReadElements(
                    Path.Combine(RepositoryPaths.ProductionProject, "NuciAPI.Controllers.csproj"),
                    "ProjectReference")
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                .ToList();

            AssertNoFailures(
                projectReferences.Where(reference => reference.Contains("Tests", StringComparison.Ordinal)).ToList(),
                "production project must not reference test projects");
        }

        [Test]
        public void ProductionProjectMustNotReferencePersistenceOrMediatorPackages()
        {
            List<string> packageReferences = ReadElements(
                    Path.Combine(RepositoryPaths.ProductionProject, "NuciAPI.Controllers.csproj"),
                    "PackageReference")
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                .ToList();

            List<string> violations = packageReferences
                .Where(reference => ProhibitedPackageReferences.Any(
                    prohibited => reference.StartsWith(prohibited, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            AssertNoFailures(violations, "prohibited persistence or mediator package references");
        }

        [Test]
        public void ArchitectureTestProjectMustUseTheRequiredFrameworks()
        {
            List<string> packageReferences = ReadElements(
                    Path.Combine(RepositoryPaths.ArchitectureTestsProject, "NuciAPI.Controllers.ArchitectureTests.csproj"),
                    "PackageReference")
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                .ToList();

            List<string> failures = [];

            if (!packageReferences.Contains("TngTech.ArchUnitNET", StringComparer.Ordinal))
            {
                failures.Add("The architecture test project must reference TngTech.ArchUnitNET.");
            }

            if (!packageReferences.Contains("TngTech.ArchUnitNET.NUnit", StringComparer.Ordinal))
            {
                failures.Add("The architecture test project must reference TngTech.ArchUnitNET.NUnit.");
            }

            if (!packageReferences.Contains("NUnit", StringComparer.Ordinal))
            {
                failures.Add("The architecture test project must reference NUnit.");
            }

            AssertNoFailures(failures, "architecture test framework references");
        }

        private static IEnumerable<string> GetProjectFiles()
            => Directory.GetFiles(RepositoryPaths.RepositoryRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(file => !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .OrderBy(file => file, StringComparer.Ordinal);

        private static string? ReadProperty(string projectFile, string propertyName)
            => ReadElements(projectFile, propertyName).FirstOrDefault()?.Value;

        private static IEnumerable<XElement> ReadElements(string projectFile, string elementName)
            => XDocument.Load(projectFile)
                .Descendants()
                .Where(element => string.Equals(element.Name.LocalName, elementName, StringComparison.Ordinal));

        private static void AssertNoFailures(List<string> failures, string convention)
        {
            if (failures.Count == 0)
            {
                return;
            }

            Assert.Fail(
                $"Violated project convention ({convention}):{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
        }
    }
}