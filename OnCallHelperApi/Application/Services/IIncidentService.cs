using OnCallHelperApi.Application.DTOs.Incident;
using OnCallHelperApi.Domain;

namespace OnCallHelperApi.Application.Services;

public interface IIncidentService
{
    Task<string> CreateAsync(CreateIncidentRequest request);
    Task<IncidentResponse?> GetByIdAsync(string id);
    Task<List<IncidentResponse>> GetAllAsync();
    Task<List<IncidentResponse>> FindSimilarIncidentsAsync(string description, int top = 5);
}