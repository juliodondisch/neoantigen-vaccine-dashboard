using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Models.Dto;
using NeoantigenPipeline.Api.Services._04_ProteinEffects;
using NeoantigenPipeline.Api.Services._05_HlaTyping;
using NeoantigenPipeline.Api.Services._09_Filtering;
using NeoantigenPipeline.Api.Services._10_Ranking;

namespace NeoantigenPipeline.Api.Testing;

/// <summary>
/// Decouples every step from every other for local development: seeds a patient's folders
/// directly with small, hand-generated fixtures so a downstream step (e.g. 08) can be
/// exercised without actually running steps 1-7 (which need tools this machine doesn't have).
/// NOTE: per docs/deviations.md, this lives in the main API project (not the Tests project as
/// spec §7 shows) because DevTestsController needs to construct it via DI; the test project
/// references the same class through its existing ProjectReference to the Api project.
/// </summary>
public class FixtureSeeder
{
    private readonly PathResolver _paths;
    private readonly FileSystemService _files;
    private readonly PatientRepository _patients;
    private readonly string _fixtureRoot;

    private static readonly string[] DefaultAlleles =
        { "HLA-A*02:01", "HLA-A*01:01", "HLA-B*07:02", "HLA-B*08:01", "HLA-C*07:01", "HLA-C*07:02" };

    public FixtureSeeder(PathResolver paths, FileSystemService files, PatientRepository patients, AppConfig config)
    {
        _paths = paths;
        _files = files;
        _patients = patients;
        _fixtureRoot = config.FixtureRoot;
    }

    public async Task<Patient> SeedPatientAsync(string name, string seedThroughStepId, bool useTinyFixtures = true)
    {
        var patient = await _patients.CreateAsync(new CreatePatientRequest { Name = name, ReferenceGenome = "chr21_test" });
        var order = PipelineStepIds.All.ToList();
        var targetIdx = order.IndexOf(seedThroughStepId);
        if (targetIdx < 0)
            targetIdx = order.Count - 1;

        for (var i = 0; i <= targetIdx; i++)
            await SeedStepAsync(patient.Id, order[i], useTinyFixtures);

        return patient;
    }

    public Task SeedStepAsync(string patientId, string stepId, bool useTinyFixtures = true) => stepId switch
    {
        PipelineStepIds.Upload => SeedUploadAsync(patientId, useTinyFixtures),
        PipelineStepIds.Alignment => SeedAlignmentAsync(patientId, useTinyFixtures),
        PipelineStepIds.Variants => SeedVariantsAsync(patientId),
        PipelineStepIds.ProteinEffects => SeedProteinEffectsAsync(patientId),
        PipelineStepIds.HlaTyping => SeedHlaTypingAsync(patientId),
        PipelineStepIds.Candidates => SeedCandidatesAsync(patientId, 100),
        PipelineStepIds.Presentation => SeedPresentationAsync(patientId),
        PipelineStepIds.Immunogenicity => SeedImmunogenicityAsync(patientId),
        PipelineStepIds.Filtering => SeedFilteringAsync(patientId),
        PipelineStepIds.Ranking => SeedRankingAsync(patientId),
        PipelineStepIds.VaccineDesign => Task.CompletedTask, // requires pvacvector; not stub-seeded
        _ => Task.CompletedTask,
    };

    public Task SeedUploadAsync(string patientId, bool tiny)
    {
        var dir = _paths.EnsureStepDir(patientId, PipelineStepIds.Upload);
        WriteTinyFastqPlaceholder(Path.Combine(dir, $"tumor_dna_{PathResolver.Timestamp()}.fastq.gz"));
        WriteTinyFastqPlaceholder(Path.Combine(dir, $"normal_dna_{PathResolver.Timestamp()}.fastq.gz"));
        _files.WriteJson(patientId, PipelineStepIds.Upload, "_manifest.json", new { seeded = true, tiny });
        return Task.CompletedTask;
    }

    public Task SeedAlignmentAsync(string patientId, bool tiny)
    {
        var dir = _paths.EnsureStepDir(patientId, PipelineStepIds.Alignment);
        WriteTinyBamPlaceholder(Path.Combine(dir, $"tumor_{PathResolver.Timestamp()}.bam"));
        WriteTinyBamPlaceholder(Path.Combine(dir, $"normal_{PathResolver.Timestamp()}.bam"));
        return Task.CompletedTask;
    }

    public Task SeedVariantsAsync(string patientId, string vcfFixtureName = "somatic_pass_20.vcf")
    {
        var dir = _paths.EnsureStepDir(patientId, PipelineStepIds.Variants);
        var rng = new Random(42);
        var lines = new List<string> { "##fileformat=VCFv4.2", "#CHROM\tPOS\tID\tREF\tALT\tQUAL\tFILTER\tINFO" };
        for (var i = 0; i < 20; i++)
        {
            var pos = 10_000_000 + i * 50_000;
            var vaf = Math.Round(0.1 + rng.NextDouble() * 0.6, 2);
            lines.Add($"chr21\t{pos}\t.\tA\tG\t99\tPASS\tVAF={vaf}");
        }
        File.WriteAllLines(Path.Combine(dir, $"somatic_pass_{PathResolver.Timestamp()}.vcf.gz.txt"), lines);
        // Written as plain text with a descriptive suffix since we don't gzip fixtures locally;
        // downstream readers treat it as an opaque input path, so this is fine for dev fixtures.
        _files.WriteJson(patientId, PipelineStepIds.Variants, $"variants_{PathResolver.Timestamp()}.summary.json",
            new { totalVariants = 20, passVariants = 20, medianVaf = 0.35 });
        return Task.CompletedTask;
    }

    public Task SeedProteinEffectsAsync(string patientId)
    {
        var dir = _paths.EnsureStepDir(patientId, PipelineStepIds.ProteinEffects);
        var rng = new Random(42);
        var variants = new List<ProteinAlteringVariant>();
        var geneNames = new[] { "TP53", "KRAS", "BRAF", "EGFR", "PIK3CA" };
        for (var i = 0; i < 10; i++)
        {
            var wt = RandomProtein(rng, 30);
            var mutPos = rng.Next(5, 25);
            var mut = wt.ToCharArray();
            mut[mutPos] = RandomAminoAcid(rng, exclude: wt[mutPos]);

            variants.Add(new ProteinAlteringVariant
            {
                Chromosome = "chr21",
                Position = 10_000_000 + i * 50_000,
                Ref = "A",
                Alt = "G",
                GeneSymbol = geneNames[i % geneNames.Length],
                GeneId = $"ENSG{i:D8}",
                TranscriptId = $"ENST{i:D8}",
                Consequence = "missense_variant",
                ProteinPosition = mutPos,
                WildTypeAminoAcid = wt[mutPos].ToString(),
                MutantAminoAcid = mut[mutPos].ToString(),
                Vaf = Math.Round(0.1 + rng.NextDouble() * 0.6, 2),
                WildTypeProteinSequence = wt,
                MutantProteinSequence = new string(mut),
            });
        }

        TsvParser.Write(Path.Combine(dir, $"protein_altering_{PathResolver.Timestamp()}.tsv"), variants);
        _files.WriteJson(patientId, PipelineStepIds.ProteinEffects, $"effects_{PathResolver.Timestamp()}.summary.json",
            new { inputVariants = 20, proteinAltering = variants.Count, discarded = 20 - variants.Count });
        return Task.CompletedTask;
    }

    public Task SeedHlaTypingAsync(string patientId)
    {
        var profile = new HlaProfile
        {
            ClassIAlleles = DefaultAlleles.ToList(),
            Source = "fixture",
            TypedAt = DateTime.UtcNow,
        };
        _files.WriteJson(patientId, PipelineStepIds.HlaTyping, $"hla_{PathResolver.Timestamp()}.json", profile);
        return Task.CompletedTask;
    }

    public Task SeedCandidatesAsync(string patientId, int count = 100)
    {
        var dir = _paths.EnsureStepDir(patientId, PipelineStepIds.Candidates);
        var candidates = BuildSyntheticCandidates(count, DefaultAlleles);
        TsvParser.Write(Path.Combine(dir, $"candidates_{PathResolver.Timestamp()}.tsv"), candidates);
        _files.WriteJson(patientId, PipelineStepIds.Candidates, $"candidates_{PathResolver.Timestamp()}.summary.json",
            new { candidateCount = candidates.Count });
        return Task.CompletedTask;
    }

    public Task SeedPresentationAsync(string patientId)
    {
        var candidates = ReadLatestCandidatesForSeeding(patientId, PipelineStepIds.Candidates, "candidates_*.tsv") ?? BuildSyntheticCandidates(100, DefaultAlleles);
        var rng = new Random(42);
        foreach (var c in candidates)
        {
            c.PresentationScore = Math.Round(rng.NextDouble(), 4);
            c.WildTypePresentationScore = Math.Round(rng.NextDouble() * 0.5, 4);
            c.PresentationPercentileRank = Math.Round(rng.NextDouble() * 10, 3);
            c.PresentationPredictor = "stub";
        }
        var dir = _paths.EnsureStepDir(patientId, PipelineStepIds.Presentation);
        TsvParser.Write(Path.Combine(dir, $"presentation_{PathResolver.Timestamp()}.tsv"), candidates);
        return Task.CompletedTask;
    }

    public Task SeedImmunogenicityAsync(string patientId)
    {
        var candidates = ReadLatestCandidatesForSeeding(patientId, PipelineStepIds.Presentation, "presentation_*.tsv") ?? BuildSyntheticCandidates(100, DefaultAlleles);
        var rng = new Random(42);
        foreach (var c in candidates)
        {
            c.ImmunogenicityScore = Math.Round(rng.NextDouble(), 4);
            c.ImmunogenicityPredictor = "stub";
        }
        var dir = _paths.EnsureStepDir(patientId, PipelineStepIds.Immunogenicity);
        TsvParser.Write(Path.Combine(dir, $"immunogenicity_{PathResolver.Timestamp()}.tsv"), candidates);
        return Task.CompletedTask;
    }

    public Task SeedFilteringAsync(string patientId)
    {
        var candidates = ReadLatestCandidatesForSeeding(patientId, PipelineStepIds.Immunogenicity, "immunogenicity_*.tsv") ?? BuildSyntheticCandidates(100, DefaultAlleles);
        foreach (var c in candidates)
        {
            c.PassedSelfFilter = true;
            c.PassedExpressionFilter = true;
        }
        var dir = _paths.EnsureStepDir(patientId, PipelineStepIds.Filtering);
        TsvParser.Write(Path.Combine(dir, $"filtered_{PathResolver.Timestamp()}.tsv"), candidates);
        TsvParser.Write(Path.Combine(dir, $"removed_{PathResolver.Timestamp()}.tsv"), new List<NeoantigenCandidate>());
        _files.WriteJson(patientId, PipelineStepIds.Filtering, $"filtering_{PathResolver.Timestamp()}.summary.json",
            new FilteringSummary { InputCount = candidates.Count, Survived = candidates.Count, ExpressionFilterApplied = false });
        return Task.CompletedTask;
    }

    public Task SeedRankingAsync(string patientId)
    {
        var candidates = ReadLatestCandidatesForSeeding(patientId, PipelineStepIds.Filtering, "filtered_*.tsv") ?? BuildSyntheticCandidates(100, DefaultAlleles);
        var calculator = new ScoreCalculator(RankingWeights.Default());
        var scored = calculator.ScoreAll(candidates).OrderByDescending(c => c.FinalScore).ToList();
        for (var i = 0; i < scored.Count; i++)
            scored[i].FinalRank = i + 1;

        var selector = new HlaSpreadSelector(0.5, DefaultAlleles.ToList());
        var selected = selector.Select(new List<NeoantigenCandidate>(scored), Math.Min(30, scored.Count));
        var selectedIds = selected.Select(c => c.CandidateId).ToHashSet();
        foreach (var c in scored)
            c.IsSelected = selectedIds.Contains(c.CandidateId);

        var dir = _paths.EnsureStepDir(patientId, PipelineStepIds.Ranking);
        TsvParser.Write(Path.Combine(dir, $"ranked_{PathResolver.Timestamp()}.tsv"), scored);
        TsvParser.Write(Path.Combine(dir, $"selected_{PathResolver.Timestamp()}.tsv"), selected);
        _files.WriteJson(patientId, PipelineStepIds.Ranking, $"weights_{PathResolver.Timestamp()}.json", RankingWeights.Default());
        return Task.CompletedTask;
    }

    public async Task CleanupTestPatientsAsync()
    {
        var summaries = await _patients.ListAsync();
        foreach (var s in summaries.Where(s => s.Name.StartsWith("__test_", StringComparison.Ordinal)))
            await _patients.DeleteAsync(s.Id, deleteFiles: true);
    }

    public List<NeoantigenCandidate> BuildSyntheticCandidates(int count, string[] alleles, Random? rng = null)
    {
        rng ??= new Random(42);
        var candidates = new List<NeoantigenCandidate>();
        for (var i = 0; i < count; i++)
        {
            var length = rng.Next(8, 12);
            var mutant = RandomProtein(rng, length);
            var wildType = mutant[..^1] + RandomAminoAcid(rng, exclude: mutant[^1]);
            candidates.Add(new NeoantigenCandidate
            {
                CandidateId = $"cand_{i:D5}",
                MutantPeptide = mutant,
                WildTypePeptide = wildType,
                HlaAllele = alleles[i % alleles.Length],
                PeptideLength = length,
                GeneSymbol = $"GENE{i % 20}",
                TranscriptId = $"ENST{i:D8}",
                SourceVariantId = $"chr21:{10_000_000 + i}:A>G",
                Position = rng.Next(1, 500),
                MutationOffsetInPeptide = length - 1,
                Vaf = Math.Round(0.05 + rng.NextDouble() * 0.7, 3),
            });
        }
        return candidates;
    }

    private List<NeoantigenCandidate> BuildSyntheticCandidates(int count, List<string> alleles) =>
        BuildSyntheticCandidates(count, alleles.ToArray());

    private List<NeoantigenCandidate>? ReadLatestCandidatesForSeeding(string patientId, string stepId, string glob)
    {
        var latest = _files.FindLatestFile(patientId, stepId, glob);
        if (latest is null) return null;
        var text = _files.ReadTextFile(patientId, stepId, latest.Name, maxBytes: 50_000_000);
        return text is null ? null : TsvParser.Parse<NeoantigenCandidate>(text);
    }

    private string FixturePath(params string[] parts) => Path.Combine(new[] { _fixtureRoot }.Concat(parts).ToArray());

    private static void WriteTinyFastqPlaceholder(string path)
    {
        var content = "@synthetic-read/1\nACGTACGTACGTACGTACGTACGTACGT\n+\nIIIIIIIIIIIIIIIIIIIIIIIIIIII\n";
        File.WriteAllText(path, content);
    }

    private static void WriteTinyBamPlaceholder(string path) =>
        File.WriteAllText(path, "# placeholder BAM fixture — not a real alignment (dev-only, generated locally)\n");

    private static string RandomProtein(Random rng, int length) =>
        new(Enumerable.Range(0, length).Select(_ => RandomAminoAcid(rng)).ToArray());

    private static readonly char[] AminoAcids = "ACDEFGHIKLMNPQRSTVWY".ToCharArray();

    private static char RandomAminoAcid(Random rng, char? exclude = null)
    {
        char c;
        do { c = AminoAcids[rng.Next(AminoAcids.Length)]; } while (exclude.HasValue && c == exclude.Value);
        return c;
    }
}
