using System;
using System.Collections.Generic;
using System.Linq;

namespace ScratchyDisk.DmnEngine.Feel.Types
{
    /// <summary>
    /// Checks and applies the DMN/FEEL type conformance rules for declared types.
    /// A declared type constrains a value: values of another type are not silently
    /// converted (a number is not a string). The conversions the specification
    /// permits are applied explicitly: numeric widening, scalar to singleton list
    /// and singleton list to scalar (one level only), and date / date-time
    /// interchange.
    /// </summary>
    public static class FeelTypeConformance
    {
        /// <summary>
        /// Tries to convert <paramref name="value"/> to the declared FEEL type
        /// <paramref name="feelTypeName"/>.
        /// </summary>
        /// <param name="value">Value to check</param>
        /// <param name="feelTypeName">Declared FEEL type name ("number", "list&lt;string&gt;", ...)</param>
        /// <param name="typeAliases">Optional named item type definitions ("tPerson" -> "context&lt;...&gt;")</param>
        /// <param name="result">Converted value when the check succeeds</param>
        /// <returns>True when the value conforms, otherwise false</returns>
        public static bool TryCoerce(
            object value,
            string feelTypeName,
            IReadOnlyDictionary<string, string> typeAliases,
            out object result)
        {
            result = value;
            if (string.IsNullOrWhiteSpace(feelTypeName)) return true;

            feelTypeName = ResolveAlias(feelTypeName, typeAliases);
            if (string.Equals(feelTypeName, "any", StringComparison.OrdinalIgnoreCase)) return true;

            // FEEL null is a value and is compatible with every declared type.
            if (value == null)
            {
                result = null;
                return true;
            }

            if (IsGeneric(feelTypeName, "list", out var elementType))
                return TryCoerceListElementOrList(value, elementType, typeAliases, out result);

            if (IsGeneric(feelTypeName, "context", out var fieldsText))
                return TryCoerceContext(value, fieldsText, typeAliases, out result);

            if (string.Equals(feelTypeName, "list", StringComparison.OrdinalIgnoreCase))
            {
                if (value is List<object> list)
                {
                    result = list;
                    return true;
                }

                result = new List<object> { value };
                return true;
            }

            if (string.Equals(feelTypeName, "context", StringComparison.OrdinalIgnoreCase))
            {
                return TryCoerceContext(value, null, typeAliases, out result);
            }

            if (feelTypeName.StartsWith("function", StringComparison.OrdinalIgnoreCase))
            {
                result = value;
                return value is FeelFunction;
            }

            // A singleton list may be coerced to a scalar (one level only, so a
            // nested list is not repeatedly unwrapped).
            if (value is List<object> singleton)
            {
                if (singleton.Count != 1) return false;
                return TryCoerceScalar(singleton[0], feelTypeName, out result);
            }

            return TryCoerceScalar(value, feelTypeName, out result);
        }

        /// <summary>
        /// Returns true when <paramref name="value"/> conforms to the declared type.
        /// </summary>
        public static bool IsCompatible(
            object value,
            string feelTypeName,
            IReadOnlyDictionary<string, string> typeAliases = null)
        {
            return TryCoerce(value, feelTypeName, typeAliases, out _);
        }

        private static bool TryCoerceListElementOrList(
            object value,
            string elementType,
            IReadOnlyDictionary<string, string> typeAliases,
            out object result)
        {
            if (value is List<object> list)
            {
                var converted = new List<object>(list.Count);
                foreach (var item in list)
                {
                    if (!TryCoerce(item, elementType, typeAliases, out var convertedItem))
                    {
                        result = null;
                        return false;
                    }

                    converted.Add(convertedItem);
                }

                result = converted;
                return true;
            }

            // A scalar conforms to a list of its own type as a singleton list.
            if (!TryCoerce(value, elementType, typeAliases, out var element))
            {
                result = null;
                return false;
            }

            result = new List<object> { element };
            return true;
        }

        private static bool TryCoerceContext(
            object value,
            string fieldsText,
            IReadOnlyDictionary<string, string> typeAliases,
            out object result)
        {
            if (value is not FeelContext context)
            {
                result = null;
                return false;
            }

            // A context value may contain more entries than the declared type:
            // it conforms to a type whose components are all present.
            if (string.IsNullOrWhiteSpace(fieldsText))
            {
                result = context;
                return true;
            }

            var converted = new FeelContext();
            foreach (var entry in context.Entries) converted.Put(entry.Key, entry.Value);

            foreach (var field in SplitTypeFields(fieldsText))
            {
                var separator = field.IndexOf(':');
                if (separator <= 0)
                {
                    result = null;
                    return false;
                }

                var key = field[..separator];
                var fieldType = field[(separator + 1)..];
                if (!context.ContainsKey(key))
                {
                    result = null;
                    return false;
                }

                if (!TryCoerce(context[key], fieldType, typeAliases, out var fieldValue))
                {
                    result = null;
                    return false;
                }

                converted.Put(key, fieldValue);
            }

            result = converted;
            return true;
        }

        private static bool TryCoerceScalar(object value, string feelTypeName, out object result)
        {
            result = value;
            switch (feelTypeName.ToLowerInvariant())
            {
                case "number":
                    if (!FeelValueComparer.IsNumeric(value)) return false;
                    result = FeelValueComparer.ToDecimal(value);
                    return true;

                case "string":
                    return value is string;

                case "boolean":
                    return value is bool;

                case "date":
                    switch (value)
                    {
                        case DateOnly:
                        case FeelExpandedDate:
                            result = value;
                            return true;
                        case DateTime dateTime:
                            result = DateOnly.FromDateTime(dateTime);
                            return true;
                        case DateTimeOffset offset:
                            result = DateOnly.FromDateTime(offset.DateTime);
                            return true;
                        case FeelDateTime zoned:
                            result = DateOnly.FromDateTime(zoned.Value.DateTime);
                            return true;
                        default:
                            return false;
                    }

                case "date and time":
                case "datetime":
                    switch (value)
                    {
                        case DateTimeOffset:
                        case FeelDateTime:
                        case FeelExpandedDateTime:
                            result = value;
                            return true;
                        case DateOnly date:
                            result = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                            return true;
                        case DateTime dateTime:
                            result = new DateTimeOffset(dateTime);
                            return true;
                        default:
                            return false;
                    }

                case "time":
                    return value is FeelTime;

                case "days and time duration":
                case "daytimeduration":
                    return value is TimeSpan;

                case "years and months duration":
                case "yearmonthduration":
                    return value is FeelYmDuration;

                case "range":
                    return value is FeelRange;

                case "function":
                    return value is FeelFunction;

                case "null":
                    return false;

                default:
                    // Unknown/unmodelled named type: do not reject values the
                    // engine cannot describe structurally.
                    return true;
            }
        }

        private static string ResolveAlias(string typeName, IReadOnlyDictionary<string, string> typeAliases)
        {
            if (typeAliases == null) return typeName;

            // Guard against recursive item definitions.
            for (var depth = 0; depth < 16; depth++)
            {
                if (!typeAliases.TryGetValue(typeName, out var aliased) || string.IsNullOrWhiteSpace(aliased))
                    return typeName;

                if (string.Equals(aliased, typeName, StringComparison.Ordinal)) return typeName;
                typeName = aliased;
            }

            return typeName;
        }

        private static bool IsGeneric(string typeName, string name, out string inner)
        {
            inner = null;
            var prefix = name + "<";
            if (!typeName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !typeName.EndsWith(">"))
                return false;

            inner = typeName[prefix.Length..^1];
            return true;
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
    }
}
