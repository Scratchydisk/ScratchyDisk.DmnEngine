using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Parsing;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.5 §10.3.4.1 time-string domain and §10.3.2.3.4/10.3.2.3.6.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - FEEL")]
    [TestCategory("DMN 1.5 conformance")]
    public class Feel15IanaTimezoneTests
    {
        private static object Eval(string expression)
        {
            return new FeelEngine().EvaluateExpression(expression, new FeelEvaluationContext(), new FeelScope());
        }

        [TestMethod]
        public void Dmn15_10_3_4_1_TimeWithFixedIanaZone()
        {
            Eval("time(\"23:59:00@Etc/GMT+5\").time offset")
                .Should().Be(TimeSpan.FromHours(-5));
        }

        [TestMethod]
        public void Dmn15_10_3_4_1_DateTimeWithIanaZoneWinter()
        {
            Eval("date and time(\"2024-12-24T23:59:00@Europe/Paris\").time offset")
                .Should().Be(TimeSpan.FromHours(1));
        }

        [TestMethod]
        public void Dmn15_10_3_4_1_DateTimeWithIanaZoneSummer()
        {
            Eval("date and time(\"2024-07-01T12:00:00@Europe/Paris\").time offset")
                .Should().Be(TimeSpan.FromHours(2));
        }

        [TestMethod]
        public void Dmn15_10_3_4_1_UnknownIanaZoneReturnsNull()
        {
            Eval("time(\"23:59:00@Not/AZone\")").Should().BeNull();
            Eval("date and time(\"2024-12-24T23:59:00@Not/AZone\")").Should().BeNull();
        }

        [TestMethod]
        public void Dmn15_10_3_4_1_OffsetFormsUnchanged()
        {
            Eval("time(\"23:59:00Z\").time offset").Should().Be(TimeSpan.Zero);
            Eval("date and time(\"2024-12-24T23:59:00+01:00\").time offset")
                .Should().Be(TimeSpan.FromHours(1));
        }
    }
}
