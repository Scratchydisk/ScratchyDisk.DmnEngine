using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScratchyDisk.DmnEngine.Test.Complex
{
    [TestClass]
    [TestCategory("Complex tests - hit policy")]
    public class HitPolicyMultiOutTests13 : HitPolicyMultiOutTests
    {
        protected override SourceEnum Source => SourceEnum.File13;
    }
}
