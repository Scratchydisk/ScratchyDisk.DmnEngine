using System.Collections.Generic;

namespace ScratchyDisk.DmnEngine.Engine.Definition
{
    /// <summary>Executable decision service: a named function over an output decision.</summary>
    public sealed class DmnDecisionService
    {
        public string Name { get; }
        public IReadOnlyList<string> ParameterNames { get; }
        public IReadOnlyList<string> ParameterVariables { get; }
        public IReadOnlyList<string> ParameterDecisionNames { get; }

        /// <summary>Declared FEEL types of the parameters, parallel to <see cref="ParameterNames"/>.</summary>
        public IReadOnlyList<string> ParameterTypes { get; }

        public string OutputDecisionName { get; }

        /// <summary>All output decisions of the service, in model order.</summary>
        public IReadOnlyList<string> OutputDecisionNames { get; }

        /// <summary>Declared FEEL type of the service output (from the service variable typeRef).</summary>
        public string OutputTypeName { get; }

        public DmnDecisionService(string name, IReadOnlyList<string> parameterNames,
            IReadOnlyList<string> parameterVariables, string outputDecisionName,
            IReadOnlyList<string> parameterDecisionNames = null,
            IReadOnlyList<string> parameterTypes = null,
            string outputTypeName = null,
            IReadOnlyList<string> outputDecisionNames = null)
        {
            Name = name;
            ParameterNames = parameterNames ?? new List<string>();
            ParameterVariables = parameterVariables ?? new List<string>();
            ParameterDecisionNames = parameterDecisionNames ?? new List<string>();
            ParameterTypes = parameterTypes;
            OutputDecisionName = outputDecisionName;
            OutputDecisionNames = outputDecisionNames
                                  ?? (string.IsNullOrWhiteSpace(outputDecisionName)
                                      ? new List<string>()
                                      : new List<string> { outputDecisionName });
            OutputTypeName = outputTypeName;
        }
    }
}
