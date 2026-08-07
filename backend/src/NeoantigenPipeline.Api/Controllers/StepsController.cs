using Microsoft.AspNetCore.Mvc;
using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Common.Exceptions;
using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Models.Dto;
using NeoantigenPipeline.Api.Services._10_Ranking;

namespace NeoantigenPipeline.Api.Controllers;

[ApiController]
[Route("api/patients/{patientId}/steps")]
public class StepsController : ControllerBase
{
    private readonly StepRegistry _registry;
    private readonly JobManager _jobs;
    private readonly PatientRepository _patients;
    private readonly ILogger<StepsController> _logger;

    public StepsController(StepRegistry registry, JobManager jobs, PatientRepository patients, ILogger<StepsController> logger)
    {
        _registry = registry;
        _jobs = jobs;
        _patients = patients;
        _logger = logger;
    }

    [HttpGet]
    [HttpGet("/api/steps")] // non-patient-scoped alias: step definitions are patient-independent
    public ActionResult<List<StepDefinition>> ListDefinitions() => Ok(_registry.GetAllDefinitions());

    [HttpGet("states")]
    public async Task<ActionResult<List<StepState>>> GetAllStates(string patientId)
    {
        var states = new List<StepState>();
        foreach (var step in _registry.GetAllSteps())
            states.Add(await step.GetStateAsync(patientId));
        return Ok(states);
    }

    [HttpGet("{stepId}")]
    public async Task<ActionResult<StepStatusResponse>> GetStatus(string patientId, string stepId)
    {
        if (!_registry.TryGetStep(stepId, out var step) || step is null)
            return NotFound(ApiError(404, $"Unknown step '{stepId}'"));

        var response = new StepStatusResponse
        {
            State = await step.GetStateAsync(patientId),
            ActiveJob = _jobs.GetActiveJobForStep(patientId, stepId),
            InputFiles = step.GetInputFiles(patientId),
            OutputFiles = step.GetOutputFiles(patientId),
        };
        return Ok(response);
    }

    [HttpGet("{stepId}/validate")]
    public ActionResult<ValidationResult> Validate(string patientId, string stepId)
    {
        if (!_registry.TryGetStep(stepId, out var step) || step is null)
            return NotFound(ApiError(404, $"Unknown step '{stepId}'"));
        return Ok(step.ValidateInputs(patientId));
    }

    [HttpPost("{stepId}/run")]
    public async Task<ActionResult<RunStepResponse>> Run(string patientId, string stepId, [FromBody] RunStepRequest request)
    {
        if (!_registry.TryGetStep(stepId, out var step) || step is null)
            return NotFound(ApiError(404, $"Unknown step '{stepId}'"));

        var parameters = new StepParameters { Values = request.Parameters ?? new Dictionary<string, object>() };

        var validation = step.ValidateInputs(patientId);
        if (!validation.IsValid)
        {
            return BadRequest(ApiError(400, $"Step {stepId} cannot run",
                string.Join("; ", validation.Errors.Concat(validation.MissingTools.Select(t => $"missing tool: {t}")))));
        }

        if (request.Async)
        {
            var jobId = _jobs.StartJob(patientId, stepId, parameters);
            return Accepted(new RunStepResponse { JobId = jobId, Completed = false });
        }

        try
        {
            var result = await _jobs.RunSynchronousAsync(patientId, stepId, parameters);
            return Ok(new RunStepResponse { Completed = true, Result = result });
        }
        catch (PythonExecutionException ex)
        {
            return StatusCode(500, ApiError(500, $"Step {stepId} failed", ex.Stderr));
        }
    }

    [HttpGet("{stepId}/jobs/{jobId}")]
    public ActionResult<JobRecord> GetJob(string patientId, string stepId, string jobId)
    {
        var job = _jobs.GetJob(patientId, jobId);
        return job is null ? NotFound(ApiError(404, $"Job '{jobId}' not found")) : Ok(job);
    }

    [HttpPost("{stepId}/jobs/{jobId}/cancel")]
    public ActionResult Cancel(string patientId, string stepId, string jobId) =>
        _jobs.CancelJob(patientId, jobId) ? NoContent() : NotFound(ApiError(404, $"Job '{jobId}' not found"));

    [HttpGet("{stepId}/summary")]
    public async Task<ActionResult<Dictionary<string, object>>> GetSummary(string patientId, string stepId)
    {
        if (!_registry.TryGetStep(stepId, out var step) || step is null)
            return NotFound(ApiError(404, $"Unknown step '{stepId}'"));
        var state = await step.GetStateAsync(patientId);
        return Ok(state.LastSummary ?? new Dictionary<string, object>());
    }

    [HttpPost("10_ranking/preview")]
    public ActionResult<List<NeoantigenCandidate>> PreviewRanking(string patientId, [FromBody] RankingPreviewRequest request)
    {
        var rankingStep = _registry.GetStep(PipelineStepIds.Ranking);
        if (rankingStep is not RankingService rankingService)
            return StatusCode(500, ApiError(500, "Ranking service misconfigured"));

        var weights = request.Weights ?? RankingWeights.Default();
        return Ok(rankingService.Preview(patientId, weights, request.TargetCount));
    }

    private static object ApiError(int status, string message, string? detail = null) => new { status, message, detail };
}

public class RankingPreviewRequest
{
    public RankingWeights? Weights { get; set; }
    public int TargetCount { get; set; } = 30;
}
