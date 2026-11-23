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
    }
}
