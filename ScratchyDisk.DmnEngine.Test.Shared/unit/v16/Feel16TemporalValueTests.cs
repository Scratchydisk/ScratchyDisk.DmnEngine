using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Parsing;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §10.3.2.15 Tables 66-67 (value property).
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - FEEL")]
    [TestCategory("DMN 1.6 conformance")]
    public class Feel16TemporalValueTests
    {
        private static object Eval(string expression)
        {
            return new FeelEngine().EvaluateExpression(expression, new FeelEvaluationContext(), new FeelScope());
        }

        [TestMethod]
        public void Dmn16_10_3_2_15_Table66_DateValueProperty()
        {
            Eval("date(1970, 1, 1).value").Should().Be(0m);
            Eval("date(1970, 1, 2).value").Should().Be(86400m);
        }

        [TestMethod]
        public void Dmn16_10_3_2_15_Table66_TimeValueProperty()
        {
            Eval("time(1, 1, 1).value").Should().Be(3661m);
        }

        [TestMethod]
        public void Dmn16_10_3_2_15_Table66_DateTimeValueProperty()
        {
            Eval("date and time(date(1970, 1, 1), time(0, 0, 1)).value").Should().Be(1m);
        }

        [TestMethod]
        public void Dmn16_10_3_2_15_Table66_DurationValueProperty()
        {
            Eval("duration(\"PT1H\").value").Should().Be(3600m);
            Eval("duration(\"P1Y1M\").value").Should().Be(13m);
        }

        [TestMethod]
        public void Dmn16_10_3_2_15_Table66_ValueOnUnsupportedTypeReturnsNull()
        {
            Eval("\"text\".value").Should().BeNull();
        }
    }
}
