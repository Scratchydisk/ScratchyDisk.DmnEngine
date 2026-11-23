using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §13.3 Exchange Formats and the DMN 1.6 XSD
    /// (https://www.omg.org/spec/DMN/20240513/DMN16.xsd). These labels are the
    /// shared CLI and testbed projection of the parsed model version.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - parser")]
    [TestCategory("DMN 1.6 conformance")]
    public class DmnVersionInfoTests
    {
        [TestMethod]
        public void Dmn16_13_3_VersionDisplayName16()
        {
            DmnVersionInfo.GetDisplayName(DmnParser.DmnVersionEnum.V1_6)
                .Should().Be("DMN 1.6");
        }

        [TestMethod]
        public void Dmn16_13_3_MetadataVersion16()
        {
            DmnVersionInfo.GetMetadataVersion(DmnParser.DmnVersionEnum.V1_6)
                .Should().Be("1.6");
        }

        [TestMethod]
        public void Dmn16_13_3_VersionDisplayName15Unchanged()
        {
            DmnVersionInfo.GetDisplayName(DmnParser.DmnVersionEnum.V1_5)
                .Should().Be("DMN 1.5");
            DmnVersionInfo.GetMetadataVersion(DmnParser.DmnVersionEnum.V1_5)
                .Should().Be("1.5");
        }
    }
}
