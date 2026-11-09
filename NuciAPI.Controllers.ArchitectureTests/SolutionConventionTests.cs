using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;

namespace NuciAPI.Controllers.ArchitectureTests
{
    [TestFixture]
    public class SolutionConventionTests
    {
        [Test]
        public void SolutionFileMustExistAsSlnx()
        {
            Assert.That(
                File.Exists(RepositoryPaths.SolutionFile),
                Is.True,
                "The repository must be described by a .slnx solution file.");

            Assert.That(
                Directory.GetFiles(RepositoryPaths.RepositoryRoot, "*.sln", SearchOption.TopDirectoryOnly),
                Is.Empty,
                "Legacy .sln files must not be present alongside the .slnx solution.");
        }

        [Test]
        public void SolutionMustBeSdkStyleWithoutDeclarationOrAttributes()
        {
            string content = File.ReadAllText(RepositoryPaths.SolutionFile);
            XDocument document = XDocument.Parse(content);

            Assert.That(content.Contains("<?xml", StringComparison.Ordinal), Is.False,
                "The .slnx file must not contain an XML declaration.");

            Assert.That(document.Root, Is.Not.Null, "The .slnx file must contain a root element.");
            Assert.That(document.Root!.Name.LocalName, Is.EqualTo("Solution"),
                "The .slnx root element must be Solution.");

            Assert.That(document.Root!.Attributes(), Is.Empty,
                "The .slnx Solution element must not declare attributes.");
        }

        [Test]
        public void EachSolutionProjectMustReferenceOnlyAPath()
        {
            List<string> failures = [];

            foreach (XElement project in ReadProjects())
            {
                List<XAttribute> unexpectedAttributes = project.Attributes()
                    .Where(attribute => !string.Equals(attribute.Name.LocalName, "Path", StringComparison.Ordinal))
                    .ToList();

                if (unexpectedAttributes.Count > 0)
                {
                    failures.Add($"Project element '{project.Attribute("Path")?.Value}' declares unsupported attributes.");
                }
            }

            AssertNoFailures(failures, "solution project elements");
        }

        [Test]
        public void EveryProjectOnDiskMustBeInTheSolution()
        {
            List<string> solutionPaths = ReadProjects()
                .Select(project => NormalisePath(project.Attribute("Path")?.Value ?? string.Empty))
                .ToList();

            List<string> projectPaths = Directory
                .GetFiles(RepositoryPaths.RepositoryRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(file => !IsBuildOutput(file))
                .Select(file => NormalisePath(Path.GetRelativePath(RepositoryPaths.RepositoryRoot, file)))
                .ToList();

            AssertNoFailures(
                projectPaths.Where(path => !solutionPaths.Contains(path, StringComparer.Ordinal)).ToList(),
                "every project is listed in the solution");

            AssertNoFailures(
                solutionPaths.Where(path => !projectPaths.Contains(path, StringComparer.Ordinal)).ToList(),
                "every solution entry exists on disk");
        }

        [Test]
        public void SolutionMustEndWithTheRequiredProjectOrder()
        {
            List<string> paths = ReadProjects()
                .Select(project => NormalisePath(project.Attribute("Path")?.Value ?? string.Empty))
                .ToList();

            string mainProject = NormalisePath(Path.Combine("NuciAPI.Controllers", "NuciAPI.Controllers.csproj"));

            Assert.That(paths.FirstOrDefault(), Is.EqualTo(mainProject),
                "The main project must be listed first in the solution.");

            List<string> testProjects = paths
                .Where(path => path.Contains("Tests", StringComparison.Ordinal))
                .ToList();

            List<string> sourceProjects = paths
                .Where(path => !path.Contains("Tests", StringComparison.Ordinal))
                .ToList();

            Assert.That(paths, Is.EqualTo(sourceProjects.Concat(testProjects).ToList()),
                "Test projects must be listed after all source projects in the solution.");
        }

        private static IEnumerable<XElement> ReadProjects()
            => XDocument.Load(RepositoryPaths.SolutionFile)
                .Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "Project", StringComparison.Ordinal));

        private static string NormalisePath(string path)
            => path.Replace('\\', '/');

        private static bool IsBuildOutput(string file)
            => file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal);

        private static void AssertNoFailures(List<string> failures, string convention)
        {
            if (failures.Count == 0)
            {
                return;
            }

            Assert.Fail(
                $"Violated solution convention ({convention}):{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
        }
    }
}