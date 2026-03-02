using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using ScratchyDisk.DmnEngine.Testbed.Models;

namespace ScratchyDisk.DmnEngine.Testbed.Services;

public class CsvBatchTestService
{
    private readonly DmnExecutionService _executionService;
    private readonly DmnFileService _fileService;

    private const string ExpectedPrefix = "expected:";

    public CsvBatchTestService(DmnExecutionService executionService, DmnFileService fileService)
    {
        _executionService = executionService;
        _fileService = fileService;
    }

    public BatchTestCsvResult RunBatchTest(string dmnName, string decisionName, Stream csvStream)
    {
        var totalSw = Stopwatch.StartNew();

        var info = _executionService.GetInfo(dmnName);
        var decision = info.Decisions.FirstOrDefault(d =>
            string.Equals(d.Name, decisionName, StringComparison.OrdinalIgnoreCase));

        if (decision == null)
            throw new ArgumentException(
                $"Decision '{decisionName}' not found in '{dmnName}'. " +
                $"Available: {string.Join(", ", info.Decisions.Select(d => d.Name))}");

        var (headers, rows) = ParseCsv(csvStream);

        if (rows.Count == 0)
            throw new ArgumentException("CSV content is empty");

        // Classify columns
        var warnings = new List<string>();
        var inputMappings = new List<ColumnMapping>();   // CSV col index -> input variable name
        var expectedMappings = new List<ColumnMapping>(); // CSV col index -> output variable name

        var allInputs = decision.AllRequiredInputs ?? [];
        var allOutputs = BuildOutputList(info, decision);

        for (var i = 0; i < headers.Length; i++)
        {
            var header = headers[i].Trim();

            // Skip metadata columns used by test suite CSV export
            if (IsMetadataColumn(header))
                continue;

            if (header.StartsWith(ExpectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var outputName = header[ExpectedPrefix.Length..].Trim();
                var match = MatchColumn(outputName, allOutputs);
                if (match != null)
                    expectedMappings.Add(new ColumnMapping(i, match.Name, match.TypeName));
                else
                    warnings.Add($"Column '{header}': no matching output variable found");
            }
            else
            {
                var match = MatchInput(header, allInputs, info.InputData, decision.TableInputs ?? []);
                if (match != null)
                    inputMappings.Add(new ColumnMapping(i, match.Name, match.TypeName));
                else
                    warnings.Add($"Column '{header}': no matching input variable found");
            }
        }

        if (inputMappings.Count == 0)
            throw new ArgumentException(
                $"No columns match known inputs for decision '{decisionName}'. " +
                $"Expected: {string.Join(", ", allInputs)}");

        var isTestMode = expectedMappings.Count > 0;
        var result = new BatchTestCsvResult
        {
            DmnFile = dmnName,
            DecisionName = decisionName,
            Mode = isTestMode ? "test" : "execute",
            Warnings = warnings,
            Summary = new BatchTestSummary()
        };

        // Execute each row
        for (var rowIdx = 0; rowIdx < rows.Count; rowIdx++)
        {
            var row = rows[rowIdx];
            var rowResult = ExecuteRow(dmnName, decisionName, row, rowIdx + 1,
                inputMappings, expectedMappings, isTestMode);
            result.Rows.Add(rowResult);

            switch (rowResult.Status)
            {
                case "pass": result.Summary.Passed++; break;
                case "fail": result.Summary.Failed++; break;
                case "error": result.Summary.Errors++; break;
            }
        }

        result.Summary.TotalRows = rows.Count;
        totalSw.Stop();
        result.TotalTimeMs = totalSw.ElapsedMilliseconds;

        return result;
    }

    public CsvImportResult ImportAsTestSuite(string dmnName, string decisionName, Stream csvStream, bool replace = false)
    {
        var info = _executionService.GetInfo(dmnName);
        var decisionsByName = info.Decisions.ToDictionary(
            d => d.Name, d => d, StringComparer.OrdinalIgnoreCase);

        if (!decisionsByName.ContainsKey(decisionName))
            throw new ArgumentException(
                $"Decision '{decisionName}' not found in '{dmnName}'. " +
                $"Available: {string.Join(", ", info.Decisions.Select(d => d.Name))}");

        var (headers, rows) = ParseCsv(csvStream);
        var warnings = new List<string>();

        // Detect reserved metadata columns
        int nameColIndex = -1, decisionColIndex = -1;
        for (var i = 0; i < headers.Length; i++)
        {
            var h = headers[i].Trim();
            if (string.Equals(h, "Name", StringComparison.OrdinalIgnoreCase))
                nameColIndex = i;
            else if (string.Equals(h, "Decision", StringComparison.OrdinalIgnoreCase))
                decisionColIndex = i;
        }

        // Build column mappings across ALL decisions so CSV files targeting any decision work
        var allInputs = info.Decisions
            .SelectMany(d => d.AllRequiredInputs ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allTableInputs = new List<TableColumnInfo>();
        var allOutputs = new List<ColumnInfo>();
        foreach (var d in info.Decisions)
        {
            foreach (var ti in d.TableInputs ?? [])
            {
                if (allTableInputs.All(t => !string.Equals(t.Name, ti.Name, StringComparison.OrdinalIgnoreCase)))
                    allTableInputs.Add(ti);
            }
            foreach (var o in d.TableOutputs ?? [])
            {
                if (allOutputs.All(x => !string.Equals(x.Name, o.Name, StringComparison.OrdinalIgnoreCase)))
                    allOutputs.Add(new ColumnInfo(o.Name, o.Label, o.TypeName));
            }
        }

        var inputMappings = new List<ColumnMapping>();
        var expectedMappings = new List<ColumnMapping>();

        for (var i = 0; i < headers.Length; i++)
        {
            if (i == nameColIndex || i == decisionColIndex) continue;

            var header = headers[i].Trim();

            if (header.StartsWith(ExpectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var outputName = header[ExpectedPrefix.Length..].Trim();
                var match = MatchColumn(outputName, allOutputs);
                if (match != null)
                    expectedMappings.Add(new ColumnMapping(i, match.Name, match.TypeName));
                else
                    warnings.Add($"Column '{header}': no matching output variable found");
            }
            else
            {
                var match = MatchInput(header, allInputs, info.InputData, allTableInputs);
                if (match != null)
                    inputMappings.Add(new ColumnMapping(i, match.Name, match.TypeName));
                else
                    warnings.Add($"Column '{header}': no matching input variable found");
            }
        }

        // Load existing suite and optionally replace
        var suite = _fileService.LoadTestSuite(dmnName);
        if (replace)
            suite.TestCases.Clear();
        var imported = 0;

        for (var rowIdx = 0; rowIdx < rows.Count; rowIdx++)
        {
            var row = rows[rowIdx];
            var inputs = new Dictionary<string, JsonElement>();
            var expectedOutputs = new Dictionary<string, JsonElement>();

            foreach (var mapping in inputMappings)
            {
                if (mapping.ColumnIndex < row.Length)
                {
                    var cellValue = row[mapping.ColumnIndex]?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(cellValue))
                        inputs[mapping.VariableName] = ToJsonElement(cellValue);
                }
            }

            foreach (var mapping in expectedMappings)
            {
                if (mapping.ColumnIndex < row.Length)
                {
                    var cellValue = row[mapping.ColumnIndex]?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(cellValue))
                        expectedOutputs[mapping.VariableName] = ToJsonElement(cellValue);
                }
            }

            // Use Name column if present, otherwise auto-generate
            var testCaseName = nameColIndex >= 0 && nameColIndex < row.Length
                ? row[nameColIndex]?.Trim() : null;
            if (string.IsNullOrEmpty(testCaseName))
                testCaseName = $"Row {rowIdx + 1}";

            // Use Decision column if present, otherwise use the query parameter
            var rowDecision = decisionColIndex >= 0 && decisionColIndex < row.Length
                ? row[decisionColIndex]?.Trim() : null;
            if (string.IsNullOrEmpty(rowDecision))
                rowDecision = decisionName;

            var testCase = new TestCase
            {
                Id = Guid.NewGuid().ToString(),
                Name = testCaseName,
                DecisionName = rowDecision,
                Inputs = inputs,
                ExpectedOutputs = expectedOutputs,
                LastRun = null
            };

            suite.TestCases.Add(testCase);
            imported++;
        }

        _fileService.SaveTestSuite(dmnName, suite);

        return new CsvImportResult
        {
            Imported = imported,
            TotalTestCases = suite.TestCases.Count,
            Warnings = warnings
        };
    }

    // ── Private helpers ──

    private BatchTestRowResult ExecuteRow(
        string dmnName,
        string decisionName,
        string[] row,
        int rowNumber,
        List<ColumnMapping> inputMappings,
        List<ColumnMapping> expectedMappings,
        bool isTestMode)
    {
        var sw = Stopwatch.StartNew();
        var rowResult = new BatchTestRowResult { RowNumber = rowNumber };

        try
        {
            var inputs = new Dictionary<string, JsonElement>();

            foreach (var mapping in inputMappings)
            {
                if (mapping.ColumnIndex < row.Length)
                {
                    var cellValue = row[mapping.ColumnIndex]?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(cellValue))
                        inputs[mapping.VariableName] = ToJsonElement(cellValue);
                }
            }

            var request = new ExecuteRequest
            {
                DecisionName = decisionName,
                Inputs = inputs
            };

            var execResult = _executionService.Execute(dmnName, request);

            if (execResult.Error != null)
            {
                sw.Stop();
                rowResult.Status = "error";
                rowResult.Error = execResult.Error;
                rowResult.ExecutionTimeMs = sw.ElapsedMilliseconds;
                return rowResult;
            }

            // Collect actual outputs
            if (execResult.HasResult && execResult.Results.Count > 0)
            {
                rowResult.ActualOutputs = execResult.Results[0].Outputs;
                rowResult.HitRules = execResult.Results[0].HitRules.Select(h => h.Index).ToList();

                // Include upstream decision outputs
                foreach (var step in execResult.Steps ?? [])
                {
                    foreach (var (varName, value) in step.VariableChanges)
                    {
                        if (!rowResult.ActualOutputs.ContainsKey(varName))
                        {
                            rowResult.ActualOutputs[varName] = new OutputValue
                            {
                                Value = value,
                                TypeName = value?.GetType() is { } t ? DmnExecutionService.TypeLabel(t) : null
                            };
                        }
                    }
                }
            }

            if (!isTestMode)
            {
                rowResult.Status = "executed";
            }
            else
            {
                // Compare expected vs actual
                var allMatch = true;
                var failureDetails = new Dictionary<string, string>();

                foreach (var mapping in expectedMappings)
                {
                    if (mapping.ColumnIndex >= row.Length) continue;

                    var expectedStr = row[mapping.ColumnIndex]?.Trim() ?? "";
                    if (string.IsNullOrEmpty(expectedStr)) continue;

                    if (!rowResult.ActualOutputs.TryGetValue(mapping.VariableName, out var actual) ||
                        actual.Value == null)
                    {
                        allMatch = false;
                        failureDetails[mapping.VariableName] =
                            $"Expected '{expectedStr}' but got no value";
                        continue;
                    }

                    if (!CompareValues(expectedStr, actual.Value))
                    {
                        allMatch = false;
                        failureDetails[mapping.VariableName] =
                            $"Expected '{expectedStr}' but got '{actual.Value}'";
                    }
                }

                rowResult.Status = allMatch ? "pass" : "fail";
                rowResult.FailureDetails = failureDetails.Count > 0 ? failureDetails : null;
            }
        }
        catch (Exception ex)
        {
            rowResult.Status = "error";
            rowResult.Error = ex.Message;
        }

        sw.Stop();
        rowResult.ExecutionTimeMs = sw.ElapsedMilliseconds;
        return rowResult;
    }

    private static (string[] Headers, List<string[]> Rows) ParseCsv(Stream csvStream)
    {
        // Read all text first so we can strip #LOOKUPS section
        using var streamReader = new StreamReader(csvStream);
        var fullText = streamReader.ReadToEnd();

        // Strip #LOOKUPS section (everything from a line starting with #LOOKUPS onwards)
        var lookupIndex = fullText.IndexOf("\n#LOOKUPS", StringComparison.OrdinalIgnoreCase);
        if (lookupIndex < 0)
            lookupIndex = fullText.IndexOf("\r\n#LOOKUPS", StringComparison.OrdinalIgnoreCase);
        if (lookupIndex >= 0)
            fullText = fullText[..lookupIndex];

        using var textReader = new StringReader(fullText);
        using var csv = new CsvReader(textReader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        });

        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];

        var rows = new List<string[]>();
        while (csv.Read())
        {
            var row = new string[headers.Length];
            for (var i = 0; i < headers.Length; i++)
            {
                row[i] = csv.GetField(i);
            }
            rows.Add(row);
        }

        return (headers, rows);
    }

    private static List<ColumnInfo> BuildOutputList(DefinitionInfo info, DecisionInfo decision)
    {
        var outputs = new List<ColumnInfo>();

        // Decision's own outputs
        foreach (var o in decision.TableOutputs ?? [])
        {
            outputs.Add(new ColumnInfo(o.Name, o.Label, o.TypeName));
        }

        // Upstream decision outputs (walk required decisions transitively)
        var decisionsByName = info.Decisions.ToDictionary(d => d.Name);
        var visited = new HashSet<string>();
        CollectUpstreamOutputs(decision, decisionsByName, visited, outputs);

        return outputs;
    }

    private static void CollectUpstreamOutputs(
        DecisionInfo decision,
        Dictionary<string, DecisionInfo> decisionsByName,
        HashSet<string> visited,
        List<ColumnInfo> outputs)
    {
        foreach (var rdName in decision.RequiredDecisions ?? [])
        {
            if (!visited.Add(rdName)) continue;
            if (!decisionsByName.TryGetValue(rdName, out var rd)) continue;

            foreach (var o in rd.TableOutputs ?? [])
            {
                if (outputs.All(x => x.Name != o.Name))
                    outputs.Add(new ColumnInfo(o.Name, o.Label, o.TypeName));
            }

            CollectUpstreamOutputs(rd, decisionsByName, visited, outputs);
        }
    }

    private static ColumnInfo MatchInput(
        string csvHeader,
        List<string> allRequiredInputs,
        List<VariableInfo> inputData,
        List<TableColumnInfo> tableInputs)
    {
        // Exact name match (case-insensitive)
        var exactInput = inputData.FirstOrDefault(v =>
            string.Equals(v.Name, csvHeader, StringComparison.OrdinalIgnoreCase));
        if (exactInput != null)
            return new ColumnInfo(exactInput.Name, exactInput.Label, exactInput.TypeName);

        // Match against table input labels
        var labelMatch = tableInputs.FirstOrDefault(t =>
            string.Equals(t.Label, csvHeader, StringComparison.OrdinalIgnoreCase));
        if (labelMatch?.Name != null)
            return new ColumnInfo(labelMatch.Name, labelMatch.Label, labelMatch.TypeName);

        // Match against allRequiredInputs list (case-insensitive)
        var reqMatch = allRequiredInputs.FirstOrDefault(name =>
            string.Equals(name, csvHeader, StringComparison.OrdinalIgnoreCase));
        if (reqMatch != null)
        {
            var varInfo = inputData.FirstOrDefault(v =>
                string.Equals(v.Name, reqMatch, StringComparison.OrdinalIgnoreCase));
            return new ColumnInfo(reqMatch, varInfo?.Label, varInfo?.TypeName);
        }

        return null;
    }

    private static ColumnInfo MatchColumn(string name, List<ColumnInfo> candidates)
    {
        // Exact name match (case-insensitive)
        var exact = candidates.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // Label match
        var label = candidates.FirstOrDefault(c =>
            string.Equals(c.Label, name, StringComparison.OrdinalIgnoreCase));
        return label;
    }

    private static bool CompareValues(string expected, object actual)
    {
        var actualStr = actual?.ToString() ?? "";

        // String comparison (case-insensitive)
        if (string.Equals(expected, actualStr, StringComparison.OrdinalIgnoreCase))
            return true;

        // Try numeric comparison for decimal tolerance
        if (decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var expectedDecimal))
        {
            if (actual is decimal actualDecimal)
                return expectedDecimal == actualDecimal;
            if (actual is int actualInt)
                return expectedDecimal == actualInt;
            if (actual is long actualLong)
                return expectedDecimal == actualLong;
            if (actual is double actualDouble)
                return Math.Abs((double)expectedDecimal - actualDouble) < 0.0001;
        }

        // Boolean comparison
        if (bool.TryParse(expected, out var expectedBool))
        {
            if (actual is bool actualBool)
                return expectedBool == actualBool;
        }

        return false;
    }

    private static JsonElement ToJsonElement(string value)
    {
        // Try to produce a typed JSON element from the string
        if (bool.TryParse(value, out var b))
            return JsonDocument.Parse(b ? "true" : "false").RootElement.Clone();
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            return JsonDocument.Parse(n.ToString(CultureInfo.InvariantCulture)).RootElement.Clone();
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            return JsonDocument.Parse(d.ToString(CultureInfo.InvariantCulture)).RootElement.Clone();

        // Default: string
        return JsonDocument.Parse($"\"{JsonEncodedText.Encode(value)}\"").RootElement.Clone();
    }

    private static bool IsMetadataColumn(string header)
    {
        return string.Equals(header, "Name", StringComparison.OrdinalIgnoreCase)
               || string.Equals(header, "Decision", StringComparison.OrdinalIgnoreCase);
    }

    // ── Internal types ──

    private record ColumnMapping(int ColumnIndex, string VariableName, string TypeName);
    private record ColumnInfo(string Name, string Label, string TypeName);
}
