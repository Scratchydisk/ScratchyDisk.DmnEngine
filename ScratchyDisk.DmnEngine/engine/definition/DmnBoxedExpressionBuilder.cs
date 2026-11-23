using System;
using System.Collections.Generic;
using System.Linq;
using ScratchyDisk.DmnEngine.Engine.Decisions.Expression;
using ScratchyDisk.DmnEngine.Parser;
using ScratchyDisk.DmnEngine.Parser.Dto;

namespace ScratchyDisk.DmnEngine.Engine.Definition
{
    /// <summary>
    /// Builds executable <see cref="DmnBoxedExpression"/> trees from the parsed DMN boxed
    /// expressions (contexts, relations, lists, invocations, nested function definitions and
    /// decision tables). The builder is stateless: boxed expressions are evaluated against the
    /// FEEL scope of the element that contains them, so no model variables are created here.
    /// </summary>
    internal static class DmnBoxedExpressionBuilder
    {
        /// <summary>
        /// Builds the body of a function definition (a business knowledge model body or a
        /// function-definition decision). Returns null when the definition has no body.
        /// </summary>
        public static DmnBoxedExpression BuildFunctionBody(FunctionDefinition logic)
        {
            if (logic == null) return null;
            if (logic.Expression != null && !string.IsNullOrWhiteSpace(logic.Expression.Text))
                return BuildLiteral(logic.Expression);

            // A body that is itself a function definition is a curried lambda: it evaluates
            // to a function that returns another function.
            if (logic.Body != null)
            {
                var nested = BuildFunctionBody(logic.Body);
                return nested == null
                    ? null
                    : new DmnFunctionDefinitionBoxedExpression(ParameterNames(logic.Body), nested);
            }

            return BuildBody(logic.Context, logic.Relation, logic.List, logic.Invocation,
                logic.DecisionTable);
        }

        /// <summary>
        /// Builds the boxed expression body of a decision, or null when the decision has no
        /// boxed expression body.
        /// </summary>
        public static DmnBoxedExpression BuildDecisionBody(Decision decision)
        {
            if (decision == null) return null;
            if (decision.Context != null) return BuildContext(decision.Context);
            if (decision.Relation != null) return BuildRelation(decision.Relation);
            if (decision.List != null) return BuildList(decision.List);
            if (decision.Invocation != null) return BuildInvocation(decision.Invocation);
            if (decision.FunctionDefinition != null)
            {
                var body = BuildFunctionBody(decision.FunctionDefinition);
                if (body == null) return null;
                var parameters = ParameterNames(decision.FunctionDefinition);
                return new DmnFunctionDefinitionBoxedExpression(parameters, body);
            }
            return null;
        }

        private static DmnBoxedExpression BuildLiteral(Expression expression)
        {
            if (expression == null) return null;
            if (string.IsNullOrWhiteSpace(expression.Text)) return null;
            return new DmnLiteralBoxedExpression(expression.Text, DmnExpressionLanguage.Resolve(expression.Language));
        }

        private static DmnBoxedExpression BuildBody(
            ContextExpression context,
            RelationExpression relation,
            ListExpression list,
            InvocationExpression invocation,
            DecisionTable decisionTable)
        {
            if (context != null) return BuildContext(context);
            if (relation != null) return BuildRelation(relation);
            if (list != null) return BuildList(list);
            if (invocation != null) return BuildInvocation(invocation);
            if (decisionTable != null) return BuildDecisionTable(decisionTable);
            return null;
        }

        private static DmnContextBoxedExpression BuildContext(ContextExpression context)
        {
            var entries = new List<DmnContextEntryExpression>();
            foreach (var entry in context?.Entries ?? Enumerable.Empty<ContextEntry>())
            {
                var value = BuildEntryValue(entry);
                if (value == null) continue;
                entries.Add(new DmnContextEntryExpression(entry.Variable?.Name, value));
            }
            return new DmnContextBoxedExpression(entries);
        }

        private static DmnBoxedExpression BuildEntryValue(ContextEntry entry)
        {
            if (entry == null) return null;

            if (entry.FunctionDefinition != null)
            {
                var body = BuildFunctionBody(entry.FunctionDefinition);
                if (body == null) return null;
                return new DmnFunctionDefinitionBoxedExpression(
                    ParameterNames(entry.FunctionDefinition), body);
            }

            return BuildBody(entry.Context, entry.Relation, entry.List, entry.Invocation,
                entry.DecisionTable)
                ?? BuildLiteral(entry.Expression);
        }

        private static DmnListBoxedExpression BuildList(ListExpression list)
        {
            var items = (list?.Expressions ?? Enumerable.Empty<Expression>())
                .Select(BuildLiteral)
                .Where(i => i != null)
                .Cast<DmnBoxedExpression>()
                .ToList();
            return new DmnListBoxedExpression(items);
        }

        private static DmnRelationBoxedExpression BuildRelation(RelationExpression relation)
        {
            var columns = (relation?.Columns ?? new List<RelationColumn>())
                .Select((column, index) => string.IsNullOrWhiteSpace(column?.Name)
                    ? $"column{index + 1}"
                    : column.Name)
                .ToList();

            var rows = new List<IReadOnlyList<DmnBoxedExpression>>();
            foreach (var row in relation?.Rows ?? Enumerable.Empty<RelationRow>())
            {
                var values = new List<DmnBoxedExpression>();
                foreach (var expression in row?.Expressions ?? new List<Expression>())
                    values.Add(BuildLiteral(expression) ?? new DmnLiteralBoxedExpression(null));
                rows.Add(values);
            }
            return new DmnRelationBoxedExpression(columns, rows);
        }

        private static DmnInvocationBoxedExpression BuildInvocation(InvocationExpression invocation)
        {
            var function = BuildLiteral(invocation?.Function) ?? new DmnLiteralBoxedExpression(null);
            var bindings = new List<DmnInvocationBindingExpression>();
            foreach (var binding in invocation?.Bindings ?? Enumerable.Empty<InvocationBinding>())
            {
                var value = BuildLiteral(binding.Expression) ?? new DmnLiteralBoxedExpression(null);
                bindings.Add(new DmnInvocationBindingExpression(binding.Parameter?.Name, value));
            }
            return new DmnInvocationBoxedExpression(function, bindings);
        }

        private static DmnNestedDecisionTableExpression BuildDecisionTable(DecisionTable table)
        {
            if (table == null) return null;

            var dialect = DmnExpressionLanguage.Resolve(table.Inputs?
                .Select(i => i.InputExpression?.Language)
                .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)));

            var inputs = (table.Inputs ?? new List<DecisionTableInput>())
                .Select(input => new DmnNestedDecisionTableInput(input.InputExpression?.Text))
                .ToList();

            var outputs = new List<DmnNestedDecisionTableOutput>();
            foreach (var output in table.Outputs ?? new List<DecisionTableOutput>())
            {
                outputs.Add(new DmnNestedDecisionTableOutput
                {
                    Name = !string.IsNullOrWhiteSpace(output.Name) ? output.Name
                        : !string.IsNullOrWhiteSpace(output.Label) ? output.Label
                        : output.Id,
                    AllowedValues = output.AllowedOutputValues?.Values?.ToList() ?? new List<string>(),
                    DefaultExpression = !string.IsNullOrWhiteSpace(output.DefaultText)
                        ? output.DefaultText
                        : output.DefaultOutputEntry?.Text
                });
            }

            var rules = new List<DmnNestedDecisionTableRule>();
            foreach (var rule in table.Rules ?? Enumerable.Empty<DecisionRule>())
            {
                rules.Add(new DmnNestedDecisionTableRule
                {
                    InputEntries = (rule.InputEntries ?? new List<Expression>())
                        .Select(e => e?.Text)
                        .ToList(),
                    OutputEntries = (rule.OutputEntries ?? new List<Expression>())
                        .Select(e => e?.Text)
                        .ToList()
                });
            }

            return new DmnNestedDecisionTableExpression(
                table.HitPolicy, table.Aggregation, inputs, outputs, rules, dialect);
        }

        private static List<string> ParameterNames(FunctionDefinition function)
        {
            return function.FormalParameters?
                .Select(p => p.Name)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList() ?? new List<string>();
        }
    }
}
