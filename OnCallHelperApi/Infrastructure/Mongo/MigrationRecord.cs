namespace OnCallHelperApi.Infrastructure.Mongo;

public class MigrationRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DateTime AppliedAt { get; set; }
}