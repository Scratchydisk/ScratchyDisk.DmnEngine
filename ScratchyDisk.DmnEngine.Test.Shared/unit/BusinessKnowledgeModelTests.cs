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

        [TestMethod]
        public void BusinessKnowledgeModelCanUseDecisionTableBody()
        {
            var xml = $@"
<definitions xmlns=""{Dmn15}"" id=""def"" name=""def"" namespace=""http://example.com"">
  <businessKnowledgeModel name=""Risk rules"" id=""b1"">
    <variable name=""Risk rules""/>
    <encapsulatedLogic id=""f1"">
      <formalParameter name=""Risk Category"" typeRef=""string"" id=""p1""/>
      <decisionTable hitPolicy=""UNIQUE"" outputLabel=""Risk rules"" id=""t1"">
        <input id=""in1"">
          <inputExpression typeRef=""string""><text>Risk Category</text></inputExpression>
        </input>
        <output id=""out1"" name=""level"" typeRef=""number""/>
        <rule id=""r1"">
          <inputEntry id=""ie1""><text>""HIGH""</text></inputEntry>
          <outputEntry id=""oe1""><text>3</text></outputEntry>
        </rule>
        <rule id=""r2"">
          <inputEntry id=""ie2""><text>""LOW""</text></inputEntry>
          <outputEntry id=""oe2""><text>1</text></outputEntry>
        </rule>
      </decisionTable>
    </encapsulatedLogic>
  </businessKnowledgeModel>
  <decision name=""d"" id=""d1"">
    <variable typeRef=""number"" name=""d""/>
    <knowledgeRequirement id=""k1""><requiredKnowledge href=""#b1""/></knowledgeRequirement>
    <invocation id=""inv1"">
      <literalExpression id=""le1""><text>Risk rules</text></literalExpression>
      <binding id=""b1"">
        <parameter id=""par1"" name=""Risk Category""/>
        <literalExpression id=""le2""><text>""HIGH""</text></literalExpression>
      </binding>
    </invocation>
  </decision>
</definitions>";

            var definition = DmnDefinitionFactory.CreateDmnDefinition(DmnParser.ParseString15(xml));
            definition.BusinessKnowledgeModels.Should().ContainKey("Risk rules");

            var result = DmnExecutionContextFactory.CreateExecutionContext(definition)
                .ExecuteDecision("d");

            result.FirstResultVariables[0].Value.Should().Be(3m);
        }

        [TestMethod]
        public void BusinessKnowledgeModelFormalParameterNamesMayContainHyphens()
        {
            var xml = $@"
<definitions xmlns=""{Dmn15}"" id=""def"" name=""def"" namespace=""http://example.com"">
  <businessKnowledgeModel name=""Pre-risk table"" id=""b1"">
    <variable name=""Pre-risk table""/>
    <encapsulatedLogic id=""f1"">
      <formalParameter name=""Pre-Risk Category"" typeRef=""string"" id=""p1""/>
      <decisionTable hitPolicy=""UNIQUE"" id=""t1"">
        <input id=""in1"">
          <inputExpression typeRef=""string""><text>Pre-Risk Category</text></inputExpression>
        </input>
        <output id=""out1"" name=""category"" typeRef=""string""/>
        <rule id=""r1"">
          <inputEntry id=""ie1""><text>""HIGH""</text></inputEntry>
          <outputEntry id=""oe1""><text>""VERY LOW""</text></outputEntry>
        </rule>
      </decisionTable>
    </encapsulatedLogic>
  </businessKnowledgeModel>
  <decision name=""d"" id=""d1"">
    <variable typeRef=""string"" name=""d""/>
    <knowledgeRequirement id=""k1""><requiredKnowledge href=""#b1""/></knowledgeRequirement>
    <invocation id=""inv1"">
      <literalExpression id=""le1""><text>Pre-risk table</text></literalExpression>
      <binding id=""b1"">
        <parameter id=""par1"" name=""Pre-Risk Category""/>
        <literalExpression id=""le2""><text>""HIGH""</text></literalExpression>
      </binding>
    </invocation>
  </decision>
</definitions>";

            var definition = DmnDefinitionFactory.CreateDmnDefinition(DmnParser.ParseString15(xml));

            var result = DmnExecutionContextFactory.CreateExecutionContext(definition)
                .ExecuteDecision("d");

            result.FirstResultVariables[0].Value.Should().Be("VERY LOW");
        }

        [TestMethod]
        public void BoxedContextDecisionCanHoldNestedDecisionTable()
        {
            var xml = $@"
<definitions xmlns=""{Dmn15}"" id=""def"" name=""def"" namespace=""http://example.com"">
  <inputData name=""x"" id=""i_x""><variable typeRef=""number"" name=""x""/></inputData>
  <decision name=""boxed"" id=""d1"">
    <variable typeRef=""number"" name=""boxed""/>
    <informationRequirement id=""ir1""><requiredInput href=""#i_x""/></informationRequirement>
    <context id=""c1"">
      <contextEntry>
        <variable name=""Factor"" typeRef=""number"" id=""v1""/>
        <literalExpression id=""le1""><text>2</text></literalExpression>
      </contextEntry>
      <contextEntry>
        <variable name=""Table Result"" typeRef=""number"" id=""v2""/>
        <decisionTable hitPolicy=""UNIQUE"" id=""t1"">
          <input id=""in1"">
            <inputExpression typeRef=""number""><text>Factor</text></inputExpression>
          </input>
          <output id=""out1""/>
          <rule id=""r1"">
            <inputEntry id=""ie1""><text>2</text></inputEntry>
            <outputEntry id=""oe1""><text>x + 20</text></outputEntry>
          </rule>
        </decisionTable>
      </contextEntry>
      <contextEntry>
        <literalExpression id=""le2""><text>Table Result</text></literalExpression>
      </contextEntry>
    </context>
  </decision>
</definitions>";

            var definition = DmnDefinitionFactory.CreateDmnDefinition(DmnParser.ParseString15(xml));

            var result = DmnExecutionContextFactory.CreateExecutionContext(definition)
                .WithInputParameter("x", 1m)
                .ExecuteDecision("boxed");

            result.FirstResultVariables[0].Value.Should().Be(21m);
        }

        [TestMethod]
        public void BoxedContextEntryCanHoldFunctionDefinition()
        {
            var xml = $@"
<definitions xmlns=""{Dmn15}"" id=""def"" name=""def"" namespace=""http://example.com"">
  <decision name=""boxed"" id=""d1"">
    <variable typeRef=""number"" name=""boxed""/>
    <context id=""c1"">
      <contextEntry>
        <variable name=""double"" id=""v1""/>
        <functionDefinition id=""f1"">
          <formalParameter name=""p"" typeRef=""number"" id=""p1""/>
          <literalExpression id=""le1""><text>p * 2</text></literalExpression>
        </functionDefinition>
      </contextEntry>
      <contextEntry>
        <literalExpression id=""le2""><text>double(21)</text></literalExpression>
      </contextEntry>
    </context>
  </decision>
</definitions>";

            var definition = DmnDefinitionFactory.CreateDmnDefinition(DmnParser.ParseString15(xml));

            var result = DmnExecutionContextFactory.CreateExecutionContext(definition)
                .ExecuteDecision("boxed");

            result.FirstResultVariables[0].Value.Should().Be(42m);
        }
    }
}
