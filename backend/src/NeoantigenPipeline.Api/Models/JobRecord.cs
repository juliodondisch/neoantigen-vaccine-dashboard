namespace NeoantigenPipeline.Api.Models;

public enum JobStatus { Queued, Running, Succeeded, Failed, Cancelled }

public class JobRecord
{
    public string JobId { get; set; } = "";
    public string PatientId { get; set; } = "";
    public string StepId { get; set; } = "";
    public JobStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public StepResult? Result { get; set; }
    public string? LogTail { get; set; }
    public int ProgressPercent { get; set; }
}
