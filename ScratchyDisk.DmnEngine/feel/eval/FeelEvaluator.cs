using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ScratchyDisk.DmnEngine.Feel.Ast;
using ScratchyDisk.DmnEngine.Feel.Types;
using ScratchyDisk.DmnEngine.Feel.Functions;
using ScratchyDisk.DmnEngine.Utils;

namespace ScratchyDisk.DmnEngine.Feel.Eval
{
    /// <summary>
    /// Tree-walking evaluator for FEEL AST nodes.
    /// Implements FEEL expression semantics including null propagation, three-valued logic,
    /// 1-based list indexing, and all FEEL operators.
    /// </summary>
    public class FeelEvaluator
    {
        private readonly FeelEvaluationContext context;
        private readonly Func<string, object[], object> functionResolver;

        /// <summary>
        /// Creates a new evaluator with the given evaluation context and function resolver.
        /// </summary>
        /// <param name="context">The evaluation context with variables</param>
        /// <param name="functionResolver">
        /// Callback to resolve function calls by name. Takes function name and arguments,
        /// returns the result. If null, only user-defined (lambda) functions work.
        /// </param>
        public FeelEvaluator(FeelEvaluationContext context, Func<string, object[], object> functionResolver = null)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.functionResolver = functionResolver;
        }

        /// <summary>
        /// Evaluates an AST node and returns the result.
        /// </summary>
        public object Evaluate(FeelAstNode node)
        {
            if (node == null) return null;

            return node switch
            {
                // Literals
                NumberLiteral n => n.Value,
                StringLiteral s => s.Value,
                BoolLiteral b => b.Value,
                NullLiteral => null,
                DateTimeLiteral dt => ParseDateTimeLiteral(dt.RawValue),
                Dash => true, // dash in unary test means "any value matches"

                // Names
                NameNode name => ResolveVariable(name.Name),
                QualifiedNameNode qn => ResolveQualifiedName(qn),

                // Operators
                BinaryOp bin => EvalBinaryOp(bin),
                UnaryMinus neg => EvalUnaryMinus(neg),
                UnaryNot not => EvalUnaryNot(not),
                Comparison cmp => EvalComparison(cmp),
                Between bet => EvalBetween(bet),
                InNode inNode => EvalIn(inNode),
                UnaryRangeNode unaryRange => EvalUnaryRange(unaryRange),
                InstanceOf io => EvalInstanceOf(io),

                // Logic
                Conjunction conj => EvalConjunction(conj),
                Disjunction disj => EvalDisjunction(disj),

                // Control flow
                IfNode ifn => EvalIf(ifn),
                ForNode f => EvalFor(f),
                QuantifiedNode q => EvalQuantified(q),

                // Collections
                ListNode list => EvalList(list),
                ContextNode ctx => EvalContext(ctx),
                RangeNode range => EvalRange(range),
                FilterNode filter => EvalFilter(filter),
                PathNode path => EvalPath(path),
                DescendantNode descendant => EvalDescendant(descendant),

                // Functions
                FunctionDefinitionNode fd => EvalFunctionDefinition(fd),
                FunctionInvocation fi => EvalFunctionInvocation(fi),
                NamedFunctionInvocation nfi => EvalNamedFunctionInvocation(nfi),

                // Unary tests
                UnaryTests ut => EvalUnaryTests(ut),

                _ => throw new FeelEvaluationException($"Unknown AST node type: {node.GetType().Name}")
            };
        }

        /// <summary>
        /// Evaluates a FEEL expression AST and returns the result as a boolean.
        /// Used for unary tests evaluation. Non-boolean results are coerced:
        /// - null → false
        /// - A value equal to InputValue → true
        /// - A range containing InputValue → true
        /// - A list containing InputValue → true
        /// </summary>
        public bool EvaluateAsUnaryTest(FeelAstNode node)
        {
            var result = Evaluate(node);
            return CoerceUnaryTestResultForNode(node, result, context.InputValue);
        }

        private static bool CoerceUnaryTestResult(object result, object inputValue)
        {
            // null result from a failed comparison/operation means "no match"
            // (explicit null test handling is done via isNullLiteral parameter overload)
            if (result == null) return false;
            if (result is bool b) return b;
            if (result is FeelRange range) return range.Contains(inputValue) == true;
            if (result is List<object> list) return list.Any(item => FeelValueComparer.FeelEqual(inputValue, item) == true);
            // Direct equality comparison
            return FeelValueComparer.FeelEqual(inputValue, result) == true;
        }

        private static bool CoerceUnaryTestResultForNode(FeelAstNode testNode, object result, object inputValue)
        {
            // For explicit null literal tests, check if inputValue is null
            if (testNode is NullLiteral) return inputValue == null;
            // The dash in a decision table input entry matches any value.
            if (testNode is Dash) return true;
            // For bool literals, compare with input value (not treat as test outcome)
            // A unary test of "true" means "input == true", not "this test passes"
            if (testNode is BoolLiteral) return FeelValueComparer.FeelEqual(inputValue, result) == true;
            // A boolean-valued expression written with boolean syntax (a comparison,
            // a conjunction, "not(...)", ...) is the test outcome. Any other test
            // that happens to evaluate to a boolean is a value test: "A.b" means
            // "input = A.b", not "A.b is true".
            if (result is bool && !IsBooleanTestSyntax(testNode))
                return FeelValueComparer.FeelEqual(inputValue, result) == true;
            return CoerceUnaryTestResult(result, inputValue);
        }

        /// <summary>
        /// True when the node is a boolean construct of the unary test syntax, i.e.
        /// the node's own value is the test outcome rather than a value to compare.
        /// </summary>
        private static bool IsBooleanTestSyntax(FeelAstNode testNode) =>
            testNode is Comparison or Between or InNode or InstanceOf
                or Conjunction or Disjunction or UnaryNot or IfNode or UnaryTests;

        // ==================== Variable Resolution ====================

        private object ResolveVariable(string name)
        {
            // Check special variables first
            if (name == "?") return context.InputValue;

            // Variables shadow built-in functions.
            if (context.TryGetVariable(name, out var value)) return value;

            // Built-in functions are first-class values: they can be passed as
            // arguments to other functions.
            return FeelBuiltInFunctions.GetFunctionValue(name);
        }

        private object ResolveQualifiedName(QualifiedNameNode qn)
        {
            // Multi-model names are stored flat, e.g. "b.model_b_input001".
            if (qn.Parts.Count > 1 && context.TryGetVariable(string.Join(".", qn.Parts), out var flatValue))
                return flatValue;

            var value = ResolveVariable(qn.Parts[0]);
            for (var i = 1; i < qn.Parts.Count; i++)
            {
                value = AccessMember(value, qn.Parts[i]);
            }
            return value;
        }

        private static object AccessMember(object source, string member)
        {
            if (source == null) return null;

            // Context member access
            if (source is FeelContext ctx)
                return ctx[member];

            // List projection: list.member → [item.member for item in list]
            if (source is List<object> list)
                return list.Select(item => AccessMember(item, member)).ToList();
            if (source is FeelRange range)
            {
                return member switch
                {
                    "start" => range.LowEndpoint,
                    "end" => range.HighEndpoint,
                    "start included" => range.LowInclusive,
                    "end included" => range.HighInclusive,
                    _ => null
                };
            }

            // Try property access via reflection for CLR objects
            var prop = source.GetType().GetProperty(member);
            if (prop != null) return FeelTypeCoercion.CoerceToFeel(prop.GetValue(source));

            // Date/time component access
            return AccessDateTimeComponent(source, member);
        }

        private static object AccessDateTimeComponent(object source, string member)
        {
            return source switch
            {
                DateOnly d => member switch
                {
                    "year" => (decimal)d.Year,
                    "month" => (decimal)d.Month,
                    "day" => (decimal)d.Day,
                    "weekday" => Weekday(d),
                    "value" => (decimal)new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds(),
                    _ => null
                },
                FeelExpandedDate ed => member switch
                {
                    "year" => (decimal)ed.Year,
                    "month" => (decimal)ed.Month,
                    "day" => (decimal)ed.Day,
                    "weekday" => Weekday(ed),
                    "value" => (decimal)(ed.ToDayNumber() * 86400),
                    _ => null
                },
                FeelExpandedDateTime edt => member switch
                {
                    "year" => (decimal)edt.Date.Year,
                    "month" => (decimal)edt.Date.Month,
                    "day" => (decimal)edt.Date.Day,
                    "weekday" => Weekday(edt.Date),
                    "hour" => (decimal)edt.Time.Hour,
                    "minute" => (decimal)edt.Time.Minute,
                    "second" => (decimal)edt.Time.Second,
                    "offset" => edt.Offset,
                    "time offset" => edt.Offset,
                    "timezone" => edt.ZoneId,
                    "value" => (decimal)((edt.Date.ToDayNumber() * 86400) + edt.Time.Hour * 3600 + edt.Time.Minute * 60 + edt.Time.Second),
                    _ => null
                },
                FeelDateTime zdt => member switch
                {
                    "timezone" => string.IsNullOrEmpty(zdt.ZoneId) ? null : zdt.ZoneId,
                    "offset" => string.IsNullOrEmpty(zdt.ZoneId) ? null : zdt.Value.Offset,
                    "time offset" => string.IsNullOrEmpty(zdt.ZoneId) ? null : zdt.Value.Offset,
                    _ => AccessDateTimeComponent(zdt.Value, member)
                },
                DateTimeOffset dt => member switch
                {
                    "year" => (decimal)dt.Year,
                    "month" => (decimal)dt.Month,
                    "day" => (decimal)dt.Day,
                    "weekday" => Weekday(DateOnly.FromDateTime(dt.DateTime)),
                    "hour" => (decimal)dt.Hour,
                    "minute" => (decimal)dt.Minute,
                    "second" => (decimal)dt.Second,
                    "value" => dt.ToUnixTimeMilliseconds() / 1000m,
                    "offset" => dt.Offset,
                    "time offset" => dt.Offset,
                    "timezone" => null,
                    _ => null
                },
                FeelTime t => member switch
                {
                    "hour" => (decimal)t.Time.Hour,
                    "minute" => (decimal)t.Time.Minute,
                    "second" => (decimal)t.Time.Second,
                    "value" => (decimal)(t.Time.Hour * 3600 + t.Time.Minute * 60 + t.Time.Second) + t.Time.Millisecond / 1000m,
                    "offset" => t.Offset,
                    "time offset" => t.Offset,
                    "timezone" => t.ZoneId,
                    _ => null
                },
                TimeSpan ts => member switch
                {
                    "days" => (decimal)ts.Days,
                    "hours" => (decimal)ts.Hours,
                    "minutes" => (decimal)ts.Minutes,
                    "seconds" => (decimal)ts.Seconds,
                    "value" => (decimal)ts.TotalSeconds,
                    _ => null
                },
                FeelYmDuration ym => member switch
                {
                    "years" => (decimal)ym.Years,
                    "months" => (decimal)ym.Months,
                    "value" => (decimal)ym.TotalMonths,
                    _ => null
                },
                _ => null
            };
        }
        /// <summary>
        /// ISO weekday: Monday = 1 ... Sunday = 7.
        /// </summary>
        private static decimal Weekday(DateOnly date)
        {
            return date.DayOfWeek == DayOfWeek.Sunday ? 7m : (decimal)date.DayOfWeek;
        }

        private static decimal Weekday(FeelExpandedDate date)
        {
            var day = (int)((date.ToDayNumber() + 4) % 7);
            if (day < 0) day += 7;
            return day == 0 ? 7m : day;
        }

        // ==================== Arithmetic ====================

        private object EvalBinaryOp(BinaryOp bin)
        {
            var left = Evaluate(bin.Left);
            var right = Evaluate(bin.Right);

            // For Add: skip null propagation — EvalAdd handles null for string concat compat
            if (bin.Op == BinaryOperator.Add)
                return EvalAdd(left, right);

            // Null propagation for all other operations
            if (left == null || right == null) return null;

            return bin.Op switch
            {
                BinaryOperator.Sub => EvalSub(left, right),
                BinaryOperator.Mul => EvalMul(left, right),
                BinaryOperator.Div => EvalDiv(left, right),
                BinaryOperator.Mod => EvalMod(left, right),
                BinaryOperator.Exp => EvalExp(left, right),
                _ => null
            };
        }

        private static object EvalAdd(object left, object right)
        {
            if (left is FeelExpandedDateTime expandedLeft && right is FeelYmDuration ymdLeft)
                return expandedLeft.Add(ymdLeft);
            if (left is FeelExpandedDateTime expandedLeft2 && right is TimeSpan tsLeft)
                return expandedLeft2.Add(tsLeft);
            if (right is FeelExpandedDateTime expandedRight && left is FeelYmDuration ymdRight)
                return expandedRight.Add(ymdRight);
            if (right is FeelExpandedDateTime expandedRight2 && left is TimeSpan tsRight)
                return expandedRight2.Add(tsRight);
            if (left is FeelExpandedDate expandedDate && right is FeelYmDuration ymdDate)
                return expandedDate.AddMonths(ymdDate.TotalMonths);
            if (left is FeelExpandedDate expandedDate2 && right is TimeSpan tsDate)
                return expandedDate2.AddDays((int)tsDate.TotalDays);

            var zoneId = left is FeelDateTime leftZone ? leftZone.ZoneId
                : right is FeelDateTime rightZone ? rightZone.ZoneId : null;
            var result = EvalAddCore(
                left is FeelDateTime leftZoned ? leftZoned.Value : left,
                right is FeelDateTime rightZoned ? rightZoned.Value : right);
            return zoneId != null && result is DateTimeOffset zonedResult
                ? new FeelDateTime(zonedResult, zoneId)
                : result;
        }

        private static object EvalAddCore(object left, object right)
        {
            // Both null → null (FEEL semantics)
            if (left == null && right == null) return null;

            // Both numeric (neither null)
            if (left != null && right != null && FeelValueComparer.IsNumeric(left) && FeelValueComparer.IsNumeric(right))
                return FeelValueComparer.ToDecimal(left) + FeelValueComparer.ToDecimal(right);

            // String + string
            if (left is string ls && right is string rs)
                return ls + rs;

            // Date/time/duration operations (require both non-null)
            if (left != null && right != null)
            {
                // Date + duration
                if (left is DateOnly date && right is FeelYmDuration ymd)
                    return ymd.AddTo(date);
                if (left is DateOnly date2 && right is TimeSpan ts1)
                    return DateOnly.FromDateTime(date2.ToDateTime(TimeOnly.MinValue).Add(ts1));
                if (left is DateTimeOffset dto && right is FeelYmDuration ymd2)
                    return ymd2.AddTo(dto);
                if (left is DateTimeOffset dto2 && right is TimeSpan ts2)
                    return dto2 + ts2;

                // Time + duration
                if (left is FeelTime ft && right is TimeSpan ts3)
                    return ft + ts3;

                // Duration + date/datetime
                if (left is FeelYmDuration ymd3 && right is DateOnly date3)
                    return ymd3.AddTo(date3);
                if (left is FeelYmDuration ymd4 && right is DateTimeOffset dto3)
                    return ymd4.AddTo(dto3);
                if (left is TimeSpan ts4 && right is DateTimeOffset dto4)
                    return dto4 + ts4;
                if (left is TimeSpan tsDate && right is DateOnly dateWithDuration)
                    return DateOnly.FromDateTime(dateWithDuration.ToDateTime(TimeOnly.MinValue).Add(tsDate));
                if (left is TimeSpan ts5 && right is FeelTime ft2)
                    return ft2 + ts5;

                // Duration + duration
                if (left is TimeSpan tsl && right is TimeSpan tsr)
                    return tsl + tsr;
                if (left is FeelYmDuration yml && right is FeelYmDuration ymr)
                    return yml + ymr;
            }

            // FEEL only defines string + string. There is no implicit string
            // conversion, so a mixed addition is null (an error at the engine
            // boundary), not C#-style concatenation.
            return null;
        }

        private static object EvalSub(object left, object right)
        {
            static bool IsUnzonedDateTime(object value) => value is FeelDateTime dt && dt.ZoneId == "";
            static bool IsDateTimeValue(object value) =>
                value is FeelDateTime or FeelExpandedDateTime or DateTimeOffset;

            if (IsUnzonedDateTime(left) && right is DateOnly) return null;
            if (IsUnzonedDateTime(right) && left is DateOnly) return null;
            if (IsUnzonedDateTime(left) && IsDateTimeValue(right) && !IsUnzonedDateTime(right)) return null;
            if (IsUnzonedDateTime(right) && IsDateTimeValue(left) && !IsUnzonedDateTime(left)) return null;

            if (left is FeelExpandedDateTime expandedLeft && right is FeelYmDuration ymdLeft)
                return expandedLeft.Add(new FeelYmDuration(-ymdLeft.TotalMonths));
            if (left is FeelExpandedDateTime expandedLeft2 && right is TimeSpan tsLeft)
                return expandedLeft2.Add(-tsLeft);
            if (left is FeelExpandedDate expandedDate && right is FeelYmDuration ymdDate)
                return expandedDate.AddMonths(-ymdDate.TotalMonths);
            if (left is FeelExpandedDate expandedDate2 && right is TimeSpan tsDate)
                return expandedDate2.AddDays(-(int)tsDate.TotalDays);

            var zoneId = left is FeelDateTime leftZone ? leftZone.ZoneId
                : right is FeelDateTime rightZone ? rightZone.ZoneId : null;
            var result = EvalSubCore(
                left is FeelDateTime leftZoned ? leftZoned.Value : left,
                right is FeelDateTime rightZoned ? rightZoned.Value : right);
            return zoneId != null && result is DateTimeOffset zonedResult
                ? new FeelDateTime(zonedResult, zoneId)
                : result;
        }

        private static object EvalSubCore(object left, object right)
        {
            if (FeelValueComparer.IsNumeric(left) && FeelValueComparer.IsNumeric(right))
                return FeelValueComparer.ToDecimal(left) - FeelValueComparer.ToDecimal(right);

            // Date - date = duration
            if (left is DateOnly d1 && right is DateOnly d2)
                return d1.ToDateTime(TimeOnly.MinValue) - d2.ToDateTime(TimeOnly.MinValue);
            if (left is DateTimeOffset dto1 && right is DateTimeOffset dto2)
                return dto1 - dto2;
            if (left is FeelTime ft1 && right is FeelTime ft2)
                return ft1 - ft2;

            // Date - date-time: the date is midnight UTC.
            if (left is DateOnly leftDate && right is DateTimeOffset rightDateTime)
                return new DateTimeOffset(leftDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) - rightDateTime;
            if (left is DateTimeOffset leftDateTime && right is DateOnly rightDate)
                return leftDateTime - new DateTimeOffset(rightDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

            // Date - duration
            if (left is DateOnly date && right is FeelYmDuration ymd)
                return new FeelYmDuration(-ymd.TotalMonths).AddTo(date);
            if (left is DateOnly date2 && right is TimeSpan ts1)
                return DateOnly.FromDateTime(date2.ToDateTime(TimeOnly.MinValue).Subtract(ts1));
            if (left is DateTimeOffset dto && right is FeelYmDuration ymd2)
                return new FeelYmDuration(-ymd2.TotalMonths).AddTo(dto);
            if (left is DateTimeOffset dto3 && right is TimeSpan ts2)
                return dto3 - ts2;
            if (left is FeelTime ft3 && right is TimeSpan ts3)
                return ft3 - ts3;

            // Duration - duration
            if (left is TimeSpan tsl && right is TimeSpan tsr)
                return tsl - tsr;
            if (left is FeelYmDuration yml && right is FeelYmDuration ymr)
                return yml - ymr;

            return null;
        }

        private static object EvalMul(object left, object right)
        {
            if (FeelValueComparer.IsNumeric(left) && FeelValueComparer.IsNumeric(right))
                return FeelValueComparer.ToDecimal(left) * FeelValueComparer.ToDecimal(right);

            // Duration * number
            if (left is TimeSpan ts && FeelValueComparer.IsNumeric(right))
                return TimeSpan.FromTicks((long)(ts.Ticks * (double)FeelValueComparer.ToDecimal(right)));
            if (FeelValueComparer.IsNumeric(left) && right is TimeSpan ts2)
                return TimeSpan.FromTicks((long)(ts2.Ticks * (double)FeelValueComparer.ToDecimal(left)));
            if (left is FeelYmDuration ym && FeelValueComparer.IsNumeric(right))
                return MultiplyYmDuration(ym, FeelValueComparer.ToDecimal(right));
            if (FeelValueComparer.IsNumeric(left) && right is FeelYmDuration ym2)
                return MultiplyYmDuration(ym2, FeelValueComparer.ToDecimal(left));

            return null;
        }

        /// <summary>
        /// FEEL years-and-months duration multiplication rounds to the nearest month,
        /// with half values rounded up (towards positive infinity).
        /// </summary>
        private static FeelYmDuration MultiplyYmDuration(FeelYmDuration duration, decimal factor)
        {
            var months = duration.TotalMonths * factor;
            var rounded = Math.Floor(months + 0.5m);
            return new FeelYmDuration((int)rounded);
        }

        private static FeelYmDuration DivideYmDuration(FeelYmDuration duration, decimal divisor)
        {
            var months = duration.TotalMonths / divisor;
            var rounded = Math.Floor(months + 0.5m);
            return new FeelYmDuration((int)rounded);
        }

        private static object EvalDiv(object left, object right)
        {
            if (FeelValueComparer.IsNumeric(left) && FeelValueComparer.IsNumeric(right))
            {
                var divisor = FeelValueComparer.ToDecimal(right);
                if (divisor == 0m) return null; // FEEL: division by zero = null
                return FeelValueComparer.ToDecimal(left) / divisor;
            }

            // Duration / number
            if (left is TimeSpan ts && FeelValueComparer.IsNumeric(right))
            {
                var d = (double)FeelValueComparer.ToDecimal(right);
                if (d == 0) return null;
                return TimeSpan.FromTicks((long)(ts.Ticks / d));
            }
            if (left is FeelYmDuration ym && FeelValueComparer.IsNumeric(right))
            {
                var d = FeelValueComparer.ToDecimal(right);
                if (d == 0m) return null;
                return DivideYmDuration(ym, d);
            }

            // Duration / duration = number
            if (left is TimeSpan tsl && right is TimeSpan tsr)
            {
                if (tsr.Ticks == 0) return null;
                return (decimal)((double)tsl.Ticks / tsr.Ticks);
            }
            if (left is FeelYmDuration yml && right is FeelYmDuration ymr)
            {
                if (ymr.TotalMonths == 0) return null;
                return (decimal)yml.TotalMonths / ymr.TotalMonths;
            }

            return null;
        }

        private static object EvalMod(object left, object right)
        {
            if (FeelValueComparer.IsNumeric(left) && FeelValueComparer.IsNumeric(right))
            {
                var divisor = FeelValueComparer.ToDecimal(right);
                if (divisor == 0m) return null;
                return FeelValueComparer.ToDecimal(left) % divisor;
            }
            return null;
        }

        private static object EvalExp(object left, object right)
        {
            if (FeelValueComparer.IsNumeric(left) && FeelValueComparer.IsNumeric(right))
            {
                var b = (double)FeelValueComparer.ToDecimal(left);
                var e = (double)FeelValueComparer.ToDecimal(right);
                return (decimal)Math.Pow(b, e);
            }
            return null;
        }

        private object EvalUnaryMinus(UnaryMinus neg)
        {
            var operand = Evaluate(neg.Operand);
            if (operand == null) return null;
            if (FeelValueComparer.IsNumeric(operand))
                return -FeelValueComparer.ToDecimal(operand);
            if (operand is TimeSpan ts)
                return -ts;
            if (operand is FeelYmDuration ym)
                return -ym;
            return null;
        }

        private object EvalUnaryNot(UnaryNot not)
        {
            var operand = Evaluate(not.Operand);
            var b = FeelValueComparer.ToBool(operand);
            return FeelValueComparer.FeelNot(b);
        }

        // ==================== Comparison ====================

        private object EvalComparison(Comparison cmp)
        {
            // If left is null, this is a unary test with implicit input value
            var left = cmp.Left != null ? Evaluate(cmp.Left) : context.InputValue;
            var right = Evaluate(cmp.Right);

            if (cmp.Op == ComparisonOperator.Eq)
                return FeelValueComparer.FeelEqual(left, right);
            if (cmp.Op == ComparisonOperator.Neq)
                return FeelValueComparer.FeelNot(FeelValueComparer.FeelEqual(left, right));

            var cmpResult = FeelValueComparer.Compare(left, right);
            if (cmpResult == null) return null;

            return cmp.Op switch
            {
                ComparisonOperator.Lt => cmpResult < 0,
                ComparisonOperator.Gt => cmpResult > 0,
                ComparisonOperator.Lte => cmpResult <= 0,
                ComparisonOperator.Gte => cmpResult >= 0,
                _ => null
            };
        }

        private object EvalBetween(Between bet)
        {
            var value = Evaluate(bet.Value);
            var low = Evaluate(bet.Low);
            var high = Evaluate(bet.High);

            if (value == null || low == null || high == null) return null;

            var cmpLow = FeelValueComparer.Compare(value, low);
            var cmpHigh = FeelValueComparer.Compare(value, high);
            if (cmpLow == null || cmpHigh == null) return null;

            return cmpLow >= 0 && cmpHigh <= 0;
        }

        private object EvalIn(InNode inNode)
        {
            var value = Evaluate(inNode.Value);

            // `x in <unary tests>` evaluates the tests against x, not against the
            // null input value. Temporarily bind x as the implicit input value so
            // comparisons, ranges and lists all use their unary-test semantics.
            if (inNode.Tests is UnaryTests unaryTests)
            {
                bool? any = false;
                foreach (var test in unaryTests.Tests)
                {
                    var match = EvaluateInTest(test, value);
                    if (match == true) { any = true; break; }
                    if (match == null) any = null;
                }

                if (unaryTests.IsNegated)
                    return any == null ? null : !any.Value;
                return any;
            }

            return EvaluateInTest(inNode.Tests, value);
        }

        /// <summary>
        /// Evaluates one unary test as the right-hand side of `in`, preserving null
        /// for ranges with a null endpoint or a null input.
        /// </summary>
        private bool? EvaluateInTest(FeelAstNode test, object value)
        {
            var previousInput = context.InputValue;
            context.InputValue = value;
            try
            {
                if (test is RangeNode rangeNode
                    && (rangeNode.Low is NullLiteral || rangeNode.High is NullLiteral))
                {
                    return null;
                }

                var evaluated = Evaluate(test);

                if (evaluated is FeelRange range)
                    return range.Contains(value);

                if (evaluated is List<object> list)
                {
                    bool? any = false;
                    foreach (var item in list)
                    {
                        var match = item is FeelRange itemRange
                            ? itemRange.Contains(value)
                            : FeelValueComparer.FeelEqual(value, item);
                        if (match == true) return true;
                        if (match == null) any = null;
                    }
                    return any;
                }

                return CoerceUnaryTestResultForNode(test, evaluated, value);
            }
            finally
            {
                context.InputValue = previousInput;
            }
        }

        private static string TypeName(object value)
        {
            if (value == null) return "null";
            if (value is string) return "string";
            if (value is bool) return "boolean";
            if (value is DateOnly or FeelExpandedDate) return "date";
            if (value is DateTimeOffset or FeelDateTime or FeelExpandedDateTime) return "date and time";
            if (value is FeelTime) return "time";
            if (value is TimeSpan) return "days and time duration";
            if (value is FeelYmDuration) return "years and months duration";
            if (value is FeelRange) return "range";
            if (value is FeelContext) return "context";
            if (value is FeelFunction) return "function";
            if (value is List<object>) return "list";
            return FeelValueComparer.IsNumeric(value) ? "number" : null;
        }

        private object EvalUnaryRange(UnaryRangeNode node)
        {
            var value = Evaluate(node.Value);
            return node.Op switch
            {
                ComparisonOperator.Lt => new FeelRange(null, false, value, false, true),
                ComparisonOperator.Lte => new FeelRange(null, false, value, true, true),
                ComparisonOperator.Gt => new FeelRange(value, false, null, false, true),
                ComparisonOperator.Gte => new FeelRange(value, true, null, false, true),
                ComparisonOperator.Eq => new FeelRange(value, true, value, true, true),
                _ => new FeelRange(value, true, value, true, true)
            };
        }

        private object EvalInstanceOf(InstanceOf io)
        {
            var value = Evaluate(io.Value);
            var typeName = io.TypeName;

            if (value == null) return typeName == "null";

            return MatchesFeelType(value, typeName);
        }

        private bool MatchesFeelType(object value, string typeName)
        {
            if (context.TypeAliases != null && context.TypeAliases.TryGetValue(typeName, out var aliasedType))
                typeName = aliasedType;

            if (string.Equals(typeName, "any", StringComparison.OrdinalIgnoreCase)) return true;
            if (value == null) return true;

            if (typeName.StartsWith("list<", StringComparison.Ordinal))
            {
                if (value is not List<object> list) return false;
                var inner = typeName[5..^1];
                return list.All(item => MatchesFeelType(item, inner));
            }

            if (typeName.StartsWith("context<", StringComparison.Ordinal))
            {
                if (value is not FeelContext contextValue) return false;
                var inner = typeName[8..^1];
                foreach (var field in SplitTypeFields(inner))
                {
                    var separator = field.IndexOf(':');
                    if (separator <= 0) return false;
                    var key = field[..separator];
                    var fieldType = field[(separator + 1)..];
                    if (!contextValue.ContainsKey(key)) return false;
                    if (!MatchesFeelType(contextValue[key], fieldType)) return false;
                }
                return true;
            }

            if (typeName.StartsWith("range<", StringComparison.Ordinal)) return value is FeelRange;

            return TypeName(value) == typeName;
        }

        private static IEnumerable<string> SplitTypeFields(string text)
        {
            var depth = 0;
            var start = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '<') depth++;
                else if (text[i] == '>') depth--;
                else if (text[i] == ',' && depth == 0)
                {
                    yield return text[start..i];
                    start = i + 1;
                }
            }
            if (start < text.Length) yield return text[start..];
        }

        // ==================== Logic ====================

        private object EvalConjunction(Conjunction conj)
        {
            bool? result = true;
            foreach (var operand in conj.Operands)
            {
                var val = Evaluate(operand);
                var b = FeelValueComparer.ToBool(val);
                result = FeelValueComparer.FeelAnd(result, b);
                if (result == false) return false; // short circuit
            }
            return result;
        }

        private object EvalDisjunction(Disjunction disj)
        {
            bool? result = false;
            foreach (var operand in disj.Operands)
            {
                var val = Evaluate(operand);
                var b = FeelValueComparer.ToBool(val);
                result = FeelValueComparer.FeelOr(result, b);
                if (result == true) return true; // short circuit
            }
            return result;
        }

        // ==================== Control Flow ====================

        private object EvalIf(IfNode ifn)
        {
            var condition = Evaluate(ifn.Condition);
            var b = FeelValueComparer.ToBool(condition);
            if (b == true) return Evaluate(ifn.ThenBranch);
            return Evaluate(ifn.ElseBranch);
        }

        private object EvalFor(ForNode forNode)
        {
            var results = new List<object>();
            return EvalForIterations(forNode.Iterations, 0, forNode.ReturnExpression, context, results)
                ? results
                : null;
        }

        private bool EvalForIterations(
            IReadOnlyList<IterationContext> iterations, int iterationIndex,
            FeelAstNode returnExpr, FeelEvaluationContext scope, List<object> results)
        {
            if (iterationIndex >= iterations.Count)
            {
                // FEEL exposes the results accumulated so far as "partial".
                scope.SetVariable("partial", new List<object>(results));
                results.Add(new FeelEvaluator(scope, functionResolver).Evaluate(returnExpr));
                return true;
            }

            var iteration = iterations[iterationIndex];
            var scopeEvaluator = new FeelEvaluator(scope, functionResolver);
            var childScope = scope.CreateChildScope();
            var childEvaluator = new FeelEvaluator(childScope, functionResolver);

            if (iteration.RangeEnd != null)
            {
                // Range iteration: for x in 1..5, or dates.
                var startVal = scopeEvaluator.Evaluate(iteration.ListExpression);
                var endVal = scopeEvaluator.Evaluate(iteration.RangeEnd);

                if (FeelValueComparer.IsNumeric(startVal) && FeelValueComparer.IsNumeric(endVal))
                {
                    var start = (int)FeelValueComparer.ToDecimal(startVal);
                    var end = (int)FeelValueComparer.ToDecimal(endVal);
                    var step = start <= end ? 1 : -1;
                    for (var i = start; step > 0 ? i <= end : i >= end; i += step)
                    {
                        childScope.SetVariable(iteration.VariableName, (decimal)i);
                        if (!EvalForIterations(iterations, iterationIndex + 1, returnExpr, childScope, results)) return false;
                    }
                    return true;
                }

                if (startVal is DateOnly startDate && endVal is DateOnly endDate)
                {
                    var step = startDate <= endDate ? 1 : -1;
                    for (var date = startDate; step > 0 ? date <= endDate : date >= endDate; date = date.AddDays(step))
                    {
                        childScope.SetVariable(iteration.VariableName, date);
                        if (!EvalForIterations(iterations, iterationIndex + 1, returnExpr, childScope, results)) return false;
                    }
                    return true;
                }

                return false;
            }

            // List iteration: for x in [1, 2, 3], or y in x for nested iterators.
            var listVal = scopeEvaluator.Evaluate(iteration.ListExpression);
            var items = CoerceToList(listVal);
            if (items == null || items.Any(item => item is FeelRange)) return false;

            foreach (var item in items)
            {
                childScope.SetVariable(iteration.VariableName, item);
                if (!EvalForIterations(iterations, iterationIndex + 1, returnExpr, childScope, results)) return false;
            }
            return true;
        }

        private object EvalQuantified(QuantifiedNode q)
        {
            var allResults = new List<bool>();
            EvalQuantifiedIterations(q.Iterations, 0, q.Satisfies, context, allResults);

            if (q.Quantifier == QuantifierType.Some)
                return allResults.Any(r => r);
            return allResults.All(r => r);
        }

        private void EvalQuantifiedIterations(
            IReadOnlyList<IterationContext> iterations, int iterationIndex,
            FeelAstNode satisfies, FeelEvaluationContext scope, List<bool> results)
        {
            if (iterationIndex >= iterations.Count)
            {
                var childEval = new FeelEvaluator(scope, functionResolver);
                var result = childEval.Evaluate(satisfies);
                var b = FeelValueComparer.ToBool(result);
                results.Add(b == true);
                return;
            }

            var iteration = iterations[iterationIndex];
            var childScope = scope.CreateChildScope();
            var childEval2 = new FeelEvaluator(childScope, functionResolver);

            var listVal = childEval2.Evaluate(iteration.ListExpression);
            var items = CoerceToList(listVal);
            foreach (var item in items)
            {
                childScope.SetVariable(iteration.VariableName, item);
                EvalQuantifiedIterations(iterations, iterationIndex + 1, satisfies, childScope, results);
            }
        }

        // ==================== Collections ====================

        private object EvalList(ListNode list)
        {
            return list.Elements.Select(Evaluate).ToList();
        }

        private object EvalContext(ContextNode ctx)
        {
            var result = new FeelContext();
            object resultValue = null;
            var hasResultEntry = false;
            var childScope = context.CreateChildScope();
            var childEval = new FeelEvaluator(childScope, functionResolver);

            foreach (var entry in ctx.Entries)
            {
                if (entry.Key != null && result.ContainsKey(entry.Key)) return null;

                var value = childEval.Evaluate(entry.Value);

                // A context entry without a name is a result expression; the
                // value of the context is the value of that entry.
                if (entry.Key == null)
                {
                    resultValue = value;
                    hasResultEntry = true;
                    continue;
                }

                result.Put(entry.Key, value);
                // Context entries are visible to subsequent entries
                childScope.SetVariable(entry.Key, value);
            }
            return hasResultEntry ? resultValue : result;
        }

        private object EvalRange(RangeNode range)
        {
            var low = Evaluate(range.Low);
            var high = Evaluate(range.High);
            return new FeelRange(low, range.LowInclusive, high, range.HighInclusive);
        }

        private object EvalFilter(FilterNode filter)
        {
            var source = Evaluate(filter.Source);
            var items = CoerceToList(source);

            var filterExpr = filter.Filter;

            // Check if filter is a numeric index
            // Create a temp evaluator to check the filter value type
            var testScope = context.CreateChildScope();
            // Try evaluating the filter without item context to see if it's a simple numeric index
            if (filterExpr is NumberLiteral numLit)
            {
                var index = (int)numLit.Value;
                return GetListElement(items, index);
            }

            // Otherwise, filter the list
            var results = new List<object>();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var childScope = context.CreateChildScope();
                childScope.InputValue = item;
                childScope.SetVariable("item", item);
                // Set context variables if item is a context
                if (item is FeelContext ctx)
                {
                    foreach (var entry in ctx)
                        childScope.SetVariable(entry.Key, entry.Value);
                }
                var childEval = new FeelEvaluator(childScope, functionResolver);
                var filterVal = childEval.Evaluate(filterExpr);

                // Numeric filter result selects by index
                if (FeelValueComparer.IsNumeric(filterVal))
                {
                    var idx = (int)FeelValueComparer.ToDecimal(filterVal);
                    return GetListElement(items, idx);
                }

                // Boolean filter
                var b = FeelValueComparer.ToBool(filterVal);
                if (b == true) results.Add(item);
            }
            return results;
        }

        /// <summary>
        /// Gets element from list using FEEL 1-based indexing.
        /// Negative indices count from the end (-1 = last).
        /// Returns null for out-of-bounds.
        /// </summary>
        private static object GetListElement(List<object> list, int feelIndex)
        {
            int clrIndex;
            if (feelIndex > 0)
                clrIndex = feelIndex - 1; // 1-based to 0-based
            else if (feelIndex < 0)
                clrIndex = list.Count + feelIndex; // -1 = last
            else
                return null; // 0 is not a valid FEEL index

            if (clrIndex < 0 || clrIndex >= list.Count) return null;
            return list[clrIndex];
        }

        private object EvalPath(PathNode path)
        {
            // Imported names are stored flat: b.input, a.person.name, etc.
            var flatName = FlatPathName(path);
            if (flatName != null && context.TryGetVariable(flatName, out var flatValue))
                return flatValue;

            var source = Evaluate(path.Source);
            return AccessMember(source, path.Member);
        }

        private static string FlatPathName(FeelAstNode node)
        {
            switch (node)
            {
                case NameNode name: return name.Name;
                case QualifiedNameNode qualified: return string.Join(".", qualified.Parts);
                case PathNode path:
                    var prefix = FlatPathName(path.Source);
                    return prefix == null ? null : prefix + "." + path.Member;
                default: return null;
            }
        }
        private object EvalDescendant(DescendantNode descendant)
        {
            var source = Evaluate(descendant.Source);
            if (source is not FeelContext context) return null;

            var results = new List<object>();
            CollectDescendant(context, descendant.Name, results);
            return results;
        }
        private static void CollectDescendant(FeelContext context, string name, List<object> results)
        {
            foreach (var entry in context.Entries)
            {
                if (entry.Key == name)
                    results.Add(entry.Value);

                if (entry.Value is FeelContext child)
                    CollectDescendant(child, name, results);
            }
        }

        // ==================== Functions ====================

        private object EvalFunctionDefinition(FunctionDefinitionNode fd)
        {
            // Capture the current scope for closure
            var closureScope = context;
            var closureResolver = functionResolver;
            var typeAliases = context.TypeAliases;

            return new FeelFunction(
                "anonymous",
                fd.Parameters.ToList(),
                args =>
                {
                    var childScope = closureScope.CreateChildScope();
                    for (var i = 0; i < fd.Parameters.Count; i++)
                    {
                        childScope.SetVariable(fd.Parameters[i], i < args.Length ? args[i] : null);
                    }
                    var childEval = new FeelEvaluator(childScope, closureResolver);
                    return childEval.Evaluate(fd.Body);
                },
                fd.ParameterTypes,
                typeAliases,
                true);
        }

        // Common CLR type aliases for static method resolution (C# compat)
        private static readonly Dictionary<string, Type> ClrTypeAliases = new()
        {
            { "double", typeof(double) },
            { "float", typeof(float) },
            { "int", typeof(int) },
            { "long", typeof(long) },
            { "decimal", typeof(decimal) },
            { "string", typeof(string) },
            { "bool", typeof(bool) },
            { "Math", typeof(Math) },
            { "Convert", typeof(Convert) },
            { "Int32", typeof(int) },
            { "Int64", typeof(long) },
            { "Double", typeof(double) },
            { "Single", typeof(float) },
            { "Decimal", typeof(decimal) },
            { "String", typeof(string) },
            { "Boolean", typeof(bool) },
            { "DateTime", typeof(DateTime) },
            { "DateTimeOffset", typeof(DateTimeOffset) },
        };

        private object EvalFunctionInvocation(FunctionInvocation fi)
        {
            // Imported/qualified function values: b.bkm(...)
            if (fi.Function is PathNode qualifiedPath
                && qualifiedPath.Source is NameNode qualifiedSource
                && context.TryGetVariable($"{qualifiedSource.Name}.{qualifiedPath.Member}", out var qualifiedValue)
                && qualifiedValue is FeelFunction qualifiedFunction)
            {
                return qualifiedFunction.Invoke(fi.Arguments.Select(Evaluate).ToArray());
            }

            // Handle CLR method calls: obj.Method(args) or Type.StaticMethod(args)
            if (fi.Function is PathNode pathNode)
            {
                var source = Evaluate(pathNode.Source);
                if (source != null)
                {
                    var args = fi.Arguments.Select(Evaluate).ToArray();
                    var method = source.GetType().GetMethod(pathNode.Member,
                        args.Length == 0 ? Type.EmptyTypes :
                        args.Select(a => a?.GetType() ?? typeof(object)).ToArray());
                    // Try parameterless overload if exact match failed
                    method ??= source.GetType().GetMethod(pathNode.Member, Type.EmptyTypes);
                    if (method != null)
                        return FeelTypeCoercion.CoerceToFeel(method.Invoke(source, method.GetParameters().Length == 0 ? Array.Empty<object>() : args));
                }
                // CLR static method calls: double.Parse(x), Math.Abs(x), etc.
                else if (pathNode.Source is NameNode clrTypeName &&
                         ClrTypeAliases.TryGetValue(clrTypeName.Name, out var clrType))
                {
                    var args = fi.Arguments.Select(Evaluate).ToArray();
                    var argTypes = args.Select(a => a?.GetType() ?? typeof(object)).ToArray();
                    var method = clrType.GetMethod(pathNode.Member, argTypes);
                    if (method != null && method.IsStatic)
                        return FeelTypeCoercion.CoerceToFeel(method.Invoke(null, args));
                }
            }

            var fnValue = Evaluate(fi.Function);
            var evalArgs = fi.Arguments.Select(Evaluate).ToArray();

            // If it resolved to a FeelFunction, invoke directly
            if (fnValue is FeelFunction fn)
                return fn.Invoke(evalArgs);

            // If the function node is a name, try the function resolver
            if (fi.Function is NameNode nameNode && functionResolver != null)
                return functionResolver(nameNode.Name, evalArgs);

            // If fnValue is null and function is a name, try the resolver
            if (fnValue == null && fi.Function is NameNode name2 && functionResolver != null)
                return functionResolver(name2.Name, evalArgs);

            return null;
        }

        private object EvalNamedFunctionInvocation(NamedFunctionInvocation nfi)
        {
            var fnValue = Evaluate(nfi.Function);
            var namedArgs = new Dictionary<string, object>();
            foreach (var (name, valueNode) in nfi.Arguments)
            {
                namedArgs[name] = Evaluate(valueNode);
            }

            // Built-ins validate the argument names and their declared order.
            // A variable with the same name shadows the built-in.
            if (nfi.Function is NameNode builtInName
                && !context.TryGetVariable(builtInName.Name, out _)
                && FeelBuiltInFunctions.HasFunction(builtInName.Name))
                return FeelBuiltInFunctions.ResolveNamed(builtInName.Name, namedArgs);

            if (fnValue is FeelFunction fn)
                return fn.InvokeNamed(namedArgs);

            // Named args with function resolver — pass as positional
            if (nfi.Function is NameNode nameNode && functionResolver != null)
            {
                var positionalArgs = namedArgs.Values.ToArray();
                return functionResolver(nameNode.Name, positionalArgs);
            }

            return null;
        }

        // ==================== Unary Tests ====================

        private object EvalUnaryTests(UnaryTests ut)
        {
            var inputValue = context.InputValue;
            var anyMatch = false;

            foreach (var test in ut.Tests)
            {
                if (CoerceUnaryTestResultForNode(test, Evaluate(test), inputValue))
                {
                    anyMatch = true;
                    break;
                }
            }

            return ut.IsNegated ? !anyMatch : anyMatch;
        }

        // ==================== Date/Time Parsing ====================

        private static object ParseDateTimeLiteral(string raw)
        {
            // FEEL allows 24:00:00 as the end of a day.
            var endOfDay = raw.Contains("T24:00");
            if (endOfDay) raw = raw.Replace("T24:00", "T00:00");

            // Expanded years (outside DateOnly's 0001-9999 range).
            var expandedDateTime = Regex.Match(raw,
                @"^(?<y>[+-]?\d{4,})-(?<mo>\d{2})-(?<d>\d{2})T(?<h>\d{2}):(?<mi>\d{2})(?::(?<sec>\d{2})(?:\.(?<f>\d{1,9}))?)?(?:(?<z>Z)|(?<off>[+-]\d{2}:\d{2})|@(?<zone>.+))?$",
                RegexOptions.None, RegexDefaults.Timeout);
            if (expandedDateTime.Success)
            {
                if (expandedDateTime.Groups["y"].Value.StartsWith("+")) return null;
                var year = int.Parse(expandedDateTime.Groups["y"].Value, CultureInfo.InvariantCulture);
                if (Math.Abs((long)year) > 999999999) return null;
                if (year is < 1 or > 9999)
                {
                    var month = int.Parse(expandedDateTime.Groups["mo"].Value, CultureInfo.InvariantCulture);
                    var day = int.Parse(expandedDateTime.Groups["d"].Value, CultureInfo.InvariantCulture);
                    var hour = int.Parse(expandedDateTime.Groups["h"].Value, CultureInfo.InvariantCulture);
                    var minute = int.Parse(expandedDateTime.Groups["mi"].Value, CultureInfo.InvariantCulture);
                    var second = expandedDateTime.Groups["sec"].Success
                        ? int.Parse(expandedDateTime.Groups["sec"].Value, CultureInfo.InvariantCulture)
                        : 0;
                    var fraction = expandedDateTime.Groups["f"].Success ? expandedDateTime.Groups["f"].Value : null;
                    var tickFraction = fraction == null ? 0L : long.Parse(fraction.PadRight(7, '0')[..7], CultureInfo.InvariantCulture);
                    var expandedDateValue = new FeelExpandedDate(year, month, day);
                    var expandedTimeValue = new TimeOnly(hour, minute, second).Add(TimeSpan.FromTicks(tickFraction));

                    var offset = TimeSpan.Zero;
                    string zoneId = null;
                    if (expandedDateTime.Groups["off"].Success)
                    {
                        var text = expandedDateTime.Groups["off"].Value;
                        var sign = text.StartsWith("-") ? -1 : 1;
                        offset = sign * TimeSpan.Parse(text.TrimStart('+', '-'), CultureInfo.InvariantCulture);
                    }
                    else if (expandedDateTime.Groups["zone"].Success)
                    {
                        zoneId = expandedDateTime.Groups["zone"].Value;
                    }
                    else if (expandedDateTime.Groups["z"].Success)
                    {
                        offset = TimeSpan.Zero;
                    }

                    var expanded = new FeelExpandedDateTime(expandedDateValue, expandedTimeValue, fraction, offset, zoneId,
                        expandedDateTime.Groups["z"].Success || expandedDateTime.Groups["off"].Success);
                    if (endOfDay) expanded = expanded.Add(TimeSpan.FromDays(1));
                    return expanded;
                }
            }

            var expandedDate = Regex.Match(raw, @"^(?<y>[+-]?\d{4,})-(?<mo>\d{2})-(?<d>\d{2})$", RegexOptions.None, RegexDefaults.Timeout);
            if (expandedDate.Success)
            {
                if (expandedDate.Groups["y"].Value.StartsWith("+")) return null;
                var year = int.Parse(expandedDate.Groups["y"].Value, CultureInfo.InvariantCulture);
                if (Math.Abs((long)year) > 999999999) return null;
                if (year is < 1 or > 9999)
                    return new FeelExpandedDate(year,
                        int.Parse(expandedDate.Groups["mo"].Value, CultureInfo.InvariantCulture),
                        int.Parse(expandedDate.Groups["d"].Value, CultureInfo.InvariantCulture));
            }

            // Try ISO date: 2024-01-15
            if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return date;

            // Try ISO time: 10:30:00, 10:30:00+05:00, 10:30:00Z
            if (TryParseFeelTime(raw, out var time))
                return time;

            // FEEL 1.5 IANA zone suffix: 2024-01-15T10:30:00@Europe/Paris
            var at = raw.LastIndexOf('@');
            if (at > 0)
            {
                var zone = ResolveTimeZone(raw[(at + 1)..]);
                if (zone == null) return null;
                if (!DateTime.TryParse(raw[..at], CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
                    return null;
                if (zone.IsInvalidTime(local)) return null;
                var zoned = new DateTimeOffset(local, zone.GetUtcOffset(local));
                if (endOfDay) zoned = zoned.AddDays(1);
                var fractionMatch = Regex.Match(raw[..at], @"\.(\d{1,9})$", RegexOptions.None, RegexDefaults.Timeout);
                return new FeelDateTime(zoned, raw[(at + 1)..],
                    fractionMatch.Success ? fractionMatch.Groups[1].Value : null);
            }

            // Try ISO date-time: 2024-01-15T10:30:00, etc. An unzoned date-time
            // is marked with an empty zone id so FEEL can reject mixed arithmetic.
            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            {
                if (endOfDay) dto = dto.AddDays(1);
                var hasZone = raw.Contains('@') || raw.EndsWith("Z") || Regex.IsMatch(raw, @"[+-]\d{2}:\d{2}$");
                return hasZone ? dto : new FeelDateTime(dto, "");
            }

            // Try duration: P1Y2M, P3DT4H, PT1H30M, etc.
            if (TryParseDuration(raw, out var duration))
                return duration;

            return null; // Unknown format
        }

        /// <summary>
        /// Resolves an IANA or Windows time zone identifier, accepting Z and UTC.
        /// </summary>
        private static TimeZoneInfo ResolveTimeZone(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return null;
            if (identifier == "Z" || identifier.Equals("UTC", StringComparison.OrdinalIgnoreCase))
                return TimeZoneInfo.Utc;

            try { return TimeZoneInfo.FindSystemTimeZoneById(identifier); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }

            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(identifier, out var windowsId))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(windowsId); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }

            return null;
        }

        private static bool TryParseFeelTime(string raw, out FeelTime result)
        {
            result = default;

            // Match time patterns: HH:mm, HH:mm:ss, HH:mm:ss.fff, with optional offset
            var match = Regex.Match(raw, @"^(\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?)(?:(Z)|([+-]\d{2}:\d{2})|@(.+))?$", RegexOptions.None, RegexDefaults.Timeout);
            if (!match.Success) return false;

            var time = TimeOnly.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            TimeSpan? offset = null;
            string zoneId = null;

            if (match.Groups[2].Success) // Z
                offset = TimeSpan.Zero;
            else if (match.Groups[3].Success) // +/-HH:mm
            {
                var text = match.Groups[3].Value;
                var sign = text.StartsWith("-") ? -1 : 1;
                offset = sign * TimeSpan.Parse(text.TrimStart('+', '-'), CultureInfo.InvariantCulture);
                if (offset.Value.Duration() > TimeSpan.FromHours(14)) return false;
            }
            else if (match.Groups[4].Success) // @zone
            {
                zoneId = match.Groups[4].Value;
                var zone = ResolveTimeZone(zoneId);
                if (zone == null) return false;
                offset = zone.GetUtcOffset(DateTime.Today);
            }

            result = new FeelTime(time, offset, zoneId,
                match.Groups[1].Value.Contains('.') ? match.Groups[1].Value.Split('.')[1] : null);
            return true;
        }

        private static bool TryParseDuration(string raw, out object duration)
        {
            duration = null;
            if (string.IsNullOrEmpty(raw)) return false;

            var negative = raw.StartsWith('-');
            var s = negative ? raw.Substring(1) : raw;
            if (!s.StartsWith('P')) return false;
            s = s.Substring(1); // Remove P

            // Check if it's a years-and-months duration (no T, only Y and M)
            if (!s.Contains('T') && !s.Contains('D') && !s.Contains('H') && !s.Contains('S'))
            {
                var years = 0;
                var months = 0;
                var yMatch = Regex.Match(s, @"(\d+)Y", RegexOptions.None, RegexDefaults.Timeout);
                if (yMatch.Success) years = int.Parse(yMatch.Groups[1].Value);
                var mMatch = Regex.Match(s, @"(\d+)M", RegexOptions.None, RegexDefaults.Timeout);
                if (mMatch.Success) months = int.Parse(mMatch.Groups[1].Value);
                var total = years * 12 + months;
                if (negative) total = -total;
                duration = new FeelYmDuration(total);
                return true;
            }

            // Days-and-time duration
            var days = 0;
            var hours = 0;
            var minutes = 0;
            var seconds = 0;

            var dMatch = Regex.Match(s, @"(\d+)D", RegexOptions.None, RegexDefaults.Timeout);
            if (dMatch.Success) days = int.Parse(dMatch.Groups[1].Value);

            var tIdx = s.IndexOf('T');
            if (tIdx >= 0)
            {
                var timePart = s.Substring(tIdx + 1);
                var hMatch = Regex.Match(timePart, @"(\d+)H", RegexOptions.None, RegexDefaults.Timeout);
                if (hMatch.Success) hours = int.Parse(hMatch.Groups[1].Value);
                var minMatch = Regex.Match(timePart, @"(\d+)M", RegexOptions.None, RegexDefaults.Timeout);
                if (minMatch.Success) minutes = int.Parse(minMatch.Groups[1].Value);
                var sMatch = Regex.Match(timePart, @"(\d+)S", RegexOptions.None, RegexDefaults.Timeout);
                if (sMatch.Success) seconds = int.Parse(sMatch.Groups[1].Value);
            }

            var ts = new TimeSpan(days, hours, minutes, seconds);
            if (negative) ts = -ts;
            duration = ts;
            return true;
        }

        // ==================== Helpers ====================

        private static List<object> CoerceToList(object value)
        {
            if (value == null) return new List<object>();
            if (value is List<object> list) return list;
            // Singleton list coercion
            return new List<object> { value };
        }
    }

    /// <summary>
    /// Exception thrown when FEEL evaluation fails.
    /// </summary>
    public class FeelEvaluationException : Exception
    {
        public FeelEvaluationException(string message, Exception innerException = null)
            : base(message, innerException) { }
    }
}
