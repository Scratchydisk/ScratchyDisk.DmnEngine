using System.Collections.Generic;
using System.Xml.Serialization;

namespace ScratchyDisk.DmnEngine.Parser.Dto
{
    /// <summary>
    /// Named DMN item definition. It either aliases a type, defines a structure
    /// through item components, or is a collection of one of those.
    /// </summary>
    public class ItemDefinition : NamedElement
    {
        /// <summary>
        /// True when the definition is a list/collection type.
        /// </summary>
        [XmlAttribute("isCollection")]
        public bool IsCollection { get; set; }

        /// <summary>
        /// Referenced type as an element (DMN 1.3+).
        /// </summary>
        [XmlElement("typeRef")]
        public string TypeRefElement { get; set; }

        /// <summary>
        /// Referenced type as an attribute (DMN 1.1/1.2 style).
        /// </summary>
        [XmlAttribute("typeRef")]
        public string TypeRefAttribute { get; set; }

        /// <summary>
        /// Structure components.
        /// </summary>
        [XmlElement("itemComponent")]
        public List<ItemComponent> ItemComponents { get; set; }

        /// <summary>
        /// Function item definition (a function type with formal parameters and an
        /// optional output type).
        /// </summary>
        [XmlElement("functionItem")]
        public FunctionItemDefinition FunctionItem { get; set; }

        /// <summary>
        /// Referenced type, whichever representation the model uses.
        /// </summary>
        [XmlIgnore]
        public string TypeRef => TypeRefElement ?? TypeRefAttribute;
    }

    /// <summary>
    /// A function item type: formal parameter types and an optional output type.
    /// </summary>
    public class FunctionItemDefinition
    {
        /// <summary>Declared output type of the function.</summary>
        [XmlAttribute("outputTypeRef")]
        public string OutputTypeRef { get; set; }

        /// <summary>Formal parameters of the function type.</summary>
        [XmlElement("formalParameter")]
        public List<Variable> FormalParameters { get; set; }
    }

    /// <summary>
    /// A component of a structured item definition.
    /// </summary>
    public class ItemComponent : NamedElement
    {
        /// <summary>
        /// True when the component is a list/collection.
        /// </summary>
        [XmlAttribute("isCollection")]
        public bool IsCollection { get; set; }

        /// <summary>
        /// Referenced type as an element.
        /// </summary>
        [XmlElement("typeRef")]
        public string TypeRefElement { get; set; }

        /// <summary>
        /// Referenced type as an attribute.
        /// </summary>
        [XmlAttribute("typeRef")]
        public string TypeRefAttribute { get; set; }

        /// <summary>
        /// Referenced type, whichever representation the model uses.
        /// </summary>
        [XmlIgnore]
        public string TypeRef => TypeRefElement ?? TypeRefAttribute;
    }
}
