using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Parsing;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.5 §10.3.4.7 Range Functions.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - FEEL")]
    [TestCategory("DMN 1.5 conformance")]
    public class Feel15OverlapsBeforeAfterTests
    {
        private static object Eval(string expression)
        {
            return new FeelEngine().EvaluateExpression(expression, new FeelEvaluationContext(), new FeelScope());
        }

        [TestMethod]
        public void Dmn15_10_3_4_7_OverlapsBefore()
        {
            Eval("overlaps before([1..5], [3..8])").Should().Be(true);
            Eval("overlaps before([1..5], [6..8])").Should().Be(false);
            Eval("overlaps before([1..5], [5..8])").Should().Be(true);
            Eval("overlaps before([1..5), [5..8])").Should().Be(false);
        }

        [TestMethod]
        public void Dmn15_10_3_4_7_OverlapsAfter()
        {
            Eval("overlaps after([3..8], [1..5])").Should().Be(true);
            Eval("overlaps after([6..8], [1..5])").Should().Be(false);
            Eval("overlaps after([5..8], [1..5])").Should().Be(true);
            Eval("overlaps after((5..8], [1..5])").Should().Be(false);
        }

        [TestMethod]
        public void Dmn15_10_3_4_7_OverlapsUnchanged()
        {
            Eval("overlaps([1..5], [3..8])").Should().Be(true);
            Eval("overlaps([1..5], [6..8])").Should().Be(false);
        }
    }
}
