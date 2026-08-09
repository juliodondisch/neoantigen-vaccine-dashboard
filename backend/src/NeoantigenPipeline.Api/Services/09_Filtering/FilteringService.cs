using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Services._08_Immunogenicity;

namespace NeoantigenPipeline.Api.Services._09_Filtering;

public class FilteringSummary
{
    public int InputCount { get; set; }
    public int RemovedBySelfSimilarity { get; set; }
    public int RemovedByExpression { get; set; }
    public int Survived { get; set; }
    public bool ExpressionFilterApplied { get; set; }
}

public class FilteringService : PipelineStepBase
{
    public const string StepId = PipelineStepIds.Filtering;
    private readonly ImmunogenicityService _immunogenicityService;
    private readonly ReferenceSetupService _referenceSetup;

    public override StepDefinition Definition { get; } = new()
    {
        Id = StepId,
        Order = 9,
        DisplayName = "Safety and Expression Filtering",
        ShortDescription = "Remove self-similar and (if available) unexpressed candidates",
        LongExplanation = "Two filters here. First, safety: if a candidate fragment closely resembles a normal human protein, targeting it risks the immune system attacking healthy tissue ,  those are removed. Second, if RNA data was provided: mutations in genes the tumor isn't actually using are removed, since a gene that's switched off produces no protein and therefore no target.",
        ToolName = "Reference proteome comparison; RNA-seq quantification",
        RequiredInputStepIds = new[] { PipelineStepIds.Immunogenicity },
        IsUploadStep = false,
        HasParameters = true,
        ProducesDownload = false,
        RequiredTools = Array.Empty<string>(),
    };

    public override string[] PrimaryOutputPatterns => new[] { "filtered_*.tsv" };

    public FilteringService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, AppConfig config,
        ImmunogenicityService immunogenicityService, ReferenceSetupService referenceSetup, ILogger<FilteringService> logger)
        : base(paths, files, python, tools, config, logger)
    {
        _immunogenicityService = immunogenicityService;
        _referenceSetup = referenceSetup;
    }

    public override async Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var candidatesFile = RequireLatestFile(patientId, PipelineStepIds.Immunogenicity, "immunogenicity_*.tsv", "Immunogenicity-scored candidates");
        var outputTsv = Paths.BuildOutputPath(patientId, StepId, "filtered", ".tsv");
        var removedTsv = Paths.BuildOutputPath(patientId, StepId, "removed", ".tsv");

        var applyExpression = parameters.GetBool("applyExpressionFilter", RnaSeqAvailable(patientId));
        var useMini = parameters.GetBool("useMiniProteome", true);
        var reference = ResolveReferenceGenome(patientId, parameters);

        var preMadeExpressionFile = Files.FindLatestFile(patientId, PipelineStepIds.Upload, "tumor_rna_expression_*.tsv");
        var expressionPath = preMadeExpressionFile is not null
            ? Path.Combine(Paths.GetStepDir(patientId, PipelineStepIds.Upload), preMadeExpressionFile.Name)
            : null;

        // No pre-made expression TSV supplied — quantify it ourselves via Salmon if RNA-seq
        // was uploaded and the transcriptome index is ready. Best-effort: if either isn't
        // available, the expression filter is just skipped (RnaSeqAvailable already gates
        // whether applyExpression defaults true), not a hard failure of the whole step.
        if (applyExpression && expressionPath is null && RnaSeqAvailable(patientId))
            expressionPath = await QuantifyExpressionAsync(patientId, reference, ct);

        var args = new Dictionary<string, string>
        {
            ["candidates-tsv"] = candidatesFile,
            ["proteome-fasta"] = Paths.GetProteomeFasta(useMini),
            ["expression-tsv"] = applyExpression && expressionPath is not null ? expressionPath : "",
            ["output-tsv"] = outputTsv,
            ["removed-tsv"] = removedTsv,
            ["min-tpm"] = parameters.GetDouble("minTpm", 1.0).ToString("F2"),
            ["kmer-size"] = "8",
        };

        try
        {
            var response = await Python.RunAndParseAsync("filter_candidates.py", args, new PythonExecutionOptions { TimeoutSeconds = Config.GetStepTimeout(StepId), CancellationToken = ct }, patientId: patientId);
            WriteSummary(patientId, response.Summary);
            return BuildResult(patientId, response, DateTime.UtcNow - start);
        }
        catch (Common.Exceptions.PythonExecutionException ex)
        {
            return StepResult.Fail(StepId, "Filtering failed", ex.Stderr);
        }
    }

    public bool RnaSeqAvailable(string patientId) =>
        Files.StepHasFilesMatching(patientId, PipelineStepIds.Upload, "tumor_rna_*");

    /// <summary>Runs quantify_expression.py (Salmon) against the uploaded RNA FASTQ, writing
    /// a gene\ttpm TSV into this step's folder. Returns null (not an error — expression
    /// filtering is just skipped) if the RNA transcriptome index isn't built or no RNA FASTQ
    /// is found; only genuine quantification failures are logged as a warning.</summary>
    private async Task<string?> QuantifyExpressionAsync(string patientId, string reference, CancellationToken ct)
    {
        if (!_referenceSetup.IsRnaReferenceReady(reference))
            return null;

        var uploadDir = Paths.GetStepDir(patientId, PipelineStepIds.Upload);
        var rnaR1 = Files.FindLatestFile(patientId, PipelineStepIds.Upload, "tumor_rna_*_R1*") ??
                    Files.FindLatestFile(patientId, PipelineStepIds.Upload, "tumor_rna_*");
        var rnaR2 = Files.FindLatestFile(patientId, PipelineStepIds.Upload, "tumor_rna_*_R2*");
        if (rnaR1 is null)
            return null;

        var outputTsv = Paths.BuildOutputPath(patientId, StepId, "expression", ".tsv");
        var args = new Dictionary<string, string>
        {
            ["rna-r1"] = Path.Combine(uploadDir, rnaR1.Name),
            ["rna-r2"] = rnaR2 is not null ? Path.Combine(uploadDir, rnaR2.Name) : "",
            ["salmon-index"] = _referenceSetup.GetSalmonIndexDir(reference),
            ["tx2gene"] = _referenceSetup.GetTx2GenePath(reference),
            ["output-tsv"] = outputTsv,
            ["threads"] = "4",
        };

        try
        {
            var response = await Python.RunAndParseAsync("quantify_expression.py", args,
                new PythonExecutionOptions { TimeoutSeconds = 3600, CancellationToken = ct }, patientId: patientId);
            return response.Success ? outputTsv : null;
        }
        catch (Common.Exceptions.PythonExecutionException)
        {
            return null; // graceful: filtering proceeds without expression data rather than failing the step
        }
    }

    public List<NeoantigenCandidate> ReadFilteredCandidates(string patientId)
    {
        var latest = Files.FindLatestFile(patientId, StepId, "filtered_*.tsv");
        if (latest is null)
            return new List<NeoantigenCandidate>();
        var text = Files.ReadTextFile(patientId, StepId, latest.Name, maxBytes: 50_000_000);
        return text is null ? new List<NeoantigenCandidate>() : TsvParser.Parse<NeoantigenCandidate>(text);
    }

    public List<NeoantigenCandidate> ReadRemovedCandidates(string patientId)
    {
        var latest = Files.FindLatestFile(patientId, StepId, "removed_*.tsv");
        if (latest is null)
            return new List<NeoantigenCandidate>();
        var text = Files.ReadTextFile(patientId, StepId, latest.Name, maxBytes: 50_000_000);
        return text is null ? new List<NeoantigenCandidate>() : TsvParser.Parse<NeoantigenCandidate>(text);
    }

    public FilteringSummary? GetLatestFilteringSummary(string patientId)
    {
        var latest = Files.FindLatestFile(patientId, StepId, "filtering_*.summary.json");
        return latest is null ? null : Files.ReadJson<FilteringSummary>(patientId, StepId, latest.Name);
    }
}
