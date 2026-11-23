using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Decisions;
using ScratchyDisk.DmnEngine.Engine.Decisions.Expression;
using ScratchyDisk.DmnEngine.Engine.Decisions.Table;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Ast;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §11.1 language selection and §13.3 expressionLanguage.
    /// </summary>
    [TestClass]
    [TestCategory("DMN 1.6 conformance")]
    public class Dmn16ExpressionRoutingTests
    {
        private const string Feel = "FEEL";

        private static string LiteralModel(string languageAttribute)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   "<definitions xmlns=\"https://www.omg.org/spec/DMN/20240513/MODEL/\" id=\"def\" name=\"def\" namespace=\"http://example.com\">" +
                   "<decision id=\"d1\" name=\"D1\">" +
                   $"<literalExpression id=\"le1\"{languageAttribute}><text>1 + 1</text></literalExpression>" +
                   "<variable id=\"v1\" name=\"D1\" typeRef=\"number\"/></decision></definitions>";
        }

        private static string TableModel(string outputLanguageAttribute)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   "<definitions xmlns=\"https://www.omg.org/spec/DMN/20240513/MODEL/\" id=\"def\" name=\"def\" namespace=\"http://example.com\">" +
                   "<inputData id=\"i1\" name=\"Age\" /><decision id=\"d1\" name=\"D1\">" +
                   "<informationRequirement id=\"ir1\"><requiredInput href=\"#i1\" /></informationRequirement>" +
                   "<decisionTable id=\"dt1\" hitPolicy=\"UNIQUE\">" +
                   "<input id=\"in1\"><inputExpression id=\"ie1\" typeRef=\"number\"><text>Age</text></inputExpression></input>" +
                   "<output id=\"out1\" name=\"D1\" typeRef=\"boolean\" />" +
                   $"<rule id=\"r1\"><inputEntry id=\"ie\"><text>&gt;= 18</text></inputEntry><outputEntry id=\"oe\"{outputLanguageAttribute}><text>true</text></outputEntry></rule>" +
                   "</decisionTable></decision></definitions>";
        }

        [TestMethod]
        public void Dmn16_11_1_LiteralExpressionLanguageRetained()
        {
            var model = DmnParser.ParseString16(LiteralModel($" expressionLanguage=\"{Feel}\""));
            var definition = DmnDefinitionFactory.CreateDmnDefinition(model);

            definition.Decisions["D1"].Should().BeOfType<DmnExpressionDecision>()
                .Which.Dialect.Should().Be(FeelDialect.Feel);
        }

        [TestMethod]
        public void Dmn16_11_1_DecisionTableEntryLanguageRetained()
        {
            var model = DmnParser.ParseString16(TableModel($" expressionLanguage=\"{Feel}\""));
            var definition = DmnDefinitionFactory.CreateDmnDefinition(model);

            var table = definition.Decisions["D1"].Should().BeOfType<DmnDecisionTable>().Subject;
            table.Rules[0].Outputs[0].Dialect.Should().Be(FeelDialect.Feel);
        }

        [TestMethod]
        public void Dmn16_11_1_OmittedLanguageDefaultsToFeel()
        {
            var model = DmnParser.ParseString16(LiteralModel(""));
            var definition = DmnDefinitionFactory.CreateDmnDefinition(model);

            definition.Decisions["D1"].Should().BeOfType<DmnExpressionDecision>()
                .Which.Dialect.Should().Be(FeelDialect.Feel);
        }

        [TestMethod]
        public void Dmn16_11_1_ExecutionContextRoutesDialectToEngine()
        {
            var model = DmnParser.ParseString16(LiteralModel($" expressionLanguage=\"{Feel}\""));
            var definition = DmnDefinitionFactory.CreateDmnDefinition(model);
            var context = (CapturingContext)DmnExecutionContextFactory.CreateCustomExecutionContext<CapturingContext>(definition);

            context.ExecuteDecision("D1");

            context.SeenDialects.Should().Contain(FeelDialect.Feel);
        }

        [TestMethod]
        public void Dmn16_11_1_TestDialectStubReachesOverride()
        {
            var engine = new SpyFeelEngine();
            var ast = engine.ParseExpression("1 + 1");
            var result = engine.Evaluate(ast, new FeelEvaluationContext(), FeelDialect.BFeel);

            result.Should().Be(42m);
            engine.SeenDialect.Should().Be(FeelDialect.BFeel);
        }

        private sealed class CapturingContext : DmnExecutionContext
        {
            public List<FeelDialect> SeenDialects { get; } = new();

            public CapturingContext(
                DmnDefinition definition,
                IReadOnlyDictionary<string, DmnExecutionVariable> variables,
                IReadOnlyDictionary<string, IDmnDecision> decisions,
                Action<DmnExecutionContextOptions> configure)
                : base(definition, variables, decisions, configure)
            {
            }

            public override object EvalExpression(string expression, Type outputType, string executionId, FeelDialect dialect)
            {
                SeenDialects.Add(dialect);
                return base.EvalExpression(expression, outputType, executionId, dialect);
            }
        }

        private sealed class SpyFeelEngine : FeelEngine
        {
            public FeelDialect? SeenDialect { get; private set; }

            public override object Evaluate(FeelAstNode ast, FeelEvaluationContext evalContext, FeelDialect dialect)
            {
                SeenDialect = dialect;
                return 42m;
            }
        }
    }
}
