using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;

namespace ScratchyDisk.DmnEngine.Test.Complex
{
    [TestClass]
    [TestCategory("Complex tests")]
    public class SfeelExpressionsTests13 : SfeelExpressionsTests
    {
        protected override SourceEnum Source => SourceEnum.File13;

        private static DmnExecutionContext ctxFile13;
        protected override DmnExecutionContext Ctx => ctxFile13;

        [ClassInitialize]
        public static void InitCtxFile13(TestContext testContext)
        {
            ctxFile13 = CTX("sfeel.dmn", SourceEnum.File13);
        }
    }
}
