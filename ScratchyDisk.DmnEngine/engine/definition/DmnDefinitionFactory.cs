using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using ScratchyDisk.DmnEngine.Engine.Decisions;
using ScratchyDisk.DmnEngine.Utils;
using ScratchyDisk.DmnEngine.Engine.Decisions.Expression;
using ScratchyDisk.DmnEngine.Engine.Decisions.Table;
using ScratchyDisk.DmnEngine.Engine.Decisions.Table.Definition;
using ScratchyDisk.DmnEngine.Engine.Definition.Extensions;
using ScratchyDisk.DmnEngine.Engine.Definition.Extensions.Diagram;
using ScratchyDisk.DmnEngine.Parser;
using ScratchyDisk.DmnEngine.Parser.Dto;
using ScratchyDisk.DmnEngine.Feel.Types;

namespace ScratchyDisk.DmnEngine.Engine.Definition
{
    /// <summary>
    /// Factory that validates and transforms the <see cref="DmnModel"/> to <see cref="DmnDefinition"/> that can be executed within <see cref="execution.context.DmnExecutionContext"/>
    /// </summary>
    /// <remarks>
    /// As the <see cref="DmnParser"/> is responsible for parsing the XML with the model, the <see cref="DmnDefinitionFactory"/> contains the
    /// main logic of checking the model and preparing it for the execution. It's recommended not to build <see cref="DmnDefinition"/> out of this factory, as
    /// the crucial validations may be missed.
    /// </remarks>
    public class DmnDefinitionFactory
    {
        /// <summary>
        /// Logger
        /// </summary>
        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Default known types recognized by factory
        /// </summary>
        public static readonly Dictionary<string, Type> DefaultKnowTypes = new Dictionary<string, Type>()
        {
            { "any", typeof(object) },
            { "string", typeof(string) },
            { "boolean", typeof(bool) },
            { "integer", typeof(int) },
            { "long", typeof(long) },
            { "double", typeof(double) },
            { "date", typeof(DateTime) },
            { "number", typeof(decimal) },
            { "time", typeof(FeelTime) },
            { "date and time", typeof(DateTimeOffset) },
            { "datetime", typeof(DateTimeOffset) },
            { "days and time duration", typeof(TimeSpan) },
            { "daytimeduration", typeof(TimeSpan) },
            { "years and months duration", typeof(FeelYmDuration) },
            { "yearmonthduration", typeof(FeelYmDuration) },
            { "context", typeof(FeelContext) },
            { "list", typeof(List<object>) }
        };



        /// <summary>
        /// Source DMN Model parsed from XML
        /// </summary>
        protected DmnModel SourceModel { get; }

        /// <summary>
        /// Variables used while executing the DMN model - can be used within the Decision Tables and/or Expressions
        /// In general, it holds the Input Data of DMN model and outputs from Decision Tables and/or Expressions
        /// Dictionary by Variable name
        /// </summary>
        protected Dictionary<string, DmnVariableDefinition> Variables { get; } = new Dictionary<string, DmnVariableDefinition>();

        /// <summary>
        /// Input data interface. Input data will be stored as Variables, complex objects are supported. Dictionary by Input/Variable name
        /// </summary>
        protected Dictionary<string, IDmnVariable> InputData { get; } = new Dictionary<string, IDmnVariable>();

        /// <summary>
        /// Dictionary of available decisions by name
        /// </summary>
        protected Dictionary<string, IDmnDecision> Decisions { get; } = new Dictionary<string, IDmnDecision>();

        /// <summary>
        /// Dictionary of input data variable definitions (by ID)
        /// </summary>
        protected Dictionary<string, DmnVariableDefinition> InputDataById { get; } = new Dictionary<string, DmnVariableDefinition>();

        /// <summary>
        /// Dictionary of decision definitions (by ID)
        /// </summary>
        protected Dictionary<string, IDmnDecision> DecisionsById { get; } = new Dictionary<string, IDmnDecision>();

        /// <summary>
        /// Business knowledge models by name.
        /// </summary>
        protected Dictionary<string, DmnBusinessKnowledgeModel> BusinessKnowledgeModels { get; } =
            new Dictionary<string, DmnBusinessKnowledgeModel>(StringComparer.Ordinal);

        private readonly Dictionary<string, string> itemTypeNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Set of known types that can be used for inputs/variables
        /// </summary>
        private Dictionary<string, Type> knownTypes = new Dictionary<string, Type>();

        /// <summary>Item definitions by name (case-insensitive).</summary>
        private readonly Dictionary<string, ItemDefinition> itemDefinitions =
            new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Resolved item definition types.</summary>
        private readonly Dictionary<string, Type> resolvedItemTypes =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Cycle guard for recursive item definitions.</summary>
        private readonly HashSet<string> resolvingItemTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool itemDefinitionsIndexed;

        /// <summary>
        /// Set of known types that can be used for inputs/variables
        /// </summary>
        public Dictionary<string, Type> KnownTypes
        {
            get
            {
                if (knownTypes == null) knownTypes = new Dictionary<string, Type>();
                if (knownTypes.Count == 0) InitKnownTypes();
                return knownTypes;
            }
        }

        /// <summary>
        /// Protected CTOR
        /// </summary>
        /// <param name="sourceModel">Source DMN Model parsed from XML</param>
        /// <exception cref="ArgumentNullException"><paramref name="sourceModel"/> is null.</exception> 
        protected DmnDefinitionFactory(DmnModel sourceModel)
        {
            SourceModel = sourceModel ?? throw Logger.Fatal<ArgumentNullException>($"{nameof(sourceModel)} is null");
        }


        /// <summary>
        /// Validates the <paramref name="source"/> model and creates the <see cref="DmnDefinition"/> from <see cref="DmnModel"/>
        /// </summary>
        /// <param name="source">Source DMN Model parsed from XML</param>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>    
        /// <returns>DMN model definition that can be used to execute the decisions within the <see cref="execution.context.DmnExecutionContext"/></returns>
        public static DmnDefinition CreateDmnDefinition(DmnModel source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ValidateExpressionLanguages(source);
            var factory = new DmnDefinitionFactory(source);
            return factory.ProcessModel();
        }
        private static void ValidateExpressionLanguages(DmnModel source)
        {
            foreach (var decision in source.Decisions ?? Enumerable.Empty<Decision>())
            {
                ValidateExpressionLanguage(decision.Expression);
                var table = decision.DecisionTable;
                if (table == null) continue;
                foreach (var input in table.Inputs ?? Enumerable.Empty<DecisionTableInput>())
                    ValidateExpressionLanguage(input.InputExpression);
                foreach (var rule in table.Rules ?? Enumerable.Empty<DecisionRule>())
                {
                    foreach (var entry in rule.InputEntries ?? Enumerable.Empty<Expression>())
                        ValidateExpressionLanguage(entry);
                    foreach (var entry in rule.OutputEntries ?? Enumerable.Empty<Expression>())
                        ValidateExpressionLanguage(entry);
                }
            }
        }
        private static void ValidateExpressionLanguage(Expression expression)
        {
            if (string.IsNullOrWhiteSpace(expression?.Language)) return;
            var language = expression.Language.Trim();
            if (string.Equals(language, DmnExpressionLanguage.BFeel, StringComparison.OrdinalIgnoreCase))
            {
                throw Logger.Fatal<DmnParserException>(
                    $"B-FEEL expression language is not supported: {DmnExpressionLanguage.BFeel}. Supported expression language: FEEL.");
            }
            if (string.Equals(language, DmnExpressionLanguage.Onnx, StringComparison.OrdinalIgnoreCase))
            {
                throw Logger.Fatal<DmnParserException>(
                    "ONNX external functions are not supported. ONNX is optional for DMN 1.6 Level 3 conformance and is not implemented.");
            }
        }

        /// <summary>
        /// Validates the <see cref="SourceModel"/> model and creates the <see cref="DmnDefinition"/> from <see cref="DmnModel"/>
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="SourceModel"/> is null.</exception>   
        /// <returns>DMN model definition that can be used to execute the decisions within the <see cref="execution.context.DmnExecutionContext"/></returns>
        /// <summary>
        /// When true (default), the factory detects when DRD input data names differ from
        /// the variable names used in decision table input expressions and creates alias
        /// variables so that input parameter values are propagated correctly at execution time.
        /// This is common in Camunda exports where input data has human-readable names
        /// (e.g. "Number of Years Incorporated") while expressions use camelCase (e.g. "yearsIncorporated").
        /// </summary>
        public static bool ResolveInputExpressionAliases { get; set; } = true;

        protected virtual DmnDefinition ProcessModel()
        {
            if (SourceModel == null) throw Logger.Fatal<InvalidOperationException>($"{nameof(SourceModel)} is null");

            //-----------------------------------------------------------------
            // Pre-process: build input expression alias map and create alias variables
            //-----------------------------------------------------------------
            Dictionary<string, List<string>> inputAliases = null;
            if (ResolveInputExpressionAliases)
            {
                inputAliases = BuildInputExpressionAliases();
            }

            //-----------------------------------------------------------------
            // Process DMN Model
            //-----------------------------------------------------------------

            //Process input data
            if (SourceModel.InputData != null) //it's not common, but OK to have no input data
            {
                ProcessInputDataSource();
            }

            // Create alias variables so that decision table expressions can resolve them
            if (inputAliases != null)
            {
                foreach (var kvp in inputAliases)
                {
                    foreach (var aliasName in kvp.Value)
                    {
                        if (!Variables.ContainsKey(aliasName))
                        {
                            var aliasVariable = new DmnVariableDefinition(aliasName);
                            Variables.Add(aliasName, aliasVariable);
                        }
                    }
                }
            }

            //Process business knowledge models before decisions so expressions can call them
            ProcessBusinessKnowledgeModelsSource();

            //Process decisions
            if (SourceModel.Decisions != null && SourceModel.Decisions.Count > 0)
            {
                foreach (var sourceDecision in SourceModel.Decisions)
                {
                    ProcessDecision(sourceDecision);
                }
            }
            else
            {
                throw Logger.Fatal<DmnParserException>($"No decision in DMN model.");
            }

            //-----------------------------------------------------------------
            // Create DMN Definition
            //-----------------------------------------------------------------
            BuildItemTypeNames();

            var definition = new DmnDefinition(
                Variables.ToDictionary(
                    v => v.Key,
                    v => (IDmnVariable)v.Value),
                Decisions,
                BusinessKnowledgeModels,
                itemTypeNames);

            // Attach the alias map to the definition
            if (inputAliases != null && inputAliases.Count > 0)
            {
                definition.InputExpressionAliases = inputAliases.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (IReadOnlyList<string>)kvp.Value.AsReadOnly());
            }

            //-----------------------------------------------------------------
            // Process extensions
            //-----------------------------------------------------------------

            // Process diagram (and shape) extension
            if (SourceModel.DmnVersion >= DmnParser.DmnVersionEnum.V1_3)
            {
                ProcessDiagramExtension(definition);
            }

            return definition;
        }

        /// <summary>
        /// Walks the model's decisions to find cases where DRD input data names differ
        /// from the variable names used in decision table input expressions.
        /// Returns a map: normalized input data name → list of alias expression variable names.
        /// </summary>
        protected virtual Dictionary<string, List<string>> BuildInputExpressionAliases()
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            if (SourceModel.InputData == null || SourceModel.Decisions == null) return map;

            // Build inputData ID → normalized name lookup
            var inputIdToNormalized = new Dictionary<string, string>();
            foreach (var input in SourceModel.InputData)
            {
                var inputName = !string.IsNullOrWhiteSpace(input.Name) ? input.Name : input.Id;
                if (DmnVariableDefinition.CanNormalizeVariableName(inputName, out var normalized, out _))
                {
                    inputIdToNormalized[input.Id] = normalized;
                }
            }

            // Build a lookup of all expression variable names and labels across all decision table inputs
            var tableInfoByExprName = new Dictionary<string, (string typeRef, string tableLabel)>(StringComparer.OrdinalIgnoreCase);
            var tableInfoByLabel = new Dictionary<string, (string exprName, string typeRef)>(StringComparer.OrdinalIgnoreCase);

            foreach (var decision in SourceModel.Decisions)
            {
                if (decision.DecisionTable?.Inputs == null) continue;
                foreach (var tableInput in decision.DecisionTable.Inputs)
                {
                    var exprText = tableInput.InputExpression?.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(exprText) &&
                        DmnVariableDefinition.CanNormalizeVariableName(exprText, out var exprNormalized, out _))
                    {
                        if (!tableInfoByExprName.ContainsKey(exprNormalized))
                            tableInfoByExprName[exprNormalized] = (tableInput.InputExpression.TypeRef, tableInput.Label);
                    }
                    if (!string.IsNullOrWhiteSpace(tableInput.Label) && !string.IsNullOrWhiteSpace(exprText))
                    {
                        if (!tableInfoByLabel.ContainsKey(tableInput.Label))
                        {
                            DmnVariableDefinition.CanNormalizeVariableName(exprText, out var en, out _);
                            tableInfoByLabel[tableInput.Label] = (en, tableInput.InputExpression?.TypeRef);
                        }
                    }
                }
            }

            // For each input data element, try to find a matching expression variable
            foreach (var (id, normalizedName) in inputIdToNormalized)
            {
                var inputLabel = SourceModel.InputData.FirstOrDefault(i => i.Id == id)?.Name;

                // Try: normalized name matches expression variable name (case-sensitive check for alias)
                if (tableInfoByExprName.TryGetValue(normalizedName, out _))
                {
                    // Check case-sensitive: if they're identical, no alias needed
                    var exactKey = tableInfoByExprName.Keys.FirstOrDefault(k =>
                        string.Equals(k, normalizedName, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(k, normalizedName, StringComparison.Ordinal));
                    if (exactKey != null) AddAlias(map, normalizedName, exactKey);
                    continue;
                }

                // Try: input label matches a table label → get the expression name
                if (!string.IsNullOrWhiteSpace(inputLabel) && tableInfoByLabel.TryGetValue(inputLabel, out var info))
                {
                    if (info.exprName != null && !string.Equals(info.exprName, normalizedName, StringComparison.Ordinal))
                        AddAlias(map, normalizedName, info.exprName);
                    continue;
                }

                // Try: single-input-data decisions via information requirement chain
                foreach (var decision in SourceModel.Decisions)
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
                        foreach (var tableInput in decision.DecisionTable.Inputs)
                        {
                            var exprText = tableInput.InputExpression?.Text?.Trim();
                            if (string.IsNullOrWhiteSpace(exprText)) continue;
                            if (!DmnVariableDefinition.CanNormalizeVariableName(exprText, out var exprNormalized, out _)) continue;
                            if (!string.Equals(exprNormalized, normalizedName, StringComparison.Ordinal))
                            {
                                AddAlias(map, normalizedName, exprNormalized);
                                break;
                            }
                        }
                        if (map.ContainsKey(normalizedName)) break;
                    }
                }
            }

            return map;
        }

        private static void AddAlias(Dictionary<string, List<string>> map, string inputName, string aliasName)
        {
            if (!map.TryGetValue(inputName, out var list))
            {
                list = new List<string>();
                map[inputName] = list;
            }
            if (!list.Contains(aliasName)) list.Add(aliasName);
        }

        /// <summary>
        /// Builds executable business knowledge models from the parsed model.
        /// Only literal-expression bodies are supported; other function bodies are
        /// left out rather than failing the whole model.
        /// </summary>
        protected virtual void ProcessBusinessKnowledgeModelsSource()
        {
            if (SourceModel?.BusinessKnowledgeModels == null) return;

            foreach (var source in SourceModel.BusinessKnowledgeModels)
            {
                var name = !string.IsNullOrWhiteSpace(source.Name) ? source.Name : source.Id;
                if (string.IsNullOrWhiteSpace(name)) continue;

                var logic = source.EncapsulatedLogic;
                if (logic?.Expression == null || string.IsNullOrWhiteSpace(logic.Expression.Text)) continue;

                var parameters = logic.FormalParameters?
                    .Select(p => p.Name)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList() ?? new List<string>();

                BusinessKnowledgeModels[name] = new DmnBusinessKnowledgeModel(
                    name,
                    name,
                    parameters,
                    logic.Expression.Text,
                    logic.Expression.Language);
            }
        }

        /// <summary>
        /// Validates the inputs of the <see cref="DmnModel"/> (<see cref="SourceModel"/>)
        /// and populates <see cref="InputData"/>, <see cref="InputDataById"/> and related <see cref="Variables"/>
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="SourceModel"/>, <see cref="InputData"/> is null.</exception>
        /// <exception cref="DmnParserException">Missing input id</exception> 
        /// <exception cref="DmnParserException">Duplicate input data name</exception> 
        protected virtual void ProcessInputDataSource()
        {
            if (SourceModel == null) throw Logger.Fatal<InvalidOperationException>($"{nameof(SourceModel)} is null");
            if (InputDataById == null) throw Logger.Fatal<InvalidOperationException>($"{nameof(InputDataById)} is null");
            if (SourceModel.InputData == null || SourceModel.InputData.Count == 0) return;//it's not common, but OK to have no input data

            //TODO    ?Input name in form varName:varType for (complex) input types
            foreach (var sourceInput in SourceModel.InputData)
            {
                var inputId = sourceInput.Id;
                if (string.IsNullOrWhiteSpace(inputId))
                {
                    // An id is optional in the schema. Synthesize a stable key so the
                    // model can still be executed (it simply cannot be referenced).
                    inputId = $"input_{InputDataById.Count + 1}";
                    while (InputDataById.ContainsKey(inputId)) inputId += "_";
                }

                var inputName = !string.IsNullOrWhiteSpace(sourceInput.Name) ? sourceInput.Name : inputId;
                var variableName = DmnVariableDefinition.NormalizeVariableName(inputName);
                if (InputData.ContainsKey(variableName))
                    throw Logger.Fatal<DmnParserException>($"Duplicate input data name {variableName} (from {inputName})");

                var variable = new DmnVariableDefinition(variableName, inputName, label: inputName);
                InputData.Add(variableName, variable);
                Variables.Add(variableName, variable);
                InputDataById.Add(inputId, variable);
            }
        }

        /// <summary>
        /// Validates the <paramref name="sourceDecision"/> within the <see cref="DmnModel"/>
        /// and creates <see cref="DmnExpressionDecision"/> or <see cref="DmnDecisionTable"/>.
        /// Populates <see cref="Decisions"/> and <see cref="DecisionsById"/>
        /// </summary>
        /// <param name="sourceDecision">Decision source parsed from XML</param>
        /// <exception cref="ArgumentNullException"><paramref name="sourceDecision"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><see cref="SourceModel"/>, <see cref="InputData"/> or <see cref="DecisionsById"/> is null.</exception>
        /// <exception cref="DmnParserException">Missing decision id</exception> 
        /// <exception cref="DmnParserException">Duplicate decision name</exception> 
        /// <exception cref="DmnParserException">Missing required decision reference</exception> 
        /// <exception cref="DmnParserException">Decision with given reference id not found</exception> 
        /// <exception cref="DmnParserException">Missing required input reference for</exception> 
        /// <exception cref="DmnParserException">Input with given reference id not found</exception> 
        protected virtual IDmnDecision ProcessDecision(Decision sourceDecision)
        {
            if (sourceDecision == null) throw Logger.Fatal<ArgumentNullException>($"{nameof(sourceDecision)} is null");
            if (SourceModel == null) throw Logger.Fatal<InvalidOperationException>($"{nameof(SourceModel)} is null");
            if (InputDataById == null) throw Logger.Fatal<InvalidOperationException>($"{nameof(InputDataById)} is null");
            if (DecisionsById == null) throw Logger.Fatal<InvalidOperationException>($"{nameof(DecisionsById)} is null");

            var allSourceDecisions = SourceModel.Decisions ?? new List<Decision>(); //All decisions parsed from XML source

            var decisionId = sourceDecision.Id;
            if (string.IsNullOrWhiteSpace(decisionId))
            {
                // An id is optional in the schema. Synthesize one so the decision can
                // be executed; it cannot be referenced by other decisions.
                decisionId = $"decision_{DecisionsById.Count + 1}";
                while (DecisionsById.ContainsKey(decisionId)) decisionId += "_";
            }
            else if (DecisionsById.ContainsKey(decisionId))
            {
                return DecisionsById[decisionId]; //already processed
            }

            var decisionName = !string.IsNullOrWhiteSpace(sourceDecision.Name) ? sourceDecision.Name : decisionId;
            if (Decisions.ContainsKey(decisionName)) throw Logger.Fatal<DmnParserException>($"Duplicate decision name {decisionName}");

            var requiredInputs = new List<IDmnVariable>();
            var requiredDecisions = new List<IDmnDecision>();
            //check dependencies
            if (sourceDecision.InformationRequirements != null)
            {
                foreach (var dependsOnDecisionRef in sourceDecision.InformationRequirements.Where(i => i.RequirementType == InformationRequirementType.Decision))
                {
                    var refId = dependsOnDecisionRef.Ref;
                    if (string.IsNullOrWhiteSpace(refId)) throw Logger.Fatal<DmnParserException>($"Missing required decision reference for {decisionName}");

                    if (DecisionsById.TryGetValue(refId, out var requiredDecision))
                    {
                        /* since v1.1 only directly required inputs are added and GetAllRequiredXxx methods are added to Definition
                        //decision already processed, just add "new" inputs
                        AddNewRequiredInputs(requiredDecision.RequiredInputs, requiredInputs);
                        */

                        //decision already processed
                        requiredDecisions.Add(requiredDecision);
                        continue;
                    }

                    var sourceDependsOnDecision = allSourceDecisions.FirstOrDefault(d => d.Id == refId);
                    if (sourceDependsOnDecision == null) throw Logger.Fatal<DmnParserException>($"Decision with reference {refId} for {decisionName} not found");

                    var dependsOnDecision = ProcessDecision(sourceDependsOnDecision); //process required decision first

                    //since v1.1 only directly required inputs are added and GetAllRequiredXxxx methods are added to Definition
                    //AddNewRequiredInputs(dependsOnDecision.RequiredInputs, requiredInputs);
                    
                    if(dependsOnDecision!=null) requiredDecisions.Add(dependsOnDecision); //when null, it was ignored (not processed)
                }
            }

            //validate input references
            if (sourceDecision.InformationRequirements != null)
            {
                var newInputs = new List<DmnVariableDefinition>();
                foreach (var informationRequirement in sourceDecision.InformationRequirements.Where(d => d.RequirementType == InformationRequirementType.Input))
                {
                    var refId = informationRequirement.Ref;
                    if (string.IsNullOrWhiteSpace(refId)) throw Logger.Fatal<DmnParserException>($"Missing required input reference for {decisionName}");
                    if (!InputDataById.ContainsKey(refId)) throw Logger.Fatal<DmnParserException>($"Input with reference {refId} for {decisionName} not found");
                    newInputs.Add(InputDataById[refId]);
                }
                AddNewRequiredInputs(newInputs, requiredInputs);
            }

            //Decision factory
            IDmnDecision decision=null;
            if (sourceDecision.DecisionTable != null)
            {
                //decision table
                decision = CreateDecisionTable(sourceDecision.DecisionTable, decisionName, requiredInputs, requiredDecisions);
            }
            else if (sourceDecision.Expression != null)
            {
                //expression
                decision = CreateExpressionDecision(sourceDecision, decisionName, requiredInputs, requiredDecisions);
            }else 
            {
                //unsupported decision type
            }

            if (decision != null)
            {
                Decisions.Add(decisionName, decision);
                DecisionsById.Add(decisionId, decision);
            }

            return decision;
            

        }

        /// <summary>
        /// Validates the <paramref name="sourceTable"/> within the <see cref="DmnModel"/>
        /// and creates <see cref="DmnDecisionTable"/>
        /// </summary>
        /// <param name="sourceTable">Decision source parsed from XML</param>
        /// <param name="decisionName">Unique name of decision</param>
        /// <param name="requiredInputs">Inputs required by decision</param>
        /// <param name="requiredDecisions">Decisions, the decision table depends on</param>
        /// <exception cref="ArgumentNullException"><paramref name="sourceTable"/>, <paramref name="decisionName"/>, <paramref name="requiredInputs"/> or <paramref name="requiredDecisions"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="decisionName"/> is empty</exception>    
        /// <exception cref="DmnParserException">No outputs for decision table</exception> 
        /// <exception cref="DmnParserException">No inputs for decision table</exception> 
        /// <exception cref="DmnParserException">Input variable for decision table not found</exception> 
        /// <exception cref="DmnParserException">Input for decision table has no source</exception>
        /// <exception cref="DmnParserException">No rules for decision table</exception> 
        /// <exception cref="DmnParserException">Number of rule input entries doesn't match the number of inputs for decision table</exception> 
        /// <exception cref="DmnParserException">Number of rule output entries doesn't match the number of outputs for decision table</exception> 
        protected virtual DmnDecisionTable CreateDecisionTable(
            DecisionTable sourceTable,
            string decisionName,
            List<IDmnVariable> requiredInputs,
            List<IDmnDecision> requiredDecisions)
        {
            if (sourceTable == null) throw Logger.Fatal<ArgumentNullException>($"{nameof(sourceTable)} is null");
            if (decisionName == null) throw Logger.Fatal<ArgumentNullException>($"{nameof(decisionName)} is null");
            if (string.IsNullOrWhiteSpace(decisionName)) throw Logger.Fatal<ArgumentException>($"{nameof(decisionName)} is empty");
            if (requiredInputs == null) throw Logger.Fatal<ArgumentNullException>($"{nameof(requiredInputs)} is null");
            if (requiredDecisions == null) throw Logger.Fatal<ArgumentNullException>($"{nameof(requiredDecisions)} is null");

            //prepare outputs
            if (sourceTable.Outputs == null || sourceTable.Outputs.Count < 1)
                throw Logger.Fatal<DmnParserException>($"No outputs for decision table {decisionName}");

            var outputs = new List<DmnDecisionTableOutput>();
            var outputIdx = 0;
            foreach (var sourceOutput in sourceTable.Outputs)
            {
                string sourceVariableName;
                if (SourceModel.DmnVersion < DmnParser.DmnVersionEnum.V1_3ext)
                {
                    //legacy mapping pre V1_3ext
                    //Label, Name, Id
                    if (!string.IsNullOrWhiteSpace(sourceOutput.Label))
                        sourceVariableName = sourceOutput.Label;
                    else if (!string.IsNullOrWhiteSpace(sourceOutput.Name))
                        sourceVariableName = sourceOutput.Name;
                    else
                        sourceVariableName = sourceOutput.Id;
                }
                else
                {
                    //mapping since V1_3ext
                    //Name,Label, Id
                    sourceVariableName =
                        !string.IsNullOrWhiteSpace(sourceOutput.Name) ? sourceOutput.Name :
                        !string.IsNullOrWhiteSpace(sourceOutput.Label) ? sourceOutput.Label :
                        sourceOutput.Id;
                }

                var variableName = DmnVariableDefinition.NormalizeVariableName(sourceVariableName);
                if (!Variables.TryGetValue(variableName, out var variable))
                {
                    //create variable
                    variable = new DmnVariableDefinition(variableName);
                    Variables.Add(variableName, variable);
                }

                variable.AddValueSetter($"Table Decision {decisionName}");
                CheckAndUpdateVariableType(variable, sourceOutput.TypeRef);

                var allowedValues = sourceOutput.AllowedOutputValues?.Values;
                var label =
                    !string.IsNullOrWhiteSpace(sourceOutput.Label) ? sourceOutput.Label :
                    !string.IsNullOrWhiteSpace(sourceOutput.Name) ? sourceOutput.Name :
                    null;//label??name??null(will default to Output#n)

                outputs.Add(new DmnDecisionTableOutput(outputIdx, variable, allowedValues, label));
                outputIdx++;
            }

            //prepare inputs
            if (sourceTable.Inputs == null || sourceTable.Inputs.Count < 1)
                throw Logger.Fatal<DmnParserException>($"No inputs for decision table {decisionName}");

            var inputs = new List<DmnDecisionTableInput>();
            var inputIdx = 0;
            foreach (var sourceInput in sourceTable.Inputs)
            {
                //Expression or  variable by Label,  Id
                DmnVariableDefinition variable = null;


                var xmlId = sourceInput.Id;
                var xmlLabel = sourceInput.Label;
                var xmlInputVariable = sourceInput.InputVariable; //new in v1.1
                var xmlExpressionText = sourceInput.InputExpression?.Text;

                bool isVariableInput;
                string sourceVariableName, variableName, expression;

                if (SourceModel.DmnVersion < DmnParser.DmnVersionEnum.V1_3ext)
                {
                    //legacy mapping pre V1_3ext
                    isVariableInput = string.IsNullOrWhiteSpace(xmlExpressionText);
                    if (isVariableInput)
                    {
                        sourceVariableName = !string.IsNullOrWhiteSpace(xmlLabel) ? xmlLabel : xmlId;
                        variableName = DmnVariableDefinition.NormalizeVariableName(sourceVariableName);
                        expression = null;
                    }
                    else
                    {
                        sourceVariableName = null;
                        variableName = null;
                        expression = xmlExpressionText;
                    }
                }
                else
                {
                    //mapping since V1_3ext
                    if (!string.IsNullOrWhiteSpace(xmlInputVariable))
                    {
                        isVariableInput = true;
                        sourceVariableName = xmlInputVariable;
                        variableName = DmnVariableDefinition.NormalizeVariableName(sourceVariableName);
                        expression = null;
                    }
                    else if ( //first "wins" (expression vs label) or it goes to fall back (ID)
                        !CheckTableInputForVariable(xmlExpressionText, Variables, out isVariableInput, out sourceVariableName, out variableName, out expression) &&
                        !CheckTableInputForVariable(xmlLabel, Variables, out isVariableInput, out sourceVariableName, out variableName, out expression))
                    {
                        //fall back - ID is a must, but can be only variable name
                        CheckTableInputForVariable(xmlId, Variables, out isVariableInput, out sourceVariableName, out variableName, out expression);
                        if (!isVariableInput)
                        {
                            //Error - don't use the ID as expression
                            throw Logger.Fatal<DmnParserException>(
                                $"Input#{inputIdx + 1} for decision table {decisionName} has no source");
                        }
                    }
                }

                if (isVariableInput)
                {
                    if (!Variables.TryGetValue(variableName, out variable))
                    {
                        throw Logger.Fatal<DmnParserException>(
                            $"Input variable {sourceVariableName} ({variableName}) for decision table {decisionName} not found");
                    }

                    CheckAndUpdateVariableType(variable, sourceInput.InputExpression?.TypeRef);
                }

                var allowedValues = sourceInput.AllowedInputValues?.Values;
                var inputLabel = !string.IsNullOrWhiteSpace(xmlLabel) ? xmlLabel : isVariableInput ? sourceVariableName : null; //label??variable??null(will default to Input#n)

                inputs.Add(new DmnDecisionTableInput(
                    inputIdx, variable, expression, allowedValues, inputLabel,
                    DmnExpressionLanguage.Resolve(sourceInput.InputExpression?.Language)));
                inputIdx++;
            }

            //prepare rules
            if (sourceTable.Rules == null || sourceTable.Rules.Count < 1)
                throw Logger.Fatal<DmnParserException>($"No rules for decision table {decisionName}");

            var rules = new List<DmnDecisionTableRule>();
            var ruleIdx = 0;
            foreach (var sourceRule in sourceTable.Rules)
            {
                if (sourceRule.InputEntries == null || sourceRule.InputEntries.Count != inputs.Count)
                    throw Logger.Fatal<DmnParserException>($"Number of input entries doesn't match the number of inputs for decision table {decisionName}. Rule Id:{sourceRule.Id} ");

                if (sourceRule.OutputEntries == null || sourceRule.OutputEntries.Count != outputs.Count)
                    throw Logger.Fatal<DmnParserException>($"Number of output entries doesn't match the number of outputs for decision table {decisionName}. Rule Id:{sourceRule.Id} ");

                var ruleInputs = new List<DmnDecisionTableRuleInput>();
                var ruleOutputs = new List<DmnDecisionTableRuleOutput>();
                var ruleName = string.IsNullOrWhiteSpace(sourceRule.Label) ? sourceRule.Id : sourceRule.Label;

                //inputs
                var ruleInputIdx = 0;
                foreach (var sourceRuleInputEntry in sourceRule.InputEntries)
                {
                    if (!string.IsNullOrWhiteSpace(sourceRuleInputEntry.Text) && sourceRuleInputEntry.Text != "-") //don't create rule inputs with no condition - can be also represented as single dash
                    {
                        var ruleInput = new DmnDecisionTableRuleInput(
                            inputs[ruleInputIdx], sourceRuleInputEntry.Text,
                            DmnExpressionLanguage.Resolve(sourceRuleInputEntry.Language));
                        ruleInputs.Add(ruleInput);
                    }
                    ruleInputIdx++;
                }

                //outputs
                var ruleOutputIdx = 0;
                foreach (var sourceRuleOutputEntry in sourceRule.OutputEntries)
                {
                    var ruleOutput = new DmnDecisionTableRuleOutput(
                        outputs[ruleOutputIdx], sourceRuleOutputEntry.Text,
                        DmnExpressionLanguage.Resolve(sourceRuleOutputEntry.Language));
                    ruleOutputs.Add(ruleOutput);
                    ruleOutputIdx++;
                }

                rules.Add(new DmnDecisionTableRule(ruleIdx, ruleName, ruleInputs.ToArray(), ruleOutputs.ToArray(), sourceRule.Description));
                ruleIdx++;
            }

            var decisionTable = new DmnDecisionTable(
                decisionName,
                sourceTable.HitPolicy, sourceTable.Aggregation,
                inputs.ToArray(), outputs.ToArray(), rules.ToArray(),
                requiredInputs, requiredDecisions,
                label: decisionName);
            return decisionTable;
        }



        /// <summary>
        /// Validates the <paramref name="sourceDecision"/> within the <see cref="DmnModel"/>
        /// and creates <see cref="DmnExpressionDecision"/>
        /// </summary>
        /// <param name="sourceDecision">Decision source parsed from XML</param>
        /// <param name="decisionName">Unique name of decision</param>
        /// <param name="requiredInputs">Inputs required by decision</param>
        /// <param name="requiredDecisions">Decisions, the decision table depends on</param>
        /// <exception cref="ArgumentNullException"><paramref name="sourceDecision"/>, <paramref name="decisionName"/>, <paramref name="requiredInputs"/> or <paramref name="requiredDecisions"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="decisionName"/> is empty</exception>    
        /// <exception cref="DmnParserException">Missing expression for expression decision</exception> 
        /// <exception cref="DmnParserException">Missing output variable for expression decision</exception> 
        protected virtual DmnExpressionDecision CreateExpressionDecision(
           Decision sourceDecision,
           string decisionName,
           IReadOnlyCollection<IDmnVariable> requiredInputs,
           IReadOnlyCollection<IDmnDecision> requiredDecisions)
        {
            if (sourceDecision == null) throw Logger.Fatal<ArgumentNullException>($"{nameof(sourceDecision)} is null");
            if (decisionName == null) throw Logger.Fatal<ArgumentNullException>($"{nameof(decisionName)} is null");
            if (string.IsNullOrWhiteSpace(decisionName)) throw Logger.Fatal<ArgumentException>($"{nameof(decisionName)} is empty");
            if (requiredInputs == null) throw Logger.Fatal<ArgumentNullException>($"{nameof(requiredInputs)} is null");
            if (requiredDecisions == null) throw Logger.Fatal<ArgumentNullException>($"{nameof(requiredDecisions)} is null");

            //prepare output variable
            if (sourceDecision.OutputVariable == null) throw Logger.Fatal<DmnParserException>($"Missing output variable for expression decision {decisionName}");
            var sourceVariableName = !string.IsNullOrWhiteSpace(sourceDecision.OutputVariable.Name)
                ? sourceDecision.OutputVariable.Name
                : sourceDecision.OutputVariable.Id;
            var variableName = DmnVariableDefinition.NormalizeVariableName(sourceVariableName);
            if (!Variables.TryGetValue(variableName, out var variable))
            {
                //create variable
                variable = new DmnVariableDefinition(variableName, label: sourceVariableName);
                Variables.Add(variableName, variable);
            }

            variable.AddValueSetter($"Expression Decision {decisionName}");
            CheckAndUpdateVariableType(variable, sourceDecision.OutputVariable.TypeRef);

            //prepare expression
            if (string.IsNullOrWhiteSpace(sourceDecision.Expression.Text))
                throw Logger.Fatal<DmnParserException>($"Missing expression for expression decision {decisionName}");
            var expr = $"{sourceDecision.Expression.Text}";

            //create
            var expressionDecision = new DmnExpressionDecision(
                decisionName, expr, variable, requiredInputs, requiredDecisions,
                label: decisionName,
                dialect: DmnExpressionLanguage.Resolve(sourceDecision.Expression.Language));
            return expressionDecision;
        }

        /// <summary>
        /// Checks whether <param name="source"> is either not set (null or whitespace) or variable or expression.</param>
        /// </summary>
        /// <param name="source">Source string to check</param>
        /// <param name="knownVariables">Dictionary of know variables</param>
        /// <param name="isVariable">Out: Flag whether the <paramref name="source"/> is set and represents variable</param>
        /// <param name="sourceVariableName">Out: Source variable name (before normalization) when <paramref name="source"/> is set and represents variable</param>
        /// <param name="variableName">Out: Variable name when <paramref name="source"/> is set and represents variable</param>
        /// <param name="expression">Out: Expression when <paramref name="source"/> is set and represents expression</param>
        /// <returns>True when <param name="source"> is not null or whitespace, so it represents variable or expression</param></returns>
        /// <exception cref="ArgumentNullException"><paramref name="knownVariables"/> is null</exception>
        protected virtual bool CheckTableInputForVariable(
            string source,
            Dictionary<string, DmnVariableDefinition> knownVariables,
            out bool isVariable,
            out string sourceVariableName,
            out string variableName,
            out string expression)
        {
            if (knownVariables is null) throw new ArgumentNullException(nameof(knownVariables));

            if (string.IsNullOrWhiteSpace(source))
            {
                isVariable = false;
                sourceVariableName = null;
                variableName = null;
                expression = null;
                return false;
            }

            if (DmnVariableDefinition.CanNormalizeVariableName(source, out var potentialVariableName, out _) &&
                Variables.TryGetValue(potentialVariableName, out _))
            {
                //source is know variable name -> variable
                isVariable = true;
                sourceVariableName = source;
                variableName = potentialVariableName;
                expression = null;
                return true;
            }

            isVariable = false;
            sourceVariableName = null;
            variableName = null;
            expression = source;
            return true;
        }

        /// <summary>
        /// Adds <paramref name="newInputs"/> into the list of <paramref name="requiredInputs"/>.
        /// <paramref name="newInputs"/> can be null or empty (no addition then)
        /// </summary>
        /// <param name="newInputs"></param>
        /// <param name="requiredInputs"></param>
        /// <exception cref="ArgumentNullException"><paramref name="requiredInputs"/> is null.</exception>
        protected virtual void AddNewRequiredInputs(IReadOnlyCollection<IDmnVariable> newInputs, List<IDmnVariable> requiredInputs)
        {
            if (requiredInputs == null) throw Logger.Fatal<ArgumentNullException>($"{nameof(requiredInputs)} is null.");
            if (newInputs == null || !newInputs.Any()) return;

            foreach (var requiredInput in newInputs)
            {
                if (!requiredInputs.Exists(i => i.Name == requiredInput.Name))
                    requiredInputs.Add(requiredInput);
            }
        }

        /// <summary>
        /// Assigns the <paramref name="variable"/> with <see cref="Type"/> corresponding to the <paramref name="typeName"/>.
        /// When the variable already contains the type information, it must match with the <paramref name="typeName"/>
        /// </summary>
        /// <param name="variable">Variable to check or update. If null, function returns without an exception</param>
        /// <param name="typeName">Type name to assign. If null or empty, function returns without an exception (assuming that the type is not know yet)</param>
        /// <exception cref="DmnParserException">Type for variable don't match the one assigned before</exception>
        protected virtual void CheckAndUpdateVariableType(DmnVariableDefinition variable, string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return;
            if (variable == null) return;

            var parsedType = ParseTypeName(typeName);
            if (variable.Type == null)
            {
                //set (update)
                variable.SetRecognizedType(parsedType);
            }
            else
            {
                //check
                if (variable.Type != parsedType)
                    throw Logger.Fatal<DmnParserException>($"Types for variable {variable.Name} don't match: {variable.Type} vs. {parsedType}");
            }
        }


        /// <summary>
        /// Initialize set of known types that can be used in DMN definition for inputs/variables
        /// </summary>
        protected virtual void InitKnownTypes()
        {
            knownTypes.Clear();
            foreach (var defaultKnowType in DefaultKnowTypes)
            {
                knownTypes.Add(defaultKnowType.Key, defaultKnowType.Value);

            }

            EnsureItemDefinitionsIndexed();
        }

        /// <summary>
        /// Indexes the model's named item definitions so they can be resolved as
        /// variable types. Resolution is lazy and supports aliases, structures and
        /// collections, including recursive references.
        /// </summary>
        private void BuildItemTypeNames()
        {
            foreach (var item in SourceModel?.ItemDefinitions ?? Enumerable.Empty<ItemDefinition>())
            {
                if (string.IsNullOrWhiteSpace(item.Name)) continue;
                itemTypeNames[item.Name] = ItemFeelTypeName(item, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
        }

        private string ItemFeelTypeName(ItemDefinition item, HashSet<string> visiting)
        {
            if (item == null) return "any";
            if (!visiting.Add(item.Name ?? "")) return "any";
            try
            {
                if (item.IsCollection)
                    return $"list<{ElementFeelTypeName(item, visiting)}>";

                if (item.ItemComponents != null && item.ItemComponents.Count > 0)
                {
                    var fields = item.ItemComponents.Select(c => $"{c.Name}:{ComponentFeelTypeName(c, visiting)}");
                    return $"context<{string.Join(",", fields)}>";
                }

                return TypeRefFeelTypeName(item.TypeRef, visiting);
            }
            finally
            {
                visiting.Remove(item.Name ?? "");
            }
        }

        private string ElementFeelTypeName(ItemDefinition item, HashSet<string> visiting)
        {
            if (item.ItemComponents != null && item.ItemComponents.Count > 0)
            {
                var fields = item.ItemComponents.Select(c => $"{c.Name}:{ComponentFeelTypeName(c, visiting)}");
                return $"context<{string.Join(",", fields)}>";
            }
            return TypeRefFeelTypeName(item.TypeRef, visiting);
        }

        private string ComponentFeelTypeName(ItemComponent component, HashSet<string> visiting)
        {
            var inner = TypeRefFeelTypeName(component.TypeRef, visiting);
            return component.IsCollection ? $"list<{inner}>" : inner;
        }

        private string TypeRefFeelTypeName(string typeRef, HashSet<string> visiting)
        {
            if (string.IsNullOrWhiteSpace(typeRef)) return "any";
            EnsureItemDefinitionsIndexed();
            if (itemDefinitions.TryGetValue(typeRef, out var referenced))
                return ItemFeelTypeName(referenced, visiting);
            return ClrFeelTypeName(ParseTypeName(typeRef));
        }

        private static string ClrFeelTypeName(Type type)
        {
            if (type == typeof(decimal) || type == typeof(int) || type == typeof(long) || type == typeof(double)) return "number";
            if (type == typeof(string)) return "string";
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(DateOnly) || type == typeof(DateTime)) return "date";
            if (type == typeof(DateTimeOffset) || type == typeof(FeelDateTime)) return "date and time";
            if (type == typeof(FeelTime)) return "time";
            if (type == typeof(TimeSpan)) return "days and time duration";
            if (type == typeof(FeelYmDuration)) return "years and months duration";
            if (type == typeof(FeelContext)) return "context";
            if (type == typeof(List<object>)) return "list";
            return "any";
        }

        private void EnsureItemDefinitionsIndexed()
        {
            if (itemDefinitionsIndexed) return;
            itemDefinitionsIndexed = true;

            if (SourceModel?.ItemDefinitions == null) return;
            foreach (var item in SourceModel.ItemDefinitions)
            {
                if (!string.IsNullOrWhiteSpace(item.Name))
                    itemDefinitions[item.Name] = item;
            }
        }

        /// <summary>
        /// Resolves an item definition to the CLR type used by the engine.
        /// Collections become FEEL lists, structures become FEEL contexts.
        /// </summary>
        private Type ResolveItemDefinition(ItemDefinition item)
        {
            var key = item.Name;
            if (resolvedItemTypes.TryGetValue(key, out var resolved)) return resolved;

            // Recursive definitions are legal; stop the recursion with "any".
            if (!resolvingItemTypes.Add(key)) return typeof(object);

            try
            {
                if (item.IsCollection)
                {
                    resolved = typeof(List<object>);
                }
                else if (item.ItemComponents != null && item.ItemComponents.Count > 0)
                {
                    resolved = typeof(FeelContext);
                }
                else if (!string.IsNullOrWhiteSpace(item.TypeRef))
                {
                    resolved = ParseTypeName(item.TypeRef);
                }
                else
                {
                    resolved = typeof(object);
                }

                resolvedItemTypes[key] = resolved;
                return resolved;
            }
            finally
            {
                resolvingItemTypes.Remove(key);
            }
        }

        /// <summary>
        /// Parses the type name to <see cref="Type"/>
        /// </summary>
        /// <param name="typeName">Type name to parse</param>
        /// <returns><see cref="Type"/> corresponding to <paramref name="typeName"/></returns>
        /// <exception cref="ArgumentException"><paramref name="typeName"/> is null or empty</exception>
        /// <exception cref="DmnParserException">Unsupported type name</exception>
        public virtual Type ParseTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) throw Logger.Fatal<ArgumentException>($"{nameof(typeName)} is null or empty");

            typeName = typeName.ToLower();
            if (KnownTypes.TryGetValue(typeName, out var type)) return type;

            EnsureItemDefinitionsIndexed();
            if (itemDefinitions.TryGetValue(typeName, out var item))
                return ResolveItemDefinition(item);

            throw Logger.Fatal<DmnParserException>($"Unsupported type name {typeName}");
        }

        /// <summary>
        /// When the <see cref="SourceModel"/> has the DI diagram data, it gets individual shapes having bounds and tries to find them
        /// within <see cref="InputDataById"/> and <see cref="DecisionsById"/>. When there is a match, it creates <see cref="DiDiagramShapeExtension"/>
        /// attached to the element. All matched bounds are also added to <see cref="DiDiagramExtension"/> attached to the definition
        /// </summary>
        /// <param name="dmnDefinition">DMN Definition built by factory</param>
        /// <exception cref="ArgumentNullException"><paramref name="dmnDefinition"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><see cref="SourceModel"/>, <see cref="InputData"/> or <see cref="DecisionsById"/> is null.</exception>
        protected virtual void ProcessDiagramExtension(DmnDefinition dmnDefinition)
        {
            if (dmnDefinition == null) throw Logger.Fatal<ArgumentNullException>(nameof(dmnDefinition));
            if (SourceModel == null) throw Logger.Fatal<InvalidOperationException>($"{nameof(SourceModel)} is null");
            if (InputDataById == null) throw Logger.Fatal<InvalidOperationException>($"{nameof(InputDataById)} is null");
            if (DecisionsById == null) throw Logger.Fatal<InvalidOperationException>($"{nameof(DecisionsById)} is null");

            if (SourceModel.DiagramExtension?.Diagrams is null) return;

            var elements = new Dictionary<string, IDmnElement>();
            InputDataById.ToList().ForEach(x => elements.Add(x.Key, x.Value));
            DecisionsById.ToList().ForEach(x => elements.Add(x.Key, x.Value));

            var diagramExtension = new DiDiagramExtension();

            foreach (var shape in SourceModel
                         .DiagramExtension
                         .Diagrams
                         .Where(d => d.Shapes != null)
                         .SelectMany(d => d.Shapes)
                         .Where(s => s.Bounds != null && !string.IsNullOrWhiteSpace(s.DmnElementRef)))
            {
                var elementRef = shape.DmnElementRef;
                var bounds = shape.Bounds;
                if (!elements.TryGetValue(elementRef, out var element)) continue;

                var shapeExtension = new DiDiagramShapeExtension(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                element.AddExtension(shapeExtension);
                diagramExtension.ShapesByElement[element]= shapeExtension.Clone();
            }

            if (diagramExtension.ShapesByElement.Count > 0)
            {
                dmnDefinition.AddExtension(diagramExtension);
            }
        }
    }
}
