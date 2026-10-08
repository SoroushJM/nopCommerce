namespace Nop.Web.Areas.Admin.Models.Common;

/// <summary>Explicit calendar control for a file export or an Excel import.</summary>
public sealed class AdminTransferCalendarModel
{
    public bool IsImport { get; init; }
}
