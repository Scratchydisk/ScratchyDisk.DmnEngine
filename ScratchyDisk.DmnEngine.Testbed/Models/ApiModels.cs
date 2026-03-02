using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScratchyDisk.DmnEngine.Testbed.Models;

// ── File listing ──

public class DmnFileInfo
{
    public string Name { get; set; }
    public string FileName { get; set; }
    public bool HasTestSuite { get; set; }
}

// ── Definition info ──

public class DefinitionInfo
{
    public string FileName { get; set; }
    public FileMetadata Metadata { get; set; }
    public List<DecisionInfo> Decisions { get; set; } = [];
    public List<VariableInfo> InputData { get; set; } = [];
}

public class FileMetadata
{
    public string DmnVersion { get; set; }
    public string DefinitionName { get; set; }
    public string Namespace { get; set; }
    public string Exporter { get; set; }
    public string ExporterVersion { get; set; }
    public string ExecutionPlatform { get; set; }
    public string ExecutionPlatformVersion { get; set; }
    public bool IsCamundaExport { get; set; }
}

public class DecisionInfo
{
    public string Name { get; set; }
    public string Type { get; set; } // "table" or "expression"
    public string HitPolicy { get; set; }
    public string Aggregation { get; set; }
    public int RuleCount { get; set; }
    public List<TableColumnInfo> TableInputs { get; set; } = [];
    public List<TableColumnInfo> TableOutputs { get; set; } = [];
    public List<string> RequiredDecisions { get; set; } = [];
    public List<string> RequiredInputs { get; set; } = [];
    public List<string> AllRequiredInputs { get; set; } = [];
    public string Expression { get; set; }
}

public class TableColumnInfo
{
    public string Name { get; set; }
    public string Label { get; set; }
    public string TypeName { get; set; }
    public List<string> AllowedValues { get; set; } = [];
    public string Expression { get; set; }
}

public class VariableInfo
{
    public string Name { get; set; }
    public string Label { get; set; }
    public string TypeName { get; set; }
    public bool IsInputParameter { get; set; }
}

// ── Execution ──

public class ExecuteRequest
{
    public string DecisionName { get; set; }
    public Dictionary<string, JsonElement> Inputs { get; set; } = [];
}

public class ExecutionResult
{
    public bool HasResult { get; set; }
    public List<SingleResult> Results { get; set; } = [];
    public List<ExecutionStep> Steps { get; set; } = [];
    public long ExecutionTimeMs { get; set; }
    public string Error { get; set; }
}

public class SingleResult
{
    public Dictionary<string, OutputValue> Outputs { get; set; } = [];
    public List<HitRuleInfo> HitRules { get; set; } = [];
}

public class OutputValue
{
    public object Value { get; set; }
    public string TypeName { get; set; }
}

public class HitRuleInfo
{
    public int Index { get; set; }
    public string Name { get; set; }
}

public class ExecutionStep
{
    public string DecisionName { get; set; }
    public string DecisionType { get; set; }
    public List<HitRuleInfo> HitRules { get; set; } = [];
    public Dictionary<string, object> VariableChanges { get; set; } = [];
}

// ── Test suites ──

public class TestSuite
{
    public int Version { get; set; } = 1;
    public string DmnFile { get; set; }
    public List<TestCase> TestCases { get; set; } = [];
}

public class TestCase
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string DecisionName { get; set; }
    public Dictionary<string, JsonElement> Inputs { get; set; } = [];
    public Dictionary<string, JsonElement> ExpectedOutputs { get; set; } = [];
    public TestCaseLastRun LastRun { get; set; }
}

public class TestCaseLastRun
{
    public string Status { get; set; } // "pass", "fail", "error"
    public Dictionary<string, OutputValue> ActualOutputs { get; set; } = [];
    public List<int> HitRules { get; set; } = [];
    public long ExecutionTimeMs { get; set; }
    public string Error { get; set; }
    public DateTimeOffset RanAt { get; set; }
}

public class TestRunRequest
{
    // Optional: run only specific test case IDs. If empty, run all.
    public List<string> TestCaseIds { get; set; }
}

public class TestRunResult
{
    public List<TestCaseResult> Results { get; set; } = [];
    public long TotalTimeMs { get; set; }
}

public class TestCaseResult
{
    public string TestCaseId { get; set; }
    public string TestCaseName { get; set; }
    public string Status { get; set; } // "pass", "fail", "error"
    public Dictionary<string, OutputValue> ActualOutputs { get; set; } = [];
    public List<int> HitRules { get; set; } = [];
    public long ExecutionTimeMs { get; set; }
    public string Error { get; set; }
}

// ── CSV Batch Test ──

public class BatchTestCsvResult
{
    public string DmnFile { get; set; }
    public string DecisionName { get; set; }
    public string Mode { get; set; } // "execute" or "test"
    public BatchTestSummary Summary { get; set; }
    public List<BatchTestRowResult> Rows { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public long TotalTimeMs { get; set; }
}

public class BatchTestSummary
{
    public int TotalRows { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Errors { get; set; }
}

public class BatchTestRowResult
{
    public int RowNumber { get; set; }
    public string Status { get; set; } // "pass", "fail", "error", "executed"
    public Dictionary<string, OutputValue> ActualOutputs { get; set; } = [];
    public Dictionary<string, string> FailureDetails { get; set; }
    public List<int> HitRules { get; set; } = [];
    public long ExecutionTimeMs { get; set; }
    public string Error { get; set; }
}

// ── DMN Upload ──

public class UploadResult
{
    public bool Success { get; set; }
    public string FileName { get; set; }
    public string Message { get; set; }
}

// ── CSV Import ──

public class CsvImportResult
{
    public int Imported { get; set; }
    public int TotalTestCases { get; set; }
    public List<string> Warnings { get; set; } = [];
}
