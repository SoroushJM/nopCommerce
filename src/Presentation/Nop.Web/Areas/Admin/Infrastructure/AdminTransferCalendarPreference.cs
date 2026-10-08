using System.Globalization;
using Nop.Services.ExportImport;

namespace Nop.Web.Areas.Admin.Infrastructure;

public static class AdminTransferCalendarPreference
{
    public const string CookieName = ".Nop.AdminTransferCalendar";
    public const string FormId = "admin-transfer-calendar-preference-form";

    public static string GetSelectedCalendar(HttpRequest request)
    {
        var saved = request.Cookies[CookieName];
        return TransferCalendar.IsValid(saved) ? saved :
            CultureInfo.CurrentUICulture.Name.StartsWith("fa", StringComparison.OrdinalIgnoreCase) ? "jalali" : "gregorian";
    }
}
