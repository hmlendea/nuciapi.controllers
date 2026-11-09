using System;
using System.Linq;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;

using NuciAPI.Controllers;

namespace NuciAPI.Controllers.ArchitectureTests
{
    internal static class ProductionArchitecture
    {
        public const string ProductionNamespace = "NuciAPI.Controllers";

        private static readonly string[] AllowedAssemblyPrefixes =
        [
            "NuciAPI.Controllers",
            "NuciAPI",
            "System",
            "Microsoft",
            "netstandard",
            "mscorlib"
        ];

        public static readonly Architecture Loaded = BuildArchitecture();

        public static GivenTypesConjunction ProductionTypes
            => ArchRuleDefinition.Types().That().ResideInNamespaceOrBelow(ProductionNamespace);

        public static bool IsAllowedAssemblyName(string assemblyName)
            => AllowedAssemblyPrefixes.Any(
                prefix => assemblyName.StartsWith(prefix, StringComparison.Ordinal));

        public static IType[] GetAllAllowedTypes(Architecture architecture)
            => architecture.Types
                .Concat(architecture.ReferencedTypes)
                .Where(type => IsAllowedAssemblyName(type.Assembly.Name))
                .ToArray();

        private static Architecture BuildArchitecture()
        {
            Architecture architecture = new ArchLoader()
                .LoadAssemblies(typeof(NuciApiAuthorisation).Assembly)
                .Build();

            if (!architecture.Types.Any())
            {
                throw new InvalidOperationException(
                    "Production architecture contains no types. Assembly loading failed.");
            }

            return architecture;
        }
    }
}
