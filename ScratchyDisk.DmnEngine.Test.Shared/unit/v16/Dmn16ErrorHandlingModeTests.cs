using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Definition.Builder;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §7.3.8 Error Handling.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - engine")]
    [TestCategory("DMN 1.6 conformance")]
    public class Dmn16ErrorHandlingModeTests
    {
        private static DmnDefinition Definition()
        {
            return new DmnDefinitionBuilder()
                .WithInput<int>("input", out var input)
                .WithVariable<int>("output", out var output)
                .WithExpressionDecision("decision", "input + 1", output)
                .Build();
        }

        [TestMethod]
        public void Dmn16_7_3_8_DefaultIsLenient()
        {
            var context = DmnExecutionContextFactory.CreateExecutionContext(Definition());

            context.Options.ErrorHandlingMode.Should().Be(DmnErrorHandlingMode.Lenient);
        }

        [TestMethod]
        public void Dmn16_7_3_8_ExplicitStrictRetained()
        {
            var context = DmnExecutionContextFactory.CreateExecutionContext(
                Definition(),
                options => options.ErrorHandlingMode = DmnErrorHandlingMode.Strict);

            context.Options.ErrorHandlingMode.Should().Be(DmnErrorHandlingMode.Strict);
        }

        [TestMethod]
        public void Dmn16_7_3_8_ExplicitLenientRetained()
        {
            var context = DmnExecutionContextFactory.CreateExecutionContext(
                Definition(),
                options => options.ErrorHandlingMode = DmnErrorHandlingMode.Lenient);

            context.Options.ErrorHandlingMode.Should().Be(DmnErrorHandlingMode.Lenient);
        }

        [TestMethod]
        public void Dmn16_7_3_8_InvalidModeRejected()
        {
            Action act = () => DmnExecutionContextFactory.CreateExecutionContext(
                Definition(),
                options => options.ErrorHandlingMode = (DmnErrorHandlingMode)99);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [TestMethod]
        public void Dmn16_7_3_8_ExistingOptionsCallersUnchanged()
        {
            var context = DmnExecutionContextFactory.CreateExecutionContext(
                Definition(),
                options =>
                {
                    options.RecordSnapshots = true;
                    options.EvaluateTableRulesInParallel = false;
                    options.ResolveInputAliases = false;
                });

            context.Options.RecordSnapshots.Should().BeTrue();
            context.Options.EvaluateTableRulesInParallel.Should().BeFalse();
            context.Options.ResolveInputAliases.Should().BeFalse();
        }
    }
}
