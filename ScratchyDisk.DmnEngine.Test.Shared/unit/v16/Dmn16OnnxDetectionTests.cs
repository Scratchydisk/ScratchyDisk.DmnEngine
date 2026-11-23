using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §2.1 (ONNX is optional) and §10.3.2.13.3 externally defined functions.
    /// </summary>
    [TestClass]
    [TestCategory("DMN 1.6 conformance")]
    public class Dmn16OnnxDetectionTests
    {
        private static string LiteralModel(string language)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   "<definitions xmlns=\"https://www.omg.org/spec/DMN/20240513/MODEL/\" id=\"def\" name=\"def\" namespace=\"http://example.com\">" +
                   "<decision id=\"d1\" name=\"D1\">" +
                   $"<literalExpression id=\"le1\" expressionLanguage=\"{language}\"><text>1</text></literalExpression>" +
                   "<variable id=\"v1\" name=\"D1\" typeRef=\"number\"/></decision></definitions>";
        }

        [TestMethod]
        public void Dmn16_2_1_OnnxKindFailsFast()
        {
            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                      "<definitions xmlns=\"https://www.omg.org/spec/DMN/20240513/MODEL/\" id=\"def\" name=\"def\" namespace=\"http://example.com\">" +
                      "<functionDefinition id=\"f1\" kind=\"ONNX\" /></definitions>";

            Action act = () => DmnParser.ParseString16(xml);

            act.Should().Throw<DmnParserException>()
                .WithMessage("*ONNX*optional*not implemented*");
        }

        [TestMethod]
        public void Dmn16_2_1_OnnxExpressionLanguageFailsFast()
        {
            var model = DmnParser.ParseString16(LiteralModel("ONNX"));

            Action act = () => DmnDefinitionFactory.CreateDmnDefinition(model);

            act.Should().Throw<DmnParserException>()
                .WithMessage("*ONNX*optional*not implemented*");
        }

        [TestMethod]
        public void Dmn16_2_1_JavaAndPmmlLanguagesUnchanged()
        {
            var java = DmnDefinitionFactory.CreateDmnDefinition(DmnParser.ParseString16(LiteralModel("Java")));
            var pmml = DmnDefinitionFactory.CreateDmnDefinition(DmnParser.ParseString16(LiteralModel("PMML")));

            java.Decisions.Should().ContainKey("D1");
            pmml.Decisions.Should().ContainKey("D1");
        }
    }
}
