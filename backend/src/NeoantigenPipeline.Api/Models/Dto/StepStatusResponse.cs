namespace NeoantigenPipeline.Api.Models.Dto;

public class StepStatusResponse
{
    public StepState State { get; set; } = new();
    public JobRecord? ActiveJob { get; set; }
    public List<ManagedFile> InputFiles { get; set; } = new();
    public List<ManagedFile> OutputFiles { get; set; } = new();
}
