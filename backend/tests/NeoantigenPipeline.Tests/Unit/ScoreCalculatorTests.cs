using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Services._10_Ranking;

namespace NeoantigenPipeline.Tests.Unit;

public class ScoreCalculatorTests
{
    private static List<NeoantigenCandidate> BuildCandidates()
    {
        var rng = new Random(7);
        var list = new List<NeoantigenCandidate>();
        for (var i = 0; i < 20; i++)
        {
            list.Add(new NeoantigenCandidate
            {
                CandidateId = $"c{i}",
                HlaAllele = i % 2 == 0 ? "HLA-A*02:01" : "HLA-B*07:02",
                PresentationScore = rng.NextDouble(),
                ImmunogenicityScore = rng.NextDouble(),
                Agretopicity = rng.NextDouble(),
                ExpressionTpm = rng.NextDouble() * 50,
                Vaf = rng.NextDouble(),
            });
        }
        return list;
    }

    [Fact]
    public void SingleWeightOfOneMatchesSortByThatCriterion()
    {
        var candidates = BuildCandidates();
        var weights = new RankingWeights { Presentation = 0, Immunogenicity = 0, Agretopicity = 1, Expression = 0, Clonality = 0, HlaSpread = 0 };
        var calculator = new ScoreCalculator(weights);

        var scored = calculator.ScoreAll(new List<NeoantigenCandidate>(candidates));
        var byScore = scored.OrderByDescending(c => c.FinalScore).Select(c => c.CandidateId).ToList();
        var byAgretopicity = candidates.OrderByDescending(c => c.Agretopicity).Select(c => c.CandidateId).ToList();

        Assert.Equal(byAgretopicity, byScore);
    }

    [Fact]
    public void AllZeroWeightsProducesStableOrder()
    {
        var candidates = BuildCandidates();
        var weights = new RankingWeights { Presentation = 0, Immunogenicity = 0, Agretopicity = 0, Expression = 0, Clonality = 0, HlaSpread = 0 };
        var calculator = new ScoreCalculator(weights);

        var exception = Record.Exception(() => calculator.ScoreAll(candidates));

        Assert.Null(exception);
        Assert.All(candidates, c => Assert.Equal(0, c.FinalScore));
    }

    [Fact]
    public void ChangingWeightReordersResults()
    {
        var candidates = BuildCandidates();
        var calcA = new ScoreCalculator(new RankingWeights { Presentation = 1, Immunogenicity = 0, Agretopicity = 0, Expression = 0, Clonality = 0, HlaSpread = 0 });
        var calcB = new ScoreCalculator(new RankingWeights { Presentation = 0, Immunogenicity = 0, Agretopicity = 0, Expression = 0, Clonality = 1, HlaSpread = 0 });

        var orderA = calcA.ScoreAll(candidates.Select(Clone).ToList()).OrderByDescending(c => c.FinalScore).Select(c => c.CandidateId).ToList();
        var orderB = calcB.ScoreAll(candidates.Select(Clone).ToList()).OrderByDescending(c => c.FinalScore).Select(c => c.CandidateId).ToList();

        Assert.NotEqual(orderA, orderB);
    }

    [Fact]
    public void HandlesNullScoresWithoutThrowing()
    {
        var candidates = new List<NeoantigenCandidate>
        {
            new() { CandidateId = "a", HlaAllele = "HLA-A*02:01" },
            new() { CandidateId = "b", HlaAllele = "HLA-A*02:01", PresentationScore = 0.9, ImmunogenicityScore = 0.5, Vaf = 0.3 },
        };
        var calculator = new ScoreCalculator(RankingWeights.Default());

        var exception = Record.Exception(() => calculator.ScoreAll(candidates));

        Assert.Null(exception);
        Assert.All(candidates, c => Assert.NotNull(c.FinalScore));
    }

    [Fact]
    public void NormalizationHandlesIdenticalValues()
    {
        var candidates = Enumerable.Range(0, 5).Select(i => new NeoantigenCandidate
        {
            CandidateId = $"c{i}",
            HlaAllele = "HLA-A*02:01",
            PresentationScore = 0.5,
            ImmunogenicityScore = 0.5,
            Vaf = 0.5,
        }).ToList();
        var calculator = new ScoreCalculator(RankingWeights.Default());

        var exception = Record.Exception(() => calculator.ScoreAll(candidates));

        Assert.Null(exception);
        var distinctScores = candidates.Select(c => c.FinalScore).Distinct().ToList();
        Assert.Single(distinctScores);
    }

    private static NeoantigenCandidate Clone(NeoantigenCandidate c) => new()
    {
        CandidateId = c.CandidateId,
        HlaAllele = c.HlaAllele,
        PresentationScore = c.PresentationScore,
        ImmunogenicityScore = c.ImmunogenicityScore,
        Agretopicity = c.Agretopicity,
        ExpressionTpm = c.ExpressionTpm,
        Vaf = c.Vaf,
    };
}
