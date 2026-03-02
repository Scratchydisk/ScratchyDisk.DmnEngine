using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScratchyDisk.DmnEngine.Test.Complex
{
    [TestClass]
    [TestCategory("Complex tests")]
    public class CircularReferenceTests14 : CircularReferenceTests13Ext
    {
        protected override SourceEnum Source => SourceEnum.File14;
    }
}
