using System.Collections.Generic;
using System.Xml.Serialization;

namespace ScratchyDisk.DmnEngine.Parser.Dto
{
    /// <summary>Boxed context expression (a decision returning a context).</summary>
    public class ContextExpression : IdedElement
    {
        [XmlElement("contextEntry")]
        public List<ContextEntry> Entries { get; set; }
    }

    /// <summary>One context entry.</summary>
    public class ContextEntry : IdedElement
    {
        [XmlElement("variable")]
        public Variable Variable { get; set; }

        [XmlElement("literalExpression")]
        public Expression Expression { get; set; }

        [XmlElement("context")]
        public ContextExpression Context { get; set; }

        [XmlElement("list")]
        public ListExpression List { get; set; }

        [XmlElement("relation")]
        public RelationExpression Relation { get; set; }

        [XmlElement("invocation")]
        public InvocationExpression Invocation { get; set; }

        /// <summary>Nested function definition (a context entry holding a FEEL function).</summary>
        [XmlElement("functionDefinition")]
        public FunctionDefinition FunctionDefinition { get; set; }

        /// <summary>Nested decision table (a context entry calculated by a table).</summary>
        [XmlElement("decisionTable")]
        public DecisionTable DecisionTable { get; set; }
    }

    /// <summary>Boxed relation expression (a decision returning a list of contexts).</summary>
    public class RelationExpression : IdedElement
    {
        [XmlElement("column")]
        public List<RelationColumn> Columns { get; set; }

        [XmlElement("row")]
        public List<RelationRow> Rows { get; set; }
    }

    /// <summary>Relation column.</summary>
    public class RelationColumn : NamedElement
    {
    }

    /// <summary>Relation row.</summary>
    public class RelationRow : IdedElement
    {
        [XmlElement("literalExpression")]
        public List<Expression> Expressions { get; set; }
    }

    /// <summary>Boxed list expression.</summary>
    public class ListExpression : IdedElement
    {
        [XmlElement("literalExpression")]
        public List<Expression> Expressions { get; set; }
    }

    /// <summary>Boxed invocation expression.</summary>
    public class InvocationExpression : IdedElement
    {
        /// <summary>Optional data type of the invocation result.</summary>
        [XmlAttribute("typeRef")]
        public string TypeRef { get; set; }

        [XmlElement("literalExpression")]
        public Expression Function { get; set; }

        [XmlElement("binding")]
        public List<InvocationBinding> Bindings { get; set; }
    }

    /// <summary>Named argument binding of an invocation.</summary>
    public class InvocationBinding : IdedElement
    {
        [XmlElement("parameter")]
        public Variable Parameter { get; set; }

        [XmlElement("literalExpression")]
        public Expression Expression { get; set; }
    }
}
