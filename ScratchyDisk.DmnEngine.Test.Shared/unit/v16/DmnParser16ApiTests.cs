using System;
using System.IO;
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
    public class DmnParser16ApiTests
    {
        private const string Dmn16 =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<definitions xmlns=\"https://www.omg.org/spec/DMN/20240513/MODEL/\" id=\"def\" name=\"def\" namespace=\"http://example.com\">" +
            "<decision id=\"d1\" name=\"D1\"><literalExpression id=\"le1\"><text>1</text></literalExpression>" +
            "<variable id=\"v1\" name=\"D1\" typeRef=\"number\"/></decision></definitions>";

        private const string Dmn15 =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<definitions xmlns=\"https://www.omg.org/spec/DMN/20230324/MODEL/\" id=\"def\" name=\"def\" namespace=\"http://example.com\">" +
            "<decision id=\"d1\" name=\"D1\"><literalExpression id=\"le1\"><text>1</text></literalExpression>" +
            "<variable id=\"v1\" name=\"D1\" typeRef=\"number\"/></decision></definitions>";

        [TestMethod]
        public void Dmn16_13_3_Parse16FileSetsVersion()
        {
            var file = Path.Combine(Path.GetTempPath(), $"dmn16-{Guid.NewGuid():N}.dmn");
            try
            {
                File.WriteAllText(file, Dmn16);

                var model = DmnParser.Parse16(file);

                model.Should().NotBeNull();
                model.DmnVersion.Should().Be(DmnParser.DmnVersionEnum.V1_6);
            }
            finally
            {
                if (File.Exists(file)) File.Delete(file);
            }
        }

        [TestMethod]
        public void Dmn16_13_3_ParseString16SetsVersion()
        {
            var model = DmnParser.ParseString16(Dmn16);

            model.Should().NotBeNull();
            model.DmnVersion.Should().Be(DmnParser.DmnVersionEnum.V1_6);
        }

        [TestMethod]
        public void Dmn16_13_3_Parse15AndParseString15Unchanged()
        {
            var file = Path.Combine(Path.GetTempPath(), $"dmn15-{Guid.NewGuid():N}.dmn");
            try
            {
                File.WriteAllText(file, Dmn15);

                DmnParser.Parse15(file).DmnVersion.Should().Be(DmnParser.DmnVersionEnum.V1_5);
                DmnParser.ParseString15(Dmn15).DmnVersion.Should().Be(DmnParser.DmnVersionEnum.V1_5);
            }
            finally
            {
                if (File.Exists(file)) File.Delete(file);
            }
        }

        [TestMethod]
        public void Dmn16_13_3_Parse16InvalidDocumentThrows()
        {
            Action act = () => DmnParser.ParseString16("<definitions");

            act.Should().Throw<DmnParserException>();
        }
    }
}
