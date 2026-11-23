using System.Diagnostics.CodeAnalysis;
using ScratchyDisk.DmnEngine.Feel;

namespace ScratchyDisk.DmnEngine.Engine.Decisions.Table.Definition
{
    /// <summary>
    /// Definition of decision table rule input - contains the input match evaluation expression and mapping to table input
    /// </summary>
    public class DmnDecisionTableRuleInput
    {
        /// <summary>
        /// Corresponding table input
        /// </summary>
        public DmnDecisionTableInput Input { get; }
        /// <summary>
        /// FEEL expression used to evaluate the rule input match (raw unary test expression)
        /// </summary>
        public string Expression { get; }

        /// <summary>
        /// Unparsed expression used to evaluate the rule input match ("right side")
        /// </summary>
        public string UnparsedExpression { get; }
        /// <summary>
        /// FEEL dialect used to evaluate the rule input.
        /// </summary>
        public FeelDialect Dialect { get; }

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="input">Corresponding table input</param>
        /// <param name="unparsedExpression">Expression used to evaluate the rule input match ("right side")</param>
        public DmnDecisionTableRuleInput(DmnDecisionTableInput input, string unparsedExpression, FeelDialect dialect = FeelDialect.Feel)
        {
            Input = input;
            UnparsedExpression = unparsedExpression;
            Expression = unparsedExpression;
            Dialect = dialect;
        }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"#{Input.Index} {Input} <-- {Expression}";
        }
    }
}
