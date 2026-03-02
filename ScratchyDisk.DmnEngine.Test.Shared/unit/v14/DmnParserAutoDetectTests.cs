using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    [TestClass]
    [TestCategory("Code tests - parser")]
    public class DmnParserAutoDetectTests
    {
        [TestMethod]
        public void DetectVersion11Test()
        {
            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
            xml += "<definitions xmlns=\"http://www.omg.org/spec/DMN/20151101/dmn.xsd\">";
            xml += "</definitions>";

            var version = DmnParser.DetectVersion(xml);
            version.Should().Be(DmnParser.DmnVersionEnum.V1_1);
        }

        [TestMethod]
        public void DetectVersion13Test()
        {
            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
            xml += "<definitions xmlns=\"https://www.omg.org/spec/DMN/20191111/MODEL/\">";
            xml += "</definitions>";

            var version = DmnParser.DetectVersion(xml);
            version.Should().Be(DmnParser.DmnVersionEnum.V1_3);
        }

        [TestMethod]
        public void DetectVersion14Test()
        {
            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
            xml += "<definitions xmlns=\"https://www.omg.org/spec/DMN/20211108/MODEL/\">";
            xml += "</definitions>";

            var version = DmnParser.DetectVersion(xml);
            version.Should().Be(DmnParser.DmnVersionEnum.V1_4);
        }

        [TestMethod]
        public void DetectVersion15Test()
        {
            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
            xml += "<definitions xmlns=\"https://www.omg.org/spec/DMN/20230324/MODEL/\">";
            xml += "</definitions>";

            var version = DmnParser.DetectVersion(xml);
            version.Should().Be(DmnParser.DmnVersionEnum.V1_5);
        }

        [TestMethod]
        public void DetectVersionUnknownNamespaceTest()
        {
            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
            xml += "<definitions xmlns=\"http://unknown.namespace/\">";
            xml += "</definitions>";

            Action act = () => DmnParser.DetectVersion(xml);
            act.Should().Throw<DmnParserException>().WithMessage("Unknown DMN namespace*");
        }

        [TestMethod]
        public void DetectVersionNoDefinitionsTest()
        {
            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><root/>";

            Action act = () => DmnParser.DetectVersion(xml);
            act.Should().Throw<DmnParserException>().WithMessage("Could not detect DMN version*");
        }

        [TestMethod]
        public void ParseAutoDetect11FileTest()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            var file = Path.Combine(dir ?? string.Empty, "dmn/test.dmn");
            var model = DmnParser.ParseAutoDetect(file);
            model.Should().NotBeNull();
            model.DmnVersion.Should().Be(DmnParser.DmnVersionEnum.V1_1);
        }

        [TestMethod]
        public void ParseAutoDetect14FileTest()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            var file = Path.Combine(dir ?? string.Empty, "dmn/dmn1.4/test.dmn");
            var model = DmnParser.ParseAutoDetect(file);
            model.Should().NotBeNull();
            model.DmnVersion.Should().Be(DmnParser.DmnVersionEnum.V1_4);
        }

        [TestMethod]
        public void ParseAutoDetect15FileTest()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            var file = Path.Combine(dir ?? string.Empty, "dmn/dmn1.5/test.dmn");
            var model = DmnParser.ParseAutoDetect(file);
            model.Should().NotBeNull();
            model.DmnVersion.Should().Be(DmnParser.DmnVersionEnum.V1_5);
        }

        [TestMethod]
        public void ParseAutoDetectNullFileTest()
        {
            Action act = () => DmnParser.ParseAutoDetect(null);
            act.Should().Throw<DmnParserException>().WithMessage("filePath is null or empty");
        }

        [TestMethod]
        public void ParseAutoDetectFileNotExistTest()
        {
            Action act = () => DmnParser.ParseAutoDetect("nonexistent.dmn");
            act.Should().Throw<DmnParserException>().WithMessage("File * doesn't exist");
        }
    }
}
