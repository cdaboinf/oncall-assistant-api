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
builder.Services.AddSwaggerGen();

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

// ----------------------------
// Configure middleware
// ----------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OnCallHelper API V1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();