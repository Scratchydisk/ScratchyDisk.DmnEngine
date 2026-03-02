using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    [TestClass]
    [TestCategory("Code tests - parser")]
    public class DmnParserDataTypesTests13Ext: DmnParserDataTypesTests13
    {
        protected override SourceEnum Source => SourceEnum.File13ext;
    }
}
