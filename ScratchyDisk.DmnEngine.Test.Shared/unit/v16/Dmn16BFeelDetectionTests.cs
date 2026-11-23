using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §11.1 and the B-FEEL URI
    /// (https://www.omg.org/spec/DMN/20240513/B-FEEL/).
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - parser")]
    [TestCategory("DMN 1.6 conformance")]
    public class Dmn16BFeelDetectionTests
    {
        private const string BFeel = "https://www.omg.org/spec/DMN/20240513/B-FEEL/";

        private static string LiteralModel(string languageAttribute)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   "<definitions xmlns=\"https://www.omg.org/spec/DMN/20240513/MODEL/\" id=\"def\" name=\"def\" namespace=\"http://example.com\">" +
                   "<decision id=\"d1\" name=\"D1\">" +
                   $"<literalExpression id=\"le1\"{languageAttribute}><text>1</text></literalExpression>" +
                   "<variable id=\"v1\" name=\"D1\" typeRef=\"number\"/></decision></definitions>";
        }

        [TestMethod]
        public void Dmn16_11_1_BFeelLiteralExpressionFailsFast()
        {
            var model = DmnParser.ParseString16(LiteralModel($" expressionLanguage=\"{BFeel}\""));

            Action act = () => DmnDefinitionFactory.CreateDmnDefinition(model);

            act.Should().Throw<DmnParserException>()
                .WithMessage("*B-FEEL*FEEL*");
        }

        [TestMethod]
        public void Dmn16_11_1_FeelLanguageStillWorks()
        {
            var model = DmnParser.ParseString16(LiteralModel(" expressionLanguage=\"FEEL\""));

            var definition = DmnDefinitionFactory.CreateDmnDefinition(model);

            definition.Decisions.Should().ContainKey("D1");
        }

        [TestMethod]
        public void Dmn16_11_1_OmittedLanguageStillWorks()
        {
            var model = DmnParser.ParseString16(LiteralModel(""));

            var definition = DmnDefinitionFactory.CreateDmnDefinition(model);

            definition.Decisions.Should().ContainKey("D1");
        }

        [TestMethod]
        public void Dmn16_11_1_BFeelDecisionTableInputExpressionFailsFast()
        {
            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                      "<definitions xmlns=\"https://www.omg.org/spec/DMN/20240513/MODEL/\" id=\"def\" name=\"def\" namespace=\"http://example.com\">" +
                      "<inputData id=\"i1\" name=\"Age\" />" +
                      "<decision id=\"d1\" name=\"D1\">" +
                      "<informationRequirement id=\"ir1\"><requiredInput href=\"#i1\" /></informationRequirement>" +
                      "<decisionTable id=\"dt1\" hitPolicy=\"UNIQUE\">" +
                      $"<input id=\"in1\"><inputExpression id=\"ie1\" typeRef=\"number\" expressionLanguage=\"{BFeel}\"><text>Age</text></inputExpression></input>" +
                      "<output id=\"out1\" name=\"D1\" typeRef=\"boolean\" />" +
                      "<rule id=\"r1\"><inputEntry id=\"ie\"><text>&gt;= 18</text></inputEntry><outputEntry id=\"oe\"><text>true</text></outputEntry></rule>" +
                      "</decisionTable></decision></definitions>";
            var model = DmnParser.ParseString16(xml);

            Action act = () => DmnDefinitionFactory.CreateDmnDefinition(model);

            act.Should().Throw<DmnParserException>().WithMessage("*B-FEEL*");
        }
    }
}
