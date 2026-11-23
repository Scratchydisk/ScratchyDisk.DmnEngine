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

    /// <summary>
    /// A parsed element that can hold any boxed expression kind as its value. Implemented
    /// by the elements whose child expression is a boxed expression (decision body, context
    /// entry and the child slots of the control-flow constructs).
    /// </summary>
    public interface IBoxedExpressionContainer
    {
        /// <summary>Literal (textual FEEL) expression.</summary>
        Expression Expression { get; }

        /// <summary>Boxed context.</summary>
        ContextExpression Context { get; }

        /// <summary>Boxed list.</summary>
        ListExpression List { get; }

        /// <summary>Boxed relation.</summary>
        RelationExpression Relation { get; }

        /// <summary>Boxed invocation.</summary>
        InvocationExpression Invocation { get; }

        /// <summary>Nested function definition.</summary>
        FunctionDefinition FunctionDefinition { get; }

        /// <summary>Nested decision table.</summary>
        DecisionTable DecisionTable { get; }

        /// <summary>Boxed conditional.</summary>
        ConditionalExpression Conditional { get; }

        /// <summary>Boxed for.</summary>
        ForExpression For { get; }

        /// <summary>Boxed some.</summary>
        QuantifiedExpression Some { get; }

        /// <summary>Boxed every.</summary>
        QuantifiedExpression Every { get; }

        /// <summary>Boxed filter.</summary>
        FilterExpression Filter { get; }
    }

    /// <summary>
    /// One child slot of a boxed construct (if/then/else, in/return, in/satisfies, in/match).
    /// In DMN every such slot holds any expression, including another boxed construct.
    /// </summary>
    public class BoxedExpressionBody : IdedElement, IBoxedExpressionContainer
    {
        /// <inheritdoc />
        [XmlElement("literalExpression")]
        public Expression Expression { get; set; }

        /// <inheritdoc />
        [XmlElement("context")]
        public ContextExpression Context { get; set; }

        /// <inheritdoc />
        [XmlElement("list")]
        public ListExpression List { get; set; }

        /// <inheritdoc />
        [XmlElement("relation")]
        public RelationExpression Relation { get; set; }

        /// <inheritdoc />
        [XmlElement("invocation")]
        public InvocationExpression Invocation { get; set; }

        /// <inheritdoc />
        [XmlElement("functionDefinition")]
        public FunctionDefinition FunctionDefinition { get; set; }

        /// <inheritdoc />
        [XmlElement("decisionTable")]
        public DecisionTable DecisionTable { get; set; }

        /// <inheritdoc />
        [XmlElement("conditional")]
        public ConditionalExpression Conditional { get; set; }

        /// <inheritdoc />
        [XmlElement("for")]
        public ForExpression For { get; set; }

        /// <inheritdoc />
        [XmlElement("some")]
        public QuantifiedExpression Some { get; set; }

        /// <inheritdoc />
        [XmlElement("every")]
        public QuantifiedExpression Every { get; set; }

        /// <inheritdoc />
        [XmlElement("filter")]
        public FilterExpression Filter { get; set; }
    }

    /// <summary>Boxed conditional expression (if/then/else).</summary>
    public class ConditionalExpression : IdedElement
    {
        [XmlElement("if")]
        public BoxedExpressionBody If { get; set; }

        [XmlElement("then")]
        public BoxedExpressionBody Then { get; set; }

        [XmlElement("else")]
        public BoxedExpressionBody Else { get; set; }
    }

    /// <summary>Boxed for expression: maps the values of <see cref="In"/> with <see cref="Return"/>.</summary>
    public class ForExpression : IdedElement
    {
        /// <summary>Name the iteration variable is bound to.</summary>
        [XmlAttribute("iteratorVariable")]
        public string IteratorVariable { get; set; }

        [XmlElement("in")]
        public BoxedExpressionBody In { get; set; }

        [XmlElement("return")]
        public BoxedExpressionBody Return { get; set; }
    }

    /// <summary>
    /// Boxed quantified expression: either <c>some</c> or <c>every</c> values of
    /// <see cref="In"/> satisfy <see cref="Satisfies"/>.
    /// </summary>
    public class QuantifiedExpression : IdedElement
    {
        /// <summary>Name the iteration variable is bound to.</summary>
        [XmlAttribute("iteratorVariable")]
        public string IteratorVariable { get; set; }

        [XmlElement("in")]
        public BoxedExpressionBody In { get; set; }

        [XmlElement("satisfies")]
        public BoxedExpressionBody Satisfies { get; set; }
    }

    /// <summary>Boxed filter expression: the values of <see cref="In"/> matching <see cref="Match"/>.</summary>
    public class FilterExpression : IdedElement
    {
        [XmlElement("in")]
        public BoxedExpressionBody In { get; set; }

        [XmlElement("match")]
        public BoxedExpressionBody Match { get; set; }
    }

    /// <summary>One context entry.</summary>
    public class ContextEntry : IdedElement, IBoxedExpressionContainer
    {
        [XmlElement("variable")]
        public Variable Variable { get; set; }

        /// <inheritdoc />
        [XmlElement("literalExpression")]
        public Expression Expression { get; set; }

        /// <inheritdoc />
        [XmlElement("context")]
        public ContextExpression Context { get; set; }

        /// <inheritdoc />
        [XmlElement("list")]
        public ListExpression List { get; set; }

        /// <inheritdoc />
        [XmlElement("relation")]
        public RelationExpression Relation { get; set; }

        /// <inheritdoc />
        [XmlElement("invocation")]
        public InvocationExpression Invocation { get; set; }

        /// <summary>Nested function definition (a context entry holding a FEEL function).</summary>
        [XmlElement("functionDefinition")]
        public FunctionDefinition FunctionDefinition { get; set; }

        /// <summary>Nested decision table (a context entry calculated by a table).</summary>
        [XmlElement("decisionTable")]
        public DecisionTable DecisionTable { get; set; }

        /// <inheritdoc />
        [XmlElement("conditional")]
        public ConditionalExpression Conditional { get; set; }

        /// <inheritdoc />
        [XmlElement("for")]
        public ForExpression For { get; set; }

        /// <inheritdoc />
        [XmlElement("some")]
        public QuantifiedExpression Some { get; set; }

        /// <inheritdoc />
        [XmlElement("every")]
        public QuantifiedExpression Every { get; set; }

        /// <inheritdoc />
        [XmlElement("filter")]
        public FilterExpression Filter { get; set; }
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
