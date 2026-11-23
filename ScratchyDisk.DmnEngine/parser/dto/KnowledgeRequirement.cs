using System.Xml.Serialization;

namespace ScratchyDisk.DmnEngine.Parser.Dto
{
    /// <summary>
    /// Knowledge requirement of a decision: a reference to a business knowledge model.
    /// </summary>
    public class KnowledgeRequirement : IdedElement
    {
        /// <summary>
        /// Reference to the required knowledge.
        /// </summary>
        [XmlElement("requiredKnowledge")]
        public RequiredKnowledge Reference { get; set; }

        /// <summary>
        /// Reference holder.
        /// </summary>
        public class RequiredKnowledge
        {
            /// <summary>
            /// Reference to the BKM by id, prefixed with "#".
            /// </summary>
            [XmlAttribute("href")]
            public string Ref { get; set; }
        }
    }
}
