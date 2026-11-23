using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;
using ScratchyDisk.DmnEngine.Feel.Types;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Tests for the boxed control-flow expressions: conditional, filter, for, some and every.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - engine")]
    public class BoxedExpressionConstructTests
    {
        private const string Dmn15 = "https://www.omg.org/spec/DMN/20230324/MODEL/";

        [TestMethod]
        public void ConditionalReturnsThenValueWhenConditionIsTrue()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d"" typeRef=""string""/>
    <conditional>
      <if><literalExpression><text>true</text></literalExpression></if>
      <then><literalExpression><text>""then""</text></literalExpression></then>
      <else><literalExpression><text>""else""</text></literalExpression></else>
    </conditional>
  </decision>");

            value.Should().Be("then");
        }

        [TestMethod]
        public void ConditionalReturnsElseValueWhenConditionIsFalse()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d"" typeRef=""string""/>
    <conditional>
      <if><literalExpression><text>false</text></literalExpression></if>
      <then><literalExpression><text>""then""</text></literalExpression></then>
      <else><literalExpression><text>""else""</text></literalExpression></else>
    </conditional>
  </decision>");

            value.Should().Be("else");
        }

        [TestMethod]
        public void ConditionalReturnsNullWhenConditionIsNotBoolean()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d""/>
    <conditional>
      <if><literalExpression><text>""abc""</text></literalExpression></if>
      <then><literalExpression><text>""then""</text></literalExpression></then>
      <else><literalExpression><text>""else""</text></literalExpression></else>
    </conditional>
  </decision>");

            value.Should().BeNull();
        }

        [TestMethod]
        public void ConditionalConditionCanReferenceInputData()
        {
            var value = Evaluate(@"
  <inputData name=""flag"" id=""i_flag""><variable name=""flag"" typeRef=""boolean""/></inputData>
  <decision name=""d"" id=""d1"">
    <variable name=""d"" typeRef=""string""/>
    <conditional>
      <if><literalExpression><text>flag</text></literalExpression></if>
      <then><literalExpression><text>""then""</text></literalExpression></then>
      <else><literalExpression><text>""else""</text></literalExpression></else>
    </conditional>
  </decision>", ("flag", true));

            value.Should().Be("then");
        }

        [TestMethod]
        public void FilterReturnsMatchingValues()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d""/>
    <filter>
      <in><literalExpression><text>[1,2,3,4,5]</text></literalExpression></in>
      <match><literalExpression><text>item &gt;= 3</text></literalExpression></match>
    </filter>
  </decision>");

            AsList(value).Should().Equal(3m, 4m, 5m);
        }

        [TestMethod]
        public void FilterReturnsEmptyListWhenNothingMatches()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d""/>
    <filter>
      <in><literalExpression><text>[1,2,3]</text></literalExpression></in>
      <match><literalExpression><text>item &gt; 5</text></literalExpression></match>
    </filter>
  </decision>");

            AsList(value).Should().BeEmpty();
        }

        [TestMethod]
        public void FilterReturnsNullWhenMatchIsNotBoolean()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d""/>
    <filter>
      <in><literalExpression><text>[1,2,3]</text></literalExpression></in>
      <match><literalExpression><text>""not a boolean""</text></literalExpression></match>
    </filter>
  </decision>");

            value.Should().BeNull();
        }

        [TestMethod]
        public void FilterReturnsNullWhenAnyMatchIsNotBoolean()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d""/>
    <filter>
      <in><literalExpression><text>[1,2,3]</text></literalExpression></in>
      <match><literalExpression><text>if (item &lt;= 2) then true else ""not a boolean""</text></literalExpression></match>
    </filter>
  </decision>");

            value.Should().BeNull();
        }

        [TestMethod]
        public void ForMapsTheValuesOfTheSourceList()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d""/>
    <for iteratorVariable=""element"">
      <in><literalExpression><text>[1,2,3,4,5]</text></literalExpression></in>
      <return><literalExpression><text>element * 2</text></literalExpression></return>
    </for>
  </decision>");

            AsList(value).Should().Equal(2m, 4m, 6m, 8m, 10m);
        }

        [TestMethod]
        public void ForReturnsEmptyListWhenSourceIsEmpty()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d""/>
    <for iteratorVariable=""element"">
      <in><literalExpression><text>[]</text></literalExpression></in>
      <return><literalExpression><text>element * 2</text></literalExpression></return>
    </for>
  </decision>");

            AsList(value).Should().BeEmpty();
        }

        [TestMethod]
        public void ForReturnsNullWhenSourceIsNotIterable()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d""/>
    <for iteratorVariable=""element"">
      <in><literalExpression><text>1</text></literalExpression></in>
      <return><literalExpression><text>element * 2</text></literalExpression></return>
    </for>
  </decision>");

            value.Should().BeNull();
        }

        [TestMethod]
        public void ForIteratesOverARange()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d""/>
    <for iteratorVariable=""element"">
      <in><literalExpression><text>[1..3]</text></literalExpression></in>
      <return><literalExpression><text>element * 2</text></literalExpression></return>
    </for>
  </decision>");

            AsList(value).Should().Equal(2m, 4m, 6m);
        }

        [TestMethod]
        public void SomeIsTrueWhenOneValueSatisfiesThePredicate()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d"" typeRef=""boolean""/>
    <some iteratorVariable=""element"">
      <in><literalExpression><text>[1,2,3,4,5]</text></literalExpression></in>
      <satisfies><literalExpression><text>element &gt;= 3</text></literalExpression></satisfies>
    </some>
  </decision>");

            value.Should().Be(true);
        }

        [TestMethod]
        public void SomeIsFalseWhenNoValueSatisfiesThePredicate()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d"" typeRef=""boolean""/>
    <some iteratorVariable=""element"">
      <in><literalExpression><text>[1,2,3]</text></literalExpression></in>
      <satisfies><literalExpression><text>element &gt; 5</text></literalExpression></satisfies>
    </some>
  </decision>");

            value.Should().Be(false);
        }

        [TestMethod]
        public void SomeIsFalseForAnEmptyList()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d"" typeRef=""boolean""/>
    <some iteratorVariable=""element"">
      <in><literalExpression><text>[]</text></literalExpression></in>
      <satisfies><literalExpression><text>true</text></literalExpression></satisfies>
    </some>
  </decision>");

            value.Should().Be(false);
        }

        [TestMethod]
        public void SomeReturnsNullWhenSatisfiesIsNotBoolean()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d""/>
    <some iteratorVariable=""element"">
      <in><literalExpression><text>1</text></literalExpression></in>
      <satisfies><literalExpression><text>""not a boolean""</text></literalExpression></satisfies>
    </some>
  </decision>");

            value.Should().BeNull();
        }

        [TestMethod]
        public void EveryIsTrueWhenAllValuesSatisfyThePredicate()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d"" typeRef=""boolean""/>
    <every iteratorVariable=""element"">
      <in><literalExpression><text>[1,2,3,4,5]</text></literalExpression></in>
      <satisfies><literalExpression><text>element &gt; 0</text></literalExpression></satisfies>
    </every>
  </decision>");

            value.Should().Be(true);
        }

        [TestMethod]
        public void EveryIsFalseWhenOneValueDoesNotSatisfyThePredicate()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d"" typeRef=""boolean""/>
    <every iteratorVariable=""element"">
      <in><literalExpression><text>[1,2,3]</text></literalExpression></in>
      <satisfies><literalExpression><text>element &gt; 2</text></literalExpression></satisfies>
    </every>
  </decision>");

            value.Should().Be(false);
        }

        [TestMethod]
        public void EveryIsTrueForAnEmptyList()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d"" typeRef=""boolean""/>
    <every iteratorVariable=""element"">
      <in><literalExpression><text>[]</text></literalExpression></in>
      <satisfies><literalExpression><text>true</text></literalExpression></satisfies>
    </every>
  </decision>");

            value.Should().Be(true);
        }

        [TestMethod]
        public void EveryReturnsNullWhenSatisfiesIsNotBoolean()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d""/>
    <every iteratorVariable=""element"">
      <in><literalExpression><text>[1,2]</text></literalExpression></in>
      <satisfies><literalExpression><text>if (element = 1) then true else ""not a boolean""</text></literalExpression></satisfies>
    </every>
  </decision>");

            value.Should().BeNull();
        }

        [TestMethod]
        public void FilterCanBeNestedInsideForWithTheIteratorInScope()
        {
            var value = Evaluate(@"
  <decision name=""d"" id=""d1"">
    <variable name=""d""/>
    <for iteratorVariable=""lower"">
      <in><literalExpression><text>[1,2,3]</text></literalExpression></in>
      <return>
        <filter>
          <in><literalExpression><text>[1,2,3]</text></literalExpression></in>
          <match><literalExpression><text>item &gt;= lower</text></literalExpression></match>
        </filter>
      </return>
    </for>
  </decision>");

            var lists = AsList(value);
            lists.Should().HaveCount(3);
            AsList(lists[0]).Should().Equal(1m, 2m, 3m);
            AsList(lists[1]).Should().Equal(2m, 3m);
            AsList(lists[2]).Should().Equal(3m);
        }

        [TestMethod]
        public void ContextEntryCanHoldAConditional()
        {
            var value = Evaluate(@"
  <inputData name=""flag"" id=""i_flag""><variable name=""flag"" typeRef=""boolean""/></inputData>
  <decision name=""d"" id=""d1"">
    <variable name=""d""/>
    <context>
      <contextEntry>
        <variable name=""a""/>
        <conditional>
          <if><literalExpression><text>flag</text></literalExpression></if>
          <then><literalExpression><text>1</text></literalExpression></then>
          <else><literalExpression><text>2</text></literalExpression></else>
        </conditional>
      </contextEntry>
      <contextEntry>
        <variable name=""b""/>
        <literalExpression><text>a + 10</text></literalExpression>
      </contextEntry>
    </context>
  </decision>", ("flag", true));

            var context = value.Should().BeOfType<FeelContext>().Subject;
            context["a"].Should().Be(1m);
            context["b"].Should().Be(11m);
        }

        [TestMethod]
        public void BusinessKnowledgeModelBodyCanBeAConditional()
        {
            var value = Evaluate(@"
  <businessKnowledgeModel name=""Label"" id=""b1"">
    <variable name=""Label""/>
    <encapsulatedLogic id=""f1"">
      <formalParameter name=""level"" typeRef=""number"" id=""p1""/>
      <conditional>
        <if><literalExpression><text>level &gt; 2</text></literalExpression></if>
        <then><literalExpression><text>""HIGH""</text></literalExpression></then>
        <else><literalExpression><text>""LOW""</text></literalExpression></else>
      </conditional>
    </encapsulatedLogic>
  </businessKnowledgeModel>
  <decision name=""d"" id=""d1"">
    <variable name=""d"" typeRef=""string""/>
    <knowledgeRequirement id=""k1""><requiredKnowledge href=""#b1""/></knowledgeRequirement>
    <literalExpression typeRef=""string""><text>Label(3)</text></literalExpression>
  </decision>");

            value.Should().Be("HIGH");
        }

        private static IReadOnlyList<object> AsList(object value)
        {
            value.Should().BeAssignableTo<IReadOnlyList<object>>();
            return (IReadOnlyList<object>)value;
        }

        private static object Evaluate(string decisionXml, params (string Name, object Value)[] inputs)
        {
            var xml = $@"
<definitions xmlns=""{Dmn15}"" id=""def"" name=""def"" namespace=""http://example.com"">
{decisionXml}
</definitions>";

            var definition = DmnDefinitionFactory.CreateDmnDefinition(DmnParser.ParseString15(xml));
            var context = DmnExecutionContextFactory.CreateExecutionContext(definition);
            foreach (var input in inputs)
                context = context.WithInputParameter(input.Name, input.Value);

            return context.ExecuteDecision("d").FirstResultVariables[0].Value;
        }
    }
}
