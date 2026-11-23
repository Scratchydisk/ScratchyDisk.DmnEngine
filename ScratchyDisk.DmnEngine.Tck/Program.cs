using System.Globalization;
using System.Reflection;
using ScratchyDisk.DmnEngine.Parser;
using ScratchyDisk.DmnEngine.Tck;

var options = Options.Parse(args);
if (options == null) return 2;

var tckRoot = options.TckRoot;
if (!Directory.Exists(tckRoot))
{
    Console.Error.WriteLine($"TCK test cases not found at '{tckRoot}'.");
    Console.Error.WriteLine("Fetch the suite first, e.g.:");
    Console.Error.WriteLine("  ./scripts/fetch-tck.sh");
    Console.Error.WriteLine("or point at an existing checkout with --tck <path>/TestCases.");
    return 2;
}

var engineVersion = EngineVersion();
var tckCommit = options.Commit ?? ReadCommit(tckRoot);
var levelDirectories = options.Levels
    .Select(level => (Name: level, Path: Path.Combine(tckRoot, level)))
    .Where(level => Directory.Exists(level.Path))
    .ToList();

if (levelDirectories.Count == 0)
{
    Console.Error.WriteLine($"No compliance-level-* directories found under '{tckRoot}'.");
    return 2;
}

var testFiles = levelDirectories
    .SelectMany(level => TestSuiteReader.ReadLevel(level.Path, level.Name))
    .Where(file => options.Suites.Count == 0
                   || options.Suites.Any(s => file.Suite.Contains(s, StringComparison.OrdinalIgnoreCase)))
    .ToList();

var runner = new TckRunner();
var results = new List<TckCaseResult>();

foreach (var file in testFiles)
{
    foreach (var testCase in file.Cases)
    {
        if (options.CaseFilter != null
            && !testCase.Id.Equals(options.CaseFilter, StringComparison.Ordinal)
            && !testCase.Id.Contains(options.CaseFilter, StringComparison.OrdinalIgnoreCase))
            continue;

        results.Add(runner.Run(file, testCase));
    }
}

if (results.Count == 0)
{
    Console.Error.WriteLine("No test cases matched the given filters.");
    return 2;
}

Console.WriteLine($"DMN TCK — engine {engineVersion}, TCK master {tckCommit}");
Console.WriteLine();

foreach (var level in results.Select(r => r.File.Level).Distinct())
{
    var summary = TckSummary.Build(results.Where(r => r.File.Level == level));
    Console.WriteLine($"{level,-18} {summary.ToLine()}");
}

var overall = TckSummary.Build(results);
Console.WriteLine($"{"overall",-18} {overall.ToLine()}");
Console.WriteLine();

var failures = results
    .Where(r => r.Status is TckStatus.Fail or TckStatus.ParseFail or TckStatus.NotFound)
    .ToList();

if (failures.Count > 0)
{
    Console.WriteLine("Top failing suites:");
    foreach (var group in failures.GroupBy(r => r.File.Suite).OrderByDescending(g => g.Count()).Take(15))
        Console.WriteLine($"  {group.Count(),4}  {group.Key}");
    Console.WriteLine();
}

var blocked = results.Where(r => r.Status == TckStatus.Unsupported).ToList();
if (blocked.Count > 0)
{
    Console.WriteLine("Unsupported constructs:");
    foreach (var group in blocked.GroupBy(r => StripAvailable(r.Reason)).OrderByDescending(g => g.Count()).Take(10))
        Console.WriteLine($"  {group.Count(),4}  {group.Key}");
    Console.WriteLine();
}

TckReport.WriteOfficialCsv(options.OutputPath, results);
TckReport.WriteDetailsCsv(options.DetailsPath, results);
TckReport.WriteMarkdown(options.MarkdownPath, "DMN TCK baseline", engineVersion, tckCommit, results);
Console.WriteLine($"Results: {options.OutputPath}");
Console.WriteLine($"Details: {options.DetailsPath}");
Console.WriteLine($"Summary: {options.MarkdownPath}");

if (options.WriteBaselinePath != null)
{
    TckReport.WriteBaseline(options.WriteBaselinePath, results, tckRoot, tckCommit, engineVersion);
    Console.WriteLine($"Baseline written: {options.WriteBaselinePath}");
    return 0;
}

if (options.BaselinePath != null)
{
    var baseline = TckReport.ReadBaseline(options.BaselinePath);
    if (baseline == null)
    {
        Console.Error.WriteLine($"Baseline not found: {options.BaselinePath}");
        return 2;
    }

    // A case is a new failure when it is worse than the baseline: absent (was
    // passing) or moved from a blocked status into a behavioural failure. This
    // catches "fixed the blocker, exposed a wrong answer" transitions too.
    var newFailures = results
        .Where(r => !r.Status.IsPass()
                    && (!baseline.Cases.TryGetValue(r.Key, out var known)
                        || BaselineStatus(known.Status) < r.Status))
        .ToList();
    var fixedCases = results
        .Where(r => r.Status.IsPass() && baseline.Cases.ContainsKey(r.Key))
        .ToList();
    var stillFailing = results
        .Where(r => !r.Status.IsPass() && baseline.Cases.ContainsKey(r.Key)
                    && !newFailures.Contains(r))
        .ToList();
    var removed = baseline.Cases.Keys.Except(results.Select(r => r.Key)).ToList();

    Console.WriteLine();
    Console.WriteLine($"Baseline comparison against {options.BaselinePath}");
    Console.WriteLine($"  still failing {stillFailing.Count}");
    Console.WriteLine($"  fixed since baseline {fixedCases.Count}");
    Console.WriteLine($"  new failures {newFailures.Count}");
    if (removed.Count > 0)
        Console.WriteLine($"  cases no longer present {removed.Count}");

    foreach (var failure in newFailures.Take(20))
        Console.WriteLine($"  NEW  {failure.Key}  {failure.Status.ToDisplay()}: {failure.Reason}");
    if (newFailures.Count > 20)
        Console.WriteLine($"  ... and {newFailures.Count - 20} more new failures");

    if (baseline.Tck?.Commit is { Length: > 0 } baselineCommit && baselineCommit != tckCommit)
        Console.WriteLine($"  warning: baseline was generated with TCK {baselineCommit}");

    return newFailures.Count > 0 ? 1 : 0;
}

var hasBehaviouralFailures = results.Any(r => !r.Status.IsPass() && !r.Status.IsBlocked());
return hasBehaviouralFailures ? 1 : 0;

static string EngineVersion()
{
    var assembly = typeof(DmnParser).Assembly;
    return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
           ?? assembly.GetName().Version?.ToString()
           ?? "unknown";
}

static string ReadCommit(string tckRoot)
{
    var commitFile = Path.Combine(tckRoot, "..", ".commit");
    if (File.Exists(commitFile)) return File.ReadAllText(commitFile).Trim();
    return "unknown";
}

static TckStatus BaselineStatus(string value)
{
    return value switch
    {
        "PASS" => TckStatus.Pass,
        "UNSUPPORTED" => TckStatus.Unsupported,
        "FIXTURE-ERROR" => TckStatus.FixtureError,
        "NOT-FOUND" => TckStatus.NotFound,
        "PARSE-FAIL" => TckStatus.ParseFail,
        "FAIL" => TckStatus.Fail,
        _ => TckStatus.Pass
    };
}

static string StripAvailable(string reason)
{
    if (string.IsNullOrWhiteSpace(reason)) return "(unspecified)";
    var index = reason.IndexOf(" (available:", StringComparison.Ordinal);
    return index > 0 ? reason[..index] : reason;
}

internal sealed class Options
{
    public string TckRoot { get; private set; } = Path.Combine("artifacts", "tck", "TestCases");
    public List<string> Levels { get; } = new() { "compliance-level-2", "compliance-level-3" };
    public List<string> Suites { get; } = new();
    public string CaseFilter { get; private set; }
    public string OutputPath { get; private set; } = Path.Combine("artifacts", "tck", "tck_results.csv");
    public string DetailsPath { get; private set; } = Path.Combine("artifacts", "tck", "tck_details.csv");
    public string MarkdownPath { get; private set; } = Path.Combine("artifacts", "tck", "tck_summary.md");
    public string BaselinePath { get; private set; }
    public string WriteBaselinePath { get; private set; }
    public string Commit { get; private set; }

    public static Options Parse(string[] args)
    {
        var options = new Options();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help" or "-h" or "-?":
                    PrintUsage();
                    return null;
                case "--tck" when i + 1 < args.Length:
                    options.TckRoot = args[++i];
                    break;
                case "--level" when i + 1 < args.Length:
                    options.Levels.Clear();
                    options.Levels.AddRange(ParseLevels(args[++i]));
                    break;
                case "--suite" when i + 1 < args.Length:
                    options.Suites.AddRange(args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "--case" when i + 1 < args.Length:
                    options.CaseFilter = args[++i];
                    break;
                case "--out" when i + 1 < args.Length:
                    options.OutputPath = args[++i];
                    break;
                case "--details" when i + 1 < args.Length:
                    options.DetailsPath = args[++i];
                    break;
                case "--markdown" when i + 1 < args.Length:
                    options.MarkdownPath = args[++i];
                    break;
                case "--baseline" when i + 1 < args.Length:
                    options.BaselinePath = args[++i];
                    break;
                case "--write-baseline" when i + 1 < args.Length:
                    options.WriteBaselinePath = args[++i];
                    break;
                case "--commit" when i + 1 < args.Length:
                    options.Commit = args[++i];
                    break;
                default:
                    Console.Error.WriteLine($"Unknown or incomplete argument: {arg}");
                    PrintUsage();
                    return null;
            }
        }

        return options;
    }

    private static IEnumerable<string> ParseLevels(string value)
    {
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "all":
                    yield return "compliance-level-2";
                    yield return "compliance-level-3";
                    break;
                case "2" or "cl2" or "level-2":
                    yield return "compliance-level-2";
                    break;
                case "3" or "cl3" or "level-3":
                    yield return "compliance-level-3";
                    break;
                case "non-compliant" or "nc":
                    yield return "non-compliant";
                    break;
                default:
                    yield return part;
                    break;
            }
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            dmntck — run the OMG DMN TCK against ScratchyDisk.DmnEngine

            Usage:
              dmntck [options]

            Options:
              --tck <dir>              TestCases directory (default artifacts/tck/TestCases)
              --level <list>           2, 3, all (default 2,3)
              --suite <list>           Comma-separated suite name substrings
              --case <id>              Only test cases whose id matches
              --out <csv>              Official-style result CSV
              --details <csv>          Per-result-node diagnostic CSV
              --markdown <md>          Human-readable summary
              --baseline <json>        Compare against a committed baseline; non-zero on new failures
              --write-baseline <json>  Write the current result set as a baseline
              --commit <sha>           TCK commit recorded in the baseline
              -h, --help               Show this help

            Exit codes: 0 ok, 1 failures/new failures, 2 usage or setup error.
            """);
    }
}
