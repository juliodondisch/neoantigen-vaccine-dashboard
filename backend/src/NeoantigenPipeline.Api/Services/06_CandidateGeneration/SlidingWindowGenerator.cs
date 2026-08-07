using NeoantigenPipeline.Api.Services._04_ProteinEffects;

namespace NeoantigenPipeline.Api.Services._06_CandidateGeneration;

public class PeptidePair
{
    public string MutantPeptide { get; set; } = "";
    public string WildTypePeptide { get; set; } = "";
    public int Length { get; set; }
    public int MutationOffsetInPeptide { get; set; }
    public string GeneSymbol { get; set; } = "";
    public string TranscriptId { get; set; } = "";
    public int ProteinPosition { get; set; }
    public double Vaf { get; set; }
    public string SourceVariantId { get; set; } = "";
}

/// <summary>
/// Pure logic, no external tools or I/O ,  the C# version is authoritative;
/// keep behaviorally identical to python/scripts/generate_candidates.py.
/// </summary>
public class SlidingWindowGenerator
{
    private static readonly HashSet<char> ValidAminoAcids = new("ACDEFGHIKLMNPQRSTVWY*");

    private readonly int _minLength;
    private readonly int _maxLength;

    public SlidingWindowGenerator(int minLength = 8, int maxLength = 11)
    {
        _minLength = minLength;
        _maxLength = maxLength;
    }

    public List<PeptidePair> GeneratePairs(ProteinAlteringVariant variant)
    {
        if (string.IsNullOrEmpty(variant.WildTypeProteinSequence) || string.IsNullOrEmpty(variant.MutantProteinSequence))
            return new List<PeptidePair>();

        var pairs = GenerateForAllLengths(variant.WildTypeProteinSequence, variant.MutantProteinSequence, variant.ProteinPosition);
        foreach (var p in pairs)
        {
            p.GeneSymbol = variant.GeneSymbol;
            p.TranscriptId = variant.TranscriptId;
            p.ProteinPosition = variant.ProteinPosition;
            p.Vaf = variant.Vaf;
            p.SourceVariantId = $"{variant.Chromosome}:{variant.Position}:{variant.Ref}>{variant.Alt}";
        }
        return pairs;
    }

    public List<string> GenerateWindows(string proteinSequence, int mutationPosition, int windowLength)
    {
        if (string.IsNullOrEmpty(proteinSequence) || proteinSequence.Length < windowLength)
            return new List<string>();

        var windows = new List<string>();
        // A window of `windowLength` must contain the 0-based `mutationPosition`.
        // Its start can range from (mutationPosition - windowLength + 1) to mutationPosition,
        // clamped so the window never runs off either end of the sequence.
        var earliestStart = Math.Max(0, mutationPosition - windowLength + 1);
        var latestStart = Math.Min(proteinSequence.Length - windowLength, mutationPosition);

        for (var start = earliestStart; start <= latestStart; start++)
        {
            var window = proteinSequence.Substring(start, windowLength);
            if (IsValidPeptide(window))
                windows.Add(window);
        }
        return windows;
    }

    public List<PeptidePair> GenerateForAllLengths(string wildTypeSequence, string mutantSequence, int mutationPosition)
    {
        var pairs = new List<PeptidePair>();
        for (var length = _minLength; length <= _maxLength; length++)
        {
            var earliestStart = Math.Max(0, mutationPosition - length + 1);
            var latestStart = Math.Min(mutantSequence.Length - length, mutationPosition);
            if (latestStart < earliestStart || mutantSequence.Length < length)
                continue;

            for (var start = earliestStart; start <= latestStart; start++)
            {
                var (wtStart, wtEnd) = ClampWindow(start, length, wildTypeSequence.Length);
                if (wtEnd - wtStart != length)
                    continue; // wild-type sequence too short at this offset (near a terminus) ,  skip rather than crash

                var mutant = mutantSequence.Substring(start, length);
                var wildType = wildTypeSequence.Substring(wtStart, length);
                if (!IsValidPeptide(mutant) || !IsValidPeptide(wildType))
                    continue;

                pairs.Add(new PeptidePair
                {
                    MutantPeptide = mutant,
                    WildTypePeptide = wildType,
                    Length = length,
                    MutationOffsetInPeptide = mutationPosition - start,
                });
            }
        }
        return pairs;
    }

    public int ExpectedWindowCount(int proteinLength, int mutationPosition)
    {
        var total = 0;
        for (var length = _minLength; length <= _maxLength; length++)
        {
            if (proteinLength < length)
                continue;
            var earliestStart = Math.Max(0, mutationPosition - length + 1);
            var latestStart = Math.Min(proteinLength - length, mutationPosition);
            if (latestStart >= earliestStart)
                total += latestStart - earliestStart + 1;
        }
        return total;
    }

    private static bool IsValidPeptide(string peptide) => peptide.Length > 0 && peptide.All(ValidAminoAcids.Contains);

    private static (int start, int end) ClampWindow(int center, int length, int sequenceLength)
    {
        var start = Math.Max(0, center);
        var end = Math.Min(sequenceLength, start + length);
        start = Math.Max(0, end - length);
        return (start, end);
    }
}
