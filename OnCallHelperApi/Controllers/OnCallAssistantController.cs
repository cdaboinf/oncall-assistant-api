namespace OnCallHelperApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using OnCallHelperApi.Application.Services;
using OnCallHelperApi.Application.DTOs;

[ApiController]
[Route("api/oncall")]
public class OnCallAssistantController : ControllerBase
{
    private readonly IOnCallAssistantService _assistant;

    public OnCallAssistantController(IOnCallAssistantService assistant)
    {
        _assistant = assistant;
    }

    [HttpPost("analyze")]
    public async Task<ActionResult<OnCallAssistantResponse>> Analyze(
        [FromBody] AnalyzeIncidentRequest request)
    {
        var result = await _assistant.AnalyzeIncidentAsync(request.Description);

        return Ok(result);
    }
}