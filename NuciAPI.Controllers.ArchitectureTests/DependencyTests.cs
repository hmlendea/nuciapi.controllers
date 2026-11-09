using System;
using System.Collections.Generic;
using System.Linq;

using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;
using ArchUnitNET.Fluent.Slices;

using NUnit.Framework;

namespace NuciAPI.Controllers.ArchitectureTests
{
    [TestFixture]
    public class DependencyTests
    {
        private static readonly string[] ProhibitedAssemblyPrefixes =
        [
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Data.Entity",
            "Amazon.DynamoDBv2",
            "AWSSDK.DynamoDBv2",
            "Dapper",
            "MediatR"
        ];

        private static readonly string[] ProhibitedNamespacePrefixes =
        [
            "Microsoft.EntityFrameworkCore",
            "Amazon.DynamoDBv2",
            "Dapper",
            "MediatR"
        ];

        private Architecture architecture = default!;

        [SetUp]
        public void SetUp()
        {
            architecture = ProductionArchitecture.Loaded;
        }

        [Test]
        public void ProductionArchitectureMustContainTypes()
            => Assert.That(
                architecture.Types,
                Is.Not.Empty,
                "The production assembly was not loaded, so every dependency rule would pass vacuously.");

        [Test]
        public void ProductionTypesMustNotDependOnTestTypes()
        {
            IType[] testDependencies = GetProductionDependencyTargets()
                .Where(IsTestType)
                .Distinct()
                .ToArray();

            Assert.That(
                testDependencies,
                Is.Empty,
                "Production code must never reference test types: " + Describe(testDependencies));
        }

        [Test]
        public void ProductionTypesMustNotDependOnPersistenceOrMediatorLibraries()
        {
            IType[] prohibitedDependencies = GetProductionDependencyTargets()
                .Where(IsProhibitedDependency)
                .Distinct()
                .ToArray();

            Assert.That(
                prohibitedDependencies,
                Is.Empty,
                "Production code must not depend on Entity Framework, DynamoDB, Dapper, or MediatR: "
                    + Describe(prohibitedDependencies));
        }

        [Test]
        public void ProductionTypesMustNotDependOnUndocumentedAssemblies()
        {
            IType[] undocumentedDependencies = GetProductionDependencyTargets()
                .Where(target => !ProductionArchitecture.IsAllowedAssemblyName(target.Assembly.Name))
                .Distinct()
                .ToArray();

            Assert.That(
                undocumentedDependencies,
                Is.Empty,
                "Production code may only depend on the framework and the documented NuciAPI assemblies: "
                    + Describe(undocumentedDependencies));
        }

        [Test]
        public void ProductionNamespacesMustBeFreeOfCycles()
        {
            SliceRule cycleRule = SliceRuleDefinition.Slices()
                .Matching("NuciAPI.Controllers.(*)")
                .Should()
                .BeFreeOfCycles();

            Assert.That(
                cycleRule.HasNoViolations(architecture),
                Is.True,
                "Production namespaces must not form dependency cycles.");
        }

        [Test]
        public void DomainLayerRulesAreNotApplicable()
            => Assert.Ignore(
                "The documented architecture defines no Domain layer. " +
                "The dependency direction in ARCHITECTURE.md is consumer, public contract layer, implementation layer.");

        [Test]
        public void ApplicationLayerRulesAreNotApplicable()
            => Assert.Ignore(
                "The documented architecture defines no Application layer. " +
                "The dependency direction in ARCHITECTURE.md is consumer, public contract layer, implementation layer.");

        [Test]
        public void InfrastructureLayerRulesAreNotApplicable()
            => Assert.Ignore(
                "The documented architecture defines no Infrastructure layer. " +
                "The dependency direction in ARCHITECTURE.md is consumer, public contract layer, implementation layer.");

        private IEnumerable<IType> GetProductionDependencyTargets()
            => architecture.Types
                .Where(type => type.Namespace.FullName.StartsWith(
                    ProductionArchitecture.ProductionNamespace,
                    StringComparison.Ordinal))
                .SelectMany(type => type.Dependencies)
                .OfType<ITypeDependency>()
                .Select(dependency => dependency.Target)
                .Where(target => target is not null);

        private static bool IsTestType(IType type)
            => type.Namespace.FullName.StartsWith("NuciAPI.Controllers.UnitTests", StringComparison.Ordinal)
                || type.Namespace.FullName.StartsWith("NuciAPI.Controllers.ArchitectureTests", StringComparison.Ordinal);

        private static bool IsProhibitedDependency(IType type)
        {
            string assemblyName = type.Assembly.Name;
            string namespaceName = type.Namespace.FullName;

            return ProhibitedAssemblyPrefixes.Any(
                    prefix => assemblyName.StartsWith(prefix, StringComparison.Ordinal))
                || ProhibitedNamespacePrefixes.Any(
                    prefix => namespaceName.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static string Describe(IEnumerable<IType> types)
            => string.Join(", ", types.Select(type => type.FullName).OrderBy(name => name, StringComparer.Ordinal));
    }
}
