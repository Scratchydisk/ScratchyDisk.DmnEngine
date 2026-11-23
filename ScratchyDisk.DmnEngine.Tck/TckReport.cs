using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ScratchyDisk.DmnEngine.Tck;

public sealed class TckSummary
{
    public int Total { get; init; }
    public int Pass { get; init; }
    public int Fail { get; init; }
    public int ParseFail { get; init; }
    public int NotFound { get; init; }
    public int Unsupported { get; init; }
    public int FixtureError { get; init; }

    /// <summary>Cases the engine cannot execute at all: missing constructs, unreadable models or unresolved names.</summary>
    public int Blocked => Unsupported + FixtureError + ParseFail + NotFound;

    /// <summary>Cases that reached evaluation.</summary>
    public int Executed => Total - Blocked;

    public double PassRate => Total == 0 ? 0 : (double)Pass / Total;
    public double ExecutedPassRate => Executed == 0 ? 0 : (double)Pass / Executed;

    public static TckSummary Build(IEnumerable<TckCaseResult> results)
    {
        var list = results.ToList();
        return new TckSummary
        {
            Total = list.Count,
            Pass = list.Count(r => r.Status == TckStatus.Pass),
            Fail = list.Count(r => r.Status == TckStatus.Fail),
            ParseFail = list.Count(r => r.Status == TckStatus.ParseFail),
            NotFound = list.Count(r => r.Status == TckStatus.NotFound),
            Unsupported = list.Count(r => r.Status == TckStatus.Unsupported),
            FixtureError = list.Count(r => r.Status == TckStatus.FixtureError)
        };
    }

    public string ToLine() =>
        $"total {Total,5}  pass {Pass,5} ({PassRate,6:P1})  fail {Fail,5}  parse-fail {ParseFail,5}  " +
        $"not-found {NotFound,4}  unsupported {Unsupported,5}  fixture {FixtureError,3}  " +
        $"executed pass {ExecutedPassRate,6:P1}";
}

public static class TckReport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static void WriteOfficialCsv(string path, IReadOnlyList<TckCaseResult> results)
    {
        var builder = new StringBuilder();
        foreach (var result in results)
        {
            builder.Append(Csv(result.File.SuiteId)).Append(',')
                .Append(Csv(Path.GetFileNameWithoutExtension(result.File.FileName))).Append(',')
                .Append(Csv(result.Case.Id)).Append(',')
                .Append(Csv(result.Status.ToOfficial())).Append(',')
                .Append(Csv(result.Status.IsPass() ? "" : result.Reason)).AppendLine();
        }
        WriteFile(path, builder.ToString());
    }

    public static void WriteDetailsCsv(string path, IReadOnlyList<TckCaseResult> results)
    {
        var builder = new StringBuilder();
        builder.AppendLine("level,suite,file,case,resultNode,resultName,resultType,status,reason,expected,actual,note");
        foreach (var result in results)
        {
            foreach (var node in result.Nodes)
            {
                builder.Append(Csv(result.File.Level)).Append(',')
                    .Append(Csv(result.File.Suite)).Append(',')
                    .Append(Csv(result.File.FileName)).Append(',')
                    .Append(Csv(result.Case.Id)).Append(',')
                    .Append(node.Index.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(Csv(node.ResultName)).Append(',')
                    .Append(Csv(node.ResultType)).Append(',')
                    .Append(Csv(node.Status.ToDisplay())).Append(',')
                    .Append(Csv(node.Reason)).Append(',')
                    .Append(Csv(node.Expected)).Append(',')
                    .Append(Csv(node.Actual)).Append(',')
                    .Append(Csv(node.Note)).AppendLine();
            }
        }
        WriteFile(path, builder.ToString());
    }

    public static void WriteMarkdown(string path, string title, string engineVersion, string tckCommit,
        IReadOnlyList<TckCaseResult> results)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        builder.AppendLine($"Engine {engineVersion} · TCK master {tckCommit} · {DateTime.UtcNow:yyyy-MM-dd}");
        builder.AppendLine();

        foreach (var level in results.Select(r => r.File.Level).Distinct())
        {
            var summary = TckSummary.Build(results.Where(r => r.File.Level == level));
            builder.AppendLine($"## {level}");
            builder.AppendLine();
            builder.AppendLine("| Cases | PASS | FAIL | PARSE-FAIL | NOT-FOUND | UNSUPPORTED | Executed pass rate |");
            builder.AppendLine("| ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
            builder.AppendLine($"| {summary.Total} | {summary.Pass} | {summary.Fail} | {summary.ParseFail} | " +
                               $"{summary.NotFound} | {summary.Unsupported + summary.FixtureError} | " +
                               $"{summary.ExecutedPassRate:P1} |");
            builder.AppendLine();
        }

        var failures = results
            .Where(r => r.Status is TckStatus.Fail or TckStatus.ParseFail or TckStatus.NotFound)
            .ToList();

        if (failures.Count > 0)
        {
            builder.AppendLine("## Top failing suites");
            builder.AppendLine();
            builder.AppendLine("| Suite | Cases |");
            builder.AppendLine("| --- | ---: |");
            foreach (var group in failures.GroupBy(r => r.File.Suite).OrderByDescending(g => g.Count()).Take(25))
                builder.AppendLine($"| {group.Key} | {group.Count()} |");
            builder.AppendLine();
        }

        var blocked = results.Where(r => r.Status == TckStatus.Unsupported).ToList();
        if (blocked.Count > 0)
        {
            builder.AppendLine("## Unsupported constructs");
            builder.AppendLine();
            builder.AppendLine("| Reason | Cases |");
            builder.AppendLine("| --- | ---: |");
            foreach (var group in blocked.GroupBy(r => BlockReason(r.Reason)).OrderByDescending(g => g.Count()))
                builder.AppendLine($"| {group.Key} | {group.Count()} |");
            builder.AppendLine();
        }

        WriteFile(path, builder.ToString());
    }

    public static void WriteBaseline(string path, IReadOnlyList<TckCaseResult> results,
        string tckRoot, string tckCommit, string engineVersion)
    {
        var document = new BaselineDocument
        {
            SchemaVersion = 1,
            GeneratedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Tck = new BaselineTck { Root = tckRoot, Commit = tckCommit },
            Engine = new BaselineEngine { Version = engineVersion },
            Totals = TckSummary.Build(results),
            Cases = results
                .Where(r => !r.Status.IsPass())
                .ToDictionary(
                    r => r.Key,
                    r => new BaselineCase { Status = r.Status.ToDisplay(), Reason = r.Reason })
        };

        WriteFile(path, JsonSerializer.Serialize(document, JsonOptions));
    }

    public static BaselineDocument ReadBaseline(string path)
    {
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<BaselineDocument>(File.ReadAllText(path), JsonOptions);
    }

    public static void WriteFile(string path, string content)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, content);
    }

    private static string BlockReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return "(unspecified)";
        var index = reason.IndexOf(" (available:", StringComparison.Ordinal);
        return index > 0 ? reason[..index] : reason;
    }

    private static string Csv(string value)
    {
        value ??= "";
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}

public sealed class BaselineDocument
{
    public int SchemaVersion { get; set; }
    public string GeneratedUtc { get; set; }
    public BaselineTck Tck { get; set; }
    public BaselineEngine Engine { get; set; }
    public TckSummary Totals { get; set; }
    public Dictionary<string, BaselineCase> Cases { get; set; } = new();
}

public sealed class BaselineTck
{
    public string Root { get; set; }
    public string Commit { get; set; }
}

public sealed class BaselineEngine
{
    public string Version { get; set; }
}

public sealed class BaselineCase
{
    public string Status { get; set; }
    public string Reason { get; set; }
}
