using System.Collections.Generic;
using System.Xml.Serialization;

namespace ScratchyDisk.DmnEngine.Parser.Dto
{
    /// <summary>
    /// Business knowledge model (a reusable FEEL function) within the DMN file.
    /// </summary>
    public class BusinessKnowledgeModel : NamedElement
    {
        /// <summary>
        /// Output variable of the BKM (the function name).
        /// </summary>
        [XmlElement("variable")]
        public Variable OutputVariable { get; set; }

        /// <summary>
        /// Encapsulated function logic.
        /// </summary>
        [XmlElement("encapsulatedLogic")]
        public FunctionDefinition EncapsulatedLogic { get; set; }
    }
}
