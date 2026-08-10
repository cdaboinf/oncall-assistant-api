namespace OnCallHelperApi.Application.Services;

using DTOs.Incident;
using Domain;
using Infrastructure.Repositories;
using Mapping;

public class IncidentService : IIncidentService
{
    private readonly IIncidentRepository _repository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IOpenAiService _openAiService;

    public IncidentService(
        IIncidentRepository repository,
        IEmbeddingService embeddingService,
        IOpenAiService openAiService)
    {
        _repository = repository;
        _embeddingService = embeddingService;
        _openAiService = openAiService;
    }

    // Extract an incident draft from free-form text (e.g. a pasted Slack thread)
    public async Task<CreateIncidentRequest> ExtractDraftAsync(string conversation)
    {
        if (string.IsNullOrWhiteSpace(conversation))
        {
            return new CreateIncidentRequest();
        }

        return await _openAiService.ExtractIncidentFromConversationAsync(conversation);
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

        return IncidentMapper.ToResponse(incident);
    }

    // Get all incidents
    public async Task<List<IncidentResponse>> GetAllAsync()
    {
        var incidents = await _repository.GetAllAsync();
        return incidents.Select(IncidentMapper.ToResponse).ToList();
    }

    // Search / filter incident history
    public async Task<List<IncidentResponse>> SearchAsync(IncidentSearchRequest query)
    {
        var incidents = await _repository.SearchAsync(query);
        return incidents.Select(IncidentMapper.ToResponse).ToList();
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
            .Select(IncidentMapper.ToResponse)
            .ToList();
    }
    
    public async Task<int> RebuildEmbeddingsAsync()
    {
        var incidents = await _repository.GetAllAsync();
        var updatedCount = 0;

        foreach (var incident in incidents)
        {
            var description = incident.Metadata?.Description;
            if (string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            incident.Embedding = await _embeddingService.GetEmbeddingAsync(description);
            incident.EmbeddingVersion = 1;

            await _repository.UpdateAsync(incident);
            updatedCount++;
        }

        return updatedCount;
    }
}