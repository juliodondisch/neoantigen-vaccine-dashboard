using Microsoft.AspNetCore.Mvc;
using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Common.Exceptions;
using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Models.Dto;

namespace NeoantigenPipeline.Api.Controllers;

[ApiController]
[Route("api/patients")]
public class PatientsController : ControllerBase
{
    private readonly PatientRepository _repository;
    private readonly ILogger<PatientsController> _logger;

    public PatientsController(PatientRepository repository, ILogger<PatientsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<PatientSummary>>> List() => Ok(await _repository.ListAsync());

    [HttpGet("{patientId}")]
    public async Task<ActionResult<Patient>> Get(string patientId)
    {
        var patient = await _repository.GetAsync(patientId);
        return patient is null ? NotFound(ApiError(404, $"Patient '{patientId}' not found")) : Ok(patient);
    }

    [HttpPost]
    public async Task<ActionResult<Patient>> Create([FromBody] CreatePatientRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ApiError(400, "Patient name is required"));
        var patient = await _repository.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { patientId = patient.Id }, patient);
    }

    [HttpPatch("{patientId}")]
    public async Task<ActionResult<Patient>> Update(string patientId, [FromBody] UpdatePatientRequest request)
    {
        try
        {
            return Ok(await _repository.UpdateAsync(patientId, request));
        }
        catch (PatientNotFoundException ex)
        {
            return NotFound(ApiError(404, ex.Message));
        }
    }

    [HttpDelete("{patientId}")]
    public async Task<ActionResult> Delete(string patientId, [FromQuery] bool deleteFiles = false)
    {
        var deleted = await _repository.DeleteAsync(patientId, deleteFiles);
        return deleted ? NoContent() : NotFound(ApiError(404, $"Patient '{patientId}' not found"));
    }

    [HttpGet("{patientId}/summary")]
    public async Task<ActionResult<PatientSummary>> GetSummary(string patientId)
    {
        var patient = await _repository.GetAsync(patientId);
        if (patient is null)
            return NotFound(ApiError(404, $"Patient '{patientId}' not found"));
        return Ok(await _repository.BuildSummaryAsync(patient));
    }

    private static object ApiError(int status, string message, string? detail = null) => new { status, message, detail };
}
