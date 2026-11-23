using System;
using System.Globalization;

namespace ScratchyDisk.DmnEngine.Feel.Types
{
    /// <summary>
    /// FEEL date-and-time for years outside <see cref="DateOnly"/>'s range.
    /// Keeps the original 1-9 digit fractional seconds so string() is lossless.
    /// </summary>
    public readonly record struct FeelExpandedDateTime
    {
        public FeelExpandedDate Date { get; }
        public TimeOnly Time { get; }
        public string Fraction { get; }
        public TimeSpan Offset { get; }
        public string ZoneId { get; }
        public bool HasOffset { get; }

        public FeelExpandedDateTime(
            FeelExpandedDate date, TimeOnly time, string fraction,
            TimeSpan offset, string zoneId, bool hasOffset = false)
        {
            Date = date;
            Time = time;
            Fraction = fraction;
            Offset = offset;
            ZoneId = zoneId;
            HasOffset = hasOffset;
        }

        public bool HasZone => !string.IsNullOrWhiteSpace(ZoneId);

        public FeelExpandedDateTime Add(TimeSpan duration)
        {
            var ticks = Date.ToDayNumber() * TimeSpan.TicksPerDay + Time.Ticks + duration.Ticks;
            var days = (long)Math.Floor((decimal)ticks / TimeSpan.TicksPerDay);
            var remainder = ticks - days * TimeSpan.TicksPerDay;
            return new FeelExpandedDateTime(
                FeelExpandedDate.FromDayNumber(days),
                TimeOnly.FromTimeSpan(TimeSpan.FromTicks(remainder)),
                Fraction, Offset, ZoneId, HasOffset);
        }

        public FeelExpandedDateTime Add(FeelYmDuration duration) =>
            new(Date.AddMonths(duration.TotalMonths), Time, Fraction, Offset, ZoneId, HasOffset);

        public override string ToString()
        {
            var text = $"{Date}T{Time:HH\\:mm\\:ss}";
            var fraction = string.IsNullOrEmpty(Fraction)
                ? Time.ToString("fffffff").TrimEnd('0')
                : Fraction.TrimEnd('0');
            if (fraction.Length > 0) text += "." + fraction;

            if (HasZone) return $"{text}@{ZoneId}";
            if (!HasOffset) return text;
            if (Offset == TimeSpan.Zero) return text + "Z";
            var sign = Offset < TimeSpan.Zero ? "-" : "+";
            var abs = Offset < TimeSpan.Zero ? -Offset : Offset;
            return $"{text}{sign}{abs:hh\\:mm}";
        }
    }
}
