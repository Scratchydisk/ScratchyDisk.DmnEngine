using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Parsing;
using ScratchyDisk.DmnEngine.Feel.Types;

namespace ScratchyDisk.DmnEngine.Test.Unit.Feel
{
    [TestClass]
    [TestCategory("FEEL Evaluator")]
    public class FeelEvaluatorTests
    {
        private readonly FeelEngine engine = new();

        private object Eval(string expression, Action<FeelEvaluationContext> setupContext = null, FeelScope scope = null)
        {
            var ctx = new FeelEvaluationContext();
            setupContext?.Invoke(ctx);
            return engine.EvaluateExpression(expression, ctx, scope);
        }

        private bool EvalUnary(string expression, object inputValue, Action<FeelEvaluationContext> setupContext = null)
        {
            var ctx = new FeelEvaluationContext();
            setupContext?.Invoke(ctx);
            return engine.EvaluateSimpleUnaryTests(expression, inputValue, ctx);
        }

        // ==================== Literals ====================

        [TestMethod]
        public void EvalIntegerLiteral() => Eval("42").Should().Be(42m);

        [TestMethod]
        public void EvalFloatLiteral() => Eval("3.14").Should().Be(3.14m);

        [TestMethod]
        public void EvalStringLiteral() => Eval("\"hello\"").Should().Be("hello");

        [TestMethod]
        public void EvalBoolTrue() => Eval("true").Should().Be(true);

        [TestMethod]
        public void EvalBoolFalse() => Eval("false").Should().Be(false);

        [TestMethod]
        public void EvalNull() => Eval("null").Should().BeNull();

        // ==================== Arithmetic ====================

        [TestMethod]
        public void EvalAddition() => Eval("1 + 2").Should().Be(3m);

        [TestMethod]
        public void EvalSubtraction() => Eval("10 - 3").Should().Be(7m);

        [TestMethod]
        public void EvalMultiplication() => Eval("4 * 5").Should().Be(20m);

        [TestMethod]
        public void EvalDivision() => Eval("10 / 4").Should().Be(2.5m);

        [TestMethod]
        public void EvalDivisionByZero() => Eval("1 / 0").Should().BeNull();

        [TestMethod]
        public void EvalExponentiation() => Eval("2 ** 10").Should().Be(1024m);

        [TestMethod]
        public void EvalUnaryMinus() => Eval("-5").Should().Be(-5m);

        [TestMethod]
        public void EvalPrecedence() => Eval("2 + 3 * 4").Should().Be(14m);

        [TestMethod]
        public void EvalParentheses() => Eval("(2 + 3) * 4").Should().Be(20m);

        [TestMethod]
        public void EvalNullPropagation_Add() => Eval("null + 1").Should().BeNull();

        [TestMethod]
        public void EvalNullPropagation_Mul() => Eval("null * 5").Should().BeNull();

        [TestMethod]
        public void EvalStringConcatenation() => Eval("\"hello\" + \" \" + \"world\"").Should().Be("hello world");

        // ==================== Comparison ====================

        [TestMethod]
        public void EvalLessThan() => Eval("3 < 5").Should().Be(true);

        [TestMethod]
        public void EvalGreaterThan() => Eval("5 > 3").Should().Be(true);

        [TestMethod]
        public void EvalLessThanOrEqual() => Eval("5 <= 5").Should().Be(true);

        [TestMethod]
        public void EvalGreaterThanOrEqual() => Eval("4 >= 5").Should().Be(false);

        [TestMethod]
        public void EvalEqual() => Eval("5 = 5").Should().Be(true);

        [TestMethod]
        public void EvalNotEqual() => Eval("5 != 3").Should().Be(true);

        [TestMethod]
        public void EvalNullEquality() => Eval("null = null").Should().Be(true);

        [TestMethod]
        public void EvalNullInequality() => Eval("null = 5").Should().Be(false);

        [TestMethod]
        public void EvalBetween()
        {
            Eval("5 between 1 and 10").Should().Be(true);
            Eval("0 between 1 and 10").Should().Be(false);
            Eval("1 between 1 and 10").Should().Be(true); // inclusive
            Eval("10 between 1 and 10").Should().Be(true); // inclusive
        }

        [TestMethod]
        public void EvalInstanceOf()
        {
            Eval("5 instance of number").Should().Be(true);
            Eval("\"hello\" instance of string").Should().Be(true);
            Eval("true instance of boolean").Should().Be(true);
            Eval("null instance of null").Should().Be(true);
            Eval("5 instance of string").Should().Be(false);
        }

        // ==================== Logic ====================

        [TestMethod]
        public void EvalConjunction()
        {
            Eval("true and true").Should().Be(true);
            Eval("true and false").Should().Be(false);
            Eval("false and true").Should().Be(false);
            Eval("false and false").Should().Be(false);
        }

        [TestMethod]
        public void EvalDisjunction()
        {
            Eval("true or true").Should().Be(true);
            Eval("true or false").Should().Be(true);
            Eval("false or true").Should().Be(true);
            Eval("false or false").Should().Be(false);
        }

        [TestMethod]
        public void EvalThreeValuedLogic()
        {
            Eval("false and null").Should().Be(false);
            Eval("true or null").Should().Be(true);
            // null and true = null, true and null = null
            // These return null (not bool), so we check they're null
            Eval("true and null").Should().BeNull();
            Eval("false or null").Should().BeNull();
        }

        // ==================== Variables ====================

        [TestMethod]
        public void EvalVariable()
        {
            Eval("x + 1", ctx => ctx.SetVariable("x", 5m)).Should().Be(6m);
        }

        [TestMethod]
        public void EvalUnknownVariable()
        {
            Eval("unknown").Should().BeNull();
        }

        // ==================== Control Flow ====================

        [TestMethod]
        public void EvalIfThenElse()
        {
            Eval("if true then 1 else 2").Should().Be(1m);
            Eval("if false then 1 else 2").Should().Be(2m);
            Eval("if null then 1 else 2").Should().Be(2m);
        }

        [TestMethod]
        public void EvalIfWithExpression()
        {
            Eval("if x > 5 then \"high\" else \"low\"", ctx => ctx.SetVariable("x", 10m))
                .Should().Be("high");
            Eval("if x > 5 then \"high\" else \"low\"", ctx => ctx.SetVariable("x", 3m))
                .Should().Be("low");
        }

        [TestMethod]
        public void EvalForExpression()
        {
            var result = Eval("for x in [1, 2, 3] return x * 2") as List<object>;
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(new object[] { 2m, 4m, 6m });
        }

        [TestMethod]
        public void EvalForRange()
        {
            var result = Eval("for i in 1..5 return i * i") as List<object>;
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(new object[] { 1m, 4m, 9m, 16m, 25m });
        }

        [TestMethod]
        public void EvalForMultipleIterations()
        {
            var result = Eval("for x in [1, 2], y in [10, 20] return x + y") as List<object>;
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(new object[] { 11m, 21m, 12m, 22m });
        }

        [TestMethod]
        public void EvalSome()
        {
            Eval("some x in [1, 2, 3] satisfies x > 2").Should().Be(true);
            Eval("some x in [1, 2, 3] satisfies x > 5").Should().Be(false);
        }

        [TestMethod]
        public void EvalEvery()
        {
            Eval("every x in [1, 2, 3] satisfies x > 0").Should().Be(true);
            Eval("every x in [1, 2, 3] satisfies x > 1").Should().Be(false);
        }

        // ==================== Collections ====================

        [TestMethod]
        public void EvalList()
        {
            var result = Eval("[1, 2, 3]") as List<object>;
            result.Should().BeEquivalentTo(new object[] { 1m, 2m, 3m });
        }

        [TestMethod]
        public void EvalEmptyList()
        {
            var result = Eval("[]") as List<object>;
            result.Should().BeEmpty();
        }

        [TestMethod]
        public void EvalContext()
        {
            var result = Eval("{ name: \"John\", age: 30 }") as FeelContext;
            result.Should().NotBeNull();
            result["name"].Should().Be("John");
            result["age"].Should().Be(30m);
        }

        [TestMethod]
        public void EvalContextSelfReference()
        {
            // Context entries can reference earlier entries
            var result = Eval("{ x: 5, y: x + 1 }") as FeelContext;
            result.Should().NotBeNull();
            result["x"].Should().Be(5m);
            result["y"].Should().Be(6m);
        }

        [TestMethod]
        public void EvalRange()
        {
            var result = Eval("[1, 2, 3, 4, 5]") as List<object>;
            result.Should().HaveCount(5);
        }

        // ==================== Postfix ====================

        [TestMethod]
        public void EvalMemberAccess()
        {
            var ctx = new FeelContext();
            ctx.Put("name", "John");
            ctx.Put("age", 30m);
            Eval("person.name", c => c.SetVariable("person", ctx)).Should().Be("John");
        }

        [TestMethod]
        public void EvalFilter()
        {
            var result = Eval("[1, 2, 3, 4, 5][item > 3]",
                ctx => { }) as List<object>;
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(new object[] { 4m, 5m });
        }

        [TestMethod]
        public void EvalFilterByIndex()
        {
            Eval("[10, 20, 30][2]").Should().Be(20m); // 1-based
        }

        [TestMethod]
        public void EvalFilterByNegativeIndex()
        {
            Eval("[10, 20, 30][-1]").Should().Be(30m); // last element
        }

        [TestMethod]
        public void EvalListProjection()
        {
            // list.member → [item.member for item in list]
            var items = new List<object>
            {
                CreateContext(("name", "Alice"), ("age", 30m)),
                CreateContext(("name", "Bob"), ("age", 25m))
            };
            var result = Eval("people.name", ctx => ctx.SetVariable("people", items)) as List<object>;
            result.Should().BeEquivalentTo(new object[] { "Alice", "Bob" });
        }

        // ==================== Functions ====================

        [TestMethod]
        public void EvalFunctionDefinitionAndInvocation()
        {
            var result = Eval("{ double: function(x) x * 2, result: double(5) }") as FeelContext;
            result.Should().NotBeNull();
            result["result"].Should().Be(10m);
        }

        // ==================== Built-in String Functions ====================

        [TestMethod]
        public void EvalStringLength()
        {
            var scope = new FeelScope();
            Eval("string length(\"hello\")", scope: scope).Should().Be(5m);
        }

        [TestMethod]
        public void EvalSubstring()
        {
            Eval("substring(\"hello\", 2, 3)").Should().Be("ell");
        }

        [TestMethod]
        public void EvalUpperCase()
        {
            var scope = new FeelScope();
            Eval("upper case(\"hello\")", scope: scope).Should().Be("HELLO");
        }

        [TestMethod]
        public void EvalLowerCase()
        {
            var scope = new FeelScope();
            Eval("lower case(\"HELLO\")", scope: scope).Should().Be("hello");
        }

        [TestMethod]
        public void EvalContains()
        {
            Eval("contains(\"foobar\", \"bar\")").Should().Be(true);
            Eval("contains(\"foobar\", \"baz\")").Should().Be(false);
        }

        [TestMethod]
        public void EvalStartsWith()
        {
            var scope = new FeelScope();
            Eval("starts with(\"hello world\", \"hello\")", scope: scope).Should().Be(true);
        }

        [TestMethod]
        public void EvalEndsWith()
        {
            var scope = new FeelScope();
            Eval("ends with(\"hello world\", \"world\")", scope: scope).Should().Be(true);
        }

        [TestMethod]
        public void EvalSubstringBefore()
        {
            var scope = new FeelScope();
            Eval("substring before(\"hello world\", \" \")", scope: scope).Should().Be("hello");
        }

        [TestMethod]
        public void EvalSubstringAfter()
        {
            var scope = new FeelScope();
            Eval("substring after(\"hello world\", \" \")", scope: scope).Should().Be("world");
        }

        [TestMethod]
        public void EvalReplace()
        {
            Eval("replace(\"abc\", \"b\", \"x\")").Should().Be("axc");
        }

        [TestMethod]
        public void EvalSplit()
        {
            var result = Eval("split(\"a,b,c\", \",\")") as List<object>;
            result.Should().BeEquivalentTo(new object[] { "a", "b", "c" });
        }

        [TestMethod]
        public void EvalMatches()
        {
            Eval("matches(\"test123\", \"test\\\\d+\")").Should().Be(true);
        }

        // ==================== Built-in Numeric Functions ====================

        [TestMethod]
        public void EvalAbs()
        {
            Eval("abs(-5)").Should().Be(5m);
            Eval("abs(5)").Should().Be(5m);
        }

        [TestMethod]
        public void EvalFloor()
        {
            Eval("floor(3.7)").Should().Be(3m);
            Eval("floor(-3.2)").Should().Be(-4m);
        }

        [TestMethod]
        public void EvalCeiling()
        {
            Eval("ceiling(3.2)").Should().Be(4m);
            Eval("ceiling(-3.7)").Should().Be(-3m);
        }

        [TestMethod]
        public void EvalModulo()
        {
            Eval("modulo(10, 3)").Should().Be(1m);
        }

        [TestMethod]
        public void EvalSqrt()
        {
            Eval("sqrt(9)").Should().Be(3m);
        }

        [TestMethod]
        public void EvalOdd()
        {
            Eval("odd(3)").Should().Be(true);
            Eval("odd(4)").Should().Be(false);
        }

        [TestMethod]
        public void EvalEven()
        {
            Eval("even(4)").Should().Be(true);
            Eval("even(3)").Should().Be(false);
        }

        // ==================== Built-in List Functions ====================

        [TestMethod]
        public void EvalCount()
        {
            Eval("count([1, 2, 3])").Should().Be(3m);
        }

        [TestMethod]
        public void EvalMin()
        {
            Eval("min([3, 1, 2])").Should().Be(1m);
            Eval("min(3, 1, 2)").Should().Be(1m);
        }

        [TestMethod]
        public void EvalMax()
        {
            Eval("max([3, 1, 2])").Should().Be(3m);
        }

        [TestMethod]
        public void EvalSum()
        {
            Eval("sum([1, 2, 3])").Should().Be(6m);
            Eval("sum(1, 2, 3)").Should().Be(6m);
        }

        [TestMethod]
        public void EvalMean()
        {
            Eval("mean([2, 4, 6])").Should().Be(4m);
        }

        [TestMethod]
        public void EvalListContains()
        {
            var scope = new FeelScope();
            Eval("list contains([1, 2, 3], 2)", scope: scope).Should().Be(true);
            Eval("list contains([1, 2, 3], 5)", scope: scope).Should().Be(false);
        }

        [TestMethod]
        public void EvalAppend()
        {
            var result = Eval("append([1, 2], 3)") as List<object>;
            result.Should().BeEquivalentTo(new object[] { 1m, 2m, 3m });
        }

        [TestMethod]
        public void EvalConcatenate()
        {
            var result = Eval("concatenate([1, 2], [3, 4])") as List<object>;
            result.Should().BeEquivalentTo(new object[] { 1m, 2m, 3m, 4m });
        }

        [TestMethod]
        public void EvalReverse()
        {
            var result = Eval("reverse([1, 2, 3])") as List<object>;
            result.Should().BeEquivalentTo(new object[] { 3m, 2m, 1m });
        }

        [TestMethod]
        public void EvalDistinctValues()
        {
            var scope = new FeelScope();
            var result = Eval("distinct values([1, 2, 1, 3, 2])", scope: scope) as List<object>;
            result.Should().BeEquivalentTo(new object[] { 1m, 2m, 3m });
        }

        [TestMethod]
        public void EvalFlatten()
        {
            var result = Eval("flatten([[1, 2], [3, [4, 5]]])") as List<object>;
            result.Should().BeEquivalentTo(new object[] { 1m, 2m, 3m, 4m, 5m });
        }

        [TestMethod]
        public void EvalProduct()
        {
            Eval("product([2, 3, 4])").Should().Be(24m);
        }

        [TestMethod]
        public void EvalMedian()
        {
            Eval("median([1, 2, 3])").Should().Be(2m);
            Eval("median([1, 2, 3, 4])").Should().Be(2.5m);
        }

        [TestMethod]
        public void EvalAll()
        {
            Eval("all([true, true, true])").Should().Be(true);
            Eval("all([true, false, true])").Should().Be(false);
        }

        [TestMethod]
        public void EvalAny()
        {
            Eval("any([false, true, false])").Should().Be(true);
            Eval("any([false, false, false])").Should().Be(false);
        }

        [TestMethod]
        public void EvalIndexOf()
        {
            var scope = new FeelScope();
            var result = Eval("index of([1, 2, 3, 2], 2)", scope: scope) as List<object>;
            result.Should().BeEquivalentTo(new object[] { 2m, 4m }); // 1-based indices
        }

        [TestMethod]
        public void EvalSort()
        {
            var result = Eval("sort([3, 1, 2])") as List<object>;
            result.Should().BeEquivalentTo(new object[] { 1m, 2m, 3m });
        }

        // ==================== Built-in Boolean Functions ====================

        [TestMethod]
        public void EvalNotFunction()
        {
            Eval("not(true)").Should().Be(false);
            Eval("not(false)").Should().Be(true);
            Eval("not(null)").Should().BeNull();
        }

        // ==================== Built-in Date/Time Functions ====================

        [TestMethod]
        public void EvalDateLiteral()
        {
            var result = Eval("@\"2024-01-15\"");
            result.Should().Be(new DateOnly(2024, 1, 15));
        }

        [TestMethod]
        public void EvalDateFunction()
        {
            Eval("date(\"2024-01-15\")").Should().Be(new DateOnly(2024, 1, 15));
            Eval("date(2024, 1, 15)").Should().Be(new DateOnly(2024, 1, 15));
        }

        [TestMethod]
        public void EvalDateTimeLiteral()
        {
            var result = Eval("@\"2024-01-15T10:30:00Z\"");
            result.Should().BeOfType<DateTimeOffset>();
            ((DateTimeOffset)result).Year.Should().Be(2024);
        }

        [TestMethod]
        public void EvalDurationLiteral()
        {
            var result = Eval("@\"P1Y2M\"");
            result.Should().Be(new FeelYmDuration(1, 2));

            var result2 = Eval("@\"P3DT4H\"");
            result2.Should().Be(new TimeSpan(3, 4, 0, 0));
        }

        [TestMethod]
        public void EvalDateArithmetic()
        {
            // Date + ym duration
            var result = Eval("@\"2024-01-15\" + @\"P1Y\"");
            result.Should().Be(new DateOnly(2025, 1, 15));
        }

        [TestMethod]
        public void EvalNowAndToday()
        {
            Eval("now()").Should().BeOfType<DateTimeOffset>();
            Eval("today()").Should().BeOfType<DateOnly>();
        }

        [TestMethod]
        public void EvalDateComponents()
        {
            Eval("@\"2024-03-15\".year").Should().Be(2024m);
            Eval("@\"2024-03-15\".month").Should().Be(3m);
            Eval("@\"2024-03-15\".day").Should().Be(15m);
        }

        // ==================== Built-in Context Functions ====================

        [TestMethod]
        public void EvalGetValue()
        {
            var scope = new FeelScope();
            Eval("get value({a: 1, b: 2}, \"a\")", scope: scope).Should().Be(1m);
        }

        [TestMethod]
        public void EvalGetEntries()
        {
            var scope = new FeelScope();
            var result = Eval("get entries({a: 1})", scope: scope) as List<object>;
            result.Should().HaveCount(1);
            var entry = result[0] as FeelContext;
            entry["key"].Should().Be("a");
            entry["value"].Should().Be(1m);
        }

        // ==================== Unary Tests ====================

        [TestMethod]
        public void EvalUnaryTestDash()
        {
            EvalUnary("-", 42).Should().BeTrue();
            EvalUnary("-", "anything").Should().BeTrue();
        }

        [TestMethod]
        public void EvalUnaryTestComparison()
        {
            EvalUnary("> 5", 10).Should().BeTrue();
            EvalUnary("> 5", 3).Should().BeFalse();
            EvalUnary("<= 10", 10).Should().BeTrue();
            EvalUnary("<= 10", 11).Should().BeFalse();
        }

        [TestMethod]
        public void EvalUnaryTestEquality()
        {
            EvalUnary("5", 5m).Should().BeTrue();
            EvalUnary("5", 3m).Should().BeFalse();
            EvalUnary("\"hello\"", "hello").Should().BeTrue();
        }

        [TestMethod]
        public void EvalUnaryTestMultiple()
        {
            EvalUnary("1, 2, 3", 2m).Should().BeTrue();
            EvalUnary("1, 2, 3", 5m).Should().BeFalse();
        }

        [TestMethod]
        public void EvalUnaryTestNegated()
        {
            EvalUnary("not(1, 2)", 3m).Should().BeTrue();
            EvalUnary("not(1, 2)", 1m).Should().BeFalse();
        }

        [TestMethod]
        public void EvalUnaryTestNull()
        {
            EvalUnary("null", null).Should().BeTrue();
            EvalUnary("null", 5).Should().BeFalse();
        }

        [TestMethod]
        public void EvalUnaryTestInterval()
        {
            EvalUnary("1..10", 5m).Should().BeTrue();
            EvalUnary("1..10", 1m).Should().BeTrue();   // inclusive
            EvalUnary("1..10", 10m).Should().BeTrue();  // inclusive
            EvalUnary("1..10", 0m).Should().BeFalse();
            EvalUnary("1..10", 11m).Should().BeFalse();
        }

        // ==================== Complex Expressions ====================

        [TestMethod]
        public void EvalComplexBusinessRule()
        {
            // if age >= 18 and income > 30000 then "approved" else "denied"
            Eval("if age >= 18 and income > 30000 then \"approved\" else \"denied\"", ctx =>
            {
                ctx.SetVariable("age", 25m);
                ctx.SetVariable("income", 50000m);
            }).Should().Be("approved");

            Eval("if age >= 18 and income > 30000 then \"approved\" else \"denied\"", ctx =>
            {
                ctx.SetVariable("age", 16m);
                ctx.SetVariable("income", 50000m);
            }).Should().Be("denied");
        }

        [TestMethod]
        public void EvalNestedForWithFilter()
        {
            var result = Eval("for x in [1, 2, 3, 4, 5] return if x > 3 then x else null") as List<object>;
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(new object[] { null, null, null, 4m, 5m });
        }

        [TestMethod]
        public void EvalStringFunctionPipeline()
        {
            var scope = new FeelScope();
            Eval("upper case(substring(\"hello world\", 7))", scope: scope).Should().Be("WORLD");
        }

        // ==================== Helpers ====================

        private static FeelContext CreateContext(params (string key, object value)[] entries)
        {
            var ctx = new FeelContext();
            foreach (var (key, value) in entries)
                ctx.Put(key, value);
            return ctx;
        }
    }
}
