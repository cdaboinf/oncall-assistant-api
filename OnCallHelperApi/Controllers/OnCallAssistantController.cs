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
    public async Task<ActionResult<OnCallAssistantResponse>> Analyze([FromBody] AnalyzeIncidentRequest request)
    {
        var result = await _assistant.AnalyzeIncidentAsync(request.Description);
        return Ok(result);
    }
}
