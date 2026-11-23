using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §13.3 expressionLanguage and §11.1 B-FEEL URI.
    /// </summary>
    [TestClass]
    [TestCategory("DMN 1.6 conformance")]
    public class Dmn16ExpressionLanguageInspectorTests
    {
        private const string BFeel = "https://www.omg.org/spec/DMN/20240513/B-FEEL/";

        [TestMethod]
        public void Dmn16_13_3_DetectDefaultFeel()
        {
            var result = DmnExpressionLanguageInspector.Detect(
                "<definitions xmlns=\"https://www.omg.org/spec/DMN/20240513/MODEL/\"><decision id=\"d\"/></definitions>");

            result.ExpressionLanguage.Should().Be("FEEL");
            result.IsSupported.Should().BeTrue();
        }

        [TestMethod]
        public void Dmn16_13_3_DetectBFeel()
        {
            var result = DmnExpressionLanguageInspector.Detect(
                $"<definitions xmlns=\"https://www.omg.org/spec/DMN/20240513/MODEL/\"><literalExpression expressionLanguage=\"{BFeel}\"/></definitions>");

            result.ExpressionLanguage.Should().Be("B-FEEL");
            result.IsSupported.Should().BeFalse();
        }

        [TestMethod]
        public void Dmn16_13_3_DetectOnnxKind()
        {
            var result = DmnExpressionLanguageInspector.Detect(
                "<definitions xmlns=\"https://www.omg.org/spec/DMN/20240513/MODEL/\"><functionDefinition kind=\"ONNX\"/></definitions>");

            result.ExpressionLanguage.Should().Be("ONNX");
            result.IsSupported.Should().BeFalse();
        }

        [TestMethod]
        public void Dmn16_13_3_DetectMixedLanguages()
        {
            var result = DmnExpressionLanguageInspector.Detect(
                $"<definitions xmlns=\"https://www.omg.org/spec/DMN/20240513/MODEL/\"><literalExpression expressionLanguage=\"FEEL\"/><literalExpression expressionLanguage=\"{BFeel}\"/></definitions>");

            result.ExpressionLanguage.Should().Be("mixed");
            result.IsSupported.Should().BeFalse();
        }
    }
}
