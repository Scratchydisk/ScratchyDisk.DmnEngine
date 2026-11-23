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
    public class ConformancePolicyTests
    {
        private static string PolicyPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", "conformance-test-policy.md");
        }

        [TestMethod]
        public void Dmn16_2_1_ConformancePolicyDocumentExists()
        {
            File.Exists(PolicyPath()).Should().BeTrue("the conformance test policy must be part of the repository");
        }

        [TestMethod]
        public void Dmn16_2_1_ConformancePolicyNamesCanonicalSources()
        {
            var text = File.ReadAllText(PolicyPath());

            text.Should().Contain("formal/25-12-02");
            text.Should().Contain("https://www.omg.org/spec/DMN/1.5/PDF");
            text.Should().Contain("https://www.omg.org/spec/DMN/20240513/DMN16.xsd");
            text.Should().Contain("https://www.omg.org/spec/DMN/20240513/B-FEEL/");
        }

        [TestMethod]
        public void Dmn16_2_1_ConformancePolicyRequiresCitationsAndRegression()
        {
            var text = File.ReadAllText(PolicyPath());

            text.Should().Contain("test-first");
            text.Should().Contain("canonical source");
            text.Should().Contain("citation");
            text.Should().Contain("regression");
        }
    }
}
