namespace NeoantigenPipeline.Api.Models.Dto;

public class UpdatePatientRequest
{
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public string? CancerType { get; set; }
}
