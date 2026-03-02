using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScratchyDisk.DmnEngine.Test.Complex
{
    [TestClass]
    [TestCategory("Complex tests - hit policy")]
    public class HitPolicyTest13Ext : HitPolicyTest13
    {
        protected override SourceEnum Source => SourceEnum.File13ext;
    }
}
