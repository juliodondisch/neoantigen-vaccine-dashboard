using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Services._07_Presentation;

namespace NeoantigenPipeline.Api.Services._08_Immunogenicity;

public class ImmunogenicityService : PipelineStepBase
{
    public const string StepId = PipelineStepIds.Immunogenicity;
    private readonly PresentationService _presentationService;

    public override StepDefinition Definition { get; } = new()
    {
        Id = StepId,
        Order = 8,
        DisplayName = "Predict Immunogenicity",
        ShortDescription = "Score which displayed candidates will actually provoke a T cell response",
        LongExplanation = "Being displayed isn't the same as being noticed. Most displayed fragments never provoke an immune response. This step predicts which ones will actually attract T cells ,  and it's the least reliable part of the whole pipeline. Current tools score only modestly better than chance, and this is an open research problem across the entire field, not a limitation of this app specifically.",
        ToolName = "BigMHC-IM (or PRIME / PepFore)",
        RequiredInputStepIds = new[] { PipelineStepIds.Presentation },
        IsUploadStep = false,
        HasParameters = true,
        ProducesDownload = false,
        RequiredTools = Array.Empty<string>(),
    };

    public ImmunogenicityService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools,
        PresentationService presentationService, ILogger<ImmunogenicityService> logger)
        : base(paths, files, python, tools, logger)
    {
        _presentationService = presentationService;
    }

    public override async Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var presentationFile = RequireLatestFile(patientId, PipelineStepIds.Presentation, "presentation_*.tsv", "Presentation-scored candidates");
        var outputTsv = Paths.BuildOutputPath(patientId, StepId, "immunogenicity", ".tsv");
        var predictor = parameters.GetString("predictor", "stub") ?? "stub";
        var useGpu = parameters.GetBool("useGpu", false);
        var useStub = predictor == "stub" || !Tools.IsAvailable(predictor);

        var args = new Dictionary<string, string>
        {
            ["candidates-tsv"] = presentationFile,
            ["output-tsv"] = outputTsv,
            ["predictor"] = useStub ? "stub" : predictor,
            ["use-gpu"] = useGpu ? "true" : "false",
            ["use-stub"] = useStub ? "true" : "false",
        };

        try
        {
            var response = await Python.RunAndParseAsync("predict_immunogenicity.py", args, new PythonExecutionOptions { TimeoutSeconds = 600, CancellationToken = ct });
            WriteSummary(patientId, response.Summary);
            return BuildResult(patientId, response, DateTime.UtcNow - start);
        }
        catch (Common.Exceptions.PythonExecutionException ex)
        {
            return StepResult.Fail(StepId, "Immunogenicity prediction failed", ex.Stderr);
        }
    }

    public List<NeoantigenCandidate> ReadScoredCandidates(string patientId)
    {
        var latest = Files.FindLatestFile(patientId, StepId, "immunogenicity_*.tsv");
        if (latest is null)
            return new List<NeoantigenCandidate>();
        var text = Files.ReadTextFile(patientId, StepId, latest.Name, maxBytes: 50_000_000);
        return text is null ? new List<NeoantigenCandidate>() : TsvParser.Parse<NeoantigenCandidate>(text);
    }
}
