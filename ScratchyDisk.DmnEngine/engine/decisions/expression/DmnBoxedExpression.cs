using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ScratchyDisk.DmnEngine.Engine.Execution;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Types;
using ScratchyDisk.DmnEngine.Parser.Dto;

namespace ScratchyDisk.DmnEngine.Engine.Decisions.Expression
{
    /// <summary>
    /// Runtime services a boxed expression needs from the surrounding DMN execution.
    /// Implemented by <see cref="Execution.Context.DmnExecutionContext"/>; kept as an
    /// interface so the boxed expression tree does not depend on the execution context.
    /// </summary>
    public interface IDmnBoxedExpressionHost
    {
        /// <summary>
        /// Parses and evaluates <paramref name="expression"/> in the given scope. The
        /// <paramref name="localNames"/> are needed for FEEL multi-word name resolution.
        /// </summary>
        object EvaluateBoxedExpression(string expression, FeelDialect dialect,
            FeelEvaluationContext scope, IReadOnlyCollection<string> localNames);

        /// <summary>
        /// Parses and evaluates simple unary tests against <paramref name="inputValue"/>.
        /// </summary>
        bool EvaluateBoxedExpressionUnaryTest(string expression, object inputValue, FeelDialect dialect,
            FeelEvaluationContext scope, IReadOnlyCollection<string> localNames);
    }

    /// <summary>
    /// Evaluation state of a boxed expression: the FEEL scope chain plus the local names
    /// that have to be visible to the FEEL parser for multi-word name resolution.
    /// </summary>
    public sealed class DmnBoxedExpressionRuntime
    {
        private readonly IDmnBoxedExpressionHost host;
        private readonly List<string> localNames;

        /// <summary>Current FEEL scope.</summary>
        public FeelEvaluationContext Scope { get; }

        /// <summary>Identifier of the current execution run.</summary>
        public string ExecutionId { get; }

        internal DmnBoxedExpressionRuntime(IDmnBoxedExpressionHost host, FeelEvaluationContext scope,
            IEnumerable<string> localNames, string executionId)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.localNames = localNames?.Distinct(StringComparer.Ordinal).ToList() ?? new List<string>();
            ExecutionId = executionId;
        }

        /// <summary>Evaluates <paramref name="expression"/> in the current runtime.</summary>
        public object Evaluate(DmnBoxedExpression expression)
        {
            if (expression == null) return null;
            return expression.Evaluate(this);
        }

        /// <summary>
        /// Creates a child runtime with a child FEEL scope and additional local names.
        /// </summary>
        public DmnBoxedExpressionRuntime CreateChild(FeelEvaluationContext scope, params string[] extraNames)
        {
            var names = localNames;
            if (extraNames != null && extraNames.Length > 0)
            {
                names = new List<string>(localNames);
                foreach (var name in extraNames)
                {
                    if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name)) names.Add(name);
                }
            }
            return new DmnBoxedExpressionRuntime(host, scope ?? Scope, names, ExecutionId);
        }

        /// <summary>Evaluates a FEEL expression text in the current scope.</summary>
        public object EvaluateText(string expression, FeelDialect dialect)
        {
            return host.EvaluateBoxedExpression(expression, dialect, Scope, localNames);
        }

        /// <summary>Evaluates FEEL unary tests in the current scope.</summary>
        public bool EvaluateUnaryTest(string expression, object inputValue, FeelDialect dialect)
        {
            return host.EvaluateBoxedExpressionUnaryTest(expression, inputValue, dialect, Scope, localNames);
        }
    }

    /// <summary>
    /// Executable boxed expression. Boxed expressions are the structured decision logic
    /// DMN allows beside literal expressions: contexts, lists, relations, invocations,
    /// nested function definitions and decision tables.
    /// </summary>
    public abstract class DmnBoxedExpression
    {
        /// <summary>Evaluates the expression and returns its FEEL value.</summary>
        public abstract object Evaluate(DmnBoxedExpressionRuntime runtime);
    }

    /// <summary>Literal (textual FEEL) boxed expression.</summary>
    public sealed class DmnLiteralBoxedExpression : DmnBoxedExpression
    {
        /// <summary>Expression text.</summary>
        public string Text { get; }

        /// <summary>Expression dialect.</summary>
        public FeelDialect Dialect { get; }

        public DmnLiteralBoxedExpression(string text, FeelDialect dialect = FeelDialect.Feel)
        {
            Text = text;
            Dialect = dialect;
        }

        /// <inheritdoc />
        public override object Evaluate(DmnBoxedExpressionRuntime runtime)
        {
            if (string.IsNullOrWhiteSpace(Text)) return null;
            return runtime.EvaluateText(Text, Dialect);
        }

        /// <inheritdoc />
        public override string ToString() => Text;
    }

    /// <summary>One entry of a boxed context. An entry without a name provides the context value.</summary>
    public sealed class DmnContextEntryExpression
    {
        /// <summary>Entry name; null for the entry that provides the context value.</summary>
        public string Name { get; }

        /// <summary>Entry value.</summary>
        public DmnBoxedExpression Expression { get; }

        /// <summary>
        /// Declared FEEL type of the entry, or null when the entry is untyped or its
        /// type takes no part in the implicit conversions. A declared type converts
        /// the value at this boundary: a single value becomes a one-item list (and
        /// back) and a date becomes a date and time.
        /// </summary>
        public string DeclaredType { get; }

        public DmnContextEntryExpression(string name, DmnBoxedExpression expression, string declaredType = null)
        {
            Name = string.IsNullOrWhiteSpace(name) ? null : name;
            Expression = expression ?? new DmnLiteralBoxedExpression(null);
            DeclaredType = string.IsNullOrWhiteSpace(declaredType) ? null : declaredType.Trim();
        }
    }

    /// <summary>Boxed context expression.</summary>
    public sealed class DmnContextBoxedExpression : DmnBoxedExpression
    {
        /// <summary>Context entries in declaration order.</summary>
        public IReadOnlyList<DmnContextEntryExpression> Entries { get; }

        public DmnContextBoxedExpression(IReadOnlyList<DmnContextEntryExpression> entries)
        {
            Entries = entries ?? new List<DmnContextEntryExpression>();
        }

        /// <inheritdoc />
        public override object Evaluate(DmnBoxedExpressionRuntime runtime)
        {
            // DMN: the last entry may omit the variable; that entry then provides the
            // value of the whole context. Otherwise the context is the result.
            var entryNames = Entries
                .Where(e => e.Name != null)
                .Select(e => e.Name)
                .ToArray();

            var childScope = runtime.Scope.CreateChildScope();
            var child = runtime.CreateChild(childScope, entryNames);

            var result = new FeelContext();
            foreach (var entry in Entries)
            {
                var value = CoerceToDeclaredType(child.Evaluate(entry.Expression), entry.DeclaredType, child);
                if (entry.Name == null) return value;
                result[entry.Name] = value;
                // Context entries are visible to the entries that follow them.
                childScope.SetVariable(entry.Name, value);
            }
            return result;
        }

        /// <summary>
        /// Applies the FEEL implicit conversions at the context entry boundary. An
        /// untyped entry keeps its value; a value that does not convert becomes null.
        /// </summary>
        private static object CoerceToDeclaredType(object value, string declaredType,
            DmnBoxedExpressionRuntime runtime)
        {
            if (declaredType == null) return value;
            return FeelTypeConformance.TryCoerce(value, declaredType, runtime.Scope.TypeAliases, out var converted)
                ? converted
                : null;
        }
    }

    /// <summary>Boxed list expression.</summary>
    public sealed class DmnListBoxedExpression : DmnBoxedExpression
    {
        /// <summary>List items.</summary>
        public IReadOnlyList<DmnBoxedExpression> Items { get; }

        public DmnListBoxedExpression(IReadOnlyList<DmnBoxedExpression> items)
        {
            Items = items ?? new List<DmnBoxedExpression>();
        }

        /// <inheritdoc />
        public override object Evaluate(DmnBoxedExpressionRuntime runtime)
        {
            var result = new List<object>(Items.Count);
            foreach (var item in Items)
                result.Add(runtime.Evaluate(item));
            return result;
        }
    }

    /// <summary>Boxed relation expression: a list of contexts with the relation columns as keys.</summary>
    public sealed class DmnRelationBoxedExpression : DmnBoxedExpression
    {
        /// <summary>Column names in declaration order.</summary>
        public IReadOnlyList<string> Columns { get; }

        /// <summary>Relation rows; each row holds one expression per column.</summary>
        public IReadOnlyList<IReadOnlyList<DmnBoxedExpression>> Rows { get; }

        public DmnRelationBoxedExpression(IReadOnlyList<string> columns,
            IReadOnlyList<IReadOnlyList<DmnBoxedExpression>> rows)
        {
            Columns = columns ?? new List<string>();
            Rows = rows ?? new List<IReadOnlyList<DmnBoxedExpression>>();
        }

        /// <inheritdoc />
        public override object Evaluate(DmnBoxedExpressionRuntime runtime)
        {
            var result = new List<object>(Rows.Count);
            foreach (var row in Rows)
            {
                var rowScope = runtime.Scope.CreateChildScope();
                var rowRuntime = runtime.CreateChild(rowScope);
                var context = new FeelContext();
                for (var i = 0; i < Columns.Count && i < row.Count; i++)
                    context[Columns[i]] = rowRuntime.Evaluate(row[i]);
                result.Add(context);
            }
            return result;
        }
    }

    /// <summary>Named argument of a boxed invocation.</summary>
    public sealed class DmnInvocationBindingExpression
    {
        /// <summary>Parameter name.</summary>
        public string ParameterName { get; }

        /// <summary>Argument value.</summary>
        public DmnBoxedExpression Value { get; }

        public DmnInvocationBindingExpression(string parameterName, DmnBoxedExpression value)
        {
            ParameterName = parameterName ?? "";
            Value = value ?? new DmnLiteralBoxedExpression(null);
        }
    }

    /// <summary>Boxed invocation expression.</summary>
    public sealed class DmnInvocationBoxedExpression : DmnBoxedExpression
    {
        /// <summary>Expression that provides the function to invoke.</summary>
        public DmnBoxedExpression Function { get; }

        /// <summary>Named argument bindings.</summary>
        public IReadOnlyList<DmnInvocationBindingExpression> Bindings { get; }

        public DmnInvocationBoxedExpression(DmnBoxedExpression function,
            IReadOnlyList<DmnInvocationBindingExpression> bindings)
        {
            Function = function ?? new DmnLiteralBoxedExpression(null);
            Bindings = bindings ?? new List<DmnInvocationBindingExpression>();
        }

        /// <inheritdoc />
        public override object Evaluate(DmnBoxedExpressionRuntime runtime)
        {
            var function = runtime.Evaluate(Function);
            if (function is FeelFunction feelFunction)
            {
                var named = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var binding in Bindings)
                    named[binding.ParameterName] = runtime.Evaluate(binding.Value);
                return feelFunction.InvokeNamed(named);
            }

            // Function values resolved by name (built-in functions, external function
            // stubs) are not FEEL values; fall back to a textual call when the binding
            // can be expressed in FEEL text.
            if (Function is DmnLiteralBoxedExpression literal
                && !string.IsNullOrWhiteSpace(literal.Text)
                && CanBuildTextCall(Bindings))
            {
                return runtime.EvaluateText(BuildTextCall(literal, Bindings), literal.Dialect);
            }

            throw new DmnEvaluationException(
                $"Boxed invocation target '{Describe(Function)}' is not a function");
        }

        private static bool CanBuildTextCall(IReadOnlyList<DmnInvocationBindingExpression> bindings)
        {
            foreach (var binding in bindings)
            {
                if (binding.Value is not DmnLiteralBoxedExpression literal
                    || string.IsNullOrWhiteSpace(literal.Text))
                    return false;
                if (!string.IsNullOrWhiteSpace(binding.ParameterName) && !IsFeelName(binding.ParameterName))
                    return false;
            }
            return true;
        }

        private static string BuildTextCall(DmnLiteralBoxedExpression function,
            IReadOnlyList<DmnInvocationBindingExpression> bindings)
        {
            var args = bindings.Select(binding =>
            {
                var value = ((DmnLiteralBoxedExpression)binding.Value).Text;
                return string.IsNullOrWhiteSpace(binding.ParameterName)
                    ? value
                    : $"{binding.ParameterName}: {value}";
            });
            return $"{function.Text}({string.Join(", ", args)})";
        }

        private static bool IsFeelName(string name)
        {
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == ' ') continue;
                return false;
            }
            return name.Length > 0;
        }

        private static string Describe(DmnBoxedExpression expression)
        {
            return expression is DmnLiteralBoxedExpression literal ? literal.Text : expression?.GetType().Name;
        }
    }

    /// <summary>Boxed function definition (a context entry that holds a FEEL function).</summary>
    public sealed class DmnFunctionDefinitionBoxedExpression : DmnBoxedExpression
    {
        /// <summary>Formal parameter names, in declaration order.</summary>
        public IReadOnlyList<string> ParameterNames { get; }

        /// <summary>Function body.</summary>
        public DmnBoxedExpression Body { get; }

        public DmnFunctionDefinitionBoxedExpression(IReadOnlyList<string> parameterNames, DmnBoxedExpression body)
        {
            ParameterNames = parameterNames ?? new List<string>();
            Body = body ?? new DmnLiteralBoxedExpression(null);
        }

        /// <inheritdoc />
        public override object Evaluate(DmnBoxedExpressionRuntime runtime)
        {
            var parameters = ParameterNames;
            return new FeelFunction("anonymous", parameters, args =>
            {
                var childScope = runtime.Scope.CreateChildScope();
                var child = runtime.CreateChild(childScope, parameters.ToArray());
                for (var i = 0; i < parameters.Count; i++)
                    childScope.SetVariable(parameters[i], i < args.Length ? args[i] : null);
                return child.Evaluate(Body);
            });
        }
    }

    /// <summary>Input of a nested (context or business knowledge model) decision table.</summary>
    public sealed class DmnNestedDecisionTableInput
    {
        /// <summary>Expression that provides the input value.</summary>
        public string Expression { get; set; }

        public DmnNestedDecisionTableInput(string expression)
        {
            Expression = expression;
        }
    }

    /// <summary>Output of a nested decision table.</summary>
    public sealed class DmnNestedDecisionTableOutput
    {
        /// <summary>Output name (used as the context key when the table has several outputs).</summary>
        public string Name { get; set; }

        /// <summary>Allowed output values, used by the PRIORITY and OUTPUT ORDER hit policies.</summary>
        public IReadOnlyList<string> AllowedValues { get; set; } = new List<string>();

        /// <summary>Default output expression used when no rule matches.</summary>
        public string DefaultExpression { get; set; }
    }

    /// <summary>Rule of a nested decision table.</summary>
    public sealed class DmnNestedDecisionTableRule
    {
        /// <summary>Unary tests per input, in input order.</summary>
        public IReadOnlyList<string> InputEntries { get; set; } = new List<string>();

        /// <summary>Output expressions per output, in output order.</summary>
        public IReadOnlyList<string> OutputEntries { get; set; } = new List<string>();
    }

    /// <summary>
    /// Decision table used as a boxed expression (inside a context entry or a business
    /// knowledge model body). It is evaluated against the FEEL scope of its host, so its
    /// input expressions see the enclosing context entries and formal parameters.
    /// </summary>
    public sealed class DmnNestedDecisionTableExpression : DmnBoxedExpression
    {
        /// <summary>Table hit policy.</summary>
        public HitPolicyEnum HitPolicy { get; }

        /// <summary>Aggregation of the COLLECT hit policy.</summary>
        public CollectHitPolicyAggregationEnum Aggregation { get; }

        /// <summary>Table inputs.</summary>
        public IReadOnlyList<DmnNestedDecisionTableInput> Inputs { get; }

        /// <summary>Table outputs.</summary>
        public IReadOnlyList<DmnNestedDecisionTableOutput> Outputs { get; }

        /// <summary>Table rules.</summary>
        public IReadOnlyList<DmnNestedDecisionTableRule> Rules { get; }

        /// <summary>Dialect used by the table expressions.</summary>
        public FeelDialect Dialect { get; }

        public DmnNestedDecisionTableExpression(
            HitPolicyEnum hitPolicy,
            CollectHitPolicyAggregationEnum aggregation,
            IReadOnlyList<DmnNestedDecisionTableInput> inputs,
            IReadOnlyList<DmnNestedDecisionTableOutput> outputs,
            IReadOnlyList<DmnNestedDecisionTableRule> rules,
            FeelDialect dialect = FeelDialect.Feel)
        {
            HitPolicy = hitPolicy;
            Aggregation = aggregation;
            Inputs = inputs ?? new List<DmnNestedDecisionTableInput>();
            Outputs = outputs ?? new List<DmnNestedDecisionTableOutput>();
            Rules = rules ?? new List<DmnNestedDecisionTableRule>();
            Dialect = dialect;
        }

        /// <inheritdoc />
        public override object Evaluate(DmnBoxedExpressionRuntime runtime)
        {
            // Evaluate the input expressions once; they define the table's input vector.
            // Note: the input values element is a constraint on the input data, not a
            // rule condition, so it is not enforced here (the enclosing model declares it).
            var inputValues = new List<object>(Inputs.Count);
            foreach (var input in Inputs)
            {
                inputValues.Add(string.IsNullOrWhiteSpace(input.Expression)
                    ? null
                    : runtime.EvaluateText(input.Expression, Dialect));
            }

            var positiveRules = new List<DmnNestedDecisionTableRule>();
            foreach (var rule in Rules)
            {
                if (MatchesRule(rule, inputValues, runtime)) positiveRules.Add(rule);
            }

            if (positiveRules.Count == 0)
            {
                return DefaultResult(runtime);
            }

            var outputsByRule = new List<object[]>(positiveRules.Count);
            foreach (var rule in positiveRules)
            {
                var values = new object[Outputs.Count];
                for (var i = 0; i < Outputs.Count; i++)
                {
                    var text = i < rule.OutputEntries.Count ? rule.OutputEntries[i] : null;
                    values[i] = string.IsNullOrWhiteSpace(text) ? null : runtime.EvaluateText(text, Dialect);
                }
                outputsByRule.Add(values);
            }

            switch (HitPolicy)
            {
                case HitPolicyEnum.Unique:
                    // Overlapping rules are an error; report rather than silently
                    // returning the first one.
                    if (positiveRules.Count > 1)
                        throw new DmnEvaluationException(
                            $"UNIQUE hit policy violation - {positiveRules.Count} matches");
                    return BuildResult(outputsByRule[0]);

                case HitPolicyEnum.First:
                    return BuildResult(outputsByRule[0]);

                case HitPolicyEnum.Any:
                {
                    // Any number of rules may match, but all of them must agree on
                    // every output; otherwise the hit policy is violated.
                    for (var rule = 1; rule < outputsByRule.Count; rule++)
                    {
                        for (var output = 0; output < Outputs.Count; output++)
                        {
                            if (FeelValueComparer.Equals(outputsByRule[0][output], outputsByRule[rule][output]) == true)
                                continue;

                            throw new DmnEvaluationException(
                                $"ANY hit policy violation - rule outputs disagree on output {output + 1}");
                        }
                    }
                    return BuildResult(outputsByRule[0]);
                }

                case HitPolicyEnum.Priority:
                    return BuildResult(outputsByRule[PriorityOrder(outputsByRule)[0]]);

                case HitPolicyEnum.OutputOrder:
                    return BuildMultiResult(outputsByRule, PriorityOrder(outputsByRule));

                case HitPolicyEnum.RuleOrder:
                {
                    var order = Enumerable.Range(0, outputsByRule.Count).ToList();
                    return BuildMultiResult(outputsByRule, order);
                }

                case HitPolicyEnum.Collect:
                    return BuildCollectResult(outputsByRule);

                default:
                    return BuildResult(outputsByRule[0]);
            }
        }

        private bool MatchesRule(DmnNestedDecisionTableRule rule, IReadOnlyList<object> inputValues,
            DmnBoxedExpressionRuntime runtime)
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var unaryTest = i < rule.InputEntries.Count ? rule.InputEntries[i] : null;
                if (string.IsNullOrWhiteSpace(unaryTest) || unaryTest.Trim() == "-") continue;

                var inputValue = i < inputValues.Count ? inputValues[i] : null;
                if (!runtime.EvaluateUnaryTest(unaryTest.Trim(), inputValue, Dialect)) return false;
            }
            return true;
        }

        private object DefaultResult(DmnBoxedExpressionRuntime runtime)
        {
            if (Outputs.Count == 0) return null;

            // A COLLECT/COUNT table returns the number of hits, which is zero.
            if (HitPolicy == HitPolicyEnum.Collect
                && Aggregation == CollectHitPolicyAggregationEnum.Count)
                return 0m;

            if (Outputs.Count == 1)
            {
                var output = Outputs[0];
                return string.IsNullOrWhiteSpace(output.DefaultExpression)
                    ? null
                    : runtime.EvaluateText(output.DefaultExpression, Dialect);
            }

            var context = new FeelContext();
            foreach (var output in Outputs)
            {
                context[output.Name] = string.IsNullOrWhiteSpace(output.DefaultExpression)
                    ? null
                    : runtime.EvaluateText(output.DefaultExpression, Dialect);
            }
            return context;
        }

        private object BuildResult(object[] values)
        {
            if (Outputs.Count == 0) return null;
            if (Outputs.Count == 1) return values.Length > 0 ? values[0] : null;

            var context = new FeelContext();
            for (var i = 0; i < Outputs.Count; i++)
                context[Outputs[i].Name] = i < values.Length ? values[i] : null;
            return context;
        }

        private object BuildMultiResult(IReadOnlyList<object[]> outputsByRule, IReadOnlyList<int> order)
        {
            var result = new List<object>(order.Count);
            foreach (var index in order)
                result.Add(BuildResult(outputsByRule[index]));
            return result;
        }

        private object BuildCollectResult(IReadOnlyList<object[]> outputsByRule)
        {
            if (Aggregation == CollectHitPolicyAggregationEnum.List)
            {
                var list = new List<object>(outputsByRule.Count);
                foreach (var values in outputsByRule) list.Add(BuildResult(values));
                return list;
            }

            var collected = outputsByRule
                .Select(values => values.Length > 0 ? values[0] : null)
                .ToList();

            switch (Aggregation)
            {
                case CollectHitPolicyAggregationEnum.Count:
                    return (decimal)collected.Count;
                case CollectHitPolicyAggregationEnum.Sum:
                    return collected.Aggregate(0m, (sum, value) => sum + ToNumber(value));
                case CollectHitPolicyAggregationEnum.Min:
                    return collected.Count == 0
                        ? null
                        : collected.Select(ToNumber).Min();
                case CollectHitPolicyAggregationEnum.Max:
                    return collected.Count == 0
                        ? null
                        : collected.Select(ToNumber).Max();
                default:
                    return collected;
            }
        }

        /// <summary>
        /// Orders rules by the priority of their output values (allowed output values,
        /// in decreasing order of priority). Rules without a known priority go last.
        /// </summary>
        private IReadOnlyList<int> PriorityOrder(IReadOnlyList<object[]> outputsByRule)
        {
            var priorities = new int[outputsByRule.Count];
            for (var i = 0; i < outputsByRule.Count; i++)
                priorities[i] = PriorityOf(outputsByRule[i]);

            return Enumerable.Range(0, outputsByRule.Count)
                .OrderBy(i => priorities[i])
                .ThenBy(i => i)
                .ToList();
        }

        private int PriorityOf(object[] values)
        {
            for (var i = 0; i < Outputs.Count && i < values.Length; i++)
            {
                var allowed = Outputs[i].AllowedValues;
                if (allowed == null || allowed.Count == 0) continue;

                var value = values[i];
                for (var priority = 0; priority < allowed.Count; priority++)
                {
                    if (MatchesAllowedValue(allowed[priority], value)) return priority;
                }
                return int.MaxValue;
            }
            return int.MaxValue;
        }

        /// <summary>
        /// Compares an output value with one entry of the allowed output values list.
        /// The list holds FEEL literal text (quotes already removed for strings).
        /// </summary>
        private static bool MatchesAllowedValue(string allowed, object value)
        {
            if (allowed == null) return value == null;
            if (value == null) return string.Equals(allowed.Trim(), "null", StringComparison.OrdinalIgnoreCase);

            var text = allowed.Trim();
            if (value is string s) return string.Equals(text, s, StringComparison.Ordinal);

            if (value is bool b)
                return string.Equals(text, b ? "true" : "false", StringComparison.OrdinalIgnoreCase);

            if (FeelValueComparer.IsNumeric(value)
                && decimal.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var number))
                return FeelValueComparer.ToDecimal(value) == number;

            return string.Equals(text, Convert.ToString(value, CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }

        private static decimal ToNumber(object value)
        {
            if (value == null) return 0m;
            if (value is decimal d) return d;
            if (value is bool b) return b ? 1m : 0m;
            try { return Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return 0m; }
        }
    }

    /// <summary>Boxed conditional expression (if/then/else).</summary>
    public sealed class DmnConditionalBoxedExpression : DmnBoxedExpression
    {
        /// <summary>Condition of the conditional.</summary>
        public DmnBoxedExpression If { get; }

        /// <summary>Value when the condition is true.</summary>
        public DmnBoxedExpression Then { get; }

        /// <summary>Value when the condition is false.</summary>
        public DmnBoxedExpression Else { get; }

        public DmnConditionalBoxedExpression(DmnBoxedExpression ifExpression,
            DmnBoxedExpression thenExpression, DmnBoxedExpression elseExpression)
        {
            If = ifExpression ?? new DmnLiteralBoxedExpression(null);
            Then = thenExpression ?? new DmnLiteralBoxedExpression(null);
            Else = elseExpression ?? new DmnLiteralBoxedExpression(null);
        }

        /// <inheritdoc />
        public override object Evaluate(DmnBoxedExpressionRuntime runtime)
        {
            // DMN: a condition that is not a boolean makes the whole conditional null.
            if (runtime.Evaluate(If) is not bool condition) return null;
            return runtime.Evaluate(condition ? Then : Else);
        }
    }

    /// <summary>Boxed for expression: maps every value of <see cref="In"/> with <see cref="Return"/>.</summary>
    public sealed class DmnForBoxedExpression : DmnBoxedExpression
    {
        /// <summary>Name the iteration variable is bound to.</summary>
        public string IteratorVariable { get; }

        /// <summary>Expression that provides the values to iterate.</summary>
        public DmnBoxedExpression In { get; }

        /// <summary>Expression evaluated for every value; its result maps to the iteration variable.</summary>
        public DmnBoxedExpression Return { get; }

        public DmnForBoxedExpression(string iteratorVariable, DmnBoxedExpression inExpression,
            DmnBoxedExpression returnExpression)
        {
            IteratorVariable = string.IsNullOrWhiteSpace(iteratorVariable) ? "item" : iteratorVariable;
            In = inExpression ?? new DmnLiteralBoxedExpression(null);
            Return = returnExpression ?? new DmnLiteralBoxedExpression(null);
        }

        /// <inheritdoc />
        public override object Evaluate(DmnBoxedExpressionRuntime runtime)
        {
            var items = DmnBoxedExpressionIteration.ToItems(runtime.Evaluate(In));
            if (items == null) return null;

            var result = new List<object>(items.Count);
            foreach (var item in items)
            {
                var scope = runtime.Scope.CreateChildScope();
                scope.SetVariable(IteratorVariable, item);
                result.Add(runtime.CreateChild(scope, IteratorVariable).Evaluate(Return));
            }
            return result;
        }

        /// <inheritdoc />
        public override string ToString() => $"for {IteratorVariable} in ... return ...";
    }

    /// <summary>Base of the boxed quantified expressions (some/every).</summary>
    public abstract class DmnQuantifiedBoxedExpression : DmnBoxedExpression
    {
        /// <summary>Name the iteration variable is bound to.</summary>
        public string IteratorVariable { get; }

        /// <summary>Expression that provides the values to iterate.</summary>
        public DmnBoxedExpression In { get; }

        /// <summary>Predicate evaluated for every value.</summary>
        public DmnBoxedExpression Satisfies { get; }

        protected DmnQuantifiedBoxedExpression(string iteratorVariable, DmnBoxedExpression inExpression,
            DmnBoxedExpression satisfies)
        {
            IteratorVariable = string.IsNullOrWhiteSpace(iteratorVariable) ? "item" : iteratorVariable;
            In = inExpression ?? new DmnLiteralBoxedExpression(null);
            Satisfies = satisfies ?? new DmnLiteralBoxedExpression(null);
        }

        /// <summary>True when the quantifier needs at least one satisfying value.</summary>
        protected abstract bool RequiresOne { get; }

        /// <inheritdoc />
        public override object Evaluate(DmnBoxedExpressionRuntime runtime)
        {
            var items = DmnBoxedExpressionIteration.ToItems(runtime.Evaluate(In));
            if (items == null) return null;

            var any = false;
            foreach (var item in items)
            {
                // Iterate over the values under the iterator name declared by this node.
                var scope = runtime.Scope.CreateChildScope();
                scope.SetVariable(IteratorVariable, item);
                var test = runtime.CreateChild(scope, IteratorVariable).Evaluate(Satisfies);

                // DMN: a predicate that is not a boolean makes the whole expression null.
                if (test is not bool satisfied) return null;

                if (satisfied) any = true;
                else if (!RequiresOne) return false;
            }

            return RequiresOne ? any : true;
        }
    }

    /// <summary>Boxed some expression: at least one value satisfies the predicate.</summary>
    public sealed class DmnSomeBoxedExpression : DmnQuantifiedBoxedExpression
    {
        public DmnSomeBoxedExpression(string iteratorVariable, DmnBoxedExpression inExpression,
            DmnBoxedExpression satisfies)
            : base(iteratorVariable, inExpression, satisfies)
        {
        }

        /// <inheritdoc />
        protected override bool RequiresOne => true;
    }

    /// <summary>Boxed every expression: all values satisfy the predicate.</summary>
    public sealed class DmnEveryBoxedExpression : DmnQuantifiedBoxedExpression
    {
        public DmnEveryBoxedExpression(string iteratorVariable, DmnBoxedExpression inExpression,
            DmnBoxedExpression satisfies)
            : base(iteratorVariable, inExpression, satisfies)
        {
        }

        /// <inheritdoc />
        protected override bool RequiresOne => false;
    }

    /// <summary>Boxed filter expression: the values of <see cref="In"/> matching <see cref="Match"/>.</summary>
    public sealed class DmnFilterBoxedExpression : DmnBoxedExpression
    {
        /// <summary>Name the current list element is bound to (the implicit filter variable).</summary>
        public const string ItemName = "item";

        /// <summary>Expression that provides the list to filter.</summary>
        public DmnBoxedExpression In { get; }

        /// <summary>Predicate evaluated for every value.</summary>
        public DmnBoxedExpression Match { get; }

        public DmnFilterBoxedExpression(DmnBoxedExpression inExpression, DmnBoxedExpression match)
        {
            In = inExpression ?? new DmnLiteralBoxedExpression(null);
            Match = match ?? new DmnLiteralBoxedExpression(null);
        }

        /// <inheritdoc />
        public override object Evaluate(DmnBoxedExpressionRuntime runtime)
        {
            var items = DmnBoxedExpressionIteration.ToItems(runtime.Evaluate(In));
            if (items == null) return null;

            var result = new List<object>();
            foreach (var item in items)
            {
                var scope = runtime.Scope.CreateChildScope();
                scope.SetVariable(ItemName, item);
                // The implicit filter variable is also available as '?'.
                scope.InputValue = item;

                var match = runtime.CreateChild(scope, ItemName).Evaluate(Match);
                // DMN: a match that is not a boolean makes the whole filter null.
                if (match is not bool keep) return null;
                if (keep) result.Add(item);
            }
            return result;
        }
    }

    /// <summary>
    /// Converts the value of a boxed construct's 'in' expression to the values it iterates.
    /// Lists and ranges are iterable; any other value (including a single value) is not,
    /// because DMN does not coerce a single value to a singleton list here.
    /// </summary>
    internal static class DmnBoxedExpressionIteration
    {
        /// <summary>Returns the values of <paramref name="value"/>, or null when it is not iterable.</summary>
        public static IReadOnlyList<object> ToItems(object value)
        {
            switch (value)
            {
                case null:
                case string:
                    return null;
                case List<object> list:
                    return list;
                case FeelRange range:
                    return RangeItems(range);
                case System.Collections.IList list:
                    return list.Cast<object>().ToList();
                default:
                    return null;
            }
        }

        private static IReadOnlyList<object> RangeItems(FeelRange range)
        {
            return FeelIteration.Items(range);
        }
    }
}
