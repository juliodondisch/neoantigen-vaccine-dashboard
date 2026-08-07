namespace NeoantigenPipeline.Api.Models;

public class StepDefinition
{
    public string Id { get; set; } = "";
    public int Order { get; set; }
    public string DisplayName { get; set; } = "";
    public string ShortDescription { get; set; } = "";
    public string LongExplanation { get; set; } = "";
    public string ToolName { get; set; } = "";
    public string[] RequiredInputStepIds { get; set; } = Array.Empty<string>();
    public bool IsUploadStep { get; set; }
    public bool HasParameters { get; set; }
    public bool ProducesDownload { get; set; }
    public string[] RequiredTools { get; set; } = Array.Empty<string>();
}
