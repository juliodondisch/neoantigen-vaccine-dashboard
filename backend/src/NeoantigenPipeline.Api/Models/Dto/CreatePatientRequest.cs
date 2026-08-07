namespace NeoantigenPipeline.Api.Models.Dto;

public class CreatePatientRequest
{
    public string Name { get; set; } = "";
    public string? Notes { get; set; }
    public string? CancerType { get; set; }
    public string? ReferenceGenome { get; set; }
}
