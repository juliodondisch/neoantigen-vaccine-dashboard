namespace NeoantigenPipeline.Api.Models;

public class Patient
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Notes { get; set; }
    public string? CancerType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? ReferenceGenome { get; set; }
}
