using System.Xml.Linq;

namespace ScratchyDisk.DmnEngine.Tck;

/// <summary>
/// Reads the OMG DMN TCK test-case XML. The service document is intentionally
/// namespace-tolerant: the TCK has used one testcase namespace since 2016, but
/// the marshaller is shared with the DMN 1.4/1.5 branches, so matching by local
/// name keeps this reader stable across branches.
/// </summary>
public static class TestSuiteReader
{
    public static IReadOnlyList<TckTestFile> ReadLevel(string levelDirectory, string levelName)
    {
        if (!Directory.Exists(levelDirectory)) return Array.Empty<TckTestFile>();

        return Directory.GetDirectories(levelDirectory)
            .OrderBy(d => d, StringComparer.Ordinal)
            .SelectMany(directory => ReadSuiteDirectory(directory, levelName))
            .ToList();
    }

    private static IEnumerable<TckTestFile> ReadSuiteDirectory(string suiteDirectory, string levelName)
    {
        foreach (var testFilePath in Directory.GetFiles(suiteDirectory, "*-test-*.xml")
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            TckTestFile testFile;
            try
            {
                testFile = ReadTestFile(testFilePath, levelName);
            }
            catch (Exception ex)
            {
                testFile = new TckTestFile
                {
                    Level = levelName,
                    Suite = Path.GetFileName(suiteDirectory),
                    FileName = Path.GetFileName(testFilePath),
                    Directory = suiteDirectory,
                    ReadError = ex.Message
                };
            }
            yield return testFile;
        }
    }

    private static TckTestFile ReadTestFile(string path, string levelName)
    {
        var document = XDocument.Load(path);
        var root = document.Root ?? throw new InvalidDataException("Missing root element");

        var modelName = Child(root, "modelName")?.Value;
        var suite = Path.GetFileName(Path.GetDirectoryName(path));

        var testFile = new TckTestFile
        {
            Level = levelName,
            Suite = suite,
            FileName = Path.GetFileName(path),
            Directory = Path.GetDirectoryName(path),
            ModelName = string.IsNullOrWhiteSpace(modelName) ? suite + ".dmn" : modelName,
            Labels = Children(root, "labels")
                .SelectMany(l => Children(l, "label"))
                .Select(l => l.Value)
                .ToList()
        };

        foreach (var testCase in Children(root, "testCase"))
        {
            testFile.Cases.Add(new TckTestCase
            {
                Id = (string)testCase.Attribute("id") ?? "",
                Type = (string)testCase.Attribute("type"),
                InvocableName = (string)testCase.Attribute("invocableName"),
                Description = Child(testCase, "description")?.Value,
                Inputs = Children(testCase, "inputNode")
                    .Select(i => new TckInput((string)i.Attribute("name"), (string)i.Attribute("namespace"), i))
                    .ToList(),
                Results = Children(testCase, "resultNode")
                    .Select((r, index) => new TckResult(
                        index,
                        (string)r.Attribute("name"),
                        (string)r.Attribute("namespace"),
                        (string)r.Attribute("type") ?? "decision",
                        (string)r.Attribute("errorResult") == "true",
                        r))
                    .ToList()
            });
        }

        return testFile;
    }

    private static XElement Child(XElement element, string localName) =>
        element.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    private static IEnumerable<XElement> Children(XElement element, string localName) =>
        element.Elements().Where(e => e.Name.LocalName == localName);
}

public sealed class TckTestFile
{
    public string Level { get; init; }
    public string Suite { get; init; }
    public string FileName { get; init; }
    public string Directory { get; init; }
    public string ModelName { get; init; }
    public string ReadError { get; set; }
    public IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();
    public List<TckTestCase> Cases { get; } = new();

    /// <summary>Stable suite identifier used by the official result CSV, e.g. compliance-level-2/0001-input-data-string.</summary>
    public string SuiteId => $"{Level}/{Suite}";

    public string ModelPath => System.IO.Path.Combine(Directory ?? "", ModelName ?? "");
}

public sealed class TckTestCase
{
    public string Id { get; init; }

    /// <summary>Test case type from the TCK XML ("decision" or "decisionService").</summary>
    public string Type { get; init; }

    /// <summary>Name of the invocable for direct decision-service invocation.</summary>
    public string InvocableName { get; init; }

    public string Description { get; init; }
    public IReadOnlyList<TckInput> Inputs { get; init; } = Array.Empty<TckInput>();
    public IReadOnlyList<TckResult> Results { get; init; } = Array.Empty<TckResult>();
}

public sealed record TckInput(string Name, string Namespace, XElement Value);

public sealed record TckResult(int Index, string Name, string Namespace, string Type, bool ExpectsError, XElement Node)
{
    public XElement Expected => Node.Elements().FirstOrDefault(e => e.Name.LocalName == "expected");
}
