using System;
using System.Collections.Generic;
using System.Linq;

namespace ScratchyDisk.DmnEngine.Engine.Definition
{
    /// <summary>
    /// Executable decision service: a named function over one or more output
    /// decisions, with input data and provided (input) decisions as parameters.
    /// </summary>
    public sealed class DmnDecisionService
    {
        /// <summary>Name of the decision service.</summary>
        public string Name { get; }

        /// <summary>
        /// Parameters in the order required for positional invocation. DMN orders
        /// the input data parameters before the input decision parameters.
        /// </summary>
        public IReadOnlyList<DmnDecisionServiceParameter> Parameters { get; }

        /// <summary>Per-parameter FEEL names (used for named parameters).</summary>
        public IReadOnlyList<string> ParameterNames { get; }

        /// <summary>Per-parameter execution variable names (used for binding).</summary>
        public IReadOnlyList<string> ParameterVariables { get; }

        /// <summary>Names of the decisions supplied as parameters, in parameter order.</summary>
        public IReadOnlyList<string> ParameterDecisionNames { get; }

        /// <summary>Declared FEEL types of the parameters, parallel to <see cref="ParameterNames"/>.</summary>
        public IReadOnlyList<string> ParameterTypes { get; }

        /// <summary>Output decision names, in declaration order.</summary>
        public IReadOnlyList<string> OutputDecisionNames { get; }

        /// <summary>Primary (first) output decision name.</summary>
        public string OutputDecisionName =>
            OutputDecisionNames.Count > 0 ? OutputDecisionNames[0] : null;

        /// <summary>Declared FEEL type of the service output (from the service variable typeRef).</summary>
        public string OutputTypeName { get; }

        public DmnDecisionService(string name, IReadOnlyList<DmnDecisionServiceParameter> parameters,
            IReadOnlyList<string> outputDecisionNames, string outputTypeName = null)
        {
            Name = name;
            Parameters = parameters ?? new List<DmnDecisionServiceParameter>();
            OutputDecisionNames = outputDecisionNames ?? new List<string>();
            OutputTypeName = outputTypeName;
            ParameterNames = Parameters.Select(p => p.Name).ToList();
            ParameterVariables = Parameters.Select(p => p.VariableName).ToList();
            ParameterDecisionNames = Parameters.Where(p => p.IsDecision).Select(p => p.DecisionName).ToList();
            ParameterTypes = Parameters.Select(p => p.FeelTypeName).ToList();
        }

        /// <summary>
        /// Finds the parameter index for a name supplied as a named argument or as
        /// a direct invocation input. Accepts the FEEL name, the execution variable
        /// name and the normalized variable name.
        /// </summary>
        /// <returns>Parameter index or -1 when no parameter matches.</returns>
        public int IndexOfParameter(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return -1;

            for (var i = 0; i < Parameters.Count; i++)
            {
                if (string.Equals(Parameters[i].Name, name, StringComparison.Ordinal)) return i;
            }
            for (var i = 0; i < Parameters.Count; i++)
            {
                if (string.Equals(Parameters[i].VariableName, name, StringComparison.Ordinal)) return i;
            }

            var normalized = DmnVariableDefinition.CanNormalizeVariableName(name, out var normalizedName, out _)
                ? normalizedName
                : null;
            if (normalized == null) return -1;

            for (var i = 0; i < Parameters.Count; i++)
            {
                if (string.Equals(Parameters[i].VariableName, normalized, StringComparison.Ordinal)) return i;
            }
            for (var i = 0; i < Parameters.Count; i++)
            {
                if (string.Equals(Parameters[i].Name, normalized, StringComparison.Ordinal)) return i;
            }
            return -1;
        }
    }

    /// <summary>One formal parameter of a decision service.</summary>
    public sealed class DmnDecisionServiceParameter
    {
        /// <summary>FEEL name of the parameter (as used for named invocation).</summary>
        public string Name { get; }

        /// <summary>Execution variable the parameter value is bound to.</summary>
        public string VariableName { get; }

        /// <summary>Decision supplied by the parameter; null for input data.</summary>
        public string DecisionName { get; }

        /// <summary>True when the parameter is an input decision, false for input data.</summary>
        public bool IsDecision => !string.IsNullOrWhiteSpace(DecisionName);

        /// <summary>Declared FEEL type name, or "any" when the model does not declare one.</summary>
        public string FeelTypeName { get; }

        public DmnDecisionServiceParameter(string name, string variableName, string decisionName = null,
            string feelTypeName = null)
        {
            Name = name;
            VariableName = variableName;
            DecisionName = decisionName;
            FeelTypeName = string.IsNullOrWhiteSpace(feelTypeName) ? "any" : feelTypeName;
        }

        /// <summary>
        /// True when the parameter is not checked against a type: undeclared ("any")
        /// and structured/collection types are not validated.
        /// </summary>
        public bool IsAnyType => string.IsNullOrWhiteSpace(FeelTypeName)
                                 || string.Equals(FeelTypeName, "any", StringComparison.Ordinal)
                                 || FeelTypeName.Contains('<');
    }
}
