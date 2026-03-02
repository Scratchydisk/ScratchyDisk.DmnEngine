using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;

namespace ScratchyDisk.DmnEngine.Test.Complex
{
    [TestClass]
    [TestCategory("Complex tests")]
    public class SfeelExpressionsTests15 : SfeelExpressionsTests14
    {
        protected override SourceEnum Source => SourceEnum.File15;

        private static DmnExecutionContext ctxFile15;
        protected override DmnExecutionContext Ctx => ctxFile15;

        [ClassInitialize]
        public static void InitCtxFile15(TestContext testContext)
        {
            ctxFile15 = CTX("sfeel.dmn", SourceEnum.File15);
        }
    }
}
