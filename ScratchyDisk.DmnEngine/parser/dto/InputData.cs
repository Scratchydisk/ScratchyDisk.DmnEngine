using System.Xml.Serialization;

namespace ScratchyDisk.DmnEngine.Parser.Dto
{
    /// <inheritdoc />
    /// <summary>
    /// Input Data element
    /// Defines the input data item provided to the DMN model. The input parameter and related variable are defined by <see cref="P:ScratchyDisk.DmnEngine.Parser.Dto.NamedElement.Name" /> 
    /// </summary>
    public class InputData : NamedElement
    {
        /// <summary>
        /// <summary>
        /// Variable that carries the input data value (its typeRef declares the input type).
        /// </summary>
        [XmlElement("variable")]
        public Variable Variable { get; set; }
    }
}
