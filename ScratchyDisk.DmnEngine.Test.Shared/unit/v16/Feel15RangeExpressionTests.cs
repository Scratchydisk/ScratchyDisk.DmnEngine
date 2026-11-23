using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Parsing;
using ScratchyDisk.DmnEngine.Feel.Types;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.5 §10.3.4.1 range(from) and §10.3.1.2 grammar rule 66.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - FEEL")]
    [TestCategory("DMN 1.5 conformance")]
    public class Feel15RangeExpressionTests
    {
        private static object Eval(string expression)
        {
            return new FeelEngine().EvaluateExpression(expression, new FeelEvaluationContext(), new FeelScope());
        }

        [TestMethod]
        public void Dmn15_10_3_4_1_RangeFromString()
        {
            var range = Eval("range(\"[18..21)\")").Should().BeOfType<FeelRange>().Subject;

            range.LowEndpoint.Should().Be(18m);
            range.LowInclusive.Should().BeTrue();
            range.HighEndpoint.Should().Be(21m);
            range.HighInclusive.Should().BeFalse();
        }

        [TestMethod]
        public void Dmn15_10_3_4_1_RangeFromOpenEndedString()
        {
            var range = Eval("range(\"(2..)\")").Should().BeOfType<FeelRange>().Subject;

            range.LowEndpoint.Should().Be(2m);
            range.LowInclusive.Should().BeFalse();
            range.HighEndpoint.Should().BeNull();
        }

        [TestMethod]
        public void Dmn15_10_3_4_1_InvalidRangeStringReturnsNull()
        {
            Eval("range(\"\")").Should().BeNull();
            Eval("range(\"[..]\")").Should().BeNull();
            Eval("range(\"not a range\")").Should().BeNull();
        }

        [TestMethod]
        public void Dmn15_10_3_1_2_Rule66_RangeLiteralExpression()
        {
            var range = Eval("[1..5]").Should().BeOfType<FeelRange>().Subject;

            range.LowEndpoint.Should().Be(1m);
            range.HighEndpoint.Should().Be(5m);
            range.LowInclusive.Should().BeTrue();
            range.HighInclusive.Should().BeTrue();
        }

        [TestMethod]
        public void Dmn15_10_3_1_2_Rule66_ListLiteralStillWorks()
        {
            Eval("[1, 2, 3]").Should().BeAssignableTo<List<object>>().Which.Should().Equal(1m, 2m, 3m);
        }
    }
}
