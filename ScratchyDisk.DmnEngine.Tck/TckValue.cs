using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ScratchyDisk.DmnEngine.Feel.Types;

namespace ScratchyDisk.DmnEngine.Tck;

/// <summary>
/// Converts TCK test-case values to engine values and compares them using
/// DMN/FEEL semantics. Values are carried as plain CLR objects, with two
/// wrappers that preserve facts the CLR types lose:
/// a date-time may or may not have a timezone, and a time may or may not have
/// an offset. FEEL compares those differently, so the wrapper is required.
/// </summary>
public static class TckValue
{
    public static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>Absolute tolerance used for number comparison (same as the Camunda TCK runner).</summary>
    public const decimal NumberTolerance = 0.00000001m;

    public readonly record struct TckDateTime(DateTimeOffset Value, bool HasOffset);
    public readonly record struct TckTime(FeelTime Value, bool HasOffset);

    public static object Parse(XElement valueType)
    {
        if (valueType == null) return null;

        var list = Child(valueType, "list");
        if (list != null)
        {
            return list.Elements()
                .Where(e => e.Name.LocalName == "item")
                .Select(Parse)
                .ToList();
        }

        var components = valueType.Elements().Where(e => e.Name.LocalName == "component").ToList();
        if (components.Count > 0)
        {
            var context = new FeelContext();
            foreach (var component in components)
            {
                context.Put((string)component.Attribute("name"), Parse(component));
            }
            return context;
        }

        var value = Child(valueType, "value");
        if (value == null) return null;

        if ((string)value.Attribute(Xsi + "nil") == "true") return null;
        if (value.IsEmpty && string.IsNullOrEmpty(value.Value)) return null;

        var type = (string)value.Attribute(Xsi + "type");
        return ParseScalar(value.Value, type);
    }

    private static object ParseScalar(string text, string type)
    {
        var local = type == null
            ? null
            : type.Contains(':') ? type[(type.IndexOf(':') + 1)..] : type;

        switch (local)
        {
            case "decimal":
            case "integer":
            case "int":
            case "long":
            case "short":
            case "byte":
                return decimal.Parse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
            case "double":
            case "float":
                return XmlConvert.ToDouble(text.Trim());
            case "boolean":
                return XmlConvert.ToBoolean(text.Trim());
            case "string":
                return text;
            case "date":
                return ParseDate(text.Trim());
            case "dateTime":
                return ParseDateTime(text.Trim());
            case "time":
                return ParseTime(text.Trim());
            case "duration":
                return ParseDuration(text.Trim());
            case null:
                // TCK leaves many values untyped. Follow the vendor-runner convention:
                // boolean first, then number, then string.
                var trimmed = text.Trim();
                if (bool.TryParse(trimmed, out var boolean)) return boolean;
                if (decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return number;
                return text;
            default:
                throw new NotSupportedException($"Unsupported TCK value type '{type}'");
        }
    }

    private static DateOnly ParseDate(string text)
    {
        // XSD allows negative years; DateOnly does not. Let the caller report a fixture error.
        return DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static TckDateTime ParseDateTime(string text)
    {
        var match = Regex.Match(
            text,
            @"^(?<wall>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?)(?<offset>Z|[+-]\d{2}:\d{2})?$");
        if (!match.Success) throw new FormatException($"Invalid dateTime '{text}'");

        var hasOffset = match.Groups["offset"].Success;
        var value = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture,
            hasOffset ? DateTimeStyles.None : DateTimeStyles.AssumeUniversal);
        return new TckDateTime(value, hasOffset);
    }

    private static TckTime ParseTime(string text)
    {
        var match = Regex.Match(
            text,
            @"^(?<time>\d{2}:\d{2}(:\d{2}(\.\d+)?)?)(?<offset>Z|[+-]\d{2}:\d{2})?$");
        if (!match.Success) throw new FormatException($"Invalid time '{text}'");

        var time = TimeOnly.Parse(match.Groups["time"].Value, CultureInfo.InvariantCulture);
        if (!match.Groups["offset"].Success) return new TckTime(new FeelTime(time), false);

        var offsetText = match.Groups["offset"].Value;
        var offset = offsetText == "Z"
            ? TimeSpan.Zero
            : TimeSpan.Parse(offsetText.TrimStart('+'), CultureInfo.InvariantCulture);
        if (offsetText.StartsWith("-")) offset = -offset;
        return new TckTime(new FeelTime(time, offset), true);
    }

    private static object ParseDuration(string text)
    {
        var sign = text.StartsWith("-") ? -1 : 1;
        var body = text.TrimStart('-');

        // A duration is years-months only when its M is in the date part
        // (P1Y2M). In PT1M the M is minutes and belongs to days-time.
        var timeIndex = body.IndexOf('T');
        var datePart = timeIndex < 0 ? body : body[..timeIndex];
        var hasTimePart = timeIndex >= 0 && timeIndex < body.Length - 1;

        var yearsMonths = Regex.Match(datePart, @"^P(?:(\d+)Y)?(?:(\d+)M)?$");
        if (!hasTimePart && yearsMonths.Success
            && (yearsMonths.Groups[1].Success || yearsMonths.Groups[2].Success))
        {
            var years = yearsMonths.Groups[1].Success ? int.Parse(yearsMonths.Groups[1].Value) : 0;
            var months = yearsMonths.Groups[2].Success ? int.Parse(yearsMonths.Groups[2].Value) : 0;
            return new FeelYmDuration(sign * (years * 12 + months));
        }

        if (!hasTimePart && (datePart.Contains('Y') || datePart.Contains('M')) && datePart.Any(char.IsDigit))
            throw new NotSupportedException($"Mixed FEEL duration '{text}' is not representable");
        return XmlConvert.ToTimeSpan(text);
    }

    /// <summary>
    /// Compares an expected TCK value with an engine result. Returns false and a
    /// human-readable explanation on mismatch.
    /// </summary>
    public static bool Compare(object expected, object actual, out string reason)
    {
        reason = null;
        if (expected == null && actual == null) return true;
        if (expected == null) { reason = "expected null, got a value"; return false; }
        if (actual == null) { reason = "expected a value, got null"; return false; }

        // Range results are compared as the four FEEL range properties, which is
        // how the TCK expresses them.
        var expectedIsContext = TryAsContext(expected, out var expectedContext);
        var actualIsContext = TryAsContext(actual, out var actualContext);
        if (expectedIsContext && actualIsContext)
            return CompareContexts(expectedContext, actualContext, out reason);

        if (expected is FeelRange || actual is FeelRange)
        {
            TryAsContext(expected is FeelRange expectedRange ? RangeToContext(expectedRange) : expected, out var expectedRangeContext);
            TryAsContext(actual is FeelRange actualRange ? RangeToContext(actualRange) : actual, out var actualRangeContext);
            if (expectedRangeContext != null && actualRangeContext != null)
                return CompareContexts(expectedRangeContext, actualRangeContext, out reason);
        }

        if (TryAsList(expected, out var expectedList) && TryAsList(actual, out var actualList))
        {
            if (expectedList.Count != actualList.Count)
            {
                reason = $"list size {expectedList.Count} vs {actualList.Count}";
                return false;
            }
            for (var i = 0; i < expectedList.Count; i++)
            {
                if (!Compare(expectedList[i], actualList[i], out var itemReason))
                {
                    reason = $"item {i}: {itemReason}";
                    return false;
                }
            }
            return true;
        }

        if (IsNumber(expected) && IsNumber(actual))
        {
            if (!TryToDecimal(expected, out var left) || !TryToDecimal(actual, out var right))
            {
                reason = $"number {Format(expected)} vs {Format(actual)}";
                return false;
            }
            if (left == right || Math.Abs(left - right) <= NumberTolerance) return true;
            reason = $"number {Format(left)} vs {Format(right)}";
            return false;
        }

        if (expected is bool expectedBoolean && actual is bool actualBoolean)
        {
            if (expectedBoolean == actualBoolean) return true;
            reason = $"{expectedBoolean.ToString().ToLowerInvariant()} vs {actualBoolean.ToString().ToLowerInvariant()}";
            return false;
        }

        if (expected is string expectedString && actual is string actualString)
        {
            if (string.Equals(expectedString, actualString, StringComparison.Ordinal)) return true;
            reason = $"string {Format(expected)} vs {Format(actual)}";
            return false;
        }

        if (expected is DateOnly || actual is DateOnly || expected is DateTime || actual is DateTime)
        {
            if (TryAsDate(expected, out var expectedDate) && TryAsDate(actual, out var actualDate))
            {
                if (expectedDate == actualDate) return true;
                reason = $"date {expectedDate:yyyy-MM-dd} vs {actualDate:yyyy-MM-dd}";
                return false;
            }
        }

        if (expected is TckDateTime || actual is DateTimeOffset || expected is DateTimeOffset)
        {
            if (CompareDateTimes(expected, actual, out reason)) return true;
            if (reason != null) return false;
        }

        if (expected is TckTime || actual is FeelTime)
        {
            if (TryAsTime(expected, out var expectedTime, out var expectedHasOffset)
                && TryAsTime(actual, out var actualTime, out var actualHasOffset))
            {
                if (expectedTime.Time == actualTime.Time
                    && (!expectedHasOffset || expectedTime.Offset == actualTime.Offset))
                    return true;
                reason = $"time {Format(expected)} vs {Format(actual)}";
                return false;
            }
        }

        if (expected is FeelYmDuration || actual is FeelYmDuration)
        {
            if (expected is FeelYmDuration expectedYm && actual is FeelYmDuration actualYm)
            {
                if (expectedYm.TotalMonths == actualYm.TotalMonths) return true;
                reason = $"years-months duration {Format(expected)} vs {Format(actual)}";
                return false;
            }
        }

        if (expected is TimeSpan || actual is TimeSpan)
        {
            if (expected is TimeSpan expectedSpan && actual is TimeSpan actualSpan)
            {
                if (expectedSpan == actualSpan) return true;
                reason = $"duration {Format(expected)} vs {Format(actual)}";
                return false;
            }
        }

        if (expected.Equals(actual)) return true;
        reason = $"{Format(expected)} vs {Format(actual)}";
        return false;
    }

    private static bool CompareDateTimes(object expected, object actual, out string reason)
    {
        reason = null;
        if (!TryAsDateTime(expected, out var expectedValue, out var expectedHasOffset)) return false;
        if (!TryAsDateTime(actual, out var actualValue, out var actualHasOffset)) return false;

        var equal = expectedHasOffset
            ? expectedValue.UtcDateTime == actualValue.UtcDateTime
            : expectedValue.DateTime == actualValue.DateTime;

        if (equal) return true;
        reason = $"date-time {Format(expected)} vs {Format(actual)}";
        return false;
    }

    private static bool CompareContexts(
        IReadOnlyDictionary<string, object> expected,
        IReadOnlyDictionary<string, object> actual,
        out string reason)
    {
        reason = null;
        if (expected.Count != actual.Count)
        {
            reason = $"context has {expected.Count} entries, actual {actual.Count}";
            return false;
        }

        foreach (var entry in expected)
        {
            if (!actual.TryGetValue(entry.Key, out var actualValue))
            {
                reason = $"missing entry '{entry.Key}'";
                return false;
            }
            if (!Compare(entry.Value, actualValue, out var entryReason))
            {
                reason = $"entry '{entry.Key}': {entryReason}";
                return false;
            }
        }
        return true;
    }

    private static FeelContext RangeToContext(FeelRange range)
    {
        var context = new FeelContext();
        context.Put("start included", range.LowInclusive);
        context.Put("start", range.LowEndpoint);
        context.Put("end", range.HighEndpoint);
        context.Put("end included", range.HighInclusive);
        return context;
    }

    private static bool TryAsContext(object value, out IReadOnlyDictionary<string, object> context)
    {
        switch (value)
        {
            case FeelContext feelContext:
                context = feelContext.Entries.ToDictionary(e => e.Key, e => e.Value);
                return true;
            case IDictionary<string, object> dictionary:
                context = new Dictionary<string, object>(dictionary);
                return true;
            default:
                context = null;
                return false;
        }
    }

    private static bool TryAsList(object value, out IReadOnlyList<object> list)
    {
        if (value is string || value is FeelContext || value is IDictionary)
        {
            list = null;
            return false;
        }

        if (value is IEnumerable<object> objects)
        {
            list = objects.ToList();
            return true;
        }

        if (value is IEnumerable enumerable)
        {
            list = enumerable.Cast<object>().ToList();
            return true;
        }

        list = null;
        return false;
    }

    private static bool TryAsDate(object value, out DateOnly date)
    {
        switch (value)
        {
            case DateOnly d:
                date = d;
                return true;
            case DateTime dt:
                date = DateOnly.FromDateTime(dt);
                return true;
            case TckDateTime tck:
                date = DateOnly.FromDateTime(tck.Value.DateTime);
                return true;
            default:
                date = default;
                return false;
        }
    }

    private static bool TryAsDateTime(object value, out DateTimeOffset dateTime, out bool hasOffset)
    {
        switch (value)
        {
            case TckDateTime tck:
                dateTime = tck.Value;
                hasOffset = tck.HasOffset;
                return true;
            case DateTimeOffset dto:
                dateTime = dto;
                // The engine has no separate timezone-less type, so treat a zero
                // offset as timezone-less; this is the only representation available.
                hasOffset = dto.Offset != TimeSpan.Zero;
                return true;
            case DateTime dt:
                dateTime = new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), TimeSpan.Zero);
                hasOffset = false;
                return true;
            default:
                dateTime = default;
                hasOffset = false;
                return false;
        }
    }

    private static bool TryAsTime(object value, out FeelTime time, out bool hasOffset)
    {
        switch (value)
        {
            case TckTime tck:
                time = tck.Value;
                hasOffset = tck.HasOffset;
                return true;
            case FeelTime feelTime:
                time = feelTime;
                hasOffset = feelTime.HasOffset;
                return true;
            default:
                time = default;
                hasOffset = false;
                return false;
        }
    }

    private static bool IsNumber(object value) =>
        value is decimal or double or float or int or long or short or byte or sbyte or uint or ulong or ushort;

    private static bool TryToDecimal(object value, out decimal result)
    {
        if (value is double d && (double.IsNaN(d) || double.IsInfinity(d)))
        {
            result = default;
            return false;
        }
        try
        {
            result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (OverflowException)
        {
            result = default;
            return false;
        }
    }

    public static string Format(object value)
    {
        switch (value)
        {
            case null: return "null";
            case string s: return $"\"{s}\"";
            case bool b: return b ? "true" : "false";
            case decimal d: return d.ToString(CultureInfo.InvariantCulture);
            case double d: return d.ToString("R", CultureInfo.InvariantCulture);
            case DateOnly date: return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            case TckDateTime tck: return tck.Value.ToString("O", CultureInfo.InvariantCulture);
            case DateTimeOffset dto: return dto.ToString("O", CultureInfo.InvariantCulture);
            case DateTime dt: return dt.ToString("O", CultureInfo.InvariantCulture);
            case TimeSpan span: return XmlConvert.ToString(span);
            case FeelTime time: return time.ToString();
            case FeelYmDuration yearsMonths: return yearsMonths.ToString();
            case FeelRange range: return range.ToString();
            case FeelContext context:
                return "{" + string.Join(", ", context.Entries.Select(e => $"{e.Key}: {Format(e.Value)}")) + "}";
            case IEnumerable<object> list:
                return "[" + string.Join(", ", list.Select(Format)) + "]";
            default:
                return value.ToString();
        }
    }

    private static XElement Child(XElement element, string localName) =>
        element.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
}
