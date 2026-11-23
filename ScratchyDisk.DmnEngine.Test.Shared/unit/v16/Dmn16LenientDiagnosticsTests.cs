using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Definition.Builder;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;
using ScratchyDisk.DmnEngine.Engine.Execution.Result;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §7.3.8 Error Handling.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - engine")]
    [TestCategory("DMN 1.6 conformance")]
    public class Dmn16LenientDiagnosticsTests
    {
        private static DmnDefinition BadTypeDefinition()
        {
            return new DmnDefinitionBuilder()
                .WithInput<int>("input", out var input)
                .WithVariable<int>("badOutput", out var badOutput)
                .WithExpressionDecision("badDecision", "\"hello\"", badOutput)
                .Build();
        }

        [TestMethod]
        public void Dmn16_7_3_8_LenientTypeMismatchRecorded()
        {
            var definition = BadTypeDefinition();
            var context = DmnExecutionContextFactory.CreateExecutionContext(definition);

            var result = context.ExecuteDecision("badDecision");

            result.HasResult.Should().BeFalse();
            context.Diagnostics.Should().ContainSingle();
            context.Diagnostics[0].DecisionName.Should().Be("badDecision");
            context.Diagnostics[0].Message.Should().NotBeNullOrWhiteSpace();
        }

        [TestMethod]
        public void Dmn16_7_3_8_LenientChildErrorVisibleFromParent()
        {
            var builder = new DmnDefinitionBuilder()
                .WithInput<int>("input", out var input)
                .WithVariable<int>("badOutput", out var badOutput)
                .WithVariable<int>("parentOutput", out var parentOutput)
                .WithExpressionDecision("badDecision", "\"hello\"", badOutput, out var badDecision)
                .WithExpressionDecision("parent", e => e.Put("1 + 1").To(parentOutput).Requires(badDecision));
            var definition = builder.Build();
            var context = DmnExecutionContextFactory.CreateExecutionContext(definition);

            var result = context.ExecuteDecision("parent");

            result.HasResult.Should().BeTrue();
            result.First["parentOutput"].Value.Should().Be(2);
            context.Diagnostics.Should().ContainSingle(d => d.DecisionName == "badDecision");
        }

        [TestMethod]
        public void Dmn16_7_3_8_LenientRetainsMultipleErrors()
        {
            var builder = new DmnDefinitionBuilder()
                .WithInput<int>("input", out var input)
                .WithVariable<int>("bad1", out var bad1)
                .WithVariable<int>("bad2", out var bad2)
                .WithExpressionDecision("badDecision1", "\"hello\"", bad1)
                .WithExpressionDecision("badDecision2", "\"world\"", bad2);
            var definition = builder.Build();
            var context = DmnExecutionContextFactory.CreateExecutionContext(definition);

            context.ExecuteDecision("badDecision1");
            context.ExecuteDecision("badDecision2");

            context.Diagnostics.Should().HaveCount(2);
            context.Diagnostics.Select(d => d.DecisionName)
                .Should().BeEquivalentTo("badDecision1", "badDecision2");
        }

        [TestMethod]
        public void Dmn16_7_3_8_LenientCleanRunHasNoDiagnostics()
        {
            var definition = new DmnDefinitionBuilder()
                .WithInput<int>("input", out var input)
                .WithVariable<int>("output", out var output)
                .WithExpressionDecision("decision", "input + 1", output)
                .Build();
            var context = DmnExecutionContextFactory.CreateExecutionContext(definition);

            var result = context.ExecuteDecision("decision");

            result.HasResult.Should().BeTrue();
            context.Diagnostics.Should().BeEmpty();
        }
    }
}
