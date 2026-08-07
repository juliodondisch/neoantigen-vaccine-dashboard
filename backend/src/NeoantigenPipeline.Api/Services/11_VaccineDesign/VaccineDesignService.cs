using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Services._05_HlaTyping;
using NeoantigenPipeline.Api.Services._10_Ranking;

namespace NeoantigenPipeline.Api.Services._11_VaccineDesign;

public class VaccineConstruct
{
    public string FullSequence { get; set; } = "";
    public int TotalLengthBp { get; set; }
    public List<ConstructElement> Elements { get; set; } = new();
    public List<string> PeptideOrder { get; set; } = new();
    public int JunctionalEpitopesAvoided { get; set; }
    public string LinkerSequence { get; set; } = "";
    public string FivePrimeUtr { get; set; } = "";
    public string ThreePrimeUtr { get; set; } = "";
    public int PolyATailLength { get; set; }
    public DateTime DesignedAt { get; set; }
}

public class ConstructElement
{
    public string Type { get; set; } = ""; // "5utr" | "signal" | "neoantigen" | "linker" | "3utr" | "polyA"
    public string Sequence { get; set; } = "";
    public int StartPosition { get; set; }
    public int EndPosition { get; set; }
    public string? Label { get; set; }
}

public class VaccineDesignService : PipelineStepBase
{
    public const string StepId = PipelineStepIds.VaccineDesign;
    private readonly RankingService _rankingService;
    private readonly HlaTypingService _hlaService;

    public override StepDefinition Definition { get; } = new()
    {
        Id = StepId,
        Order = 11,
        DisplayName = "Design Vaccine Sequence",
        ShortDescription = "Assemble selected targets into a synthesizable mRNA construct",
        LongExplanation = "This assembles the final selected targets into a single mRNA sequence — the actual blueprint a lab would synthesize. The chosen fragments are strung together with short connector sequences between them, wrapped in standard start and end elements that help cells read the instructions properly. The output is a sequence file, not a physical vaccine; manufacturing requires specialized facilities and regulatory approval.",
        ToolName = "pVACvector",
        RequiredInputStepIds = new[] { PipelineStepIds.Ranking },
        IsUploadStep = false,
        HasParameters = true,
        ProducesDownload = true,
        RequiredTools = new[] { "pvacvector" },
    };

    public VaccineDesignService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools,
        RankingService rankingService, HlaTypingService hlaService, ILogger<VaccineDesignService> logger)
        : base(paths, files, python, tools, logger)
    {
        _rankingService = rankingService;
        _hlaService = hlaService;
    }

    public override async Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var selected = _rankingService.ReadSelectedCandidates(patientId);
        if (selected.Count == 0)
            return StepResult.Fail(StepId, "No selected candidates available", "Run ranking (step 10) first.");

        var selectedTsvPath = Paths.BuildOutputPath(patientId, StepId, "_selected_input", ".tsv");
        TsvParser.Write(selectedTsvPath, selected);

        var hlaProfile = _hlaService.GetHlaProfile(patientId);
        var hlaJsonPath = Path.Combine(Path.GetTempPath(), $"hla_{Guid.NewGuid():N}.json");
        Files.WriteJson(patientId, StepId, "_hla_context.json", hlaProfile?.ClassIAlleles ?? new List<string>());

        var outputDir = Paths.GetStepDir(patientId, StepId);
        var linkerType = parameters.GetString("linkerType", "gs") ?? "gs";
        var includeSignal = parameters.GetBool("includeSignalPeptide", true);
        var codonOptimize = parameters.GetBool("codonOptimize", true);
        var exportFormat = parameters.GetString("exportFormat", "both") ?? "both";

        var args = new Dictionary<string, string>
        {
            ["selected-tsv"] = selectedTsvPath,
            ["hla-json"] = Path.Combine(Paths.GetStepDir(patientId, StepId), "_hla_context.json"),
            ["output-dir"] = outputDir,
            ["linker-type"] = linkerType,
            ["include-signal"] = includeSignal ? "true" : "false",
            ["codon-optimize"] = codonOptimize ? "true" : "false",
            ["export-format"] = exportFormat,
        };

        try
        {
            var response = await Python.RunAndParseAsync("design_vaccine.py", args, new PythonExecutionOptions { TimeoutSeconds = 600, CancellationToken = ct });
            WriteSummary(patientId, response.Summary);
            return BuildResult(patientId, response, DateTime.UtcNow - start);
        }
        catch (Common.Exceptions.PythonExecutionException ex)
        {
            return StepResult.Fail(StepId, "Vaccine design failed", ex.Stderr);
        }
    }

    public VaccineConstruct? GetLatestConstruct(string patientId)
    {
        var latest = Files.FindLatestFile(patientId, StepId, "construct_*.json");
        return latest is null ? null : Files.ReadJson<VaccineConstruct>(patientId, StepId, latest.Name);
    }

    public Stream OpenFastaStream(string patientId, string? fileName = null)
    {
        fileName ??= Files.FindLatestFile(patientId, StepId, "vaccine_*.fasta")?.Name
            ?? throw new FileNotFoundException("No FASTA output found.");
        return Files.OpenRead(patientId, StepId, fileName);
    }

    public Stream OpenGenBankStream(string patientId, string? fileName = null)
    {
        fileName ??= Files.FindLatestFile(patientId, StepId, "vaccine_*.gb")?.Name
            ?? throw new FileNotFoundException("No GenBank output found.");
        return Files.OpenRead(patientId, StepId, fileName);
    }
}
