using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Parsing;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §10.3.1.2 grammar rule 35.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - FEEL")]
    [TestCategory("DMN 1.6 conformance")]
    public class Feel16NumericLiteralTests
    {
        private static object Eval(string expression)
        {
            return new FeelEngine().EvaluateExpression(expression, new FeelEvaluationContext(), new FeelScope());
        }

        [TestMethod]
        public void Dmn16_10_3_1_2_Rule35_ExponentWithoutDecimalPoint()
        {
            Eval("1e3").Should().Be(1000m);
        }

        [TestMethod]
        public void Dmn16_10_3_1_2_Rule35_ExponentWithDecimalPoint()
        {
            Eval("1.5e-2").Should().Be(0.015m);
        }

        [TestMethod]
        public void Dmn16_10_3_1_2_Rule35_LeadingDotLiteral()
        {
            Eval(".5").Should().Be(0.5m);
        }

        [TestMethod]
        public void Dmn16_10_3_1_2_Rule35_NegativeExponent()
        {
            Eval("-1.2e+2").Should().Be(-120m);
        }

        [TestMethod]
        public void Dmn16_10_3_1_2_Rule35_InvalidExponentStillFails()
        {
            Action act = () => Eval("1e");

            act.Should().Throw<Exception>();
        }
    }
}
