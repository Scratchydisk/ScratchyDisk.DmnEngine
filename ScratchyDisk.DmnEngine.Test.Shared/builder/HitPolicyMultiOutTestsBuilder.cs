using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Test.Complex;

namespace ScratchyDisk.DmnEngine.Test.Builder
{
    [TestClass]
    [TestCategory("Complex tests - hit policy")]
    public class HitPolicyMultiOutTestsBuilder : HitPolicyMultiOutTests
    {
        protected override SourceEnum Source => SourceEnum.Builder;
    }
}
