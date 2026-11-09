using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis.CSharp.Syntax;

using NUnit.Framework;

namespace NuciAPI.Controllers.ArchitectureTests
{
    [TestFixture]
    public class SourceFileConventionTests
    {
        [Test]
        public void CheckedProjectsMustProvideEligibleSourceFiles()
            => Assert.That(
                AllCheckedFiles().Count(),
                Is.GreaterThan(0),
                "No eligible source files were found, so the source conventions would be vacuous.");

        [Test]
        public void EachFileMustDeclareExactlyOneType()
        {
            List<string> failures = [];

            foreach (string file in AllCheckedFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);
                string fileName = Path.GetFileNameWithoutExtension(file);

                if (SourceSyntax.ContainsTopLevelStatements(root))
                {
                    failures.Add($"'{fileName}.cs' uses top-level statements; an explicit type is required.");
                }

                int typeCount = SourceSyntax.CountTypeDeclarations(root);

                if (typeCount != 1)
                {
                    failures.Add($"'{fileName}.cs' declares {typeCount} types; exactly one is required.");
                }
            }

            AssertNoFailures(failures, "one declared type per file");
        }

        [Test]
        public void EachFileNameMustMatchItsDeclaredTypeName()
        {
            List<string> failures = [];

            foreach (string file in AllCheckedFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);
                string fileName = Path.GetFileNameWithoutExtension(file);
                TypeDeclarationSyntax? declaredType = SourceSyntax.EnumerateTypeDeclarations(root).FirstOrDefault();

                if (declaredType is null)
                {
                    continue;
                }

                string typeName = declaredType.Identifier.ValueText;

                if (!string.Equals(typeName, fileName, StringComparison.Ordinal))
                {
                    failures.Add($"'{fileName}.cs' declares type '{typeName}'; the names must match exactly.");
                }
            }

            AssertNoFailures(failures, "filename and type-name correspondence");
        }

        [Test]
        public void EachFileMustUseABlockNamespaceMatchingItsFolder()
        {
            List<string> failures = [];

            foreach (string file in AllCheckedFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);
                string fileName = Path.GetFileNameWithoutExtension(file);

                if (root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().Any())
                {
                    failures.Add($"'{fileName}.cs' uses a file-scoped namespace; a block namespace is required.");
                }

                List<NamespaceDeclarationSyntax> namespaces = root.DescendantNodes()
                    .OfType<NamespaceDeclarationSyntax>()
                    .ToList();

                if (namespaces.Count != 1)
                {
                    failures.Add($"'{fileName}.cs' declares {namespaces.Count} namespaces; exactly one is required.");
                    continue;
                }

                string expectedNamespace = SourceSyntax.BuildExpectedNamespace(file);
                string declaredNamespace = namespaces[0].Name.ToString();

                if (!string.Equals(declaredNamespace, expectedNamespace, StringComparison.Ordinal))
                {
                    failures.Add(
                        $"'{fileName}.cs' declares namespace '{declaredNamespace}' but its folder implies '{expectedNamespace}'.");
                }
            }

            AssertNoFailures(failures, "namespace and folder correspondence");
        }

        [Test]
        public void EachFileMustSeparateUsingsFromTheNamespaceWithOneBlankLine()
        {
            List<string> failures = [];

            foreach (string file in AllCheckedFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);
                string fileName = Path.GetFileNameWithoutExtension(file);

                if (root.Usings.Count == 0)
                {
                    continue;
                }

                int lastUsingLine = root.Usings.Last().GetLocation().GetLineSpan().EndLinePosition.Line;
                int namespaceLine = root.Members.First().GetLocation().GetLineSpan().StartLinePosition.Line;

                if (namespaceLine - lastUsingLine != 2)
                {
                    failures.Add(
                        $"'{fileName}.cs' must place exactly one blank line between the final using and the namespace.");
                }
            }

            AssertNoFailures(failures, "using/namespace separation");
        }

        [Test]
        public void EachFileMustGroupAndAlphabetiseItsUsings()
        {
            List<string> failures = [];

            foreach (string file in AllCheckedFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);
                string fileName = Path.GetFileNameWithoutExtension(file);

                List<List<string>> groups = UsingDirectiveRules.GroupUsings(root).ToList();
                int previousRank = -1;

                foreach (List<string> group in groups)
                {
                    if (!group.SequenceEqual(group.OrderBy(value => value, StringComparer.Ordinal)))
                    {
                        failures.Add($"'{fileName}.cs' has a using group that is not alphabetised.");
                    }

                    int rank = UsingDirectiveRules.GetRank(group[0]);

                    if (rank < previousRank)
                    {
                        failures.Add($"'{fileName}.cs' has using groups in the wrong order.");
                    }

                    previousRank = rank;

                    if (group.Any(value => UsingDirectiveRules.GetRank(value) != rank))
                    {
                        failures.Add($"'{fileName}.cs' mixes different using categories in one group.");
                    }
                }
            }

            AssertNoFailures(failures, "grouped and alphabetised usings");
        }

        [Test]
        public void EachDeclarationMustExplicitlyStateItsAccessibility()
        {
            List<string> failures = [];

            foreach (string file in AllCheckedFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);
                string fileName = Path.GetFileNameWithoutExtension(file);
                List<MemberDeclarationSyntax> members = SourceSyntax
                    .EnumerateTypeDeclarations(root)
                    .SelectMany(SourceSyntax.EnumerateMembers)
                    .ToList();

                foreach (MemberDeclarationSyntax member in members.Where(SourceSyntax.RequiresAccessibility))
                {
                    if (SourceSyntax.HasExplicitAccessibility(member))
                    {
                        continue;
                    }

                    failures.Add(
                        $"'{fileName}.cs' declares '{member.Kind()}' without an explicit accessibility modifier.");
                }
            }

            AssertNoFailures(failures, "explicit accessibility");
        }

        [Test]
        public void EachFileMustBeFreeOfNullableDirectivesAndRegions()
        {
            List<string> failures = [];

            foreach (string file in AllCheckedFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);
                string fileName = Path.GetFileNameWithoutExtension(file);

                if (root.DescendantNodes().OfType<NullableDirectiveTriviaSyntax>().Any())
                {
                    failures.Add($"'{fileName}.cs' uses a nullable directive.");
                }

                if (root.DescendantNodes().OfType<RegionDirectiveTriviaSyntax>().Any())
                {
                    failures.Add($"'{fileName}.cs' uses a region directive.");
                }
            }

            AssertNoFailures(failures, "no nullable or region directives");
        }

        private static IEnumerable<string> AllCheckedFiles()
            => RepositoryPaths.CheckedSourceProjects.SelectMany(ProductionSourceFiles.Enumerate);

        private static void AssertNoFailures(List<string> failures, string convention)
        {
            if (failures.Count == 0)
            {
                return;
            }

            Assert.Fail(
                $"Violated source-file convention ({convention}):{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
        }
    }
}
