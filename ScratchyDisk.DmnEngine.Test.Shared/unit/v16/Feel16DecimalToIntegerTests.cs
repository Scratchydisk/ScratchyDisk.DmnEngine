using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Parsing;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §10.3.2.9.4 implicit conversion from decimal to integer.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - FEEL")]
    [TestCategory("DMN 1.6 conformance")]
    public class Feel16DecimalToIntegerTests
    {
        private static object Eval(string expression)
        {
            return new FeelEngine().EvaluateExpression(expression, new FeelEvaluationContext(), new FeelScope());
        }

        [TestMethod]
        public void Dmn16_10_3_2_9_4_SubstringPositionTruncates()
        {
            Eval("substring(\"abcdef\", 2.9, 2.9)").Should().Be("bc");
        }

        [TestMethod]
        public void Dmn16_10_3_2_9_4_DecimalScaleTruncates()
        {
            Eval("decimal(1.55, 0.9)").Should().Be(2m);
        }

        [TestMethod]
        public void Dmn16_10_3_2_9_4_RoundingScaleTruncates()
        {
            Eval("round up(1.21, 1.9)").Should().Be(1.3m);
        }

        [TestMethod]
        public void Dmn16_10_3_2_9_4_ListPositionTruncates()
        {
            Eval("insert before([1, 2], 1.9, 9)").Should().BeAssignableTo<List<object>>()
                .Which.Should().Equal(9m, 1m, 2m);
        }
    }
}
