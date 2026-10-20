using System.Transactions;
using CricaStudio.Domain.Catalog;
using CricaStudio.Infrastructure.Persistence;
using Xunit;

namespace CricaStudio.Application.Tests;

public sealed class SupplierManagementTests
{
    [Theory]
    [InlineData(null,"unchecked","unchecked",true)]
    [InlineData("0","ok","in_stock",true)]
    [InlineData("149.90","ok","out_of_stock",true)]
    [InlineData("10","broken","in_stock",true)]
    [InlineData("-1","ok","in_stock",false)]
    [InlineData("1.234","ok","in_stock",false)]
    [InlineData("10000000000","ok","in_stock",false)]
    [InlineData("10","inventado","in_stock",false)]
    [InlineData("10","ok","inventado",false)]
    public void Validates_price_and_independent_link_and_stock_statuses(string? price,string link,string stock,bool valid)
    {
        var value=price is null?(decimal?)null:decimal.Parse(price,System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(valid,new SupplierManagement(value,link,stock).IsValid());
    }

    [PlatformDatabaseFact]
    public async Task Management_round_trips_and_can_be_cleared_without_altering_publication()
    {
        using var transaction=new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var connection=Environment.GetEnvironmentVariable("CRICA_PLATFORMS_TEST_DATABASE")!;
        var read=new PostgresCatalogReadRepository(connection);
        var write=new PostgresCatalogWriteRepository(connection);
        var type=(await read.GetAllTypesAsync(default)).First(t=>t.Scope is "suppliers" or "both");
        var item=await write.SaveAffiliateProductAsync(new AffiliateProduct(Guid.Empty,type.Id,"Management test","Test","Shopee",null,null,null,true,false,"draft",0)
            { Management=new(149.90m,"ok","out_of_stock") },default);
        var saved=(await read.GetAllAffiliateProductsAsync(default)).Single(p=>p.Id==item.Id);
        Assert.Equal(item.Management,saved.Management);
        await write.SaveAffiliateProductAsync(saved with { Management=new(null,"broken","in_stock") },default);
        saved=(await read.GetAllAffiliateProductsAsync(default)).Single(p=>p.Id==item.Id);
        Assert.Null(saved.Management.LastStorePrice);
        Assert.Equal("broken",saved.Management.LinkStatus);
        Assert.Equal("in_stock",saved.Management.StockStatus);
        Assert.Equal("draft",saved.Status);
    }
}
