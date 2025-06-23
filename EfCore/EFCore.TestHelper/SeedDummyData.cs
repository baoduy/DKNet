using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bogus;
using EfCore.TestDataLayer;

namespace EFCore.TestHelper;

public static class SeedDummyData
{
    #region Methods

    public static async Task SeedData(this MyDbContext @this, int number = 100, bool force = false)
    {
        if (!force && @this.Set<User>().Count() >= number) return;

        await @this.Set<User>().AddRangeAsync(GenerateUsers(number)).ConfigureAwait(false);
        await @this.SaveChangesAsync().ConfigureAwait(false);
    }

    private static Account GenerateAccount()
    {
        var item = new Faker<Account>()
            .RuleFor(u => u.UserName, f => f.Person.UserName)
            .RuleFor(u => u.Password, f => f.Person.Random.String(6, 15))
            .RuleFor(u => u.CreatedBy, f => f.Name.FindName())
            .RuleFor(u => u.UpdatedBy, f => f.Name.FirstName())
            .RuleFor(u => u.CreatedOn, f => f.Date.SoonOffset())
            .RuleFor(u => u.UpdatedOn, f => f.Date.SoonOffset())
            .Generate(1).First();

        item.SetCreatedBy("Unit Test");

        return item;
    }

    private static IEnumerable<Address> GenerateAddresses()
    {
        var list = new Faker<Address>()
            .RuleFor(u => u.Street, f => f.Address.StreetName())
            .Generate(3);

        return list;
    }

    private static IEnumerable<User> GenerateUsers(int number)
    {
        var list = new Faker<User>()
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.Account, f => GenerateAccount())
            .RuleFor(u => u.Addresses, f => GenerateAddresses())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.CreatedBy, f => f.Name.FirstName())
            .RuleFor(u => u.UpdatedBy, f => f.Name.FirstName())
            .RuleFor(u => u.CreatedOn, f => f.Date.SoonOffset())
            .RuleFor(u => u.UpdatedOn, f => f.Date.SoonOffset())
            .Generate(number);

        foreach (var item in list) 
            item.SetCreatedBy("Unit Test");

        return list;
    }

    #endregion Methods
}