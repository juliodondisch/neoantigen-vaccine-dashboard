namespace NeoantigenPipeline.Api.Models.Dto;

public class RunStepRequest
{
    public Dictionary<string, object>? Parameters { get; set; }
    public bool Async { get; set; } = true;
}
