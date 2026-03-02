using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;
using ScratchyDisk.DmnEngine.Test.Complex;

namespace ScratchyDisk.DmnEngine.Test.Builder
{
    [TestClass]
    [TestCategory("Complex tests - builder")]
    public class SfeelExpressionsTestsBuilder : SfeelExpressionsTests
    {
        protected override SourceEnum Source => SourceEnum.Builder;

        private static DmnExecutionContext ctxBuilder;
        protected override DmnExecutionContext Ctx => ctxBuilder;

        [ClassInitialize]
        public static void InitCtxBuilder(TestContext testContext)
        {
            ctxBuilder = CTX("sfeel.dmn", SourceEnum.Builder);
        }
    }
}
