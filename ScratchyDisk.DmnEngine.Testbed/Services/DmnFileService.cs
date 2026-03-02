using System.Text.Json;
using System.Xml.Linq;
using ScratchyDisk.DmnEngine.Testbed.Models;

namespace ScratchyDisk.DmnEngine.Testbed.Services;

public class DmnFileService
{
    private readonly string _directory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public DmnFileService(string directory)
    {
        _directory = Path.GetFullPath(directory);
        if (!System.IO.Directory.Exists(_directory))
            throw new DirectoryNotFoundException($"DMN directory not found: {_directory}");
    }

    public string Directory => _directory;

    public List<DmnFileInfo> ListFiles()
    {
        var files = System.IO.Directory.GetFiles(_directory, "*.dmn", SearchOption.AllDirectories);
        return files
            .OrderBy(f => f)
            .Select(f =>
            {
                var relativePath = Path.GetRelativePath(_directory, f);
                var name = relativePath.Replace('\\', '/');
                var testSuitePath = GetTestSuitePath(f);
                return new DmnFileInfo
                {
                    Name = name,
                    FileName = Path.GetFileName(f),
                    HasTestSuite = File.Exists(testSuitePath)
                };
            })
            .ToList();
    }

    public string GetFilePath(string name)
    {
        // Guard against path traversal
        var fullPath = Path.GetFullPath(Path.Combine(_directory, name));
        if (!fullPath.StartsWith(_directory, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid file path");
        return fullPath;
    }

    public string ReadXml(string name)
    {
        var path = GetFilePath(name);
        if (!File.Exists(path))
            throw new FileNotFoundException($"DMN file not found: {name}");
        return File.ReadAllText(path);
    }

    public TestSuite LoadTestSuite(string name)
    {
        var dmnPath = GetFilePath(name);
        var testPath = GetTestSuitePath(dmnPath);
        if (!File.Exists(testPath))
            return new TestSuite { Version = 1, DmnFile = Path.GetFileName(dmnPath), TestCases = [] };

        var json = File.ReadAllText(testPath);
        return JsonSerializer.Deserialize<TestSuite>(json, JsonOptions)
               ?? new TestSuite { Version = 1, DmnFile = Path.GetFileName(dmnPath), TestCases = [] };
    }

    public void SaveTestSuite(string name, TestSuite suite)
    {
        var dmnPath = GetFilePath(name);
        var testPath = GetTestSuitePath(dmnPath);
        var json = JsonSerializer.Serialize(suite, JsonOptions);
        File.WriteAllText(testPath, json);
    }

    public void SaveDmnFile(string name, string xmlContent)
    {
        var fullPath = GetFilePath(name);

        if (!fullPath.EndsWith(".dmn", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("File name must end with .dmn");

        var dir = Path.GetDirectoryName(fullPath)!;
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        // Basic XML validation
        System.Xml.Linq.XDocument.Parse(xmlContent);

        File.WriteAllText(fullPath, xmlContent);
    }

    private static string GetTestSuitePath(string dmnPath)
    {
        var dir = Path.GetDirectoryName(dmnPath)!;
        var baseName = Path.GetFileNameWithoutExtension(dmnPath);
        return Path.Combine(dir, $"{baseName}.tests.json");
    }
}
