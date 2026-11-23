using System.Collections.Generic;
using System.Xml.Serialization;
using ScratchyDisk.DmnEngine.Parser.Dto.Diagram;

namespace ScratchyDisk.DmnEngine.Parser.Dto
{
    /// <summary>
    /// Root of DMN definition XML - DMN Model
    /// </summary>
    public class DmnModel
    {
        /// <summary>Definitions namespace attribute.</summary>
        [XmlAttribute("namespace")]
        public string Namespace { get; set; }

        /// <summary>Definitions name attribute.</summary>
        [XmlAttribute("name")]
        public string Name { get; set; }

        /// <summary>Import declarations.</summary>
        [XmlElement("import")]
        public List<Import> Imports { get; set; }

        /// <summary>Source file path when parsed from a file.</summary>
        [XmlIgnore]
        public string SourcePath { get; set; }

        /// <summary>Aliases produced by import flattening.</summary>
        [XmlIgnore]
        public ModelImportAliases ImportAliases { get; set; }

        /// <summary>
        /// List of input data parameters to be used by <see cref="Decisions"/> for DMN Model
        /// </summary>
        [XmlElement("inputData")]
        public List<InputData> InputData { get; set; }

        /// <summary>
        /// List of <see cref="Decision">decisions</see> within DMN Model
        /// </summary>
        [XmlElement("decision")]
        public List<Decision> Decisions { get; set; }

        /// <summary>
        /// List of business knowledge models (reusable FEEL functions).
        /// </summary>
        [XmlElement("businessKnowledgeModel")]
        public List<BusinessKnowledgeModel> BusinessKnowledgeModels { get; set; }

        /// <summary>
        /// Named item definitions (aliases, structures and collections).
        /// </summary>
        [XmlElement("itemDefinition")]
        public List<ItemDefinition> ItemDefinitions { get; set; }

        /// <summary>
        /// Diagrams within DMN Model
        /// </summary>
        [XmlElement("DMNDI", Namespace = "https://www.omg.org/spec/DMN/20191111/DMNDI/")]
        public DmnDi DiagramExtension { get; set; }

        /// <summary>
        /// DMN Version used by parser
        /// </summary>
        [XmlIgnore]
        public DmnParser.DmnVersionEnum DmnVersion { get; set; }
    }
}
