using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Services._04_ProteinEffects;

public class EffectsSummary
{
    public int InputVariants { get; set; }
    public int ProteinAltering { get; set; }
    public int Discarded { get; set; }
    public Dictionary<string, int> ConsequenceCounts { get; set; } = new();
}

public class ProteinAlteringVariant
{
    public string Chromosome { get; set; } = "";
    public int Position { get; set; }
    public string Ref { get; set; } = "";
    public string Alt { get; set; } = "";
    public string GeneSymbol { get; set; } = "";
    public string GeneId { get; set; } = "";
    public string TranscriptId { get; set; } = "";
    public string Consequence { get; set; } = "";
    public int ProteinPosition { get; set; }
    public string WildTypeAminoAcid { get; set; } = "";
    public string MutantAminoAcid { get; set; } = "";
    public double Vaf { get; set; }
    public string? WildTypeProteinSequence { get; set; }
    public string? MutantProteinSequence { get; set; }
}

public class ProteinEffectsService : PipelineStepBase
{
    public const string StepId = PipelineStepIds.ProteinEffects;
    private static readonly string[] KeptConsequences =
        { "missense_variant", "stop_gained", "frameshift_variant", "inframe_insertion", "inframe_deletion", "start_lost" };

    public override StepDefinition Definition { get; } = new()
    {
        Id = StepId,
        Order = 4,
        DisplayName = "Determine Protein Consequences",
        ShortDescription = "Translate mutations into protein-level effects",
        LongExplanation = "Not every DNA mutation matters. Only about 1-2% of the genome codes for proteins at all, and even within that, some mutations happen to produce the same amino acid as before ,  changing the DNA without changing the protein. This step translates each mutation into its protein-level effect and keeps only the ones that genuinely alter a protein, since those are the only ones the immune system could possibly notice.",
        ToolName = "VEP (Variant Effect Predictor)",
        RequiredInputStepIds = new[] { PipelineStepIds.Variants },
        IsUploadStep = false,
        HasParameters = true,
        ProducesDownload = false,
        RequiredTools = new[] { "vep" },
    };

    public ProteinEffectsService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, ILogger<ProteinEffectsService> logger)
        : base(paths, files, python, tools, logger)
    {
    }

    public override async Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var inputVcf = RequireLatestFile(patientId, PipelineStepIds.Variants, "somatic_pass_*.vcf.gz", "PASS-filtered VCF");
        var outputVcf = Paths.BuildOutputPath(patientId, StepId, "annotated", ".vcf.gz");
        var outputTsv = Paths.BuildOutputPath(patientId, StepId, "protein_altering", ".tsv");
        var useDatabase = parameters.GetBool("useDatabaseMode", true);
        var keep = parameters.Get<string[]>("keepConsequences") ?? KeptConsequences;

        var args = new Dictionary<string, string>
        {
            ["input-vcf"] = inputVcf,
            ["output-vcf"] = outputVcf,
            ["output-tsv"] = outputTsv,
            ["use-database"] = useDatabase ? "true" : "false",
            ["cache-dir"] = "",
            ["keep-consequences"] = string.Join(',', keep),
        };

        try
        {
            var response = await Python.RunAndParseAsync("annotate_effects.py", args, new PythonExecutionOptions { TimeoutSeconds = 1800, CancellationToken = ct });
            WriteSummary(patientId, response.Summary);
            return BuildResult(patientId, response, DateTime.UtcNow - start);
        }
        catch (Common.Exceptions.PythonExecutionException ex)
        {
            return StepResult.Fail(StepId, "Protein effect annotation failed", ex.Stderr);
        }
    }

    public EffectsSummary? GetLatestEffectsSummary(string patientId)
    {
        var latest = Files.FindLatestFile(patientId, StepId, "effects_*.summary.json");
        return latest is null ? null : Files.ReadJson<EffectsSummary>(patientId, StepId, latest.Name);
    }

    public List<ProteinAlteringVariant> ReadProteinAlteringVariants(string patientId)
    {
        var latest = Files.FindLatestFile(patientId, StepId, "protein_altering_*.tsv");
        if (latest is null)
            return new List<ProteinAlteringVariant>();

        var text = Files.ReadTextFile(patientId, StepId, latest.Name, maxBytes: 20_000_000);
        return text is null ? new List<ProteinAlteringVariant>() : TsvParser.Parse<ProteinAlteringVariant>(text);
    }
}
