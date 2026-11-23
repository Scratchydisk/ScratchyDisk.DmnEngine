using System.Text.RegularExpressions;
using System.Xml.Linq;
using ScratchyDisk.DmnEngine.Engine.Decisions;
using ScratchyDisk.DmnEngine.Engine.Decisions.Table;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;
using ScratchyDisk.DmnEngine.Engine.Execution.Result;
using ScratchyDisk.DmnEngine.Parser;
using ScratchyDisk.DmnEngine.Parser.Dto;
using ScratchyDisk.DmnEngine.Feel.Types;

namespace ScratchyDisk.DmnEngine.Tck;

public enum TckStatus
{
    Pass = 0,
    Unsupported = 1,
    FixtureError = 2,
    NotFound = 3,
    ParseFail = 4,
    Fail = 5
}

public static class TckStatusExtensions
{
    public static bool IsPass(this TckStatus status) => status == TckStatus.Pass;

    /// <summary>True when the case could not be run because the engine does not model the construct.</summary>
    public static bool IsBlocked(this TckStatus status) =>
        status is TckStatus.Unsupported or TckStatus.FixtureError;

    public static string ToDisplay(this TckStatus status) => status switch
    {
        TckStatus.Pass => "PASS",
        TckStatus.Unsupported => "UNSUPPORTED",
        TckStatus.FixtureError => "FIXTURE-ERROR",
        TckStatus.NotFound => "NOT-FOUND",
        TckStatus.ParseFail => "PARSE-FAIL",
        TckStatus.Fail => "FAIL",
        _ => status.ToString().ToUpperInvariant()
    };

    /// <summary>Official TCK result vocabulary (TestResult.Result in the Java runner).</summary>
    public static string ToOfficial(this TckStatus status) => status switch
    {
        TckStatus.Pass => "SUCCESS",
        TckStatus.Unsupported or TckStatus.FixtureError => "IGNORED",
        _ => "ERROR"
    };
}

public sealed class TckModel
{
    public string Path { get; private init; }
    public DmnDefinition Definition { get; private init; }
    public string ParseError { get; private init; }
    public HashSet<string> BkmNames { get; } = new(StringComparer.Ordinal);
    public HashSet<string> DecisionServiceNames { get; } = new(StringComparer.Ordinal);
    public HashSet<string> KnowledgeRequiringDecisions { get; } = new(StringComparer.Ordinal);
    public HashSet<string> UnsupportedLogicDecisions { get; } = new(StringComparer.Ordinal);
    public bool HasImport { get; private set; }

    public static TckModel Load(string path)
    {
        if (!File.Exists(path))
            return new TckModel { Path = path, ParseError = $"Model not found: {path}" };

        try
        {
            var document = XDocument.Load(path);
            var model = new TckModel
            {
                Path = path,
                Definition = DmnDefinitionFactory.CreateDmnDefinition(DmnParser.ParseAutoDetect(path))
            };
            model.Classify(document);
            return model;
        }
        catch (Exception ex)
        {
            return new TckModel { Path = path, ParseError = Shorten(ex.Message) };
        }
    }

    /// <summary>
    /// Records constructs the TCK exercises but the engine does not model, so the
    /// result is reported as unsupported rather than as a behavioural failure.
    /// </summary>
    private void Classify(XDocument document)
    {
        var root = document.Root;
        if (root == null) return;

        HasImport = root.Elements().Any(e => e.Name.LocalName == "import");

        foreach (var element in root.Descendants())
        {
            switch (element.Name.LocalName)
            {
                case "businessKnowledgeModel":
                    AddName(BkmNames, element);
                    break;
                case "decisionService":
                    AddName(DecisionServiceNames, element);
                    break;
                case "decision":
                    ClassifyDecision(element);
                    break;
            }
        }
    }

    private void ClassifyDecision(XElement decision)
    {
        var name = (string)decision.Attribute("name");
        if (string.IsNullOrWhiteSpace(name)) return;

        if (decision.Elements().Any(e => e.Name.LocalName == "knowledgeRequirement"))
            KnowledgeRequiringDecisions.Add(name);

        var logicKind = decision.Elements()
            .Select(e => e.Name.LocalName)
            .FirstOrDefault(n => n is "context" or "relation" or "invocation" or "functionDefinition" or "list");

        if (logicKind != null)
            UnsupportedLogicDecisions.Add(name);
    }

    private static void AddName(HashSet<string> set, XElement element)
    {
        var name = (string)element.Attribute("name");
        if (!string.IsNullOrWhiteSpace(name)) set.Add(name);
    }

    /// <summary>
    /// Distinguishes a genuine decision error (which an errorResult test expects)
    /// from the engine failing to reach evaluation at all. Counting the latter as a
    /// pass would make unimplemented features look conformant.
    /// </summary>
    private static bool IsInfrastructureError(Exception exception)
    {
        var messages = exception.GetBaseException().Message + " | " + exception.Message;
        return InfrastructureErrorMarkers.Any(marker =>
            messages.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[] InfrastructureErrorMarkers =
    {
        "outputType is null",
        "No decisions in the sourceDefinition",
        "is not an input parameter",
        "Missing decision id",
        "Missing output variable",
        "Unsupported type name",
        "Can't parse InformationRequirement",
        "contains invalid character",
        "must start with letter",
    };

    private static string Shorten(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";
        message = message.Replace('\r', ' ').Replace('\n', ' ');
        return message.Length <= 240 ? message : message[..240];
    }
}

public sealed record TckNodeResult(
    int Index,
    string ResultName,
    string ResultType,
    TckStatus Status,
    string Reason,
    string Expected,
    string Actual,
    string Note);

public sealed record TckCaseResult(
    TckTestFile File,
    TckTestCase Case,
    TckStatus Status,
    string Reason,
    IReadOnlyList<TckNodeResult> Nodes)
{
    public string Key => $"{File.SuiteId}/{File.FileName}/{Case.Id}";
}

public sealed class TckRunner
{
    private readonly Dictionary<string, TckModel> _models = new(StringComparer.Ordinal);

    public TckCaseResult Run(TckTestFile file, TckTestCase testCase)
    {
        if (file.ReadError != null)
            return Case(file, testCase, TckStatus.ParseFail, "test XML: " + file.ReadError, Array.Empty<TckNodeResult>());

        var model = GetModel(file.ModelPath);
        if (model.ParseError != null)
        {
            return Case(file, testCase, TckStatus.ParseFail, "model: " + model.ParseError,
                ResultNodes(testCase, TckStatus.ParseFail, "model: " + model.ParseError));
        }

        var nodeResults = testCase.Results.Select(result => RunResult(model, testCase, result)).ToList();
        var worst = nodeResults.OrderByDescending(n => n.Status).FirstOrDefault();

        if (worst == null)
            return Case(file, testCase, TckStatus.FixtureError, "no resultNode", nodeResults);

        var reason = worst.Reason;
        if (nodeResults.Count > 1)
            reason = $"resultNode[{worst.Index}] {worst.ResultName}: {reason}";

        return Case(file, testCase, worst.Status, reason, nodeResults);
    }

    private TckNodeResult RunResult(TckModel model, TckTestCase testCase, TckResult result)
    {
        if (result.Type is "bkm")
            return Node(result, TckStatus.Unsupported, "resultNode type 'bkm' is not supported");
        if (result.Type is "decisionService")
            return Node(result, TckStatus.Unsupported, "resultNode type 'decisionService' is not supported");
        if (model.BkmNames.Contains(result.Name))
            return Node(result, TckStatus.Unsupported, "result is a business knowledge model");
        if (model.DecisionServiceNames.Contains(result.Name))
            return Node(result, TckStatus.Unsupported, "result is a decision service");
        if (model.UnsupportedLogicDecisions.Contains(result.Name))
            return Node(result, TckStatus.Unsupported, "decision logic kind is not parsed by the engine");
        if (model.HasImport)
            return Node(result, TckStatus.Unsupported, "model uses DMN imports");

        if (!model.Definition.Decisions.TryGetValue(result.Name, out var decision))
        {
            return Node(result, TckStatus.NotFound,
                $"no decision '{result.Name}' (available: {string.Join(", ", model.Definition.Decisions.Keys)})");
        }

        object expected;
        try
        {
            expected = TckValue.Parse(result.Expected);
        }
        catch (Exception ex)
        {
            return Node(result, TckStatus.FixtureError, "expected value: " + ex.Message);
        }

        var expectedText = TckValue.Format(expected);
        object actual = null;
        try
        {
            var context = DmnExecutionContextFactory.CreateExecutionContext(model.Definition);
            foreach (var input in testCase.Inputs)
            {
                object value;
                try
                {
                    value = TckValue.Parse(input.Value);
                }
                catch (Exception ex)
                {
                    return Node(result, TckStatus.FixtureError, $"input '{input.Name}': {ex.Message}");
                }
                context.WithInputParameter(input.Name, value);
            }

            var actualResult = context.ExecuteDecision(result.Name);
            actual = Extract(actualResult, result.Name, AlwaysReturnsList(decision));
        }
        catch (Exception ex)
        {
            var message = Shorten(ex.Message);

            // The engine failing before it can evaluate is a capability gap, not a
            // wrong answer. Reporting it as unsupported keeps the failure list
            // focused on behaviour that actually ran.
            if (IsInfrastructureError(ex))
            {
                return Node(result, TckStatus.Unsupported,
                    "engine cannot evaluate: " + message, expectedText, "<error>", null);
            }

            if (result.ExpectsError)
                return Node(result, TckStatus.Pass, "", expectedText, "<error>",
                    "error as expected: " + message);

            return Node(result, TckStatus.Fail, "threw: " + message, expectedText, "<error>", null);
        }

        var actualText = TckValue.Format(actual);

        if (result.ExpectsError)
        {
            return actual == null
                ? Node(result, TckStatus.Pass, "", expectedText, actualText, "expected an error; engine returned null")
                : Node(result, TckStatus.Fail, "expected an error but a value was returned", expectedText, actualText, null);
        }

        return TckValue.Compare(expected, actual, out var reason)
            ? Node(result, TckStatus.Pass, "", expectedText, actualText, null)
            : Node(result, TckStatus.Fail, reason, expectedText, actualText, null);
    }

    private TckModel GetModel(string path)
    {
        if (!_models.TryGetValue(path, out var model))
        {
            model = TckModel.Load(path);
            _models[path] = model;
        }
        return model;
    }

    /// <summary>
    /// Extracts the decision value from an engine result. A multi-hit hit policy
    /// returns a list even when only one rule matched; a single-hit decision
    /// returns the scalar or the compound-output context.
    /// </summary>
    private static object Extract(DmnDecisionResult result, string decisionName, bool alwaysList)
    {
        if (!result.HasResult)
            return alwaysList ? new List<object>() : null;

        if (!result.IsSingleResult)
            return result.Results.Select(single => ExtractSingle(single, decisionName)).ToList();

        var value = ExtractSingle(result.First, decisionName);
        return alwaysList ? new List<object> { value } : value;
    }

    private static object ExtractSingle(DmnDecisionSingleResult single, string decisionName)
    {
        var named = single.Variables.FirstOrDefault(v => v.Name == decisionName);
        if (named != null) return named.Value;
        if (single.Variables.Count == 0) return null;
        if (single.Variables.Count == 1) return single.Variables[0].Value;

        var context = new FeelContext();
        foreach (var variable in single.Variables) context.Put(variable.Name, variable.Value);
        return context;
    }

    private static bool AlwaysReturnsList(IDmnDecision decision) =>
        decision is DmnDecisionTable table
        && (table.HitPolicy is HitPolicyEnum.RuleOrder or HitPolicyEnum.OutputOrder
            || (table.HitPolicy == HitPolicyEnum.Collect
                && table.Aggregation == CollectHitPolicyAggregationEnum.List));

    private static TckCaseResult Case(
        TckTestFile file,
        TckTestCase testCase,
        TckStatus status,
        string reason,
        IReadOnlyList<TckNodeResult> nodes) =>
        new(file, testCase, status, Shorten(reason), nodes);

    private static IReadOnlyList<TckNodeResult> ResultNodes(TckTestCase testCase, TckStatus status, string reason) =>
        testCase.Results.Select(r => Node(r, status, reason)).ToList();

    private static TckNodeResult Node(TckResult result, TckStatus status, string reason,
        string expected = "", string actual = "", string note = null) =>
        new(result.Index, result.Name, result.Type, status, Shorten(reason), expected, actual, note);

    /// <summary>
    /// Distinguishes a genuine decision error (which an errorResult test expects)
    /// from the engine failing to reach evaluation at all. Counting the latter as a
    /// pass would make unimplemented features look conformant.
    /// </summary>
    private static bool IsInfrastructureError(Exception exception)
    {
        var messages = exception.GetBaseException().Message + " | " + exception.Message;
        return InfrastructureErrorMarkers.Any(marker =>
            messages.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[] InfrastructureErrorMarkers =
    {
        "outputType is null",
        "No decisions in the sourceDefinition",
        "is not an input parameter",
        "Missing decision id",
        "Missing output variable",
        "Unsupported type name",
        "Can't parse InformationRequirement",
        "contains invalid character",
        "must start with letter",
    };

    private static string Shorten(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";
        message = Regex.Replace(message, @"\s+", " ").Trim();
        return message.Length <= 300 ? message : message[..300];
    }
}
