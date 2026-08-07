using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Services._01_Upload;

public class UploadManifest
{
    public List<ManagedFile> TumorDna { get; set; } = new();
    public List<ManagedFile> NormalDna { get; set; } = new();
    public List<ManagedFile> TumorRna { get; set; } = new();
    public bool AlreadyAligned { get; set; }
    public long TotalBytes { get; set; }
}

public class UploadService : PipelineStepBase
{
    public const string StepId = PipelineStepIds.Upload;
    private static readonly string[] AllowedExtensions = { ".fastq", ".fq", ".fastq.gz", ".fq.gz", ".bam", ".cram" };
    private const long MaxBrowserUploadBytes = 2_147_483_648; // 2GB

    public override StepDefinition Definition { get; } = new()
    {
        Id = StepId,
        Order = 1,
        DisplayName = "Upload Sequencing Data",
        ShortDescription = "Upload tumor and normal DNA (and optional RNA) samples",
        LongExplanation = "Every analysis starts with two DNA samples from the same person: one from their tumor, one from healthy tissue. Comparing them is what reveals which mutations belong to the cancer specifically, rather than being part of the person's normal inherited genetics. Optionally, you can also upload RNA sequencing data, which shows which genes the tumor is actually using ,  this improves target selection later but isn't required.",
        ToolName = "None (file intake only)",
        RequiredInputStepIds = Array.Empty<string>(),
        IsUploadStep = true,
        HasParameters = false,
        ProducesDownload = false,
        RequiredTools = Array.Empty<string>(),
    };

    public UploadService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, ILogger<UploadService> logger)
        : base(paths, files, python, tools, logger)
    {
    }

    public override ValidationResult ValidateInputs(string patientId)
    {
        var result = ValidationResult.Valid();
        if (!HasTumorDna(patientId))
            result.AddError("Tumor DNA is required.");
        if (!HasNormalDna(patientId))
            result.AddError("Normal DNA is required.");
        return result;
    }

    public override Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var manifest = BuildManifest(patientId);
        Files.WriteJson(patientId, StepId, "_manifest.json", manifest);

        var summary = new Dictionary<string, object>
        {
            ["tumorDnaFiles"] = manifest.TumorDna.Count,
            ["normalDnaFiles"] = manifest.NormalDna.Count,
            ["rnaFiles"] = manifest.TumorRna.Count,
            ["alreadyAligned"] = manifest.AlreadyAligned,
            ["totalBytes"] = manifest.TotalBytes,
        };
        WriteSummary(patientId, summary);

        var result = StepResult.Ok(StepId, "Manifest built", GetOutputFiles(patientId), summary, DateTime.UtcNow - start);
        return Task.FromResult(result);
    }

    public ValidationResult ValidateUpload(IFormFile file, string? fileKind)
    {
        var result = ValidationResult.Valid();
        if (!IsAllowedExtension(file.FileName))
            result.AddError($"'{file.FileName}' has an unsupported extension. Allowed: {string.Join(", ", AllowedExtensions)}");
        if (file.Length == 0)
            result.AddError($"'{file.FileName}' is empty.");
        if (file.Length > MaxBrowserUploadBytes)
            result.AddWarning($"'{file.FileName}' exceeds the browser upload limit; use the server-side path mode instead.");
        return result;
    }

    public bool HasTumorDna(string patientId) => Files.StepHasFilesMatching(patientId, StepId, "tumor_dna_*") ||
        Files.StepHasFilesMatching(patientId, StepId, "tumor*.bam");

    public bool HasNormalDna(string patientId) => Files.StepHasFilesMatching(patientId, StepId, "normal_dna_*") ||
        Files.StepHasFilesMatching(patientId, StepId, "normal*.bam");

    public bool HasRnaSeq(string patientId) => Files.StepHasFilesMatching(patientId, StepId, "tumor_rna_*") ||
        Files.StepHasFilesMatching(patientId, StepId, "*rna*.bam");

    public bool InputsAreBam(string patientId) => Files.ListStepFiles(patientId, StepId)
        .Any(f => f.Extension.Equals(".bam", StringComparison.OrdinalIgnoreCase));

    public UploadManifest BuildManifest(string patientId)
    {
        var allFiles = Files.ListStepFiles(patientId, StepId).Where(f => f.Name != "_manifest.json").ToList();
        return new UploadManifest
        {
            TumorDna = allFiles.Where(f => f.FileKind == "tumor_dna").ToList(),
            NormalDna = allFiles.Where(f => f.FileKind == "normal_dna").ToList(),
            TumorRna = allFiles.Where(f => f.FileKind == "rna").ToList(),
            AlreadyAligned = allFiles.Any(f => f.Extension.Equals(".bam", StringComparison.OrdinalIgnoreCase)),
            TotalBytes = allFiles.Sum(f => f.SizeBytes),
        };
    }

    private static bool IsAllowedExtension(string fileName)
    {
        var normalized = NormalizeExtension(fileName);
        return AllowedExtensions.Contains(normalized);
    }

    private static string NormalizeExtension(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        foreach (var ext in AllowedExtensions.OrderByDescending(e => e.Length))
        {
            if (lower.EndsWith(ext))
                return ext;
        }
        return Path.GetExtension(lower);
    }
}
