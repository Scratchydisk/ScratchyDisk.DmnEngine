using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Parser;
using ScratchyDisk.DmnEngine.Parser.Dto;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §13.3 Exchange Formats and the DMN 1.6 XSD
    /// (https://www.omg.org/spec/DMN/20240513/DMN16.xsd).
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - parser")]
    [TestCategory("DMN 1.6 conformance")]
    public class DmnParser16NegativeNamespaceTests
    {
        [TestMethod]
        public void Dmn16_13_3_UnknownDefinitionsNamespaceThrows()
        {
            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
            xml += "<definitions xmlns=\"http://unknown.namespace/\"></definitions>";

            Action act = () => DmnParser.DetectVersion(xml);

            act.Should().Throw<DmnParserException>()
                .WithMessage("Unknown DMN namespace*http://unknown.namespace/*");
        }

        [TestMethod]
        public void Dmn16_13_3_UnknownEmbeddedFragmentNamespaceThrows()
        {
            var xml = "<decisionTable xmlns=\"http://unknown.namespace/\"><input id=\"i1\"/></decisionTable>";
            using var reader = XmlReader.Create(new StringReader(xml));
            reader.MoveToContent();
            var table = new DecisionTable();

            Action act = () => ((IXmlSerializable)table).ReadXml(reader);

            act.Should().Throw<Exception>("an unknown embedded namespace must not silently fall back to DMN 1.3");
        }

        [TestMethod]
        public void Dmn16_13_3_SupportedNamespacesStillParse()
        {
            var cases = new[]
            {
                ("http://www.omg.org/spec/DMN/20151101/dmn.xsd", DmnParser.DmnVersionEnum.V1_1),
                ("https://www.omg.org/spec/DMN/20191111/MODEL/", DmnParser.DmnVersionEnum.V1_3),
                ("https://www.omg.org/spec/DMN/20211108/MODEL/", DmnParser.DmnVersionEnum.V1_4),
                ("https://www.omg.org/spec/DMN/20230324/MODEL/", DmnParser.DmnVersionEnum.V1_5)
            };

            foreach (var (ns, version) in cases)
            {
                var xml = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><definitions xmlns=\"{ns}\" id=\"def\" name=\"def\" namespace=\"http://example.com\"><decision id=\"d1\" name=\"D1\"><literalExpression id=\"le1\"><text>1</text></literalExpression><variable id=\"v1\" name=\"D1\" typeRef=\"number\"/></decision></definitions>";
                var model = DmnParser.ParseString(xml, version);
                model.DmnVersion.Should().Be(version);
            }
        }
    }
}
