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

    public FilteringService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools,
        ImmunogenicityService immunogenicityService, ILogger<FilteringService> logger)
        : base(paths, files, python, tools, logger)
    {
        _immunogenicityService = immunogenicityService;
    }

    public override async Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var candidatesFile = RequireLatestFile(patientId, PipelineStepIds.Immunogenicity, "immunogenicity_*.tsv", "Immunogenicity-scored candidates");
        var outputTsv = Paths.BuildOutputPath(patientId, StepId, "filtered", ".tsv");
        var removedTsv = Paths.BuildOutputPath(patientId, StepId, "removed", ".tsv");

        var applyExpression = parameters.GetBool("applyExpressionFilter", RnaSeqAvailable(patientId));
        var useMini = parameters.GetBool("useMiniProteome", true);
        var expressionFile = Files.FindLatestFile(patientId, PipelineStepIds.Upload, "tumor_rna_expression_*.tsv");

        var args = new Dictionary<string, string>
        {
            ["candidates-tsv"] = candidatesFile,
            ["proteome-fasta"] = Paths.GetProteomeFasta(useMini),
            ["expression-tsv"] = applyExpression && expressionFile is not null
                ? Path.Combine(Paths.GetStepDir(patientId, PipelineStepIds.Upload), expressionFile.Name)
                : "",
            ["output-tsv"] = outputTsv,
            ["removed-tsv"] = removedTsv,
            ["min-tpm"] = parameters.GetDouble("minTpm", 1.0).ToString("F2"),
            ["kmer-size"] = "8",
        };

        try
        {
            var response = await Python.RunAndParseAsync("filter_candidates.py", args, new PythonExecutionOptions { TimeoutSeconds = 600, CancellationToken = ct });
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
