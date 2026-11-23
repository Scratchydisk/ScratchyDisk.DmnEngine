using System.Collections.Generic;
using System.Xml.Serialization;

namespace ScratchyDisk.DmnEngine.Parser.Dto
{
    /// <summary>Decision service declaration.</summary>
    public class DecisionService : NamedElement
    {
        [XmlElement("variable")]
        public Variable OutputVariable { get; set; }

        [XmlElement("outputDecision")]
        public DecisionServiceReference OutputDecision { get; set; }

        [XmlElement("encapsulatedDecision")]
        public List<DecisionServiceReference> EncapsulatedDecisions { get; set; }

        [XmlElement("inputDecision")]
        public List<DecisionServiceReference> InputDecisions { get; set; }

        [XmlElement("inputData")]
        public List<DecisionServiceReference> InputData { get; set; }
    }

    /// <summary>Reference to an element inside a decision service.</summary>
    public class DecisionServiceReference : IdedElement
    {
        [XmlAttribute("href")]
        public string Ref { get; set; }
    }
}
