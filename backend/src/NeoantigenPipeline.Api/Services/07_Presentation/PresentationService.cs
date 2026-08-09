using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Services._06_CandidateGeneration;

namespace NeoantigenPipeline.Api.Services._07_Presentation;

public class PresentationService : PipelineStepBase
{
    public const string StepId = PipelineStepIds.Presentation;
    private readonly CandidateGenerationService _candidateService;

    public override StepDefinition Definition { get; } = new()
    {
        Id = StepId,
        Order = 7,
        DisplayName = "Predict HLA Presentation",
        ShortDescription = "Score which candidates will be displayed on the patient's HLA",
        LongExplanation = "This predicts which candidate fragments will actually be displayed on this patient's HLA molecules. A fragment that can't physically fit the display case will never be seen by the immune system, no matter how foreign it looks. Roughly half to three-quarters of the top-ranked predictions here turn out to be genuinely displayed.",
        ToolName = "MHCflurry 2.0 (optional BigMHC-EL second opinion)",
        RequiredInputStepIds = new[] { PipelineStepIds.Candidates },
        IsUploadStep = false,
        HasParameters = true,
        ProducesDownload = false,
        // No hard tool requirement: predict_stub() is a specified fallback used automatically
        // when mhcflurry isn't installed, per CLAUDE.md's "stubs are a specified feature" rule.
        RequiredTools = Array.Empty<string>(),
    };

    public override string[] PrimaryOutputPatterns => new[] { "presentation_*.tsv" };

    public PresentationService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, AppConfig config,
        CandidateGenerationService candidateService, ILogger<PresentationService> logger)
        : base(paths, files, python, tools, config, logger)
    {
        _candidateService = candidateService;
    }

    public override async Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var candidatesFile = RequireLatestFile(patientId, PipelineStepIds.Candidates, "candidates_*.tsv", "Candidate peptide list");
        var outputTsv = Paths.BuildOutputPath(patientId, StepId, "presentation", ".tsv");
        var predictorName = ResolvePredictorName(parameters);
        var useStub = predictorName == "stub" || !Tools.IsAvailable("mhcflurry");

        var args = new Dictionary<string, string>
        {
            ["candidates-tsv"] = candidatesFile,
            ["output-tsv"] = outputTsv,
            ["predictor"] = useStub ? "stub" : predictorName,
            ["batch-size"] = "512",
            ["use-stub"] = useStub ? "true" : "false",
        };

        try
        {
            var response = await Python.RunAndParseAsync("predict_presentation.py", args, new PythonExecutionOptions { TimeoutSeconds = Config.GetStepTimeout(StepId), CancellationToken = ct }, patientId: patientId);
            if (useStub)
                response.Message = (response.Message ?? "Completed") + " (stub predictor ,  mhcflurry not installed)";
            WriteSummary(patientId, response.Summary);
            return BuildResult(patientId, response, DateTime.UtcNow - start);
        }
        catch (Common.Exceptions.PythonExecutionException ex)
        {
            return StepResult.Fail(StepId, "Presentation prediction failed", ex.Stderr);
        }
    }

    public List<NeoantigenCandidate> ReadScoredCandidates(string patientId)
    {
        var latest = Files.FindLatestFile(patientId, StepId, "presentation_*.tsv");
        if (latest is null)
            return new List<NeoantigenCandidate>();
        var text = Files.ReadTextFile(patientId, StepId, latest.Name, maxBytes: 50_000_000);
        return text is null ? new List<NeoantigenCandidate>() : TsvParser.Parse<NeoantigenCandidate>(text);
    }

    private static string ResolvePredictorName(StepParameters parameters) => parameters.GetString("predictor", "mhcflurry") ?? "mhcflurry";
}
