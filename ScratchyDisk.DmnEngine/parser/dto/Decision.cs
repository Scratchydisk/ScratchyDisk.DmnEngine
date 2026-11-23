using System.Collections.Generic;
using System.Xml.Serialization;

namespace ScratchyDisk.DmnEngine.Parser.Dto
{
    /// <inheritdoc />
    /// <summary>
    /// Single decision within the DMN file
    /// It can be expression decision of decision table.
    /// Decision table is recognized by having the <see cref="DecisionTable"/> element, 
    /// otherwise, the expression decision is expected with elements <see cref="Expression"/> and <see cref="OutputVariable"/>
    /// </summary>
    public class Decision : NamedElement
    {
        /// <summary>
        /// List of inputs of decisions the decision depends on
        /// </summary>
        [XmlElement("informationRequirement")]
        public List<InformationRequirement> InformationRequirements { get; set; }

        /// <summary>
        /// References to business knowledge models required by the decision.
        /// </summary>
        [XmlElement("knowledgeRequirement")]
        public List<KnowledgeRequirement> KnowledgeRequirements { get; set; }

        //------------------------------------
        //Expression
        //------------------------------------

        /// <summary>
        /// Output variable for the expression decision
        /// </summary>
        [XmlElement("variable")]
        public Variable OutputVariable { get; set; }
        /// <summary>
        /// Expression of the expression decision
        /// </summary>
        [XmlElement("literalExpression")]
        public Expression Expression { get; set; }

        /// <summary>Function body of a decision that evaluates to a function.</summary>
        [XmlElement("functionDefinition")]
        public FunctionDefinition FunctionDefinition { get; set; }

        /// <summary>Boxed context body.</summary>
        [XmlElement("context")]
        public ContextExpression Context { get; set; }

        /// <summary>Boxed relation body.</summary>
        [XmlElement("relation")]
        public RelationExpression Relation { get; set; }

        /// <summary>Boxed list body.</summary>
        [XmlElement("list")]
        public ListExpression List { get; set; }

        /// <summary>Boxed invocation body.</summary>
        [XmlElement("invocation")]
        public InvocationExpression Invocation { get; set; }

        //------------------------------------
        //Decision table
        //------------------------------------

        /// <summary>
        /// Decision table encapsulating element
        /// </summary>
        [XmlElement("decisionTable")]
        public DecisionTable DecisionTable { get; set; }
        
    }
}
