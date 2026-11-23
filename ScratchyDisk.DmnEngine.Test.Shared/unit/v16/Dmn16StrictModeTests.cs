using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Definition.Builder;
using ScratchyDisk.DmnEngine.Engine.Execution;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §7.3.8 Error Handling.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - engine")]
    [TestCategory("DMN 1.6 conformance")]
    public class Dmn16StrictModeTests
    {
        private static DmnExecutionContext StrictContext(DmnDefinition definition)
        {
            return DmnExecutionContextFactory.CreateExecutionContext(
                definition,
                options => options.ErrorHandlingMode = DmnErrorHandlingMode.Strict);
        }

        [TestMethod]
        public void Dmn16_7_3_8_StrictStopsBeforeDependentDecision()
        {
            var builder = new DmnDefinitionBuilder()
                .WithInput<int>("input", out var input)
                .WithVariable<int>("badOutput", out var badOutput)
                .WithVariable<int>("parentOutput", out var parentOutput)
                .WithExpressionDecision("badDecision", "\"hello\"", badOutput, out var badDecision)
                .WithExpressionDecision("parent", e => e.Put("1 + 1").To(parentOutput).Requires(badDecision));
            var context = StrictContext(builder.Build());

            Action act = () => context.ExecuteDecision("parent");

            act.Should().Throw<DmnEvaluationException>();
            context.Diagnostics.Should().ContainSingle(d => d.DecisionName == "badDecision");
            context.GetVariable("parentOutput").Value.Should().BeNull();
        }

        [TestMethod]
        public void Dmn16_7_3_8_StrictFailureContract()
        {
            var definition = new DmnDefinitionBuilder()
                .WithInput<int>("input", out var input)
                .WithVariable<int>("badOutput", out var badOutput)
                .WithExpressionDecision("badDecision", "\"hello\"", badOutput)
                .Build();
            var context = StrictContext(definition);

            Action act = () => context.ExecuteDecision("badDecision");

            act.Should().Throw<DmnEvaluationException>();
            context.Diagnostics.Should().ContainSingle(d => d.DecisionName == "badDecision");
        }

        [TestMethod]
        public void Dmn16_7_3_8_StrictValidModelMatchesLenient()
        {
            var definition = new DmnDefinitionBuilder()
                .WithInput<int>("input", out var input)
                .WithVariable<int>("output", out var output)
                .WithExpressionDecision("decision", "input + 1", output)
                .Build();

            var strict = StrictContext(definition);
            var lenient = DmnExecutionContextFactory.CreateExecutionContext(definition);
            var strictResult = strict.WithInputParameter("input", 4).ExecuteDecision("decision");
            var lenientResult = lenient.WithInputParameter("input", 4).ExecuteDecision("decision");

            strictResult.First["output"].Value.Should().Be(5);
            lenientResult.First["output"].Value.Should().Be(5);
            strict.Diagnostics.Should().BeEmpty();
        }

        [TestMethod]
        public void Dmn16_7_3_8_LenientModeDoesNotUseStrictPath()
        {
            var definition = new DmnDefinitionBuilder()
                .WithInput<int>("input", out var input)
                .WithVariable<int>("badOutput", out var badOutput)
                .WithExpressionDecision("badDecision", "\"hello\"", badOutput)
                .Build();
            var context = DmnExecutionContextFactory.CreateExecutionContext(definition);

            Action act = () => context.ExecuteDecision("badDecision");

            act.Should().NotThrow();
            context.Diagnostics.Should().ContainSingle(d => d.DecisionName == "badDecision");
        }
    }
}
