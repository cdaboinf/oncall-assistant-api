namespace OnCallHelperApi.Domain;

public class IncidentMetadata
{
    public string ServiceName { get; set; }
    public string Environment { get; set; }
    public string Description { get; set; }
    public string Severity { get; set; }
}