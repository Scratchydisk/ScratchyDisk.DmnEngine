using System;
using System.Collections.Generic;
using System.Linq;
using ScratchyDisk.DmnEngine.Engine.Decisions;

namespace ScratchyDisk.DmnEngine.Engine.Definition
{
    /// <summary>
    /// DMN model definition for execution engine - encapsulates Decisions, Variables (incl. Input data)
    /// </summary>
    public class DmnDefinition : IDmnDefinition
    {
        /// <summary>
        /// Unique identifier of the definition (set at CTOR)
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Variables used while executing the DMN model - can be used within the Decision Tables and/or Expressions
        /// In general, it holds the Input Data of DMN model and outputs from Decision Tables and/or Expressions
        /// </summary>
        public IReadOnlyDictionary<string, IDmnVariable> Variables { get; }

        /// <summary>
        /// Input data interface. Input data are stored as Variables with <see cref="IDmnVariable.IsInputParameter"/> flag,
        /// complex objects are supported
        /// </summary>
        public IReadOnlyDictionary<string, IDmnVariable> InputData =>
            Variables
                .Where(v => v.Value.IsInputParameter)
                .ToDictionary(i => i.Key, i => i.Value);

        /// <summary>
        /// Dictionary of available decisions by name
        /// </summary>
        public IReadOnlyDictionary<string, IDmnDecision> Decisions { get; }

        /// <summary>
        /// Business knowledge models available to decision expressions, by name.
        /// </summary>
        public IReadOnlyDictionary<string, DmnBusinessKnowledgeModel> BusinessKnowledgeModels { get; }

        /// <summary>
        /// Item definition names mapped to their FEEL type name (for instance of).
        /// </summary>
        public IReadOnlyDictionary<string, string> ItemTypeNames { get; }

        /// <summary>Decision names mapped to the variable that holds their result.</summary>
        public IReadOnlyDictionary<string, string> DecisionAliases { get; }

        /// <summary>Imported names mapped to the definition variable that holds their value.</summary>
        public IReadOnlyDictionary<string, string> ImportAliases { get; }

        /// <summary>Imported decisions addressed by namespace#name.</summary>
        public IReadOnlyDictionary<string, string> QualifiedDecisionNames { get; }

        /// <summary>
        /// Maps input data variable names to expression variable aliases.
        /// When a DMN file uses different names in the DRD input data elements
        /// vs. the decision table input expressions (common in Camunda exports),
        /// this map tracks the relationship so input parameter values can be
        /// propagated to the expression variables at execution time.
        /// Key: input data variable name. Value: list of alias variable names.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> InputExpressionAliases { get; internal set; }
            = new Dictionary<string, IReadOnlyList<string>>();

        /// <summary>
        /// List of extensions that can be used to any related data.
        /// Engine doesn't neither manage nor touches the extensions
        /// </summary>
        public List<object> Extensions { get; } = new List<object>();

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="variables">Variables used while executing the DMN model</param>
        /// <param name="decisions">Dictionary of available decisions by name</param>
        public DmnDefinition(
            IReadOnlyDictionary<string, IDmnVariable> variables,
            IReadOnlyDictionary<string, IDmnDecision> decisions,
            IReadOnlyDictionary<string, DmnBusinessKnowledgeModel> businessKnowledgeModels = null,
            IReadOnlyDictionary<string, string> itemTypeNames = null,
            IReadOnlyDictionary<string, string> decisionAliases = null,
            IReadOnlyDictionary<string, string> importAliases = null,
            IReadOnlyDictionary<string, string> qualifiedDecisionNames = null)
        {
            Id = Guid.NewGuid().ToString();
            Variables = variables;
            Decisions = decisions;
            BusinessKnowledgeModels = businessKnowledgeModels
                                      ?? new Dictionary<string, DmnBusinessKnowledgeModel>();
            ItemTypeNames = itemTypeNames ?? new Dictionary<string, string>();
            DecisionAliases = decisionAliases ?? new Dictionary<string, string>();
            ImportAliases = importAliases ?? new Dictionary<string, string>();
            QualifiedDecisionNames = qualifiedDecisionNames ?? new Dictionary<string, string>();
        }
    }
}
