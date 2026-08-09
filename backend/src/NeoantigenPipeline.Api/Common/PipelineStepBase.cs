using NeoantigenPipeline.Api.Common.Exceptions;
using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Common;

public abstract class PipelineStepBase : IPipelineStep
{
    protected readonly PathResolver Paths;
    protected readonly FileSystemService Files;
    protected readonly PythonRunner Python;
    protected readonly ToolChecker Tools;
    protected readonly AppConfig Config;
    protected readonly ILogger Logger;

    protected PipelineStepBase(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, AppConfig config, ILogger logger)
    {
        Paths = paths;
        Files = files;
        Python = python;
        Tools = tools;
        Config = config;
        Logger = logger;
    }

    public abstract StepDefinition Definition { get; }

    public virtual string[] PrimaryOutputPatterns => Array.Empty<string>();

    public abstract Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken cancellationToken = default);

    public virtual ValidationResult ValidateInputs(string patientId)
    {
        var result = ValidateRequiredSteps(patientId);
        var toolResult = ValidateRequiredTools();
        result.Errors.AddRange(toolResult.Errors);
        result.MissingTools.AddRange(toolResult.MissingTools);
        result.Warnings.AddRange(toolResult.Warnings);
        if (toolResult.MissingTools.Count > 0)
            result.IsValid = false;
        return result;
    }

    public virtual Task<StepState> GetStateAsync(string patientId)
    {
        var validation = ValidateInputs(patientId);
        var outputFiles = GetOutputFiles(patientId);
        var hasPrimaryOutputs = PrimaryOutputPatterns.Length > 0
            ? PrimaryOutputPatterns.Any(pattern => Files.StepHasFilesMatching(patientId, Definition.Id, pattern))
            : outputFiles.Count > 0;

        var lastJob = GetLastJobRecord(patientId);

        // Status resolution order: an active job wins outright; a job that's known to have
        // failed wins even if some intermediate file happens to be sitting on disk (that used
        // to make a failed step look Completed — see docs/CORRECTION_PLAN.md §5.2); only then
        // do real primary outputs count as Completed.
        StepStatus status;
        if (lastJob is { Status: JobStatus.Running or JobStatus.Queued })
            status = StepStatus.Running;
        else if (lastJob is { Status: JobStatus.Failed })
            status = StepStatus.Failed;
        else if (hasPrimaryOutputs)
            status = StepStatus.Completed;
        else if (!validation.IsValid && validation.MissingTools.Count == 0)
            status = StepStatus.InputsMissing;
        else
            status = StepStatus.NotStarted;

        var state = new StepState
        {
            StepId = Definition.Id,
            Status = status,
            OutputFileCount = outputFiles.Count,
            OutputBytes = outputFiles.Sum(f => f.SizeBytes),
            ActiveJobId = lastJob is { Status: JobStatus.Running or JobStatus.Queued } ? lastJob.JobId : null,
            LastError = lastJob is { Status: JobStatus.Failed } ? lastJob.ErrorMessage : null,
            LastRunAt = outputFiles.Count > 0 ? outputFiles.Max(f => f.CreatedAt) : null,
            LastSummary = ReadLatestSummary(patientId),
        };
        return Task.FromResult(state);
    }

    /// <summary>Reads the most recently started job record for this step directly off disk
    /// (rather than through JobManager, which would create a DI cycle: JobManager depends on
    /// StepRegistry, which depends on every IPipelineStep). Persisted job files are written by
    /// JobManager at both job-start and job-completion, so this reflects live status with only
    /// a brief window of staleness around a status transition.</summary>
    private JobRecord? GetLastJobRecord(string patientId)
    {
        var dir = Paths.GetJobsDir(patientId);
        if (!Directory.Exists(dir))
            return null;

        JobRecord? latest = null;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            JobRecord? job;
            try
            {
                job = System.Text.Json.JsonSerializer.Deserialize<JobRecord>(File.ReadAllText(file));
            }
            catch
            {
                continue;
            }
            if (job is null || job.StepId != Definition.Id)
                continue;
            if (latest is null || job.StartedAt > latest.StartedAt)
                latest = job;
        }
        return latest;
    }

    public virtual List<ManagedFile> GetInputFiles(string patientId)
    {
        var files = new List<ManagedFile>();
        foreach (var stepId in Definition.RequiredInputStepIds)
            files.AddRange(Files.ListStepFiles(patientId, stepId));
        return files;
    }

    public virtual List<ManagedFile> GetOutputFiles(string patientId) => Files.ListStepFiles(patientId, Definition.Id);

    protected ValidationResult ValidateRequiredSteps(string patientId)
    {
        var result = ValidationResult.Valid();
        foreach (var requiredStepId in Definition.RequiredInputStepIds)
        {
            if (!Files.StepHasFiles(patientId, requiredStepId))
            {
                result.AddError($"Required input step '{requiredStepId}' has no output files. Run it first.");
            }
        }
        return result;
    }

    protected ValidationResult ValidateRequiredTools()
    {
        var result = ValidationResult.Valid();
        foreach (var missing in Tools.GetMissingTools(Definition.RequiredTools))
        {
            result.AddMissingTool(missing);
        }
        return result;
    }

    protected StepResult BuildResult(string patientId, PythonResponse response, TimeSpan duration)
    {
        var outputFiles = response.OutputFiles
            .Select(path => Files.ListStepFiles(patientId, Definition.Id, Path.GetFileName(path)).FirstOrDefault())
            .Where(f => f is not null)
            .Cast<ManagedFile>()
            .ToList();

        if (outputFiles.Count == 0)
            outputFiles = GetOutputFiles(patientId).OrderByDescending(f => f.CreatedAt).Take(response.OutputFiles.Count == 0 ? 0 : response.OutputFiles.Count).ToList();

        return response.Success
            ? StepResult.Ok(Definition.Id, response.Message ?? "Completed", outputFiles, response.Summary, duration)
            : StepResult.Fail(Definition.Id, response.Message ?? "Failed", response.Error);
    }

    /// <summary>Resolution order: explicit step parameter → the patient's own stored
    /// ReferenceGenome → app-wide default. Reads patient.json directly rather than through
    /// PatientRepository, since PatientRepository -> StepRegistry -> IPipelineStep would be a
    /// DI cycle if injected here.</summary>
    protected string ResolveReferenceGenome(string patientId, StepParameters parameters)
    {
        var explicitValue = parameters.GetString("referenceGenome");
        if (!string.IsNullOrWhiteSpace(explicitValue))
            return explicitValue;

        var jsonPath = Paths.GetPatientJsonPath(patientId);
        if (File.Exists(jsonPath))
        {
            var patient = System.Text.Json.JsonSerializer.Deserialize<Models.Patient>(File.ReadAllText(jsonPath));
            if (!string.IsNullOrWhiteSpace(patient?.ReferenceGenome))
                return patient!.ReferenceGenome!;
        }

        return Config.DefaultReferenceGenome;
    }

    protected string RequireLatestFile(string patientId, string stepId, string glob, string friendlyName)
    {
        var file = Files.FindLatestFile(patientId, stepId, glob);
        if (file is null)
            throw new StepValidationException(ValidationResult.Invalid($"{friendlyName} not found in {stepId} (pattern: {glob})"), Definition.Id);
        return Path.Combine(Paths.GetStepDir(patientId, stepId), file.Name);
    }

    protected void WriteSummary(string patientId, Dictionary<string, object> summary) =>
        Files.WriteJson(patientId, Definition.Id, $"_last_summary.json", summary);

    protected Dictionary<string, object>? ReadLatestSummary(string patientId) =>
        Files.ReadJson<Dictionary<string, object>>(patientId, Definition.Id, "_last_summary.json");
}
