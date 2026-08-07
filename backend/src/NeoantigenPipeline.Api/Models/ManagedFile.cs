namespace NeoantigenPipeline.Api.Models;

public class ManagedFile
{
    public string Name { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string Extension { get; set; } = "";
    public string? FileKind { get; set; }
    public bool IsUserUploaded { get; set; }
}
