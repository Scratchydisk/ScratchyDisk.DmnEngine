using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §2.1 and the repository canonical-source test policy.
    /// </summary>
    [TestClass]
    [TestCategory("DMN 1.6 conformance")]
    public class CiConformanceWorkflowTests
    {
        private static string WorkflowPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".github", "workflows", "conformance.yml");
        }

        [TestMethod]
        public void Dmn16_2_1_ConformanceWorkflowExists()
        {
            File.Exists(WorkflowPath()).Should().BeTrue("CI must run the conformance suite");
        }

        [TestMethod]
        public void Dmn16_2_1_ConformanceWorkflowRunsBothStandards()
        {
            var text = File.ReadAllText(WorkflowPath());

            text.Should().Contain("dotnet test");
            text.Should().Contain("DMN 1.6 conformance");
            text.Should().Contain("DMN 1.5 conformance");
        }

        [TestMethod]
        public void Dmn16_2_1_ConformanceWorkflowReportsCounts()
        {
            var text = File.ReadAllText(WorkflowPath());

            text.Should().Contain("--logger");
            text.Should().Contain("console");
        }
    }
}
