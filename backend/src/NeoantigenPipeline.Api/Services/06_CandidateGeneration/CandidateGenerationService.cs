using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Services._04_ProteinEffects;
using NeoantigenPipeline.Api.Services._05_HlaTyping;

namespace NeoantigenPipeline.Api.Services._06_CandidateGeneration;

public class CandidateGenerationService : PipelineStepBase
{
    public const string StepId = PipelineStepIds.Candidates;
    private readonly ProteinEffectsService _effectsService;
    private readonly HlaTypingService _hlaService;
    private readonly SlidingWindowGenerator _generator;

    public override StepDefinition Definition { get; } = new()
    {
        Id = StepId,
        Order = 6,
        DisplayName = "Generate Candidate Peptides",
        ShortDescription = "Slide a window across each mutation to build candidate fragments",
        LongExplanation = "HLA display cases only hold short fragments ,  around 8 to 11 amino acids. Since we can't know exactly where the cell's internal machinery will cut a protein, this step generates every plausible short fragment containing each mutation, sliding a window across the mutated position. It also generates the matching unmutated version of each fragment, which is needed later to check how different the mutant version really looks to the immune system.",
        ToolName = "pVACtools (windowing + wild-type pairing; reimplemented natively in C#)",
        RequiredInputStepIds = new[] { PipelineStepIds.ProteinEffects, PipelineStepIds.HlaTyping },
        IsUploadStep = false,
        HasParameters = true,
        ProducesDownload = false,
        RequiredTools = Array.Empty<string>(),
    };

    public override string[] PrimaryOutputPatterns => new[] { "candidates_*.tsv" };

    public CandidateGenerationService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, AppConfig config,
        ProteinEffectsService effectsService, HlaTypingService hlaService, ILogger<CandidateGenerationService> logger)
        : base(paths, files, python, tools, config, logger)
    {
        _effectsService = effectsService;
        _hlaService = hlaService;
        _generator = new SlidingWindowGenerator();
    }

    public override async Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var minLength = parameters.GetInt("minLength", 8);
        var maxLength = parameters.GetInt("maxLength", 11);
        var generator = new SlidingWindowGenerator(minLength, maxLength);

        var variants = _effectsService.ReadProteinAlteringVariants(patientId);
        var hlaProfile = _hlaService.GetHlaProfile(patientId);
        if (hlaProfile is null || hlaProfile.ClassIAlleles.Count == 0)
            return StepResult.Fail(StepId, "No HLA profile available", "Run HLA typing (or supply a manual override) before generating candidates.");

        var allPairs = new List<PeptidePair>();
        foreach (var variant in variants)
            allPairs.AddRange(generator.GeneratePairs(variant));

        var candidates = ExpandAcrossAlleles(allPairs, hlaProfile.ClassIAlleles);
        var outputPath = Paths.BuildOutputPath(patientId, StepId, "candidates", ".tsv");
        WriteCandidatesTsv(patientId, candidates, outputPath);

        var summary = new Dictionary<string, object>
        {
            ["mutationCount"] = variants.Count,
            ["peptidePairCount"] = allPairs.Count,
            ["candidateCount"] = candidates.Count,
            ["hlaAlleleCount"] = hlaProfile.ClassIAlleles.Count,
        };
        WriteSummary(patientId, summary);
        await Task.CompletedTask;
        return StepResult.Ok(StepId, $"Generated {candidates.Count} candidates from {variants.Count} mutation(s)",
            GetOutputFiles(patientId), summary, DateTime.UtcNow - start);
    }

    public List<NeoantigenCandidate> ReadCandidates(string patientId)
    {
        var latest = Files.FindLatestFile(patientId, StepId, "candidates_*.tsv");
        if (latest is null)
            return new List<NeoantigenCandidate>();
        var text = Files.ReadTextFile(patientId, StepId, latest.Name, maxBytes: 50_000_000);
        return text is null ? new List<NeoantigenCandidate>() : TsvParser.Parse<NeoantigenCandidate>(text);
    }

    public int CountCandidates(string patientId) => ReadCandidates(patientId).Count;

    private List<NeoantigenCandidate> ExpandAcrossAlleles(List<PeptidePair> pairs, List<string> alleles)
    {
        var candidates = new List<NeoantigenCandidate>();
        foreach (var pair in pairs)
        {
            foreach (var allele in alleles)
            {
                candidates.Add(new NeoantigenCandidate
                {
                    CandidateId = Guid.NewGuid().ToString("N")[..12],
                    MutantPeptide = pair.MutantPeptide,
                    WildTypePeptide = pair.WildTypePeptide,
                    HlaAllele = allele,
                    PeptideLength = pair.Length,
                    GeneSymbol = pair.GeneSymbol,
                    TranscriptId = pair.TranscriptId,
                    SourceVariantId = pair.SourceVariantId,
                    Position = pair.ProteinPosition,
                    MutationOffsetInPeptide = pair.MutationOffsetInPeptide,
                    Vaf = pair.Vaf,
                });
            }
        }
        return candidates;
    }

    private void WriteCandidatesTsv(string patientId, List<NeoantigenCandidate> candidates, string outputPath) =>
        TsvParser.Write(outputPath, candidates, new[]
        {
            nameof(NeoantigenCandidate.CandidateId), nameof(NeoantigenCandidate.MutantPeptide), nameof(NeoantigenCandidate.WildTypePeptide),
            nameof(NeoantigenCandidate.HlaAllele), nameof(NeoantigenCandidate.PeptideLength), nameof(NeoantigenCandidate.GeneSymbol),
            nameof(NeoantigenCandidate.TranscriptId), nameof(NeoantigenCandidate.SourceVariantId), nameof(NeoantigenCandidate.Position),
            nameof(NeoantigenCandidate.MutationOffsetInPeptide), nameof(NeoantigenCandidate.Vaf),
        });
}
