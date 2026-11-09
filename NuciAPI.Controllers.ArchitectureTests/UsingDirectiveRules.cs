using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NuciAPI.Controllers.ArchitectureTests
{
    internal static class UsingDirectiveRules
    {
        private const string SystemRoot = "System";
        private const string MicrosoftRoot = "Microsoft";
        private const string SolutionRoot = "NuciAPI";
        private const int SystemGroupRank = 0;
        private const int MicrosoftGroupRank = 1;
        private const int ThirdPartyGroupRank = 2;
        private const int SolutionGroupRank = 3;

        public static IEnumerable<List<string>> GroupUsings(CompilationUnitSyntax root)
        {
            List<string> currentGroup = [];
            int? previousLine = null;

            foreach (UsingDirectiveSyntax usingDirective in root.Usings)
            {
                int line = usingDirective.GetLocation().GetLineSpan().StartLinePosition.Line;

                if (previousLine is not null && line - previousLine > 1 && currentGroup.Count > 0)
                {
                    yield return currentGroup;
                    currentGroup = [];
                }

                currentGroup.Add(usingDirective.Name?.ToString() ?? string.Empty);
                previousLine = line;
            }

            if (currentGroup.Count > 0)
            {
                yield return currentGroup;
            }
        }

        public static int GetRank(string usingName)
        {
            if (IsRoot(usingName, SystemRoot))
            {
                return SystemGroupRank;
            }

            if (IsRoot(usingName, MicrosoftRoot))
            {
                return MicrosoftGroupRank;
            }

            if (IsRoot(usingName, SolutionRoot))
            {
                return SolutionGroupRank;
            }

            return ThirdPartyGroupRank;
        }

        public static string GetRoot(string usingName)
            => usingName.Split('.')[0];

        private static bool IsRoot(string usingName, string expectedRoot)
            => string.Equals(GetRoot(usingName), expectedRoot, System.StringComparison.Ordinal);
    }
}