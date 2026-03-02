using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Ast;
using ScratchyDisk.DmnEngine.Feel.Parsing;

namespace ScratchyDisk.DmnEngine.Test.Unit.Feel
{
    [TestClass]
    [TestCategory("FEEL Parser")]
    public class FeelParserTests
    {
        private readonly FeelEngine engine = new();

        // ==================== Literals ====================

        [TestMethod]
        public void ParseIntegerLiteral()
        {
            var ast = engine.ParseExpression("42");
            ast.Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(42m);
        }

        [TestMethod]
        public void ParseFloatLiteral()
        {
            var ast = engine.ParseExpression("3.14");
            ast.Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(3.14m);
        }

        [TestMethod]
        public void ParseStringLiteral()
        {
            var ast = engine.ParseExpression("\"hello world\"");
            ast.Should().BeOfType<StringLiteral>().Which.Value.Should().Be("hello world");
        }

        [TestMethod]
        public void ParseStringLiteralWithEscapes()
        {
            var ast = engine.ParseExpression("\"line1\\nline2\"");
            ast.Should().BeOfType<StringLiteral>().Which.Value.Should().Be("line1\nline2");
        }

        [TestMethod]
        public void ParseBoolTrue()
        {
            var ast = engine.ParseExpression("true");
            ast.Should().BeOfType<BoolLiteral>().Which.Value.Should().BeTrue();
        }

        [TestMethod]
        public void ParseBoolFalse()
        {
            var ast = engine.ParseExpression("false");
            ast.Should().BeOfType<BoolLiteral>().Which.Value.Should().BeFalse();
        }

        [TestMethod]
        public void ParseNullLiteral()
        {
            var ast = engine.ParseExpression("null");
            ast.Should().BeOfType<NullLiteral>();
        }

        [TestMethod]
        public void ParseAtLiteral()
        {
            var ast = engine.ParseExpression("@\"2024-01-15\"");
            ast.Should().BeOfType<DateTimeLiteral>().Which.RawValue.Should().Be("2024-01-15");
        }

        // ==================== Names ====================

        [TestMethod]
        public void ParseSimpleName()
        {
            var ast = engine.ParseExpression("myVariable");
            ast.Should().BeOfType<NameNode>().Which.Name.Should().Be("myVariable");
        }

        // ==================== Arithmetic ====================

        [TestMethod]
        public void ParseAddition()
        {
            var ast = engine.ParseExpression("1 + 2");
            var bin = ast.Should().BeOfType<BinaryOp>().Subject;
            bin.Op.Should().Be(BinaryOperator.Add);
            bin.Left.Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(1m);
            bin.Right.Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(2m);
        }

        [TestMethod]
        public void ParseSubtraction()
        {
            var ast = engine.ParseExpression("10 - 3");
            var bin = ast.Should().BeOfType<BinaryOp>().Subject;
            bin.Op.Should().Be(BinaryOperator.Sub);
        }

        [TestMethod]
        public void ParseMultiplication()
        {
            var ast = engine.ParseExpression("4 * 5");
            var bin = ast.Should().BeOfType<BinaryOp>().Subject;
            bin.Op.Should().Be(BinaryOperator.Mul);
        }

        [TestMethod]
        public void ParseDivision()
        {
            var ast = engine.ParseExpression("10 / 2");
            var bin = ast.Should().BeOfType<BinaryOp>().Subject;
            bin.Op.Should().Be(BinaryOperator.Div);
        }

        [TestMethod]
        public void ParseExponentiation()
        {
            var ast = engine.ParseExpression("2 ** 3");
            var bin = ast.Should().BeOfType<BinaryOp>().Subject;
            bin.Op.Should().Be(BinaryOperator.Exp);
        }

        [TestMethod]
        public void ParsePrecedence_MulBeforeAdd()
        {
            // 1 + 2 * 3 should parse as 1 + (2 * 3)
            var ast = engine.ParseExpression("1 + 2 * 3");
            var add = ast.Should().BeOfType<BinaryOp>().Subject;
            add.Op.Should().Be(BinaryOperator.Add);
            add.Left.Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(1m);
            var mul = add.Right.Should().BeOfType<BinaryOp>().Subject;
            mul.Op.Should().Be(BinaryOperator.Mul);
        }

        [TestMethod]
        public void ParsePrecedence_ParenOverride()
        {
            // (1 + 2) * 3 should parse as (1 + 2) * 3
            var ast = engine.ParseExpression("(1 + 2) * 3");
            var mul = ast.Should().BeOfType<BinaryOp>().Subject;
            mul.Op.Should().Be(BinaryOperator.Mul);
            mul.Left.Should().BeOfType<BinaryOp>().Which.Op.Should().Be(BinaryOperator.Add);
        }

        [TestMethod]
        public void ParseUnaryMinus()
        {
            var ast = engine.ParseExpression("-5");
            var neg = ast.Should().BeOfType<UnaryMinus>().Subject;
            neg.Operand.Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(5m);
        }

        [TestMethod]
        public void ParseExponentiationRightAssociative()
        {
            // 2 ** 3 ** 2 should parse as 2 ** (3 ** 2)
            var ast = engine.ParseExpression("2 ** 3 ** 2");
            var outer = ast.Should().BeOfType<BinaryOp>().Subject;
            outer.Op.Should().Be(BinaryOperator.Exp);
            outer.Left.Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(2m);
            var inner = outer.Right.Should().BeOfType<BinaryOp>().Subject;
            inner.Op.Should().Be(BinaryOperator.Exp);
            inner.Left.Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(3m);
            inner.Right.Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(2m);
        }

        // ==================== Comparison ====================

        [TestMethod]
        public void ParseLessThan()
        {
            var ast = engine.ParseExpression("x < 10");
            var cmp = ast.Should().BeOfType<Comparison>().Subject;
            cmp.Op.Should().Be(ComparisonOperator.Lt);
        }

        [TestMethod]
        public void ParseGreaterThanOrEqual()
        {
            var ast = engine.ParseExpression("x >= 5");
            var cmp = ast.Should().BeOfType<Comparison>().Subject;
            cmp.Op.Should().Be(ComparisonOperator.Gte);
        }

        [TestMethod]
        public void ParseNotEqual()
        {
            var ast = engine.ParseExpression("x != 0");
            var cmp = ast.Should().BeOfType<Comparison>().Subject;
            cmp.Op.Should().Be(ComparisonOperator.Neq);
        }

        [TestMethod]
        public void ParseBetween()
        {
            var ast = engine.ParseExpression("x between 1 and 10");
            var between = ast.Should().BeOfType<Between>().Subject;
            between.Value.Should().BeOfType<NameNode>();
            between.Low.Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(1m);
            between.High.Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(10m);
        }

        [TestMethod]
        public void ParseInstanceOf()
        {
            var ast = engine.ParseExpression("x instance of number");
            var io = ast.Should().BeOfType<InstanceOf>().Subject;
            io.TypeName.Should().Be("number");
        }

        // ==================== Logic ====================

        [TestMethod]
        public void ParseConjunction()
        {
            var ast = engine.ParseExpression("x > 1 and x < 10");
            var conj = ast.Should().BeOfType<Conjunction>().Subject;
            conj.Operands.Should().HaveCount(2);
        }

        [TestMethod]
        public void ParseDisjunction()
        {
            var ast = engine.ParseExpression("x = 1 or x = 2");
            var disj = ast.Should().BeOfType<Disjunction>().Subject;
            disj.Operands.Should().HaveCount(2);
        }

        [TestMethod]
        public void ParsePrecedence_AndBeforeOr()
        {
            // a or b and c should parse as a or (b and c)
            var ast = engine.ParseExpression("a = 1 or b = 2 and c = 3");
            var disj = ast.Should().BeOfType<Disjunction>().Subject;
            disj.Operands.Should().HaveCount(2);
            disj.Operands[0].Should().BeOfType<Comparison>();
            disj.Operands[1].Should().BeOfType<Conjunction>();
        }

        // ==================== Control Flow ====================

        [TestMethod]
        public void ParseIfThenElse()
        {
            var ast = engine.ParseExpression("if x > 5 then \"high\" else \"low\"");
            var ifNode = ast.Should().BeOfType<IfNode>().Subject;
            ifNode.Condition.Should().BeOfType<Comparison>();
            ifNode.ThenBranch.Should().BeOfType<StringLiteral>().Which.Value.Should().Be("high");
            ifNode.ElseBranch.Should().BeOfType<StringLiteral>().Which.Value.Should().Be("low");
        }

        [TestMethod]
        public void ParseForExpression()
        {
            var ast = engine.ParseExpression("for x in [1, 2, 3] return x * 2");
            var forNode = ast.Should().BeOfType<ForNode>().Subject;
            forNode.Iterations.Should().HaveCount(1);
            forNode.Iterations[0].VariableName.Should().Be("x");
            forNode.Iterations[0].ListExpression.Should().BeOfType<ListNode>();
            forNode.ReturnExpression.Should().BeOfType<BinaryOp>();
        }

        [TestMethod]
        public void ParseForExpressionRange()
        {
            var ast = engine.ParseExpression("for i in 1..10 return i");
            var forNode = ast.Should().BeOfType<ForNode>().Subject;
            forNode.Iterations[0].RangeEnd.Should().NotBeNull();
        }

        [TestMethod]
        public void ParseQuantifiedSome()
        {
            var ast = engine.ParseExpression("some x in [1, 2, 3] satisfies x > 2");
            var q = ast.Should().BeOfType<QuantifiedNode>().Subject;
            q.Quantifier.Should().Be(QuantifierType.Some);
            q.Iterations.Should().HaveCount(1);
        }

        [TestMethod]
        public void ParseQuantifiedEvery()
        {
            var ast = engine.ParseExpression("every x in [1, 2, 3] satisfies x > 0");
            var q = ast.Should().BeOfType<QuantifiedNode>().Subject;
            q.Quantifier.Should().Be(QuantifierType.Every);
        }

        // ==================== Collections ====================

        [TestMethod]
        public void ParseList()
        {
            var ast = engine.ParseExpression("[1, 2, 3]");
            var list = ast.Should().BeOfType<ListNode>().Subject;
            list.Elements.Should().HaveCount(3);
            list.Elements[0].Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(1m);
        }

        [TestMethod]
        public void ParseEmptyList()
        {
            var ast = engine.ParseExpression("[]");
            var list = ast.Should().BeOfType<ListNode>().Subject;
            list.Elements.Should().BeEmpty();
        }

        [TestMethod]
        public void ParseContext()
        {
            var ast = engine.ParseExpression("{ name: \"John\", age: 30 }");
            var ctx = ast.Should().BeOfType<ContextNode>().Subject;
            ctx.Entries.Should().HaveCount(2);
            ctx.Entries[0].Key.Should().Be("name");
            ctx.Entries[0].Value.Should().BeOfType<StringLiteral>().Which.Value.Should().Be("John");
            ctx.Entries[1].Key.Should().Be("age");
            ctx.Entries[1].Value.Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(30m);
        }

        [TestMethod]
        public void ParseEmptyContext()
        {
            var ast = engine.ParseExpression("{}");
            var ctx = ast.Should().BeOfType<ContextNode>().Subject;
            ctx.Entries.Should().BeEmpty();
        }

        // ==================== Postfix ====================

        [TestMethod]
        public void ParseMemberAccess()
        {
            var ast = engine.ParseExpression("person.name");
            var path = ast.Should().BeOfType<PathNode>().Subject;
            path.Source.Should().BeOfType<NameNode>().Which.Name.Should().Be("person");
            path.Member.Should().Be("name");
        }

        [TestMethod]
        public void ParseFilter()
        {
            var ast = engine.ParseExpression("items[price > 10]");
            var filter = ast.Should().BeOfType<FilterNode>().Subject;
            filter.Source.Should().BeOfType<NameNode>().Which.Name.Should().Be("items");
            filter.Filter.Should().BeOfType<Comparison>();
        }

        [TestMethod]
        public void ParseFunctionInvocation()
        {
            var ast = engine.ParseExpression("abs(-5)");
            var invocation = ast.Should().BeOfType<FunctionInvocation>().Subject;
            invocation.Function.Should().BeOfType<NameNode>().Which.Name.Should().Be("abs");
            invocation.Arguments.Should().HaveCount(1);
        }

        [TestMethod]
        public void ParseFunctionInvocationNoArgs()
        {
            var ast = engine.ParseExpression("now()");
            var invocation = ast.Should().BeOfType<FunctionInvocation>().Subject;
            invocation.Function.Should().BeOfType<NameNode>().Which.Name.Should().Be("now");
            invocation.Arguments.Should().BeEmpty();
        }

        [TestMethod]
        public void ParseNamedArgInvocation()
        {
            var ast = engine.ParseExpression("substring(string: \"hello\", start: 2)");
            var invocation = ast.Should().BeOfType<NamedFunctionInvocation>().Subject;
            invocation.Arguments.Should().HaveCount(2);
            invocation.Arguments[0].Name.Should().Be("string");
            invocation.Arguments[1].Name.Should().Be("start");
        }

        [TestMethod]
        public void ParseChainedMemberAccess()
        {
            var ast = engine.ParseExpression("a.b.c");
            var outer = ast.Should().BeOfType<PathNode>().Subject;
            outer.Member.Should().Be("c");
            var inner = outer.Source.Should().BeOfType<PathNode>().Subject;
            inner.Member.Should().Be("b");
            inner.Source.Should().BeOfType<NameNode>().Which.Name.Should().Be("a");
        }

        // ==================== Function Definition ====================

        [TestMethod]
        public void ParseFunctionDefinition()
        {
            var ast = engine.ParseExpression("function(x, y) x + y");
            var fn = ast.Should().BeOfType<FunctionDefinitionNode>().Subject;
            fn.Parameters.Should().BeEquivalentTo(new[] { "x", "y" });
            fn.Body.Should().BeOfType<BinaryOp>();
            fn.IsExternal.Should().BeFalse();
        }

        // ==================== Unary Tests ====================

        [TestMethod]
        public void ParseUnaryTestDash()
        {
            var ast = engine.ParseSimpleUnaryTests("-");
            ast.Should().BeOfType<Dash>();
        }

        [TestMethod]
        public void ParseUnaryTestComparison()
        {
            var ast = engine.ParseSimpleUnaryTests("> 5");
            // > 5 as a unary test — implicit left operand (input value)
            var cmp = ast.Should().BeOfType<Comparison>().Subject;
            cmp.Op.Should().Be(ComparisonOperator.Gt);
            cmp.Left.Should().BeNull("the left operand is implicit in unary tests");
            cmp.Right.Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(5m);
        }

        [TestMethod]
        public void ParseUnaryTestInterval()
        {
            var ast = engine.ParseSimpleUnaryTests("1..10");
            var range = ast.Should().BeOfType<RangeNode>().Subject;
            range.Low.Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(1m);
            range.High.Should().BeOfType<NumberLiteral>().Which.Value.Should().Be(10m);
        }

        [TestMethod]
        public void ParseUnaryTestMultiple()
        {
            var ast = engine.ParseSimpleUnaryTests("1, 2, 3");
            var tests = ast.Should().BeOfType<UnaryTests>().Subject;
            tests.Tests.Should().HaveCount(3);
            tests.IsNegated.Should().BeFalse();
        }

        [TestMethod]
        public void ParseUnaryTestNegated()
        {
            var ast = engine.ParseSimpleUnaryTests("not(1, 2)");
            var tests = ast.Should().BeOfType<UnaryTests>().Subject;
            tests.IsNegated.Should().BeTrue();
        }

        [TestMethod]
        public void ParseUnaryTestNull()
        {
            var ast = engine.ParseSimpleUnaryTests("null");
            ast.Should().BeOfType<NullLiteral>();
        }

        // ==================== Complex Expressions ====================

        [TestMethod]
        public void ParseNestedIfInContext()
        {
            var ast = engine.ParseExpression("{ result: if x > 0 then \"positive\" else \"non-positive\" }");
            var ctx = ast.Should().BeOfType<ContextNode>().Subject;
            ctx.Entries[0].Value.Should().BeOfType<IfNode>();
        }

        [TestMethod]
        public void ParseForWithMultipleIterations()
        {
            var ast = engine.ParseExpression("for x in [1, 2], y in [3, 4] return x + y");
            var forNode = ast.Should().BeOfType<ForNode>().Subject;
            forNode.Iterations.Should().HaveCount(2);
        }

        [TestMethod]
        public void ParseInListTest()
        {
            var ast = engine.ParseExpression("x in (1, 2, 3)");
            var inNode = ast.Should().BeOfType<InNode>().Subject;
            inNode.Value.Should().BeOfType<NameNode>();
        }

        // ==================== Multi-word Name Resolution ====================

        [TestMethod]
        public void ParseMultiWordFunctionName()
        {
            var scope = new FeelScope();
            var ast = engine.ParseExpression("string length(\"hello\")", scope);
            var invocation = ast.Should().BeOfType<FunctionInvocation>().Subject;
            invocation.Function.Should().BeOfType<NameNode>().Which.Name.Should().Be("string length");
        }

        [TestMethod]
        public void ParseMultiWordVariableName()
        {
            var scope = new FeelScope();
            scope.AddName("customer name");
            var ast = engine.ParseExpression("customer name", scope);
            ast.Should().BeOfType<NameNode>().Which.Name.Should().Be("customer name");
        }

        // ==================== Error Handling ====================

        [TestMethod]
        public void ParseInvalidExpression_ThrowsFeelParseException()
        {
            var action = () => engine.ParseExpression("+ + +");
            action.Should().Throw<FeelParseException>();
        }

        [TestMethod]
        public void ParseEmptyExpression_ThrowsArgumentException()
        {
            var action = () => engine.ParseExpression("");
            action.Should().Throw<System.ArgumentException>();
        }

        [TestMethod]
        public void ParseNullExpression_ThrowsArgumentException()
        {
            var action = () => engine.ParseExpression(null);
            action.Should().Throw<System.ArgumentException>();
        }
    }
}
