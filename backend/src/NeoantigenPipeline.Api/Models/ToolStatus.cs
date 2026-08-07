namespace NeoantigenPipeline.Api.Models;

public class ToolStatus
{
    public string ToolName { get; set; } = "";
    public bool IsAvailable { get; set; }
    public string? Version { get; set; }
    public string? ResolvedPath { get; set; }
    public string? Error { get; set; }
    public string[] UsedBySteps { get; set; } = Array.Empty<string>();
}
