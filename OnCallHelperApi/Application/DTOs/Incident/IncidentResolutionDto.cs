namespace OnCallHelperApi.Application.DTOs.Incident;

public class IncidentResolutionDto
{
    public string RootCause { get; set; }

    public string Summary { get; set; }

    public List<string> StepsTaken { get; set; }

    public string ResolvedBy { get; set; }
}