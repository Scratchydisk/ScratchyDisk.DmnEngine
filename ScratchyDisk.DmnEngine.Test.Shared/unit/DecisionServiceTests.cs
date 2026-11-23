using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    [TestClass]
    [TestCategory("Code tests - engine")]
    public class DecisionServiceTests
    {
        private const string Dmn15 = "https://www.omg.org/spec/DMN/20230324/MODEL/";

        /// <summary>
        /// A decision service with two input decisions and one input data. Input data
        /// parameters come first, then the input decisions.
        /// </summary>
        private const string ThreeParameterModel = @"
<definitions xmlns=""" + Dmn15 + @""" id=""def"" name=""def"" namespace=""http://example.com"">
  <decisionService name=""svc"" id=""svc1"">
    <variable name=""svc""/>
    <outputDecision href=""#out""/>
    <inputDecision href=""#in1""/>
    <inputDecision href=""#in2""/>
    <inputData href=""#data""/>
  </decisionService>
  <decision name=""out"" id=""out"">
    <variable typeRef=""string"" name=""out""/>
    <informationRequirement><requiredDecision href=""#in1""/></informationRequirement>
    <informationRequirement><requiredDecision href=""#in2""/></informationRequirement>
    <informationRequirement><requiredInput href=""#data""/></informationRequirement>
    <literalExpression><text>data + "" "" + in1 + "" "" + in2</text></literalExpression>
  </decision>
  <decision name=""in1"" id=""in1"">
    <variable typeRef=""string"" name=""in1""/>
    <literalExpression><text>""never""</text></literalExpression>
  </decision>
  <decision name=""in2"" id=""in2"">
    <variable typeRef=""string"" name=""in2""/>
    <literalExpression><text>""never""</text></literalExpression>
  </decision>
  <inputData name=""data"" id=""data""><variable typeRef=""string"" name=""data""/></inputData>
  <decision name=""probe"" id=""probe"">
    <variable name=""probe""/>
    <knowledgeRequirement><requiredKnowledge href=""#svc1""/></knowledgeRequirement>
    <literalExpression><text>svc(""A"", ""B"", ""C"")</text></literalExpression>
  </decision>
</definitions>";

        private static DmnDefinition Definition(string xml) =>
            DmnDefinitionFactory.CreateDmnDefinition(DmnParser.ParseString15(xml));

        private static object Execute(DmnDefinition definition, string decisionName,
            Action<DmnExecutionContext> configure = null)
        {
            var context = DmnExecutionContextFactory.CreateExecutionContext(definition);
            configure?.Invoke(context);
            var result = context.ExecuteDecision(decisionName);
            return result.HasResult ? result.First.Variables[0].Value : null;
        }

        [TestMethod]
        public void PositionalParametersFollowDataThenDecisionsOrder()
        {
            var definition = Definition(ThreeParameterModel);

            var result = Execute(definition, "probe");

            result.Should().Be("A B C");
        }

        [TestMethod]
        public void ParametersBindByDecisionName()
        {
            var definition = Definition(ThreeParameterModel);

            var outputs = DmnExecutionContextFactory.CreateExecutionContext(definition)
                .InvokeDecisionService("svc", new Dictionary<string, object>
                {
                    ["data"] = "A",
                    ["in1"] = "B",
                    ["in2"] = "C"
                });

            outputs["out"].Should().Be("A B C",
                "a provided input decision overrides its own decision logic");
        }

        [TestMethod]
        public void MissingParameterIsAnError()
        {
            var definition = Definition(ThreeParameterModel);
            var context = DmnExecutionContextFactory.CreateExecutionContext(definition);

            Action act = () => context.InvokeDecisionService("svc",
                new Dictionary<string, object> { ["data"] = "A" });

            act.Should().Throw<DmnExecutorException>().WithMessage("*requires a value for parameter*");
        }

        [TestMethod]
        public void UnknownParameterIsAnError()
        {
            var definition = Definition(ThreeParameterModel);
            var context = DmnExecutionContextFactory.CreateExecutionContext(definition);

            Action act = () => context.InvokeDecisionService("svc",
                new Dictionary<string, object> { ["nope"] = "A" });

            act.Should().Throw<DmnExecutorException>().WithMessage("*has no parameter named 'nope'*");
        }

        [TestMethod]
        public void ProvidedDecisionWithWrongTypeIsAnError()
        {
            var definition = Definition(ThreeParameterModel);
            var context = DmnExecutionContextFactory.CreateExecutionContext(definition);

            Action act = () => context.InvokeDecisionService("svc", new Dictionary<string, object>
            {
                ["data"] = "A",
                ["in1"] = 1234m,
                ["in2"] = "C"
            });

            act.Should().Throw<DmnEvaluationException>().WithMessage("*expects string*");
        }

        [TestMethod]
        public void InputDataWithWrongTypeBindsNull()
        {
            var definition = Definition(ThreeParameterModel);
            var context = DmnExecutionContextFactory.CreateExecutionContext(definition);

            var outputs = context.InvokeDecisionService("svc", new Dictionary<string, object>
            {
                ["data"] = 10m,
                ["in1"] = "B",
                ["in2"] = "C"
            });

            outputs["out"].Should().BeNull("FEEL coercion of an incompatible value yields null");
        }

        [TestMethod]
        public void ParametersDoNotLeakIntoTheSurroundingContext()
        {
            var xml = @"
<definitions xmlns=""" + Dmn15 + @""" id=""def"" name=""def"" namespace=""http://example.com"">
  <decisionService name=""svc"" id=""svc1"">
    <variable name=""svc""/>
    <outputDecision href=""#out""/>
    <inputData href=""#data""/>
  </decisionService>
  <decision name=""out"" id=""out"">
    <variable typeRef=""string"" name=""out""/>
    <informationRequirement><requiredInput href=""#data""/></informationRequirement>
    <literalExpression><text>data</text></literalExpression>
  </decision>
  <decision name=""caller"" id=""caller"">
    <variable typeRef=""string"" name=""caller""/>
    <informationRequirement><requiredInput href=""#data""/></informationRequirement>
    <knowledgeRequirement><requiredKnowledge href=""#svc1""/></knowledgeRequirement>
    <literalExpression><text>svc(""from service"") + "" / "" + data</text></literalExpression>
  </decision>
  <inputData name=""data"" id=""data""><variable typeRef=""string"" name=""data""/></inputData>
</definitions>";

            var result = Execute(Definition(xml), "caller",
                context => context.WithInputParameter("data", "global"));

            result.Should().Be("from service / global");
        }

        [TestMethod]
        public void DecisionWithoutLogicIsNullAndCanBeProvided()
        {
            var xml = @"
<definitions xmlns=""" + Dmn15 + @""" id=""def"" name=""def"" namespace=""http://example.com"">
  <decisionService name=""svc"" id=""svc1"">
    <variable name=""svc""/>
    <outputDecision href=""#out""/>
    <inputDecision href=""#provided""/>
  </decisionService>
  <decision name=""out"" id=""out"">
    <variable typeRef=""string"" name=""out""/>
    <informationRequirement><requiredDecision href=""#provided""/></informationRequirement>
    <literalExpression><text>""evaluation: "" + provided</text></literalExpression>
  </decision>
  <decision name=""provided"" id=""provided"">
    <variable typeRef=""string"" name=""provided""/>
  </decision>
</definitions>";

            var definition = Definition(xml);
            definition.Decisions.Should().ContainKey("provided");

            Execute(definition, "provided").Should().BeNull();

            var outputs = DmnExecutionContextFactory.CreateExecutionContext(definition)
                .InvokeDecisionService("svc",
                    new Dictionary<string, object> { ["provided"] = "provided" });

            outputs["out"].Should().Be("evaluation: provided");
        }

        [TestMethod]
        public void NamedParametersSelectByIdentifier()
        {
            var definition = Definition(ThreeParameterModel);
            var context = DmnExecutionContextFactory.CreateExecutionContext(definition);

            var outputs = context.InvokeDecisionService("svc", new Dictionary<string, object>
            {
                ["in2"] = "C",
                ["data"] = "A",
                ["in1"] = "B"
            });

            outputs["out"].Should().Be("A B C");
        }
    }
}
