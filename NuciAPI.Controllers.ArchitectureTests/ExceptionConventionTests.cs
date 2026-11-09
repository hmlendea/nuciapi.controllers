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
    public class ExceptionConventionTests
    {
        [Test]
        public void CustomExceptionsMustEndWithException()
        {
            List<string> failures = SourceSyntax
                .EnumerateTypeDeclarationsIn(ProductionFiles())
                .OfType<ClassDeclarationSyntax>()
                .Where(declaration => InheritsFromException(declaration))
                .Where(declaration => !declaration.Identifier.ValueText.EndsWith("Exception", StringComparison.Ordinal))
                .Select(declaration => $"'{declaration.Identifier.ValueText}' must end with 'Exception'.")
                .ToList();

            AssertNoFailures(failures, "exception naming");
        }

        [Test]
        public void CustomExceptionConstructorsMustChainToTheBaseException()
        {
            List<string> failures = [];

            foreach (ClassDeclarationSyntax declaration in SourceSyntax
                .EnumerateTypeDeclarationsIn(ProductionFiles())
                .OfType<ClassDeclarationSyntax>()
                .Where(InheritsFromException))
            {
                List<ConstructorDeclarationSyntax> constructors = declaration.Members
                    .OfType<ConstructorDeclarationSyntax>()
                    .ToList();

                if (constructors.Count == 0)
                {
                    continue;
                }

                foreach (ConstructorDeclarationSyntax constructor in constructors)
                {
                    if (!constructor.Modifiers.Any(SyntaxKind.PublicKeyword)
                        && !constructor.Modifiers.Any(SyntaxKind.InternalKeyword))
                    {
                        continue;
                    }

                    List<InvocationExpressionSyntax> baseInvocations = constructor.Body?.Statements
                        .OfType<InvocationExpressionSyntax>()
                        .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax access
                            && string.Equals(access.Name.Identifier.ValueText, "base", StringComparison.Ordinal))
                        .ToList() ?? [];

                    if (baseInvocations.Count == 0)
                    {
                        failures.Add($"'{declaration.Identifier.ValueText}' constructor '{constructor.Identifier.ValueText}' does not forward to the base Exception constructor.");
                    }
                }
            }

            AssertNoFailures(failures, "constructor chaining");
        }

        [Test]
        public void CustomExceptionsMustProvideInnerExceptionOverloads()
        {
            List<string> failures = [];

            foreach (ClassDeclarationSyntax declaration in SourceSyntax
                .EnumerateTypeDeclarationsIn(ProductionFiles())
                .OfType<ClassDeclarationSyntax>()
                .Where(InheritsFromException))
            {
                List<ConstructorDeclarationSyntax> constructors = declaration.Members
                    .OfType<ConstructorDeclarationSyntax>()
                    .Where(constructor => constructor.Modifiers.Any(SyntaxKind.PublicKeyword)
                        || constructor.Modifiers.Any(SyntaxKind.InternalKeyword))
                    .ToList();

                if (constructors.Count == 0)
                {
                    continue;
                }

                bool hasInnerExceptionOverload = constructors
                    .Any(constructor => constructor.ParameterList.Parameters
                        .Any(parameter => string.Equals(parameter.Type.ToString(), "Exception", StringComparison.Ordinal)));

                if (!hasInnerExceptionOverload)
                {
                    failures.Add($"'{declaration.Identifier.ValueText}' must declare at least one constructor accepting an inner Exception.");
                }
            }

            AssertNoFailures(failures, "inner-exception overloads");
        }

        [Test]
        public void ExceptionTypesMustNotUsePersistenceAttributes()
        {
            List<string> failures = SourceSyntax
                .EnumerateTypeDeclarationsIn(ProductionFiles())
                .OfType<ClassDeclarationSyntax>()
                .Where(InheritsFromException)
                .SelectMany(declaration => declaration.AttributeLists.SelectMany(list => list.Attributes))
                .Where(attribute => attribute.Name.ToString().Contains("Table", StringComparison.OrdinalIgnoreCase)
                    || attribute.Name.ToString().Contains("Entity", StringComparison.OrdinalIgnoreCase))
                .Select(attribute => $"Exception type uses a persistence attribute: '{attribute.Name}'.")
                .ToList();

            AssertNoFailures(failures, "no persistence attributes on exceptions");
        }

        private static IEnumerable<string> ProductionFiles()
            => ProductionSourceFiles.Enumerate(RepositoryPaths.ProductionProject);

        private static bool InheritsFromException(ClassDeclarationSyntax declaration)
            => declaration.BaseList?.Types
                .OfType<SimpleBaseTypeSyntax>()
                .Any(baseType => string.Equals(
                    baseType.Type.ToString(),
                    "Exception",
                    StringComparison.Ordinal)
                    || baseType.Type.ToString().EndsWith("Exception", StringComparison.Ordinal)) ?? false;

        private static void AssertNoFailures(List<string> failures, string convention)
        {
            if (failures.Count == 0)
            {
                return;
            }

            Assert.Fail(
                $"Violated exception convention ({convention}):{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
        }
    }
}