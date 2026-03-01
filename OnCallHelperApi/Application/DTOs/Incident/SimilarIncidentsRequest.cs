namespace OnCallHelperApi.Application.DTOs.Incident;

public class SimilarIncidentsRequest
{
    public string Description { get; set; } = string.Empty;
    public int Top { get; set; } = 5;
}