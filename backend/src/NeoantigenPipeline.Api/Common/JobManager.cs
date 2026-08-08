using System.Collections.Concurrent;
using System.Text.Json;
using NeoantigenPipeline.Api.Common.Exceptions;
using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Common;

public class JobManager
{
    private readonly ConcurrentDictionary<string, JobRecord> _jobs = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations = new();
    private readonly PathResolver _paths;
    private readonly StepRegistry _registry;
    private readonly PatientLogger _patientLog;
    private readonly ILogger<JobManager> _logger;

    public JobManager(PathResolver paths, StepRegistry registry, PatientLogger patientLog, ILogger<JobManager> logger)
    {
        _paths = paths;
        _registry = registry;
        _patientLog = patientLog;
        _logger = logger;
    }

    public string StartJob(string patientId, string stepId, StepParameters parameters)
    {
        var jobId = Guid.NewGuid().ToString();
        var job = new JobRecord
        {
            JobId = jobId,
            PatientId = patientId,
            StepId = stepId,
            Status = JobStatus.Queued,
            StartedAt = DateTime.UtcNow,
        };
        _jobs[JobKey(patientId, jobId)] = job;
        var cts = new CancellationTokenSource();
        _cancellations[JobKey(patientId, jobId)] = cts;

        _ = ExecuteJobAsync(job, parameters);

        return jobId;
    }

    public JobRecord? GetJob(string patientId, string jobId) =>
        _jobs.TryGetValue(JobKey(patientId, jobId), out var job) ? job : LoadJob(patientId, jobId);

    public JobRecord? GetActiveJobForStep(string patientId, string stepId) =>
        _jobs.Values.FirstOrDefault(j => j.PatientId == patientId && j.StepId == stepId &&
            (j.Status == JobStatus.Queued || j.Status == JobStatus.Running));

    public List<JobRecord> ListJobs(string patientId) =>
        _jobs.Values.Where(j => j.PatientId == patientId).OrderByDescending(j => j.StartedAt).ToList();

    public bool CancelJob(string patientId, string jobId)
    {
        if (!_cancellations.TryGetValue(JobKey(patientId, jobId), out var cts))
            return false;
        cts.Cancel();
        if (_jobs.TryGetValue(JobKey(patientId, jobId), out var job))
            job.Status = JobStatus.Cancelled;
        return true;
    }

    public async Task<StepResult> RunSynchronousAsync(string patientId, string stepId, StepParameters parameters, CancellationToken ct = default)
    {
        var step = _registry.GetStep(stepId);
        _patientLog.Info(patientId, stepId, $"Run requested (sync). Parameters: {DescribeParameters(parameters)}");

        var validation = step.ValidateInputs(patientId);
        if (!validation.IsValid)
        {
            _patientLog.Warning(patientId, stepId, $"Validation failed: {DescribeValidation(validation)}");
            throw new StepValidationException(validation, stepId);
        }

        var start = DateTime.UtcNow;
        var result = await step.RunAsync(patientId, parameters, ct);
        LogOutcome(patientId, stepId, result, DateTime.UtcNow - start);
        return result;
    }

    private async Task ExecuteJobAsync(JobRecord job, StepParameters parameters)
    {
        var key = JobKey(job.PatientId, job.JobId);
        var cts = _cancellations.TryGetValue(key, out var c) ? c : new CancellationTokenSource();

        job.Status = JobStatus.Running;
        PersistJob(job);
        _patientLog.Info(job.PatientId, job.StepId, $"Run requested (job {job.JobId}). Parameters: {DescribeParameters(parameters)}");

        try
        {
            var step = _registry.GetStep(job.StepId);
            var validation = step.ValidateInputs(job.PatientId);
            if (!validation.IsValid)
            {
                job.Status = JobStatus.Failed;
                job.ErrorMessage = string.Join("; ", validation.Errors.Concat(validation.MissingTools.Select(t => $"missing tool: {t}")));
                job.CompletedAt = DateTime.UtcNow;
                _patientLog.Warning(job.PatientId, job.StepId, $"Validation failed: {DescribeValidation(validation)}");
                PersistJob(job);
                return;
            }

            var start = DateTime.UtcNow;
            var result = await step.RunAsync(job.PatientId, parameters, cts.Token);
            var duration = DateTime.UtcNow - start;
            job.Result = result;
            job.Status = result.Success ? JobStatus.Succeeded : JobStatus.Failed;
            job.ErrorMessage = result.Success ? null : (result.ErrorDetail ?? result.Message);
            job.ProgressPercent = 100;
            LogOutcome(job.PatientId, job.StepId, result, duration);
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Cancelled;
            _patientLog.Warning(job.PatientId, job.StepId, $"Job {job.JobId} cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} for step {StepId} failed", job.JobId, job.StepId);
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;
            _patientLog.Error(job.PatientId, job.StepId, $"Unhandled exception: {ex.Message}");
        }
        finally
        {
            job.CompletedAt = DateTime.UtcNow;
            PersistJob(job);
            _cancellations.TryRemove(key, out _);
        }
    }

    private void LogOutcome(string patientId, string stepId, StepResult result, TimeSpan duration)
    {
        if (result.Success)
        {
            _patientLog.Info(patientId, stepId, $"Succeeded in {duration.TotalSeconds:F1}s: {result.Message}");
            foreach (var file in result.OutputFiles.Where(f => f.SizeBytes == 0))
                _patientLog.Warning(patientId, stepId, $"Output file '{file.Name}' is empty (0 bytes)");
        }
        else
        {
            _patientLog.Error(patientId, stepId, $"Failed after {duration.TotalSeconds:F1}s: {result.Message} — {result.ErrorDetail}");
        }
    }

    private static string DescribeParameters(StepParameters parameters) =>
        parameters.Values.Count == 0 ? "(none)" : string.Join(", ", parameters.Values.Select(kv => $"{kv.Key}={kv.Value}"));

    private static string DescribeValidation(ValidationResult validation) =>
        string.Join("; ", validation.Errors.Concat(validation.MissingTools.Select(t => $"missing tool: {t}")));

    private void PersistJob(JobRecord job)
    {
        try
        {
            var dir = _paths.GetJobsDir(job.PatientId);
            Directory.CreateDirectory(dir);
            var path = _paths.GetJobPath(job.PatientId, job.JobId);
            File.WriteAllText(path, JsonSerializer.Serialize(job, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist job {JobId}", job.JobId);
        }
    }

    private JobRecord? LoadJob(string patientId, string jobId)
    {
        var path = _paths.GetJobPath(patientId, jobId);
        if (!File.Exists(path))
            return null;
        return JsonSerializer.Deserialize<JobRecord>(File.ReadAllText(path));
    }

    private void UpdateProgress(string jobId, int percent, string? logLine)
    {
        var job = _jobs.Values.FirstOrDefault(j => j.JobId == jobId);
        if (job is null) return;
        job.ProgressPercent = percent;
        if (logLine is not null) job.LogTail = logLine;
    }

    private static string JobKey(string patientId, string jobId) => $"{patientId}:{jobId}";
}
