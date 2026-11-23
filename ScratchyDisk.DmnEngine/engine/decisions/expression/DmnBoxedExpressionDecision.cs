using System;
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;
using ScratchyDisk.DmnEngine.Engine.Execution.Result;
using ScratchyDisk.DmnEngine.Utils;

namespace ScratchyDisk.DmnEngine.Engine.Decisions.Expression
{
    /// <summary>
    /// Decision whose logic is a boxed expression (context, relation, list, invocation,
    /// function definition or decision table) instead of a single FEEL literal expression.
    /// </summary>
    public class DmnBoxedExpressionDecision : DmnDecision
    {
        /// <summary>Boxed expression that provides the decision value.</summary>
        public DmnBoxedExpression Body { get; }

        /// <summary>Decision output variable.</summary>
        public IDmnVariable Output { get; }

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="name">Unique name of the decision</param>
        /// <param name="body">Boxed expression body</param>
        /// <param name="output">Decision output variable</param>
        /// <param name="requiredInputs">Decision required inputs (input variables)</param>
        /// <param name="requiredDecisions">List of decisions, the decision depends on</param>
        /// <param name="label">Optional label of decision, name is used when not provided</param>
        public DmnBoxedExpressionDecision(
            string name,
            DmnBoxedExpression body,
            IDmnVariable output,
            System.Collections.Generic.IReadOnlyCollection<IDmnVariable> requiredInputs,
            System.Collections.Generic.IReadOnlyCollection<IDmnDecision> requiredDecisions,
            string label = null)
            : base(name, requiredInputs, requiredDecisions, label)
        {
            Body = body ?? throw Logger.Fatal<ArgumentNullException>($"{nameof(body)} is null");
            Output = output ?? throw Logger.Fatal<ArgumentNullException>($"{nameof(output)} is null");
        }

        /// <inheritdoc />
        protected override DmnDecisionResult Evaluate(DmnExecutionContext context, string executionId)
        {
            if (context == null) throw Logger.FatalCorr<ArgumentNullException>(executionId, $"{nameof(context)} is null");

            Logger.InfoCorr(executionId, $"Evaluating boxed expression decision {Name}...");
            var runtime = context.CreateBoxedExpressionRuntime(executionId);
            object result;
            try
            {
                result = runtime.Evaluate(Body);
                result = DmnExecutionContext.ConvertToOutputType(result, Output.Type);
            }
            catch (DmnEvaluationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Logger.Fatal<DmnEvaluationException>(
                    $"Exception while evaluating boxed expression decision {Name}: {exception.Message}", exception);
            }

            Logger.InfoCorr(executionId, $"Evaluated boxed expression decision {Name}");
            var outVariable = context.GetVariable(Output);
            outVariable.Value = result;
            return new DmnDecisionResult(new DmnDecisionSingleResult(outVariable));
        }
    }
}
