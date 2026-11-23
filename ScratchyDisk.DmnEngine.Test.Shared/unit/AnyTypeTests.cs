using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.5 §7.3.2 (variable typeRef is optional; an omitted
    /// type is the DMN type "any") and §7.3.3 collect aggregation over numbers.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - engine")]
    public class AnyTypeTests
    {
        private const string Dmn15 = "https://www.omg.org/spec/DMN/20230324/MODEL/";

        [TestMethod]
        public void ExpressionDecisionWithoutTypeRef_EvaluatesAsAny()
        {
            var context = Context($@"
<definitions xmlns=""{Dmn15}"" id=""def"" name=""def"" namespace=""http://example.com"">
  <decision name=""Answer"" id=""d1"">
    <variable name=""Answer""/>
    <literalExpression><text>40 + 2</text></literalExpression>
  </decision>
</definitions>", "Answer");

            var result = context.ExecuteDecision("Answer");

            result.FirstResultVariables[0].Value.Should().Be(42m);
        }

        [TestMethod]
        public void ExpressionDecisionWithAnyTypeRef_EvaluatesAsAny()
        {
            var context = Context($@"
<definitions xmlns=""{Dmn15}"" id=""def"" name=""def"" namespace=""http://example.com"">
  <decision name=""Answer"" id=""d1"">
    <variable name=""Answer"" typeRef=""any""/>
    <literalExpression><text>""a"" + ""b""</text></literalExpression>
  </decision>
</definitions>", "Answer");

            var result = context.ExecuteDecision("Answer");

            result.FirstResultVariables[0].Value.Should().Be("ab");
        }

        [TestMethod]
        public void DecisionTableCollectSumWithoutOutputTypeRef_UsesNumericValues()
        {
            var context = Context($@"
<definitions xmlns=""{Dmn15}"" id=""def"" name=""def"" namespace=""http://example.com"">
  <inputData name=""x"" id=""i_x""><variable typeRef=""number"" name=""x""/></inputData>
  <decision name=""Total"" id=""d1"">
    <variable name=""Total""/>
    <decisionTable hitPolicy=""COLLECT"" aggregation=""SUM"">
      <input id=""i1""><inputExpression typeRef=""number""><text>x</text></inputExpression></input>
      <output id=""o1"" name=""Total""/>
      <rule id=""r1""><inputEntry><text>&gt; 0</text></inputEntry><outputEntry><text>10</text></outputEntry></rule>
      <rule id=""r2""><inputEntry><text>&gt; 0</text></inputEntry><outputEntry><text>20</text></outputEntry></rule>
    </decisionTable>
  </decision>
</definitions>", "Total");

            context.WithInputParameter("x", 1m);
            var result = context.ExecuteDecision("Total");

            result.FirstResultVariables[0].Value.Should().Be(30m);
        }

        [TestMethod]
        public void DecisionTableCollectMinWithNumberType_UsesDecimalValues()
        {
            var context = Context($@"
<definitions xmlns=""{Dmn15}"" id=""def"" name=""def"" namespace=""http://example.com"">
  <inputData name=""x"" id=""i_x""><variable typeRef=""number"" name=""x""/></inputData>
  <decision name=""Lowest"" id=""d1"">
    <variable typeRef=""number"" name=""Lowest""/>
    <decisionTable hitPolicy=""COLLECT"" aggregation=""MIN"">
      <input id=""i1""><inputExpression typeRef=""number""><text>x</text></inputExpression></input>
      <output id=""o1"" name=""Lowest"" typeRef=""number""/>
      <rule id=""r1""><inputEntry><text>&gt; 0</text></inputEntry><outputEntry><text>64.32</text></outputEntry></rule>
      <rule id=""r2""><inputEntry><text>&gt; 0</text></inputEntry><outputEntry><text>98.83</text></outputEntry></rule>
    </decisionTable>
  </decision>
</definitions>", "Lowest");

            context.WithInputParameter("x", 1m);
            var result = context.ExecuteDecision("Lowest");

            result.FirstResultVariables[0].Value.Should().Be(64.32m);
        }

        private static DmnExecutionContext Context(string xml, string decision)
        {
            var model = DmnParser.ParseString15(xml);
            var definition = DmnDefinitionFactory.CreateDmnDefinition(model);
            definition.Decisions.Should().ContainKey(decision);
            return DmnExecutionContextFactory.CreateExecutionContext(definition);
        }
    }
}
