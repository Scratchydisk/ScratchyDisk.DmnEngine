using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §13.3 Exchange Formats and the DMN 1.6 XSD
    /// (https://www.omg.org/spec/DMN/20240513/DMN16.xsd).
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - parser")]
    [TestCategory("DMN 1.6 conformance")]
    public class DmnParserAutoDetectTests16
    {
        [TestMethod]
        public void Dmn16_13_3_DetectVersion16Namespace()
        {
            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
            xml += "<definitions xmlns=\"https://www.omg.org/spec/DMN/20240513/MODEL/\">";
            xml += "</definitions>";

            var version = DmnParser.DetectVersion(xml);

            // The V1_6 enum member is added by the implementation task. Keeping this
            // assertion string-based lets the test compile and fail for the right
            // reason while the production API is still at DMN 1.5.
            version.ToString().Should().Be("V1_6");
        }

        [TestMethod]
        public void Dmn16_13_3_ExistingVersionDetectionUnchanged()
        {
            var cases = new[]
            {
                ("http://www.omg.org/spec/DMN/20151101/dmn.xsd", DmnParser.DmnVersionEnum.V1_1),
                ("https://www.omg.org/spec/DMN/20191111/MODEL/", DmnParser.DmnVersionEnum.V1_3),
                ("https://www.omg.org/spec/DMN/20211108/MODEL/", DmnParser.DmnVersionEnum.V1_4),
                ("https://www.omg.org/spec/DMN/20230324/MODEL/", DmnParser.DmnVersionEnum.V1_5)
            };

            foreach (var (ns, expected) in cases)
            {
                var xml = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><definitions xmlns=\"{ns}\"></definitions>";
                DmnParser.DetectVersion(xml).Should().Be(expected, "the {0} namespace must keep its existing version", ns);
            }
        }

        [TestMethod]
        public void Dmn16_13_3_DetectVersionUnknownNamespace()
        {
            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
            xml += "<definitions xmlns=\"http://unknown.namespace/\">";
            xml += "</definitions>";

            Action act = () => DmnParser.DetectVersion(xml);

            act.Should().Throw<DmnParserException>()
                .WithMessage("Unknown DMN namespace*");
        }
    }
}
