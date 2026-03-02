using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScratchyDisk.DmnEngine.Test.Complex
{
    [TestClass]
    [TestCategory("Complex tests")]
    public class ComplexInputsTests13Ext : ComplexInputsTests13
    {
        protected override SourceEnum Source => SourceEnum.File13ext;
    }
}
