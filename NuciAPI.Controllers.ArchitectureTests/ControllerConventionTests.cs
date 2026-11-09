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
    public class ControllerConventionTests
    {
        private static readonly string[] PluralisationExceptions =
        [
            "Controller",
            "Configuration",
            "Information",
            "Settings"
        ];

        [Test]
        public void ControllerTypesMustUsePluralNouns()
        {
            List<string> failures = SourceSyntax
                .EnumerateTypeDeclarationsIn(ProductionFiles())
                .OfType<ClassDeclarationSyntax>()
                .Where(declaration => declaration.Identifier.ValueText.EndsWith("Controller", StringComparison.Ordinal))
                .Where(declaration => !declaration.Modifiers.Any(SyntaxKind.AbstractKeyword))
                .Where(declaration => !IsPlural(GetControllerNoun(declaration)))
                .Select(declaration => $"'{declaration.Identifier.ValueText}' must use a plural noun.")
                .ToList();

            AssertNoFailures(failures, "plural controller nouns");
        }

        [Test]
        public void ControllersMustNotReturnActionResultOfT()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(method => method.ReturnType is GenericNameSyntax generic
                        && string.Equals(generic.Identifier.ValueText, "ActionResult", StringComparison.Ordinal)
                        && generic.TypeArgumentList.Arguments.Count == 1)
                    .Select(method => $"{Describe(file, method)} returns ActionResult<T>; controllers must return ActionResult."));
            }

            AssertNoFailures(failures, "no ActionResult<T> returns");
        }

        [Test]
        public void ControllerActionsMustBeExpressionBodied()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(method => method.Modifiers.Any(SyntaxKind.PublicKeyword))
                    .Where(method => method.ReturnType is not null)
                    .Where(method => IsActionResultType(method.ReturnType))
                    .Where(method => method.Body is not null)
                    .Select(method => $"{Describe(file, method)} must use an expression-bodied delegate to ProcessRequest."));
            }

            AssertNoFailures(failures, "expression-bodied controller actions");
        }

        [Test]
        public void ControllersMustBindInputsWithExplicitAttributes()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                foreach (MethodDeclarationSyntax method in root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(method => method.Modifiers.Any(SyntaxKind.PublicKeyword))
                    .Where(method => IsActionResultType(method.ReturnType)))
                {
                    foreach (ParameterSyntax parameter in method.ParameterList.Parameters)
                    {
                        if (!parameter.AttributeLists.Any())
                        {
                            failures.Add(
                                $"{Describe(file, parameter)} is a controller parameter without an explicit binding attribute.");
                        }
                    }
                }
            }

            AssertNoFailures(failures, "explicit binding attributes");
        }

        [Test]
        public void ControllersMustNotReadRequestValuesDirectly()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<MemberAccessExpressionSyntax>()
                    .Where(access => IsRequestValueAccess(access))
                    .Select(access => $"{Describe(file, access)} reads request values directly; use explicit binding attributes."));
            }

            AssertNoFailures(failures, "no direct request value reads");
        }

        private static IEnumerable<string> ProductionFiles()
            => ProductionSourceFiles.Enumerate(RepositoryPaths.ProductionProject);

        private static string GetControllerNoun(ClassDeclarationSyntax declaration)
        {
            string name = declaration.Identifier.ValueText;

            return name.Substring(0, name.Length - "Controller".Length);
        }

        private static bool IsPlural(string noun)
        {
            if (string.IsNullOrEmpty(noun))
            {
                return false;
            }

            if (noun.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                && !noun.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool IsActionResultType(TypeSyntax returnType)
        {
            if (returnType is GenericNameSyntax generic)
            {
                return string.Equals(generic.Identifier.ValueText, "ActionResult", StringComparison.Ordinal);
            }

            if (returnType is QualifiedNameSyntax qualified)
            {
                return string.Equals(qualified.Right.Identifier.ValueText, "ActionResult", StringComparison.Ordinal);
            }

            if (returnType is NameSyntax name)
            {
                return string.Equals(name.ToString(), "ActionResult", StringComparison.Ordinal);
            }

            return false;
        }

        private static bool IsRequestValueAccess(MemberAccessExpressionSyntax access)
        {
            if (!string.Equals(access.Name.Identifier.ValueText, "Value", StringComparison.Ordinal))
            {
                return false;
            }

            string expression = access.Expression.ToString();

            return expression.Contains("Request", StringComparison.Ordinal)
                || expression.Contains("HttpContext", StringComparison.Ordinal);
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
                $"Violated controller convention ({convention}):{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
        }
    }
}