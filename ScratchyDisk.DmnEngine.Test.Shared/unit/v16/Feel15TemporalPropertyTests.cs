using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Parsing;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.5 §10.3.2.15 Tables 66-67.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - FEEL")]
    [TestCategory("DMN 1.5 conformance")]
    public class Feel15TemporalPropertyTests
    {
        private static object Eval(string expression)
        {
            return new FeelEngine().EvaluateExpression(expression, new FeelEvaluationContext(), new FeelScope());
        }

        [TestMethod]
        public void Dmn15_10_3_2_15_Table66_Weekday()
        {
            Eval("date(2024, 1, 1).weekday").Should().Be(1m); // Monday
            Eval("date(2024, 1, 7).weekday").Should().Be(7m); // Sunday
        }

        [TestMethod]
        public void Dmn15_10_3_2_15_Table66_DateTimeWeekday()
        {
            Eval("date and time(date(2024, 1, 1), time(12, 0, 0)).weekday").Should().Be(1m);
        }

        [TestMethod]
        public void Dmn15_10_3_2_15_Table67_TimeOffset()
        {
            Eval("time(1, 2, 3, duration(\"PT2H\")).time offset")
                .Should().Be(TimeSpan.FromHours(2));
            Eval("time(1, 2, 3).time offset").Should().BeNull();
        }

        [TestMethod]
        public void Dmn15_10_3_2_15_Table67_TimezoneAbsentReturnsNull()
        {
            Eval("date and time(date(2024, 1, 1), time(12, 0, 0)).timezone").Should().BeNull();
        }
    }
}
