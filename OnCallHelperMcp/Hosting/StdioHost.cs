using OnCallHelperMcp.Tools;

namespace OnCallHelperMcp.Hosting;

public static class StdioHost
{
    public static async Task RunAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // stdout carries the MCP protocol, so every log line must go to stderr.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

        builder.Services.AddOnCallApi();
        builder.Services
            .AddMcpServer(ApiRegistration.ServerOptions)
            .WithStdioServerTransport()
            .WithLenientTools<OnCallTools>()
            .WithPromptsFromAssembly();

        await builder.Build().RunAsync();
    }
}
