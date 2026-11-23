using System;
using System.Globalization;

namespace ScratchyDisk.DmnEngine.Feel.Types
{
    /// <summary>
    /// Proleptic Gregorian date with no year range restriction (FEEL allows
    /// expanded years such as -2021 or 999999999). Used only when
    /// <see cref="DateOnly"/> cannot represent the value.
    /// </summary>
    public readonly record struct FeelExpandedDate : IComparable<FeelExpandedDate>
    {
        public int Year { get; }
        public int Month { get; }
        public int Day { get; }

        public FeelExpandedDate(int year, int month, int day)
        {
            if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
            if (day < 1 || day > DaysInMonth(year, month)) throw new ArgumentOutOfRangeException(nameof(day));
            Year = year;
            Month = month;
            Day = day;
        }

        public static bool IsLeapYear(int year) =>
            year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);

        public static int DaysInMonth(int year, int month) => month switch
        {
            1 or 3 or 5 or 7 or 8 or 10 or 12 => 31,
            4 or 6 or 9 or 11 => 30,
            2 => IsLeapYear(year) ? 29 : 28,
            _ => throw new ArgumentOutOfRangeException(nameof(month))
        };

        /// <summary>Days since 1970-01-01 (Howard Hinnant's civil algorithm).</summary>
        public long ToDayNumber()
        {
            var y = Year - (Month <= 2 ? 1 : 0);
            var era = (y >= 0 ? y : y - 399) / 400;
            var yoe = y - era * 400;
            var doy = (153 * (Month + (Month > 2 ? -3 : 9)) + 2) / 5 + Day - 1;
            var doe = yoe * 365L + yoe / 4 - yoe / 100 + doy;
            return era * 146097L + doe - 719468;
        }

        public static FeelExpandedDate FromDayNumber(long z)
        {
            z += 719468;
            var era = (z >= 0 ? z : z - 146096) / 146097;
            var doe = z - era * 146097;
            var yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
            var y = (int)(yoe + era * 400);
            var doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
            var mp = (5 * doy + 2) / 153;
            var d = (int)(doy - (153 * mp + 2) / 5 + 1);
            var m = (int)(mp + (mp < 10 ? 3 : -9));
            y += m <= 2 ? 1 : 0;
            return new FeelExpandedDate(y, m, d);
        }

        public FeelExpandedDate AddDays(int days) =>
            FromDayNumber(ToDayNumber() + days);

        public FeelExpandedDate AddMonths(int months)
        {
            var total = (long)Year * 12 + (Month - 1) + months;
            var year = (int)Math.Floor(total / 12m);
            var month = (int)(total - (long)year * 12) + 1;
            var day = Math.Min(Day, DaysInMonth(year, month));
            return new FeelExpandedDate(year, month, day);
        }

        public int CompareTo(FeelExpandedDate other) => ToDayNumber().CompareTo(other.ToDayNumber());

        public DateOnly ToDateOnly() => new(Year, Month, Day);

        public override string ToString()
        {
            var year = Year is >= 1 and <= 9999
                ? Year.ToString("D4", CultureInfo.InvariantCulture)
                : Year.ToString(CultureInfo.InvariantCulture);
            return $"{year}-{Month:D2}-{Day:D2}";
        }
    }
}
