namespace Storefront.UI;

public record QuantityRequest(int LineId, int Quantity);
public record PhoneRequest(string Phone);
public record VerifyRequest(string Phone, string Code);
public record OtpResult(bool Success, string Message, string? TestCode = null)
{
    public OtpResult() : this(false, "") { }
}