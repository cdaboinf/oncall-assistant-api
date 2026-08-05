namespace OnCallHelperApi.Application.DTOs.Incident;

/// <summary>
/// Query parameters for browsing/searching incident history.
/// All fields are optional; omitting everything returns the most recent incidents.
/// </summary>
public class IncidentSearchRequest
{
    /// <summary>Free-text term matched against title, description and service name.</summary>
    public string? Q { get; set; }

    public string? ServiceName { get; set; }

    public string? Severity { get; set; }

    public string? Environment { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int Limit { get; set; } = 100;
}
