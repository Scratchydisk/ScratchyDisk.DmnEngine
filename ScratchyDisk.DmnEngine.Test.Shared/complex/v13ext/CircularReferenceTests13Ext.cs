using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScratchyDisk.DmnEngine.Test.Complex
{
    [TestClass]
    [TestCategory("Not regular test")]
    public class CircularReferenceTests13Ext: CircularReferenceTests13
    {
        protected override SourceEnum Source => SourceEnum.File13ext;
    }
}
