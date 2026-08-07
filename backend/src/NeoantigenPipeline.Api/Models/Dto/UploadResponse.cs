namespace NeoantigenPipeline.Api.Models.Dto;

public class UploadResponse
{
    public bool Success { get; set; }
    public List<ManagedFile> UploadedFiles { get; set; } = new();
    public string? Error { get; set; }
}
