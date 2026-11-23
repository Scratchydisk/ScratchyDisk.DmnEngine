using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace ScratchyDisk.DmnEngine.Parser.Dto
{
    /// <inheritdoc />
    /// <summary>
    /// Decision table output definition
    /// </summary>
    public class DecisionTableOutput : NamedElement
    {
        /// <summary>
        /// Label ouf the output 
        /// </summary>
        [XmlAttribute("label")]
        public string Label { get; set; }

        /// <summary>
        /// Type of the output variable
        /// </summary>
        [XmlAttribute("typeRef")]
        public string TypeRef { get; set; }

        /// <summary>
        /// Allowed output values
        /// </summary>
        [XmlElement("outputValues")]
        public AllowedValues AllowedOutputValues { get; set; }

        /// <summary>Default output expression (non-standard text form used by some exporters).</summary>
        [XmlElement("text")]
        public string DefaultText { get; set; }

        /// <summary>Default output entry.</summary>
        [XmlElement("defaultOutputEntry")]
        public Expression DefaultOutputEntry { get; set; }

        /// <inheritdoc />
        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"{Name}{(string.IsNullOrWhiteSpace(TypeRef) ? "" : ":" + TypeRef)}/{Label}";
        }
    }
}
