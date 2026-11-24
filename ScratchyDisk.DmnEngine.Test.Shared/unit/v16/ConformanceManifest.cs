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
                ["ConformanceTraceabilityTests"] = CanonicalSources.For("DMN 1.6 §2.1 and the repository canonical-source test policy"),
                ["ConformancePolicyTests"] = CanonicalSources.For("DMN 1.6 §2.1 and docs/conformance-test-policy.md"),
                ["CiConformanceWorkflowTests"] = CanonicalSources.For("DMN 1.6 §2.1 and .github/workflows/conformance.yml"),
                ["DocumentationSupportMatrixTests"] = CanonicalSources.For("DMN 1.6 §2.1, readme.md and docs/conformance-test-policy.md"),
                ["ReadmeTestingApproachTests"] = CanonicalSources.For("docs/conformance-test-policy.md and DMN 1.6 §2.1"),
                ["Dmn16BFeelDetectionTests"] = CanonicalSources.For("DMN 1.6 §11.1 and the B-FEEL language URI"),
                ["Dmn16ExpressionRoutingTests"] = CanonicalSources.For("DMN 1.6 §11.1 language selection and §13.3 expressionLanguage"),
                ["Dmn16ExpressionLanguageInspectorTests"] = CanonicalSources.For("DMN 1.6 §13.3 expressionLanguage and §11.1 B-FEEL URI"),
                ["Dmn16OnnxDetectionTests"] = CanonicalSources.For("DMN 1.6 §2.1 and §10.3.2.13.3 ONNX external functions"),
                ["Dmn16ErrorHandlingModeTests"] = CanonicalSources.For("DMN 1.6 §7.3.8"),
                ["Dmn16LenientDiagnosticsTests"] = CanonicalSources.For("DMN 1.6 §7.3.8"),
                ["Dmn16StrictModeTests"] = CanonicalSources.For("DMN 1.6 §7.3.8"),
                ["DmnParser16ApiTests"] = CanonicalSources.For("DMN 1.6 §13.3 and the DMN 1.6 XSD"),
                ["DmnParser16FixtureTests"] = CanonicalSources.For("DMN 1.6 §12 examples and the DMN 1.6 XSD"),
                ["DmnParser16NegativeNamespaceTests"] = CanonicalSources.For("DMN 1.6 §13.3 and the DMN 1.6 XSD"),
                ["DmnParserAutoDetectTests16"] = CanonicalSources.For("DMN 1.6 §13.3 and the DMN 1.6 XSD"),
                ["DmnVersionInfoTests"] = CanonicalSources.For("DMN 1.6 §13.3"),
                ["Feel15IanaTimezoneTests"] = CanonicalSources.For("DMN 1.5 §10.3.4.1 and §10.3.2.3.4/10.3.2.3.6"),
                ["Feel15ListReplaceTests"] = CanonicalSources.For("DMN 1.5 §10.3.4.4 List functions"),
                ["Feel15OverlapsBeforeAfterTests"] = CanonicalSources.For("DMN 1.5 §10.3.4.7 Range Functions"),
                ["Feel15RangeExpressionTests"] = CanonicalSources.For("DMN 1.5 §10.3.4.1 range(from) and §10.3.1.2 rule 66"),
                ["Feel15RangePropertyTests"] = CanonicalSources.For("DMN 1.5 §10.3.2.15 Table 67"),
                ["Feel15RoundingCorrectnessTests"] = CanonicalSources.For("DMN 1.5 §10.3.4.5 Numeric functions"),
                ["Feel15TemporalPropertyTests"] = CanonicalSources.For("DMN 1.5 §10.3.2.15 Tables 66-67"),
                ["Feel16DecimalToIntegerTests"] = CanonicalSources.For("DMN 1.6 §10.3.2.9.4"),
                ["Feel16DescendantTests"] = CanonicalSources.For("DMN 1.6 §10.3.1.2 rule 68 and §10.3.2.15 descendant semantics"),
                ["Feel16NumericLiteralTests"] = CanonicalSources.For("DMN 1.6 §10.3.1.2 rule 35"),
                ["Feel16RoundingOverloadTests"] = CanonicalSources.For("DMN 1.6 §10.3.4.5 Numeric functions"),
                ["Feel16TemporalValueTests"] = CanonicalSources.For("DMN 1.6 §10.3.2.15 Tables 66-67"),
                ["Feel16ZonedDateTimeTests"] = CanonicalSources.For("DMN 1.6 §10.3.4.1 Conversion functions")
            };
    }
}
