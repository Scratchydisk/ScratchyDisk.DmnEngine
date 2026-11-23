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
        public static DmnBoxedExpression BuildFunctionBody(FunctionDefinition logic,
            Func<string, string> resolveType = null)
        {
            if (logic == null) return null;
            if (logic.Expression != null && !string.IsNullOrWhiteSpace(logic.Expression.Text))
                return BuildLiteral(logic.Expression);

            // A body that is itself a function definition is a curried lambda: it evaluates
            // to a function that returns another function.
            if (logic.Body != null)
            {
                var nested = BuildFunctionBody(logic.Body, resolveType);
                return nested == null
                    ? null
                    : new DmnFunctionDefinitionBoxedExpression(ParameterNames(logic.Body), nested);
            }

            return BuildConstruct(logic.Conditional, logic.For, logic.Some, logic.Every, logic.Filter,
                    resolveType)
                ?? BuildBody(logic.Context, logic.Relation, logic.List, logic.Invocation,
                    logic.DecisionTable, resolveType);
        }

        /// <summary>
        /// Builds the boxed expression body of a decision, or null when the decision has no
        /// boxed expression body.
        /// </summary>
        public static DmnBoxedExpression BuildDecisionBody(Decision decision,
            Func<string, string> resolveType = null)
        {
            return BuildBody(decision, resolveType);
        }

        private static DmnBoxedExpression BuildLiteral(Expression expression)
        {
            if (expression == null) return null;
            if (string.IsNullOrWhiteSpace(expression.Text)) return null;
            return new DmnLiteralBoxedExpression(expression.Text, DmnExpressionLanguage.Resolve(expression.Language));
        }

        /// <summary>
        /// Builds the boxed expression kind that provides the value of
        /// <paramref name="body"/>, or null when it holds no expression.
        /// </summary>
        private static DmnBoxedExpression BuildBody(IBoxedExpressionContainer body,
            Func<string, string> resolveType = null)
        {
            if (body == null) return null;

            if (body.FunctionDefinition != null)
            {
                var functionBody = BuildFunctionBody(body.FunctionDefinition, resolveType);
                if (functionBody == null) return null;
                return new DmnFunctionDefinitionBoxedExpression(
                    ParameterNames(body.FunctionDefinition), functionBody);
            }

            return BuildConstruct(body.Conditional, body.For, body.Some, body.Every, body.Filter,
                       resolveType)
                   ?? BuildBody(body.Context, body.Relation, body.List, body.Invocation,
                       body.DecisionTable, resolveType)
                   ?? BuildLiteral(body.Expression);
        }

        private static DmnBoxedExpression BuildConstruct(
            ConditionalExpression conditional,
            ForExpression forExpression,
            QuantifiedExpression some,
            QuantifiedExpression every,
            FilterExpression filter,
            Func<string, string> resolveType = null)
        {
            if (conditional != null) return BuildConditional(conditional, resolveType);
            if (forExpression != null) return BuildFor(forExpression, resolveType);
            if (some != null) return BuildQuantified(some, isSome: true, resolveType);
            if (every != null) return BuildQuantified(every, isSome: false, resolveType);
            if (filter != null) return BuildFilter(filter, resolveType);
            return null;
        }

        private static DmnBoxedExpression BuildBody(
            ContextExpression context,
            RelationExpression relation,
            ListExpression list,
            InvocationExpression invocation,
            DecisionTable decisionTable,
            Func<string, string> resolveType = null)
        {
            if (context != null) return BuildContext(context, resolveType);
            if (relation != null) return BuildRelation(relation);
            if (list != null) return BuildList(list);
            if (invocation != null) return BuildInvocation(invocation);
            if (decisionTable != null) return BuildDecisionTable(decisionTable);
            return null;
        }

        private static DmnBoxedExpression BuildChild(BoxedExpressionBody body,
            Func<string, string> resolveType = null)
        {
            return BuildBody(body, resolveType) ?? new DmnLiteralBoxedExpression(null);
        }

        private static DmnBoxedExpression BuildConditional(ConditionalExpression conditional,
            Func<string, string> resolveType)
        {
            return new DmnConditionalBoxedExpression(
                BuildChild(conditional.If, resolveType),
                BuildChild(conditional.Then, resolveType),
                BuildChild(conditional.Else, resolveType));
        }

        private static DmnBoxedExpression BuildFor(ForExpression forExpression,
            Func<string, string> resolveType)
        {
            return new DmnForBoxedExpression(
                forExpression.IteratorVariable,
                BuildChild(forExpression.In, resolveType),
                BuildChild(forExpression.Return, resolveType));
        }

        private static DmnBoxedExpression BuildQuantified(QuantifiedExpression quantified, bool isSome,
            Func<string, string> resolveType)
        {
            return isSome
                ? new DmnSomeBoxedExpression(quantified.IteratorVariable,
                    BuildChild(quantified.In, resolveType), BuildChild(quantified.Satisfies, resolveType))
                : new DmnEveryBoxedExpression(quantified.IteratorVariable,
                    BuildChild(quantified.In, resolveType), BuildChild(quantified.Satisfies, resolveType));
        }

        private static DmnBoxedExpression BuildFilter(FilterExpression filter,
            Func<string, string> resolveType)
        {
            return new DmnFilterBoxedExpression(
                BuildChild(filter.In, resolveType),
                BuildChild(filter.Match, resolveType));
        }

        private static DmnContextBoxedExpression BuildContext(ContextExpression context,
            Func<string, string> resolveType = null)
        {
            var entries = new List<DmnContextEntryExpression>();
            foreach (var entry in context?.Entries ?? Enumerable.Empty<ContextEntry>())
            {
                var value = BuildEntryValue(entry, resolveType);
                if (value == null) continue;
                entries.Add(new DmnContextEntryExpression(entry.Variable?.Name, value,
                    DeclaredConvertibleType(entry.Variable?.TypeRef, resolveType)));
            }
            return new DmnContextBoxedExpression(entries);
        }

        /// <summary>
        /// Resolves the declared type of a context entry to a FEEL type name when the
        /// type takes part in the implicit conversions (a base scalar type, or a list
        /// of those). Structure types and custom item definitions need no conversion,
        /// and their FEEL type text is not a plain scalar type, so they are left alone.
        /// </summary>
        private static string DeclaredConvertibleType(string typeRef, Func<string, string> resolveType)
        {
            if (string.IsNullOrWhiteSpace(typeRef) || resolveType == null) return null;
            var typeName = resolveType(typeRef.Trim());
            return IsImplicitlyConvertibleTypeName(typeName) ? typeName : null;
        }

        private static bool IsImplicitlyConvertibleTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return false;
            var name = typeName.Trim();
            if (name.StartsWith("list<", StringComparison.Ordinal) && name.EndsWith(">", StringComparison.Ordinal))
                return IsImplicitlyConvertibleTypeName(name[5..^1]);
            return name.ToLowerInvariant() is "number" or "string" or "boolean" or "date" or "date and time"
                or "time" or "days and time duration" or "years and months duration";
        }

        private static DmnBoxedExpression BuildEntryValue(ContextEntry entry,
            Func<string, string> resolveType = null)
        {
            return BuildBody(entry, resolveType);
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
