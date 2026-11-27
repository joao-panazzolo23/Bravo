using Bravo.Application.Analytics;
using Bravo.Infrastructure.PostgreSQL.Dapper.Extensions;
using Bravo.ModelContextProtocol.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Enum arguments and results are rendered by name, matching the HTTP API's JSON configuration.
// Without this the MCP client would receive raw integers for things like aggregation level,
// sort order and margin direction, which is unreadable and version-fragile. The options must carry
// a type-info resolver because the MCP SDK builds reflection functions from them.
var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    Converters = { new JsonStringEnumConverter() }
};

// Analytical read model only: Mediator + validators from the Application analytics module, and the
// Dapper repositories that implement IElectionDataAnalytics. The ingestion pipeline is deliberately
// not referenced, so the host cannot accidentally start writes.
builder.Services
    .AddElectionAnalytics()
    .AddPostgreSQLDapper();

// Tool arguments are parsed with the same JSON conventions as the HTTP API, so an enumeration such
// as MarginDirection may be sent either as a name ("Largest") or as its numeric value.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ElectionAnalyticsTools>(serializerOptions);

await builder.Build().RunAsync();
