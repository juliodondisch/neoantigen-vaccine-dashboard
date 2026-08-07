using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Services._10_Ranking;

/// <summary>
/// Set-level diversity constraint — HLA spread cannot be scored on a single peptide in
/// isolation, so it's applied here as a greedy selection penalty rather than folded into
/// ScoreCalculator's additive weighted sum.
/// </summary>
public class HlaSpreadSelector
{
    private readonly double _spreadWeight;
    private readonly List<string> _availableAlleles;

    public HlaSpreadSelector(double spreadWeight, List<string> availableAlleles)
    {
        _spreadWeight = spreadWeight;
        _availableAlleles = availableAlleles;
    }

    public List<NeoantigenCandidate> Select(List<NeoantigenCandidate> scoredCandidates, int targetCount)
    {
        var remaining = new List<NeoantigenCandidate>(scoredCandidates);
        var selected = new List<NeoantigenCandidate>();
        var coverage = new Dictionary<string, int>();

        while (selected.Count < targetCount && remaining.Count > 0)
        {
            NeoantigenCandidate? best = null;
            double bestEffective = double.NegativeInfinity;

            foreach (var candidate in remaining)
            {
                var baseScore = candidate.FinalScore ?? 0;
                var penalty = ComputeDiversityPenalty(candidate.HlaAllele, coverage, selected.Count);
                var effective = baseScore - _spreadWeight * penalty;
                if (effective > bestEffective)
                {
                    bestEffective = effective;
                    best = candidate;
                }
            }

            if (best is null)
                break;

            best.IsSelected = true;
            selected.Add(best);
            remaining.Remove(best);
            coverage[best.HlaAllele] = coverage.GetValueOrDefault(best.HlaAllele, 0) + 1;
        }

        for (var i = 0; i < selected.Count; i++)
            selected[i].FinalRank = i + 1;

        return selected;
    }

    public Dictionary<string, int> GetAlleleCoverage(List<NeoantigenCandidate> selected)
    {
        var coverage = new Dictionary<string, int>();
        foreach (var c in selected)
            coverage[c.HlaAllele] = coverage.GetValueOrDefault(c.HlaAllele, 0) + 1;
        return coverage;
    }

    public double ComputeDiversityPenalty(string allele, Dictionary<string, int> currentCoverage, int totalSelected)
    {
        if (totalSelected == 0)
            return 0;
        var alleleCount = currentCoverage.GetValueOrDefault(allele, 0);
        // Penalty rises with how much of the selection so far already shares this allele.
        return (double)alleleCount / totalSelected;
    }

    private static double GiniCoefficient(IEnumerable<int> counts)
    {
        var values = counts.OrderBy(c => c).ToList();
        var n = values.Count;
        if (n == 0)
            return 0;
        var total = values.Sum();
        if (total == 0)
            return 0;

        double cumulative = 0;
        for (var i = 0; i < n; i++)
            cumulative += (i + 1) * values[i];

        return (2.0 * cumulative) / (n * total) - (n + 1.0) / n;
    }
}
