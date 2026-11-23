using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Parsing;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.5 §10.3.2.15 Table 67.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - FEEL")]
    [TestCategory("DMN 1.5 conformance")]
    public class Feel15RangePropertyTests
    {
        private static object Eval(string expression)
        {
            return new FeelEngine().EvaluateExpression(expression, new FeelEvaluationContext(), new FeelScope());
        }

        [TestMethod]
        public void Dmn15_10_3_2_15_Table67_StartAndEnd()
        {
            Eval("[1..5].start").Should().Be(1m);
            Eval("[1..5].end").Should().Be(5m);
        }

        [TestMethod]
        public void Dmn15_10_3_2_15_Table67_InclusionProperties()
        {
            Eval("[1..5).start included").Should().Be(true);
            Eval("[1..5).end included").Should().Be(false);
            Eval("(1..5].start included").Should().Be(false);
            Eval("(1..5].end included").Should().Be(true);
        }

        [TestMethod]
        public void Dmn15_10_3_2_15_Table67_UnboundedEndpointReturnsNull()
        {
            Eval("(1..).end").Should().BeNull();
            Eval("(..5].start").Should().BeNull();
        }

        [TestMethod]
        public void Dmn15_10_3_2_15_Table67_NonRangeReturnsNull()
        {
            Eval("1.start").Should().BeNull();
        }
    }
}
