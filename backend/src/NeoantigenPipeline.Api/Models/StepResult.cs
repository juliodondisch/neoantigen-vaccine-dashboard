namespace NeoantigenPipeline.Api.Models;

public class StepResult
{
    public bool Success { get; set; }
    public string StepId { get; set; } = "";
    public string? Message { get; set; }
    public string? ErrorDetail { get; set; }
    public List<ManagedFile> OutputFiles { get; set; } = new();
    public Dictionary<string, object> Summary { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public DateTime CompletedAt { get; set; }

    public static StepResult Ok(string stepId, string message, List<ManagedFile> files, Dictionary<string, object> summary, TimeSpan duration) =>
        new()
        {
            Success = true,
            StepId = stepId,
            Message = message,
            OutputFiles = files,
            Summary = summary,
            Duration = duration,
            CompletedAt = DateTime.UtcNow,
        };

    public static StepResult Fail(string stepId, string message, string? detail = null) =>
        new()
        {
            Success = false,
            StepId = stepId,
            Message = message,
            ErrorDetail = detail,
            CompletedAt = DateTime.UtcNow,
        };
}
