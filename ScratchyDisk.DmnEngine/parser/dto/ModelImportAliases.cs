using System.Collections.Generic;

namespace ScratchyDisk.DmnEngine.Parser.Dto
{
    /// <summary>
    /// Name/alias maps produced when a root model is flattened with its imports.
    /// </summary>
    public sealed class ModelImportAliases
    {
        /// <summary>Global decision name -> local aliases used inside its model.</summary>
        public Dictionary<string, IReadOnlyDictionary<string, string>> DecisionAliases { get; } = new();

        /// <summary>Global BKM name -> local aliases used inside its model.</summary>
        public Dictionary<string, IReadOnlyDictionary<string, string>> BkmAliases { get; } = new();

        /// <summary>Unqualified or namespace#name input -> global input name, for binding.</summary>
        public Dictionary<string, string> InputAliases { get; } = new();

        /// <summary>namespace#decision-name -> global decision name.</summary>
        public Dictionary<string, string> QualifiedDecisionNames { get; } = new();

        /// <summary>namespace#input-name -> global input name.</summary>
        public Dictionary<string, string> QualifiedInputNames { get; } = new();
    }
}
