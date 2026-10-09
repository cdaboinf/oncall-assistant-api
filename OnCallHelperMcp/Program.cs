using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnCallHelperMcp;

var builder = Host.CreateApplicationBuilder(args);

// stdout carries the MCP protocol, so every log line must go to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var baseUrl = Environment.GetEnvironmentVariable("ONCALL_API_BASE_URL") ?? "http://localhost:5172";
var token = Environment.GetEnvironmentVariable("ONCALL_API_TOKEN");

builder.Services.AddHttpClient<OnCallApiClient>(http =>
{
    http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    // Triage calls OpenAI + vector search, so allow for slow responses.
    http.Timeout = TimeSpan.FromSeconds(90);
    if (!string.IsNullOrWhiteSpace(token))
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
});

builder.Services
    .AddMcpServer(o =>
    {
        o.ServerInfo = new() { Name = "oncall-helper", Version = "1.0.0" };
        o.ServerInstructions = ServerInstructions.Text;
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithPromptsFromAssembly();

await builder.Build().RunAsync();
