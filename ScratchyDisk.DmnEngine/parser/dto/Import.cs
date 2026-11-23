using System.Xml.Serialization;

namespace ScratchyDisk.DmnEngine.Parser.Dto
{
    /// <summary>DMN import declaration.</summary>
    public class Import : IdedElement
    {
        [XmlAttribute("namespace")]
        public string Namespace { get; set; }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("importType")]
        public string ImportType { get; set; }

        [XmlAttribute("locationURI")]
        public string LocationUri { get; set; }

        [XmlAttribute("location")]
        public string Location { get; set; }

        [XmlIgnore]
        public string LocationPath => LocationUri ?? Location;
    }
}
