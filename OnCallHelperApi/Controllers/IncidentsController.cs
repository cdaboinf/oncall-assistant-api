using System.ClientModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnCallHelperApi.Application.DTOs.Incident;
using OnCallHelperApi.Application.Services;

namespace OnCallHelperApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class IncidentsController : ControllerBase
{
    private readonly IIncidentService _service;

    public IncidentsController(IIncidentService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateIncidentRequest request)
    {
        try
        {
            var id = await _service.CreateAsync(request);
            return Ok(new { Id = id });
        }
        catch (ClientResultException ex)
        {
            return AiUnavailable(ex);
        }
    }

    [HttpPost("similar")]
    public async Task<IActionResult> FindSimilar([FromBody] SimilarIncidentsRequest request)
    {
        try
        {
            var result = await _service.FindSimilarIncidentsAsync(request.Description, request.Top);
            return Ok(result);
        }
        catch (ClientResultException ex)
        {
            return AiUnavailable(ex);
        }
    }

    [HttpPost("rebuild-embeddings")]
    public async Task<IActionResult> RebuildEmbeddings()
    {
        try
        {
            var updated = await _service.RebuildEmbeddingsAsync();
            return Ok(new { Updated = updated });
        }
        catch (ClientResultException ex)
        {
            return AiUnavailable(ex);
        }
    }

    // Embedding generation goes through OpenAI; surface quota/rate-limit errors cleanly.
    private IActionResult AiUnavailable(ClientResultException ex) =>
        StatusCode(StatusCodes.Status502BadGateway, new
        {
            error = "The AI service is unavailable right now.",
            detail = ex.Message
        });

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] IncidentSearchRequest query)
    {
        var incidents = await _service.SearchAsync(query);
        return Ok(incidents);
    }
}
