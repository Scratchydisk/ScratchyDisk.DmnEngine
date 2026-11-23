using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Canonical source: DMN 1.6 §2.1 and docs/conformance-test-policy.md.
    /// </summary>
    [TestClass]
    [TestCategory("DMN 1.6 conformance")]
    public class DocumentationSupportMatrixTests
    {
        private static string ReadmePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "readme.md");
        }

        [TestMethod]
        public void Dmn16_2_1_ReadmeListsDmn16()
        {
            var text = File.ReadAllText(ReadmePath());

            text.Should().Contain("DMN 1.6");
            text.Should().Contain("descendant");
            text.Should().Contain(".value");
            text.Should().Contain("lenient");
            text.Should().Contain("strict");
        }

        [TestMethod]
        public void Dmn16_2_1_ReadmeStatesOptionalDialectStatus()
        {
            var text = File.ReadAllText(ReadmePath());

            text.Should().Contain("B-FEEL");
            text.Should().Contain("ONNX");
            text.Should().Contain("deferred");
        }

        [TestMethod]
        public void Dmn16_2_1_ReadmeLinksCanonicalSources()
        {
            var text = File.ReadAllText(ReadmePath());

            text.Should().Contain("https://www.omg.org/spec/DMN/1.6/PDF");
            text.Should().Contain("https://www.omg.org/spec/DMN/1.5/PDF");
            text.Should().Contain("conformance-test-policy.md");
        }
    }
}
