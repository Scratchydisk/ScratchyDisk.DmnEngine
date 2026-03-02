using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;

namespace ScratchyDisk.DmnEngine.Test.Complex
{
    [TestClass]
    [TestCategory("Complex tests - hit policy - non parallel rules evaluation")]
    public class HitPolicyMultiOutTestsOptNonParallelRules : HitPolicyMultiOutTests
    {
        protected override Action<DmnExecutionContextOptions> ConfigureCtx => (options => options.EvaluateTableRulesInParallel = false);
    }

    [TestClass]
    [TestCategory("Complex tests - hit policy - parallel rule outputs evaluation")]
    public class HitPolicyMultiOutTestsOptParallelOutputs : HitPolicyMultiOutTests
    {
        protected override Action<DmnExecutionContextOptions> ConfigureCtx => (options => options.EvaluateTableOutputsInParallel = true);
    }
}
