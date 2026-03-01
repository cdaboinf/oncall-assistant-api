namespace OnCallHelperApi.Application.Services;

using DTOs.Incident;
using Domain;
using Infrastructure.Repositories;

public class IncidentService : IIncidentService
{
    private readonly IIncidentRepository _repository;
    private readonly IEmbeddingService _embeddingService;

    public IncidentService(IIncidentRepository repository, IEmbeddingService embeddingService)
    {
        _repository = repository;
        _embeddingService = embeddingService;
    }

    // Create a new incident with embedding
    public async Task<string> CreateAsync(CreateIncidentRequest request)
    {
        var incident = new Incident
        {
            Title = request.Title,
            Metadata = new IncidentMetadata
            {
                ServiceName = request.ServiceName,
                Environment = request.Environment,
                Severity = request.Severity,
                Description = request.Description
            },
            Resolution = new IncidentResolution
            {
                RootCause = request.Resolution.RootCause,
                Summary = request.Resolution.Summary,
                StepsTaken = request.Resolution.StepsTaken,
                ResolvedBy = request.Resolution.ResolvedBy
            },
            CreatedAt = DateTime.UtcNow,
            // Generate embedding vector for the description
            Embedding = await _embeddingService.GetEmbeddingAsync(request.Description),
            EmbeddingVersion = 1
        };

        await _repository.CreateAsync(incident);
        return incident.Id;
    }

    // Get incident by ID
    public async Task<IncidentResponse?> GetByIdAsync(string id)
    {
        var incident = await _repository.GetByIdAsync(id);
        if (incident == null) return null;

        return MapToResponse(incident);
    }

    // Get all incidents
    public async Task<List<IncidentResponse>> GetAllAsync()
    {
        var incidents = await _repository.GetAllAsync();
        return incidents.Select(MapToResponse).ToList();
    }

    // Optional: find similar incidents based on embedding
    public async Task<List<IncidentResponse>> FindSimilarIncidentsAsync(string description, int top = 5)
    {
        // 1️⃣ Generate embedding for incoming description
        var queryEmbedding = await _embeddingService.GetEmbeddingAsync(description);

        // 2️⃣ Let MongoDB Atlas perform vector search
        var similarIncidents = await _repository.FindSimilarAsync(queryEmbedding, top);

        // 3️⃣ Map to response
        return similarIncidents
            .Select(MapToResponse)
            .ToList();
    }

    // Helper: map Incident -> IncidentResponse
    private static IncidentResponse MapToResponse(Incident incident)
    {
        return new IncidentResponse
        {
            Id = incident.Id,
            Title = incident.Title,
            Description = incident.Metadata?.Description ?? string.Empty, // ensure description exists
            ServiceName = incident.Metadata?.ServiceName ?? string.Empty,
            Environment = incident.Metadata?.Environment ?? string.Empty,
            Severity = incident.Metadata?.Severity ?? string.Empty,
            Score = incident.Score,
            CreatedAt = incident.CreatedAt,
            Resolution = incident.Resolution != null 
                ? new IncidentResolution
                {
                    RootCause = incident.Resolution?.RootCause,
                    Summary = incident.Resolution?.Summary,
                    StepsTaken = incident.Resolution?.StepsTaken ?? [],
                    ResolvedBy = incident.Resolution?.ResolvedBy
                }
                : null
        };
    }
}