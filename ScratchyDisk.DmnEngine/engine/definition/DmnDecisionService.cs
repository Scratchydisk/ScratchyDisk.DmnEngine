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
        public string OutputDecisionName { get; }

        public DmnDecisionService(string name, IReadOnlyList<string> parameterNames,
            IReadOnlyList<string> parameterVariables, string outputDecisionName,
            IReadOnlyList<string> parameterDecisionNames = null)
        {
            Name = name;
            ParameterNames = parameterNames ?? new List<string>();
            ParameterVariables = parameterVariables ?? new List<string>();
            ParameterDecisionNames = parameterDecisionNames ?? new List<string>();
            OutputDecisionName = outputDecisionName;
        }
    }
}
