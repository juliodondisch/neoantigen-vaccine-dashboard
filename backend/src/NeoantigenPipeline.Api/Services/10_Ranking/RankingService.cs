using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Services._05_HlaTyping;
using NeoantigenPipeline.Api.Services._09_Filtering;

namespace NeoantigenPipeline.Api.Services._10_Ranking;

public class RankingService : PipelineStepBase
{
    public const string StepId = PipelineStepIds.Ranking;
    private readonly FilteringService _filteringService;
    private readonly HlaTypingService _hlaService;

    public override StepDefinition Definition { get; } = new()
    {
        Id = StepId,
        Order = 10,
        DisplayName = "Weighted Final Ranking",
        ShortDescription = "Combine binding, immunogenicity, clonality, and HLA spread into a final rank",
        LongExplanation = "The final ranking combines several signals, and you can control how much each one matters. Binding strength difference (agretopicity) measures how much more strongly the mutated fragment binds compared to its normal counterpart — a bigger gap means it looks more foreign. Expression is how actively the gene is used. Clonality is what fraction of tumor cells carry the mutation — targeting a mutation present in every cell is safer than one present in only some. HLA spread means deliberately choosing targets across different HLA types, so the tumor can't escape by losing just one.",
        ToolName = "Custom scoring logic (C#)",
        RequiredInputStepIds = new[] { PipelineStepIds.Filtering },
        IsUploadStep = false,
        HasParameters = true,
        ProducesDownload = false,
        RequiredTools = Array.Empty<string>(),
    };

    public RankingService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools,
        FilteringService filteringService, HlaTypingService hlaService, ILogger<RankingService> logger)
        : base(paths, files, python, tools, logger)
    {
        _filteringService = filteringService;
        _hlaService = hlaService;
    }

    public override Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var weights = RankingWeights.FromParameters(parameters);
        var targetCount = parameters.GetInt("targetCount", 30);

        var candidates = _filteringService.ReadFilteredCandidates(patientId);
        if (candidates.Count == 0)
            return Task.FromResult(StepResult.Fail(StepId, "No filtered candidates available", "Run filtering (step 09) first."));

        var calculator = new ScoreCalculator(weights);
        var scored = calculator.ScoreAll(candidates);

        var rankedByScore = scored.OrderByDescending(c => c.FinalScore).ToList();
        for (var i = 0; i < rankedByScore.Count; i++)
            rankedByScore[i].FinalRank = i + 1;

        var alleles = _hlaService.GetHlaProfile(patientId)?.ClassIAlleles ?? new List<string>();
        var selector = new HlaSpreadSelector(weights.HlaSpread, alleles);
        var selected = selector.Select(new List<NeoantigenCandidate>(rankedByScore), Math.Min(targetCount, rankedByScore.Count));
        var selectedIds = selected.Select(c => c.CandidateId).ToHashSet();
        foreach (var c in rankedByScore)
            c.IsSelected = selectedIds.Contains(c.CandidateId);

        var rankedPath = Paths.BuildOutputPath(patientId, StepId, "ranked", ".tsv");
        var selectedPath = Paths.BuildOutputPath(patientId, StepId, "selected", ".tsv");
        var weightsPath = Paths.BuildOutputPath(patientId, StepId, "weights", ".json");

        TsvParser.Write(rankedPath, rankedByScore);
        TsvParser.Write(selectedPath, selected);
        Files.WriteJson(patientId, StepId, Path.GetFileName(weightsPath), weights);

        var summary = new Dictionary<string, object>
        {
            ["rankedCount"] = rankedByScore.Count,
            ["selectedCount"] = selected.Count,
            ["alleleCoverage"] = selector.GetAlleleCoverage(selected),
        };
        WriteSummary(patientId, summary);

        var result = StepResult.Ok(StepId, $"Ranked {rankedByScore.Count} candidates, selected top {selected.Count}",
            GetOutputFiles(patientId), summary, DateTime.UtcNow - start);
        return Task.FromResult(result);
    }

    public List<NeoantigenCandidate> Preview(string patientId, RankingWeights weights, int targetCount)
    {
        var candidates = _filteringService.ReadFilteredCandidates(patientId);
        if (candidates.Count == 0)
            return new List<NeoantigenCandidate>();

        var calculator = new ScoreCalculator(weights);
        var scored = calculator.ScoreAll(candidates);
        var rankedByScore = scored.OrderByDescending(c => c.FinalScore).ToList();
        for (var i = 0; i < rankedByScore.Count; i++)
            rankedByScore[i].FinalRank = i + 1;

        var alleles = _hlaService.GetHlaProfile(patientId)?.ClassIAlleles ?? new List<string>();
        var selector = new HlaSpreadSelector(weights.HlaSpread, alleles);
        var selected = selector.Select(new List<NeoantigenCandidate>(rankedByScore), Math.Min(targetCount, rankedByScore.Count));
        var selectedIds = selected.Select(c => c.CandidateId).ToHashSet();
        foreach (var c in rankedByScore)
            c.IsSelected = selectedIds.Contains(c.CandidateId);

        return rankedByScore;
    }

    public List<NeoantigenCandidate> ReadRankedCandidates(string patientId)
    {
        var latest = Files.FindLatestFile(patientId, StepId, "ranked_*.tsv");
        if (latest is null)
            return new List<NeoantigenCandidate>();
        var text = Files.ReadTextFile(patientId, StepId, latest.Name, maxBytes: 50_000_000);
        return text is null ? new List<NeoantigenCandidate>() : TsvParser.Parse<NeoantigenCandidate>(text);
    }

    public List<NeoantigenCandidate> ReadSelectedCandidates(string patientId) =>
        ReadRankedCandidates(patientId).Where(c => c.IsSelected).ToList();

    public RankingWeights? GetLastUsedWeights(string patientId)
    {
        var latest = Files.FindLatestFile(patientId, StepId, "weights_*.json");
        return latest is null ? null : Files.ReadJson<RankingWeights>(patientId, StepId, latest.Name);
    }
}
