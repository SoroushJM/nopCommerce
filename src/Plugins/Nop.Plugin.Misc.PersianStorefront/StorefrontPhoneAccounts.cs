using Nop.Core.Domain.Customers;
using Nop.Data;
using Nop.Services.Customers;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed class StorefrontPhoneAccounts(IRepository<Customer> repository, ICustomerService customers)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public async Task<Customer?> ResolveAsync(string canonical, int storeId)
    {
        await Gate.WaitAsync();
        try
        {
            var forms = IranianMobile.LookupForms(canonical);
            var matches = await repository.GetAllAsync(query => query.Where(c => forms.Contains(c.Phone)), includeDeleted: true);
            if (matches.Count > 1) return null;
            var customer = matches.SingleOrDefault();
            if (customer is not null && (!customer.Active || customer.Deleted || customer.IsSystemAccount ||
                await customers.IsAdminAsync(customer) || !await customers.IsRegisteredAsync(customer))) return null;
            if (customer is null)
            {
                var identity = Guid.NewGuid();
                customer = new Customer
                {
                    CustomerGuid = identity, Email = "mobile-" + identity.ToString("N") + "@accounts.invalid", Phone = canonical,
                    PhoneSmsVerified = true, Username = "mobile:" + identity.ToString("N"), Active = true,
                    CreatedOnUtc = DateTime.UtcNow, LastActivityDateUtc = DateTime.UtcNow, LastLoginDateUtc = DateTime.UtcNow, RegisteredInStoreId = storeId
                };
                if (await HasIdentityCollisionAsync(customer)) return null;
                await customers.InsertCustomerAsync(customer);
                var role = await customers.GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.RegisteredRoleName);
                await customers.AddCustomerRoleMappingAsync(new() { CustomerId = customer.Id, CustomerRoleId = role.Id });
            }
            else
            {
                // Native cookies resolve email/username on the next request. Never issue
                // a cookie whose identity could resolve to a different customer.
                if (string.IsNullOrWhiteSpace(customer.Email)) customer.Email = "mobile-" + customer.CustomerGuid.ToString("N") + "@accounts.invalid";
                if (string.IsNullOrWhiteSpace(customer.Username)) customer.Username = "mobile:" + customer.CustomerGuid.ToString("N");
                if (await HasIdentityCollisionAsync(customer)) return null;
                customer.Phone = canonical;
                customer.PhoneSmsVerified = true;
                customer.RequireReLogin = false;
                customer.LastLoginDateUtc = DateTime.UtcNow;
                await customers.UpdateCustomerAsync(customer);
            }
            return customer;
        }
        finally { Gate.Release(); }
    }

    private async Task<bool> HasIdentityCollisionAsync(Customer customer)
    {
        var email = customer.Email.ToUpperInvariant();
        var username = customer.Username.ToUpperInvariant();
        var collisions = await repository.GetAllAsync(query => query.Where(c => c.Id != customer.Id &&
            ((c.Email != null && c.Email.ToUpper() == email) || (c.Username != null && c.Username.ToUpper() == username))), includeDeleted: true);
        return collisions.Count != 0;
    }
}
