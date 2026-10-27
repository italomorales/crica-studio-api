using CricaStudio.Api.Catalog;
using Xunit;
namespace CricaStudio.Application.Tests;

public sealed class StatisticsTests
{
    [Theory]
    [InlineData("shop","view",true)]
    [InlineData("shop","click",true)]
    [InlineData("shop","whatsapp",true)]
    [InlineData("supplier","click",true)]
    [InlineData("supplier","view",false)]
    [InlineData("article","view",true)]
    [InlineData("article","click",false)]
    [InlineData("other","view",false)]
    public void Records_only_supported_events(string type,string metric,bool expected)
    {
        Assert.Equal(expected,StatisticsEndpoints.Valid(new(type,Guid.NewGuid(),metric,"home")));
        Assert.False(StatisticsEndpoints.Valid(new(type,Guid.Empty,metric,"home")));
        Assert.False(StatisticsEndpoints.Valid(new(type,Guid.NewGuid(),metric,"unknown")));
    }
}
