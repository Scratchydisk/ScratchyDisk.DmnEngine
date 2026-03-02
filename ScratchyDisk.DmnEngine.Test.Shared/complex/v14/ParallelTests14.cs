using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;

namespace ScratchyDisk.DmnEngine.Test.Complex
{
    [TestClass]
    [TestCategory("Complex tests")]
    public class ParallelTests14 : ParallelTests13Ext
    {
        protected override SourceEnum Source => SourceEnum.File14;

        private static DmnDefinition defFile14;
        protected override DmnDefinition DefStatic => defFile14;

        [ClassInitialize]
        public static void InitCtxFile14(TestContext testContext)
        {
            defFile14 = DEF("parallel.dmn", SourceEnum.File14);
        }
    }
}
