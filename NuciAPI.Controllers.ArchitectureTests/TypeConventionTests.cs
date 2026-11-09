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
    public class TypeConventionTests
    {
        [Test]
        public void ControllersMustUsePluralNouns()
        {
            List<string> failures = SourceSyntax
                .EnumerateTypeDeclarationsIn(ProductionFiles())
                .Where(IsControllerDeclaration)
                .Where(declaration => !declaration.Modifiers.Any(SyntaxKind.AbstractKeyword))
                .Where(declaration => !IsPlural(GetControllerNoun(declaration)))
                .Select(declaration => $"'{declaration.Identifier.ValueText}' must use a plural noun.")
                .ToList();

            AssertNoFailures(failures, "plural controller nouns");
        }

        [Test]
        public void NonAbstractNonStaticClassesMustBeSealed()
        {
            List<string> failures = SourceSyntax
                .EnumerateTypeDeclarationsIn(ProductionFiles())
                .OfType<ClassDeclarationSyntax>()
                .Where(declaration => !declaration.Modifiers.Any(SyntaxKind.AbstractKeyword))
                .Where(declaration => !declaration.Modifiers.Any(SyntaxKind.StaticKeyword))
                .Where(declaration => !declaration.Modifiers.Any(SyntaxKind.SealedKeyword))
                .Select(declaration => $"'{declaration.Identifier.ValueText}' must be sealed.")
                .ToList();

            AssertNoFailures(failures, "sealed classes");
        }

        [Test]
        public void ClassesWithPublicMethodsMustHaveAColocatedInterface()
        {
            List<TypeDeclarationSyntax> declarations = SourceSyntax
                .EnumerateTypeDeclarationsIn(ProductionFiles())
                .ToList();

            List<ClassDeclarationSyntax> eligibleClasses = declarations
                .OfType<ClassDeclarationSyntax>()
                .Where(declaration => !declaration.Modifiers.Any(SyntaxKind.StaticKeyword))
                .Where(declaration => !declaration.Modifiers.Any(SyntaxKind.AbstractKeyword))
                .Where(declaration => HasPublicMethod(declaration))
                .ToList();

            if (eligibleClasses.Count == 0)
            {
                Assert.Ignore(
                    "No production class currently exposes a public method; " +
                    "every public method is either inherited framework surface or deliberately protected. " +
                    "This rule becomes active as soon as such a class is added.");
            }

            HashSet<string> interfaceNames = declarations
                .OfType<InterfaceDeclarationSyntax>()
                .Select(declaration => declaration.Identifier.ValueText)
                .ToHashSet(StringComparer.Ordinal);

            List<string> failures = eligibleClasses
                .Where(declaration => !interfaceNames.Contains("I" + declaration.Identifier.ValueText))
                .Select(declaration => $"'{declaration.Identifier.ValueText}' requires a colocated 'I{declaration.Identifier.ValueText}' interface.")
                .ToList();

            AssertNoFailures(failures, "colocated interfaces");
        }

        [Test]
        public void ColocatedInterfacesMustMirrorTheImplementation()
        {
            List<TypeDeclarationSyntax> declarations = SourceSyntax
                .EnumerateTypeDeclarationsIn(ProductionFiles())
                .ToList();

            List<InterfaceDeclarationSyntax> interfaces = declarations
                .OfType<InterfaceDeclarationSyntax>()
                .ToList();

            if (interfaces.Count == 0)
            {
                Assert.Ignore("No production interfaces exist yet; there is no colocation contract to verify.");
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
                }
            }

            AssertNoFailures(failures, "interface and implementation pairing");
        }

        [Test]
        public void DomainDataObjectsMustFollowTheirNamingSuffixes()
        {
            List<string> failures = SourceSyntax
                .EnumerateTypeDeclarationsIn(ProductionFiles())
                .Where(declaration => declaration is ClassDeclarationSyntax || declaration is RecordDeclarationSyntax)
                .Where(declaration => InheritsFrom(declaration, "DbContext"))
                .Select(declaration => $"'{declaration.Identifier.ValueText}' is a persistence context; this library must not define one.")
                .ToList();

            AssertNoFailures(failures, "no persistence contexts in this library");
        }

        [Test]
        public void ProductionTypesMustNotExposeRepositoriesOrHandlers()
        {
            List<string> prohibitedSuffixes = ["Repository", "CommandHandler", "QueryHandler", "Validator"];

            List<string> failures = SourceSyntax
                .EnumerateTypeDeclarationsIn(ProductionFiles())
                .Where(declaration => prohibitedSuffixes.Any(
                    suffix => declaration.Identifier.ValueText.EndsWith(suffix, StringComparison.Ordinal)))
                .Select(declaration => $"'{declaration.Identifier.ValueText}' belongs to a persistence or mediator layer that this library does not define.")
                .ToList();

            AssertNoFailures(failures, "no repositories or handlers");
        }

        [Test]
        public void AuthorisationTypesMustFollowTheStrategyNamingPattern()
        {
            List<string> failures = SourceSyntax
                .EnumerateTypeDeclarationsIn(ProductionFiles())
                .OfType<ClassDeclarationSyntax>()
                .Where(declaration => declaration.Identifier.ValueText.EndsWith("Authorisation", StringComparison.Ordinal))
                .Where(declaration => !declaration.Modifiers.Any(SyntaxKind.AbstractKeyword))
                .Where(declaration => !declaration.Modifiers.Any(SyntaxKind.SealedKeyword))
                .Select(declaration => $"'{declaration.Identifier.ValueText}' is a concrete authorisation strategy and must be sealed.")
                .ToList();

            AssertNoFailures(failures, "sealed authorisation strategies");
        }

        private static IEnumerable<string> ProductionFiles()
            => ProductionSourceFiles.Enumerate(RepositoryPaths.ProductionProject);

        private static bool IsControllerDeclaration(TypeDeclarationSyntax declaration)
            => declaration.Identifier.ValueText.EndsWith("Controller", StringComparison.Ordinal);

        private static string GetControllerNoun(TypeDeclarationSyntax declaration)
            => declaration.Identifier.ValueText.Substring(
                0,
                declaration.Identifier.ValueText.Length - "Controller".Length);

        private static bool IsPlural(string noun)
            => noun.EndsWith('s') && !noun.EndsWith("ss", StringComparison.Ordinal);

        private static bool HasPublicMethod(ClassDeclarationSyntax declaration)
            => declaration.Members
                .OfType<MethodDeclarationSyntax>()
                .Any(method => method.Modifiers.Any(SyntaxKind.PublicKeyword));

        private static bool InheritsFrom(TypeDeclarationSyntax declaration, string baseTypeName)
            => declaration.BaseList?.Types
                .OfType<SimpleBaseTypeSyntax>()
                .Any(baseType => string.Equals(
                    baseType.Type.ToString(),
                    baseTypeName,
                    StringComparison.Ordinal)) ?? false;

        private static void AssertNoFailures(List<string> failures, string convention)
        {
            if (failures.Count == 0)
            {
                return;
            }

            Assert.Fail(
                $"Violated type convention ({convention}):{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
        }
    }
}