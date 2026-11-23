using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Types;
using ScratchyDisk.DmnEngine.Utils;

namespace ScratchyDisk.DmnEngine.Feel.Functions
{
    /// <summary>
    /// Registry and implementation of all FEEL built-in functions (~80 functions).
    /// Functions support both positional and named argument invocation.
    /// </summary>
    public static class FeelBuiltInFunctions
    {
        private static readonly Dictionary<string, FeelFunction> Functions = new();

        static FeelBuiltInFunctions()
        {
            RegisterStringFunctions();
            RegisterNumericFunctions();
            RegisterListFunctions();
            RegisterBooleanFunctions();
            RegisterDateTimeFunctions();
            RegisterConversionFunctions();
            RegisterContextFunctions();
            RegisterRangeFunctions();
        }

        /// <summary>
        /// Resolves a built-in function call by name.
        /// Returns null if the function is not found.
        /// </summary>
        public static object Resolve(string name, object[] args)
        {
            if (Functions.TryGetValue(name, out var fn))
                return fn.Invoke(args);
            return null;
        }

        /// <summary>
        /// Resolves a built-in call with named arguments. Unknown argument names are
        /// rejected, and arguments are reordered to the declared parameter order.
        /// </summary>
        public static object ResolveNamed(string name, IDictionary<string, object> namedArgs)
        {
            if (namedArgs == null || !Functions.TryGetValue(name, out var fn)) return null;

            if (name == "date")
            {
                var dateArgs = namedArgs.ContainsKey("from")
                    ? new[] { namedArgs["from"] }
                    : new[] { ValueOrNull(namedArgs, "year"), ValueOrNull(namedArgs, "month"), ValueOrNull(namedArgs, "day") };
                return fn.Invoke(dateArgs);
            }

            if (name == "date and time")
            {
                if (namedArgs.ContainsKey("from") && !namedArgs.ContainsKey("date"))
                    return fn.Invoke(new[] { namedArgs["from"] });

                var date = namedArgs.TryGetValue("date", out var d) ? d
                    : namedArgs.TryGetValue("from", out var f) ? f : null;
                var time = namedArgs.TryGetValue("time", out var t) ? t : null;
                if (namedArgs.TryGetValue("timezone", out var tz))
                    return fn.Invoke(new[] { date, time, tz });
                return fn.Invoke(new[] { date, time });
            }

            if (name == "list replace")
            {
                foreach (var key in namedArgs.Keys)
                {
                    if (key is not ("list" or "match" or "position" or "newItem")) return null;
                }

                var listReplaceArgs = new[]
                {
                    ValueOrNull(namedArgs, "list"),
                    namedArgs.TryGetValue("match", out var matchArg) ? matchArg : ValueOrNull(namedArgs, "position"),
                    ValueOrNull(namedArgs, "newItem")
                };
                return fn.Invoke(listReplaceArgs);
            }

            if (name == "time")
            {
                object[] timeArgs;
                if (namedArgs.ContainsKey("from"))
                {
                    timeArgs = new[] { namedArgs["from"] };
                }
                else
                {
                    timeArgs = new[]
                    {
                        ValueOrNull(namedArgs, "hour"), ValueOrNull(namedArgs, "minute"),
                        ValueOrNull(namedArgs, "second"), ValueOrNull(namedArgs, "offset")
                    };
                }
                return fn.Invoke(timeArgs);
            }

            if (name == "context put")
            {
                if (namedArgs.TryGetValue("key", out var keyArg) && keyArg is List<object>) return null;
                if (namedArgs.TryGetValue("keys", out var keysArg) && keysArg is not List<object>) return null;
            }

            foreach (var key in namedArgs.Keys)
            {
                if (!IsAcceptedArgumentName(name, key, fn.ParameterNames)) return null;
            }

            var positional = new object[fn.ParameterNames.Count];
            for (var i = 0; i < fn.ParameterNames.Count; i++)
            {
                var parameter = fn.ParameterNames[i];
                if (namedArgs.TryGetValue(parameter, out var value))
                {
                    positional[i] = value;
                }
                else if (name == "context put" && parameter == "key" && namedArgs.TryGetValue("keys", out var keys))
                {
                    positional[i] = keys;
                }
            }

            return fn.Invoke(positional);
        }

        private static bool IsAcceptedArgumentName(string function, string key, IReadOnlyList<string> parameters)
        {
            if (parameters.Contains(key)) return true;
            if (function == "context put" && key == "keys") return true;
            if (function == "list replace" && key == "match") return true;
            if (function == "date and time" && key is "date" or "timezone") return true;
            return false;
        }

        /// <summary>
        /// Checks if a function with the given name exists.
        /// </summary>
        public static bool HasFunction(string name) => Functions.ContainsKey(name);

        /// <summary>
        /// Gets a built-in function by name, or null if not found.
        /// </summary>
        public static FeelFunction GetFunction(string name) =>
            Functions.TryGetValue(name, out var fn) ? fn : null;

        /// <summary>
        /// Functions that accept a variable number of arguments. Their declared
        /// parameter list describes the first argument rather than a maximum arity.
        /// </summary>
        private static readonly HashSet<string> VariadicFunctions = new()
        {
            "append", "concatenate", "union",
            "min", "max", "sum", "mean", "all", "any",
            "product", "median", "stddev", "mode",
        };

        /// <summary>
        /// Functions whose implementation accepts more positional arguments than
        /// their declared parameter list (for example the optional timezone of
        /// "date and time").
        /// </summary>
        private static readonly Dictionary<string, int> MaxArity = new()
        {
            { "date and time", 3 },
        };

        /// <summary>
        /// Gets a built-in function as a first-class FEEL value (a lambda that may
        /// be passed to another function). The returned function rejects more
        /// arguments than the built-in accepts.
        /// </summary>
        public static FeelFunction GetFunctionValue(string name)
        {
            if (!Functions.TryGetValue(name, out var fn)) return null;

            var maxArgs = VariadicFunctions.Contains(name)
                ? int.MaxValue
                : MaxArity.TryGetValue(name, out var declared)
                    ? declared
                    : fn.ParameterNames.Count;

            return new FeelFunction(fn.Name, fn.ParameterNames, args =>
            {
                if (args.Length > maxArgs)
                    throw new FeelEvaluationException(
                        $"Function '{name}' expects at most {maxArgs} argument(s) but got {args.Length}");
                return fn.Invoke(args);
            });
        }

        private static void Register(string name, string[] paramNames, Func<object[], object> body)
        {
            Functions[name] = new FeelFunction(name, paramNames, body);
        }

        private static object Arg(object[] args, int index) =>
            index < args.Length ? args[index] : null;

        private static object ValueOrNull(IDictionary<string, object> args, string key) =>
            args != null && args.TryGetValue(key, out var value) ? value : null;

        /// <summary>
        /// FEEL type name of a value, used by the type() built-in.
        /// </summary>
        private static object TypeName(object value)
        {
            switch (value)
            {
                case null: return "null";
                case string: return "string";
                case bool: return "boolean";
                case DateOnly: return "date";
                case FeelExpandedDate: return "date";
                case FeelExpandedDateTime: return "date and time";
                case DateTimeOffset: return "date and time";
                case FeelDateTime: return "date and time";
                case FeelTime: return "time";
                case TimeSpan: return "days and time duration";
                case FeelYmDuration: return "years and months duration";
                case FeelRange: return "range";
                case FeelContext: return "context";
                case FeelFunction: return "function";
                case IEnumerable<object>: return "list";
                default:
                    return FeelValueComparer.IsNumeric(value) ? "number" : null;
            }
        }

        /// <summary>
        /// A singleton list may be used wherever a single value is expected
        /// (DMN FEEL implicit conversion, [a] → a).
        /// </summary>
        private static object Scalar(object value) =>
            value is List<object> { Count: 1 } list ? list[0] : value;

        private static decimal? ArgDecimal(object[] args, int index)
        {
            var val = Scalar(Arg(args, index));
            if (val == null) return null;
            if (FeelValueComparer.IsNumeric(val)) return FeelValueComparer.ToDecimal(val);
            return null;
        }

        private static string ArgString(object[] args, int index) =>
            Scalar(Arg(args, index)) as string;

        private static List<object> ArgList(object[] args, int index)
        {
            var val = Arg(args, index);
            if (val is List<object> list) return list;
            if (val != null) return new List<object> { val };
            return null;
        }

        /// <summary>
        /// The fractional-second digits of a decimal number of seconds, without a
        /// trailing decimal point ("1.3" → "3"), or null when there is no fraction.
        /// </summary>
        private static string FractionDigits(decimal seconds)
        {
            var text = seconds.ToString(CultureInfo.InvariantCulture);
            var point = text.IndexOf('.');
            if (point < 0) return null;
            var fraction = text[(point + 1)..].TrimEnd('0');
            return fraction.Length == 0 ? null : fraction;
        }

        // ==================== String Functions ====================

        private static void RegisterStringFunctions()
        {
            Register("substring", new[] { "string", "start position", "length" }, args =>
            {
                var s = ArgString(args, 0);
                var start = ArgDecimal(args, 1);
                if (s == null || start == null) return null;

                // FEEL string positions count Unicode code points, not UTF-16 units.
                var runes = s.EnumerateRunes().ToList();
                var startIdx = (int)start.Value;
                var index = startIdx > 0 ? startIdx - 1 : runes.Count + startIdx;
                if (index < 0) index = 0;
                if (index >= runes.Count) return "";

                var length = ArgDecimal(args, 2);
                var count = length != null ? (int)length.Value : runes.Count - index;
                if (count < 0) return null;
                return string.Concat(runes.Skip(index).Take(Math.Min(count, runes.Count - index)).Select(r => r.ToString()));
            });

            Register("string length", new[] { "string" }, args =>
            {
                var s = ArgString(args, 0);
                return s != null ? (decimal)s.EnumerateRunes().Count() : null;
            });

            Register("upper case", new[] { "string" }, args =>
            {
                var s = ArgString(args, 0);
                return s?.ToUpperInvariant();
            });

            Register("lower case", new[] { "string" }, args =>
            {
                var s = ArgString(args, 0);
                return s?.ToLowerInvariant();
            });

            Register("substring before", new[] { "string", "match" }, args =>
            {
                var s = ArgString(args, 0);
                var match = ArgString(args, 1);
                if (s == null || match == null) return null;
                var idx = s.IndexOf(match, StringComparison.Ordinal);
                return idx < 0 ? "" : s.Substring(0, idx);
            });

            Register("substring after", new[] { "string", "match" }, args =>
            {
                var s = ArgString(args, 0);
                var match = ArgString(args, 1);
                if (s == null || match == null) return null;
                var idx = s.IndexOf(match, StringComparison.Ordinal);
                return idx < 0 ? "" : s.Substring(idx + match.Length);
            });

            Register("contains", new[] { "string", "match" }, args =>
            {
                var s = ArgString(args, 0);
                var match = ArgString(args, 1);
                if (s == null || match == null) return null;
                return (object)s.Contains(match, StringComparison.Ordinal);
            });

            Register("starts with", new[] { "string", "match" }, args =>
            {
                var s = ArgString(args, 0);
                var match = ArgString(args, 1);
                if (s == null || match == null) return null;
                return (object)s.StartsWith(match, StringComparison.Ordinal);
            });

            Register("ends with", new[] { "string", "match" }, args =>
            {
                var s = ArgString(args, 0);
                var match = ArgString(args, 1);
                if (s == null || match == null) return null;
                return (object)s.EndsWith(match, StringComparison.Ordinal);
            });

            Register("matches", new[] { "input", "pattern", "flags" }, args =>
            {
                if (args.Length > 3) return null;
                var input = ArgString(args, 0);
                var pattern = ArgString(args, 1);
                if (input == null || pattern == null) return null;
                if (args.Length >= 3 && Arg(args, 2) != null && Arg(args, 2) is not string) return null;
                var flags = ArgString(args, 2);
                var regexOpts = RegexOptions.None;
                if (flags != null)
                {
                    if (flags.Any(c => c is not ('i' or 'm' or 's' or 'x'))) return null;
                    if (flags.Contains('i')) regexOpts |= RegexOptions.IgnoreCase;
                    if (flags.Contains('m')) regexOpts |= RegexOptions.Multiline;
                    if (flags.Contains('s')) regexOpts |= RegexOptions.Singleline;
                    if (flags.Contains('x')) pattern = StripPatternWhitespace(pattern);
                }
                if (HasInvalidBackreference(pattern)) return null;
                if (flags == null || !flags.Contains('s')) pattern = RewriteDotOutsideClasses(pattern);

                try
                {
                    return (object)Regex.IsMatch(input, pattern, regexOpts, RegexDefaults.Timeout);
                }
                catch
                {
                    return null;
                }
            });

            Register("replace", new[] { "input", "pattern", "replacement", "flags" }, args =>
            {
                var input = ArgString(args, 0);
                var pattern = ArgString(args, 1);
                var replacement = ArgString(args, 2);
                if (input == null || pattern == null || replacement == null) return null;
                var flags = ArgString(args, 3);
                var regexOpts = RegexOptions.None;
                if (flags != null)
                {
                    if (flags.Contains('i')) regexOpts |= RegexOptions.IgnoreCase;
                    if (flags.Contains('m')) regexOpts |= RegexOptions.Multiline;
                    if (flags.Contains('s')) regexOpts |= RegexOptions.Singleline;
                }
                try
                {
                    return Regex.Replace(input, pattern, replacement, regexOpts, RegexDefaults.Timeout);
                }
                catch
                {
                    return null;
                }
            });

            Register("split", new[] { "string", "delimiter" }, args =>
            {
                var s = ArgString(args, 0);
                var delim = ArgString(args, 1);
                if (s == null || delim == null) return null;
                // The delimiter is a regular expression, not a literal string.
                return (object)Regex.Split(s, delim, RegexOptions.None, RegexDefaults.Timeout)
                    .Select(x => (object)x).ToList();
            });

            Register("string join", new[] { "list", "delimiter" }, args =>
            {
                if (args.Length is < 1 or > 2) return null;
                var list = ArgList(args, 0);
                if (list == null) return null;
                if (list.Any(item => item != null && item is not string)) return null;

                var delimiter = "";
                if (args.Length == 2 && Arg(args, 1) != null)
                {
                    delimiter = ArgString(args, 1);
                    if (delimiter == null) return null;
                }

                return string.Join(delimiter, list.Where(item => item is string).Cast<string>());
            });
        }

        // ==================== Numeric Functions ====================

        private static void RegisterNumericFunctions()
        {
            Register("decimal", new[] { "n", "scale" }, args =>
            {
                var n = ArgDecimal(args, 0);
                var scale = ArgDecimal(args, 1);
                if (n == null || scale == null) return null;
                return Math.Round(n.Value, (int)scale.Value, MidpointRounding.ToEven);
            });

            Register("floor", new[] { "n", "scale" }, args =>
            {
                if (args.Length > 2) return null;
                var n = ArgDecimal(args, 0);
                if (n == null) return null;
                if (args.Length == 2 && ArgDecimal(args, 1) == null) return null;
                if (args.Length > 1 && ArgDecimal(args, 1) is decimal floorScale && Math.Abs((int)floorScale) > 28)
                    return n.Value;
                return args.Length > 1 && ArgDecimal(args, 1) is decimal scale
                    ? Math.Floor(n.Value * PowerOf10(scale)) / PowerOf10(scale)
                    : Math.Floor(n.Value);
            });

            Register("ceiling", new[] { "n", "scale" }, args =>
            {
                if (args.Length > 2) return null;
                var n = ArgDecimal(args, 0);
                if (n == null) return null;
                if (args.Length == 2 && ArgDecimal(args, 1) == null) return null;
                if (args.Length > 1 && ArgDecimal(args, 1) is decimal ceilingScale && Math.Abs((int)ceilingScale) > 28)
                    return n.Value;
                return args.Length > 1 && ArgDecimal(args, 1) is decimal scale
                    ? Math.Ceiling(n.Value * PowerOf10(scale)) / PowerOf10(scale)
                    : Math.Ceiling(n.Value);
            });

            Register("round up", new[] { "n", "scale" }, args =>
            {
                if (args.Length is < 1 or > 2) return null;
                var n = ArgDecimal(args, 0);
                var scale = args.Length > 1 ? ArgDecimal(args, 1) : 0m;
                if (n == null || scale == null) return null;
                var scaleValue = (int)scale.Value;
                if (scaleValue < -6111 || scaleValue > 6176) return null;
                if (Math.Abs(scaleValue) > 28) return n.Value;
                var factor = PowerOf10(scale.Value);
                return n.Value >= 0
                    ? Math.Ceiling(n.Value * factor) / factor
                    : Math.Floor(n.Value * factor) / factor;
            });

            Register("round down", new[] { "n", "scale" }, args =>
            {
                if (args.Length is < 1 or > 2) return null;
                var n = ArgDecimal(args, 0);
                var scale = args.Length > 1 ? ArgDecimal(args, 1) : 0m;
                if (n == null || scale == null) return null;
                var scaleValue = (int)scale.Value;
                if (scaleValue < -6111 || scaleValue > 6176) return null;
                if (Math.Abs(scaleValue) > 28) return n.Value;
                var factor = PowerOf10(scale.Value);
                return n.Value >= 0
                    ? Math.Floor(n.Value * factor) / factor
                    : Math.Ceiling(n.Value * factor) / factor;
            });

            Register("round half up", new[] { "n", "scale" }, args =>
            {
                if (args.Length is < 1 or > 2) return null;
                var n = ArgDecimal(args, 0);
                var scale = args.Length > 1 ? ArgDecimal(args, 1) : 0m;
                if (n == null || scale == null) return null;
                var scaleValue = (int)scale.Value;
                if (scaleValue < -6111 || scaleValue > 6176) return null;
                if (Math.Abs(scaleValue) > 28) return n.Value;
                return Math.Round(n.Value, (int)scale.Value, MidpointRounding.AwayFromZero);
            });

            Register("round half down", new[] { "n", "scale" }, args =>
            {
                if (args.Length is < 1 or > 2) return null;
                var n = ArgDecimal(args, 0);
                var scale = args.Length > 1 ? ArgDecimal(args, 1) : 0m;
                if (n == null || scale == null) return null;
                var scaleValue = (int)scale.Value;
                if (scaleValue < -6111 || scaleValue > 6176) return null;
                if (Math.Abs(scaleValue) > 28) return n.Value;
                return RoundHalfDown(n.Value, (int)scale.Value);
            });

            Register("abs", new[] { "n" }, args =>
            {
                var val = Arg(args, 0);
                if (val == null) return null;
                if (FeelValueComparer.IsNumeric(val)) return Math.Abs(FeelValueComparer.ToDecimal(val));
                if (val is TimeSpan ts) return ts < TimeSpan.Zero ? -ts : ts;
                if (val is FeelYmDuration ym) return new FeelYmDuration(Math.Abs(ym.TotalMonths));
                return null;
            });

            Register("modulo", new[] { "dividend", "divisor" }, args =>
            {
                if (args.Length != 2) return null;
                var a = ArgDecimal(args, 0);
                var b = ArgDecimal(args, 1);
                if (a == null || b == null || b == 0) return null;
                return a.Value - b.Value * Math.Floor(a.Value / b.Value);
            });

            Register("sqrt", new[] { "number" }, args =>
            {
                var n = ArgDecimal(args, 0);
                if (n == null || n.Value < 0) return null;
                return (decimal)Math.Sqrt((double)n.Value);
            });

            Register("log", new[] { "number" }, args =>
            {
                var n = ArgDecimal(args, 0);
                if (n == null || n.Value <= 0) return null;
                return (decimal)Math.Log((double)n.Value);
            });

            Register("exp", new[] { "number" }, args =>
            {
                var n = ArgDecimal(args, 0);
                if (n == null) return null;
                return (decimal)Math.Exp((double)n.Value);
            });

            Register("odd", new[] { "number" }, args =>
            {
                var n = ArgDecimal(args, 0);
                if (n == null) return null;
                return (object)((int)n.Value % 2 != 0);
            });

            Register("even", new[] { "number" }, args =>
            {
                var n = ArgDecimal(args, 0);
                if (n == null) return null;
                return (object)((int)n.Value % 2 == 0);
            });
        }

        private static decimal PowerOf10(decimal scale)
        {
            var s = (int)scale;
            decimal result = 1;
            for (var i = 0; i < Math.Abs(s); i++) result *= 10;
            return s >= 0 ? result : 1 / result;
        }
        /// <summary>
        /// FEEL round half down: exact ties go toward zero, all other values round normally.
        /// </summary>
        private static decimal RoundHalfDown(decimal n, int scale)
        {
            var factor = PowerOf10(scale);
            var scaled = n * factor;
            var truncated = Math.Truncate(scaled);
            if (Math.Abs(scaled - truncated) == 0.5m)
                return truncated / factor;
            return Math.Round(n, scale, MidpointRounding.AwayFromZero);
        }
        /// <summary>
        /// Resolves an IANA or Windows time zone identifier, accepting "Z" and "UTC".
        /// </summary>
        private static TimeZoneInfo ResolveTimeZone(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return null;
            if (identifier == "Z" || string.Equals(identifier, "UTC", StringComparison.OrdinalIgnoreCase))
                return TimeZoneInfo.Utc;
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(identifier);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(identifier, out var windowsId))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }
            return null;
        }

        // ==================== List Functions ====================

        private static void RegisterListFunctions()
        {
            Register("list contains", new[] { "list", "element" }, args =>
            {
                var list = ArgList(args, 0);
                var element = Arg(args, 1);
                if (list == null) return null;
                return (object)list.Any(item => FeelValueComparer.FeelEqual(item, element) == true);
            });

            Register("count", new[] { "list" }, args =>
            {
                var list = ArgList(args, 0);
                if (list == null) return 0m;
                return (decimal)list.Count;
            });

            Register("min", new[] { "list" }, args =>
            {
                var list = FlattenArgs(args);
                if (list == null || list.Count == 0) return null;
                return list.Where(x => x != null).Aggregate((a, b) =>
                    FeelValueComparer.Compare(a, b) <= 0 ? a : b);
            });

            Register("max", new[] { "list" }, args =>
            {
                var list = FlattenArgs(args);
                if (list == null || list.Count == 0) return null;
                return list.Where(x => x != null).Aggregate((a, b) =>
                    FeelValueComparer.Compare(a, b) >= 0 ? a : b);
            });

            Register("sum", new[] { "list" }, args =>
            {
                var list = FlattenArgs(args);
                if (list == null || list.Count == 0) return null;
                decimal total = 0;
                foreach (var item in list)
                {
                    if (item == null || !FeelValueComparer.IsNumeric(item)) return null;
                    total += FeelValueComparer.ToDecimal(item);
                }
                return total;
            });

            Register("mean", new[] { "list" }, args =>
            {
                var list = FlattenArgs(args);
                if (list == null || list.Count == 0) return null;
                decimal total = 0;
                var count = 0;
                foreach (var item in list)
                {
                    if (item == null || !FeelValueComparer.IsNumeric(item)) return null;
                    total += FeelValueComparer.ToDecimal(item);
                    count++;
                }
                return count > 0 ? total / count : null;
            });

            Register("all", new[] { "list" }, args =>
            {
                if (args.Length == 0)
                    throw new FeelEvaluationException("Function 'all' expects at least one argument");
                var list = FlattenArgs(args);
                if (list == null || list.Count == 0) return true;
                bool? result = true;
                foreach (var item in list)
                {
                    var b = FeelValueComparer.ToBool(item);
                    result = FeelValueComparer.FeelAnd(result, b);
                    if (result == false) return false;
                }
                return result;
            });

            Register("any", new[] { "list" }, args =>
            {
                if (args.Length == 0)
                    throw new FeelEvaluationException("Function 'any' expects at least one argument");
                var list = FlattenArgs(args);
                if (list == null || list.Count == 0) return false;
                bool? result = false;
                foreach (var item in list)
                {
                    var b = FeelValueComparer.ToBool(item);
                    result = FeelValueComparer.FeelOr(result, b);
                    if (result == true) return true;
                }
                return result;
            });

            Register("sublist", new[] { "list", "start position", "length" }, args =>
            {
                var list = ArgList(args, 0);
                var start = ArgDecimal(args, 1);
                if (list == null || start == null) return null;
                var startIdx = (int)start.Value - 1; // 1-based
                if (startIdx < 0) startIdx = list.Count + startIdx + 1;
                var length = ArgDecimal(args, 2);
                var len = length != null ? (int)length.Value : list.Count - startIdx;
                if (startIdx < 0 || startIdx >= list.Count) return new List<object>();
                len = Math.Min(len, list.Count - startIdx);
                return list.GetRange(startIdx, len).ToList();
            });

            Register("append", new[] { "list", "item" }, args =>
            {
                var list = ArgList(args, 0);
                if (list == null) return null;
                var result = new List<object>(list);
                for (var i = 1; i < args.Length; i++)
                    result.Add(Arg(args, i));
                return (object)result;
            });

            Register("concatenate", new[] { "list" }, args =>
            {
                var result = new List<object>();
                foreach (var arg in args)
                {
                    if (arg is List<object> list)
                        result.AddRange(list);
                    else if (arg != null)
                        result.Add(arg);
                }
                return (object)result;
            });

            Register("insert before", new[] { "list", "position", "newItem" }, args =>
            {
                var list = ArgList(args, 0);
                var pos = ArgDecimal(args, 1);
                var newItem = Arg(args, 2);
                if (list == null || pos == null) return null;
                var result = new List<object>(list);
                var idx = (int)pos.Value - 1; // 1-based
                if (idx < 0) idx = result.Count + idx + 1;
                if (idx < 0) idx = 0;
                if (idx > result.Count) idx = result.Count;
                result.Insert(idx, newItem);
                return (object)result;
            });

            Register("remove", new[] { "list", "position" }, args =>
            {
                var list = ArgList(args, 0);
                var pos = ArgDecimal(args, 1);
                if (list == null || pos == null) return null;
                var result = new List<object>(list);
                var idx = (int)pos.Value - 1; // 1-based
                if (idx < 0 || idx >= result.Count) return (object)result;
                result.RemoveAt(idx);
                return (object)result;
            });

            Register("list replace", new[] { "list", "position", "newItem" }, args =>
            {
                var list = ArgList(args, 0);
                var selector = Arg(args, 1);
                var newItem = Arg(args, 2);
                if (list == null || selector == null) return null;

                if (selector is FeelFunction match)
                {
                    if (match.ParameterNames.Count != 2) return null;
                    var result = new List<object>(list);
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (match.Invoke(list[i], newItem) is not bool replace) return null;
                        if (replace) result[i] = newItem;
                    }
                    return (object)result;
                }

                if (!FeelValueComparer.IsNumeric(selector)) return null;
                var position = FeelValueComparer.ToDecimal(selector);
                var idx = (int)position;
                if (idx == 0) return null;
                idx = idx > 0 ? idx - 1 : list.Count + idx;
                if (idx < 0 || idx >= list.Count) return null;
                var replaced = new List<object>(list);
                replaced[idx] = newItem;
                return (object)replaced;
            });
            Register("reverse", new[] { "list" }, args =>
            {
                var list = ArgList(args, 0);
                if (list == null) return null;
                var result = new List<object>(list);
                result.Reverse();
                return (object)result;
            });

            Register("index of", new[] { "list", "match" }, args =>
            {
                var list = ArgList(args, 0);
                var match = Arg(args, 1);
                if (list == null) return null;
                var result = new List<object>();
                for (var i = 0; i < list.Count; i++)
                {
                    if (FeelValueComparer.FeelEqual(list[i], match) == true)
                        result.Add((decimal)(i + 1)); // 1-based
                }
                return (object)result;
            });

            Register("union", new[] { "list" }, args =>
            {
                var result = new List<object>();
                foreach (var arg in args)
                {
                    if (arg is List<object> list)
                    {
                        foreach (var item in list)
                        {
                            if (!result.Any(existing => FeelValueComparer.FeelEqual(existing, item) == true))
                                result.Add(item);
                        }
                    }
                }
                return (object)result;
            });

            Register("distinct values", new[] { "list" }, args =>
            {
                var list = ArgList(args, 0);
                if (list == null) return null;
                var result = new List<object>();
                foreach (var item in list)
                {
                    if (!result.Any(existing => FeelValueComparer.FeelEqual(existing, item) == true))
                        result.Add(item);
                }
                return (object)result;
            });

            Register("flatten", new[] { "list" }, args =>
            {
                var list = ArgList(args, 0);
                if (list == null) return null;
                var result = new List<object>();
                FlattenRecursive(list, result);
                return (object)result;
            });

            Register("product", new[] { "list" }, args =>
            {
                var list = FlattenArgs(args);
                if (list == null || list.Count == 0) return null;
                decimal product = 1;
                foreach (var item in list)
                {
                    if (item == null || !FeelValueComparer.IsNumeric(item)) return null;
                    product *= FeelValueComparer.ToDecimal(item);
                }
                return product;
            });

            Register("median", new[] { "list" }, args =>
            {
                var list = FlattenArgs(args);
                if (list == null || list.Count == 0) return null;
                var nums = new List<decimal>();
                foreach (var item in list)
                {
                    if (item == null || !FeelValueComparer.IsNumeric(item)) return null;
                    nums.Add(FeelValueComparer.ToDecimal(item));
                }
                nums.Sort();
                var n = nums.Count;
                return n % 2 == 1 ? nums[n / 2] : (nums[n / 2 - 1] + nums[n / 2]) / 2;
            });

            Register("stddev", new[] { "list" }, args =>
            {
                var list = FlattenArgs(args);
                if (list == null || list.Count < 2) return null;
                var nums = new List<decimal>();
                foreach (var item in list)
                {
                    if (item == null || !FeelValueComparer.IsNumeric(item)) return null;
                    nums.Add(FeelValueComparer.ToDecimal(item));
                }
                var mean = nums.Sum() / nums.Count;
                var variance = nums.Sum(x => (x - mean) * (x - mean)) / (nums.Count - 1);
                return (decimal)Math.Sqrt((double)variance);
            });

            Register("mode", new[] { "list" }, args =>
            {
                if (args.Length == 0) return null;
                List<object> list;
                if (args.Length == 1)
                {
                    if (Arg(args, 0) == null) return null;
                    list = ArgList(args, 0);
                }
                else
                {
                    list = args.ToList();
                }
                if (list == null) return null;
                if (list.Count == 0) return new List<object>();
                if (list.Any(item => item == null)) return null;
                var firstType = TypeName(list[0]);
                if (list.Any(item => !Equals(TypeName(item), firstType))) return null;
                var groups = new List<(object value, int count)>();
                foreach (var item in list)
                {
                    var found = false;
                    for (var i = 0; i < groups.Count; i++)
                    {
                        if (FeelValueComparer.FeelEqual(groups[i].value, item) == true)
                        {
                            groups[i] = (groups[i].value, groups[i].count + 1);
                            found = true;
                            break;
                        }
                    }
                    if (!found) groups.Add((item, 1));
                }
                var maxCount = groups.Max(g => g.count);
                return (object)groups.Where(g => g.count == maxCount)
                    .Select(g => g.value)
                    .OrderBy(v => v, Comparer<object>.Create((a, b) => FeelValueComparer.Compare(a, b) ?? 0))
                    .ToList();
            });

            Register("sort", new[] { "list", "precedes" }, args =>
            {
                var list = ArgList(args, 0);
                if (list == null) return null;
                var precedes = Arg(args, 1) as FeelFunction;
                var result = new List<object>(list);

                if (precedes != null)
                {
                    result.Sort((a, b) =>
                    {
                        var r = precedes.Invoke(a, b);
                        if (r is bool bv && bv) return -1;
                        return 1;
                    });
                }
                else
                {
                    result.Sort((a, b) => FeelValueComparer.Compare(a, b) ?? 0);
                }
                return (object)result;
            });
        }

        private static void FlattenRecursive(List<object> source, List<object> result)
        {
            foreach (var item in source)
            {
                if (item is List<object> nested)
                    FlattenRecursive(nested, result);
                else
                    result.Add(item);
            }
        }

        /// <summary>
        /// If a single list argument is passed, use it. Otherwise treat all args as the list.
        /// This supports both sum([1,2,3]) and sum(1,2,3) calling conventions.
        /// </summary>
        private static List<object> FlattenArgs(object[] args)
        {
            if (args.Length == 1 && args[0] is List<object> list) return list;
            return args.ToList();
        }

        // ==================== Boolean Functions ====================

        private static void RegisterBooleanFunctions()
        {
            Register("not", new[] { "negand" }, args =>
            {
                var b = FeelValueComparer.ToBool(Arg(args, 0));
                return FeelValueComparer.FeelNot(b);
            });

            Register("is", new[] { "value1", "value2" }, args =>
            {
                var a = Arg(args, 0);
                var b = Arg(args, 1);
                if (a == null || b == null) return a == null && b == null;
                if (!Equals(TypeName(a), TypeName(b))) return false;

                // Zoned date-times must agree on the zone identifier as well as the
                // wall clock; an unzoned or offset value is a different type.
                if (a is FeelDateTime za || b is FeelDateTime zb)
                {
                    if (a is not FeelDateTime za2 || b is not FeelDateTime zb2) return false;
                    return za2.ZoneId == zb2.ZoneId
                           && za2.Value.DateTime == zb2.Value.DateTime
                           && za2.Value.Offset == zb2.Value.Offset;
                }

                // "is" is stricter than "=": timezone presence and the wall-clock
                // components matter, not just the instant.
                if (a is DateTimeOffset da && b is DateTimeOffset db)
                    return da.DateTime == db.DateTime && da.Offset == db.Offset;
                if (a is FeelTime ta && b is FeelTime tb)
                    return ta.Time == tb.Time && ta.Offset == tb.Offset && ta.ZoneId == tb.ZoneId;

                return FeelValueComparer.FeelEqual(a, b) == true;
            });
        }

        // ==================== Date/Time Functions ====================

        private static void RegisterDateTimeFunctions()
        {
            Register("date", new[] { "from", "year", "month" }, args =>
            {
                if (args.Length >= 3)
                {
                    var y = ArgDecimal(args, 0);
                    var m = ArgDecimal(args, 1);
                    var d = ArgDecimal(args, 2);
                    if (y == null || m == null || d == null) return null;
                    var year = (int)y.Value;
                    if (Math.Abs((long)year) > 999999999) return null;
                    return year is < 1 or > 9999
                        ? new FeelExpandedDate(year, (int)m.Value, (int)d.Value)
                        : new DateOnly(year, (int)m.Value, (int)d.Value);
                }
                var val = Arg(args, 0);
                if (val is string s)
                {
                    if (DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                        return d;
                    var expanded = Regex.Match(s, @"^(?<y>[+-]?\d{4,})-(?<mo>\d{2})-(?<d>\d{2})$", RegexOptions.None, RegexDefaults.Timeout);
                    if (expanded.Success)
                    {
                        if (expanded.Groups["y"].Value.StartsWith("+")) return null;
                        var year = int.Parse(expanded.Groups["y"].Value, CultureInfo.InvariantCulture);
                        if (Math.Abs((long)year) > 999999999) return null;
                        return year is < 1 or > 9999
                            ? new FeelExpandedDate(year,
                                int.Parse(expanded.Groups["mo"].Value, CultureInfo.InvariantCulture),
                                int.Parse(expanded.Groups["d"].Value, CultureInfo.InvariantCulture))
                            : null;
                    }
                    return null;
                }
                if (val is FeelExpandedDateTime expandedDateTime) return expandedDateTime.Date;
                if (val is FeelExpandedDate) return val;
                if (val is FeelDateTime fdt) return DateOnly.FromDateTime(fdt.Value.DateTime);
                if (val is DateTimeOffset dto) return DateOnly.FromDateTime(dto.DateTime);
                if (val is DateOnly) return val;
                return null;
            });

            Register("time", new[] { "from", "hour", "minute", "second", "offset" }, args =>
            {
                if (args.Length >= 3)
                {
                    var h = ArgDecimal(args, 0);
                    var m = ArgDecimal(args, 1);
                    var s = ArgDecimal(args, 2);
                    if (h == null || m == null || s == null) return null;
                    if (h is < 0 or > 23 || m is < 0 or > 59 || s < 0 || s >= 60) return null;
                    TimeSpan? offset = null;
                    if (args.Length >= 4 && Arg(args, 3) is TimeSpan tz) offset = tz;

                    // Seconds are decimal: keep the fractional part rather than
                    // truncating it to a whole second.
                    var secondTicks = (long)(s.Value * TimeSpan.TicksPerSecond);
                    var time = new TimeOnly((int)h.Value, (int)m.Value, 0)
                        .Add(TimeSpan.FromTicks(secondTicks));
                    var fraction = FractionDigits(s.Value);
                    return new FeelTime(time, offset, null, fraction);
                }
                var val = Arg(args, 0);
                if (val is string str)
                {
                    if (TryParseTime(str, out var t)) return t;
                    return null;
                }
                if (val is FeelTime) return val;
                if (val is FeelExpandedDateTime expandedDateTime)
                    return new FeelTime(expandedDateTime.Time, expandedDateTime.Offset, expandedDateTime.ZoneId, expandedDateTime.Fraction);
                if (val is DateOnly) return new FeelTime(TimeOnly.MinValue, TimeSpan.Zero);
                if (val is FeelDateTime zdt)
                    return new FeelTime(TimeOnly.FromDateTime(zdt.Value.DateTime), zdt.Value.Offset, zdt.ZoneId, zdt.Fraction);
                if (val is DateTimeOffset dto)
                    return new FeelTime(TimeOnly.FromTimeSpan(dto.TimeOfDay), dto.Offset);
                return null;
            });

            Register("date and time", new[] { "from", "time" }, args =>
            {
                if (args.Length >= 2)
                {
                    var dateVal = Arg(args, 0);
                    var timeVal = Arg(args, 1);

                    FeelExpandedDate? expandedDate = null;
                    DateOnly date = default;
                    var hasExpandedDate = false;
                    if (dateVal is FeelExpandedDate fed) { expandedDate = fed; hasExpandedDate = true; }
                    else if (dateVal is FeelExpandedDateTime fedt) { expandedDate = fedt.Date; hasExpandedDate = true; }
                    if (hasExpandedDate)
                    {
                        TimeOnly timeOnly;
                        if (timeVal is FeelTime et) timeOnly = et.Time;
                        else if (timeVal is string ets && TryParseTime(ets, out var parsedEt)) timeOnly = parsedEt.Time;
                        else return null;

                        string zoneId = null;
                        var offset = TimeSpan.Zero;
                        if (args.Length >= 3 && Arg(args, 2) is string tz2)
                        {
                            zoneId = tz2;
                            var zone2 = ResolveTimeZone(tz2);
                            if (zone2 == null) return null;
                        }
                        else if (timeVal is FeelTime ftz)
                        {
                            zoneId = ftz.ZoneId;
                            offset = ftz.Offset ?? TimeSpan.Zero;
                        }
                        return new FeelExpandedDateTime(expandedDate.Value, timeOnly,
                            timeVal is FeelTime ftFraction ? ftFraction.Fraction : null,
                            offset, zoneId);
                    }
                    if (dateVal is DateOnly d) date = d;
                    else if (dateVal is FeelDateTime fdt2) date = DateOnly.FromDateTime(fdt2.Value.DateTime);
                    else if (dateVal is DateTimeOffset dto2) date = DateOnly.FromDateTime(dto2.DateTime);
                    else if (dateVal is string ds && DateOnly.TryParseExact(ds, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dp))
                        date = dp;
                    else return null;

                    if (args.Length >= 3 && Arg(args, 2) is string timezone)
                    {
                        var zone = ResolveTimeZone(timezone);
                        if (zone == null) return null;
                        TimeOnly time;
                        if (timeVal is FeelTime zonedTime) time = zonedTime.Time;
                        else if (timeVal is string zonedTimeString && TryParseTime(zonedTimeString, out var parsedTime))
                            time = parsedTime.Time;
                        else return null;
                        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
                        if (zone.IsInvalidTime(local)) return null;
                        return new FeelDateTime(new DateTimeOffset(local, zone.GetUtcOffset(local)), timezone);
                    }
                    if (timeVal is FeelTime ft)
                    {
                        if (!string.IsNullOrWhiteSpace(ft.ZoneId))
                        {
                            var zone = ResolveTimeZone(ft.ZoneId);
                            if (zone == null) return null;
                            var local = date.ToDateTime(ft.Time, DateTimeKind.Unspecified);
                            if (zone.IsInvalidTime(local)) return null;
                            return new FeelDateTime(new DateTimeOffset(local, zone.GetUtcOffset(local)), ft.ZoneId, ft.Fraction);
                        }
                        return new DateTimeOffset(date.ToDateTime(ft.Time), ft.Offset ?? TimeSpan.Zero);
                    }
                    if (timeVal is string ts && TryParseTime(ts, out var t))
                        return new DateTimeOffset(date.ToDateTime(t.Time), t.Offset ?? TimeSpan.Zero);
                    return null;
                }
                var val = Arg(args, 0);
                if (val is string s)
                {
                    if (TryParseExpandedDateTime(s, out var expandedDateTime)) return expandedDateTime;

                    var match = Regex.Match(s,
                        @"^(?<y>\d{4})-(?<mo>\d{2})-(?<d>\d{2})(?:T(?<h>\d{2}):(?<mi>\d{2})(?::(?<sec>\d{2})(?:\.(?<f>\d{1,9}))?)?)?(?:(?<z>Z)|(?<off>[+-]\d{2}:\d{2})|@(?<zone>.+))?$",
                        RegexOptions.None, RegexDefaults.Timeout);
                    if (!match.Success) return null;

                    var year = int.Parse(match.Groups["y"].Value);
                    var month = int.Parse(match.Groups["mo"].Value);
                    var day = int.Parse(match.Groups["d"].Value);
                    var hour = match.Groups["h"].Success ? int.Parse(match.Groups["h"].Value) : 0;
                    var minute = match.Groups["mi"].Success ? int.Parse(match.Groups["mi"].Value) : 0;
                    var second = match.Groups["sec"].Success ? int.Parse(match.Groups["sec"].Value) : 0;
                    if (month is < 1 or > 12 || day is < 1 or > 31 || hour > 23 || minute > 59 || second > 59)
                        return null;

                    var fractionText = match.Groups["f"].Success ? match.Groups["f"].Value : "";
                    var ticks = 0L;
                    if (fractionText.Length > 0)
                    {
                        var scale = 1m;
                        if (fractionText.Length <= 7)
                            for (var i = fractionText.Length; i < 7; i++) scale *= 10m;
                        else
                            for (var i = 7; i < fractionText.Length; i++) scale /= 10m;
                        ticks = (long)Math.Round(decimal.Parse(fractionText, CultureInfo.InvariantCulture) * scale);
                    }
                    var time = new TimeOnly(hour, minute, second).Add(TimeSpan.FromTicks(ticks));

                    if (match.Groups["zone"].Success)
                    {
                        var zoneId = match.Groups["zone"].Value;
                        var zone = ResolveTimeZone(zoneId);
                        if (zone == null) return null;
                        var local = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified).Add(time.ToTimeSpan());
                        if (zone.IsInvalidTime(local)) return null;
                        return new FeelDateTime(new DateTimeOffset(local, zone.GetUtcOffset(local)), zoneId, fractionText);
                    }

                    var offset = TimeSpan.Zero;
                    if (match.Groups["off"].Success)
                    {
                        var text = match.Groups["off"].Value;
                        var sign = text.StartsWith("-") ? -1 : 1;
                        offset = sign * TimeSpan.Parse(text.TrimStart('+', '-'), CultureInfo.InvariantCulture);
                        if (offset.Duration() > TimeSpan.FromHours(14)) return null;
                    }

                    var localOffset = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified).Add(time.ToTimeSpan());
                    if (!match.Groups["z"].Success && !match.Groups["off"].Success)
                    {
                        // No timezone in the text: the result is a local date-time
                        // (no zone id), matching the @"..." literal representation.
                        return new FeelDateTime(new DateTimeOffset(localOffset), "", fractionText);
                    }
                    return new DateTimeOffset(localOffset, offset);
                }
                if (val is DateTimeOffset) return val;
                return null;
            });

            Register("duration", new[] { "from" }, args =>
            {
                var s = ArgString(args, 0);
                if (s == null) return null;
                return ParseDuration(s);
            });

            Register("years and months duration", new[] { "from", "to" }, args =>
            {
                if (args.Length != 2) return null;
                if (!TryTemporalDate(Arg(args, 0), out var fromYear, out var fromMonth, out var fromDay)) return null;
                if (!TryTemporalDate(Arg(args, 1), out var toYear, out var toMonth, out var toDay)) return null;

                var months = (toYear - fromYear) * 12 + (toMonth - fromMonth);
                var direction = new FeelExpandedDate(toYear, toMonth, toDay)
                    .CompareTo(new FeelExpandedDate(fromYear, fromMonth, fromDay));
                if (direction > 0 && toDay < fromDay) months--;
                if (direction < 0 && toDay > fromDay) months++;
                return new FeelYmDuration(months);
            });

            Register("now", Array.Empty<string>(), _ => DateTimeOffset.Now);
            Register("today", Array.Empty<string>(), _ => DateOnly.FromDateTime(DateTime.Today));

            Register("day of year", new[] { "date" }, args =>
            {
                if (args.Length > 1) return null;
                var val = Arg(args, 0);
                if (val is DateOnly d) return (decimal)d.DayOfYear;
                if (val is DateTimeOffset dto) return (decimal)dto.DayOfYear;
                if (val is FeelDateTime zdt) return (decimal)zdt.Value.DayOfYear;
                if (val is FeelExpandedDateTime edt)
                    return (decimal)(edt.Date.ToDayNumber() - new FeelExpandedDate(edt.Date.Year, 1, 1).ToDayNumber() + 1);
                if (val is FeelExpandedDate ed)
                    return (decimal)(ed.ToDayNumber() - new FeelExpandedDate(ed.Year, 1, 1).ToDayNumber() + 1);
                return null;
            });

            Register("day of week", new[] { "date" }, args =>
            {
                if (args.Length != 1) return null;
                var val = Arg(args, 0);
                DayOfWeek dow;
                if (val is DateOnly d) dow = d.DayOfWeek;
                else if (val is DateTimeOffset dto) dow = dto.DayOfWeek;
                else if (val is FeelDateTime zdt) dow = zdt.Value.DayOfWeek;
                else if (val is FeelExpandedDate ed) dow = (DayOfWeek)(((ed.ToDayNumber() + 4) % 7 + 7) % 7);
                else if (val is FeelExpandedDateTime edt) dow = (DayOfWeek)(((edt.Date.ToDayNumber() + 4) % 7 + 7) % 7);
                else return null;
                return CultureInfo.InvariantCulture.DateTimeFormat.GetDayName(dow);
            });

            Register("month of year", new[] { "date" }, args =>
            {
                if (args.Length != 1) return null;
                var val = Arg(args, 0);
                int month;
                if (val is DateOnly d) month = d.Month;
                else if (val is DateTimeOffset dto) month = dto.Month;
                else if (val is FeelDateTime zdt) month = zdt.Value.Month;
                else if (val is FeelExpandedDate ed) month = ed.Month;
                else if (val is FeelExpandedDateTime edt) month = edt.Date.Month;
                else return null;
                return CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);
            });

            Register("week of year", new[] { "date" }, args =>
            {
                if (args.Length != 1) return null;
                var val = Arg(args, 0);
                DateTime dt;
                if (val is DateOnly d) dt = d.ToDateTime(TimeOnly.MinValue);
                else if (val is DateTimeOffset dto) dt = dto.DateTime;
                else if (val is FeelDateTime zdt) dt = zdt.Value.DateTime;
                else if (val is FeelExpandedDate ed) dt = ed.ToDateOnly().ToDateTime(TimeOnly.MinValue);
                else if (val is FeelExpandedDateTime edt) dt = edt.Date.ToDateOnly().ToDateTime(TimeOnly.MinValue);
                else return null;
                return (decimal)ISOWeek.GetWeekOfYear(dt);
            });
        }

        private static bool TryTemporalDate(object value, out int year, out int month, out int day)
        {
            switch (value)
            {
                case DateOnly d: year = d.Year; month = d.Month; day = d.Day; return true;
                case DateTimeOffset dto: year = dto.Year; month = dto.Month; day = dto.Day; return true;
                case FeelDateTime zdt: year = zdt.Value.Year; month = zdt.Value.Month; day = zdt.Value.Day; return true;
                case FeelExpandedDate ed: year = ed.Year; month = ed.Month; day = ed.Day; return true;
                case FeelExpandedDateTime edt:
                    year = edt.Date.Year; month = edt.Date.Month; day = edt.Date.Day; return true;
                default:
                    year = month = day = 0;
                    return false;
            }
        }

        private static bool TryParseExpandedDateTime(string raw, out FeelExpandedDateTime result)
        {
            result = default;
            var match = Regex.Match(raw,
                @"^(?<y>[+-]?\d{4,})-(?<mo>\d{2})-(?<d>\d{2})T(?<h>\d{2}):(?<mi>\d{2})(?::(?<sec>\d{2})(?:\.(?<f>\d{1,9}))?)?(?:(?<z>Z)|(?<off>[+-]\d{2}:\d{2})|@(?<zone>.+))?$",
                RegexOptions.None, RegexDefaults.Timeout);
            if (!match.Success) return false;

            if (match.Groups["y"].Value.StartsWith("+")) return false;
            var year = int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture);
            if (year is >= 1 and <= 9999) return false;
            if (Math.Abs((long)year) > 999999999) return false;

            var date = new FeelExpandedDate(year,
                int.Parse(match.Groups["mo"].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups["d"].Value, CultureInfo.InvariantCulture));
            var hour = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
            var minute = int.Parse(match.Groups["mi"].Value, CultureInfo.InvariantCulture);
            var second = match.Groups["sec"].Success ? int.Parse(match.Groups["sec"].Value, CultureInfo.InvariantCulture) : 0;
            var fraction = match.Groups["f"].Success ? match.Groups["f"].Value : null;
            var tickFraction = fraction == null ? 0L : long.Parse(fraction.PadRight(7, '0')[..7], CultureInfo.InvariantCulture);
            var time = new TimeOnly(hour, minute, second).Add(TimeSpan.FromTicks(tickFraction));

            var offset = TimeSpan.Zero;
            string zoneId = null;
            if (match.Groups["off"].Success)
            {
                var text = match.Groups["off"].Value;
                var sign = text.StartsWith("-") ? -1 : 1;
                offset = sign * TimeSpan.Parse(text.TrimStart('+', '-'), CultureInfo.InvariantCulture);
            }
            else if (match.Groups["zone"].Success)
            {
                zoneId = match.Groups["zone"].Value;
            }

            result = new FeelExpandedDateTime(date, time, fraction, offset, zoneId,
                match.Groups["z"].Success || match.Groups["off"].Success);
            return true;
        }

        private static bool TryParseTime(string raw, out FeelTime result)
        {
            result = default;
            var match = Regex.Match(raw, @"^(\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?)(?:(Z)|([+-]\d{2}:\d{2})|@(.+))?$", RegexOptions.None, RegexDefaults.Timeout);
            if (!match.Success) return false;

            var time = TimeOnly.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            TimeSpan? offset = null;
            string zoneId = null;

            if (match.Groups[2].Success) offset = TimeSpan.Zero;
            else if (match.Groups[3].Success)
            {
                var text = match.Groups[3].Value;
                var sign = text.StartsWith("-") ? -1 : 1;
                offset = sign * TimeSpan.Parse(text.TrimStart('+', '-'), CultureInfo.InvariantCulture);
                if (offset.Value.Duration() > TimeSpan.FromHours(14)) return false;
            }
            else if (match.Groups[4].Success)
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

        private static object ParseDuration(string s)
        {
            var negative = s.StartsWith('-');
            var raw = negative ? s.Substring(1) : s;
            if (!raw.StartsWith('P')) return null;
            raw = raw.Substring(1);
            if (raw.Length == 0) return null;
            if (!raw.Any(char.IsLetter)) return null;

            if (!raw.Contains('T') && !raw.Contains('D') && !raw.Contains('Y') && !raw.Contains('M')) return null;
            if (!raw.Contains('T') && (raw.Contains('H') || raw.Contains('S'))) return null;

            if (!raw.Contains('T') && !raw.Contains('D') && !raw.Contains('H') && !raw.Contains('S'))
            {
                var years = 0;
                var months = 0;
                var yMatch = Regex.Match(raw, @"(\d+)Y", RegexOptions.None, RegexDefaults.Timeout);
                if (yMatch.Success) years = int.Parse(yMatch.Groups[1].Value);
                var mMatch = Regex.Match(raw, @"(\d+)M", RegexOptions.None, RegexDefaults.Timeout);
                if (mMatch.Success) months = int.Parse(mMatch.Groups[1].Value);
                var total = years * 12 + months;
                if (negative) total = -total;
                return new FeelYmDuration(total);
            }

            var days = 0;
            var hours = 0;
            var minutes = 0;
            long secondTicks = 0;
            var dMatch = Regex.Match(raw, @"(\d+)D", RegexOptions.None, RegexDefaults.Timeout);
            if (dMatch.Success) days = int.Parse(dMatch.Groups[1].Value);
            var tIdx = raw.IndexOf('T');
            if (tIdx >= 0)
            {
                var timePart = raw.Substring(tIdx + 1);
                var hMatch = Regex.Match(timePart, @"(\d+)H", RegexOptions.None, RegexDefaults.Timeout);
                if (hMatch.Success) hours = int.Parse(hMatch.Groups[1].Value);
                var minMatch = Regex.Match(timePart, @"(\d+)M", RegexOptions.None, RegexDefaults.Timeout);
                if (minMatch.Success) minutes = int.Parse(minMatch.Groups[1].Value);
                var sMatch = Regex.Match(timePart, @"(\d+(?:\.\d*)?)S", RegexOptions.None, RegexDefaults.Timeout);
                if (sMatch.Success)
                {
                    var secondText = sMatch.Groups[1].Value;
                    if (secondText.EndsWith('.')) secondText = secondText[..^1];
                    var secondValue = decimal.Parse(secondText, CultureInfo.InvariantCulture);
                    secondTicks = (long)(secondValue * TimeSpan.TicksPerSecond);
                }
            }
            var ts = new TimeSpan(days, hours, minutes, 0).Add(TimeSpan.FromTicks(secondTicks));
            if (negative) ts = -ts;
            return ts;
        }

        // ==================== Conversion Functions ====================

        private static void RegisterConversionFunctions()
        {
            Register("number", new[] { "from", "grouping separator", "decimal separator" }, args =>
            {
                if (args.Length > 3) return null;
                var val = Arg(args, 0);
                if (val == null) return null;
                if (args.Length > 1 && Arg(args, 1) != null && Arg(args, 1) is not string) return null;
                if (args.Length > 2 && Arg(args, 2) != null && Arg(args, 2) is not string) return null;

                var groupSeparator = ArgString(args, 1);
                var decimalSeparator = ArgString(args, 2);
                if (groupSeparator != null && decimalSeparator != null && groupSeparator == decimalSeparator) return null;

                if (FeelValueComparer.IsNumeric(val)) return FeelValueComparer.ToDecimal(val);

                if (val is string s)
                {
                    var groupSep = groupSeparator;
                    var decSep = decimalSeparator;
                    if (decSep != null && decSep != "." && s.Contains('.') && groupSep != ".") return null;
                    if (groupSep != null) s = s.Replace(groupSep, "");
                    if (decSep != null && decSep != ".") s = s.Replace(decSep, ".");

                    // Anything left other than a simple decimal literal means the
                    // supplied separators did not describe the input.
                    if (!Regex.IsMatch(s, @"^[+-]?\d+(\.\d+)?$", RegexOptions.None, RegexDefaults.Timeout))
                        return null;
                    if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                        return d;
                }
                return null;
            });

            Register("string", new[] { "from" }, args =>
            {
                var val = Arg(args, 0);
                // FEEL functions propagate null: string(null) is null, not "null".
                if (val == null) return null;
                if (val is string s) return s;
                if (val is bool b) return b ? "true" : "false";
                if (FeelValueComparer.IsNumeric(val))
                    return FeelValueComparer.ToDecimal(val).ToString(CultureInfo.InvariantCulture);
                if (val is FeelExpandedDate ed) return ed.ToString();
                if (val is FeelExpandedDateTime edt) return edt.ToString();
                if (val is DateOnly d) return d.ToString("yyyy-MM-dd");
                if (val is FeelDateTime fdt) return fdt.ToString();
                if (val is DateTimeOffset dto) return dto.ToString("yyyy-MM-ddTHH:mm:sszzz");
                if (val is FeelTime ft) return ft.ToString();
                if (val is TimeSpan ts) return FormatTimeSpanDuration(ts);
                if (val is FeelYmDuration ym) return ym.ToString();
                return val.ToString();
            });
            Register("type", new[] { "value" }, args => TypeName(Arg(args, 0)));
            Register("range", new[] { "from" }, args =>
            {
                if (args.Length != 1) return null;
                var text = ArgString(args, 0)?.Trim();
                if (string.IsNullOrWhiteSpace(text) || text.Length < 5) return null;
                if (text[0] is not ('[' or '(' or ']') || text[^1] is not (']' or ')' or '[')) return null;

                var lowInclusive = text[0] == '[';
                var highInclusive = text[^1] == ']';
                var body = text[1..^1];
                var separator = body.IndexOf("..", StringComparison.Ordinal);
                if (separator < 0) return null;

                var lowText = body[..separator].Trim();
                var highText = body[(separator + 2)..].Trim();
                if (lowText.Length == 0 && highText.Length == 0) return null;

                // An endpoint written with a closed bracket must have a value:
                // "[..2]" and "[1..]" are not valid intervals.
                if (lowText.Length == 0 && lowInclusive) return null;
                if (highText.Length == 0 && highInclusive) return null;

                if (lowText.Contains("string(") || highText.Contains("string(")) return null;

                try
                {
                    var engine = new FeelEngine();
                    // Endpoint text is FEEL: resolve multi-word function names such
                    // as "date and time" the same way the expression evaluator does.
                    var endpointScope = new Parsing.FeelScope();
                    var low = lowText.Length == 0 ? null : engine.EvaluateExpression(lowText, new FeelEvaluationContext(), endpointScope);
                    var high = highText.Length == 0 ? null : engine.EvaluateExpression(highText, new FeelEvaluationContext(), endpointScope);
                    if ((lowText.Length > 0 && low == null) || (highText.Length > 0 && high == null)) return null;

                    if (low != null && high != null)
                    {
                        if (!Equals(TypeName(low), TypeName(high))) return null;
                        var comparison = FeelValueComparer.Compare(low, high);
                        if (comparison == null || comparison > 0) return null;
                    }

                    return new FeelRange(low, lowInclusive, high, highInclusive);
                }
                catch
                {
                    return null;
                }
            });
        }

        private static string FormatTimeSpanDuration(TimeSpan ts)
        {
            var neg = ts < TimeSpan.Zero;
            if (neg) ts = -ts;
            var parts = new List<string>();
            if (ts.Days > 0) parts.Add($"{ts.Days}D");
            var timeParts = new List<string>();
            if (ts.Hours > 0) timeParts.Add($"{ts.Hours}H");
            if (ts.Minutes > 0) timeParts.Add($"{ts.Minutes}M");
            if (ts.Seconds > 0) timeParts.Add($"{ts.Seconds}S");
            var result = "P" + string.Join("", parts);
            if (timeParts.Count > 0) result += "T" + string.Join("", timeParts);
            else if (parts.Count == 0) result += "T0S";
            return neg ? $"-{result}" : result;
        }

        // ==================== Context Functions ====================

        private static void RegisterContextFunctions()
        {
            Register("get value", new[] { "m", "key" }, args =>
            {
                var ctx = Arg(args, 0) as FeelContext;
                var key = ArgString(args, 1);
                if (ctx == null || key == null) return null;
                return ctx[key];
            });

            Register("get entries", new[] { "m" }, args =>
            {
                var ctx = Arg(args, 0) as FeelContext;
                if (ctx == null) return null;
                var result = new List<object>();
                foreach (var entry in ctx)
                {
                    var entryCtx = new FeelContext();
                    entryCtx.Put("key", entry.Key);
                    entryCtx.Put("value", entry.Value);
                    result.Add(entryCtx);
                }
                return (object)result;
            });

            Register("context", new[] { "entries" }, args =>
            {
                if (args.Length != 1) return null;
                var entries = ArgList(args, 0);
                if (entries == null) return null;
                var result = new FeelContext();
                foreach (var entry in entries)
                {
                    if (entry is not FeelContext entryCtx) return null;
                    if (!entryCtx.ContainsKey("key") || !entryCtx.ContainsKey("value")) return null;
                    var key = entryCtx["key"] as string;
                    if (key == null) return null;
                    if (result.ContainsKey(key)) return null;
                    result.Put(key, entryCtx["value"]);
                }
                return result;
            });

            Register("context put", new[] { "context", "key", "value" }, args =>
            {
                if (args.Length != 3) return null;
                var ctx = Arg(args, 0) as FeelContext;
                if (ctx == null) return null;

                var keyArg = Arg(args, 1);
                if (keyArg is string key)
                {
                    var result = new FeelContext();
                    result.Merge(ctx);
                    result.Put(key, Arg(args, 2));
                    return result;
                }

                if (keyArg is List<object> path && path.Count > 0)
                    return ContextPutPath(ctx, path, 0, Arg(args, 2));

                return null;
            });

            Register("context merge", new[] { "contexts" }, args =>
            {
                if (args.Length != 1) return null;
                var list = ArgList(args, 0);
                if (list == null) return null;
                if (list.Any(item => item is not FeelContext)) return null;
                var result = new FeelContext();
                foreach (var item in list)
                    result.Merge((FeelContext)item);
                return result;
            });
        }

        private static object ContextPutPath(FeelContext context, IReadOnlyList<object> path, int index, object value)
        {
            var key = path[index] as string ?? path[index]?.ToString();
            if (key == null) return null;

            var result = new FeelContext();
            result.Merge(context);

            if (index == path.Count - 1)
            {
                result.Put(key, value);
                return result;
            }

            if (!context.TryGetValue(key, out var existing) || existing is not FeelContext child)
                return null;

            var updated = ContextPutPath(child, path, index + 1, value);
            if (updated == null) return null;

            result.Put(key, updated);
            return result;
        }

        private static bool HasInvalidBackreference(string pattern)
        {
            var inCharacterClass = false;
            for (var i = 0; i < pattern.Length - 1; i++)
            {
                var c = pattern[i];
                if (c == '[') inCharacterClass = true;
                else if (c == ']') inCharacterClass = false;
                else if (c == '\\')
                {
                    var next = pattern[i + 1];
                    if (next == '0') return true;
                    if (inCharacterClass && char.IsDigit(next)) return true;
                    i++;
                }
            }
            return false;
        }

        private static string RewriteDotOutsideClasses(string pattern)
        {
            var builder = new System.Text.StringBuilder(pattern.Length);
            var inCharacterClass = false;
            for (var i = 0; i < pattern.Length; i++)
            {
                var c = pattern[i];
                if (c == '\\' && i + 1 < pattern.Length)
                {
                    builder.Append(c).Append(pattern[++i]);
                    continue;
                }
                if (c == '[') inCharacterClass = true;
                if (c == ']') inCharacterClass = false;
                builder.Append(!inCharacterClass && c == '.' ? "[^\\r\\n]" : c.ToString());
            }
            return builder.ToString();
        }

        private static string StripPatternWhitespace(string pattern)
        {
            var builder = new System.Text.StringBuilder(pattern.Length);
            var inCharacterClass = false;

            foreach (var c in pattern)
            {
                if (c == '[') inCharacterClass = true;
                if (c == ']') inCharacterClass = false;

                // FEEL's x flag ignores whitespace outside character classes,
                // including whitespace that follows a backslash (\ s -> \s).
                if (!inCharacterClass && char.IsWhiteSpace(c)) continue;
                builder.Append(c);
            }

            return builder.ToString();
        }

        // ==================== Range Functions ====================

        private static void RegisterRangeFunctions()
        {
            Register("before", new[] { "point1", "point2" }, args =>
            {
                var a = Arg(args, 0);
                var b = Arg(args, 1);
                return RangeRelation(a, b, "before");
            });

            Register("after", new[] { "point1", "point2" }, args =>
            {
                var a = Arg(args, 0);
                var b = Arg(args, 1);
                return RangeRelation(a, b, "after");
            });

            Register("meets", new[] { "range1", "range2" }, args =>
            {
                var a = Arg(args, 0) as FeelRange;
                var b = Arg(args, 1) as FeelRange;
                if (a == null || b == null) return null;
                return FeelValueComparer.FeelEqual(a.HighEndpoint, b.LowEndpoint) == true
                    && a.HighInclusive && b.LowInclusive;
            });

            Register("met by", new[] { "range1", "range2" }, args =>
            {
                var a = Arg(args, 0) as FeelRange;
                var b = Arg(args, 1) as FeelRange;
                if (a == null || b == null) return null;
                return FeelValueComparer.FeelEqual(a.LowEndpoint, b.HighEndpoint) == true
                    && a.LowInclusive && b.HighInclusive;
            });

            Register("overlaps", new[] { "range1", "range2" }, args =>
            {
                var a = Arg(args, 0) as FeelRange;
                var b = Arg(args, 1) as FeelRange;
                if (a == null || b == null) return null;
                // Two ranges overlap if neither is completely before the other
                var aBeforeB = FeelValueComparer.Compare(a.HighEndpoint, b.LowEndpoint);
                var bBeforeA = FeelValueComparer.Compare(b.HighEndpoint, a.LowEndpoint);
                if (aBeforeB == null || bBeforeA == null) return null;
                var aEndsBefore = aBeforeB < 0 || (aBeforeB == 0 && !(a.HighInclusive && b.LowInclusive));
                var bEndsBefore = bBeforeA < 0 || (bBeforeA == 0 && !(b.HighInclusive && a.LowInclusive));
                return (object)(!aEndsBefore && !bEndsBefore);
            });
            Register("overlaps before", new[] { "range1", "range2" }, args =>
            {
                var a = Arg(args, 0) as FeelRange;
                var b = Arg(args, 1) as FeelRange;
                return OverlapsBefore(a, b);
            });
            Register("overlaps after", new[] { "range1", "range2" }, args =>
            {
                var a = Arg(args, 0) as FeelRange;
                var b = Arg(args, 1) as FeelRange;
                return OverlapsBefore(b, a);
            });

            Register("includes", new[] { "range", "point" }, args =>
            {
                var range = Arg(args, 0) as FeelRange;
                var point = Arg(args, 1);
                if (range == null) return null;
                if (point is FeelRange inner)
                {
                    // Range includes range
                    var lowOk = FeelValueComparer.Compare(range.LowEndpoint, inner.LowEndpoint);
                    var highOk = FeelValueComparer.Compare(range.HighEndpoint, inner.HighEndpoint);
                    if (lowOk == null || highOk == null) return null;
                    return (object)(lowOk <= 0 && highOk >= 0);
                }
                return range.Contains(point);
            });

            Register("during", new[] { "point", "range" }, args =>
            {
                var point = Arg(args, 0);
                var range = Arg(args, 1) as FeelRange;
                if (range == null) return null;
                if (point is FeelRange inner)
                {
                    var lowOk = FeelValueComparer.Compare(range.LowEndpoint, inner.LowEndpoint);
                    var highOk = FeelValueComparer.Compare(range.HighEndpoint, inner.HighEndpoint);
                    if (lowOk == null || highOk == null) return null;
                    return (object)(lowOk <= 0 && highOk >= 0);
                }
                return range.Contains(point);
            });

            Register("starts", new[] { "point", "range" }, args =>
            {
                var point = Arg(args, 0);
                var range = Arg(args, 1) as FeelRange;
                if (range == null) return null;
                if (point is FeelRange pointRange) return RangeStarts(pointRange, range);
                return range.LowInclusive && FeelValueComparer.FeelEqual(point, range.LowEndpoint) == true;
            });

            Register("started by", new[] { "range", "point" }, args =>
            {
                var range = Arg(args, 0) as FeelRange;
                var point = Arg(args, 1);
                if (range == null) return null;
                if (point is FeelRange pointRange) return RangeStarts(pointRange, range);
                return range.LowInclusive && FeelValueComparer.FeelEqual(range.LowEndpoint, point) == true;
            });

            Register("finishes", new[] { "point", "range" }, args =>
            {
                var point = Arg(args, 0);
                var range = Arg(args, 1) as FeelRange;
                if (range == null) return null;
                if (point is FeelRange pointRange) return RangeFinishes(pointRange, range);
                return range.HighInclusive && FeelValueComparer.FeelEqual(point, range.HighEndpoint) == true;
            });

            Register("finished by", new[] { "range", "point" }, args =>
            {
                var range = Arg(args, 0) as FeelRange;
                var point = Arg(args, 1);
                if (range == null) return null;
                if (point is FeelRange pointRange) return RangeFinishes(pointRange, range);
                return range.HighInclusive && FeelValueComparer.FeelEqual(range.HighEndpoint, point) == true;
            });

            Register("coincides", new[] { "point1", "point2" }, args =>
            {
                var a = Arg(args, 0);
                var b = Arg(args, 1);
                if (a is FeelRange ra && b is FeelRange rb)
                {
                    return (object)(FeelValueComparer.FeelEqual(ra.LowEndpoint, rb.LowEndpoint) == true
                        && FeelValueComparer.FeelEqual(ra.HighEndpoint, rb.HighEndpoint) == true
                        && ra.LowInclusive == rb.LowInclusive
                        && ra.HighInclusive == rb.HighInclusive);
                }
                return FeelValueComparer.FeelEqual(a, b);
            });
        }

        /// <summary>
        /// True when range <paramref name="a"/> starts range <paramref name="b"/>:
        /// they share the same lower bound (with the same inclusivity) and
        /// <paramref name="a"/> does not extend past the upper bound of
        /// <paramref name="b"/>.
        /// </summary>
        private static bool? RangeStarts(FeelRange a, FeelRange b)
        {
            if (a.LowEndpoint == null || b.LowEndpoint == null) return null;
            var low = FeelValueComparer.Compare(a.LowEndpoint, b.LowEndpoint);
            if (low == null) return null;
            if (low != 0 || a.LowInclusive != b.LowInclusive) return false;

            var high = FeelValueComparer.Compare(a.HighEndpoint, b.HighEndpoint);
            if (high == null) return null;
            if (high < 0) return true;
            if (high > 0) return false;
            return b.HighInclusive || !a.HighInclusive;
        }

        /// <summary>
        /// True when range <paramref name="a"/> finishes range <paramref name="b"/>:
        /// they share the same upper bound (with the same inclusivity) and
        /// <paramref name="a"/> does not start before the lower bound of
        /// <paramref name="b"/>.
        /// </summary>
        private static bool? RangeFinishes(FeelRange a, FeelRange b)
        {
            if (a.HighEndpoint == null || b.HighEndpoint == null) return null;
            var high = FeelValueComparer.Compare(a.HighEndpoint, b.HighEndpoint);
            if (high == null) return null;
            if (high != 0 || a.HighInclusive != b.HighInclusive) return false;

            var low = FeelValueComparer.Compare(a.LowEndpoint, b.LowEndpoint);
            if (low == null) return null;
            if (low > 0) return true;
            if (low < 0) return false;
            return b.LowInclusive || !a.LowInclusive;
        }

        private static bool? OverlapsBefore(FeelRange a, FeelRange b)
        {
            if (a == null || b == null) return null;
            var start = FeelValueComparer.Compare(a.LowEndpoint, b.LowEndpoint);
            var end = FeelValueComparer.Compare(a.HighEndpoint, b.LowEndpoint);
            if (start == null || end == null) return null;
            var startOk = start < 0 || (start == 0 && a.LowInclusive && !b.LowInclusive);
            var endOk = end > 0 || (end == 0 && a.HighInclusive && b.LowInclusive);
            return startOk && endOk;
        }
        private static object RangeRelation(object a, object b, string relation)
        {
            // Point before/after point
            if (a is not FeelRange && b is not FeelRange)
            {
                var cmp = FeelValueComparer.Compare(a, b);
                if (cmp == null) return null;
                return relation == "before" ? (object)(cmp < 0) : (object)(cmp > 0);
            }

            // Point before/after range or range before/after point
            if (a is FeelRange ra && b is not FeelRange)
            {
                var cmp = FeelValueComparer.Compare(ra.HighEndpoint, b);
                if (cmp == null) return null;
                if (relation == "before")
                    return (object)(cmp < 0 || (cmp == 0 && !ra.HighInclusive));
                cmp = FeelValueComparer.Compare(ra.LowEndpoint, b);
                if (cmp == null) return null;
                return (object)(cmp > 0 || (cmp == 0 && !ra.LowInclusive));
            }

            if (a is not FeelRange && b is FeelRange rb)
            {
                var cmp = FeelValueComparer.Compare(a, rb.LowEndpoint);
                if (cmp == null) return null;
                if (relation == "before")
                    return (object)(cmp < 0 || (cmp == 0 && !rb.LowInclusive));
                cmp = FeelValueComparer.Compare(a, rb.HighEndpoint);
                if (cmp == null) return null;
                return (object)(cmp > 0 || (cmp == 0 && !rb.HighInclusive));
            }

            // Range before/after range
            if (a is FeelRange ra2 && b is FeelRange rb2)
            {
                if (relation == "before")
                {
                    var cmp = FeelValueComparer.Compare(ra2.HighEndpoint, rb2.LowEndpoint);
                    if (cmp == null) return null;
                    return (object)(cmp < 0 || (cmp == 0 && !(ra2.HighInclusive && rb2.LowInclusive)));
                }
                else
                {
                    var cmp = FeelValueComparer.Compare(ra2.LowEndpoint, rb2.HighEndpoint);
                    if (cmp == null) return null;
                    return (object)(cmp > 0 || (cmp == 0 && !(ra2.LowInclusive && rb2.HighInclusive)));
                }
            }

            return null;
        }
    }
}
