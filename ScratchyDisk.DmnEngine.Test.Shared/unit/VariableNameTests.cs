using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    [TestClass]
    [TestCategory("Code tests - engine")]
    public class VariableNameTests
    {
        private const string Dmn15 = "https://www.omg.org/spec/DMN/20230324/MODEL/";

        [TestMethod]
        public void DecisionTableOutputNameWithSlash_IsPreservedInResult()
        {
            // DMN name attributes are free text; the result variable must keep the
            // name as written even when it is not addressable from FEEL.
            var xml = $@"
<definitions xmlns=""{Dmn15}"" id=""def"" name=""def"" namespace=""http://example.com"">
  <inputData name=""x"" id=""i_x""><variable typeRef=""number"" name=""x""/></inputData>
  <decision name=""Approved"" id=""d1"">
    <variable name=""Approved""/>
    <decisionTable hitPolicy=""UNIQUE"">
      <input id=""i1""><inputExpression typeRef=""number""><text>x</text></inputExpression></input>
      <output id=""o1"" name=""Approved/Declined"" typeRef=""string""/>
      <output id=""o2"" name=""Rate"" typeRef=""string""/>
      <rule id=""r1"">
        <inputEntry><text>&gt; 0</text></inputEntry>
        <outputEntry><text>""Approved""</text></outputEntry>
        <outputEntry><text>""Standard""</text></outputEntry>
      </rule>
    </decisionTable>
  </decision>
</definitions>";

            var definition = DmnDefinitionFactory.CreateDmnDefinition(DmnParser.ParseString15(xml));
            var context = DmnExecutionContextFactory.CreateExecutionContext(definition)
                .WithInputParameter("x", 1m);

            var result = context.ExecuteDecision("Approved");

            result.First["Approved/Declined"].Value.Should().Be("Approved");
            result.First["Rate"].Value.Should().Be("Standard");
        }
    }
}
