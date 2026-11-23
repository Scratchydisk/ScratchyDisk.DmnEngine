using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Parsing;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §10.3.4.5 Numeric functions.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - FEEL")]
    [TestCategory("DMN 1.6 conformance")]
    public class Feel16RoundingOverloadTests
    {
        private static object Eval(string expression)
        {
            return new FeelEngine().EvaluateExpression(expression, new FeelEvaluationContext(), new FeelScope());
        }

        [TestMethod]
        public void Dmn16_10_3_4_5_OneArgumentRoundUp()
        {
            Eval("round up(5.5)").Should().Be(6m);
            Eval("round up(-5.5)").Should().Be(-6m);
        }

        [TestMethod]
        public void Dmn16_10_3_4_5_OneArgumentRoundDown()
        {
            Eval("round down(5.5)").Should().Be(5m);
            Eval("round down(-5.5)").Should().Be(-5m);
        }

        [TestMethod]
        public void Dmn16_10_3_4_5_OneArgumentRoundHalfUp()
        {
            Eval("round half up(5.5)").Should().Be(6m);
            Eval("round half up(-5.5)").Should().Be(-6m);
        }

        [TestMethod]
        public void Dmn16_10_3_4_5_OneArgumentRoundHalfDown()
        {
            Eval("round half down(5.5)").Should().Be(5m);
            Eval("round half down(-5.5)").Should().Be(-5m);
        }

        [TestMethod]
        public void Dmn16_10_3_4_5_TwoArgumentRoundingUnchanged()
        {
            Eval("round up(1.121, 2)").Should().Be(1.13m);
            Eval("round down(1.121, 2)").Should().Be(1.12m);
            Eval("round half up(1.126, 2)").Should().Be(1.13m);
            Eval("round half down(1.126, 2)").Should().Be(1.13m);
        }

        [TestMethod]
        public void Dmn16_10_3_4_5_RoundingNullPropagates()
        {
            Eval("round up(null)").Should().BeNull();
            Eval("round half down(null)").Should().BeNull();
        }
    }
}
