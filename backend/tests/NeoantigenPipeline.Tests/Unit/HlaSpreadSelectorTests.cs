using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Services._10_Ranking;

namespace NeoantigenPipeline.Tests.Unit;

public class HlaSpreadSelectorTests
{
    private static NeoantigenCandidate Candidate(string id, string allele, double score) => new()
    {
        CandidateId = id,
        HlaAllele = allele,
        FinalScore = score,
    };

    [Fact]
    public void SelectsExactlyTargetCount()
    {
        var candidates = Enumerable.Range(0, 50)
            .Select(i => Candidate($"c{i}", i % 3 == 0 ? "HLA-A*02:01" : "HLA-B*07:02", 1.0 - i * 0.01))
            .ToList();
        var selector = new HlaSpreadSelector(0.5, new List<string> { "HLA-A*02:01", "HLA-B*07:02" });

        var selected = selector.Select(candidates, 20);

        Assert.Equal(20, selected.Count);
    }

    [Fact]
    public void HighSpreadWeightIncludesMultipleAlleles()
    {
        // Top 30 by raw score all share one allele; lower-scoring candidates cover others.
        var candidates = new List<NeoantigenCandidate>();
        for (var i = 0; i < 30; i++)
            candidates.Add(Candidate($"top{i}", "HLA-A*02:01", 1.0 - i * 0.001));
        for (var i = 0; i < 20; i++)
            candidates.Add(Candidate($"other{i}", i % 2 == 0 ? "HLA-B*07:02" : "HLA-C*07:01", 0.5 - i * 0.001));

        var selector = new HlaSpreadSelector(2.0, new List<string> { "HLA-A*02:01", "HLA-B*07:02", "HLA-C*07:01" });
        var selected = selector.Select(candidates, 20);
        var coverage = selector.GetAlleleCoverage(selected);

        Assert.True(coverage.Keys.Count > 1, "Expected selection to span more than one allele under a high spread weight.");
    }

    [Fact]
    public void ZeroSpreadWeightSelectsPurelyByScore()
    {
        var candidates = Enumerable.Range(0, 20)
            .Select(i => Candidate($"c{i}", i % 2 == 0 ? "HLA-A*02:01" : "HLA-B*07:02", 1.0 - i * 0.01))
            .ToList();
        var selector = new HlaSpreadSelector(0.0, new List<string> { "HLA-A*02:01", "HLA-B*07:02" });

        var selected = selector.Select(new List<NeoantigenCandidate>(candidates), 10);
        var expectedIds = candidates.OrderByDescending(c => c.FinalScore).Take(10).Select(c => c.CandidateId).ToHashSet();

        Assert.Equal(expectedIds, selected.Select(c => c.CandidateId).ToHashSet());
    }

    [Fact]
    public void HandlesFewerCandidatesThanTargetCount()
    {
        var candidates = Enumerable.Range(0, 5).Select(i => Candidate($"c{i}", "HLA-A*02:01", 1.0 - i * 0.1)).ToList();
        var selector = new HlaSpreadSelector(0.5, new List<string> { "HLA-A*02:01" });

        var selected = selector.Select(candidates, 20);

        Assert.Equal(5, selected.Count);
    }

    [Fact]
    public void SingleAlleleCandidateSetDoesNotThrow()
    {
        var candidates = Enumerable.Range(0, 10).Select(i => Candidate($"c{i}", "HLA-A*02:01", 1.0 - i * 0.1)).ToList();
        var selector = new HlaSpreadSelector(1.0, new List<string> { "HLA-A*02:01" });

        var exception = Record.Exception(() => selector.Select(candidates, 5));

        Assert.Null(exception);
    }
}
