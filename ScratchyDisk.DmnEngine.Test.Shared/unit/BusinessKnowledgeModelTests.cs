using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    [TestClass]
    [TestCategory("Code tests - engine")]
    public class BusinessKnowledgeModelTests
    {
        private const string Dmn15 = "https://www.omg.org/spec/DMN/20230324/MODEL/";

        [TestMethod]
        public void DecisionCanInvokeBusinessKnowledgeModel()
        {
            var xml = $@"
<definitions xmlns=""{Dmn15}"" id=""def"" name=""def"" namespace=""http://example.com"">
  <inputData name=""x"" id=""i_x""><variable typeRef=""number"" name=""x""/></inputData>
  <businessKnowledgeModel name=""Double"" id=""b1"">
    <variable name=""Double""/>
    <encapsulatedLogic id=""f1"">
      <formalParameter name=""p"" typeRef=""number"" id=""p1""/>
      <literalExpression typeRef=""number""><text>p * 2</text></literalExpression>
    </encapsulatedLogic>
  </businessKnowledgeModel>
  <decision name=""d"" id=""d1"">
    <variable typeRef=""number"" name=""d""/>
    <knowledgeRequirement id=""k1""><requiredKnowledge href=""#b1""/></knowledgeRequirement>
    <literalExpression typeRef=""number""><text>Double(x) + 1</text></literalExpression>
  </decision>
</definitions>";

            var definition = DmnDefinitionFactory.CreateDmnDefinition(DmnParser.ParseString15(xml));
            definition.BusinessKnowledgeModels.Should().ContainKey("Double");

            var context = DmnExecutionContextFactory.CreateExecutionContext(definition)
                .WithInputParameter("x", 20m);

            var result = context.ExecuteDecision("d");

            result.FirstResultVariables[0].Value.Should().Be(41m);
        }
    }
}
