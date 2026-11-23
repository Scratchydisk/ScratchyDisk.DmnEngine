using System;
using System.Globalization;

namespace ScratchyDisk.DmnEngine.Feel.Types
{
    /// <summary>
    /// A FEEL date-and-time value that carries an IANA (or Windows) timezone
    /// identifier. <see cref="DateTimeOffset"/> alone loses that identifier, which
    /// FEEL needs for string(), is() and duration arithmetic.
    /// </summary>
    public readonly record struct FeelDateTime
    {
        /// <summary>The instant, with the zone's offset applied.</summary>
        public DateTimeOffset Value { get; }

        /// <summary>The timezone identifier, for example "Europe/Paris".</summary>
        public string ZoneId { get; }

        /// <summary>Original fractional-second digits (1-9), preserved for string().</summary>
        public string Fraction { get; }

        public FeelDateTime(DateTimeOffset value, string zoneId, string fraction = null)
        {
            Value = value;
            ZoneId = zoneId;
            Fraction = fraction;
        }

        public bool HasZone => !string.IsNullOrWhiteSpace(ZoneId);

        public FeelDateTime Add(TimeSpan duration) => new FeelDateTime(Value + duration, ZoneId);

        public FeelDateTime Add(FeelYmDuration duration) => new FeelDateTime(duration.AddTo(Value), ZoneId);

        /// <summary>
        /// FEEL textual form: local wall-clock time followed by @zone.
        /// </summary>
        public override string ToString()
        {
            var local = Value.DateTime;
            var text = local.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            // DateTimeOffset keeps 7 fractional digits; FEEL prints up to 9.
            var fraction = string.IsNullOrEmpty(Fraction)
                ? local.ToString("fffffff", CultureInfo.InvariantCulture).TrimEnd('0')
                : Fraction.TrimEnd('0');
            if (fraction.Length > 0) text += "." + fraction;

            return HasZone ? $"{text}@{ZoneId}" : text;
        }
    }
}
