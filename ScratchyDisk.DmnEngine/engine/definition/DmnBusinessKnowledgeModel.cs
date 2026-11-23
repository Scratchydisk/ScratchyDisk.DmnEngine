using System.Collections.Generic;

namespace ScratchyDisk.DmnEngine.Engine.Definition
{
    /// <summary>
    /// Executable definition of a business knowledge model: a named FEEL function
    /// with formal parameters and a body.
    /// </summary>
    public sealed class DmnBusinessKnowledgeModel
    {
        /// <summary>Name used to invoke the function from FEEL.</summary>
        public string Name { get; }

        /// <summary>Name as written in the model.</summary>
        public string Label { get; }

        /// <summary>Formal parameter names, in declaration order.</summary>
        public IReadOnlyList<string> ParameterNames { get; }

        /// <summary>FEEL expression that forms the function body.</summary>
        public string Expression { get; }

        /// <summary>Expression language URI, if present.</summary>
        public string ExpressionLanguage { get; }

        public DmnBusinessKnowledgeModel(
            string name,
            string label,
            IReadOnlyList<string> parameterNames,
            string expression,
            string expressionLanguage = null)
        {
            Name = name;
            Label = label;
            ParameterNames = parameterNames ?? new List<string>();
            Expression = expression;
            ExpressionLanguage = expressionLanguage;
        }
    }
}
