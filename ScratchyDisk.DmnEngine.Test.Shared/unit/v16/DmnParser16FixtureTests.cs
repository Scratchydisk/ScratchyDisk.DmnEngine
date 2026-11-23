using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §12 examples and the DMN 1.6 XSD
    /// (https://www.omg.org/spec/DMN/20240513/DMN16.xsd).
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - parser")]
    [TestCategory("DMN 1.6 conformance")]
    public class DmnParser16FixtureTests
    {
        private static string Fixture(string name)
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(dir ?? string.Empty, "dmn", "dmn1.6", name);
        }

        [TestMethod]
        public void Dmn16_12_1_LiteralExpressionFixtureParsesAndExecutes()
        {
            var model = DmnParser.ParseAutoDetect(Fixture("literal.dmn"));

            model.DmnVersion.Should().Be(DmnParser.DmnVersionEnum.V1_6);
            var def = DmnDefinitionFactory.CreateDmnDefinition(model);
            var result = DmnExecutionContextFactory.CreateExecutionContext(def)
                .ExecuteDecision("Greeting");

            result.HasResult.Should().BeTrue();
            result.First["Greeting"].Value.Should().Be("hello");
        }

        [TestMethod]
        public void Dmn16_12_1_DecisionTableFixtureParsesAndExecutes()
        {
            var model = DmnParser.ParseAutoDetect(Fixture("decisiontable.dmn"));
            var def = DmnDefinitionFactory.CreateDmnDefinition(model);

            model.DmnVersion.Should().Be(DmnParser.DmnVersionEnum.V1_6);
            var adult = DmnExecutionContextFactory.CreateExecutionContext(def)
                .WithInputParameter("Age", 20)
                .ExecuteDecision("Eligible");
            var child = DmnExecutionContextFactory.CreateExecutionContext(def)
                .WithInputParameter("Age", 16)
                .ExecuteDecision("Eligible");

            adult.First["Eligible"].Value.Should().Be(true);
            child.First["Eligible"].Value.Should().Be(false);
        }

        [TestMethod]
        public void Dmn16_12_1_DrdFixtureResolvesDependencies()
        {
            var model = DmnParser.ParseAutoDetect(Fixture("drd.dmn"));
            var def = DmnDefinitionFactory.CreateDmnDefinition(model);

            model.DmnVersion.Should().Be(DmnParser.DmnVersionEnum.V1_6);
            var result = DmnExecutionContextFactory.CreateExecutionContext(def)
                .WithInputParameter("Age", 20)
                .ExecuteDecision("Eligible");

            result.HasResult.Should().BeTrue();
            result.First["Eligible"].Value.Should().Be(true);
        }

        [TestMethod]
        public void Dmn16_12_1_EmbeddedFragmentsUse16Namespace()
        {
            var model = DmnParser.Parse16(Fixture("fragments.dmn"));
            var def = DmnDefinitionFactory.CreateDmnDefinition(model);

            model.DmnVersion.Should().Be(DmnParser.DmnVersionEnum.V1_6);
            var decision = def.Decisions["Eligible"];
            decision.RequiredInputs.Should().ContainSingle(i => i.Name == "Age");
            var result = DmnExecutionContextFactory.CreateExecutionContext(def)
                .WithInputParameter("Age", 20)
                .ExecuteDecision("Eligible");
            result.First["Eligible"].Value.Should().Be(true);
        }
    }
}
