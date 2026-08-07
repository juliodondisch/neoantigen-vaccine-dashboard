namespace NeoantigenPipeline.Api.Models;

public enum StepStatus
{
    NotStarted,
    InputsMissing,
    Ready,
    Running,
    Completed,
    Failed
}

public class StepState
{
    public string StepId { get; set; } = "";
    public StepStatus Status { get; set; }
    public DateTime? LastRunAt { get; set; }
    public string? LastError { get; set; }
    public int OutputFileCount { get; set; }
    public long OutputBytes { get; set; }
    public string? ActiveJobId { get; set; }
    public Dictionary<string, object>? LastSummary { get; set; }
}
