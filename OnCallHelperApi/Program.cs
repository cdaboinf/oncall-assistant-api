using Amazon.Lambda.AspNetCoreServer.Hosting;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using OnCallHelperApi.Infrastructure.Auth;
using OnCallHelperApi.Application.Services;
using OnCallHelperApi.Infrastructure.Mongo;
using OnCallHelperApi.Infrastructure.Mongo.Migrations;
using OnCallHelperApi.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Lambda hosting
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

// Load secrets from SSM using parameter names passed in env vars
var ssm = new AmazonSimpleSystemsManagementClient();

async Task<string?> GetParameterValueAsync(string envVarName)
{
    var parameterName = Environment.GetEnvironmentVariable(envVarName);

    if (string.IsNullOrWhiteSpace(parameterName))
    {
        return null;
    }

    var response = await ssm.GetParameterAsync(new GetParameterRequest
    {
        Name = parameterName,
        WithDecryption = true
    });

    return response.Parameter?.Value;
}

var bootstrapConfig = new Dictionary<string, string?>();

var openAiApiKey = await GetParameterValueAsync("OPENAI_API_KEY_PARAM");
var mongoConnectionString = await GetParameterValueAsync("MONGO_CONNECTION_STRING_PARAM");
var mongoDatabase = await GetParameterValueAsync("MONGO_DATABASE_PARAM");

if (!string.IsNullOrWhiteSpace(openAiApiKey))
{
    bootstrapConfig["OpenAI:ApiKey"] = openAiApiKey;
}

if (!string.IsNullOrWhiteSpace(mongoConnectionString))
{
    bootstrapConfig["Mongo:ConnectionString"] = mongoConnectionString;
}

if (!string.IsNullOrWhiteSpace(mongoDatabase))
{
    bootstrapConfig["Mongo:Database"] = mongoDatabase;
}

builder.Configuration.AddInMemoryCollection(bootstrapConfig);

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Mongo Context
builder.Services.AddSingleton<MongoContext>();

// Repositories
builder.Services.AddScoped<IIncidentRepository, IncidentRepository>();

// Migrations
//builder.Services.AddScoped<IMongoMigration, CreateInitialIndexes>();
//builder.Services.AddScoped<IMongoMigration, AddEmbeddingVersionField>();
//builder.Services.AddScoped<MongoMigrationRunner>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IOnCallAssistantService, OnCallAssistantService>();
builder.Services.AddSingleton<IOpenAiService, OpenAiService>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        var scheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token"
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes.Add("Bearer", scheme);

        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Id = "Bearer",
                    Type = ReferenceType.SecurityScheme
                }
            }] = Array.Empty<string>()
        });

        return Task.CompletedTask;
    });
});

// Auth can be disabled for local/personal use so the UI works without a token.
// Set "Auth:Enabled": false in appsettings.Development.json (or the ONCALL_AUTH_ENABLED env var).
var authEnabled = builder.Configuration.GetValue("Auth:Enabled", true);

if (authEnabled)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Auth0:Authority"];
            options.Audience = builder.Configuration["Auth0:Audience"];

            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidateAudience = true;
            options.TokenValidationParameters.ValidateLifetime = true;
        });
}
else
{
    // Dev scheme: every request is treated as an authenticated local user,
    // so [Authorize] controllers work without pasting a token.
    builder.Services.AddAuthentication(DevAuthHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevAuthHandler.SchemeName, _ => { });
}

builder.Services.AddAuthorization();

const string CorsPolicyName = "UiCorsPolicy";

// Origins come from config (Cors:AllowedOrigins, set via the Cors__AllowedOrigins__N
// Lambda env vars). If none are configured, allow any origin so the API can be
// called from anywhere. Add origins to lock CORS down without a code change.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
    .ToArray() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod();

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }
        else
        {
            policy.AllowAnyOrigin();
        }
    });
});

var app = builder.Build();

/*using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<MongoMigrationRunner>();
    await runner.RunAsync();
}*/

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

// CORS must run before HTTPS redirection so cross-origin preflight responses
// (and any redirect) still carry the Access-Control-Allow-Origin header.
app.UseCors(CorsPolicyName);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();