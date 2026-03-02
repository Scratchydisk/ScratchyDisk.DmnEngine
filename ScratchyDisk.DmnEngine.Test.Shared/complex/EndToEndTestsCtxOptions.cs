using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;

namespace ScratchyDisk.DmnEngine.Test.Complex
{
    [TestClass]
    [TestCategory("Complex tests - non parallel rules evaluation")]
    public class EndToEndTestsOptNonParallelRules : EndToEndTests
    {
        protected override Action<DmnExecutionContextOptions> ConfigureCtx => (options => options.EvaluateTableRulesInParallel = false); 
    }

    [TestClass]
    [TestCategory("Complex tests - parallel rule outputs evaluation")]
    public class EndToEndTestsOptParallelOutputs : EndToEndTests
    {
        protected override Action<DmnExecutionContextOptions> ConfigureCtx => (options => options.EvaluateTableOutputsInParallel = true);
    }
}
