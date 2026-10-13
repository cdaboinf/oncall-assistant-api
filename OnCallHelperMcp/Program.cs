using OnCallHelperMcp.Hosting;

// One project, two ways to run:
//   stdio (default)  local MCP clients (Claude Desktop / Code) start it as a child process.
//   http             hosted: Streamable HTTP behind Auth0 token validation (see Hosting/HttpHost.cs).
// HTTP is chosen with --http, ONCALL_MCP_MODE=http, or automatically inside AWS Lambda.
var http = args.Contains("--http")
    || string.Equals(Environment.GetEnvironmentVariable("ONCALL_MCP_MODE"), "http", StringComparison.OrdinalIgnoreCase)
    || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));

if (http) await HttpHost.RunAsync(args.Where(a => a != "--http").ToArray());
else await StdioHost.RunAsync(args);
