using System.Xml.Linq;
using ClosedXML.Excel;

namespace Nop.Storefront.Tests;

[LocalAdminFixture]
[NotInParallel("storefront-http")]
public sealed class AdminTransferPluginTests
{
    [Test]
    [Arguments("jalali")]
    [Arguments("gregorian")]
    public async Task NativeAdminXmlExportsPassThroughPluginCalendarFilter(string calendar)
    {
        await using var admin = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(admin);
        using var response = await admin.PostFormAsync("Admin/Order/ExportXmlSelected", new()
        {
            ["selectedIds"] = "",
            ["transferCalendar"] = calendar,
            ["__RequestVerificationToken"] = admin.Token
        });
        response.EnsureSuccessStatusCode();
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/xml");
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());
        await Assert.That(xml.Root!.Attribute("dateCalendar")?.Value).IsEqualTo(calendar == "jalali" ? calendar : null);
    }

    [Test]
    [Arguments("jalali")]
    [Arguments("gregorian")]
    public async Task NativeAdminWorkbookExportsKeepPluginCalendarMetadata(string calendar)
    {
        await using var admin = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(admin);
        using var response = await admin.PostFormAsync("Admin/Product/ExportExcelSelected", new()
        {
            ["selectedIds"] = StorefrontClient.ProductId.ToString(),
            ["transferCalendar"] = calendar,
            ["__RequestVerificationToken"] = admin.Token
        });
        response.EnsureSuccessStatusCode();
        using var input = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var book = new XLWorkbook(input);
        var metadata = book.Worksheet("__NopCalendar");
        await Assert.That(metadata.Cell(1, 2).GetString()).IsEqualTo(calendar);
        await Assert.That(metadata.Visibility).IsEqualTo(XLWorksheetVisibility.VeryHidden);
        await Assert.That(book.Worksheets.Count(sheet => sheet.Name == "__NopCalendar")).IsEqualTo(1);
    }
}