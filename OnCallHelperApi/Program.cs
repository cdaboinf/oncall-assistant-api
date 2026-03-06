using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using OnCallHelperApi.Application.Services;
using OnCallHelperApi.Infrastructure.Mongo;
using OnCallHelperApi.Infrastructure.Mongo.Migrations;
using OnCallHelperApi.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------
// Register services (BEFORE Build)
// ----------------------------

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Mongo Context
builder.Services.AddSingleton<MongoContext>();

// Repositories
builder.Services.AddScoped<IIncidentRepository, IncidentRepository>();

// Migrations
builder.Services.AddScoped<IMongoMigration, CreateInitialIndexes>();
builder.Services.AddScoped<IMongoMigration, AddEmbeddingVersionField>();
builder.Services.AddScoped<MongoMigrationRunner>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IOnCallAssistantService, OnCallAssistantService>();
builder.Services.AddSingleton<IOpenAiService, OpenAiService>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        // Define the Security Scheme
        var scheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token"
        };

        // Add it to the document
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes.Add("Bearer", scheme);

        // Apply it globally
        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Id = "Bearer", Type = ReferenceType.SecurityScheme
                }
            }] = Array.Empty<string>()
        });

        return Task.CompletedTask;
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://dev-k0sl1xaa1o87ofbn.us.auth0.com/";
        options.Audience = "http://localhost:5172";

        // Optional: Validate token lifetime, issuer, etc.
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
    });

builder.Services.AddAuthorization();

const string CorsPolicyName = "UiCorsPolicy";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:8080", "http://127.0.0.1:8080"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ----------------------------
// Build the app
// ----------------------------

var app = builder.Build();

// ----------------------------
// Run migrations BEFORE serving requests
// ----------------------------

using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider
        .GetRequiredService<MongoMigrationRunner>();

    await runner.RunAsync();
}

if (app.Environment.IsDevelopment())
{
    // This maps the JSON endpoint (usually /openapi/v1.json)
    app.MapOpenApi();

    // If you still want the Swagger UI visual interface:
    app.UseSwaggerUI(options => { options.SwaggerEndpoint("/openapi/v1.json", "v1"); });
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();