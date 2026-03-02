using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;

namespace ScratchyDisk.DmnEngine.Test.Complex
{
    [TestClass]
    [TestCategory("Complex tests")]
    public class ParallelTests15 : ParallelTests14
    {
        protected override SourceEnum Source => SourceEnum.File15;

        private static DmnDefinition defFile15;
        protected override DmnDefinition DefStatic => defFile15;

        [ClassInitialize]
        public static void InitCtxFile15(TestContext testContext)
        {
            defFile15 = DEF("parallel.dmn", SourceEnum.File15);
        }
    }
}
