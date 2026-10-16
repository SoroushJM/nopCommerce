using System.Text.RegularExpressions;

namespace Nop.Plugin.Misc.PersianStorefront;

public static partial class IranianMobile
{
    public static string Digits(string? input)
    {
        var value = input?.Trim() ?? "";
        for (var i = 0; i < 10; i++)
            value = value.Replace((char)('۰' + i), (char)('0' + i)).Replace((char)('٠' + i), (char)('0' + i));
        return value;
    }
    public static string? Normalize(string? input)
    {
        var phone = string.Concat(Digits(input).Where(c => !char.IsWhiteSpace(c) && c is not '-' and not '(' and not ')'));
        if (phone.StartsWith("0098", StringComparison.Ordinal)) phone = "+98" + phone[4..];
        else if (phone.StartsWith("98", StringComparison.Ordinal)) phone = "+" + phone;
        else if (phone.StartsWith("09", StringComparison.Ordinal)) phone = "+98" + phone[1..];
        return Mobile().IsMatch(phone) ? phone : null;
    }
    public static string[] LookupForms(string canonical) => [canonical, "0" + canonical[3..], canonical[1..], "00" + canonical[1..]];
    [GeneratedRegex(@"^\+989[0-9]{9}$")]
    private static partial Regex Mobile();
}
