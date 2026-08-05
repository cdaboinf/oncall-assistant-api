using OnCallHelperApi.Application.DTOs.Incident;
using OnCallHelperApi.Domain;

namespace OnCallHelperApi.Infrastructure.Repositories;

public interface IIncidentRepository
{
    Task CreateAsync(Incident incident);
    Task UpdateAsync(Incident incident);
    Task<Incident?> GetByIdAsync(string id);
    Task<List<Incident>> GetAllAsync();
    Task<List<Incident>> SearchAsync(IncidentSearchRequest query);
    Task<List<Incident>> FindSimilarAsync(float[] embedding, int top);
}