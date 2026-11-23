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
    public class Decision : NamedElement, IBoxedExpressionContainer
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

        /// <summary>Boxed conditional (if/then/else) body.</summary>
        [XmlElement("conditional")]
        public ConditionalExpression Conditional { get; set; }

        /// <summary>Boxed for body.</summary>
        [XmlElement("for")]
        public ForExpression For { get; set; }

        /// <summary>Boxed some body.</summary>
        [XmlElement("some")]
        public QuantifiedExpression Some { get; set; }

        /// <summary>Boxed every body.</summary>
        [XmlElement("every")]
        public QuantifiedExpression Every { get; set; }

        /// <summary>Boxed filter body.</summary>
        [XmlElement("filter")]
        public FilterExpression Filter { get; set; }

        //------------------------------------
        //Decision table
        //------------------------------------

        /// <summary>
        /// Decision table encapsulating element
        /// </summary>
        [XmlElement("decisionTable")]
        public DecisionTable DecisionTable { get; set; }

        /// <summary>
        /// Child elements this DTO does not model. A decision with an unrecognised child
        /// element can be told apart from a decision that declares no logic at all.
        /// </summary>
        [XmlAnyElement]
        public List<System.Xml.XmlElement> UnknownElements { get; set; }

    }
}
