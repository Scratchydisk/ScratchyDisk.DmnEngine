using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Parsing;
using ScratchyDisk.DmnEngine.Feel.Types;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §10.3.1.2 grammar rule 68 and §10.3.2.15
    /// descendant semantics.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - FEEL")]
    [TestCategory("DMN 1.6 conformance")]
    public class Feel16DescendantTests
    {
        private static object Eval(string expression)
        {
            var engine = new FeelEngine();
            return engine.EvaluateExpression(expression, new FeelEvaluationContext(), new FeelScope());
        }

        [TestMethod]
        public void Dmn16_10_3_1_2_Rule68_DescendantExpression()
        {
            var result = Eval("{a: {b: {b: 1}}}...b");

            var values = result.Should().BeAssignableTo<List<object>>().Subject;
            values.Should().HaveCount(2);
            values[0].Should().BeOfType<FeelContext>().Which["b"].Should().Be(1m);
            values[1].Should().Be(1m);
        }

        [TestMethod]
        public void Dmn16_10_3_1_2_Rule68_DescendantNoMatchReturnsEmptyList()
        {
            var result = Eval("{a: {b: 1}}...missing");

            result.Should().BeAssignableTo<List<object>>().Which.Should().BeEmpty();
        }

        [TestMethod]
        public void Dmn16_10_3_1_2_Rule68_DescendantNonContextReturnsNull()
        {
            Eval("42...b").Should().BeNull();
        }

        [TestMethod]
        public void Dmn16_10_3_1_2_Rule68_DescendantPreservesEntryOrder()
        {
            var result = Eval("{b: 1, a: {b: 2, c: {b: 3}}}...b");

            var values = result.Should().BeAssignableTo<List<object>>().Subject;
            values.Should().Equal(1m, 2m, 3m);
        }

        [TestMethod]
        public void Dmn16_10_3_1_2_Rule68_DescendantInLiteralExpressionDecision()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            var file = Path.Combine(dir ?? string.Empty, "dmn", "dmn1.6", "descendant.dmn");
            var model = DmnParser.ParseAutoDetect(file);
            var definition = DmnDefinitionFactory.CreateDmnDefinition(model);

            var result = DmnExecutionContextFactory.CreateExecutionContext(definition)
                .ExecuteDecision("Descendants");

            result.First["Descendants"].Value.Should().Be(2m);
        }
    }
}
