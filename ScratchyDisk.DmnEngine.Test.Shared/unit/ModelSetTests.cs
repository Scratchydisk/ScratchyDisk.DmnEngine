using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    [TestClass]
    [TestCategory("Code tests - engine")]
    public class ModelSetTests
    {
        private const string Dmn15 = "https://www.omg.org/spec/DMN/20230324/MODEL/";

        [TestMethod]
        public void LoadDirectory_ResolvesImportsByNamespace()
        {
            var directory = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(directory, "customers.dmn"), $@"
<definitions xmlns=""{Dmn15}"" id=""customers"" name=""customers"" namespace=""https://example.org/customers"">
  <inputData name=""name"" id=""i_name""/>
  <decision name=""Greeting"" id=""d_greeting"">
    <variable typeRef=""string"" name=""Greeting""/>
    <literalExpression><text>""Hello "" + name</text></literalExpression>
  </decision>
</definitions>");

                File.WriteAllText(Path.Combine(directory, "orders.dmn"), $@"
<definitions xmlns=""{Dmn15}"" id=""orders"" name=""orders"" namespace=""https://example.org/orders"">
  <import namespace=""https://example.org/customers"" name=""cust"" importType=""{Dmn15}""/>
  <decision name=""OrderGreeting"" id=""d_order"">
    <variable typeRef=""string"" name=""OrderGreeting""/>
    <informationRequirement id=""r1""><requiredDecision href=""https://example.org/customers#d_greeting""/></informationRequirement>
    <literalExpression><text>cust.Greeting</text></literalExpression>
  </decision>
</definitions>");

                var set = DmnModelSet.LoadDirectory(directory);
                set.Namespaces.Should().Contain("https://example.org/customers");
                set.Namespaces.Should().Contain("https://example.org/orders");

                var definition = set.CreateDefinition("https://example.org/orders");
                var context = DmnExecutionContextFactory.CreateExecutionContext(definition)
                    .WithInputParameter("https://example.org/customers#name", "Ada");

                var result = context.ExecuteDecision("OrderGreeting");

                result.FirstResultVariables[0].Value.Should().Be("Hello Ada");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void LoadDirectory_RejectsDuplicateNamespaces()
        {
            var directory = CreateTempDirectory();
            try
            {
                foreach (var name in new[] { "a.dmn", "b.dmn" })
                {
                    File.WriteAllText(Path.Combine(directory, name), $@"
<definitions xmlns=""{Dmn15}"" id=""{name}"" name=""{name}"" namespace=""https://example.org/duplicate"">
  <decision name=""d"" id=""d1""><variable name=""d""/><literalExpression><text>1</text></literalExpression></decision>
</definitions>");
                }

                Action act = () => DmnModelSet.LoadDirectory(directory);

                act.Should().Throw<DmnModelSetException>().WithMessage("*Duplicate model namespace*");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void LoadDirectory_ReportsParseErrorsWhenIgnored()
        {
            var directory = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(directory, "broken.dmn"), "<definitions><oops></definitions>");
                File.WriteAllText(Path.Combine(directory, "ok.dmn"), $@"
<definitions xmlns=""{Dmn15}"" id=""ok"" name=""ok"" namespace=""https://example.org/ok"">
  <decision name=""d"" id=""d1""><variable name=""d""/><literalExpression><text>1</text></literalExpression></decision>
</definitions>");

                var set = DmnModelSet.LoadDirectory(directory, ignoreParseErrors: true);

                set.Errors.Should().HaveCount(1);
                set.Namespaces.Should().Contain("https://example.org/ok");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void GetByName_UsesDefinitionsName()
        {
            var directory = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(directory, "named.dmn"), $@"
<definitions xmlns=""{Dmn15}"" id=""named"" name=""Vacation Model"" namespace=""https://example.org/vacation"">
  <decision name=""d"" id=""d1""><variable name=""d""/><literalExpression><text>1</text></literalExpression></decision>
</definitions>");

                var set = DmnModelSet.LoadDirectory(directory);
                var model = set.GetByName("Vacation Model");

                model.Namespace.Should().Be("https://example.org/vacation");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "dmn-modelset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
