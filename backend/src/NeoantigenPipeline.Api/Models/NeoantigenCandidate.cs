namespace NeoantigenPipeline.Api.Models;

/// <summary>The central data object flowing through steps 6-11.</summary>
public class NeoantigenCandidate
{
    // Identity
    public string CandidateId { get; set; } = "";
    public string MutantPeptide { get; set; } = "";
    public string WildTypePeptide { get; set; } = "";
    public string HlaAllele { get; set; } = "";
    public int PeptideLength { get; set; }

    // Provenance
    public string GeneSymbol { get; set; } = "";
    public string TranscriptId { get; set; } = "";
    public string SourceVariantId { get; set; } = "";
    public string Chromosome { get; set; } = "";
    public int Position { get; set; }
    public string Consequence { get; set; } = "";
    public int MutationOffsetInPeptide { get; set; }

    // Step 7
    public double? PresentationScore { get; set; }
    public double? PresentationPercentileRank { get; set; }
    public double? WildTypePresentationScore { get; set; }
    public string? PresentationPredictor { get; set; }

    // Step 8
    public double? ImmunogenicityScore { get; set; }
    public string? ImmunogenicityPredictor { get; set; }

    // Step 9
    public bool PassedSelfFilter { get; set; } = true;
    public bool PassedExpressionFilter { get; set; } = true;
    public string? RemovalReason { get; set; }
    public double? SelfSimilarityScore { get; set; }
    public double? ExpressionTpm { get; set; }

    // Step 3 carry-through
    public double Vaf { get; set; }

    // Step 10
    public double? Agretopicity { get; set; }
    public double? FinalScore { get; set; }
    public int? FinalRank { get; set; }
    public bool IsSelected { get; set; }

    public double ComputeAgretopicity()
    {
        if (PresentationScore is null or 0)
            return 0;
        var wt = WildTypePresentationScore ?? 0;
        return (PresentationScore.Value - wt) / PresentationScore.Value;
    }

    public bool IsComplete() =>
        PresentationScore.HasValue && ImmunogenicityScore.HasValue && PassedSelfFilter;
}
