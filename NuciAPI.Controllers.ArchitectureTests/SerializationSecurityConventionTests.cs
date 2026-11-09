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
    public class SerializationSecurityConventionTests
    {
        private static readonly string[] SensitiveNames =
        [
            "password",
            "apiKey",
            "apikey",
            "secret",
            "token",
            "credential",
            "authorization"
        ];

        [Test]
        public void ProductionCodeMustNotLogSensitiveIdentifiersDirectly()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    string expression = invocation.Expression.ToString();

                    if (!expression.Contains("Log", StringComparison.OrdinalIgnoreCase)
                        && !expression.Contains("Telemetry", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
                    {
                        string value = argument.ToString();

                        if (SensitiveNames.Any(name => value.Contains(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            failures.Add($"'{Path.GetFileName(file)}' passes a sensitive identifier to '{expression}'.");
                        }
                    }
                }
            }

            AssertNoFailures(failures, "sensitive data is not logged directly");
        }

        [Test]
        public void ProductionCodeMustNotEmbedExceptionMessagesOrStackTracesInStrings()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                foreach (InterpolatedStringExpressionSyntax interpolation in root.DescendantNodes().OfType<InterpolatedStringExpressionSyntax>())
                {
                    string text = interpolation.ToString();

                    if (text.Contains("Message", StringComparison.Ordinal)
                        || text.Contains("StackTrace", StringComparison.Ordinal))
                    {
                        failures.Add($"'{Path.GetFileName(file)}' embeds an exception message or stack trace in text.");
                    }
                }
            }

            AssertNoFailures(failures, "exception details are not embedded in text");
        }

        [Test]
        public void ProductionCodeMustNotUseCultureDependentDateFormatting()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    string methodName = invocation.Expression.ToString();

                    if (!methodName.EndsWith("DateTime.Parse", StringComparison.Ordinal)
                        && !methodName.EndsWith("DateTime.ParseExact", StringComparison.Ordinal)
                        && !methodName.EndsWith("DateTime.TryParse", StringComparison.Ordinal)
                        && !methodName.EndsWith("DateTime.ToString", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (invocation.ArgumentList.Arguments.Count < 2)
                    {
                        failures.Add($"'{Path.GetFileName(file)}' uses '{methodName}' without a format or provider.");
                    }
                }
            }

            AssertNoFailures(failures, "culture-independent date formatting");
        }

        [Test]
        public void ProductionCodeMustCacheSerializerOptions()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);
                List<ObjectCreationExpressionSyntax> options = root.DescendantNodes()
                    .OfType<ObjectCreationExpressionSyntax>()
                    .Where(creation => creation.Type.ToString().EndsWith("JsonSerializerOptions", StringComparison.Ordinal))
                    .ToList();

                foreach (ObjectCreationExpressionSyntax option in options)
                {
                    if (option.Parent is not FieldDeclarationSyntax field
                        || !field.Modifiers.Any(SyntaxKind.StaticKeyword)
                        || !field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
                    {
                        failures.Add($"'{Path.GetFileName(file)}' allocates JsonSerializerOptions outside a static readonly field.");
                    }
                }
            }

            AssertNoFailures(failures, "cached serializer options");
        }

        [Test]
        public void MappingTypesMustUseTheDedicatedMappingNamespace()
        {
            List<string> failures = [];

            foreach (string file in AllSourceFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);
                string fileName = Path.GetFileNameWithoutExtension(file);

                if (!fileName.EndsWith("MappingExtensions", StringComparison.Ordinal))
                {
                    continue;
                }

                NamespaceDeclarationSyntax? namespaceDeclaration = root.DescendantNodes()
                    .OfType<NamespaceDeclarationSyntax>()
                    .FirstOrDefault();

                if (namespaceDeclaration is null
                    || !namespaceDeclaration.Name.ToString().EndsWith(".Mapping", StringComparison.Ordinal))
                {
                    failures.Add($"'{fileName}.cs' must use a '.Mapping' namespace.");
                }
            }

            AssertNoFailures(failures, "mapping namespace placement");
        }

        [Test]
        public void MappingTypesMustBeInternalStatic()
        {
            List<string> failures = [];

            foreach (string file in AllSourceFiles().Where(file => Path.GetFileNameWithoutExtension(file).EndsWith("MappingExtensions", StringComparison.Ordinal)))
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                foreach (ClassDeclarationSyntax declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    if (!declaration.Modifiers.Any(SyntaxKind.InternalKeyword)
                        || !declaration.Modifiers.Any(SyntaxKind.StaticKeyword))
                    {
                        failures.Add($"'{Path.GetFileName(file)}' must declare an internal static mapping class.");
                    }
                }
            }

            AssertNoFailures(failures, "mapping type accessibility");
        }

        [Test]
        public void EnumerationNamespacesMustNotUseEnumsOrEnumerations()
        {
            List<string> failures = [];

            foreach (string file in AllSourceFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<NamespaceDeclarationSyntax>()
                    .Where(namespaceDeclaration => namespaceDeclaration.Name.ToString().Contains(".Enums", StringComparison.Ordinal)
                        || namespaceDeclaration.Name.ToString().Contains(".Enumerations", StringComparison.Ordinal))
                    .Select(namespaceDeclaration => $"'{Path.GetFileName(file)}' uses a prohibited enum namespace."));
            }

            AssertNoFailures(failures, "enumeration namespace placement");
        }

        [Test]
        public void ProductionCodeMustNotUsePreviewLanguageDirectives()
        {
            List<string> failures = [];

            foreach (string file in AllSourceFiles())
            {
                SyntaxTree tree = SourceSyntax.ParseFile(file);

                failures.AddRange(tree.GetRoot().DescendantTrivia()
                    .Where(trivia => trivia.IsKind(SyntaxKind.PreprocessingMessageTrivia))
                    .Where(trivia => trivia.ToString().Contains("preview", StringComparison.OrdinalIgnoreCase))
                    .Select(_ => $"'{Path.GetFileName(file)}' contains a preview language directive."));
            }

            AssertNoFailures(failures, "no preview language directives");
        }

        private static IEnumerable<string> ProductionFiles()
            => ProductionSourceFiles.Enumerate(RepositoryPaths.ProductionProject);

        private static IEnumerable<string> AllSourceFiles()
            => RepositoryPaths.CheckedSourceProjects.SelectMany(ProductionSourceFiles.Enumerate);

        private static void AssertNoFailures(List<string> failures, string convention)
        {
            if (failures.Count == 0)
            {
                return;
            }

            Assert.Fail(
                $"Violated serialization/security convention ({convention}):{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
        }
    }
}
