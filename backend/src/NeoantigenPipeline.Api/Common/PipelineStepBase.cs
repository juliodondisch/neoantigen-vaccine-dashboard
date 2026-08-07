using NeoantigenPipeline.Api.Common.Exceptions;
using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Common;

public abstract class PipelineStepBase : IPipelineStep
{
    protected readonly PathResolver Paths;
    protected readonly FileSystemService Files;
    protected readonly PythonRunner Python;
    protected readonly ToolChecker Tools;
    protected readonly ILogger Logger;

    protected PipelineStepBase(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, ILogger logger)
    {
        Paths = paths;
        Files = files;
        Python = python;
        Tools = tools;
        Logger = logger;
    }

    public abstract StepDefinition Definition { get; }

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
        var hasFiles = Files.StepHasFiles(patientId, Definition.Id);
        var validation = ValidateInputs(patientId);

        var status = hasFiles
            ? StepStatus.Completed
            : !validation.IsValid && validation.MissingTools.Count == 0
                ? StepStatus.InputsMissing
                : StepStatus.NotStarted;

        var outputFiles = GetOutputFiles(patientId);
        var state = new StepState
        {
            StepId = Definition.Id,
            Status = status,
            OutputFileCount = outputFiles.Count,
            OutputBytes = outputFiles.Sum(f => f.SizeBytes),
            LastRunAt = outputFiles.Count > 0 ? outputFiles.Max(f => f.CreatedAt) : null,
            LastSummary = ReadLatestSummary(patientId),
        };
        return Task.FromResult(state);
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
