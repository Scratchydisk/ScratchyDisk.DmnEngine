using System.Collections.Generic;
using System.Xml.Serialization;

namespace ScratchyDisk.DmnEngine.Parser.Dto
{
    /// <summary>
    /// Function definition used by business knowledge models.
    /// </summary>
    public class FunctionDefinition : IdedElement
    {
        /// <summary>
        /// Optional output type of the function.
        /// </summary>
        [XmlAttribute("typeRef")]
        public string TypeRef { get; set; }

        /// <summary>
        /// Function kind (for example "FEEL" or "ONNX").
        /// </summary>
        [XmlAttribute("kind")]
        public string Kind { get; set; }

        /// <summary>
        /// Formal parameters of the function.
        /// </summary>
        [XmlElement("formalParameter")]
        public List<Variable> FormalParameters { get; set; }

        /// <summary>
        /// Function body when expressed as a literal expression.
        /// </summary>
        [XmlElement("literalExpression")]
        public Expression Expression { get; set; }

        /// <summary>
        /// Function body when it is itself a function definition (a curried lambda).
        /// </summary>
        [XmlElement("functionDefinition")]
        public FunctionDefinition Body { get; set; }

        /// <summary>Function body when expressed as a boxed context.</summary>
        [XmlElement("context")]
        public ContextExpression Context { get; set; }

        /// <summary>Function body when expressed as a boxed relation.</summary>
        [XmlElement("relation")]
        public RelationExpression Relation { get; set; }

        /// <summary>Function body when expressed as a boxed list.</summary>
        [XmlElement("list")]
        public ListExpression List { get; set; }

        /// <summary>Function body when expressed as a boxed invocation.</summary>
        [XmlElement("invocation")]
        public InvocationExpression Invocation { get; set; }

        /// <summary>Function body when expressed as a boxed decision table.</summary>
        [XmlElement("decisionTable")]
        public DecisionTable DecisionTable { get; set; }

        /// <summary>Function body when expressed as a boxed conditional.</summary>
        [XmlElement("conditional")]
        public ConditionalExpression Conditional { get; set; }

        /// <summary>Function body when expressed as a boxed for.</summary>
        [XmlElement("for")]
        public ForExpression For { get; set; }

        /// <summary>Function body when expressed as a boxed some.</summary>
        [XmlElement("some")]
        public QuantifiedExpression Some { get; set; }

        /// <summary>Function body when expressed as a boxed every.</summary>
        [XmlElement("every")]
        public QuantifiedExpression Every { get; set; }

        /// <summary>Function body when expressed as a boxed filter.</summary>
        [XmlElement("filter")]
        public FilterExpression Filter { get; set; }
    }
}
