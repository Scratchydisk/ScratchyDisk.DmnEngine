using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: docs/conformance-test-policy.md and DMN 1.6 §2.1.
    /// </summary>
    [TestClass]
    [TestCategory("DMN 1.6 conformance")]
    public class ReadmeTestingApproachTests
    {
        private static string ReadmePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "readme.md");
        }

        [TestMethod]
        public void Dmn16_2_1_ReadmeHasTestingAndConformanceSection()
        {
            var text = File.ReadAllText(ReadmePath());

            text.Should().Contain("Testing and conformance");
            text.Should().Contain("test-first");
            text.Should().Contain("regression");
        }

        [TestMethod]
        public void Dmn16_2_1_ReadmeExplainsCitationRule()
        {
            var text = File.ReadAllText(ReadmePath());

            text.Should().Contain("canonical source");
            text.Should().Contain("Dmn16_");
            text.Should().Contain("conformance-test-policy.md");
        }

        [TestMethod]
        public void Dmn16_2_1_ReadmeTestingSectionListsSources()
        {
            var text = File.ReadAllText(ReadmePath());

            text.Should().Contain("formal/25-12-02");
            text.Should().Contain("https://www.omg.org/spec/DMN/1.5/PDF");
            text.Should().Contain("https://www.omg.org/spec/DMN/20240513/DMN16.xsd");
            text.Should().Contain("https://www.omg.org/spec/DMN/20240513/B-FEEL/");
        }
    }
}
