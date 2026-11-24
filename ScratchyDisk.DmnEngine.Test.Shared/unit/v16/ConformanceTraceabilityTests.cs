using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §2.1 conformance levels and the repository
    /// canonical-source test policy.
    /// </summary>
    [TestClass]
    [TestCategory("DMN 1.6 conformance")]
    public class ConformanceTraceabilityTests
    {
        private const string Dmn15Category = "DMN 1.5 conformance";
        private const string Dmn16Category = "DMN 1.6 conformance";

        private static IEnumerable<Type> ConformanceClasses()
        {
            return typeof(ConformanceManifest).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => Categories(t).Any(c => c is Dmn15Category or Dmn16Category));
        }

        private static IEnumerable<string> Categories(MemberInfo member)
        {
            return member.GetCustomAttributes(typeof(TestCategoryAttribute), false)
                .Cast<TestCategoryAttribute>()
                .SelectMany(a => a.TestCategories);
        }

        [TestMethod]
        public void Dmn16_Conformance_EveryConformanceClassHasCanonicalSource()
        {
            var missing = ConformanceClasses()
                .Where(t => !ConformanceManifest.Sources.ContainsKey(t.Name))
                .Select(t => t.Name)
                .ToList();

            missing.Should().BeEmpty("every conformance test class must cite its canonical source");
        }

        [TestMethod]
        public void Dmn16_Conformance_EveryConformanceTestNameCitesStandard()
        {
            var badNames = new List<string>();
            foreach (var type in ConformanceClasses())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (method.GetCustomAttribute<TestMethodAttribute>() == null) continue;
                    if (!method.Name.StartsWith("Dmn15_", StringComparison.Ordinal) &&
                        !method.Name.StartsWith("Dmn16_", StringComparison.Ordinal))
                    {
                        badNames.Add($"{type.Name}.{method.Name}");
                    }
                }
            }

            badNames.Should().BeEmpty("conformance test names must carry the DMN clause reference");
        }

        [TestMethod]
        public void Dmn16_Conformance_ManifestSourcesUseCanonicalSpecUrls()
        {
            var canonical = new[] { CanonicalSources.Dmn14, CanonicalSources.Dmn15, CanonicalSources.Dmn16 };

            foreach (var source in ConformanceManifest.Sources)
            {
                canonical.Any(url => source.Value.Contains(url, StringComparison.Ordinal))
                    .Should().BeTrue($"{source.Key} must cite DMN 1.4, 1.5 or 1.6 at {string.Join(" / ", canonical)}");
            }
        }

        [TestMethod]
        public void Dmn16_Conformance_ManifestCoversRequiredAreas()
        {
            var required = new Dictionary<string, string>
            {
                ["descendant expressions"] = "Feel16DescendantTests",
                ["temporal .value"] = "Feel16TemporalValueTests",
                ["single-parameter rounding"] = "Feel16RoundingOverloadTests",
                ["zoned date-time"] = "Feel16ZonedDateTimeTests",
                ["numeric literals"] = "Feel16NumericLiteralTests",
                ["decimal-to-integer"] = "Feel16DecimalToIntegerTests",
                ["list replace"] = "Feel15ListReplaceTests",
                ["range expressions"] = "Feel15RangeExpressionTests",
                ["overlaps before/after"] = "Feel15OverlapsBeforeAfterTests",
                ["temporal properties"] = "Feel15TemporalPropertyTests",
                ["range properties"] = "Feel15RangePropertyTests",
                ["rounding correctness"] = "Feel15RoundingCorrectnessTests",
                ["IANA timezones"] = "Feel15IanaTimezoneTests",
                ["lenient diagnostics"] = "Dmn16LenientDiagnosticsTests",
                ["strict mode"] = "Dmn16StrictModeTests",
                ["B-FEEL detection"] = "Dmn16BFeelDetectionTests"
            };

            var present = ConformanceClasses().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
            var missing = required
                .Where(kv => !present.Contains(kv.Value))
                .Select(kv => $"{kv.Key} -> {kv.Value}")
                .ToList();

            missing.Should().BeEmpty("the manifest must cover every required DMN 1.6/FEEL area");
        }
    }
}
