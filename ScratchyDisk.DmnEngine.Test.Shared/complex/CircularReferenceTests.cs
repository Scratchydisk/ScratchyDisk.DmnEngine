using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScratchyDisk.DmnEngine.Test.Complex
{
    [TestClass]
    [TestCategory("Not regular test")]
    public class CircularReferenceTests : DmnTestBase
    {
        //[TestMethod] - uncomment to see the StackOverflowException with circular dependency in model
        public void CircularReferenceTest()
        {
            var ctx = CTX("circular_err.dmn");
            ctx.WithInputParameter("a", 0);
            ctx.ExecuteDecision("Second");
        }
    }
}
