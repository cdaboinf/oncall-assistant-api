using MongoDB.Driver;

namespace OnCallHelperApi.Infrastructure.Mongo;

public class MongoMigrationRunner
{
    private readonly MongoContext _context;
    private readonly IEnumerable<IMongoMigration> _migrations;

    public MongoMigrationRunner(
        MongoContext context,
        IEnumerable<IMongoMigration> migrations)
    {
        _context = context;
        _migrations = migrations;
    }

    public async Task RunAsync()
    {
        var applied = await _context.Migrations
            .Find(_ => true)
            .ToListAsync();

        foreach (var migration in _migrations)
        {
            if (applied.Any(x => x.Name == migration.Name))
                continue;

            await migration.ApplyAsync(_context.Database);

            await _context.Migrations.InsertOneAsync(new MigrationRecord
            {
                Id = Guid.NewGuid(),
                Name = migration.Name,
                AppliedAt = DateTime.UtcNow
            });
        }
    }
}