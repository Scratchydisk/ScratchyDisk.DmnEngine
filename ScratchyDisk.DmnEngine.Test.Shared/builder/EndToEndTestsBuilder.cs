using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Test.Complex;

namespace ScratchyDisk.DmnEngine.Test.Builder
{
    [TestClass]
    [TestCategory("Complex tests")]
    public class EndToEndTestsBuilder : EndToEndTests
    {
        protected override SourceEnum Source => SourceEnum.Builder;
    }
}
