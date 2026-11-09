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
    public class CodingConventionTests
    {
        private const int MaximumParameterCount = 7;
        private const int MaximumConditionalLineLength = 72;
        private const int MaximumSignatureLineLength = 96;

        [Test]
        public void ProductionFilesMustNotUseImplicitlyTypedLocals()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                foreach (VariableDeclarationSyntax declaration in root.DescendantNodes().OfType<VariableDeclarationSyntax>())
                {
                    if (declaration.Type.IsVar)
                    {
                        failures.Add($"{Describe(file, declaration)} uses 'var'; declare an explicit type.");
                    }
                }
            }

            AssertNoFailures(failures, "explicit local types");
        }

        [Test]
        public void ProductionFilesMustNotUseConditionalExpressions()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                foreach (ConditionalExpressionSyntax conditional in root.DescendantNodes().OfType<ConditionalExpressionSyntax>())
                {
                    failures.Add($"{Describe(file, conditional)} uses a conditional expression; use if/else.");
                }
            }

            AssertNoFailures(failures, "no conditional expressions");
        }

        [Test]
        public void ProductionFilesMustBraceEveryControlFlowBody()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                foreach (IfStatementSyntax statement in root.DescendantNodes().OfType<IfStatementSyntax>())
                {
                    failures.AddRange(CheckBraced(file, statement, statement.Statement));
                }

                foreach (ElseClauseSyntax clause in root.DescendantNodes().OfType<ElseClauseSyntax>())
                {
                    bool isElseIf = clause.Statement is IfStatementSyntax;
                    failures.AddRange(isElseIf ? [] : CheckBraced(file, clause, clause.Statement));
                }

                foreach (ForStatementSyntax statement in root.DescendantNodes().OfType<ForStatementSyntax>())
                {
                    failures.AddRange(CheckBraced(file, statement, statement.Statement));
                }

                foreach (ForEachStatementSyntax statement in root.DescendantNodes().OfType<ForEachStatementSyntax>())
                {
                    failures.AddRange(CheckBraced(file, statement, statement.Statement));
                }

                foreach (WhileStatementSyntax statement in root.DescendantNodes().OfType<WhileStatementSyntax>())
                {
                    failures.AddRange(CheckBraced(file, statement, statement.Statement));
                }

                foreach (DoStatementSyntax statement in root.DescendantNodes().OfType<DoStatementSyntax>())
                {
                    failures.AddRange(CheckBraced(file, statement, statement.Statement));
                }

                foreach (UsingStatementSyntax statement in root.DescendantNodes().OfType<UsingStatementSyntax>())
                {
                    failures.AddRange(CheckBraced(file, statement, statement.Statement));
                }

                foreach (LockStatementSyntax statement in root.DescendantNodes().OfType<LockStatementSyntax>())
                {
                    failures.AddRange(CheckBraced(file, statement, statement.Statement));
                }
            }

            AssertNoFailures(failures, "braced control-flow bodies");
        }

        [Test]
        public void ProductionFilesMustNotUseRefParameters()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                foreach (ParameterSyntax parameter in root.DescendantNodes().OfType<ParameterSyntax>())
                {
                    if (parameter.Modifiers.Any(SyntaxKind.RefKeyword))
                    {
                        failures.Add($"{Describe(file, parameter)} declares a ref parameter.");
                    }
                }
            }

            AssertNoFailures(failures, "no ref parameters");
        }

        [Test]
        public void ProductionFilesMustNotDeclareOptionalParameters()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                foreach (ParameterSyntax parameter in root.DescendantNodes().OfType<ParameterSyntax>())
                {
                    if (parameter.Default is not null)
                    {
                        failures.Add($"{Describe(file, parameter)} declares an optional parameter; use an overload.");
                    }
                }
            }

            AssertNoFailures(failures, "no optional parameters");
        }

        [Test]
        public void ProductionFilesMustNotUseTuples()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<TupleTypeSyntax>()
                    .Select(tuple => $"{Describe(file, tuple)} uses a tuple type."));

                failures.AddRange(root.DescendantNodes()
                    .OfType<TupleExpressionSyntax>()
                    .Select(tuple => $"{Describe(file, tuple)} uses a tuple expression."));
            }

            AssertNoFailures(failures, "no tuples");
        }

        [Test]
        public void ProductionMembersMustNotExceedTheParameterLimit()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<BaseMethodDeclarationSyntax>()
                    .Where(method => method.ParameterList.Parameters.Count > MaximumParameterCount)
                    .Select(method => $"{Describe(file, method)} exceeds {MaximumParameterCount} parameters."));

                failures.AddRange(root.DescendantNodes()
                    .OfType<ParenthesizedLambdaExpressionSyntax>()
                    .Where(lambda => lambda.ParameterList.Parameters.Count > MaximumParameterCount)
                    .Select(lambda => $"{Describe(file, lambda)} exceeds {MaximumParameterCount} parameters."));
            }

            AssertNoFailures(failures, "parameter count limit");
        }

        [Test]
        public void ProductionFilesMustNotUseTheNullCoalescingOperator()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<BinaryExpressionSyntax>()
                    .Where(expression => expression.IsKind(SyntaxKind.CoalesceExpression))
                    .Select(expression => $"{Describe(file, expression)} uses the null-coalescing operator."));
            }

            AssertNoFailures(failures, "no null-coalescing operator");
        }

        [Test]
        public void ProductionFilesMustUseNullPatternsInsteadOfEqualityComparisons()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<BinaryExpressionSyntax>()
                    .Where(expression => IsNullEqualityComparison(expression))
                    .Select(expression => $"{Describe(file, expression)} compares against null; use 'is null' or 'is not null'."));
            }

            AssertNoFailures(failures, "null pattern usage");
        }

        [Test]
        public void ProductionFilesMustKeepElseIfOnOneLine()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                foreach (ElseClauseSyntax clause in root.DescendantNodes().OfType<ElseClauseSyntax>())
                {
                    if (clause.Statement is not IfStatementSyntax nested)
                    {
                        continue;
                    }

                    int elseLine = clause.ElseKeyword.GetLocation().GetLineSpan().StartLinePosition.Line;
                    int ifLine = nested.IfKeyword.GetLocation().GetLineSpan().StartLinePosition.Line;

                    if (elseLine != ifLine)
                    {
                        failures.Add($"{Describe(file, clause)} splits 'else' and 'if' onto separate lines.");
                    }
                }
            }

            AssertNoFailures(failures, "else-if layout");
        }

        [Test]
        public void ProductionPropertiesMustNotUseInitAccessors()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<AccessorDeclarationSyntax>()
                    .Where(accessor => accessor.IsKind(SyntaxKind.InitAccessorDeclaration))
                    .Select(accessor => $"{Describe(file, accessor)} uses an init accessor."));
            }

            AssertNoFailures(failures, "no init accessors");
        }

        [Test]
        public void ProductionFilesMustNotDeclareConstants()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<LocalDeclarationStatementSyntax>()
                    .Where(statement => statement.Modifiers.Any(SyntaxKind.ConstKeyword))
                    .Select(statement => $"{Describe(file, statement)} declares a local constant."));

                failures.AddRange(root.DescendantNodes()
                    .OfType<FieldDeclarationSyntax>()
                    .Where(field => field.Modifiers.Any(SyntaxKind.ConstKeyword))
                    .Select(field => $"{Describe(file, field)} declares a constant field; use a static property."));
            }

            AssertNoFailures(failures, "constant declaration form");
        }

        [Test]
        public void ProductionFilesMustNotUseFullyQualifiedTypeNames()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<QualifiedNameSyntax>()
                    .Where(name => name.Parent is not UsingDirectiveSyntax
                        && name.Parent is not NamespaceDeclarationSyntax)
                    .Where(name => !IsTypeInNamespaceDeclaration(name))
                    .Select(name => $"{Describe(file, name)} uses a fully qualified type name; add a using directive."));
            }

            AssertNoFailures(failures, "no fully qualified type names");
        }

        [Test]
        public void ProductionFilesMustNotDeclareGlobalUsings()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                failures.AddRange(root.Usings
                    .Where(usingDirective => usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                    .Select(usingDirective => $"{Describe(file, usingDirective)} uses a global using directive."));
            }

            AssertNoFailures(failures, "no global usings");
        }

        [Test]
        public void InterfacesMustBePrefixedWithI()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                failures.AddRange(root.DescendantNodes()
                    .OfType<InterfaceDeclarationSyntax>()
                    .Where(declaration => !declaration.Identifier.ValueText.StartsWith('I'))
                    .Select(declaration => $"{Describe(file, declaration)} is an interface without the 'I' prefix."));
            }

            AssertNoFailures(failures, "interface naming");
        }

        [Test]
        public void PublicNuGetMembersMustHaveXmlDocumentation()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                foreach (MemberDeclarationSyntax member in SourceSyntax.EnumerateTypeDeclarations(root)
                    .SelectMany(SourceSyntax.EnumerateMembers)
                    .Where(SourceSyntax.IsPublic))
                {
                    if (SourceSyntax.HasXmlDocumentation(member))
                    {
                        continue;
                    }

                    failures.Add($"{Describe(file, member)} is public without XML documentation.");
                }
            }

            AssertNoFailures(failures, "XML documentation on public NuGet members");
        }

        [Test]
        public void ProductionFilesMustNotUseTabsForIndentation()
        {
            List<string> failures = ProductionFiles()
                .Where(file => File.ReadAllText(file).Contains('\t'))
                .Select(file => $"{Path.GetFileName(file)} uses tab characters for indentation.")
                .ToList();

            AssertNoFailures(failures, "space indentation");
        }

        [Test]
        public void MultiClauseConditionsMustRespectTheLineLengthThreshold()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                foreach (IfStatementSyntax statement in root.DescendantNodes().OfType<IfStatementSyntax>())
                {
                    string condition = statement.Condition.ToString();
                    bool isMultiClause = condition.Contains("&&", StringComparison.Ordinal)
                        || condition.Contains("||", StringComparison.Ordinal);

                    if (!isMultiClause)
                    {
                        continue;
                    }

                    int lineLength = GetLineLength(file, statement.Condition.SpanStart);

                    if (lineLength > MaximumConditionalLineLength)
                    {
                        failures.Add(
                            $"{Describe(file, statement)} has a condition line of {lineLength} characters; split it at {MaximumConditionalLineLength} characters.");
                    }
                }
            }

            AssertNoFailures(failures, "conditional line length");
        }

        [Test]
        public void LongSignaturesMustBeSplitAcrossLines()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                foreach (ParameterListSyntax parameterList in root.DescendantNodes().OfType<ParameterListSyntax>())
                {
                    int lineLength = GetLineLength(file, parameterList.SpanStart);

                    if (lineLength <= MaximumSignatureLineLength)
                    {
                        continue;
                    }

                    bool isSplit = parameterList.Parameters
                        .Any(parameter => parameter.GetLocation().GetLineSpan().StartLinePosition.Line
                            != parameterList.GetLocation().GetLineSpan().StartLinePosition.Line);

                    if (!isSplit)
                    {
                        failures.Add(
                            $"{Describe(file, parameterList)} has a {lineLength}-character signature line; split it at {MaximumSignatureLineLength} characters.");
                    }
                }
            }

            AssertNoFailures(failures, "signature line length");
        }

        [Test]
        public void ProductionMethodsMustBeSeparatedByExactlyOneBlankLine()
        {
            List<string> failures = [];

            foreach (string file in ProductionFiles())
            {
                CompilationUnitSyntax root = SourceSyntax.ParseCompilationUnit(file);

                foreach (TypeDeclarationSyntax type in SourceSyntax.EnumerateTypeDeclarations(root))
                {
                    List<MemberDeclarationSyntax> methods = type.Members
                        .Where(member => member is BaseMethodDeclarationSyntax)
                        .OrderBy(member => member.SpanStart)
                        .ToList();

                    for (int index = 1; index < methods.Count; index++)
                    {
                        int previousEnd = GetLastLine(methods[index - 1]);
                        int currentStart = GetFirstLine(methods[index]);

                        if (currentStart - previousEnd != 2)
                        {
                            failures.Add(
                                $"{Describe(file, methods[index])} must be separated from the previous method by exactly one blank line.");
                        }
                    }
                }
            }

            AssertNoFailures(failures, "single blank line between methods");
        }

        private static IEnumerable<string> ProductionFiles()
            => ProductionSourceFiles.Enumerate(RepositoryPaths.ProductionProject);

        private static IEnumerable<string> CheckBraced(string file, SyntaxNode owner, StatementSyntax body)
            => body is BlockSyntax ? [] : [$"{Describe(file, owner)} has an unbraced control-flow body."];

        private static bool IsNullEqualityComparison(BinaryExpressionSyntax expression)
        {
            if (!expression.IsKind(SyntaxKind.EqualsExpression) && !expression.IsKind(SyntaxKind.NotEqualsExpression))
            {
                return false;
            }

            return expression.Left.IsKind(SyntaxKind.NullLiteralExpression)
                || expression.Right.IsKind(SyntaxKind.NullLiteralExpression);
        }

        private static bool IsTypeInNamespaceDeclaration(QualifiedNameSyntax name)
        {
            SyntaxNode? parent = name.Parent;

            while (parent is not null)
            {
                if (parent is UsingDirectiveSyntax || parent is NamespaceDeclarationSyntax)
                {
                    return true;
                }

                if (parent is MemberDeclarationSyntax || parent is StatementSyntax)
                {
                    return false;
                }

                parent = parent.Parent;
            }

            return true;
        }

        private static int GetLineLength(string file, int position)
        {
            string[] lines = File.ReadAllLines(file);
            int lineIndex = SourceSyntax.ParseCompilationUnit(file)
                .SyntaxTree
                .GetLineSpan(new Microsoft.CodeAnalysis.Text.TextSpan(position, 0))
                .StartLinePosition
                .Line;

            if (lineIndex >= lines.Length)
            {
                return 0;
            }

            return lines[lineIndex].Length;
        }

        private static int GetFirstLine(MemberDeclarationSyntax member)
        {
            SyntaxTrivia documentation = member.GetLeadingTrivia()
                .FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

            if (documentation == default)
            {
                return member.GetLocation().GetLineSpan().StartLinePosition.Line;
            }

            return documentation.GetLocation().GetLineSpan().StartLinePosition.Line;
        }

        private static int GetLastLine(MemberDeclarationSyntax member)
            => member.GetLocation().GetLineSpan().EndLinePosition.Line;

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
                $"Violated coding convention ({convention}):{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
        }
    }
}