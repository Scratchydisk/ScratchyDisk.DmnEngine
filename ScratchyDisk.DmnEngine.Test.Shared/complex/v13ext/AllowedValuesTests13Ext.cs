using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScratchyDisk.DmnEngine.Test.Complex
{
    [TestClass]
    [TestCategory("Complex tests")]
    public class AllowedValuesTests13Ext : AllowedValuesTests13
    {
        protected override SourceEnum Source => SourceEnum.File13ext;
    }
}
