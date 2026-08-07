using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Services._01_Upload;

namespace NeoantigenPipeline.Api.Services._02_Alignment;

public class AlignmentService : PipelineStepBase
{
    public const string StepId = PipelineStepIds.Alignment;
    private readonly UploadService _uploadService;

    public override StepDefinition Definition { get; } = new()
    {
        Id = StepId,
        Order = 2,
        DisplayName = "Align to Reference Genome",
        ShortDescription = "Align raw sequencing reads to the reference genome",
        LongExplanation = "Sequencing machines don't read DNA in order — they shatter it into millions of short fragments and read those. Alignment figures out where each fragment belongs on the human genome, like matching puzzle pieces to the picture on the box. If you uploaded BAM files, this step is already done and can be skipped.",
        ToolName = "bwa-mem2 (DNA), STAR (RNA)",
        RequiredInputStepIds = new[] { PipelineStepIds.Upload },
        IsUploadStep = false,
        HasParameters = true,
        ProducesDownload = false,
        RequiredTools = new[] { "bwa-mem2", "samtools" },
    };

    public AlignmentService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, UploadService uploadService, ILogger<AlignmentService> logger)
        : base(paths, files, python, tools, logger)
    {
        _uploadService = uploadService;
    }

    public override ValidationResult ValidateInputs(string patientId)
    {
        var result = ValidateRequiredSteps(patientId);
        if (CanSkip(patientId))
            return ValidationResult.Valid(); // BAMs already provided; tools not needed
        foreach (var missing in Tools.GetMissingTools(Definition.RequiredTools))
            result.AddMissingTool(missing);
        return result;
    }

    public override async Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (CanSkip(patientId))
            return await PassThroughBamsAsync(patientId);

        var dryRun = parameters.GetBool("dryRun", false);
        var threads = parameters.GetInt("threads", 4);
        var results = new List<PythonResponse>();

        foreach (var sampleType in new[] { "tumor", "normal" })
        {
            var response = await AlignSampleAsync(patientId, sampleType, isRna: false, parameters, ct);
            results.Add(response);
        }

        if (_uploadService.HasRnaSeq(patientId))
        {
            results.Add(await AlignSampleAsync(patientId, "rna", isRna: true, parameters, ct));
        }

        var duration = DateTime.UtcNow - start;
        var failed = results.FirstOrDefault(r => !r.Success);
        if (failed is not null)
            return StepResult.Fail(StepId, "Alignment failed", failed.Error);

        var summary = new Dictionary<string, object>
        {
            ["samplesAligned"] = results.Count,
            ["dryRun"] = dryRun,
            ["threads"] = threads,
        };
        WriteSummary(patientId, summary);
        return StepResult.Ok(StepId, $"Aligned {results.Count} sample(s)", GetOutputFiles(patientId), summary, duration);
    }

    public bool CanSkip(string patientId) => _uploadService.InputsAreBam(patientId);

    public Task<StepResult> PassThroughBamsAsync(string patientId)
    {
        var start = DateTime.UtcNow;
        var uploaded = Files.ListStepFiles(patientId, PipelineStepIds.Upload)
            .Where(f => f.Extension.Equals(".bam", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var bam in uploaded)
        {
            var src = Path.Combine(Paths.GetStepDir(patientId, PipelineStepIds.Upload), bam.Name);
            var dest = Paths.BuildOutputPath(patientId, StepId, Path.GetFileNameWithoutExtension(bam.Name), ".bam");
            File.Copy(src, dest);
        }

        var summary = new Dictionary<string, object> { ["skippedAlignment"] = true, ["reason"] = "Inputs already aligned (BAM upload)" };
        WriteSummary(patientId, summary);
        var result = StepResult.Ok(StepId, "Inputs were already aligned; BAMs passed through", GetOutputFiles(patientId), summary, DateTime.UtcNow - start);
        return Task.FromResult(result);
    }

    private Dictionary<string, string> BuildPythonArgs(string patientId, string sampleType, StepParameters parameters)
    {
        var reference = parameters.GetString("referenceGenome") ?? "chr21_test";
        var outputBam = Paths.BuildOutputPath(patientId, StepId, sampleType, ".bam");
        var uploadDir = Paths.GetStepDir(patientId, PipelineStepIds.Upload);

        var r1 = Files.FindLatestFile(patientId, PipelineStepIds.Upload, $"{sampleType}_dna_*_R1*") ??
                 Files.FindLatestFile(patientId, PipelineStepIds.Upload, $"{sampleType}_dna_*");
        var r2 = Files.FindLatestFile(patientId, PipelineStepIds.Upload, $"{sampleType}_dna_*_R2*");

        return new Dictionary<string, string>
        {
            ["fastq-r1"] = r1 is not null ? Path.Combine(uploadDir, r1.Name) : "",
            ["fastq-r2"] = r2 is not null ? Path.Combine(uploadDir, r2.Name) : "",
            ["reference"] = Paths.GetReferenceFasta(reference),
            ["output-bam"] = outputBam,
            ["threads"] = parameters.GetInt("threads", 4).ToString(),
            ["sample-name"] = sampleType,
            ["dry-run"] = parameters.GetBool("dryRun", false) ? "true" : "false",
        };
    }

    private async Task<PythonResponse> AlignSampleAsync(string patientId, string sampleType, bool isRna, StepParameters parameters, CancellationToken ct)
    {
        var args = BuildPythonArgs(patientId, sampleType, parameters);
        if (isRna) args["rna"] = "true";
        try
        {
            return await Python.RunAndParseAsync("align.py", args, new PythonExecutionOptions { TimeoutSeconds = 7200, CancellationToken = ct });
        }
        catch (Common.Exceptions.PythonExecutionException ex)
        {
            return new PythonResponse { Success = false, Error = ex.Stderr };
        }
    }
}
