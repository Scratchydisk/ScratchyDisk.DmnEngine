using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NLog;
using ScratchyDisk.DmnEngine.Engine.Decisions;
using ScratchyDisk.DmnEngine.Engine.Decisions.Expression;
using ScratchyDisk.DmnEngine.Utils;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution.Result;
using System.Text.RegularExpressions;
using ScratchyDisk.DmnEngine.Feel;
using ScratchyDisk.DmnEngine.Feel.Ast;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Parsing;
using ScratchyDisk.DmnEngine.Feel.Types;
using ScratchyDisk.DmnEngine.Parser;

namespace ScratchyDisk.DmnEngine.Engine.Execution.Context
{
    /// <summary>
    /// Context where is the DMN model executed
    /// </summary>
    public class DmnExecutionContext : IDmnBoxedExpressionHost
    {
        /// <summary>
        /// Logger
        /// </summary>
        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Parsed (pre-processed) expressions cache (Global and Definition)
        /// </summary>
        protected static readonly ConcurrentDictionary<string, FeelAstNode> ParsedExpressionsCache =
            new ConcurrentDictionary<string, FeelAstNode>();

        /// <summary>
        /// Parsed (pre-processed) expressions cache (Context and Execution)
        /// </summary>
        protected readonly ConcurrentDictionary<string, FeelAstNode> ParsedExpressionsInstanceCache =
            new ConcurrentDictionary<string, FeelAstNode>();

        /// <summary>
        /// Shared FEEL engine instance
        /// </summary>
        protected static readonly FeelEngine FeelEngine = new();
        /// <summary>
        /// Returns the FEEL engine used by this context. Override to inject a dialect-aware engine in tests.
        /// </summary>
        protected virtual FeelEngine GetFeelEngine() => FeelEngine;

        /// <summary>
        /// Unique identifier of the execution context (set at CTOR)
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// DMN Model definition
        /// </summary>
        public DmnDefinition Definition { get; }

        /// <summary>
        /// Variables used while executing the DMN model - can be used within the Decision Tables and/or Expressions
        /// In general, it holds the Input Data of DMN model and outputs from Decision Tables and/or Expressions
        /// </summary>
        public IReadOnlyDictionary<string, DmnExecutionVariable> Variables { get; }

        /// <summary>
        /// Dictionary of available decisions by name
        /// </summary>
        public IReadOnlyDictionary<string, IDmnDecision> Decisions { get; }


        /// <summary>
        /// Snapshots of the context state during the execution
        /// </summary>
        public DmnExecutionSnapshots Snapshots { get; } = new DmnExecutionSnapshots();
        private readonly List<DmnDiagnostic> diagnostics = new();
        /// <summary>
        /// Semantic errors collected during lenient execution.
        /// </summary>
        public IReadOnlyList<DmnDiagnostic> Diagnostics => diagnostics;

        /// <summary>Aliases active for the decision currently being evaluated.</summary>
        internal IReadOnlyDictionary<string, string> CurrentNameAliases { get; set; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly HashSet<string> providedDecisions = new(StringComparer.Ordinal);

        internal IReadOnlyCollection<string> ProvidedDecisions
        {
            get => providedDecisions;
            set
            {
                providedDecisions.Clear();
                if (value != null) providedDecisions.UnionWith(value);
            }
        }

        internal void AddProvidedDecision(string decisionName)
        {
            if (!string.IsNullOrWhiteSpace(decisionName)) providedDecisions.Add(decisionName);
        }

        internal bool IsProvidedDecision(string decisionName) => providedDecisions.Contains(decisionName);
        /// <summary>
        /// Adds a semantic error to the execution diagnostics.
        /// </summary>
        internal void AddDiagnostic(string decisionName, Exception exception)
        {
            diagnostics.Add(new DmnDiagnostic(decisionName, exception.Message, exception.GetType().Name));
        }

        /// <summary>
        /// Execution context options
        /// </summary>
        private readonly DmnExecutionContextOptions options = new DmnExecutionContextOptions();

        /// <summary>
        /// Execution context options
        /// </summary>
        public IDmnExecutionContextOptions Options => options;

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="definition">DMN Model definition</param>
        /// <param name="variables">Variables used while executing the DMN model</param>
        /// <param name="decisions">Dictionary of available decisions by name</param>
        /// <param name="configure">Optional configuration action</param>
        /// <exception cref="ArgumentNullException">Any of the parameters is null</exception>
        public DmnExecutionContext(
            DmnDefinition definition,
            IReadOnlyDictionary<string, DmnExecutionVariable> variables,
            IReadOnlyDictionary<string, IDmnDecision> decisions,
            Action<DmnExecutionContextOptions> configure = null)
        {
            Id = Guid.NewGuid().ToString();

            Definition = definition ?? throw Logger.Fatal<ArgumentNullException>($"{nameof(definition)} is null");
            Variables = variables ?? throw Logger.Fatal<ArgumentNullException>($"{nameof(variables)} is null");
            Decisions = decisions ?? throw Logger.Fatal<ArgumentNullException>($"{nameof(decisions)} is null");

            configure?.Invoke(options);
            if (!Enum.IsDefined(options.ErrorHandlingMode))
                throw new ArgumentOutOfRangeException(
                    nameof(configure),
                    options.ErrorHandlingMode,
                    "Unsupported DMN error handling mode");
        }

        /// <summary>
        /// Resets the DMN execution context - clears all variables except the input parameters (sets them to null)
        /// and clears the snapshots
        /// </summary>
        /// <returns><see cref="DmnExecutionContext"/></returns>
        public virtual DmnExecutionContext Reset()
        {
            if (Variables == null) return this;
            foreach (var variable in Variables.Values.Where(i => !i.IsInputParameter))
            {
                variable.Value = null;
            }

            Snapshots.Reset();
            diagnostics.Clear();
            providedDecisions.Clear();
            return this;
        }

        /// <summary>
        /// Sets the <paramref name="name">named</paramref> input parameter <paramref name="value"/>
        /// </summary>
        /// <remarks>
        /// Variable <see cref="DmnExecutionVariable.Value"/> setter doesn't allow to set the value for the input parameters to prevent the change of them,
        ///  so <see cref="DmnExecutionVariable.SetInputParameterValue"/> is to be used explicitly
        /// </remarks>
        /// <param name="name">Name of the input parameter</param>
        /// <param name="value">Value of the input parameter</param>
        /// <returns><see cref="DmnExecutionContext"/></returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty</exception>
        /// <exception cref="DmnExecutorException">Input parameter with given <paramref name="name"/> doesn't exist</exception>
        public virtual DmnExecutionContext WithInputParameter(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw Logger.Fatal<ArgumentException>($"{nameof(name)} is null or empty");

            if (Definition.ImportAliases.TryGetValue(name, out var importedName))
                name = importedName;

            // A namespaced input ("namespace#name") may address an element of an
            // imported model; the definition knows the flattened name it was given.
            if (Definition.QualifiedDecisionNames.TryGetValue(name, out var qualifiedName))
                name = qualifiedName;

            // Aliases usually map an import-qualified name onto a variable name;
            // the target is then subject to the same normalization as a direct name.
            if (!Variables.ContainsKey(name)
                && DmnVariableDefinition.CanNormalizeVariableName(name, out var normalizedName, out _))
                name = normalizedName;

            var variable = Variables?.Values.FirstOrDefault(i => i.IsInputParameter && i.Name == name)
                           ?? Variables?.Values.FirstOrDefault(i => i.Name == name);
            if (variable == null)
                throw Logger.Fatal<DmnExecutorException>($"WithInputParameter: {name} is not an input parameter");

            if (variable.IsInputParameter)
            {
                variable.SetInputParameterValue(value);
            }
            else
            {
                variable.Value = value;
                providedDecisions.Add(variable.Name);
            }
            Logger.Info($"Execution context input parameter {name} set to {value}");

            // Propagate value to alias variables (e.g. when Camunda exports use different
            // names in DRD input data vs. decision table expressions)
            if (options.ResolveInputAliases &&
                Definition.InputExpressionAliases.TryGetValue(name, out var aliases))
            {
                foreach (var aliasName in aliases)
                {
                    if (Variables.TryGetValue(aliasName, out var aliasVar))
                    {
                        aliasVar.Value = value;
                        Logger.Info($"Execution context alias variable {aliasName} set to {value} (alias of input {name})");
                    }
                }
            }

            return this;
        }

        /// <summary>
        /// Sets the input parameters from key-value collection (key=name of parameter, value=value to be set)
        /// </summary>
        /// <remarks>
        /// Variable <see cref="DmnExecutionVariable.Value"/> setter doesn't allow to set the value for the input parameters to prevent the change of them,
        ///  so <see cref="DmnExecutionVariable.SetInputParameterValue"/> is to be used explicitly
        /// </remarks>
        /// <param name="parameters">Collection of parameters - Key=name, Value=value</param>
        /// <returns><see cref="DmnExecutionContext"/></returns>
        /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is null</exception>
        public virtual DmnExecutionContext WithInputParameters(
            IReadOnlyCollection<KeyValuePair<string, object>> parameters)
        {
            if (parameters == null) throw Logger.Fatal<ArgumentNullException>($"{nameof(parameters)} is null");

            foreach (var parameter in parameters)
            {
                WithInputParameter(parameter.Key, parameter.Value);
            }

            return this;
        }

        /// <summary>
        /// Executes (evaluates) decision with given <paramref name="decisionName"/>
        /// </summary>
        /// <param name="decisionName">Name of the decision to execute</param>
        /// <returns>Decision result</returns>
        /// <exception cref="ArgumentException"><paramref name="decisionName"/> is null or empty</exception>
        /// <exception cref="DmnExecutorException">Decision with <paramref name="decisionName"/> not found</exception>
        public DmnDecisionResult ExecuteDecision(string decisionName)
        {
            if (string.IsNullOrWhiteSpace(decisionName))
                throw Logger.Fatal<ArgumentException>($"{nameof(decisionName)} is null or empty");
            if (!Decisions.ContainsKey(decisionName))
                throw Logger.Fatal<DmnExecutorException>($"ExecuteDecision: - decision {decisionName} not found");

            var decision = Decisions[decisionName];
            return ExecuteDecision(decision);
        }

        /// <summary>
        /// Executes (evaluates) given <paramref name="decision"/>
        /// </summary>
        /// <param name="decision">Decision to execute</param>
        /// <returns>Decision result</returns>
        /// <exception cref="ArgumentNullException"><paramref name="decision"/> is null</exception>
        public virtual DmnDecisionResult ExecuteDecision(IDmnDecision decision)
        {
            var decisionResult = ExecuteDecisionCore(decision);
            UpdateDecisionAlias(decision, decisionResult);
            return decisionResult;
        }

        /// <summary>
        /// Invokes a decision service as a function (direct invocation), passing the
        /// values for its input parameters by name.
        /// </summary>
        /// <param name="serviceName">Name of the decision service</param>
        /// <param name="inputs">Input values keyed by parameter name (missing parameters are null)</param>
        /// <returns>Decision service output value</returns>
        /// <exception cref="ArgumentException"><paramref name="serviceName"/> is null or empty</exception>
        /// <exception cref="DmnExecutorException">Decision service not found</exception>
        public virtual object ExecuteDecisionService(string serviceName, IReadOnlyDictionary<string, object> inputs = null)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw Logger.Fatal<ArgumentException>($"{nameof(serviceName)} is null or empty");

            var service = Definition.DecisionServices.Values.FirstOrDefault(s =>
                              string.Equals(s.Name, serviceName, StringComparison.Ordinal))
                          ?? Definition.DecisionServices.Values.FirstOrDefault(s =>
                              string.Equals(s.Name, serviceName, StringComparison.OrdinalIgnoreCase))
                          ?? Definition.DecisionServices.Values.FirstOrDefault(s =>
                              string.Equals(s.Name, DmnVariableDefinition.NormalizeVariableName(serviceName),
                                  StringComparison.Ordinal));
            if (service == null)
                throw Logger.Fatal<DmnExecutorException>($"ExecuteDecisionService: decision service {serviceName} not found");

            var args = new object[service.ParameterNames.Count];
            for (var i = 0; i < service.ParameterNames.Count; i++)
            {
                args[i] = FindServiceInput(inputs, service.ParameterNames[i]);
            }

            return CreateDecisionServiceFunction(service).Invoke(args);
        }

        private static object FindServiceInput(IReadOnlyDictionary<string, object> inputs, string parameterName)
        {
            if (inputs == null) return null;
            if (inputs.TryGetValue(parameterName, out var value)) return value;

            var normalized = DmnVariableDefinition.NormalizeVariableName(parameterName);
            foreach (var input in inputs)
            {
                if (string.Equals(input.Key, parameterName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(DmnVariableDefinition.NormalizeVariableName(input.Key), normalized,
                        StringComparison.Ordinal))
                    return input.Value;
            }

            return null;
        }

        internal void UpdateDecisionAlias(IDmnDecision decision, DmnDecisionResult result)
        {
            if (decision == null || !Definition.DecisionAliases.TryGetValue(decision.Name, out var aliasName)) return;
            if (!Variables.TryGetValue(aliasName, out var aliasVariable)) return;
            if (Variables.Values.Any(v => v.Name == aliasName && v == aliasVariable) && aliasName == decision.Name) return;

            if (!result.HasResult)
            {
                aliasVariable.Value = null;
                return;
            }

            if (!result.IsSingleResult)
            {
                aliasVariable.Value = result.Results.Select(ExtractSingleValue).ToList();
                return;
            }

            aliasVariable.Value = ExtractSingleValue(result.First);
        }

        private static object ExtractSingleValue(DmnDecisionSingleResult single)
        {
            if (single.Variables.Count == 0) return null;
            if (single.Variables.Count == 1) return single.Variables[0].Value;
            var context = new FeelContext();
            foreach (var variable in single.Variables) context.Put(variable.Name, variable.Value);
            return context;
        }

        private DmnDecisionResult ExecuteDecisionCore(IDmnDecision decision)
        {
            if (decision == null) throw Logger.Fatal<ArgumentNullException>($"{nameof(decision)} is null");

            if (Options.RecordSnapshots)
            {
                //clear snapshots and create "initial" snapshot of the variables
                Snapshots.Reset();
                Snapshots.CreateSnapshot(this);
            }

            var correlationId = Guid.NewGuid().ToString();
            var result = decision.Execute(this, correlationId);
            //note: the decision-after-execute snapshot is created by decision (for each decision in "dependency graph")

            //clear execution cache
            PurgeExpressionCacheExecutionScope(correlationId);

            return result;
        }

        /// <summary>
        /// Creates the execution context snapshot - to be called by decision after the evaluation
        /// </summary>
        /// <param name="decision">Decision evaluated just before the snapshot</param>
        /// <param name="result"><paramref name="decision"/> result</param>
        internal virtual void CreateSnapshot(IDmnDecision decision, DmnDecisionResult result)
        {
            if (Options.RecordSnapshots)
                Snapshots.CreateSnapshot(this, decision, result);
        }

        /// <summary>
        /// Builds a FEEL evaluation context from the current execution variables.
        /// </summary>
        protected virtual FeelEvaluationContext BuildFeelEvaluationContext()
        {
            var evalContext = new FeelEvaluationContext
            {
                TypeAliases = Definition.ItemTypeNames
            };
            foreach (var variable in Variables.Values)
            {
                // FEEL null is a value. Do not substitute CLR defaults: null must
                // propagate through arithmetic, comparisons and boolean logic.
                var value = FeelTypeCoercion.CoerceToFeel(variable.Value);
                evalContext.SetVariable(variable.Name, value);

                // The FEEL expression refers to the DMN label (for example
                // "Full Name"); the engine's variable key is normalized
                // ("Full_Name"). Alias the label so both resolve.
                if (!string.IsNullOrWhiteSpace(variable.Label)
                    && !string.Equals(variable.Label, variable.Name, StringComparison.Ordinal))
                {
                    evalContext.SetVariable(variable.Label, value);
                }
            }

            // Business knowledge models are FEEL functions, not stored variables.
            ApplyAliases(evalContext, Definition.ImportAliases);
            ApplyAliases(evalContext, CurrentNameAliases);

            foreach (var bkm in Definition.BusinessKnowledgeModels.Values)
            {
                evalContext.SetVariable(bkm.Name, CreateBusinessKnowledgeModelFunction(bkm));
            }

            // Decision services are FEEL functions too; they can be passed to a
            // BKM as a lambda argument just like a BKM or a user-defined function.
            foreach (var service in Definition.DecisionServices.Values)
            {
                evalContext.SetVariable(service.Name, CreateDecisionServiceFunction(service));
            }

            return evalContext;
        }

        /// <summary>
        /// Builds a FEEL scope with all known variable names for multi-word name resolution.
        /// </summary>
        private void ApplyAliases(FeelEvaluationContext evalContext, IReadOnlyDictionary<string, string> aliases)
        {
            if (aliases == null) return;
            foreach (var alias in aliases)
            {
                if (TryResolveAliasTarget(alias.Value, out var source))
                {
                    evalContext.SetVariable(alias.Key, FeelTypeCoercion.CoerceToFeel(source.Value));
                }
                else if (Definition.BusinessKnowledgeModels.TryGetValue(alias.Value, out var bkm))
                {
                    evalContext.SetVariable(alias.Key, CreateBusinessKnowledgeModelFunction(bkm));
                }
                else if (Definition.DecisionServices.TryGetValue(alias.Value, out var service))
                {
                    evalContext.SetVariable(alias.Key, CreateDecisionServiceFunction(service));
                }
            }
        }

        /// <summary>
        /// Resolves the target of an import alias. Alias targets are written as they
        /// appear in the model, so a name with spaces ("A Person") has to be matched
        /// against the normalized variable name and the variable label as well.
        /// </summary>
        private bool TryResolveAliasTarget(string name, out DmnExecutionVariable variable)
        {
            variable = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (Variables.TryGetValue(name, out variable)) return true;

            // Names with spaces or operator characters are stored under their
            // normalized form; import-qualified names are not normalizable, so the
            // conversion is attempted only when it is valid.
            if (DmnVariableDefinition.CanNormalizeVariableName(name, out var normalized, out _)
                && Variables.TryGetValue(normalized, out variable))
                return true;

            variable = Variables.Values.FirstOrDefault(v =>
                string.Equals(v.Label, name, StringComparison.Ordinal));
            return variable != null;
        }

        protected virtual FeelScope BuildFeelScope()
        {
            var scope = new FeelScope();
            foreach (var variable in Variables.Values)
            {
                scope.AddName(variable.Name);
                // FEEL names may contain spaces; the label preserves the name as
                // written in the model ("Full Name") while the key is normalized.
                if (!string.IsNullOrWhiteSpace(variable.Label)
                    && !string.Equals(variable.Label, variable.Name, StringComparison.Ordinal))
                {
                    scope.AddName(variable.Label);
                }
            }

            foreach (var bkm in Definition.BusinessKnowledgeModels.Values)
            {
                scope.AddName(bkm.Name);
            }
            foreach (var service in Definition.DecisionServices.Values)
            {
                scope.AddName(service.Name);
            }

            foreach (var alias in CurrentNameAliases.Keys)
                scope.AddName(alias);
            foreach (var alias in Definition.ImportAliases.Keys)
                scope.AddName(alias);

            return scope;
        }

        /// <summary>
        /// Creates a callable FEEL function for a decision service. The function binds
        /// its arguments to the input data / input decision variables, executes the
        /// output decisions and restores the execution context afterwards so that a
        /// decision service call never leaks its parameters into the surrounding scope.
        /// </summary>
        private FeelFunction CreateDecisionServiceFunction(DmnDecisionService service)
        {
            return new FeelFunction(service.Name, service.ParameterNames,
                args => InvokeDecisionServiceAsFunction(service, args),
                service.ParameterTypes, Definition.ItemTypeNames, true,
                namedBody: namedArguments => InvokeDecisionServiceAsFunction(
                    service, BindNamedArguments(service, namedArguments)));
        }

        /// <summary>
        /// Maps named arguments (FEEL <c>f(p: v)</c>) onto the parameter order.
        /// Unknown or missing parameter names are errors: a decision service is
        /// only invocable when every parameter has a value.
        /// </summary>
        private static object[] BindNamedArguments(DmnDecisionService service,
            IReadOnlyDictionary<string, object> namedArguments)
        {
            return BindArguments(service, namedArguments, message => new DmnEvaluationException(message));
        }

        /// <summary>
        /// Maps arguments supplied by name onto the parameter order. Unknown,
        /// duplicate or missing parameters are errors.
        /// </summary>
        private static object[] BindArguments(DmnDecisionService service,
            IEnumerable<KeyValuePair<string, object>> arguments, Func<string, Exception> error)
        {
            var args = new object[service.Parameters.Count];
            var supplied = new bool[args.Length];

            foreach (var argument in arguments ?? Enumerable.Empty<KeyValuePair<string, object>>())
            {
                var index = service.IndexOfParameter(argument.Key);
                if (index < 0)
                    throw error($"Decision service {service.Name} has no parameter named '{argument.Key}'"
                                + (service.Parameters.Count > 0
                                    ? $" (parameters: {string.Join(", ", service.ParameterNames)})"
                                    : " (it has no parameters)"));
                if (supplied[index])
                    throw error(
                        $"Decision service {service.Name}: parameter '{service.Parameters[index].Name}' is supplied more than once");
                args[index] = argument.Value;
                supplied[index] = true;
            }

            for (var i = 0; i < supplied.Length; i++)
            {
                if (!supplied[i])
                    throw error(
                        $"Decision service {service.Name} requires a value for parameter '{service.Parameters[i].Name}'");
            }

            return args;
        }

        private object InvokeDecisionServiceAsFunction(DmnDecisionService service, object[] args)
        {
            var outputs = ExecuteDecisionService(service, args);

            if (service.OutputDecisionNames.Count == 1)
                return ConformServiceOutput(service, outputs[service.OutputDecisionNames[0]]);

            var context = new FeelContext();
            foreach (var output in outputs) context.Put(output.Key, output.Value);
            return context;
        }

        /// <summary>
        /// Invokes a decision service directly (not through a FEEL expression).
        /// Inputs are matched to parameters by name.
        /// </summary>
        /// <param name="serviceName">Name of the decision service</param>
        /// <param name="inputs">Parameter values by name</param>
        /// <returns>Output decision values by decision name</returns>
        /// <exception cref="DmnExecutorException">The decision service does not exist or a parameter cannot be bound</exception>
        public IReadOnlyDictionary<string, object> InvokeDecisionService(string serviceName,
            IReadOnlyDictionary<string, object> inputs)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw Logger.Fatal<ArgumentException>($"{nameof(serviceName)} is null or empty");
            if (!Definition.DecisionServices.TryGetValue(serviceName, out var service))
                throw Logger.Fatal<DmnExecutorException>(
                    $"InvokeDecisionService: decision service {serviceName} not found");

            var args = BindArguments(service, inputs,
                message => Logger.Fatal<DmnExecutorException>("InvokeDecisionService: " + message));

            var outputs = ExecuteDecisionService(service, args);
            if (service.OutputDecisionNames.Count != 1) return outputs;

            // A decision service is directly invocable only when its result conforms
            // to the declared output type; a non-conforming result is an error.
            var outputName = service.OutputDecisionNames[0];
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [outputName] = ConformServiceOutput(service, outputs[outputName],
                    message => Logger.Fatal<DmnEvaluationException>(message))
            };
        }

        /// <summary>
        /// Applies the declared FEEL output type of the decision service. A result that
        /// does not conform becomes null (the DMN null-coercion result), or is reported
        /// through <paramref name="onTypeError"/> when the caller treats it as an error.
        /// </summary>
        private object ConformServiceOutput(DmnDecisionService service, object value,
            Func<string, Exception> onTypeError = null)
        {
            if (string.IsNullOrWhiteSpace(service.OutputTypeName)) return value;
            if (FeelTypeConformance.TryCoerce(value, service.OutputTypeName, Definition.ItemTypeNames,
                    out var conformed))
                return conformed;

            return onTypeError != null
                ? throw onTypeError(
                    $"Decision service {service.Name}: result does not conform to the declared type {service.OutputTypeName}")
                : null;
        }

        /// <summary>
        /// Binds <paramref name="args"/> to the decision service parameters, runs the
        /// output decisions and restores every touched variable (and the provided
        /// decision set) before returning.
        /// </summary>
        private IReadOnlyDictionary<string, object> ExecuteDecisionService(DmnDecisionService service, object[] args)
        {
            var expected = service.Parameters.Count;
            if (args == null || args.Length != expected)
                throw new DmnEvaluationException(
                    $"Decision service {service.Name} expects {expected} parameter(s) but got {args?.Length ?? 0}");

            var savedVariables = new List<KeyValuePair<DmnExecutionVariable, object>>();
            var previousProvided = providedDecisions.ToList();
            try
            {
                for (var i = 0; i < expected; i++)
                {
                    var parameter = service.Parameters[i];
                    if (!Variables.TryGetValue(parameter.VariableName, out var variable))
                        throw new DmnEvaluationException(
                            $"Decision service {service.Name}: parameter '{parameter.Name}' has no variable '{parameter.VariableName}'");

                    var value = CheckArgumentType(service, parameter, args[i]);
                    savedVariables.Add(new KeyValuePair<DmnExecutionVariable, object>(variable, variable.Value));
                    SetVariableValue(variable, value);

                    // A provided decision must not be executed: its supplied value is
                    // the value the output decision sees.
                    if (parameter.IsDecision) providedDecisions.Add(parameter.DecisionName);
                }

                var outputs = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var outputDecisionName in service.OutputDecisionNames)
                {
                    outputs[outputDecisionName] = ExtractDecisionValue(ExecuteDecision(outputDecisionName));
                }
                return outputs;
            }
            finally
            {
                for (var i = savedVariables.Count - 1; i >= 0; i--)
                {
                    SetVariableValue(savedVariables[i].Key, savedVariables[i].Value);
                }
                ProvidedDecisions = previousProvided;
            }
        }

        /// <summary>
        /// Checks an argument against the declared parameter type. FEEL converts a
        /// singleton list to the value it contains; any other incompatible value
        /// binds null to an input data parameter (the value FEEL coercion produces)
        /// but makes the service not invocable when an input decision is missing a
        /// value of its declared type.
        /// </summary>
        private static object CheckArgumentType(DmnDecisionService service,
            DmnDecisionServiceParameter parameter, object value)
        {
            if (value == null || parameter.IsAnyType) return value;

            var feelType = FeelTypeCoercion.GetFeelTypeName(FeelTypeCoercion.CoerceToFeel(value));
            if (string.Equals(feelType, parameter.FeelTypeName, StringComparison.Ordinal)) return value;

            // FEEL treats a singleton list as the value it contains.
            if (value is List<object> list && list.Count == 1)
                return CheckArgumentType(service, parameter, list[0]);

            if (!parameter.IsDecision) return null;

            throw new DmnEvaluationException(
                $"Decision service {service.Name}: parameter '{parameter.Name}' expects {parameter.FeelTypeName} but got {feelType}");
        }

        private static void SetVariableValue(DmnExecutionVariable variable, object value)
        {
            if (variable.IsInputParameter) variable.SetInputParameterValue(value);
            else variable.Value = value;
        }

        private static object ExtractDecisionValue(DmnDecisionResult result)
        {
            if (!result.HasResult) return null;
            if (!result.IsSingleResult)
                return result.Results.Select(single => ExtractSingleDecisionValue(single)).ToList();

            return ExtractSingleDecisionValue(result.First);
        }

        private static object ExtractSingleDecisionValue(DmnDecisionSingleResult single)
        {
            if (single.Variables.Count == 0) return null;
            if (single.Variables.Count == 1) return single.Variables[0].Value;

            var context = new FeelContext();
            foreach (var variable in single.Variables) context.Put(variable.Name, variable.Value);
            return context;
        }

        /// <summary>
        /// Creates a callable FEEL function for a business knowledge model. The body
        /// is evaluated in a child scope containing the formal parameters plus the
        /// current execution variables.
        /// </summary>
        private FeelFunction CreateBusinessKnowledgeModelFunction(DmnBusinessKnowledgeModel bkm)
        {
            var parameters = bkm.ParameterNames;

            return new FeelFunction(bkm.Name, parameters, args =>
            {
                var childContext = BuildFeelEvaluationContext();
                ApplyAliases(childContext, bkm.NameAliases);
                var child = childContext.CreateChildScope();
                for (var i = 0; i < parameters.Count; i++)
                {
                    child.SetVariable(parameters[i], i < args.Length ? args[i] : null);
                }

                if (bkm.Body != null)
                {
                    // Boxed bodies (context, relation, list, invocation, decision table or
                    // literal expression) are evaluated against the function scope.
                    var runtime = new DmnBoxedExpressionRuntime(this, child, parameters, null);
                    var boxedResult = runtime.Evaluate(bkm.Body);

                    // The body can declare its output type; a non-conforming value is
                    // the DMN null-coercion result.
                    if (!string.IsNullOrWhiteSpace(bkm.OutputTypeName)
                        && !FeelTypeConformance.TryCoerce(boxedResult, bkm.OutputTypeName,
                            Definition.ItemTypeNames, out boxedResult))
                        return null;

                    return boxedResult;
                }

                var scope = BuildFeelScope();
                foreach (var parameter in parameters)
                {
                    scope.AddName(parameter);
                }

                var engine = GetFeelEngine();
                var ast = engine.ParseExpression(bkm.Expression, scope);
                var result = engine.Evaluate(ast, child, DmnExpressionLanguage.Resolve(bkm.ExpressionLanguage));

                // The body can declare its output type; a non-conforming value is
                // the DMN null-coercion result.
                if (!string.IsNullOrWhiteSpace(bkm.OutputTypeName)
                    && !FeelTypeConformance.TryCoerce(result, bkm.OutputTypeName, Definition.ItemTypeNames, out result))
                    return null;

                return result;
            }, bkm.ParameterTypes, Definition.ItemTypeNames, true);
        }

        /// <summary>
        /// Creates a runtime for evaluating a boxed expression against the current variables.
        /// </summary>
        internal DmnBoxedExpressionRuntime CreateBoxedExpressionRuntime(string executionId)
        {
            return new DmnBoxedExpressionRuntime(this, BuildFeelEvaluationContext(), null, executionId);
        }

        /// <summary>
        /// Converts a FEEL result to the declared CLR output type of a variable.
        /// </summary>
        internal static object ConvertToOutputType(object result, Type outputType)
        {
            return ConvertResult(result, outputType);
        }

        /// <inheritdoc />
        object IDmnBoxedExpressionHost.EvaluateBoxedExpression(string expression, FeelDialect dialect,
            FeelEvaluationContext scope, IReadOnlyCollection<string> localNames)
        {
            if (string.IsNullOrWhiteSpace(expression)) return null;

            expression = PreProcessExpression(expression);
            var feelScope = BuildFeelScope();
            if (localNames != null)
            {
                foreach (var name in localNames) feelScope.AddName(name);
            }

            FeelAstNode parsedAst;
            try
            {
                parsedAst = GetFeelEngine().ParseExpression(expression, feelScope);
            }
            catch (Exception exception)
            {
                throw Logger.Fatal<DmnEvaluationException>(
                    $"Exception while parsing the expression {expression}", exception);
            }

            try
            {
                return GetFeelEngine().Evaluate(parsedAst, scope ?? BuildFeelEvaluationContext(), dialect);
            }
            catch (Exception exception)
            {
                throw Logger.Fatal<DmnEvaluationException>(
                    $"Exception while invoking the expression {expression}", exception);
            }
        }

        /// <inheritdoc />
        bool IDmnBoxedExpressionHost.EvaluateBoxedExpressionUnaryTest(string expression, object inputValue,
            FeelDialect dialect, FeelEvaluationContext scope, IReadOnlyCollection<string> localNames)
        {
            if (string.IsNullOrWhiteSpace(expression))
                throw Logger.Fatal<ArgumentException>($"{nameof(expression)} is null or empty");

            expression = PreProcessExpression(expression);
            var feelScope = BuildFeelScope();
            if (localNames != null)
            {
                foreach (var name in localNames) feelScope.AddName(name);
            }

            FeelAstNode parsedAst;
            try
            {
                parsedAst = GetFeelEngine().ParseSimpleUnaryTests(expression, feelScope);
            }
            catch
            {
                // Camunda and other tools allow full FEEL expressions (with or/and
                // operators) in input entries, not just simple unary tests.
                try
                {
                    parsedAst = GetFeelEngine().ParseExpression(expression, feelScope);
                }
                catch (Exception fallbackException)
                {
                    throw Logger.Fatal<DmnEvaluationException>(
                        $"Exception while parsing the unary tests expression {expression}", fallbackException);
                }
            }

            var evalContext = scope ?? BuildFeelEvaluationContext();
            evalContext.InputValue = FeelTypeCoercion.CoerceToFeel(inputValue);

            try
            {
                if (dialect == FeelDialect.BFeel)
                    throw new NotSupportedException("B-FEEL evaluation is not implemented. Supported dialect: FEEL.");
                var evaluator = new FeelEvaluator(evalContext,
                    ScratchyDisk.DmnEngine.Feel.Functions.FeelBuiltInFunctions.Resolve);
                return evaluator.EvaluateAsUnaryTest(parsedAst);
            }
            catch (Exception exception)
            {
                throw Logger.Fatal<DmnEvaluationException>(
                    $"Exception while evaluating the unary tests expression {expression}", exception);
            }
        }

        /// <summary>
        /// Evaluates expression
        /// </summary>
        /// <param name="expression">Expression to evaluate</param>
        /// <param name="outputType">Output (result) type</param>
        /// <param name="executionId">Identifier of the execution run</param>
        /// <exception cref="ArgumentException"><paramref name="expression"/> is null or empty</exception>
        /// <exception cref="ArgumentNullException"><paramref name="outputType"/> is null</exception>
        /// <exception cref="DmnExecutorException">Exception while invoking the expression</exception>
        /// <exception cref="DmnExecutorException">Can't convert the expression result to <paramref name="outputType"/></exception>
        /// <returns>The expression result converted to <paramref name="outputType"/></returns>
        public virtual object EvalExpression(string expression, Type outputType, string executionId)
        {
            return EvalExpression(expression, outputType, executionId, FeelDialect.Feel);
        }
        /// <summary>
        /// Evaluates an expression using the selected FEEL dialect.
        /// </summary>
        public virtual object EvalExpression(string expression, Type outputType, string executionId, FeelDialect dialect)
        {
            if (string.IsNullOrWhiteSpace(expression))
                throw Logger.Fatal<ArgumentException>($"{nameof(expression)} is null or empty");

            // A missing typeRef is the DMN type "any": evaluate the expression and
            // return the FEEL value without conversion.
            expression = PreProcessExpression(expression);
            var scope = BuildFeelScope();

            // Check parsed expression cache
            var cacheKey = GetParsedExpressionCacheKey(executionId, expression, outputType);
            if (Options.ParsedExpressionCacheScope == ParsedExpressionCacheScopeEnum.None ||
                !GetParsedExpressionsFromCache(cacheKey, out var parsedAst))
            {
                try
                {
                    parsedAst = GetFeelEngine().ParseExpression(expression, scope);
                }
                catch (Exception exception)
                {
                    throw Logger.Fatal<DmnEvaluationException>($"Exception while parsing the expression {expression}",
                        exception);
                }

                if (Options.ParsedExpressionCacheScope != ParsedExpressionCacheScopeEnum.None)
                    CacheParsedExpression(cacheKey, parsedAst);
            }

            // Evaluate expression
            var evalContext = BuildFeelEvaluationContext();
            object result;
            try
            {
                result = GetFeelEngine().Evaluate(parsedAst, evalContext, dialect);
            }
            catch (Exception exception)
            {
                throw Logger.Fatal<DmnEvaluationException>($"Exception while invoking the expression {expression}",
                    exception);
            }

            // Convert the result to the expected output type
            object resultConverted;
            try
            {
                resultConverted = outputType == null ? result : ConvertResult(result, outputType);
            }
            catch (Exception exception)
            {
                throw Logger.Fatal<DmnEvaluationException>(
                    $"Can't convert the expression result to {outputType?.Name ?? "any"}", exception);
            }

            return resultConverted;
        }

        /// <summary>
        /// Applies the declared FEEL type and the expected CLR type to a decision
        /// result. A value that does not conform to the declared type becomes null
        /// (the DMN null-coercion result).
        /// </summary>
        /// <param name="result">Raw FEEL result</param>
        /// <param name="outputType">Expected CLR output type, if known</param>
        /// <param name="declaredTypeName">Declared FEEL type name, if known</param>
        public virtual object ConvertDecisionResult(object result, Type outputType, string declaredTypeName)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(declaredTypeName))
                {
                    if (!FeelTypeConformance.TryCoerce(result, declaredTypeName, Definition.ItemTypeNames,
                            out var conformed))
                    {
                        throw Logger.Error<DmnEvaluationException>(
                            $"Decision result does not conform to the declared type {declaredTypeName}");
                    }

                    result = conformed;
                }

                return ConvertResult(result, outputType);
            }
            catch (DmnEvaluationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Logger.Fatal<DmnEvaluationException>(
                    $"Can't convert the expression result to {outputType?.Name ?? "any"}", exception);
            }
        }

        /// <summary>
        /// Evaluates a FEEL simple unary tests expression against the given input value.
        /// Used for decision table rule input evaluation.
        /// </summary>
        /// <param name="unaryTestsExpression">The unary tests expression (e.g. "> 5", "1..10", "\"hello\"")</param>
        /// <param name="inputValue">The input value to test against</param>
        /// <param name="executionId">Identifier of the execution run</param>
        /// <returns>True if the input value matches the unary tests</returns>
        public virtual bool EvalUnaryTests(string unaryTestsExpression, object inputValue, string executionId)
        {
            return EvalUnaryTests(unaryTestsExpression, inputValue, executionId, FeelDialect.Feel);
        }
        /// <summary>
        /// Evaluates unary tests using the selected FEEL dialect.
        /// </summary>
        public virtual bool EvalUnaryTests(string unaryTestsExpression, object inputValue, string executionId, FeelDialect dialect)
        {
            if (string.IsNullOrWhiteSpace(unaryTestsExpression))
                throw Logger.Fatal<ArgumentException>($"{nameof(unaryTestsExpression)} is null or empty");

            unaryTestsExpression = PreProcessExpression(unaryTestsExpression);
            var scope = BuildFeelScope();

            // Check parsed expression cache
            var cacheKey = GetParsedExpressionCacheKey(executionId, $"UT:{unaryTestsExpression}", typeof(bool));
            if (Options.ParsedExpressionCacheScope == ParsedExpressionCacheScopeEnum.None ||
                !GetParsedExpressionsFromCache(cacheKey, out var parsedAst))
            {
                try
                {
                    parsedAst = GetFeelEngine().ParseSimpleUnaryTests(unaryTestsExpression, scope);
                }
                catch
                {
                    // Fallback: Camunda and other tools allow full FEEL expressions
                    // (with or/and operators) in input entries, not just simple unary tests.
                    try
                    {
                        parsedAst = GetFeelEngine().ParseExpression(unaryTestsExpression, scope);
                    }
                    catch (Exception fallbackException)
                    {
                        throw Logger.Fatal<DmnEvaluationException>(
                            $"Exception while parsing the unary tests expression {unaryTestsExpression}", fallbackException);
                    }
                }

                if (Options.ParsedExpressionCacheScope != ParsedExpressionCacheScopeEnum.None)
                    CacheParsedExpression(cacheKey, parsedAst);
            }

            // Evaluate
            var evalContext = BuildFeelEvaluationContext();
            evalContext.InputValue = FeelTypeCoercion.CoerceToFeel(inputValue);

            try
            {
                if (dialect == FeelDialect.BFeel)
                    throw new NotSupportedException("B-FEEL evaluation is not implemented. Supported dialect: FEEL.");
                var evaluator = new FeelEvaluator(evalContext, ScratchyDisk.DmnEngine.Feel.Functions.FeelBuiltInFunctions.Resolve);
                return evaluator.EvaluateAsUnaryTest(parsedAst);
            }
            catch (Exception exception)
            {
                throw Logger.Fatal<DmnEvaluationException>(
                    $"Exception while evaluating the unary tests expression {unaryTestsExpression}", exception);
            }
        }

        /// <summary>
        /// Converts a FEEL evaluation result to the expected CLR output type.
        /// Handles FEEL-specific types (decimal, DateOnly, etc.) and standard .NET conversions.
        /// </summary>
        protected static object ConvertResult(object result, Type outputType)
        {
            if (outputType == null) return result;
            // FEEL null is a value, not an absence: it must not be turned into the
            // CLR default (0, false) of a declared output type.
            if (result == null) return null;
            if (outputType == typeof(object)) return result;
            if (outputType.IsInstanceOfType(result)) return result;

            if (result is FeelExpandedDate || result is FeelExpandedDateTime)
                return result;

            // FEEL date/time values and their CLR counterparts.
            if (result is FeelDateTime zonedDateTime)
            {
                if (outputType == typeof(DateTimeOffset)) return zonedDateTime.Value;
                if (outputType == typeof(DateTime)) return zonedDateTime.Value.DateTime;
                if (outputType == typeof(DateOnly)) return DateOnly.FromDateTime(zonedDateTime.Value.DateTime);
                if (outputType == typeof(object)) return zonedDateTime;
            }
            if (result is DateOnly dateValue)
            {
                if (outputType == typeof(DateTime))
                    return dateValue.ToDateTime(TimeOnly.MinValue);
                if (outputType == typeof(DateTimeOffset))
                    return new DateTimeOffset(dateValue.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            }
            if (result is DateTime dateTimeValue)
            {
                if (outputType == typeof(DateOnly)) return DateOnly.FromDateTime(dateTimeValue);
                if (outputType == typeof(DateTimeOffset))
                    return new DateTimeOffset(dateTimeValue);
            }
            if (result is DateTimeOffset offsetValue && outputType == typeof(DateOnly))
                return DateOnly.FromDateTime(offsetValue.DateTime);

            // Use FEEL type coercion first
            var coerced = FeelTypeCoercion.CoerceToClr(result, outputType);
            if (coerced != null) return coerced;

            // Fallback to Convert.ChangeType for standard types
            return Convert.ChangeType(result, outputType);
        }

        /// <summary>
        /// Tries to retrieve the <paramref name="parsedExpression"/> from the parsed expression cache using the <paramref name="cacheKey"/>.
        /// <see cref="ParsedExpressionCacheScopeEnum.Global"/> and <see cref="ParsedExpressionCacheScopeEnum.Definition"/> scopes use static <see cref="ParsedExpressionsCache"/>,
        /// otherwise the <see cref="ParsedExpressionsInstanceCache"/> is used.
        /// </summary>
        /// <param name="cacheKey">Retrieval key of the <paramref name="parsedExpression"/></param>
        /// <param name="parsedExpression">Parsed expression retrieved from cache if successful</param>
        /// <returns>True when the <paramref name="parsedExpression"/> has been retrieved from cache, otherwise false</returns>
        protected virtual bool GetParsedExpressionsFromCache(string cacheKey, out FeelAstNode parsedExpression)
        {

            if (Options.ParsedExpressionCacheScope == ParsedExpressionCacheScopeEnum.Global ||
                Options.ParsedExpressionCacheScope == ParsedExpressionCacheScopeEnum.Definition)
            {
                return ParsedExpressionsCache.TryGetValue(cacheKey, out parsedExpression);
            }

            return ParsedExpressionsInstanceCache.TryGetValue(cacheKey, out parsedExpression);
        }

        /// <summary>
        /// Store the <paramref name="parsedExpression"/> into parsed expression cache using the <paramref name="cacheKey"/>.
        /// <see cref="ParsedExpressionCacheScopeEnum.Global"/> and <see cref="ParsedExpressionCacheScopeEnum.Definition"/> scopes use static <see cref="ParsedExpressionsCache"/>,
        /// otherwise the <see cref="ParsedExpressionsInstanceCache"/> is used.
        /// </summary>
        /// <param name="cacheKey">Retrieval key of the <paramref name="parsedExpression"/></param>
        /// <param name="parsedExpression">Parsed expression to cache</param>
        protected virtual void CacheParsedExpression(string cacheKey, FeelAstNode parsedExpression)
        {
            if (Options.ParsedExpressionCacheScope == ParsedExpressionCacheScopeEnum.Global ||
                Options.ParsedExpressionCacheScope == ParsedExpressionCacheScopeEnum.Definition)
            {
                ParsedExpressionsCache[cacheKey] = parsedExpression;
            }
            else
            {
                ParsedExpressionsInstanceCache[cacheKey] = parsedExpression;
            }
        }

        /// <summary>
        /// Compose the parsed expression key (based on <see cref="DmnExecutionContextOptions.ParsedExpressionCacheScope"/>)
        /// </summary>
        /// <param name="executionId">Identifier of the execution run</param>
        /// <param name="expression">Unparsed (raw) expression</param>
        /// <param name="outputType">Expression output type</param>
        /// <returns>Parsed expression key</returns>
        protected virtual string GetParsedExpressionCacheKey(string executionId, string expression, Type outputType)
        {
            string prefix;
            switch (Options.ParsedExpressionCacheScope)
            {
                case ParsedExpressionCacheScopeEnum.None:
                    prefix = Guid.NewGuid().ToString(); //fallback, always unique key
                    break;
                case ParsedExpressionCacheScopeEnum.Execution:
                    prefix = executionId;
                    break;
                case ParsedExpressionCacheScopeEnum.Context:
                    prefix = Id;
                    break;
                case ParsedExpressionCacheScopeEnum.Definition:
                    prefix = Definition.Id;
                    break;
                case ParsedExpressionCacheScopeEnum.Global:
                    prefix = "";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(Options.ParsedExpressionCacheScope),
                        Options.ParsedExpressionCacheScope,
                        "Unsupported parsed-expression cache scope");
            }

            return $"{prefix}||{expression}||{outputType?.AssemblyQualifiedName ?? "any"}";
        }

        /// <summary>
        /// Purge all cached expressions belonging to given Execution <paramref name="executionId"/> scope
        /// </summary>
        /// <param name="executionId">Execution Id</param>
        public virtual void PurgeExpressionCacheExecutionScope(string executionId)
        {
            var keys = ParsedExpressionsInstanceCache.Keys.Where(k => k.StartsWith($"{executionId}||"));
            foreach (var key in keys)
            {
                ParsedExpressionsInstanceCache.TryRemove(key, out _);
            }
        }


        /// <summary>
        /// Purge all cached expressions belonging to any Execution scope
        /// </summary>
        public virtual void PurgeExpressionCacheExecutionScopeAll()
        {
            var keys = ParsedExpressionsInstanceCache.Keys.Where(k => !k.StartsWith($"{Id}||")); //anything that doesn't belongs to Context should be Execution scope (None scope is not stored)
            foreach (var key in keys)
            {
                ParsedExpressionsInstanceCache.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Purge all cached expressions belonging to this Context scope
        /// </summary>
        public virtual void PurgeExpressionCacheContextScope()
        {
            var keys = ParsedExpressionsInstanceCache.Keys.Where(k => k.StartsWith($"{Id}||"));
            foreach (var key in keys)
            {
                ParsedExpressionsInstanceCache.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Purge all cached expressions belonging to given Definition <paramref name="definitionId"/> scope
        /// </summary>
        /// <param name="definitionId">Definition Id</param>
        public static void PurgeExpressionCacheDefinitionScope(string definitionId)
        {
            var keys = ParsedExpressionsCache.Keys.Where(k => k.StartsWith($"{definitionId}||"));
            foreach (var key in keys)
            {
                ParsedExpressionsCache.TryRemove(key, out _);
            }
        }


        /// <summary>
        /// Purge all cached expressions belonging to any Definition scope
        /// </summary>
        public static void PurgeExpressionCacheDefinitionScopeAll()
        {
            var keys = ParsedExpressionsCache.Keys.Where(k => !k.StartsWith($"||")); //anything that doesn't belongs to Global should be Definition scope
            foreach (var key in keys)
            {
                ParsedExpressionsCache.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Purge all cached expressions belonging to Global scope
        /// </summary>
        public static void PurgeExpressionCacheGlobalScope()
        {
            var keys = ParsedExpressionsCache.Keys.Where(k => k.StartsWith($"||"));
            foreach (var key in keys)
            {
                ParsedExpressionsCache.TryRemove(key, out _);
            }
        }
        /// <summary>
        /// Pre-processes a DMN expression to handle DMN-style date/time/duration constructor syntax.
        /// Converts unquoted date/time arguments to quoted strings:
        /// <c>date(2018-01-23)</c> → <c>date("2018-01-23")</c>
        /// <c>time(13:00)</c> → <c>time("13:00")</c>
        /// <c>duration(P3Y)</c> → <c>duration("P3Y")</c>
        /// String literals are preserved (not modified).
        /// </summary>
        protected static string PreProcessExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return expression;

            // Single regex that matches EITHER a string literal (to skip it) OR a date/time/duration
            // constructor with an unquoted literal argument. The alternation ensures string contents
            // are never modified. Argument must start with a digit (dates/times), 'P' (durations),
            // or 'T' followed by a digit (time-only), to avoid matching variable names.
            expression = Regex.Replace(expression,
                @"""(?:[^""\\]|\\.)*""" +                                          // Alt 1: string literal
                @"|\b(date\s+and\s+time|date|time|duration)\((?!"")" +             // Alt 2: function(
                @"(\d[\d\-T:+.Z]*|P[\dYMDTHS.]+|T\d[\d:.+\-Z]*)\)",              // unquoted literal arg)
                match =>
                {
                    if (!match.Groups[1].Success) return match.Value; // string literal — keep as-is
                    return $"{match.Groups[1].Value}(\"{match.Groups[2].Value}\")";
                },
                RegexOptions.None,
                RegexDefaults.Timeout);

            return expression;
        }

        /// <summary>
        /// Evaluates expression
        /// </summary>
        /// <param name="expression">Expression to evaluate</param>
        /// <param name="executionId">Identifier of the execution run</param>
        /// <typeparam name="TOutputType">Output (result) type</typeparam>
        /// <exception cref="ArgumentNullException"><paramref name="expression"/> is null or empty</exception>
        /// <returns>The expression result converted to <typeparamref name="TOutputType"/></returns>
        public TOutputType EvalExpression<TOutputType>(string expression, string executionId)
        {
            return EvalExpression<TOutputType>(expression, executionId, FeelDialect.Feel);
        }
        public TOutputType EvalExpression<TOutputType>(string expression, string executionId, FeelDialect dialect)
        {
            if (string.IsNullOrWhiteSpace(expression))
                throw Logger.Fatal<ArgumentNullException>($"EvalExpression: - {nameof(expression)} is null or empty");

            var result = EvalExpression(expression, typeof(TOutputType), executionId, dialect);
            var resultConverted = (TOutputType)result;
            return resultConverted;
        }

        /// <summary>
        /// Gets the runtime (execution) variable with given <paramref name="name"/>
        /// </summary>
        /// <param name="name">Name of the variable</param>
        /// <returns>Variable with given <paramref name="name"/></returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty</exception>
        /// <exception cref="DmnExecutorException">Variable not found</exception>
        public virtual DmnExecutionVariable GetVariable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw Logger.Fatal<ArgumentException>($"{nameof(name)} is null or empty");

            if (!Variables.ContainsKey(name))
            {
                throw Logger.Fatal<DmnExecutorException>($"GetVariable: - variable {name} not found");
            }

            return Variables[name];
        }

        /// <summary>
        /// Gets the runtime (execution) variable corresponding to its <paramref name="definition"/>
        /// </summary>
        /// <param name="definition">Name of the variable</param>
        /// <returns>Variable  corresponding to its <paramref name="definition"/></returns>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null</exception>
        public DmnExecutionVariable GetVariable(IDmnVariable definition)
        {
            if (definition == null)
                throw Logger.Fatal<ArgumentNullException>($"{nameof(definition)} is null");

            return GetVariable(definition.Name);
        }

    }
}
