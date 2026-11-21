using Bravo.Application.Importing.Sources.Ibge;
using Bravo.Infrastructure.Ibge.Clients;
using Bravo.Infrastructure.Ibge.Extensions;
using Bravo.Infrastructure.Ibge.Options;
using Bravo.Infrastructure.Tse.Clients;
using Bravo.Infrastructure.Tse.Extensions;
using Bravo.Infrastructure.Tse.Options;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bravo.UnitTests.HttpClients;

/// <summary>
/// Verifies that both external providers compose through <c>IHttpClientFactory</c> and take
/// their settings from the bound options. A missing registration previously left the IBGE
/// reader unresolved and its base address unset, so this pins the wiring end to end.
/// </summary>
public sealed class ExternalHttpClientRegistrationTests
{
    private static IConfiguration Configuration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tse:BaseAddress"] = "https://cdn.tse.jus.br/",
                ["Tse:HttpTimeoutSeconds"] = "300",
                ["Tse:TempDirectory"] = Path.GetTempPath(),
                ["Ibge:BaseAddress"] = "https://servicodados.ibge.gov.br/",
                ["Ibge:HttpTimeoutSeconds"] = "120"
            })
            .Build();

    private static ServiceProvider CreateProvider()
    {
        var configuration = Configuration();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddIbge(configuration);
        services.AddTse(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = false
        });
    }

    [Fact]
    public void TheTseClientResolvesWithItsConfiguredBounds()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<IOptions<TseOptions>>().Value;

        options.BaseAddress.Should().Be("https://cdn.tse.jus.br/");
        options.HttpTimeoutSeconds.Should().Be(300);
        scope.ServiceProvider.GetRequiredService<TseHttpClient>().Should().NotBeNull();
    }

    [Fact]
    public void TheIbgeClientAndReaderResolveWithTheirConfiguredBounds()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<IOptions<IbgeOptions>>().Value;

        options.BaseAddress.Should().Be("https://servicodados.ibge.gov.br/");
        options.HttpTimeoutSeconds.Should().Be(120);
        scope.ServiceProvider.GetRequiredService<IbgeHttpClient>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IIbgeReader>().Should().NotBeNull();
    }

    [Fact]
    public void TheTwoProvidersBindIndependentAddresses()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        var tseAddress = scope.ServiceProvider.GetRequiredService<IOptions<TseOptions>>().Value.BaseAddress;
        var ibgeAddress = scope.ServiceProvider.GetRequiredService<IOptions<IbgeOptions>>().Value.BaseAddress;

        tseAddress.Should().NotBe(ibgeAddress);
    }

    [Fact]
    public void TheTseDatasetAcquirerResolvesThroughTheTypedClient()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider
            .GetRequiredService<Bravo.Domain.Electoral.Services.ITseDatasetAcquirer>()
            .Should().NotBeNull();
    }

    [Fact]
    public void EachTypedClientReceivesTheAddressConfiguredForItsProvider()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        factory.CreateClient(nameof(TseHttpClient)).BaseAddress
            .Should().Be(new Uri("https://cdn.tse.jus.br/"));

        factory.CreateClient(nameof(IbgeHttpClient)).BaseAddress
            .Should().Be(new Uri("https://servicodados.ibge.gov.br/"));
    }
}
