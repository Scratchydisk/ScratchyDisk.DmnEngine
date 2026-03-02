using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;

namespace ScratchyDisk.DmnEngine.Test.Complex
{
    [TestClass]
    [TestCategory("Complex tests - hit policy - non parallel rules evaluation")]
    public class HitPolicyTestOptNonParallelRules : HitPolicyTest
    {
        protected override Action<DmnExecutionContextOptions> ConfigureCtx => (options => options.EvaluateTableRulesInParallel = false);
    }

    [TestClass]
    [TestCategory("Complex tests - hit policy - parallel rule outputs evaluation")]
    public class HitPolicyTestOptParallelOutputs : HitPolicyTest
    {
        protected override Action<DmnExecutionContextOptions> ConfigureCtx => (options => options.EvaluateTableOutputsInParallel = true);
    }
}
