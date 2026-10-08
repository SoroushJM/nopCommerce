using Nop.Services.ExportImport;
using NUnit.Framework;

namespace Nop.Tests.Nop.Services.Tests.ExportImport;

[TestFixture]
public class TransferCalendarTests
{
    [TestCase("2025-03-20T17:43:29.1234567", "1403/12/30 17:43:29.1234567")]
    [TestCase("2025-03-21T00:00:00", "1404/01/01 00:00:00.0000000")]
    [TestCase("2026-10-08T12:30:00", "1405/07/16 12:30:00.0000000")]
    public void JalaliTransfersRoundTripWithoutChangingTime(string iso, string expected)
    {
        var date = DateTime.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);
        Assert.That(TransferCalendar.Format(date, "jalali"), Is.EqualTo(expected));
        Assert.That(TransferCalendar.ParseJalali(expected), Is.EqualTo(date));
        Assert.That(TransferCalendar.ParseJalali(TransferCalendar.PersianDigits(expected)), Is.EqualTo(date));
    }

    [Test]
    public void InvalidJalaliLeapDayIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TransferCalendar.ParseJalali("1404/12/30"));
    }

    [Test]
    public void PersianAndArabicDigitsNormalizeForImports()
    {
        Assert.That(TransferCalendar.LatinDigits("۱۴۰۵/٠٧/۱۶"), Is.EqualTo("1405/07/16"));
    }
}
