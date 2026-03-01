namespace OnCallHelperApi.Application.DTOs.Incident;

public class CreateIncidentRequest
{
    public string Title { get; set; }
    public string Description { get; set; }
    public string ServiceName { get; set; }
    public string Environment { get; set; }
    public string Severity { get; set; }
    public IncidentResolutionDto? Resolution { get; set; }
}