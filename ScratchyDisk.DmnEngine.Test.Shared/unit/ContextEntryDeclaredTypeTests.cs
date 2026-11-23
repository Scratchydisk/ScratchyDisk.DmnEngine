using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// A boxed context entry converts its value to the entry's declared type: a single
    /// value becomes a one-item list (and back) and a date becomes a date and time.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - engine")]
    public class ContextEntryDeclaredTypeTests
    {
        private const string Xml = @"
<definitions xmlns=""https://www.omg.org/spec/DMN/20230324/MODEL/"" id=""def"" name=""def"" namespace=""http://example.com"">
  <itemDefinition name=""numberList"" isCollection=""true"">
    <typeRef>number</typeRef>
  </itemDefinition>
  <decision name=""d"" id=""d1"">
    <variable typeRef=""any"" name=""d""/>
    <context id=""c1"">
      <contextEntry id=""e1"">
        <variable name=""numberList"" typeRef=""numberList""/>
        <literalExpression typeRef=""number""><text>1</text></literalExpression>
      </contextEntry>
      <contextEntry id=""e2"">
        <variable name=""number"" typeRef=""number""/>
        <literalExpression typeRef=""numberList""><text>[2]</text></literalExpression>
      </contextEntry>
      <contextEntry id=""e3"">
        <variable name=""dateTime"" typeRef=""date and time""/>
        <literalExpression typeRef=""date""><text>date(""2000-12-01"")</text></literalExpression>
      </contextEntry>
      <contextEntry id=""e4"">
        <literalExpression><text>[numberList, number, dateTime]</text></literalExpression>
      </contextEntry>
    </context>
  </decision>
</definitions>";

        [TestMethod]
        public void ContextEntriesConvertToTheirDeclaredType()
        {
            var definition = DmnDefinitionFactory.CreateDmnDefinition(DmnParser.ParseString15(Xml));
            var context = DmnExecutionContextFactory.CreateExecutionContext(definition);

            var result = context.ExecuteDecision("d");

            var value = result.FirstResultVariables[0].Value as List<object>;
            value.Should().NotBeNull();
            value.Should().HaveCount(3);
            value[0].Should().BeEquivalentTo(new object[] { 1m });
            value[1].Should().Be(2m);
            value[2].Should().Be(new DateTimeOffset(2000, 12, 1, 0, 0, 0, TimeSpan.Zero));
        }
    }
}
