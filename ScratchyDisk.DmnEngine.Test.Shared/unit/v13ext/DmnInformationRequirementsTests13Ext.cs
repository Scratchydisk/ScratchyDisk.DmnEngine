using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScratchyDisk.DmnEngine.Test.Unit
{

    [TestClass]
    [TestCategory("Code tests - parser")]
    public class DmnInformationRequirementsTests13Ext : DmnInformationRequirementsTests13
    {
        protected override SourceEnum Source => SourceEnum.File13ext;
    }
}
