using System.Globalization;

namespace Nop.Services.ExportImport;

/// <summary>Calendar of a human-facing admin transfer. Database and plugin contracts remain Gregorian.</summary>
public static class TransferCalendar
{
    private static readonly AsyncLocal<string> _current = new();
    public static string Current { get => _current.Value; set => _current.Value = value; }
    public static bool IsValid(string value) => value is "jalali" or "gregorian";
    public static string PersianDigits(string value) => value is null ? null : string.Concat(value.Select(c => c is >= '0' and <= '9' ? (char)('۰' + c - '0') : c));
    public static string LatinDigits(string value) => value is null ? null : string.Concat(value.Select(c => c is >= '۰' and <= '۹' ? (char)('0' + c - '۰') : c is >= '٠' and <= '٩' ? (char)('0' + c - '٠') : c));

    public static string Format(DateTime date, string calendar, bool persianDigits = false)
    {
        var pc = new PersianCalendar();
        var text = calendar == "jalali"
            ? $"{pc.GetYear(date):0000}/{pc.GetMonth(date):00}/{pc.GetDayOfMonth(date):00} {date:HH:mm:ss.fffffff}"
            : date.ToString("yyyy/MM/dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture);
        return persianDigits ? PersianDigits(text) : text;
    }

    public static DateTime ParseJalali(string text)
    {
        var parts = LatinDigits(text).Trim().Split(['/', '-', ' ', 'T', ':', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || parts.Length > 7)
            throw new FormatException("Invalid Jalali date; expected yyyy/MM/dd HH:mm:ss.");
        var values = parts.Take(6).Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        var result = new PersianCalendar().ToDateTime(values[0], values[1], values[2], values.Length > 3 ? values[3] : 0,
            values.Length > 4 ? values[4] : 0, values.Length > 5 ? values[5] : 0, 0);
        if (parts.Length == 7)
            result = result.AddTicks(long.Parse(parts[6].PadRight(7, '0'), CultureInfo.InvariantCulture));
        return result;
    }

    public static string PdfDate(DateTime date, string culture)
    {
        if (Current is null)
            return date.ToString("D", new CultureInfo(culture));
        return Format(date, Current, culture.StartsWith("fa", StringComparison.OrdinalIgnoreCase)).Split(' ')[0];
    }

    public static object ReportDate(DateTime date) => Current is null ? date.ToString("D") : date;

    public static string ReportSummary(DateTime date, int grouping, string original)
    {
        if (Current is null) return original;
        var first = Format(date, Current).Split(' ')[0];
        return grouping switch
        {
            1 => $"{first} - {Format(date.AddDays(6), Current).Split(' ')[0]}",
            2 when Current == "jalali" => $"{first} - {Format(new DateTime(date.Year, date.Month, 1).AddMonths(1).AddDays(-1), Current).Split(' ')[0]}",
            2 => first[..7],
            _ => first
        };
    }
}
