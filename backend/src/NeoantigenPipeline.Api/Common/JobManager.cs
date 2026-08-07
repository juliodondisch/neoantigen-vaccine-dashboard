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
    private readonly ILogger<JobManager> _logger;

    public JobManager(PathResolver paths, StepRegistry registry, ILogger<JobManager> logger)
    {
        _paths = paths;
        _registry = registry;
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
        var validation = step.ValidateInputs(patientId);
        if (!validation.IsValid)
        {
            throw new StepValidationException(validation, stepId);
        }
        return await step.RunAsync(patientId, parameters, ct);
    }

    private async Task ExecuteJobAsync(JobRecord job, StepParameters parameters)
    {
        var key = JobKey(job.PatientId, job.JobId);
        var cts = _cancellations.TryGetValue(key, out var c) ? c : new CancellationTokenSource();

        job.Status = JobStatus.Running;
        PersistJob(job);

        try
        {
            var step = _registry.GetStep(job.StepId);
            var validation = step.ValidateInputs(job.PatientId);
            if (!validation.IsValid)
            {
                job.Status = JobStatus.Failed;
                job.ErrorMessage = string.Join("; ", validation.Errors.Concat(validation.MissingTools.Select(t => $"missing tool: {t}")));
                job.CompletedAt = DateTime.UtcNow;
                PersistJob(job);
                return;
            }

            var result = await step.RunAsync(job.PatientId, parameters, cts.Token);
            job.Result = result;
            job.Status = result.Success ? JobStatus.Succeeded : JobStatus.Failed;
            job.ErrorMessage = result.Success ? null : (result.ErrorDetail ?? result.Message);
            job.ProgressPercent = 100;
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Cancelled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} for step {StepId} failed", job.JobId, job.StepId);
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;
        }
        finally
        {
            job.CompletedAt = DateTime.UtcNow;
            PersistJob(job);
            _cancellations.TryRemove(key, out _);
        }
    }

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
