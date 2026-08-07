namespace NeoantigenPipeline.Api.Models.Dto;

public class RunStepResponse
{
    public string? JobId { get; set; }
    public bool Completed { get; set; }
    public StepResult? Result { get; set; }
}
