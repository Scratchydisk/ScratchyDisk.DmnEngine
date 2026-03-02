using System.Globalization;
using ScratchyDisk.DmnEngine.Engine.Decisions;
using ScratchyDisk.DmnEngine.Engine.Decisions.Expression;
using ScratchyDisk.DmnEngine.Engine.Decisions.Table;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;
using ScratchyDisk.DmnEngine.Engine.Execution.Result;
using ScratchyDisk.DmnEngine.Parser;
using ScratchyDisk.DmnEngine.Parser.Dto;

// Suppress NLog console output so it doesn't pollute CLI output
NLog.LogManager.Configuration = new NLog.Config.LoggingConfiguration();

var (filePath, csvPath, inputs, decisionName, templatePath, versionOverride) = ParseArgs(args);

if (filePath == null)
{
    PrintUsage();
    return 1;
}

if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"Error: File not found: {filePath}");
    return 1;
}

try
{
    // Parse DMN file
    var dmnModel = DmnParser.ParseAutoDetect(filePath);

    // Apply version override if specified
    if (versionOverride != null)
    {
        dmnModel.DmnVersion = versionOverride.ToLowerInvariant() switch
        {
            "1.1" => DmnParser.DmnVersionEnum.V1_1,
            "1.3" => DmnParser.DmnVersionEnum.V1_3,
            "1.3ext" or "1.3-ext" => DmnParser.DmnVersionEnum.V1_3ext,
            "1.4" => DmnParser.DmnVersionEnum.V1_4,
            "1.5" => DmnParser.DmnVersionEnum.V1_5,
            _ => throw new ArgumentException($"Unknown DMN version: {versionOverride}. Supported: 1.1, 1.3, 1.3ext, 1.4, 1.5")
        };
    }

    // Template mode: always use the raw model to extract actual variable names,
    // types, and allowed values from decision table input expressions
    if (templatePath != null)
    {
        GenerateTemplateCsvFromModel(dmnModel, templatePath);

        Console.WriteLine($"Template CSV written to: {templatePath}");
        Console.WriteLine("Edit the Value column, then run:");
        Console.WriteLine($"  dmnrunner {filePath} --csv {templatePath}");
        return 0;
    }

    var definition1 = DmnDefinitionFactory.CreateDmnDefinition(dmnModel);
    var versionLabel = dmnModel.DmnVersion switch
    {
        DmnParser.DmnVersionEnum.V1_1 => "DMN 1.1",
        DmnParser.DmnVersionEnum.V1_3 => "DMN 1.3",
        DmnParser.DmnVersionEnum.V1_3ext => "DMN 1.3ext",
        DmnParser.DmnVersionEnum.V1_4 => "DMN 1.4",
        DmnParser.DmnVersionEnum.V1_5 => "DMN 1.5",
        _ => "DMN"
    };

    Console.WriteLine();
    Console.WriteLine($"=== DMN Smoke Test ===");
    Console.WriteLine($"File: {Path.GetFileName(filePath)} ({versionLabel})");

    // Print definition summary
    PrintDefinition(definition1);

    // Collect inputs from CSV and --input args
    var inputParams = new Dictionary<string, object>();

    if (csvPath != null)
    {
        if (!File.Exists(csvPath))
        {
            Console.Error.WriteLine($"Error: CSV file not found: {csvPath}");
            return 1;
        }
        foreach (var (name, value) in ReadCsv(csvPath))
        {
            inputParams[name] = value;
        }
    }

    foreach (var (name, value) in inputs)
    {
        inputParams[name] = value;
    }

    if (inputParams.Count == 0)
    {
        Console.WriteLine();
        Console.WriteLine("-- No inputs provided --");
        Console.WriteLine("Use --input Name=Value or --csv file.csv to provide inputs.");
        return 0;
    }

    // Print inputs
    Console.WriteLine();
    Console.WriteLine("-- Inputs --");
    foreach (var (name, value) in inputParams)
    {
        Console.WriteLine($"  {name,-20} = {FormatValue(value)}");
    }

    // Determine which decision to execute
    var rootDecisions = FindRootDecisions(definition1);
    IDmnDecision targetDecision;

    if (decisionName != null)
    {
        if (!definition1.Decisions.TryGetValue(decisionName, out targetDecision))
        {
            Console.Error.WriteLine($"Error: Decision '{decisionName}' not found.");
            Console.Error.WriteLine($"Available decisions: {string.Join(", ", definition1.Decisions.Keys)}");
            return 1;
        }
    }
    else if (rootDecisions.Count == 1)
    {
        targetDecision = rootDecisions[0];
    }
    else if (rootDecisions.Count > 1)
    {
        Console.Error.WriteLine($"Error: Multiple root decisions found. Use --decision to specify one:");
        foreach (var root in rootDecisions)
        {
            Console.Error.WriteLine($"  {root.Name}");
        }
        return 1;
    }
    else
    {
        Console.Error.WriteLine("Error: No decisions found in the DMN file.");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine($"-- Executing: {targetDecision.Name} --");

    // Create execution context with snapshots
    var ctx = DmnExecutionContextFactory.CreateExecutionContext(
        definition1,
        opts => { opts.RecordSnapshots = true; });

    // Set input parameters — the library automatically propagates values to
    // expression alias variables (e.g. Camunda exports with mismatched names)
    foreach (var (name, value) in inputParams)
    {
        var normalizedName = DmnVariableDefinition.NormalizeVariableName(name);
        ctx.WithInputParameter(normalizedName, value);
    }

    // Execute
    var result = ctx.ExecuteDecision(targetDecision);

    // Print trace from snapshots
    PrintTrace(ctx, definition1);

    // Print final result
    PrintResult(result);

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}


// ── Argument parsing ──

static (string filePath, string csvPath, List<(string name, object value)> inputs, string decisionName, string templatePath, string versionOverride) ParseArgs(string[] args)
{
    string filePath = null;
    string csvPath = null;
    string decisionName = null;
    string templatePath = null;
    string versionOverride = null;
    var inputs = new List<(string name, object value)>();

    for (int i = 0; i < args.Length; i++)
    {
        var arg = args[i];

        if (arg is "--help" or "-h" or "-?")
        {
            return (null, null, inputs, null, null, null);
        }
        else if (arg is "--csv" or "-c")
        {
            if (i + 1 >= args.Length) { Console.Error.WriteLine("Error: --csv requires a file path."); return (null, null, inputs, null, null, null); }
            csvPath = args[++i];
        }
        else if (arg is "--template" or "-t")
        {
            if (i + 1 >= args.Length) { Console.Error.WriteLine("Error: --template requires an output file path."); return (null, null, inputs, null, null, null); }
            templatePath = args[++i];
        }
        else if (arg is "--decision" or "-d")
        {
            if (i + 1 >= args.Length) { Console.Error.WriteLine("Error: --decision requires a name."); return (null, null, inputs, null, null, null); }
            decisionName = args[++i];
        }
        else if (arg is "--version" or "-v")
        {
            if (i + 1 >= args.Length) { Console.Error.WriteLine("Error: --version requires a version string."); return (null, null, inputs, null, null, null); }
            versionOverride = args[++i];
        }
        else if (arg is "--input" or "-i")
        {
            if (i + 1 >= args.Length) { Console.Error.WriteLine("Error: --input requires Name=Value."); return (null, null, inputs, null, null, null); }
            var parsed = ParseInputArg(args[++i]);
            if (parsed == null) { Console.Error.WriteLine($"Error: Invalid input format: {args[i]}. Expected Name=Value or Name:Type=Value."); return (null, null, inputs, null, null, null); }
            inputs.Add(parsed.Value);
        }
        else if (!arg.StartsWith("-"))
        {
            filePath = arg;
        }
        else
        {
            Console.Error.WriteLine($"Error: Unknown option: {arg}");
            return (null, null, inputs, null, null, null);
        }
    }

    return (filePath, csvPath, inputs, decisionName, templatePath, versionOverride);
}

static (string name, object value)? ParseInputArg(string input)
{
    var eqIdx = input.IndexOf('=');
    if (eqIdx <= 0) return null;

    var namePart = input[..eqIdx];
    var valuePart = input[(eqIdx + 1)..];

    // Check for Name:Type=Value syntax
    var colonIdx = namePart.IndexOf(':');
    if (colonIdx > 0)
    {
        var name = namePart[..colonIdx];
        var typeName = namePart[(colonIdx + 1)..].ToLowerInvariant();
        return (name, ConvertTyped(valuePart, typeName));
    }

    // Auto-infer type
    return (namePart, InferValue(valuePart));
}

static object InferValue(string value)
{
    if (bool.TryParse(value, out var b)) return b;
    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n;
    if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d;
    return value;
}

static object ConvertTyped(string value, string typeName)
{
    return typeName switch
    {
        "string" => value,
        "boolean" or "bool" => bool.Parse(value),
        "integer" or "int" => int.Parse(value, CultureInfo.InvariantCulture),
        "long" => long.Parse(value, CultureInfo.InvariantCulture),
        "double" => double.Parse(value, CultureInfo.InvariantCulture),
        "number" or "decimal" => decimal.Parse(value, CultureInfo.InvariantCulture),
        "date" => DateTime.Parse(value, CultureInfo.InvariantCulture),
        _ => value
    };
}

// ── CSV template generation ──

static void GenerateTemplateCsvFromModel(DmnModel model, string outputPath)
{
    // Collect input data elements: these define the external inputs the engine accepts.
    // The engine normalizes their names (spaces→underscores etc.) and that normalized
    // name is what WithInputParameter expects.
    var inputDataList = new List<(string id, string label, string normalizedName)>();
    var inputDataIds = new HashSet<string>();

    if (model.InputData != null)
    {
        foreach (var input in model.InputData)
        {
            var label = !string.IsNullOrWhiteSpace(input.Name) ? input.Name : input.Id;
            string normalizedName;
            try { normalizedName = DmnVariableDefinition.NormalizeVariableName(label); }
            catch { normalizedName = label.Trim().Replace(' ', '_').Replace('-', '_'); }

            inputDataList.Add((input.Id, label, normalizedName));
            inputDataIds.Add(input.Id);
        }
    }

    // Walk all decision tables to collect type info from table input columns.
    // We build lookups by expression variable name and by label so we can match
    // them to input data elements by name rather than by fragile position.
    var tableInfoByExprName = new Dictionary<string, (string typeRef, string exprVarName, string tableLabel, string allowedValues)>(StringComparer.OrdinalIgnoreCase);
    var tableInfoByLabel = new Dictionary<string, (string typeRef, string exprVarName, string tableLabel, string allowedValues)>(StringComparer.OrdinalIgnoreCase);

    if (model.Decisions != null)
    {
        foreach (var decision in model.Decisions)
        {
            if (decision.DecisionTable?.Inputs == null) continue;

            foreach (var tableInput in decision.DecisionTable.Inputs)
            {
                var info = ExtractTableInputInfo(tableInput);
                if (!string.IsNullOrWhiteSpace(info.exprVarName) && !tableInfoByExprName.ContainsKey(info.exprVarName))
                    tableInfoByExprName[info.exprVarName] = info;
                if (!string.IsNullOrWhiteSpace(info.tableLabel) && !tableInfoByLabel.ContainsKey(info.tableLabel))
                    tableInfoByLabel[info.tableLabel] = info;
            }
        }
    }

    // For each input data element, find the best matching table input by name.
    // Key: inputData ID → matched table input info
    var typeInfoByInputId = new Dictionary<string, (string typeRef, string exprVarName, string tableLabel, string allowedValues)>();
    foreach (var (id, label, normalizedName) in inputDataList)
    {
        // Try matching: normalized name == expression variable name
        if (tableInfoByExprName.TryGetValue(normalizedName, out var info1))
        { typeInfoByInputId[id] = info1; continue; }

        // Try matching: original label == expression variable name
        if (tableInfoByExprName.TryGetValue(label, out var info2))
        { typeInfoByInputId[id] = info2; continue; }

        // Try matching: original label == table input label
        if (tableInfoByLabel.TryGetValue(label, out var info3))
        { typeInfoByInputId[id] = info3; continue; }

        // Try matching: normalized name == table input label
        if (tableInfoByLabel.TryGetValue(normalizedName, out var info4))
        { typeInfoByInputId[id] = info4; continue; }

        // For single-input-data decisions, use the information requirement chain
        if (model.Decisions != null)
        {
            foreach (var decision in model.Decisions)
            {
                if (decision.InformationRequirements == null || decision.DecisionTable?.Inputs == null) continue;

                var requiredInputIds = new List<string>();
                foreach (var req in decision.InformationRequirements)
                {
                    try { if (req.RequirementType == InformationRequirementType.Input) requiredInputIds.Add(req.Ref); }
                    catch { }
                }

                if (requiredInputIds.Count == 1 && requiredInputIds[0] == id)
                {
                    // This decision requires only this input data — pick the first table input
                    // that isn't a known decision output
                    foreach (var tableInput in decision.DecisionTable.Inputs)
                    {
                        var exprText = tableInput.InputExpression?.Text?.Trim();
                        if (string.IsNullOrWhiteSpace(exprText)) continue;
                        var ti = ExtractTableInputInfo(tableInput);
                        typeInfoByInputId[id] = ti;
                        break;
                    }
                    if (typeInfoByInputId.ContainsKey(id)) break;
                }
            }
        }
    }

    // Write CSV using the canonical input data names the engine expects
    using var writer = new StreamWriter(outputPath);
    writer.WriteLine("Name,Type,Value,Notes");
    foreach (var (id, label, normalizedName) in inputDataList)
    {
        var typeName = "";
        var placeholder = "";
        var notes = new List<string>();

        if (label != normalizedName)
            notes.Add(label);

        if (typeInfoByInputId.TryGetValue(id, out var info))
        {
            if (!string.IsNullOrWhiteSpace(info.typeRef))
            {
                typeName = info.typeRef.ToLowerInvariant();
                placeholder = SampleValueFromTypeRef(typeName);
            }
            if (!string.IsNullOrWhiteSpace(info.exprVarName) && info.exprVarName != normalizedName)
                notes.Add($"expr: {info.exprVarName}");
            if (!string.IsNullOrWhiteSpace(info.tableLabel) && info.tableLabel != label && info.tableLabel != normalizedName)
                notes.Add(info.tableLabel);
            if (!string.IsNullOrWhiteSpace(info.allowedValues))
                notes.Add($"allowed: {info.allowedValues}");
        }

        var notesStr = notes.Count > 0 ? CsvEscape(string.Join("; ", notes)) : "";
        writer.WriteLine($"{CsvEscape(normalizedName)},{typeName},{placeholder},{notesStr}");
    }
}

static (string typeRef, string exprVarName, string tableLabel, string allowedValues) ExtractTableInputInfo(DecisionTableInput tableInput)
{
    var expr = tableInput.InputExpression;
    var varName = expr?.Text?.Trim();
    var typeRef = expr?.TypeRef;
    var tableLabel = tableInput.Label;
    var allowed = tableInput.AllowedInputValues?.Values != null
        ? string.Join(", ", tableInput.AllowedInputValues.Values)
        : null;
    return (typeRef, varName, tableLabel, allowed);
}

static string SampleValueFromTypeRef(string typeRef)
{
    return typeRef switch
    {
        "string" => "",
        "integer" or "int" => "0",
        "long" => "0",
        "double" => "0.0",
        "number" or "decimal" => "0",
        "boolean" or "bool" => "false",
        "date" => "2024-01-01",
        _ => ""
    };
}

static string CsvEscape(string value)
{
    if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        return $"\"{value.Replace("\"", "\"\"")}\"";
    return value;
}

// ── CSV reading ──

static IEnumerable<(string name, object value)> ReadCsv(string csvPath)
{
    var lines = File.ReadAllLines(csvPath);
    if (lines.Length == 0) yield break;

    // Skip header row (Name,Type,Value)
    for (int i = 1; i < lines.Length; i++)
    {
        var line = lines[i].Trim();
        if (string.IsNullOrEmpty(line)) continue;

        var parts = SplitCsvLine(line);
        if (parts.Count < 2) continue;

        var name = parts[0].Trim();
        string typeName = null;
        string value;

        if (parts.Count >= 3)
        {
            typeName = parts[1].Trim().ToLowerInvariant();
            value = parts[2].Trim();
        }
        else
        {
            // Only Name,Value — no type column
            value = parts[1].Trim();
        }

        if (string.IsNullOrEmpty(typeName))
            yield return (name, InferValue(value));
        else
            yield return (name, ConvertTyped(value, typeName));
    }
}

static List<string> SplitCsvLine(string line)
{
    var result = new List<string>();
    var current = new System.Text.StringBuilder();
    bool inQuotes = false;

    for (int i = 0; i < line.Length; i++)
    {
        var ch = line[i];
        if (ch == '"')
        {
            if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');
                i++;
            }
            else
            {
                inQuotes = !inQuotes;
            }
        }
        else if (ch == ',' && !inQuotes)
        {
            result.Add(current.ToString());
            current.Clear();
        }
        else
        {
            current.Append(ch);
        }
    }
    result.Add(current.ToString());
    return result;
}


// ── Definition printing ──

static void PrintDefinition(DmnDefinition definition)
{
    Console.WriteLine();
    Console.WriteLine("-- Definition --");

    // Inputs
    Console.WriteLine("Inputs:");
    if (definition.InputData.Count == 0)
    {
        Console.WriteLine("  (none)");
    }
    else
    {
        var maxNameLen = definition.InputData.Values.Max(v => v.Name.Length);
        foreach (var input in definition.InputData.Values)
        {
            var typeName = input.Type != null ? TypeLabel(input.Type) : "any";
            var label = input.Label != null && input.Label != input.Name ? $"  \"{input.Label}\"" : "";
            Console.WriteLine($"  {input.Name.PadRight(maxNameLen)}  {typeName}{label}");
        }
    }

    Console.WriteLine();

    // Decisions
    var rootDecisions = FindRootDecisions(definition);
    Console.WriteLine("Decisions:");
    foreach (var decision in definition.Decisions.Values)
    {
        var info = DecisionInfo(decision);
        var deps = decision.RequiredDecisions.Count > 0
            ? "  requires: " + string.Join(", ", decision.RequiredDecisions.Select(d => d.Name))
            : "";
        var inputDeps = decision.RequiredInputs.Count > 0
            ? (deps.Length > 0 ? ", " : "  requires: ") + string.Join(", ", decision.RequiredInputs.Select(v => v.Name))
            : "";
        var rootMarker = rootDecisions.Contains(decision) ? "  <- ROOT" : "";
        Console.WriteLine($"  {decision.Name,-30} {info}{deps}{inputDeps}{rootMarker}");
    }
}

static string DecisionInfo(IDmnDecision decision)
{
    if (decision is DmnDecisionTable table)
    {
        var agg = table.HitPolicy == HitPolicyEnum.Collect ? $"+{table.Aggregation}" : "";
        return $"[table, {table.HitPolicy}{agg}, {table.Rules.Length} rules]";
    }
    if (decision is DmnExpressionDecision expr)
    {
        return $"[expression: {expr.Expression}]";
    }
    return "[unknown]";
}

static List<IDmnDecision> FindRootDecisions(DmnDefinition definition)
{
    var allRequired = new HashSet<IDmnDecision>(
        definition.Decisions.Values.SelectMany(d => d.RequiredDecisions));

    return definition.Decisions.Values
        .Where(d => !allRequired.Contains(d))
        .ToList();
}


// ── Trace printing ──

static void PrintTrace(DmnExecutionContext ctx, DmnDefinition definition)
{
    var snapshots = ctx.Snapshots.Snapshots;
    if (snapshots.Count <= 1)
    {
        Console.WriteLine();
        Console.WriteLine("-- Trace --");
        Console.WriteLine("  (no execution steps recorded)");
        return;
    }

    Console.WriteLine();
    Console.WriteLine("-- Trace --");

    for (int i = 1; i < snapshots.Count; i++)
    {
        var snapshot = snapshots[i];
        var prevSnapshot = snapshots[i - 1];
        var decision = snapshot.Decision;
        var info = DecisionInfo(decision);

        Console.WriteLine($"Step {i}: {snapshot.DecisionName} {info}");

        // For table decisions, show hit rules
        if (decision is DmnDecisionTable table && snapshot.DecisionResult?.HasResult == true)
        {
            foreach (var singleResult in snapshot.DecisionResult.Results)
            {
                foreach (var hitRule in singleResult.HitRules)
                {
                    Console.WriteLine($"  Rule {hitRule.Index + 1} HIT{(hitRule.Name != null ? $" ({hitRule.Name})" : "")}");

                    // Show input conditions
                    foreach (var ruleInput in hitRule.Inputs)
                    {
                        var inputLabel = ruleInput.Input.Variable?.Name ?? ruleInput.Input.Expression ?? ruleInput.Input.Label;
                        var inputVar = ruleInput.Input.Variable;
                        var currentValue = inputVar != null ? snapshot[inputVar.Name]?.Value : null;
                        var expr = string.IsNullOrEmpty(ruleInput.UnparsedExpression) ? "-" : ruleInput.UnparsedExpression;
                        Console.WriteLine($"    input: {inputLabel} (= {FormatValue(currentValue)}) matches \"{expr}\"");
                    }

                    // Show outputs
                    foreach (var ruleOutput in hitRule.Outputs)
                    {
                        var outputName = ruleOutput.Output.Variable?.Name ?? ruleOutput.Output.Label;
                        var expr = ruleOutput.Expression ?? "(empty)";
                        Console.WriteLine($"    output -> {outputName} = {expr}");
                    }
                }
            }
        }

        // Show variable changes
        var changes = new List<string>();
        foreach (var variable in snapshot.Variables)
        {
            if (variable.IsInputParameter) continue;
            var prevVar = prevSnapshot[variable.Name];
            var prevValue = prevVar?.Value;
            var curValue = variable.Value;

            if (!Equals(prevValue, curValue))
            {
                changes.Add($"  -> {variable.Name} = {FormatValue(curValue)}");
            }
        }

        foreach (var change in changes)
        {
            Console.WriteLine(change);
        }

        Console.WriteLine();
    }
}


// ── Result printing ──

static void PrintResult(DmnDecisionResult result)
{
    Console.WriteLine("-- Result --");

    if (!result.HasResult)
    {
        Console.WriteLine("  (no result)");
        return;
    }

    if (result.IsSingleResult)
    {
        foreach (var variable in result.FirstResultVariables)
        {
            var typeName = variable.Type != null ? TypeLabel(variable.Type) : "?";
            Console.WriteLine($"  {variable.Name,-20} = {FormatValue(variable.Value),-30} ({typeName})");
        }
    }
    else
    {
        // Multiple results (Collect/RuleOrder/OutputOrder)
        for (int i = 0; i < result.Results.Count; i++)
        {
            Console.WriteLine($"  [{i + 1}]");
            foreach (var variable in result.Results[i].Variables)
            {
                var typeName = variable.Type != null ? TypeLabel(variable.Type) : "?";
                Console.WriteLine($"      {variable.Name,-20} = {FormatValue(variable.Value),-30} ({typeName})");
            }
        }
    }
}


// ── Formatting helpers ──

static string FormatValue(object value)
{
    if (value == null) return "null";
    if (value is string s) return $"\"{s}\"";
    if (value is bool b) return b ? "true" : "false";
    if (value is DateTime dt) return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    return Convert.ToString(value, CultureInfo.InvariantCulture);
}

static string TypeLabel(Type type)
{
    if (type == typeof(string)) return "string";
    if (type == typeof(int)) return "integer";
    if (type == typeof(long)) return "long";
    if (type == typeof(double)) return "double";
    if (type == typeof(decimal)) return "number";
    if (type == typeof(bool)) return "boolean";
    if (type == typeof(DateTime)) return "date";
    return type.Name;
}

static void PrintUsage()
{
    Console.WriteLine("DMN Smoke Test Tool");
    Console.WriteLine();
    Console.WriteLine("Usage: dmnrunner <file.dmn> [options]");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  <file.dmn>                    Path to DMN file");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -i, --input Name=Value        Input parameter (repeatable)");
    Console.WriteLine("  -i, --input Name:Type=Value   Input with explicit type");
    Console.WriteLine("  -c, --csv <file.csv>          CSV file with input parameters");
    Console.WriteLine("  -t, --template <file.csv>     Generate a template CSV from the DMN inputs");
    Console.WriteLine("  -d, --decision <name>         Decision to execute (default: auto-detect root)");
    Console.WriteLine("  -v, --version <ver>           Override DMN version (1.1, 1.3, 1.3ext, 1.4, 1.5)");
    Console.WriteLine("  -h, --help                    Show this help");
    Console.WriteLine();
    Console.WriteLine("Supported types: string, integer, long, double, number, boolean, date");
    Console.WriteLine();
    Console.WriteLine("CSV format:");
    Console.WriteLine("  Name,Type,Value");
    Console.WriteLine("  Age,integer,33");
    Console.WriteLine("  Greeting,string,Hello");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dmnrunner model.dmn --input Age=33 --input Name=John");
    Console.WriteLine("  dmnrunner model.dmn --csv params.csv");
    Console.WriteLine("  dmnrunner model.dmn --input Age:integer=33 --decision MainDT");
}
