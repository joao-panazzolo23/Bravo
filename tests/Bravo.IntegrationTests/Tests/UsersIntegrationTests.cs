using Bravo.IntegrationTests.Abstract;
using Bravo.IntegrationTests.Configurations;

namespace Bravo.IntegrationTests.Tests;

[Collection("Database")]
public class UsersIntegrationTests(
    PostgresContainerFixture pg
) : IntegrationTestBase(pg)
{
    protected override string[] Schemas => ["users"];
}