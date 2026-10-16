using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Data;
using Nop.Plugin.Misc.PersianStorefront;
using Nop.Plugin.Sms.SmsIr;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Security;
using Storefront.UI.Shared;

namespace Nop.Storefront.Tests;

[NotInParallel]
public sealed class SmsIrTests
{
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [Test]
    public void DomesticInternationalAndPersianInputsHaveOneStoredIdentity()
    {
        foreach (var phone in new[] { "09123456789", "+989123456789", "00989123456789", "۹۸۹۱۲۳۴۵۶۷۸۹", "۰۹۱۲ ۳۴۵ ۶۷۸۹" })
            Require(IranianMobile.Normalize(phone) == "+989123456789", "Equivalent phone inputs must share one E.164 identity.");
        foreach (var invalid in new[] { "", "09123", "+449123456789", "abc", "+9891234567890" })
            Require(IranianMobile.Normalize(invalid) is null, "Invalid mobile must be rejected.");
    }

    [Test]
    public async Task VerifyTransportUsesStructuredCodeAndChecksBothHttpAndApplicationStatus()
    {
        var handler = new Handler();
        var client = new SmsIrClient(new HttpClient(handler));
        Require((await client.SendCodeAsync("test-secret", 123456, "CODE", "+989123456789", "654321")).Success, "Success status must be accepted.");
        using var payload = JsonDocument.Parse(handler.Body!);
        Require(handler.Url == "https://api.sms.ir/v1/send/verify" && handler.Key == "test-secret", "Verify transport and header must match SMS.ir.");
        Require(payload.RootElement.GetProperty("mobile").GetString() == "+989123456789" && payload.RootElement.GetProperty("parameters")[0].GetProperty("value").GetString() == "654321", "Phone/code must be sent as structured parameters.");
        handler.BodyResponse = "{\"status\":102}";
        Require(!(await client.SendCodeAsync("test-secret", 123456, "CODE", "+989123456789", "654321")).Success, "HTTP 200 with insufficient credit must fail.");
        handler.Status = HttpStatusCode.TooManyRequests;
        Require(!(await client.SendCodeAsync("test-secret", 123456, "CODE", "+989123456789", "654321")).Success, "HTTP throttling must fail without a retry.");
        Require(handler.Calls == 3, "Each send must make exactly one HTTP request.");
        handler.Status = HttpStatusCode.OK;
        foreach (var malformed in new[] { "[]", "null", "{\"status\":\"1\"}", "{\"status\":null}", "{broken" })
        {
            handler.BodyResponse = malformed;
            Require(!(await client.SendCodeAsync("test-secret", 123456, "CODE", "+989123456789", "654321")).Success, "Malformed provider responses must fail without throwing.");
        }
    }

    [Test]
    public async Task ChallengesSurviveServiceRecreationAreGuestBoundAndCannotBeReplayed()
    {
        var rows = new List<GenericAttribute>();
        var repo = Repository(rows);
        var sender = new Sender();
        var guest = Guid.NewGuid();
        var first = Service(repo, sender);
        Require((await first.SendAsync(guest, "09123456789")).Success, "Initial send must succeed.");
        var recreated = Service(repo, sender);
        Require(await recreated.VerifyAsync(Guid.NewGuid(), "09123456789", sender.Code) is null, "Another guest cannot consume this code.");
        Require(!(await recreated.SendAsync(Guid.NewGuid(), "+989123456789")).Success, "New guest must not bypass the phone cooldown.");
        Require(await recreated.VerifyAsync(guest, "+989123456789", sender.Code) == "+989123456789", "Recreated service must read durable challenge.");
        Require(await recreated.VerifyAsync(guest, "09123456789", sender.Code) is null, "A successful code must be consumed.");
        Require(!rows.Any(row => row.Value.Contains(sender.Code, StringComparison.Ordinal)), "Verification codes must not be stored as plaintext.");
    }

    [Test]
    public async Task AttemptExhaustionExpiryAndFailedDeliveryCannotAuthenticate()
    {
        var rows = new List<GenericAttribute>();
        var sender = new Sender();
        var service = Service(Repository(rows), sender);
        var guest = Guid.NewGuid();
        await service.SendAsync(guest, "09123456789");
        for (var attempt = 0; attempt < 5; attempt++) Require(await service.VerifyAsync(guest, "09123456789", "000000") is null, "Wrong codes must fail.");
        Require(await service.VerifyAsync(guest, "09123456789", sender.Code) is null, "Correct code must remain locked after five wrong attempts.");
        var expiringGuest = Guid.NewGuid();
        await service.SendAsync(expiringGuest, "09361234567");
        var row = rows.Single(a => a.Key == "guest-" + expiringGuest.ToString("N"));
        var challenge = JsonSerializer.Deserialize<StorefrontOtpService.Challenge>(row.Value)!;
        challenge.Attempts = 0; challenge.Sent = DateTime.UtcNow.AddMinutes(-3);
        row.Value = JsonSerializer.Serialize(challenge);
        Require(await service.VerifyAsync(expiringGuest, "09361234567", sender.Code) is null, "Expired code must fail.");
        sender.Success = false;
        var other = Guid.NewGuid();
        Require(!(await service.SendAsync(other, "09351234567")).Success, "Failed delivery must be reported.");
        Require(await service.VerifyAsync(other, "09351234567", sender.Code) is null, "Failed delivery must not activate a challenge.");
    }

    [Test]
    public async Task ProductionRejectsSandboxAndNeverReturnsTestCodes()
    {
        var configuration = new SmsIrSettings { EncryptedApiKey = "encrypted" };
        Require(configuration.ConfigurationWarning(false) is not null && configuration.ConfigurationWarning(true) is null, "Sandbox must warn/block outside Development.");
        var handler = new Handler();
        var encryption = Stub<IEncryptionService>.Create((method, args) => method.Name == "DecryptText" ? "test-secret" : null);
        var realSender = new SmsIrSender(configuration, new SmsIrClient(new HttpClient(handler)), encryption, Environment("Production"), new Journal());
        Require(!(await realSender.SendAsync("+989123456789", "123456")).Success && handler.Calls == 0, "Production Sandbox must not contact SMS.ir.");
        var result = await Service(Repository(new List<GenericAttribute>()), new Sender(), "Production").SendAsync(Guid.NewGuid(), "09123456789");
        Require(result.TestCode is null, "The challenge layer must strip test codes in Production, even from a faulty sender.");
        var rows = new List<GenericAttribute>();
        var sender = new Sender();
        var guest = Guid.NewGuid();
        await Service(Repository(rows), sender).SendAsync(guest, "09123456789");
        Require(await Service(Repository(rows), sender, "Production").VerifyAsync(guest, "09123456789", sender.Code) is null, "Development challenges must not authenticate after an environment switch.");
    }

    [Test]
    public async Task DuplicateAndBlockedAccountsAreRejectedRatherThanSelectedOrRecreated()
    {
        var rows = new List<Customer> { new() { Id=1, Phone="09123456789", Active=true }, new() { Id=2, Phone="+989123456789", Active=true } };
        var customers = Stub<ICustomerService>.Create((method, args) => throw new InvalidOperationException("Ambiguous/blocked lookup must not create or sign in a customer."));
        var service = new StorefrontPhoneAccounts(Repository(rows), customers);
        Require(await service.ResolveAsync("+989123456789", 1) is null && rows.Count == 2, "Duplicate account must fail without mutation.");
        rows.RemoveAt(1); rows[0].Active=false;
        Require(await service.ResolveAsync("+989123456789", 1) is null && rows.Count == 1, "Inactive account must not be duplicated.");
        rows[0].Active=true; rows[0].Deleted=true;
        Require(await service.ResolveAsync("+989123456789", 1) is null, "Deleted account must not be reactivated.");
    }

    [Test]
    public async Task InternalIdentityCannotCollideWithAnExistingNativeAccount()
    {
        var rows = new List<Customer> { new() { Id=1, Email="mobile-09123456789@accounts.invalid", Username="mobile:09123456789", Phone="+989350000000", Active=true } };
        var service = new StorefrontPhoneAccounts(Repository(rows), Customers(rows));
        var created = await service.ResolveAsync("+989123456789", 1);
        Require(created is not null && created.Email != rows[0].Email && created.Username != rows[0].Username && rows.Count == 2, "A phone account must receive an independent random authentication identity.");
        Require(rows.Single(c => c.Email == created!.Email).Id == created!.Id, "Native email lookup must resolve the intended account.");
        created.RequireReLogin=true;
        created.LastLoginDateUtc=null;
        Require(await service.ResolveAsync("+989123456789", 1) == created && !created.RequireReLogin && created.LastLoginDateUtc is not null, "Verified OTP must clear re-login and record successful authentication.");
        rows[0].Email=created.Email;
        Require(await service.ResolveAsync("+989123456789", 1) is null, "An existing duplicate cookie identity must fail closed.");
    }

    [Test]
    public async Task NonRegisteredPhoneAccountsCannotReceiveAuthenticationCookies()
    {
        var rows = new List<Customer> { new() { Id=1, Phone="+989123456789", Active=true } };
        var customers = Stub<ICustomerService>.Create((method,args) => method.Name switch
        {
            "IsAdminAsync" or "IsRegisteredAsync" => Task.FromResult(false),
            _ => throw new InvalidOperationException("An unregistered account must not be mutated.")
        });
        Require(await new StorefrontPhoneAccounts(Repository(rows), customers).ResolveAsync("+989123456789", 1) is null, "Guest accounts must not be signed in as registered customers.");
    }

    [Test]
    public async Task ProviderMessageIdsAndDeliveryBlacklistRemainDistinctFromAcceptedSends()
    {
        var handler = new Handler { BodyResponse="{\"status\":1,\"data\":{\"messageId\":9123}}" };
        var client = new SmsIrClient(new HttpClient(handler));
        var sent = await client.SendCodeAsync("test-secret", 123456, "CODE", "+989123456789", "654321");
        Require(sent.Success && sent.MessageId==9123 && sent.ProviderStatus==1, "Accepted send must retain the provider message ID for a separate delivery lookup.");
        handler.BodyResponse="{\"status\":1,\"data\":{\"messageId\":9123,\"deliveryState\":7,\"messageText\":\"NEVER STORE THIS OTP\"}}";
        var delivery = await client.GetDeliveryAsync("test-secret", 9123);
        Require(delivery.Success && delivery.Status==7 && handler.Url=="https://api.sms.ir/v1/send/9123", "Blacklist must be read from a separate delivery report.");
        Require(!delivery.ToString().Contains("NEVER STORE"), "Delivery parsing must discard remote message text.");
        handler.BodyResponse="{\"status\":1,\"data\":{\"messageId\":9999,\"deliveryState\":7}}";
        Require(!(await client.GetDeliveryAsync("test-secret", 9123)).Success, "A report for a different message must not update the requested message.");
        handler.BodyResponse="{\"status\":1,\"data\":{\"messageId\":9123,\"deliveryState\":null}}";
        Require((await client.GetDeliveryAsync("test-secret", 9123)).Status is null, "Pending delivery must remain unknown.");
        handler.Status=HttpStatusCode.BadRequest;
        handler.BodyResponse="{\"status\":115}";
        var rejected = await client.SendCodeAsync("test-secret",123456,"CODE","+989123456789","654321");
        Require(!rejected.Success && rejected.IsBlacklisted && rejected.ProviderStatus==115, "HTTP 400 application blacklist rejection must be retained.");
        handler.Status=HttpStatusCode.OK;
        handler.BodyResponse="{\"status\":1,\"data\":{\"messageIds\":[0]}}";
        var zeroId = await client.SendTextAsync("test-secret",30000000,"+989123456789","text");
        Require(!zeroId.Success && zeroId.IsBlacklisted && zeroId.MessageId is null, "Bulk zero ID is a blacklist rejection, not an accepted send.");
        handler.BodyResponse="{\"status\":1,\"data\":{\"messageIds\":[null]}}";
        Require(!(await client.SendTextAsync("test-secret",30000000,"+989123456789","text")).Success, "Bulk null ID must not count as accepted delivery.");
    }

    [Test]
    public async Task AuditPersistsSanitizedMetadataAndScopedTemporaryBlocks()
    {
        var rows = new List<SmsIrMessage>();
        var work = Stub<Nop.Core.IWorkContext>.Create((method,args) => method.Name=="GetCurrentCustomerAsync" ? Task.FromResult(new Customer { Id=17 }) : null);
        var store = Stub<Nop.Core.IStoreContext>.Create((method,args) => method.Name=="GetCurrentStoreAsync" ? Task.FromResult(new Nop.Core.Domain.Stores.Store { Id=1 }) : null);
        var journal = new SmsIrJournal(Repository(rows), new SmsIrSettings(), work, store);
        var pending = await journal.BeginAsync("+989123456789", "OTP", 123456);
        Require(rows.Count==1 && pending.Outcome=="Pending" && pending.CustomerId==17, "Audit intent must exist before the provider call and retain its requesting customer.");
        await journal.CompleteAsync(pending, new(true,"accepted",1,9123));
        Require(pending.Outcome=="Accepted" && pending.MessageId==9123 && pending.Sandbox && pending.DeliveryStatus is null, "Accepted Sandbox requests must not claim actual delivery.");
        var service = new StorefrontOtpService(Environment("Development"), new Sender(), Repository(new List<GenericAttribute>()), Stub<IWebHelper>.Create((method,args)=>method.Name=="GetCurrentIpAddress" ? "127.0.0.1" : null), journal);
        await service.SendAsync(Guid.NewGuid(), "09123456789");
        Require(!(await service.SendAsync(Guid.NewGuid(), "09123456789")).Success, "A second send must be blocked by phone cooldown.");
        Require(rows.Count==2 && rows[1].Outcome=="Blocked" && rows[1].Reason=="PhoneCooldown" && rows[1].BlockedUntilUtc>DateTime.UtcNow, "The report must retain the exact blocking scope and expiry without another provider send.");
        await service.SendAsync(Guid.NewGuid(), "09123456789");
        Require(rows.Count==2 && rows[1].RequestCount==2, "Repeated blocked requests must be counted without growing one row per rejection.");
        await journal.CompleteAsync(pending, new(false,"ambiguous transport",IsAmbiguous:true));
        Require(pending.Outcome=="Unknown", "An ambiguous transport outcome must not be counted as a definite failure.");
        Require(!typeof(SmsIrMessage).GetProperties().Any(p=>p.Name is "Code" or "ApiKey" or "MessageText" or "Body"), "Audit schema must not persist codes, keys or SMS bodies.");
    }

    private static ICustomerService Customers(List<Customer> rows) => Stub<ICustomerService>.Create((method,args) =>
    {
        switch (method.Name)
        {
            case "IsAdminAsync": return Task.FromResult(false);
            case "IsRegisteredAsync": return Task.FromResult(true);
            case "InsertCustomerAsync": var customer=(Customer)args[0]!; customer.Id=rows.Max(c=>c.Id)+1; rows.Add(customer); return Task.CompletedTask;
            case "GetCustomerRoleBySystemNameAsync": return Task.FromResult(new CustomerRole { Id=3 });
            case "AddCustomerRoleMappingAsync": case "UpdateCustomerAsync": return Task.CompletedTask;
            default: throw new NotSupportedException(method.Name);
        }
    });

    private static StorefrontOtpService Service(IRepository<GenericAttribute> repo, Sender sender, string environment="Development") =>
        new(Environment(environment), sender, repo, Stub<IWebHelper>.Create((method,args) => method.Name=="GetCurrentIpAddress" ? "127.0.0.1" : null), new NullStorefrontOtpAudit());
    private static IWebHostEnvironment Environment(string name) => Stub<IWebHostEnvironment>.Create((method,args) => method.Name=="get_EnvironmentName" ? name : null);

    private static IRepository<T> Repository<T>(List<T> rows) where T : Nop.Core.BaseEntity => Stub<IRepository<T>>.Create((method,args) =>
    {
        switch (method.Name)
        {
            case "GetAllAsync":
                var query = ((Func<IQueryable<T>,IQueryable<T>>?)args[0])?.Invoke(rows.AsQueryable()) ?? rows.AsQueryable();
                return Task.FromResult<IList<T>>(query.ToList());
            case "InsertAsync": rows.Add((T)args[0]!); return Task.CompletedTask;
            case "UpdateAsync": return Task.CompletedTask;
            case "DeleteAsync": return Task.FromResult(rows.RemoveAll(((Expression<Func<T,bool>>)args[0]!).Compile().Invoke));
            default: throw new NotSupportedException(method.Name);
        }
    });

    private sealed class Sender : IStorefrontOtpSender
    {
        public string Code=""; public bool Success=true;
        public Task<OtpResult> SendAsync(string phone,string code) { Code=code; return Task.FromResult(new OtpResult(Success,"test",Success?code:null)); }
    }
    private sealed class Handler : HttpMessageHandler
    {
        public string? Url,Key,Body; public string BodyResponse="{\"status\":1,\"data\":{\"messageId\":9001}}"; public HttpStatusCode Status=HttpStatusCode.OK; public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Calls++; Url=request.RequestUri!.ToString(); Key=request.Headers.GetValues("X-API-KEY").Single(); Body=request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new(Status) { Content=new StringContent(BodyResponse) };
        }
    }
    private sealed class Journal : ISmsIrJournal
    {
        public Task<SmsIrMessage> BeginAsync(string phone,string kind,int templateId) => Task.FromResult(new SmsIrMessage { Phone=phone,Kind=kind,TemplateId=templateId });
        public Task CompleteAsync(SmsIrMessage message,SmsIrSendResult result) => Task.CompletedTask;
    }
    public class Stub<T> : DispatchProxy where T : class
    {
        private Func<MethodInfo,object?[],object?> _invoke=null!;
        public static T Create(Func<MethodInfo,object?[],object?> invoke)
        {
            var proxy=DispatchProxy.Create<T,Stub<T>>(); ((Stub<T>)(object)proxy)._invoke=invoke; return proxy;
        }
        protected override object? Invoke(MethodInfo? method,object?[]? args) => _invoke(method!,args??[]);
    }
}
