using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Parsing;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.5 §10.3.4.5 Numeric functions.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - FEEL")]
    [TestCategory("DMN 1.5 conformance")]
    public class Feel15RoundingCorrectnessTests
    {
        private static object Eval(string expression)
        {
            return new FeelEngine().EvaluateExpression(expression, new FeelEvaluationContext(), new FeelScope());
        }

        [TestMethod]
        public void Dmn15_10_3_4_5_RoundHalfDownTies()
        {
            Eval("round half down(5.5, 0)").Should().Be(5m);
            Eval("round half down(-5.5, 0)").Should().Be(-5m);
        }

        [TestMethod]
        public void Dmn15_10_3_4_5_DecimalHalfEven()
        {
            Eval("decimal(0.505, 2)").Should().Be(0.50m);
            Eval("decimal(0.515, 2)").Should().Be(0.52m);
            Eval("decimal(2.5, 0)").Should().Be(2m);
            Eval("decimal(3.5, 0)").Should().Be(4m);
        }

        [TestMethod]
        public void Dmn15_10_3_4_5_OtherRoundingUnchanged()
        {
            Eval("round half up(5.5, 0)").Should().Be(6m);
            Eval("round up(1.121, 2)").Should().Be(1.13m);
            Eval("round down(1.126, 2)").Should().Be(1.12m);
        }
    }
}
