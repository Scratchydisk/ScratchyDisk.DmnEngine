using System;
using ScratchyDisk.DmnEngine.Feel;

namespace ScratchyDisk.DmnEngine.Parser
{
    /// <summary>
    /// Well-known DMN expression language identifiers.
    /// </summary>
    public static class DmnExpressionLanguage
    {
        /// <summary>
        /// DMN 1.6 B-FEEL dialect. Optional for Level 3 conformance and not implemented by this engine.
        /// </summary>
        public const string BFeel = "https://www.omg.org/spec/DMN/20240513/B-FEEL/";
        /// <summary>
        /// Maps an expressionLanguage value to the evaluator dialect. FEEL and an omitted
        /// language map to <see cref="FeelDialect.Feel"/>.
        /// </summary>
        public static FeelDialect Resolve(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) return FeelDialect.Feel;
            return string.Equals(language.Trim(), BFeel, StringComparison.OrdinalIgnoreCase)
                ? FeelDialect.BFeel
                : FeelDialect.Feel;
        }
    }
}
