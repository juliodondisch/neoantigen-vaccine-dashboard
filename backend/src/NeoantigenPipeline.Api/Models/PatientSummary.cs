namespace NeoantigenPipeline.Api.Models;

public class PatientSummary
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? CancerType { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CompletedSteps { get; set; }
    public int TotalSteps { get; set; }
    public string? FurthestStepId { get; set; }
    public long TotalDiskBytes { get; set; }
}
