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
    public class InterfaceConventionTests
    {
        [Test]
        public void InterfacesMustBePrefixedWithI()
        {
            List<string> failures = SourceSyntax
                .EnumerateTypeDeclarationsIn(ProductionFiles())
                .OfType<InterfaceDeclarationSyntax>()
                .Where(declaration => !declaration.Identifier.ValueText.StartsWith('I'))
                .Select(declaration => $"'{declaration.Identifier.ValueText}' is an interface without the 'I' prefix.")
                .ToList();

            AssertNoFailures(failures, "interface naming");
        }

        [Test]
        public void InterfaceNamesMustUsePascalCase()
        {
            List<string> failures = SourceSyntax
                .EnumerateTypeDeclarationsIn(ProductionFiles())
                .OfType<InterfaceDeclarationSyntax>()
                .Where(declaration => !IsPascalCase(declaration.Identifier.ValueText))
                .Select(declaration => $"'{declaration.Identifier.ValueText}' must use PascalCase.")
                .ToList();

            AssertNoFailures(failures, "PascalCase interface names");
        }

        [Test]
        public void InterfacesMustMirrorTheirImplementation()
        {
            List<TypeDeclarationSyntax> declarations = SourceSyntax
                .EnumerateTypeDeclarationsIn(ProductionFiles())
                .ToList();

            List<InterfaceDeclarationSyntax> interfaces = declarations
                .OfType<InterfaceDeclarationSyntax>()
                .ToList();

            if (interfaces.Count == 0)
            {
                Assert.Ignore(
                    "No production interfaces exist yet; there is no colocation contract to verify. " +
                    "The rule becomes active as soon as a production interface is added.");
            }

            List<string> failures = [];

            foreach (InterfaceDeclarationSyntax interfaceDeclaration in interfaces)
            {
                string implementationName = interfaceDeclaration.Identifier.ValueText.Substring(1);

                if (!declarations.Any(declaration => string.Equals(
                    declaration.Identifier.ValueText,
                    implementationName,
                    StringComparison.Ordinal)))
                {
                    failures.Add($"'{interfaceDeclaration.Identifier.ValueText}' has no matching '{implementationName}' implementation.");
                    continue;
                }

                ClassDeclarationSyntax? implementation = declarations
                    .OfType<ClassDeclarationSyntax>()
                    .First(declaration => string.Equals(
                        declaration.Identifier.ValueText,
                        implementationName,
                        StringComparison.Ordinal));

                List<string> interfaceMembers = interfaceDeclaration.Members
                    .OfType<MethodDeclarationSyntax>()
                    .Select(method => method.Identifier.ValueText)
                    .ToList();

                List<string> implementationMembers = implementation.Members
                    .OfType<MethodDeclarationSyntax>()
                    .Select(method => method.Identifier.ValueText)
                    .ToList();

                foreach (string interfaceMember in interfaceMembers)
                {
                    if (!implementationMembers.Contains(interfaceMember, StringComparer.Ordinal))
                    {
                        failures.Add($"'{implementationName}' does not implement '{interfaceMember}' from '{interfaceDeclaration.Identifier.ValueText}'.");
                    }
                }
            }

            AssertNoFailures(failures, "interface and implementation alignment");
        }

        [Test]
        public void NuGetInterfaceMembersMustHaveXmlDocumentation()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<InterfaceDeclarationSyntax>()
                    .SelectMany(declaration => declaration.Members)
                    .Where(member => member is MethodDeclarationSyntax)
                    .Cast<MethodDeclarationSyntax>()
                    .Where(method => !SourceSyntax.HasXmlDocumentation(method))
                    .Select(method => $"{Describe(file, method)} is a public NuGet interface member without XML documentation."));
            }

            AssertNoFailures(failures, "XML documentation on NuGet interface members");
        }

        [Test]
        public void InterfaceMembersMustNotBePartial()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<InterfaceDeclarationSyntax>()
                    .Where(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                    .Select(declaration => $"{Describe(file, declaration)} is a partial interface; partial declarations are prohibited."));
            }

            AssertNoFailures(failures, "no partial interfaces");
        }

        private static IEnumerable<string> ProductionFiles()
            => ProductionSourceFiles.Enumerate(RepositoryPaths.ProductionProject);

        private static bool IsPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (!char.IsUpper(name[0]))
            {
                return false;
            }

            return !name.Contains('_');
        }

        private static string Describe(string file, SyntaxNode node)
        {
            FileLinePositionSpan span = node.GetLocation().GetLineSpan();

            return $"'{Path.GetFileName(file)}:{span.StartLinePosition.Line + 1}'";
        }

        private static void AssertNoFailures(List<string> failures, string convention)
        {
            if (failures.Count == 0)
            {
                return;
            }

            Assert.Fail(
                $"Violated interface convention ({convention}):{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
        }
    }
}