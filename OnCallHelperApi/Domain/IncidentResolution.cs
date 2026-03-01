namespace OnCallHelperApi.Domain;

public class IncidentResolution
{
    public string? Summary { get; set; }

    public List<string>? StepsTaken { get; set; }

    public string? RootCause { get; set; }

    public string? ResolvedBy { get; set; }
}