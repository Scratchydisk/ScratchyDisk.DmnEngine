using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Test.Complex;

namespace ScratchyDisk.DmnEngine.Test.Builder
{
    [TestClass]
    [TestCategory("Complex tests - builder")]
    public class ParallelTestsBuilder : ParallelTests
    {
        protected override SourceEnum Source => SourceEnum.Builder;

        private static DmnDefinition defBuilder;
        protected override DmnDefinition DefStatic => defBuilder;

        [ClassInitialize]
        public static void InitCtxBuilder(TestContext testContext)
        {
            defBuilder = DEF("parallel.dmn", DmnTestBase.SourceEnum.Builder);
        }
    }
}
