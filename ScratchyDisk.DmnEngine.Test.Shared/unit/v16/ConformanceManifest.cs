using System.Collections.Generic;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Traceability manifest for canonical DMN/FEEL conformance tests.
    /// Every test class carrying a DMN 1.5/1.6 conformance category must appear here.
    /// </summary>
    public static class ConformanceManifest
    {
        public static readonly IReadOnlyDictionary<string, string> Sources =
            new Dictionary<string, string>
            {
                ["ConformanceTraceabilityTests"] = "DMN 1.6 §2.1 and the repository canonical-source test policy",
                ["ConformancePolicyTests"] = "DMN 1.6 §2.1 and docs/conformance-test-policy.md",
                ["CiConformanceWorkflowTests"] = "DMN 1.6 §2.1 and .github/workflows/conformance.yml",
                ["DocumentationSupportMatrixTests"] = "DMN 1.6 §2.1, readme.md and docs/conformance-test-policy.md",
                ["ReadmeTestingApproachTests"] = "docs/conformance-test-policy.md and DMN 1.6 §2.1",
                ["Dmn16BFeelDetectionTests"] = "DMN 1.6 §11.1 and the B-FEEL language URI",
                ["Dmn16ExpressionRoutingTests"] = "DMN 1.6 §11.1 language selection and §13.3 expressionLanguage",
                ["Dmn16ExpressionLanguageInspectorTests"] = "DMN 1.6 §13.3 expressionLanguage and §11.1 B-FEEL URI",
                ["Dmn16ErrorHandlingModeTests"] = "DMN 1.6 §7.3.8",
                ["Dmn16LenientDiagnosticsTests"] = "DMN 1.6 §7.3.8",
                ["Dmn16StrictModeTests"] = "DMN 1.6 §7.3.8",
                ["DmnParser16ApiTests"] = "DMN 1.6 §13.3 and the DMN 1.6 XSD",
                ["DmnParser16FixtureTests"] = "DMN 1.6 §12 examples and the DMN 1.6 XSD",
                ["DmnParser16NegativeNamespaceTests"] = "DMN 1.6 §13.3 and the DMN 1.6 XSD",
                ["DmnParserAutoDetectTests16"] = "DMN 1.6 §13.3 and the DMN 1.6 XSD",
                ["DmnVersionInfoTests"] = "DMN 1.6 §13.3",
                ["Feel15IanaTimezoneTests"] = "DMN 1.5 §10.3.4.1 and §10.3.2.3.4/10.3.2.3.6",
                ["Feel15ListReplaceTests"] = "DMN 1.5 §10.3.4.4 List functions",
                ["Feel15OverlapsBeforeAfterTests"] = "DMN 1.5 §10.3.4.7 Range Functions",
                ["Feel15RangeExpressionTests"] = "DMN 1.5 §10.3.4.1 range(from) and §10.3.1.2 rule 66",
                ["Feel15RangePropertyTests"] = "DMN 1.5 §10.3.2.15 Table 67",
                ["Feel15RoundingCorrectnessTests"] = "DMN 1.5 §10.3.4.5 Numeric functions",
                ["Feel15TemporalPropertyTests"] = "DMN 1.5 §10.3.2.15 Tables 66-67",
                ["Feel16DecimalToIntegerTests"] = "DMN 1.6 §10.3.2.9.4",
                ["Feel16DescendantTests"] = "DMN 1.6 §10.3.1.2 rule 68 and §10.3.2.15 descendant semantics",
                ["Feel16NumericLiteralTests"] = "DMN 1.6 §10.3.1.2 rule 35",
                ["Feel16RoundingOverloadTests"] = "DMN 1.6 §10.3.4.5 Numeric functions",
                ["Feel16TemporalValueTests"] = "DMN 1.6 §10.3.2.15 Tables 66-67",
                ["Feel16ZonedDateTimeTests"] = "DMN 1.6 §10.3.4.1 Conversion functions"
            };
    }
}
