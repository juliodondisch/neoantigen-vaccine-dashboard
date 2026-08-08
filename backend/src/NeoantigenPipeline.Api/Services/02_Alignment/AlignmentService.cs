using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Services._01_Upload;

namespace NeoantigenPipeline.Api.Services._02_Alignment;

public class AlignmentService : PipelineStepBase
{
    public const string StepId = PipelineStepIds.Alignment;
    private readonly UploadService _uploadService;
    private readonly ReferenceSetupService _referenceSetup;
    private readonly BamValidationService _bamValidation;

    public override StepDefinition Definition { get; } = new()
    {
        Id = StepId,
        Order = 2,
        DisplayName = "Align to Reference Genome",
        ShortDescription = "Align raw sequencing reads to the reference genome",
        LongExplanation = "Sequencing machines don't read DNA in order ,  they shatter it into millions of short fragments and read those. Alignment figures out where each fragment belongs on the human genome, like matching puzzle pieces to the picture on the box. If you uploaded BAM files, this step is already done and can be skipped.",
        ToolName = "bwa-mem2 (DNA), STAR (RNA)",
        RequiredInputStepIds = new[] { PipelineStepIds.Upload },
        IsUploadStep = false,
        HasParameters = true,
        ProducesDownload = false,
        RequiredTools = new[] { "bwa-mem2", "samtools" },
    };

    public AlignmentService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools,
        UploadService uploadService, ReferenceSetupService referenceSetup, BamValidationService bamValidation, ILogger<AlignmentService> logger)
        : base(paths, files, python, tools, logger)
    {
        _uploadService = uploadService;
        _referenceSetup = referenceSetup;
        _bamValidation = bamValidation;
    }

    public override ValidationResult ValidateInputs(string patientId)
    {
        // BAMs uploaded directly into this step's own folder (e.g. test data that already
        // arrives aligned) need neither step 1's output nor bwa-mem2/samtools.
        if (HasOwnBams(patientId))
            return ValidationResult.Valid();

        var result = ValidateRequiredSteps(patientId);
        if (CanSkip(patientId))
            return ValidationResult.Valid(); // BAMs already provided via step 1; tools not needed
        foreach (var missing in Tools.GetMissingTools(Definition.RequiredTools))
            result.AddMissingTool(missing);

        // Fail fast rather than starting a job doomed to run out of disk partway through
        // a multi-GB reference download; if there's room, just warn — Run will fetch it.
        const string defaultGenome = "chr21_test";
        if (!_referenceSetup.IsReady(defaultGenome))
        {
            var blocker = _referenceSetup.DescribeBlocker(defaultGenome);
            if (blocker is not null)
                result.AddError(blocker);
            else
                result.AddWarning($"Reference genome '{defaultGenome}' will be downloaded and indexed automatically on first run.");
        }
        return result;
    }

    public override async Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (HasOwnBams(patientId))
        {
            var ownBamsError = await ValidateBamsInPlaceAsync(patientId, StepId, ct);
            if (ownBamsError is not null)
                return StepResult.Fail(StepId, "Uploaded BAM failed validation", ownBamsError);
            return AlreadyHasBams(patientId, start);
        }

        if (CanSkip(patientId))
            return await PassThroughBamsAsync(patientId, ct);

        var dryRun = parameters.GetBool("dryRun", false);
        var threads = parameters.GetInt("threads", 4);
        var reference = parameters.GetString("referenceGenome") ?? "chr21_test";
        var needsRna = _uploadService.HasRnaSeq(patientId);

        // Dry-run never touches the reference (align.py's dry_run_stub short-circuits before
        // any file check), so only fetch/build the real thing when actually aligning.
        if (!dryRun)
        {
            var (ready, refError) = await _referenceSetup.EnsureReferenceAsync(reference, needsRna, patientId, ct);
            if (!ready)
                return StepResult.Fail(StepId, $"Reference genome '{reference}' is not ready", refError);
        }

        var results = new List<PythonResponse>();

        foreach (var sampleType in new[] { "tumor", "normal" })
        {
            var response = await AlignSampleAsync(patientId, sampleType, isRna: false, parameters, ct);
            results.Add(response);
        }

        if (needsRna)
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

    /// <summary>True when BAMs were uploaded straight into 02_alignment itself
    /// (via AlignmentPanel's own upload zone), bypassing step 1 entirely.</summary>
    public bool HasOwnBams(string patientId) =>
        Files.StepHasFilesMatching(patientId, StepId, "tumor_*.bam") &&
        Files.StepHasFilesMatching(patientId, StepId, "normal_*.bam");

    private StepResult AlreadyHasBams(string patientId, DateTime start)
    {
        var summary = new Dictionary<string, object> { ["skippedAlignment"] = true, ["reason"] = "BAMs uploaded directly to this step" };
        WriteSummary(patientId, summary);
        return StepResult.Ok(StepId, "BAM inputs already present ,  nothing to align", GetOutputFiles(patientId), summary, DateTime.UtcNow - start);
    }

    public async Task<StepResult> PassThroughBamsAsync(string patientId, CancellationToken ct = default)
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

        var validationError = await ValidateBamsInPlaceAsync(patientId, StepId, ct);
        if (validationError is not null)
            return StepResult.Fail(StepId, "Uploaded BAM failed validation", validationError);

        var summary = new Dictionary<string, object> { ["skippedAlignment"] = true, ["reason"] = "Inputs already aligned (BAM upload)" };
        WriteSummary(patientId, summary);
        return StepResult.Ok(StepId, "Inputs were already aligned; BAMs passed through", GetOutputFiles(patientId), summary, DateTime.UtcNow - start);
    }

    /// <summary>Runs validate_bam.py against every tumor_*/normal_*.bam currently in this
    /// step's folder, replacing any that needed a fix (bad @RG SM:, wrong sort order, missing
    /// index) with the repaired copy. Returns an error message if a BAM is unfixably broken,
    /// or null if everything is valid (with or without in-place fixes).</summary>
    private async Task<string?> ValidateBamsInPlaceAsync(string patientId, string stepId, CancellationToken ct)
    {
        var dir = Paths.GetStepDir(patientId, stepId);
        foreach (var (glob, sampleName) in new[] { ("tumor_*.bam", "tumor"), ("normal_*.bam", "normal") })
        {
            var bam = Files.FindLatestFile(patientId, stepId, glob);
            if (bam is null)
                continue;

            var bamPath = Path.Combine(dir, bam.Name);
            var outcome = await _bamValidation.ValidateAndFixAsync(bamPath, sampleName, dir, patientId, ct);
            if (!outcome.Success)
                return $"'{bam.Name}': {outcome.Error}";
        }
        return null;
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
            return await Python.RunAndParseAsync("align.py", args, new PythonExecutionOptions { TimeoutSeconds = 7200, CancellationToken = ct }, patientId: patientId);
        }
        catch (Common.Exceptions.PythonExecutionException ex)
        {
            return new PythonResponse { Success = false, Error = ex.Stderr };
        }
    }
}
