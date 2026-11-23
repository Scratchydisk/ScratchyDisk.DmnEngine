using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Parsing;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §10.3.4.1 Conversion functions,
    /// date and time(date, time, timezone).
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - FEEL")]
    [TestCategory("DMN 1.6 conformance")]
    public class Feel16ZonedDateTimeTests
    {
        private static object Eval(string expression)
        {
            return new FeelEngine().EvaluateExpression(expression, new FeelEvaluationContext(), new FeelScope());
        }

        [TestMethod]
        public void Dmn16_10_3_4_1_ZonedDateTimeWinterOffset()
        {
            var result = Eval("date and time(date(2024, 12, 24), time(23, 59, 0), \"Europe/Paris\")");

            var value = result.Should().BeOfType<DateTimeOffset>().Subject;
            value.Year.Should().Be(2024);
            value.Month.Should().Be(12);
            value.Day.Should().Be(24);
            value.Hour.Should().Be(23);
            value.Minute.Should().Be(59);
            value.Offset.Should().Be(TimeSpan.FromHours(1));
        }

        [TestMethod]
        public void Dmn16_10_3_4_1_ZonedDateTimeSummerOffset()
        {
            var result = Eval("date and time(date(2024, 7, 1), time(12, 0, 0), \"Europe/Paris\")");

            var value = result.Should().BeOfType<DateTimeOffset>().Subject;
            value.Offset.Should().Be(TimeSpan.FromHours(2));
        }

        [TestMethod]
        public void Dmn16_10_3_4_1_ZonedDateTimeUtc()
        {
            var result = Eval("date and time(date(2024, 12, 24), time(23, 59, 0), \"Z\")");

            result.Should().BeOfType<DateTimeOffset>().Which.Offset.Should().Be(TimeSpan.Zero);
        }

        [TestMethod]
        public void Dmn16_10_3_4_1_ZonedDateTimeInvalidZoneReturnsNull()
        {
            Eval("date and time(date(2024, 12, 24), time(23, 59, 0), \"Not/AZone\")").Should().BeNull();
        }

        [TestMethod]
        public void Dmn16_10_3_4_1_TwoArgumentDateTimeUnchanged()
        {
            var result = Eval("date and time(date(2024, 12, 24), time(23, 59, 0))");

            result.Should().BeOfType<DateTimeOffset>().Which.Offset.Should().Be(TimeSpan.Zero);
        }
    }
}
