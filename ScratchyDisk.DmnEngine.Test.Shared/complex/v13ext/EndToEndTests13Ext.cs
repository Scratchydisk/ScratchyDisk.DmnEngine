using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScratchyDisk.DmnEngine.Test.Complex
{
    [TestClass]
    [TestCategory("Complex tests")]
    public class EndToEndTests13Ext : EndToEndTests13
    {
        protected override SourceEnum Source => SourceEnum.File13ext;
    }
}
