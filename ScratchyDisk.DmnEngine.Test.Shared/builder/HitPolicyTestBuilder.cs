using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Test.Complex;

namespace ScratchyDisk.DmnEngine.Test.Builder
{
    [TestClass]
    [TestCategory("Complex tests - hit policy - builder")]
    public class HitPolicyTestBuilder : HitPolicyTest
    {
        protected override SourceEnum Source => SourceEnum.Builder;

        //Not applicable tests
        [Ignore]
        public override void WrongHitPolicyTest()
        {
        }

        [Ignore]
        public override void WrongHitPolicyAggregationTest()
        {
        }

    }
}
