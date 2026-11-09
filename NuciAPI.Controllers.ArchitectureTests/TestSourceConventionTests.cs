using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NUnit.Framework;

namespace NuciAPI.Controllers.ArchitectureTests
{
    [TestFixture]
    public class TestSourceConventionTests
    {
        private static readonly string[] ProhibitedAssertions =
        [
            "AreEqual",
            "AreNotEqual",
            "IsTrue",
            "IsFalse",
            "IsNull",
            "IsNotNull",
            "Greater",
            "Less"
        ];

        private static readonly string[] ProhibitedFrameworks =
        [
            "xunit",
            "Xunit",
            "xUnit"
        ];

        [Test]
        public void UnitTestFixturesMustBeNamedAfterTheirSubject()
        {
            List<string> failures = UnitTestTypeDeclarations()
                .OfType<ClassDeclarationSyntax>()
                .Where(declaration => declaration.AttributeLists.Any(list => list.ToString().Contains("TestFixture", StringComparison.Ordinal)))
                .Where(declaration => !declaration.Identifier.ValueText.EndsWith("Tests", StringComparison.Ordinal))
                .Select(declaration => $"Fixture '{declaration.Identifier.ValueText}' must be named Subject + 'Tests'.")
                .ToList();

            AssertNoFailures(failures, "unit test fixture naming");
        }

        [Test]
        public void UnitTestMethodsMustFollowTheGivenWhenThenNamingPattern()
        {
            List<string> failures = UnitTestMethods()
                .Where(method => !IsGivenWhenThenName(method.Identifier.ValueText))
                .Select(method => $"'{method.Identifier.ValueText}' must follow Given[x]_When[y]_Then[z].")
                .ToList();

            AssertNoFailures(failures, "unit test method naming");
        }

        [Test]
        public void UnitTestMethodsMustDeclareATestAttribute()
        {
            List<string> failures = UnitTestMethods()
                .Where(method => !HasAttribute(method, "Test") && !HasAttribute(method, "TestCase"))
                .Select(method => $"'{method.Identifier.ValueText}' must be marked with [Test] or [TestCase].")
                .ToList();

            AssertNoFailures(failures, "unit test attributes");
        }

        [Test]
        public void UnitTestsMustUseTheConstraintAssertionModel()
        {
            List<string> failures = [];

            foreach (string file in UnitTestFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);
                string fileName = Path.GetFileNameWithoutExtension(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(invocation => IsProhibitedAssertion(invocation))
                    .Select(invocation => $"'{fileName}.cs' uses a legacy NUnit assertion; use Assert.That with constraints."));
            }

            AssertNoFailures(failures, "constraint assertion model");
        }

        [Test]
        public void ParameterisedUnitTestsMustDeclareAtLeastTwoCases()
        {
            List<string> failures = [];

            foreach (ClassDeclarationSyntax fixture in UnitTestTypeDeclarations().OfType<ClassDeclarationSyntax>())
            {
                foreach (MethodDeclarationSyntax method in fixture.Members.OfType<MethodDeclarationSyntax>())
                {
                    int testCaseCount = method.AttributeLists
                        .SelectMany(list => list.Attributes)
                        .Count(attribute => attribute.Name.ToString().EndsWith("TestCase", StringComparison.Ordinal));

                    if (testCaseCount == 1)
                    {
                        failures.Add($"'{fixture.Identifier.ValueText}.{method.Identifier.ValueText}' declares a single test case.");
                    }
                }
            }

            AssertNoFailures(failures, "parameterised test case count");
        }

        [Test]
        public void UnitTestsMustNotUseReflectionToReachNonPublicCode()
        {
            List<string> failures = [];

            foreach (string file in UnitTestFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);
                string fileName = Path.GetFileNameWithoutExtension(file);

                bool usesNonPublicBinding = root.DescendantNodes()
                    .OfType<MemberAccessExpressionSyntax>()
                    .Any(access => string.Equals(access.Name.Identifier.ValueText, "NonPublic", StringComparison.Ordinal));

                if (usesNonPublicBinding)
                {
                    failures.Add($"'{fileName}.cs' uses reflection with NonPublic binding flags.");
                }
            }

            AssertNoFailures(failures, "no reflection over non-public code");
        }

        [Test]
        public void TestProjectsMustNotAdoptUnapprovedFrameworks()
        {
            List<string> failures = [];

            foreach (string project in new[] { RepositoryPaths.UnitTestsProject, RepositoryPaths.ArchitectureTestsProject })
            {
                string file = Path.Combine(project, Path.GetFileName(project) + ".csproj");
                string content = File.ReadAllText(file);

                failures.AddRange(ProhibitedFrameworks
                    .Where(framework => content.Contains(framework, StringComparison.Ordinal))
                    .Select(framework => $"{Path.GetFileName(project)} references the unapproved '{framework}' framework."));
            }

            AssertNoFailures(failures, "approved test frameworks");
        }

        [Test]
        public void IntegrationTestsMustNotLiveInProductionOrUnitTestProjects()
        {
            List<string> failures = ProductionSourceFiles
                .Enumerate(RepositoryPaths.ProductionProject)
                .Concat(ProductionSourceFiles.Enumerate(RepositoryPaths.UnitTestsProject))
                .SelectMany(file => SourceSyntax.ParseCompilationUnit(file).DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Where(declaration => declaration.Identifier.ValueText.EndsWith("IntegrationTests", StringComparison.Ordinal))
                    .Select(declaration => $"'{declaration.Identifier.ValueText}' must live in a dedicated integration-test project."))
                .ToList();

            AssertNoFailures(failures, "integration test placement");
        }

        [Test]
        public void ProductionProjectMustNotReferenceInternalsToTests()
        {
            string projectFile = Path.Combine(RepositoryPaths.ProductionProject, "NuciAPI.Controllers.csproj");

            Assert.That(
                File.ReadAllText(projectFile).Contains("InternalsVisibleTo", StringComparison.Ordinal),
                Is.False,
                "Tests must not require InternalsVisibleTo in the production project.");
        }

        [Test]
        public void UnitTestMethodsMustKeepWithinTheParameterLimit()
        {
            List<string> failures = UnitTestMethods()
                .Where(method => method.ParameterList.Parameters.Count > 7)
                .Select(method => $"'{method.Identifier.ValueText}' exceeds 7 parameters.")
                .ToList();

            AssertNoFailures(failures, "test parameter limit");
        }

        [Test]
        public void ReusableExpectedValuesMustNotBePassedAsInlineCollectionExpressions()
        {
            List<string> failures = [];

            foreach (string file in UnitTestFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);
                string fileName = Path.GetFileNameWithoutExtension(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(invocation => invocation.Expression.ToString().StartsWith("Assert.That", StringComparison.Ordinal))
                    .Where(invocation => invocation.ArgumentList.Arguments
                        .Any(argument => argument.Expression is CollectionExpressionSyntax))
                    .Select(invocation => $"'{fileName}.cs' passes an inline collection expression to an assertion; use a static readonly field."));
            }

            AssertNoFailures(failures, "no inline collection expressions in assertions");
        }

        private static IEnumerable<string> TestFiles()
            => ProductionSourceFiles.Enumerate(RepositoryPaths.UnitTestsProject)
                .Concat(ProductionSourceFiles.Enumerate(RepositoryPaths.ArchitectureTestsProject));

        private static IEnumerable<string> UnitTestFiles()
            => File.Exists(Path.Combine(RepositoryPaths.UnitTestsProject, "NuciAPI.Controllers.UnitTests.csproj"))
                ? ProductionSourceFiles.Enumerate(RepositoryPaths.UnitTestsProject)
                : [];

        private static IEnumerable<TypeDeclarationSyntax> TestTypeDeclarations()
            => SourceSyntax.EnumerateTypeDeclarationsIn(TestFiles());

        private static IEnumerable<TypeDeclarationSyntax> UnitTestTypeDeclarations()
            => SourceSyntax.EnumerateTypeDeclarationsIn(UnitTestFiles());

        private static IEnumerable<MethodDeclarationSyntax> UnitTestMethods()
            => UnitTestTypeDeclarations()
                .Where(declaration => declaration is ClassDeclarationSyntax)
                .SelectMany(declaration => declaration.Members.OfType<MethodDeclarationSyntax>())
                .Where(method => !string.Equals(method.Identifier.ValueText, "SetUp", StringComparison.Ordinal))
                .Where(method => HasAttribute(method, "Test")
                    || HasAttribute(method, "TestCase"));

        private static bool IsGivenWhenThenName(string name)
            => name.StartsWith("Given", StringComparison.Ordinal)
                && name.Contains("_When", StringComparison.Ordinal)
                && name.Contains("_Then", StringComparison.Ordinal);

        private static bool HasAttribute(MethodDeclarationSyntax method, string attributeName)
            => method.AttributeLists
                .SelectMany(list => list.Attributes)
                .Any(attribute => string.Equals(attribute.Name.ToString(), attributeName, StringComparison.Ordinal)
                    || string.Equals(attribute.Name.ToString(), attributeName + "Attribute", StringComparison.Ordinal));

        private static bool IsProhibitedAssertion(InvocationExpressionSyntax invocation)
        {
            string expression = invocation.Expression.ToString();

            if (!expression.StartsWith("Assert.", StringComparison.Ordinal))
            {
                return false;
            }

            string memberName = expression.Substring("Assert.".Length);

            return ProhibitedAssertions.Any(
                prohibited => string.Equals(memberName, prohibited, StringComparison.Ordinal));
        }

        private static void AssertNoFailures(List<string> failures, string convention)
        {
            if (failures.Count == 0)
            {
                return;
            }

            Assert.Fail(
                $"Violated test source convention ({convention}):{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
        }
    }
}