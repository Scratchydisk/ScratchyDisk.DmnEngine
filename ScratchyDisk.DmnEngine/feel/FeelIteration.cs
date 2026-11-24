using System;
using System.Collections.Generic;
using ScratchyDisk.DmnEngine.Feel.Eval;
using ScratchyDisk.DmnEngine.Feel.Types;

namespace ScratchyDisk.DmnEngine.Feel
{
    /// <summary>
    /// Range iteration shared by the FEEL evaluator (for/some/every) and the executable
    /// boxed expressions. A FEEL range is bounded by its endpoints, not by the size of
    /// its text, so every enumeration is validated and capped by
    /// <see cref="FeelEvaluationLimits.MaxIterationItems"/>.
    /// </summary>
    internal static class FeelIteration
    {
        private static int MaxItems => FeelEvaluationLimits.MaxIterationItems;

        /// <summary>
        /// Enumerates the whole numbers in a numeric range. Endpoints must be whole
        /// numbers; a fractional endpoint is an evaluation error rather than a silent
        /// truncation.
        /// </summary>
        /// <exception cref="FeelEvaluationException">An endpoint is not a whole number, or the range exceeds the iteration limit.</exception>
        public static IEnumerable<decimal> Numbers(decimal start, decimal end,
            bool startInclusive = true, bool endInclusive = true)
        {
            if (start != decimal.Truncate(start) || end != decimal.Truncate(end))
                throw new FeelEvaluationException(
                    $"Range endpoints must be whole numbers to be iterated: {start}..{end}");

            // Ranges are iterated in the declared direction; the evaluator has always
            // supported a descending range, so keep that behaviour.
            var step = start <= end ? 1m : -1m;
            var first = startInclusive ? start : start + step;
            var last = endInclusive ? end : end - step;

            var count = step > 0
                ? (last < first ? 0 : last - first + 1)
                : (last > first ? 0 : first - last + 1);
            EnsureWithinLimit(count, $"{start}..{end}");

            for (var value = first; step > 0 ? value <= last : value >= last; value += step)
                yield return value;
        }

        /// <summary>
        /// Enumerates the days in a date range.
        /// </summary>
        /// <exception cref="FeelEvaluationException">The range exceeds the iteration limit.</exception>
        public static IEnumerable<DateOnly> Dates(DateOnly start, DateOnly end,
            bool startInclusive = true, bool endInclusive = true)
        {
            var step = start <= end ? 1 : -1;
            var first = startInclusive ? start : start.AddDays(step);
            var last = endInclusive ? end : end.AddDays(-step);

            var days = Math.Abs(last.DayNumber - first.DayNumber);
            var count = step > 0
                ? (last < first ? 0 : days + 1)
                : (last > first ? 0 : days + 1);
            EnsureWithinLimit(count, $"{start:yyyy-MM-dd}..{end:yyyy-MM-dd}");

            for (var date = first; step > 0 ? date <= last : date >= last; date = date.AddDays(step))
                yield return date;
        }

        /// <summary>
        /// Enumerates a range as boxed FEEL values. Returns null for a unary-test range
        /// and for endpoint types that cannot be iterated.
        /// </summary>
        public static IReadOnlyList<object> Items(FeelRange range)
        {
            if (range == null || range.IsUnaryTest) return null;

            if (FeelValueComparer.IsNumeric(range.LowEndpoint)
                && FeelValueComparer.IsNumeric(range.HighEndpoint))
            {
                var start = FeelValueComparer.ToDecimal(range.LowEndpoint);
                var end = FeelValueComparer.ToDecimal(range.HighEndpoint);
                var items = new List<object>();
                foreach (var value in Numbers(start, end, range.LowInclusive, range.HighInclusive))
                    items.Add(value);
                return items;
            }

            if (range.LowEndpoint is DateOnly startDate && range.HighEndpoint is DateOnly endDate)
            {
                var items = new List<object>();
                foreach (var date in Dates(startDate, endDate, range.LowInclusive, range.HighInclusive))
                    items.Add(date);
                return items;
            }

            return null;
        }

        /// <summary>
        /// Throws when an iteration would produce more items than the configured limit.
        /// </summary>
        /// <exception cref="FeelEvaluationException">The limit is exceeded.</exception>
        public static void EnsureWithinLimit(decimal count, string range)
        {
            if (MaxItems > 0 && count > MaxItems)
                throw new FeelEvaluationException(
                    $"Range {range} has {count:N0} items, which exceeds the iteration limit of {MaxItems:N0}. " +
                    $"Raise {nameof(FeelEvaluationLimits)}.{nameof(FeelEvaluationLimits.MaxIterationItems)} to allow it.");
        }
    }
}
