using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScratchyDisk.DmnEngine.Test.Unit
{
    /// <summary>
    /// Guards the published NuGet package. The package is built from the current sources into a
    /// temporary folder, then its metadata and contents are checked against <c>Version.props</c>
    /// and the library csproj. Packaging previously had no coverage, which allowed a hand-built
    /// package to ship with a stale NLog dependency.
    /// </summary>
    [TestClass]
    [TestCategory("Code tests - packaging")]
    public class NuGetPackageTests
    {
        private const string PackageId = "ScratchyDisk.DmnEngine";

        private static string _repositoryRoot;
        private static string _outputDirectory;
        private static string _packagePath;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _repositoryRoot = FindRepositoryRoot();
            _outputDirectory = Path.Combine(
                Path.GetTempPath(),
                "dmnengine-nupkg-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_outputDirectory);

            var projectPath = Path.Combine(_repositoryRoot, "ScratchyDisk.DmnEngine", "ScratchyDisk.DmnEngine.csproj");
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = _repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("pack");
            startInfo.ArgumentList.Add(projectPath);
            startInfo.ArgumentList.Add("--no-build");
            startInfo.ArgumentList.Add("--configuration");
            startInfo.ArgumentList.Add(CurrentConfiguration());
            startInfo.ArgumentList.Add("--output");
            startInfo.ArgumentList.Add(_outputDirectory);

            using var process = Process.Start(startInfo);
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
            {
                process.Kill(true);
                Assert.Fail("dotnet pack did not finish within five minutes.");
            }

            var output = stdout.Result + Environment.NewLine + stderr.Result;
            Assert.AreEqual(0, process.ExitCode, "dotnet pack failed:" + Environment.NewLine + output);

            var packages = Directory.GetFiles(_outputDirectory, "*.nupkg");
            packages.Should().HaveCount(1, "the library project must produce exactly one package");
            _packagePath = packages[0];
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            try
            {
                if (Directory.Exists(_outputDirectory))
                {
                    Directory.Delete(_outputDirectory, true);
                }
            }
            catch (IOException)
            {
                // A leftover temp folder must not fail the suite.
            }
        }

        [TestMethod]
        public void PackageCarriesVersionFromVersionProps()
        {
            var declaredVersion = XDocument
                .Load(Path.Combine(_repositoryRoot, "Version.props"))
                .Descendants()
                .First(element => element.Name.LocalName == "Version")
                .Value;

            var nuspec = ReadNuspec();
            MetadataValue(nuspec, "id").Should().Be(PackageId);
            MetadataValue(nuspec, "version").Should().StartWith(
                declaredVersion,
                "the package version must come from Version.props");
        }

        [TestMethod]
        public void PackageContainsLibraryAndReadme()
        {
            using var archive = ZipFile.OpenRead(_packagePath);
            var entries = archive.Entries.Select(entry => entry.FullName).ToList();

            entries.Should().Contain("lib/net10.0/ScratchyDisk.DmnEngine.dll");
            entries.Should().Contain("nuget.md");
            MetadataValue(ReadNuspec(), "readme").Should().Be("nuget.md");
        }

        [TestMethod]
        public void PackageExposesOnlyThePublicRuntimeDependencies()
        {
            // The dependency surface of the public package is a contract: change this list
            // deliberately when the library gains or drops a runtime dependency. It must never
            // contain build-time assets such as Antlr4BuildTasks or MSBuild utilities.
            var expected = new[] { "Antlr4.Runtime.Standard", "NLog" };
            var actual = PackageDependencies(ReadNuspec());

            actual.Keys.Should().BeEquivalentTo(
                expected,
                "the package must expose exactly the library runtime dependencies");
        }

        [TestMethod]
        public void PackageDependencyVersionsMatchCsproj()
        {
            var expected = DeclaredRuntimeDependencies();
            var actual = PackageDependencies(ReadNuspec());

            actual.Should().BeEquivalentTo(
                expected,
                "the package dependencies must be pinned to the csproj versions");
        }

        private static string CurrentConfiguration()
        {
            var segments = AppContext.BaseDirectory
                .TrimEnd(Path.DirectorySeparatorChar)
                .Split(Path.DirectorySeparatorChar);
            var binIndex = Array.LastIndexOf(segments, "bin");
            Assert.IsTrue(binIndex >= 0 && binIndex + 1 < segments.Length,
                "cannot determine the build configuration from " + AppContext.BaseDirectory);
            return segments[binIndex + 1];
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Version.props")) &&
                    File.Exists(Path.Combine(directory.FullName, "ScratchyDisk.DmnEngine", "ScratchyDisk.DmnEngine.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            Assert.Fail("could not find the repository root from " + AppContext.BaseDirectory);
            return null;
        }

        private static XElement ReadNuspec()
        {
            using var archive = ZipFile.OpenRead(_packagePath);
            var entry = archive.Entries.Single(item =>
                item.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            using var stream = entry.Open();
            return XDocument.Load(stream).Root;
        }

        private static string MetadataValue(XElement nuspec, string name)
        {
            return nuspec
                .Descendants()
                .First(element => element.Name.LocalName == name)
                .Value;
        }

        private static Dictionary<string, string> PackageDependencies(XElement nuspec)
        {
            var dependencies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dependency in nuspec.Descendants().Where(element => element.Name.LocalName == "dependency"))
            {
                dependencies[(string)dependency.Attribute("id")] = (string)dependency.Attribute("version");
            }

            return dependencies;
        }

        private static Dictionary<string, string> DeclaredRuntimeDependencies()
        {
            var project = XDocument.Load(
                Path.Combine(_repositoryRoot, "ScratchyDisk.DmnEngine", "ScratchyDisk.DmnEngine.csproj"));
            var dependencies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in project.Descendants().Where(element => element.Name.LocalName == "PackageReference"))
            {
                var privateAssets = (string)reference.Attribute("PrivateAssets");
                if (string.Equals(privateAssets, "all", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                dependencies[(string)reference.Attribute("Include")] = (string)reference.Attribute("Version");
            }

            return dependencies;
        }
    }
}
