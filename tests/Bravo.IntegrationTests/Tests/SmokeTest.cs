using Bravo.IntegrationTests.Abstract;
using Bravo.IntegrationTests.Configurations;
using Dapper;
using FluentAssertions;

namespace Bravo.IntegrationTests.Tests;
/// <summary>
/// Minimal class just to test Docker & Postgres Containers 
/// </summary>
/// <param name="pg"></param>
public class SmokeTests(
    PostgresContainerFixture pg
    ) : IntegrationTestBase(pg)
{
    [Fact]
    public async Task Database_ShouldBeReachable()
    {
        var result = await Db.ExecuteScalarAsync<int>("SELECT 1");
        result.Should().Be(1);
    }

    [Fact]
    public async Task Database_ShouldHaveTables()
    {
        var count = await Db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'");

        count.Should().BeGreaterThan(0);
    }
}