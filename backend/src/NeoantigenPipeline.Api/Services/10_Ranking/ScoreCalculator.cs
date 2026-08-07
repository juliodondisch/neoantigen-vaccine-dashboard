using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Services._10_Ranking;

public class RankingWeights
{
    public double Presentation { get; set; } = 1.0;
    public double Immunogenicity { get; set; } = 1.0;
    public double Agretopicity { get; set; } = 0.5;
    public double Expression { get; set; } = 0.5;
    public double Clonality { get; set; } = 0.5;
    public double HlaSpread { get; set; } = 0.5;

    public bool AllZero() => Presentation == 0 && Immunogenicity == 0 && Agretopicity == 0 &&
                              Expression == 0 && Clonality == 0 && HlaSpread == 0;

    public RankingWeights Normalized()
    {
        // HlaSpread is excluded from the per-candidate sum — it's a set-level selection
        // constraint applied separately by HlaSpreadSelector, not an additive term.
        var sum = Presentation + Immunogenicity + Agretopicity + Expression + Clonality;
        if (sum <= 0)
            return new RankingWeights { Presentation = 0, Immunogenicity = 0, Agretopicity = 0, Expression = 0, Clonality = 0, HlaSpread = HlaSpread };

        return new RankingWeights
        {
            Presentation = Presentation / sum,
            Immunogenicity = Immunogenicity / sum,
            Agretopicity = Agretopicity / sum,
            Expression = Expression / sum,
            Clonality = Clonality / sum,
            HlaSpread = HlaSpread,
        };
    }

    public static RankingWeights FromParameters(StepParameters parameters) => new()
    {
        Presentation = parameters.GetDouble("presentationWeight", 1.0),
        Immunogenicity = parameters.GetDouble("immunogenicityWeight", 1.0),
        Agretopicity = parameters.GetDouble("agretopicityWeight", 0.5),
        Expression = parameters.GetDouble("expressionWeight", 0.5),
        Clonality = parameters.GetDouble("clonalityWeight", 0.5),
        HlaSpread = parameters.GetDouble("hlaSpreadWeight", 0.5),
    };

    public static RankingWeights Default() => new();
}

public class NormalizationBounds
{
    public double MinPresentation { get; set; }
    public double MaxPresentation { get; set; }
    public double MinImmunogenicity { get; set; }
    public double MaxImmunogenicity { get; set; }
    public double MinAgretopicity { get; set; }
    public double MaxAgretopicity { get; set; }
    public double MinExpression { get; set; }
    public double MaxExpression { get; set; }
    public double MinVaf { get; set; }
    public double MaxVaf { get; set; }
}

public class ScoreCalculator
{
    private readonly RankingWeights _weights;

    public ScoreCalculator(RankingWeights weights)
    {
        _weights = weights.Normalized();
    }

    public double ComputeScore(NeoantigenCandidate candidate, NormalizationBounds bounds)
    {
        var presentation = Normalize(SafeGet(candidate.PresentationScore), bounds.MinPresentation, bounds.MaxPresentation);
        var immunogenicity = Normalize(SafeGet(candidate.ImmunogenicityScore), bounds.MinImmunogenicity, bounds.MaxImmunogenicity);
        var agretopicity = Normalize(SafeGet(candidate.Agretopicity ?? candidate.ComputeAgretopicity()), bounds.MinAgretopicity, bounds.MaxAgretopicity);
        var expression = Normalize(SafeGet(candidate.ExpressionTpm), bounds.MinExpression, bounds.MaxExpression);
        var clonality = Normalize(candidate.Vaf, bounds.MinVaf, bounds.MaxVaf);

        return _weights.Presentation * presentation
             + _weights.Immunogenicity * immunogenicity
             + _weights.Agretopicity * agretopicity
             + _weights.Expression * expression
             + _weights.Clonality * clonality;
    }

    public List<NeoantigenCandidate> ScoreAll(List<NeoantigenCandidate> candidates)
    {
        var bounds = ComputeBounds(candidates);
        foreach (var c in candidates)
            c.FinalScore = ComputeScore(c, bounds);
        return candidates;
    }

    public NormalizationBounds ComputeBounds(List<NeoantigenCandidate> candidates)
    {
        if (candidates.Count == 0)
            return new NormalizationBounds();

        double Min(Func<NeoantigenCandidate, double> f) => candidates.Select(f).DefaultIfEmpty(0).Min();
        double Max(Func<NeoantigenCandidate, double> f) => candidates.Select(f).DefaultIfEmpty(0).Max();

        return new NormalizationBounds
        {
            MinPresentation = Min(c => SafeGet(c.PresentationScore)),
            MaxPresentation = Max(c => SafeGet(c.PresentationScore)),
            MinImmunogenicity = Min(c => SafeGet(c.ImmunogenicityScore)),
            MaxImmunogenicity = Max(c => SafeGet(c.ImmunogenicityScore)),
            MinAgretopicity = Min(c => SafeGet(c.Agretopicity ?? c.ComputeAgretopicity())),
            MaxAgretopicity = Max(c => SafeGet(c.Agretopicity ?? c.ComputeAgretopicity())),
            MinExpression = Min(c => SafeGet(c.ExpressionTpm)),
            MaxExpression = Max(c => SafeGet(c.ExpressionTpm)),
            MinVaf = Min(c => c.Vaf),
            MaxVaf = Max(c => c.Vaf),
        };
    }

    private static double Normalize(double value, double min, double max) =>
        max > min ? (value - min) / (max - min) : 0.5;

    private static double SafeGet(double? value, double fallback = 0.0) => value ?? fallback;
}
