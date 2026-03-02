using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Xml;
using ScratchyDisk.DmnEngine.Engine.Decisions;
using ScratchyDisk.DmnEngine.Engine.Decisions.Expression;
using ScratchyDisk.DmnEngine.Engine.Decisions.Table;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;
using ScratchyDisk.DmnEngine.Parser;
using ScratchyDisk.DmnEngine.Parser.Dto;
using ScratchyDisk.DmnEngine.Testbed.Models;

namespace ScratchyDisk.DmnEngine.Testbed.Services;

public class DmnExecutionService
{
    private readonly DmnFileService _fileService;
    private readonly ConcurrentDictionary<string, (DmnDefinition Definition, DmnModel Model, DateTime LastWrite)> _cache = new();

    public DmnExecutionService(DmnFileService fileService)
    {
        _fileService = fileService;
    }

    // ── Definition caching ──

    private (DmnDefinition Definition, DmnModel Model) GetDefinitionAndModel(string name)
    {
        var path = _fileService.GetFilePath(name);
        var lastWrite = File.GetLastWriteTimeUtc(path);

        if (_cache.TryGetValue(name, out var cached) && cached.LastWrite >= lastWrite)
            return (cached.Definition, cached.Model);

        var model = DmnParser.ParseAutoDetect(path);
        var definition = DmnDefinitionFactory.CreateDmnDefinition(model);
        _cache[name] = (definition, model, lastWrite);
        return (definition, model);
    }

    private DmnDefinition GetDefinition(string name) => GetDefinitionAndModel(name).Definition;

    // ── Get definition info ──

    public DefinitionInfo GetInfo(string name)
    {
        var (definition, model) = GetDefinitionAndModel(name);
        var filePath = _fileService.GetFilePath(name);

        var info = new DefinitionInfo
        {
            FileName = Path.GetFileName(filePath),
            Metadata = ExtractMetadata(filePath, model),
            InputData = definition.InputData.Values.Select(v => new VariableInfo
            {
                Name = v.Name,
                Label = v.Label,
                TypeName = v.Type != null ? TypeLabel(v.Type) : null,
                IsInputParameter = v.IsInputParameter
            }).ToList(),
            Decisions = definition.Decisions.Values.Select(d =>
            {
                var di = new DecisionInfo
                {
                    Name = d.Name,
                    RequiredDecisions = d.RequiredDecisions.Select(rd => rd.Name).ToList(),
                    RequiredInputs = d.RequiredInputs.Select(ri => ri.Name).ToList(),
                    AllRequiredInputs = d.GetAllRequiredInputs().Select(ri => ri.Name).ToList()
                };

                if (d is DmnDecisionTable table)
                {
                    di.Type = "table";
                    di.HitPolicy = table.HitPolicy.ToString();
                    di.Aggregation = table.HitPolicy == HitPolicyEnum.Collect
                        ? table.Aggregation.ToString()
                        : null;
                    di.RuleCount = table.Rules.Length;
                    di.TableInputs = table.Inputs.Select(inp => new TableColumnInfo
                    {
                        Name = inp.Variable?.Name,
                        Label = inp.Label,
                        TypeName = inp.Variable?.Type != null ? TypeLabel(inp.Variable.Type) : null,
                        AllowedValues = inp.AllowedValues?.ToList() ?? [],
                        Expression = inp.Expression
                    }).ToList();
                    di.TableOutputs = table.Outputs.Select(outp => new TableColumnInfo
                    {
                        Name = outp.Variable?.Name,
                        Label = outp.Label,
                        TypeName = outp.Variable?.Type != null ? TypeLabel(outp.Variable.Type) : null,
                        AllowedValues = outp.AllowedValues?.ToList() ?? []
                    }).ToList();
                }
                else if (d is DmnExpressionDecision expr)
                {
                    di.Type = "expression";
                    di.Expression = expr.Expression;
                    if (expr.Output != null)
                    {
                        di.TableOutputs =
                        [
                            new TableColumnInfo
                            {
                                Name = expr.Output.Name,
                                Label = expr.Output.Label,
                                TypeName = expr.Output.Type != null ? TypeLabel(expr.Output.Type) : null
                            }
                        ];
                    }
                }

                return di;
            }).ToList()
        };

        return info;
    }

    // ── Execute a decision ──

    public ExecutionResult Execute(string name, ExecuteRequest request)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var definition = GetDefinition(name);

            if (!definition.Decisions.TryGetValue(request.DecisionName, out var targetDecision))
                return new ExecutionResult { Error = $"Decision '{request.DecisionName}' not found." };

            var ctx = DmnExecutionContextFactory.CreateExecutionContext(
                definition,
                opts => { opts.RecordSnapshots = true; });

            // Set input parameters
            foreach (var (inputName, jsonValue) in request.Inputs)
            {
                var normalizedName = DmnVariableDefinition.NormalizeVariableName(inputName);
                var variable = definition.Variables.GetValueOrDefault(normalizedName);
                var value = ConvertJsonElement(jsonValue, variable?.Type);
                ctx.WithInputParameter(normalizedName, value);
            }

            var result = ctx.ExecuteDecision(targetDecision);
            sw.Stop();

            var executionResult = new ExecutionResult
            {
                HasResult = result.HasResult,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };

            // Extract results
            if (result.HasResult)
            {
                foreach (var singleResult in result.Results)
                {
                    var sr = new SingleResult();

                    foreach (var variable in singleResult.Variables)
                    {
                        sr.Outputs[variable.Name] = new OutputValue
                        {
                            Value = variable.Value,
                            TypeName = variable.Type != null ? TypeLabel(variable.Type) : null
                        };
                    }

                    foreach (var hitRule in singleResult.HitRules)
                    {
                        sr.HitRules.Add(new HitRuleInfo
                        {
                            Index = hitRule.Index,
                            Name = hitRule.Name
                        });
                    }

                    executionResult.Results.Add(sr);
                }
            }

            // Extract execution trace from snapshots
            var snapshots = ctx.Snapshots.Snapshots;
            for (var i = 1; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                var prevSnapshot = snapshots[i - 1];

                var step = new ExecutionStep
                {
                    DecisionName = snapshot.DecisionName
                };

                if (snapshot.Decision is DmnDecisionTable)
                    step.DecisionType = "table";
                else if (snapshot.Decision is DmnExpressionDecision)
                    step.DecisionType = "expression";

                // Hit rules from this step
                if (snapshot.DecisionResult?.HasResult == true)
                {
                    foreach (var sr in snapshot.DecisionResult.Results)
                    {
                        foreach (var hitRule in sr.HitRules)
                        {
                            step.HitRules.Add(new HitRuleInfo
                            {
                                Index = hitRule.Index,
                                Name = hitRule.Name
                            });
                        }
                    }
                }

                // Variable changes
                foreach (var variable in snapshot.Variables)
                {
                    if (variable.IsInputParameter) continue;
                    var prevVar = prevSnapshot[variable.Name];
                    if (!Equals(prevVar?.Value, variable.Value))
                    {
                        step.VariableChanges[variable.Name] = variable.Value;
                    }
                }

                executionResult.Steps.Add(step);
            }

            return executionResult;
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ExecutionResult
            {
                Error = ex.Message,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    // ── Run test cases ──

    public TestCaseResult RunTestCase(string name, TestCase testCase)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var request = new ExecuteRequest
            {
                DecisionName = testCase.DecisionName,
                Inputs = testCase.Inputs
            };

            var result = Execute(name, request);
            sw.Stop();

            if (result.Error != null)
            {
                return new TestCaseResult
                {
                    TestCaseId = testCase.Id,
                    TestCaseName = testCase.Name,
                    Status = "error",
                    Error = result.Error,
                    ExecutionTimeMs = sw.ElapsedMilliseconds
                };
            }

            var tcResult = new TestCaseResult
            {
                TestCaseId = testCase.Id,
                TestCaseName = testCase.Name,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };

            // Collect actual outputs from first result + upstream decision outputs
            if (result.HasResult && result.Results.Count > 0)
            {
                tcResult.ActualOutputs = result.Results[0].Outputs;
                tcResult.HitRules = result.Results[0].HitRules.Select(h => h.Index).ToList();

                // Include outputs from upstream decisions (execution steps)
                // so expected values for upstream outputs can be verified
                foreach (var step in result.Steps ?? [])
                {
                    foreach (var (varName, value) in step.VariableChanges)
                    {
                        if (!tcResult.ActualOutputs.ContainsKey(varName))
                        {
                            tcResult.ActualOutputs[varName] = new OutputValue
                            {
                                Value = value,
                                TypeName = value?.GetType() is { } t ? TypeLabel(t) : null
                            };
                        }
                    }
                }
            }

            // Compare against expected outputs
            if (testCase.ExpectedOutputs == null || testCase.ExpectedOutputs.Count == 0)
            {
                tcResult.Status = "pass"; // No expectations = pass
            }
            else
            {
                var allMatch = true;
                foreach (var (outputName, expectedJson) in testCase.ExpectedOutputs)
                {
                    // Skip empty/null expected values
                    if (expectedJson.ValueKind == JsonValueKind.Null ||
                        (expectedJson.ValueKind == JsonValueKind.String && string.IsNullOrEmpty(expectedJson.GetString())))
                        continue;

                    if (!tcResult.ActualOutputs.TryGetValue(outputName, out var actual) || actual.Value == null)
                    {
                        allMatch = false;
                        break;
                    }

                    if (!ValuesMatch(expectedJson, actual.Value))
                    {
                        allMatch = false;
                        break;
                    }
                }

                tcResult.Status = allMatch ? "pass" : "fail";
            }

            return tcResult;
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new TestCaseResult
            {
                TestCaseId = testCase.Id,
                TestCaseName = testCase.Name,
                Status = "error",
                Error = ex.Message,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    // ── Type conversion ──

    private static object ConvertJsonElement(JsonElement element, Type targetType)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.True: return true;
            case JsonValueKind.False: return false;
            case JsonValueKind.Null or JsonValueKind.Undefined: return null;

            case JsonValueKind.Number:
                if (targetType == typeof(int)) return element.GetInt32();
                if (targetType == typeof(long)) return element.GetInt64();
                if (targetType == typeof(double)) return element.GetDouble();
                if (targetType == typeof(decimal)) return element.GetDecimal();
                // Default for numbers: try int, then decimal
                if (element.TryGetInt32(out var intVal)) return intVal;
                return element.GetDecimal();

            case JsonValueKind.String:
                var str = element.GetString();
                if (str == null) return null;
                if (targetType == null) return InferValue(str);
                return ConvertStringToType(str, targetType);

            default:
                return element.ToString();
        }
    }

    internal static object ConvertStringToType(string value, Type targetType)
    {
        if (targetType == typeof(string)) return value;
        if (targetType == typeof(bool)) return bool.Parse(value);
        if (targetType == typeof(int)) return int.Parse(value, CultureInfo.InvariantCulture);
        if (targetType == typeof(long)) return long.Parse(value, CultureInfo.InvariantCulture);
        if (targetType == typeof(double)) return double.Parse(value, CultureInfo.InvariantCulture);
        if (targetType == typeof(decimal)) return decimal.Parse(value, CultureInfo.InvariantCulture);
        if (targetType == typeof(DateTime)) return DateTime.Parse(value, CultureInfo.InvariantCulture);
        if (targetType == typeof(DateOnly)) return DateOnly.Parse(value, CultureInfo.InvariantCulture);
        if (targetType == typeof(DateTimeOffset)) return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
        if (targetType == typeof(TimeSpan)) return TimeSpan.Parse(value, CultureInfo.InvariantCulture);
        return value;
    }

    internal static object InferValue(string value)
    {
        if (bool.TryParse(value, out var b)) return b;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n;
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d;
        return value;
    }

    private static bool ValuesMatch(JsonElement expected, object actual)
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.True: return actual is true;
            case JsonValueKind.False: return actual is false;
            case JsonValueKind.Null: return actual == null;

            case JsonValueKind.Number:
                if (actual is int i) return expected.TryGetInt32(out var ei) && i == ei;
                if (actual is long l) return expected.TryGetInt64(out var el) && l == el;
                if (actual is decimal dm) return expected.TryGetDecimal(out var edm) && dm == edm;
                if (actual is double db) return expected.TryGetDouble(out var edb) && Math.Abs(db - edb) < 0.0001;
                return false;

            case JsonValueKind.String:
                var expectedStr = expected.GetString();
                var actualStr = actual?.ToString();
                return string.Equals(expectedStr, actualStr, StringComparison.OrdinalIgnoreCase);

            default:
                return string.Equals(expected.ToString(), actual?.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Metadata extraction ──

    private static FileMetadata ExtractMetadata(string filePath, DmnModel model)
    {
        var metadata = new FileMetadata
        {
            DmnVersion = model.DmnVersion switch
            {
                DmnParser.DmnVersionEnum.V1_1 => "1.1",
                DmnParser.DmnVersionEnum.V1_3 => "1.3",
                DmnParser.DmnVersionEnum.V1_3ext => "1.3",
                DmnParser.DmnVersionEnum.V1_4 => "1.4",
                DmnParser.DmnVersionEnum.V1_5 => "1.5",
                _ => model.DmnVersion.ToString()
            }
        };

        // Parse the raw XML to extract <definitions> element attributes
        try
        {
            var xmlContent = File.ReadAllText(filePath);
            using var reader = XmlReader.Create(new StringReader(xmlContent));
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "definitions") continue;

                metadata.DefinitionName = reader.GetAttribute("name");
                metadata.Namespace = reader.GetAttribute("namespace");
                metadata.Exporter = reader.GetAttribute("exporter");
                metadata.ExporterVersion = reader.GetAttribute("exporterVersion");

                // Camunda modeler attributes (in modeler namespace)
                metadata.ExecutionPlatform = reader.GetAttribute("executionPlatform", "http://camunda.org/schema/modeler/1.0")
                                             ?? reader.GetAttribute("modeler:executionPlatform");
                metadata.ExecutionPlatformVersion = reader.GetAttribute("executionPlatformVersion", "http://camunda.org/schema/modeler/1.0")
                                                    ?? reader.GetAttribute("modeler:executionPlatformVersion");

                metadata.IsCamundaExport = metadata.Exporter != null &&
                                           metadata.Exporter.Contains("amunda", StringComparison.OrdinalIgnoreCase);
                break;
            }
        }
        catch
        {
            // Best-effort metadata extraction
        }

        return metadata;
    }

    // ── Helpers ──

    internal static string TypeLabel(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "integer";
        if (type == typeof(long)) return "long";
        if (type == typeof(double)) return "double";
        if (type == typeof(decimal)) return "number";
        if (type == typeof(bool)) return "boolean";
        if (type == typeof(DateTime)) return "date";
        if (type == typeof(DateOnly)) return "date";
        if (type == typeof(DateTimeOffset)) return "date and time";
        if (type == typeof(TimeSpan)) return "days and time duration";
        return type.Name;
    }
}
