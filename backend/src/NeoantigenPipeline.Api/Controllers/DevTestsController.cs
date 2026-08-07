using Microsoft.AspNetCore.Mvc;
using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Testing;

namespace NeoantigenPipeline.Api.Controllers;

[ApiController]
[Route("api/dev/tests")]
public class DevTestsController : ControllerBase
{
    private readonly AppConfig _config;
    private readonly FixtureSeeder _seeder;
    private readonly StepRegistry _registry;
    private readonly ILogger<DevTestsController> _logger;
    private static List<TestRunResult> _lastResults = new();

    public DevTestsController(AppConfig config, FixtureSeeder seeder, StepRegistry registry, ILogger<DevTestsController> logger)
    {
        _config = config;
        _seeder = seeder;
        _registry = registry;
        _logger = logger;
    }

    private ActionResult? RequireDevEnabled() =>
        _config.EnableDevEndpoints ? null : NotFound();

    [HttpPost("seed")]
    public async Task<ActionResult<Patient>> SeedTestPatient([FromBody] SeedRequest request)
    {
        if (RequireDevEnabled() is { } blocked) return blocked;
        var name = request.PatientName ?? $"__test_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        var patient = await _seeder.SeedPatientAsync(name, request.SeedThroughStepId, request.UseTinyFixtures);
        return Ok(patient);
    }

    [HttpPost("run")]
    public async Task<ActionResult<List<TestRunResult>>> RunTests([FromBody] RunTestsRequest request)
    {
        if (RequireDevEnabled() is { } blocked) return blocked;

        var patientId = request.PatientId;
        if (patientId is null)
        {
            var patient = await _seeder.SeedPatientAsync($"__test_{DateTime.UtcNow:yyyyMMdd_HHmmss}", PipelineStepIds.Filtering, useTinyFixtures: true);
            patientId = patient.Id;
        }

        var stepIds = request.StepIds ?? PipelineStepIds.All;
        var results = new List<TestRunResult>();

        foreach (var stepId in stepIds)
        {
            if (!_registry.TryGetStep(stepId, out var step) || step is null)
                continue;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var validation = step.ValidateInputs(patientId);

            if (validation.MissingTools.Count > 0)
            {
                results.Add(new TestRunResult
                {
                    StepId = stepId,
                    TestName = $"{stepId}_tier{request.Tier}",
                    Outcome = "Skipped",
                    SkipReason = $"Missing tool(s): {string.Join(", ", validation.MissingTools)}",
                    DurationSeconds = sw.Elapsed.TotalSeconds,
                });
                continue;
            }

            if (!validation.IsValid)
            {
                results.Add(new TestRunResult
                {
                    StepId = stepId,
                    TestName = $"{stepId}_tier{request.Tier}",
                    Outcome = "Failed",
                    Message = string.Join("; ", validation.Errors),
                    DurationSeconds = sw.Elapsed.TotalSeconds,
                });
                continue;
            }

            try
            {
                var result = await step.RunAsync(patientId, new StepParameters());
                results.Add(new TestRunResult
                {
                    StepId = stepId,
                    TestName = $"{stepId}_tier{request.Tier}",
                    Outcome = result.Success ? "Passed" : "Failed",
                    Message = result.Message,
                    DurationSeconds = sw.Elapsed.TotalSeconds,
                    Assertions = new List<string> { result.Success ? "RunAsync returned Success=true" : "RunAsync returned Success=false" },
                });
            }
            catch (Exception ex)
            {
                results.Add(new TestRunResult
                {
                    StepId = stepId,
                    TestName = $"{stepId}_tier{request.Tier}",
                    Outcome = "Failed",
                    Message = ex.Message,
                    DurationSeconds = sw.Elapsed.TotalSeconds,
                });
            }
        }

        _lastResults = results;
        return Ok(results);
    }

    [HttpGet("results")]
    public ActionResult<List<TestRunResult>> GetLastResults()
    {
        if (RequireDevEnabled() is { } blocked) return blocked;
        return Ok(_lastResults);
    }

    [HttpDelete("cleanup")]
    public async Task<ActionResult> CleanupTestPatients()
    {
        if (RequireDevEnabled() is { } blocked) return blocked;
        await _seeder.CleanupTestPatientsAsync();
        return NoContent();
    }
}

public class SeedRequest
{
    public string? PatientName { get; set; }
    public string SeedThroughStepId { get; set; } = PipelineStepIds.Filtering;
    public bool UseTinyFixtures { get; set; } = true;
}

public class RunTestsRequest
{
    public int Tier { get; set; } = 1;
    public string[]? StepIds { get; set; }
    public string? PatientId { get; set; }
}

public class TestRunResult
{
    public string StepId { get; set; } = "";
    public string TestName { get; set; } = "";
    public string Outcome { get; set; } = ""; // "Passed" | "Failed" | "Skipped"
    public string? Message { get; set; }
    public string? SkipReason { get; set; }
    public double DurationSeconds { get; set; }
    public List<string> Assertions { get; set; } = new();
}
