using System.Globalization;
using System.Text;
using System.Xml.Linq;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Services.ExportImport;

namespace Nop.Web.Areas.Admin.Filters;

/// <summary>Adapts built-in admin files at the presentation boundary without changing service/plugin contracts.</summary>
public sealed class AdminTransferCalendarAttribute : ActionFilterAttribute
{
    private const string MetadataSheet = "__NopCalendar";
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // External plugin transfers retain their own formats and validation contracts.
        if (context.Controller.GetType().Assembly != typeof(Nop.Web.Areas.Admin.Controllers.BaseAdminController).Assembly)
        {
            await next();
            return;
        }
        context.ActionDescriptor.RouteValues.TryGetValue("action", out var action);
        action ??= "";
        if (!action.Contains("Export", StringComparison.OrdinalIgnoreCase) && !action.Contains("ImportFromXlsx", StringComparison.OrdinalIgnoreCase) && !action.Contains("Pdf", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }
        var request = context.HttpContext.Request;
        var form = request.HasFormContentType ? await request.ReadFormAsync() : null;
        var calendar = (form?["transferCalendar"].FirstOrDefault() ?? request.Query["transferCalendar"].FirstOrDefault());
        var importing = action.Contains("ImportFromXlsx", StringComparison.OrdinalIgnoreCase);
        if (!importing && string.IsNullOrEmpty(calendar))
            calendar = request.Cookies[Infrastructure.AdminTransferCalendarPreference.CookieName];
        if (!TransferCalendar.IsValid(calendar))
        {
            if (importing)
            {
                context.Result = new BadRequestObjectResult("نوع تقویم فایل را انتخاب کنید / Select the file calendar (Jalali or Gregorian).");
                return;
            }
            calendar = "gregorian"; // Existing integrations and direct links retain their original contract.
        }
        try
        {
            if (importing)
                foreach (var key in context.ActionArguments.Keys.ToList())
                    if (context.ActionArguments[key] is IFormFile file && file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        using var input = file.OpenReadStream();
                        var bytes = ConvertWorkbook(input, calendar, true);
                        var stream = new MemoryStream(bytes);
                        context.HttpContext.Response.RegisterForDispose(stream);
                        context.ActionArguments[key] = new FormFile(stream, 0, bytes.Length, file.Name, file.FileName)
                        { Headers = file.Headers, ContentType = file.ContentType };
                    }
            var previous = TransferCalendar.Current;
            TransferCalendar.Current = calendar;
            ActionExecutedContext executed;
            try { executed = await next(); }
            finally { TransferCalendar.Current = previous; }
            if (!importing && executed.Result is FileContentResult result)
            {
                if (result.FileDownloadName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    using var input = new MemoryStream(result.FileContents);
                    var isPersianReport = context.Controller.GetType().Name == "ReportController" && CultureInfo.CurrentUICulture.Name.StartsWith("fa", StringComparison.OrdinalIgnoreCase);
                    result.FileContents = ConvertWorkbook(input, calendar, false, isPersianReport);
                }
                else if (result.FileDownloadName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    result.FileContents = ConvertXml(result.FileContents, calendar);
            }
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException or InvalidDataException)
        {
            context.Result = new BadRequestObjectResult($"تقویم یا تاریخ فایل معتبر نیست / Invalid file calendar or date: {ex.Message}");
        }
    }

    public static byte[] ConvertWorkbook(Stream input, string calendar, bool importing, bool persianReport = false)
    {
        using var book = new XLWorkbook(input);
        var dateCells = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (importing && book.TryGetWorksheet(MetadataSheet, out var metadata))
        {
            if (metadata.Cell(1, 2).GetString() != calendar)
                throw new InvalidDataException("Selected calendar does not match the exported file metadata.");
            foreach (var row in metadata.RowsUsed().Where(r => r.RowNumber() > 2))
                dateCells.Add(row.Cell(1).GetString());
            book.Worksheets.Delete(MetadataSheet);
        }
        foreach (var sheet in book.Worksheets.Where(s => s.Name != MetadataSheet))
            foreach (var cell in sheet.CellsUsed().ToList())
            {
                if (!importing && calendar == "jalali" && cell.DataType == XLDataType.DateTime)
                {
                    dateCells.Add(sheet.Name + "!" + cell.Address.ToString());
                    cell.Value = TransferCalendar.Format(cell.GetDateTime(), calendar);
                    cell.Style.NumberFormat.Format = "@";
                }
                else if (importing && calendar == "jalali" && cell.DataType == XLDataType.Text && (dateCells.Contains(sheet.Name + "!" + cell.Address.ToString()) || IsDateColumn(sheet, cell)))
                {
                    if (!string.IsNullOrWhiteSpace(cell.GetString()))
                        cell.Value = TransferCalendar.ParseJalali(cell.GetString());
                }
            }
        if (!importing)
        {
            if (persianReport)
            {
                // Keep numerical data in its original sheet for analysis; the first sheet is the Persian presentation.
                var dataSheet = book.Worksheets.First();
                var display = dataSheet.CopyTo("گزارش فارسی");
                display.Position = 1;
                display.RightToLeft = true;
                foreach (var cell in display.CellsUsed().ToList())
                {
                    if (cell.Address.RowNumber == 1)
                        cell.Value = ReportHeader(cell.GetString());
                    else if (cell.DataType == XLDataType.Number || cell.DataType == XLDataType.DateTime)
                        cell.Value = TransferCalendar.PersianDigits(cell.DataType == XLDataType.DateTime ? TransferCalendar.Format(cell.GetDateTime(), calendar) : cell.GetString());
                    else if (cell.DataType == XLDataType.Text && (IsDateColumn(dataSheet, cell) || dataSheet.Cell(1, cell.Address.ColumnNumber).GetString() is "Summary" or "OrderTotal" or "TotalAmountStr" or "SumOrdersStr" or "ProfitStr"))
                        cell.Value = TransferCalendar.PersianDigits(cell.GetString());
                }
                display.Columns().AdjustToContents();
            }
            if (book.TryGetWorksheet(MetadataSheet, out _)) book.Worksheets.Delete(MetadataSheet);
            var marker = book.AddWorksheet(MetadataSheet);
            marker.Cell(1, 1).Value = "Calendar";
            marker.Cell(1, 2).Value = calendar;
            marker.Cell(2, 1).Value = "DateFormat";
            marker.Cell(2, 2).Value = calendar == "jalali" ? "yyyy/MM/dd HH:mm:ss.fffffff (Jalali, original timezone)" : "Excel Gregorian date (original timezone)";
            var rowIndex = 3;
            foreach (var address in dateCells) marker.Cell(rowIndex++, 1).Value = address;
            marker.Visibility = XLWorksheetVisibility.VeryHidden;
        }
        using var output = new MemoryStream();
        book.SaveAs(output);
        return output.ToArray();
    }

    private static bool IsDateColumn(IXLWorksheet sheet, IXLCell cell)
    {
        if (cell.Address.RowNumber <= 1) return false;
        for (var row = cell.Address.RowNumber - 1; row >= 1; row--)
        {
            var header = sheet.Cell(row, cell.Address.ColumnNumber).GetString();
            if (string.IsNullOrEmpty(header) || char.IsDigit(TransferCalendar.LatinDigits(header)[0])) continue;
            return header.Contains("Date", StringComparison.OrdinalIgnoreCase) || header.EndsWith("OnUtc", StringComparison.OrdinalIgnoreCase) || header.Equals("Created on", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static byte[] ConvertXml(byte[] bytes, string calendar)
    {
        var document = XDocument.Parse(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
        if (calendar != "jalali")
            return Encoding.UTF8.GetBytes(document.ToString());
        document.Root?.SetAttributeValue("dateCalendar", "jalali");
        document.Root?.SetAttributeValue("dateFormat", "yyyy/MM/dd HH:mm:ss.fffffff; original timezone");
        foreach (var element in document.Descendants().Where(e => !e.HasElements && (e.Name.LocalName.Contains("Date") || e.Name.LocalName.EndsWith("OnUtc"))))
            if (DateTime.TryParse(element.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
                element.Value = TransferCalendar.Format(date, "jalali");
        return Encoding.UTF8.GetBytes(document.ToString());
    }

    private static string ReportHeader(string header) => header switch
    {
        "Summary" => "خلاصه", "SummaryDate" => "تاریخ", "NumberOfOrders" => "تعداد سفارش‌ها",
        "Profit" => "سود", "ProfitStr" => "سود (نمایش)", "Shipping" => "هزینه ارسال", "Tax" => "مالیات",
        "OrderTotal" => "مبلغ سفارش", "SummaryType" => "نوع خلاصه", "Id" => "شناسه", "Name" => "نام",
        "StockQuantity" => "موجودی", "ManageInventoryMethod" => "روش مدیریت موجودی", "Published" => "منتشرشده",
        "ProductId" => "شناسه محصول", "ProductName" => "نام محصول", "TotalQuantity" => "تعداد کل",
        "TotalAmount" => "مبلغ کل", "TotalAmountStr" => "مبلغ کل (نمایش)", "CountryName" => "کشور",
        "TotalOrders" => "تعداد سفارش‌ها", "SumOrders" => "مجموع سفارش‌ها", "SumOrdersStr" => "مجموع سفارش‌ها (نمایش)",
        "Period" => "بازه زمانی", "Customers" => "تعداد مشتریان", "OrderCount" => "تعداد سفارش‌ها", _ => header
    };
}
