using System;
using System.Collections.Generic;
using ScratchyDisk.DmnEngine.Engine.Decisions.Expression;

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

        /// <summary>
        /// Executable function body. A literal expression body is wrapped as a
        /// <see cref="DmnLiteralBoxedExpression"/>; other DMN boxed bodies (context,
        /// relation, list, invocation, decision table) are executable as well.
        /// </summary>
        public DmnBoxedExpression Body { get; }

        /// <summary>Aliases used inside the function body.</summary>
        public IReadOnlyDictionary<string, string> NameAliases { get; set; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public DmnBusinessKnowledgeModel(
            string name,
            string label,
            IReadOnlyList<string> parameterNames,
            string expression,
            string expressionLanguage = null)
            : this(name, label, parameterNames,
                new DmnLiteralBoxedExpression(expression,
                    Parser.DmnExpressionLanguage.Resolve(expressionLanguage)),
                expression, expressionLanguage)
        {
        }

        public DmnBusinessKnowledgeModel(
            string name,
            string label,
            IReadOnlyList<string> parameterNames,
            DmnBoxedExpression body,
            string expression = null,
            string expressionLanguage = null)
        {
            Name = name;
            Label = label;
            ParameterNames = parameterNames ?? new List<string>();
            Body = body;
            Expression = expression ?? (body as DmnLiteralBoxedExpression)?.Text;
            ExpressionLanguage = expressionLanguage;
        }
    }
}
