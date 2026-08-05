using System.ClientModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnCallHelperApi.Application.DTOs;
using OnCallHelperApi.Application.Services;

namespace OnCallHelperApi.Controllers;

[ApiController]
[Authorize]
[Route("api/oncall")]
public class OnCallAssistantController : ControllerBase
{
    private readonly IOnCallAssistantService _assistant;

    public OnCallAssistantController(IOnCallAssistantService assistant)
    {
        _assistant = assistant;
    }

    [HttpPost("analyze")]
    public async Task<ActionResult<TriageResult>> Analyze([FromBody] AnalyzeIncidentRequest request)
    {
        try
        {
            var result = await _assistant.AnalyzeIncidentAsync(request.Description);
            return Ok(result);
        }
        catch (ClientResultException ex)
        {
            // OpenAI errors (quota exhausted, rate limit, auth) -> clean message for the UI.
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "The AI service is unavailable right now.",
                detail = ex.Message
            });
        }
    }
}
