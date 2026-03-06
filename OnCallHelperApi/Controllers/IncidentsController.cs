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
        var id = await _service.CreateAsync(request);
        return Ok(new { Id = id });
    }

    [HttpPost("similar")]
    public async Task<IActionResult> FindSimilar([FromBody] SimilarIncidentsRequest request)
    {
        var result = await _service.FindSimilarIncidentsAsync(request.Description, request.Top);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var incidents = await _service.GetAllAsync();
        return Ok(incidents);
    }
}
