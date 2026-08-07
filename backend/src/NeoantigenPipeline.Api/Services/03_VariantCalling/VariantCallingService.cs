using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Services._03_VariantCalling;

public class VariantSummary
{
    public int TotalVariants { get; set; }
    public int PassVariants { get; set; }
    public int FilteredVariants { get; set; }
    public Dictionary<string, int> FilterReasons { get; set; } = new();
    public List<double> VafDistribution { get; set; } = new();
    public double MedianVaf { get; set; }
}

public class VariantCallingService : PipelineStepBase
{
    public const string StepId = PipelineStepIds.Variants;

    public override StepDefinition Definition { get; } = new()
    {
        Id = StepId,
        Order = 3,
        DisplayName = "Call Somatic Mutations",
        ShortDescription = "Find DNA differences between tumor and normal samples",
        LongExplanation = "This compares the tumor DNA against the healthy DNA from the same person and flags every position where they differ. Those differences are mutations that arose in the cancer specifically. The comparison against the person's own healthy tissue is essential — without it, you'd be flagging thousands of harmless inherited variations that every human has.",
        ToolName = "Mutect2 (GATK)",
        RequiredInputStepIds = new[] { PipelineStepIds.Alignment },
        IsUploadStep = false,
        HasParameters = true,
        ProducesDownload = false,
        RequiredTools = new[] { "gatk" },
    };

    public VariantCallingService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, ILogger<VariantCallingService> logger)
        : base(paths, files, python, tools, logger)
    {
    }

    public override async Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var tumorBam = ResolveTumorBam(patientId);
        var normalBam = ResolveNormalBam(patientId);
        if (normalBam is null)
            return StepResult.Fail(StepId, "No matched normal BAM found",
                "Mutect2 requires a matched normal sample; running tumor-only produces unreliable calls.");

        var reference = parameters.GetString("referenceGenome") ?? "chr21_test";
        var outputVcf = Paths.BuildOutputPath(patientId, StepId, "somatic", ".vcf.gz");

        var args = new Dictionary<string, string>
        {
            ["tumor-bam"] = tumorBam!,
            ["normal-bam"] = normalBam,
            ["reference"] = Paths.GetReferenceFasta(reference),
            ["output-vcf"] = outputVcf,
            ["panel-of-normals"] = parameters.GetBool("usePanelOfNormals", true) ? Paths.GetPanelOfNormals(reference) : "",
            ["intervals"] = parameters.GetString("intervals") ?? "",
            ["min-vaf"] = parameters.GetDouble("minVaf", 0.05).ToString("F2"),
        };

        try
        {
            var response = await Python.RunAndParseAsync("call_variants.py", args, new PythonExecutionOptions { TimeoutSeconds = 7200, CancellationToken = ct });
            WriteSummary(patientId, response.Summary);
            return BuildResult(patientId, response, DateTime.UtcNow - start);
        }
        catch (Common.Exceptions.PythonExecutionException ex)
        {
            return StepResult.Fail(StepId, "Variant calling failed", ex.Stderr);
        }
    }

    public VariantSummary? GetLatestVariantSummary(string patientId)
    {
        var latest = Files.FindLatestFile(patientId, StepId, "variants_*.summary.json");
        return latest is null ? null : Files.ReadJson<VariantSummary>(patientId, StepId, latest.Name);
    }

    private string? ResolveTumorBam(string patientId) =>
        Files.FindLatestFile(patientId, PipelineStepIds.Alignment, "tumor_*.bam") is { } f
            ? Path.Combine(Paths.GetStepDir(patientId, PipelineStepIds.Alignment), f.Name)
            : null;

    private string? ResolveNormalBam(string patientId) =>
        Files.FindLatestFile(patientId, PipelineStepIds.Alignment, "normal_*.bam") is { } f
            ? Path.Combine(Paths.GetStepDir(patientId, PipelineStepIds.Alignment), f.Name)
            : null;
}
