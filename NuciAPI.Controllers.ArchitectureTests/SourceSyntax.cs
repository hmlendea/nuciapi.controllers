using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NuciAPI.Controllers.ArchitectureTests
{
    internal static class SourceSyntax
    {
        public static SyntaxTree ParseFile(string file)
            => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);

        public static CompilationUnitSyntax ParseCompilationUnit(string file)
            => ParseFile(file).GetCompilationUnitRoot();

        public static IEnumerable<TypeDeclarationSyntax> EnumerateTypeDeclarations(CompilationUnitSyntax root)
            => root.DescendantNodes().OfType<TypeDeclarationSyntax>();

        public static IEnumerable<TypeDeclarationSyntax> EnumerateTypeDeclarationsIn(IEnumerable<string> files)
            => files.SelectMany(file => EnumerateTypeDeclarations(ParseCompilationUnit(file)));

        public static int CountTypeDeclarations(CompilationUnitSyntax root)
        {
            int count = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Count();
            count += root.DescendantNodes().OfType<StructDeclarationSyntax>().Count();
            count += root.DescendantNodes().OfType<RecordDeclarationSyntax>().Count();
            count += root.DescendantNodes().OfType<InterfaceDeclarationSyntax>().Count();
            count += root.DescendantNodes().OfType<EnumDeclarationSyntax>().Count();
            count += root.DescendantNodes().OfType<DelegateDeclarationSyntax>().Count();

            return count;
        }

        public static bool ContainsTopLevelStatements(CompilationUnitSyntax root)
            => root.Members.OfType<GlobalStatementSyntax>().Any();

        public static IEnumerable<MemberDeclarationSyntax> EnumerateMembers(MemberDeclarationSyntax member)
        {
            yield return member;

            foreach (MemberDeclarationSyntax child in member.ChildNodes().OfType<MemberDeclarationSyntax>())
            {
                foreach (MemberDeclarationSyntax descendant in EnumerateMembers(child))
                {
                    yield return descendant;
                }
            }
        }

        public static bool HasBlockBody(SyntaxNode node)
            => node.DescendantNodes().Any(child => child is BlockSyntax);

        public static int CountStatements(SyntaxNode node)
            => node.DescendantNodes().OfType<StatementSyntax>().Count();

        public static bool IsExpressionBodied(SyntaxNode node)
            => node is ArrowExpressionClauseSyntax;

        public static bool HasExplicitAccessibility(MemberDeclarationSyntax member)
        {
            if (member.Modifiers.Any(SyntaxKind.PublicKeyword)
                || member.Modifiers.Any(SyntaxKind.PrivateKeyword)
                || member.Modifiers.Any(SyntaxKind.ProtectedKeyword)
                || member.Modifiers.Any(SyntaxKind.InternalKeyword))
            {
                return true;
            }

            return member.Parent is InterfaceDeclarationSyntax;
        }

        public static bool RequiresAccessibility(MemberDeclarationSyntax member)
            => member is BaseTypeDeclarationSyntax
                || member is BaseMethodDeclarationSyntax
                || member is BasePropertyDeclarationSyntax
                || member is FieldDeclarationSyntax
                || member is EventFieldDeclarationSyntax
                || member is DelegateDeclarationSyntax;

        public static bool IsPublic(MemberDeclarationSyntax member)
            => member.Modifiers.Any(SyntaxKind.PublicKeyword)
                || (member.Modifiers.Count == 0 && member.Parent is InterfaceDeclarationSyntax);

        public static bool HasXmlDocumentation(MemberDeclarationSyntax member)
            => member.GetLeadingTrivia().Any(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

        public static string BuildExpectedNamespace(string file)
        {
            string projectDirectory = RepositoryPaths.CheckedSourceProjects
                .OrderByDescending(project => project.Length)
                .First(project => IsInsideProject(project, file));
            string projectName = Path.GetFileName(projectDirectory);
            string relativeDirectory = Path.GetRelativePath(projectDirectory, Path.GetDirectoryName(file) ?? projectDirectory);

            if (relativeDirectory == ".")
            {
                return projectName;
            }

            string suffix = relativeDirectory
                .Replace(Path.DirectorySeparatorChar, '.')
                .Replace(Path.AltDirectorySeparatorChar, '.');

            return $"{projectName}.{suffix}";
        }

        private static bool IsInsideProject(string projectDirectory, string file)
        {
            string relativePath = Path.GetRelativePath(projectDirectory, file);

            return !relativePath.StartsWith("..", StringComparison.Ordinal);
        }
    }
}