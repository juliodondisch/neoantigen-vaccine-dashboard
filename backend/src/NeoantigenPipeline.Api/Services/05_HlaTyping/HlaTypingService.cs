using System.Text.RegularExpressions;
using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Services._05_HlaTyping;

public class HlaProfile
{
    public List<string> ClassIAlleles { get; set; } = new();
    public List<string> ClassIIAlleles { get; set; } = new();
    public Dictionary<string, double> Confidence { get; set; } = new();
    public DateTime TypedAt { get; set; }
    public string Source { get; set; } = "OptiType";

    public List<string> GetAllAlleles() => ClassIAlleles.Concat(ClassIIAlleles).ToList();
    public bool IsComplete() => ClassIAlleles.Count >= 6;
}

public class HlaTypingService : PipelineStepBase
{
    public const string StepId = PipelineStepIds.HlaTyping;
    private static readonly Regex AllelePattern = new(@"^HLA-[A-C]\*\d{2}:\d{2}$", RegexOptions.Compiled);

    public override StepDefinition Definition { get; } = new()
    {
        Id = StepId,
        Order = 5,
        DisplayName = "HLA Typing",
        ShortDescription = "Determine the patient's HLA class I alleles",
        LongExplanation = "HLA molecules are the display cases cells use to show the immune system samples of what they're building inside. Everyone inherits a specific set of HLA variants, and different variants physically hold different protein fragments ,  a target that works for one person may be invisible in another. This step reads the healthy DNA (HLA type is inherited, not caused by the cancer) to determine this patient's specific HLA variants.",
        ToolName = "OptiType",
        RequiredInputStepIds = new[] { PipelineStepIds.Upload },
        IsUploadStep = false,
        HasParameters = true,
        ProducesDownload = false,
        RequiredTools = new[] { "OptiType" },
    };

    public override string[] PrimaryOutputPatterns => new[] { "hla_*.json" };

    public HlaTypingService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, AppConfig config, ILogger<HlaTypingService> logger)
        : base(paths, files, python, tools, config, logger)
    {
    }

    public override ValidationResult ValidateInputs(string patientId)
    {
        var result = ValidationResult.Valid();
        var hasNormal = Files.StepHasFilesMatching(patientId, PipelineStepIds.Upload, "normal_dna_*") ||
                         Files.StepHasFilesMatching(patientId, PipelineStepIds.Upload, "normal*.bam") ||
                         Files.StepHasFilesMatching(patientId, PipelineStepIds.Alignment, "normal_*.bam");
        if (!hasNormal)
            result.AddError("Normal DNA (from step 01_upload or 02_alignment) is required for HLA typing.");

        foreach (var missing in Tools.GetMissingTools(Definition.RequiredTools))
            result.AddMissingTool(missing);

        return result;
    }

    public override async Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var manualAlleles = parameters.Get<string[]>("manualAlleles");
        var includeClassII = parameters.GetBool("includeClassII", false);

        if (manualAlleles is { Length: > 0 })
        {
            var (valid, invalid) = ValidateAlleles(manualAlleles);
            if (!valid)
                return StepResult.Fail(StepId, "Invalid HLA allele format", string.Join(", ", invalid));

            var profile = new HlaProfile { ClassIAlleles = manualAlleles.ToList(), Source = "manual", TypedAt = DateTime.UtcNow };
            return WriteProfile(patientId, profile, start, "Manual HLA override applied");
        }

        var inputBam = Files.FindLatestFile(patientId, PipelineStepIds.Alignment, "normal_*.bam")
            ?? Files.FindLatestFile(patientId, PipelineStepIds.Upload, "normal_dna_*")
            ?? Files.FindLatestFile(patientId, PipelineStepIds.Upload, "normal*.bam");
        if (inputBam is null)
            return StepResult.Fail(StepId, "No normal DNA input found");

        var isBam = inputBam.Extension.Equals(".bam", StringComparison.OrdinalIgnoreCase);
        var outputJson = Paths.BuildOutputPath(patientId, StepId, "hla", "json");

        var args = new Dictionary<string, string>
        {
            ["input"] = Path.Combine(Paths.GetStepDir(patientId, isBam ? PipelineStepIds.Alignment : PipelineStepIds.Upload), inputBam.Name),
            ["output-dir"] = Paths.GetStepDir(patientId, StepId),
            ["output-json"] = outputJson,
            ["is-bam"] = isBam ? "true" : "false",
            ["include-class-ii"] = includeClassII ? "true" : "false",
        };

        try
        {
            var response = await Python.RunAndParseAsync("type_hla.py", args, new PythonExecutionOptions { TimeoutSeconds = Config.GetStepTimeout(StepId), CancellationToken = ct }, patientId: patientId);
            var duration = DateTime.UtcNow - start;
            WriteSummary(patientId, response.Summary);
            return BuildResult(patientId, response, duration);
        }
        catch (Common.Exceptions.PythonExecutionException ex)
        {
            return StepResult.Fail(StepId, "HLA typing failed", ex.Stderr);
        }
    }

    public HlaProfile? GetHlaProfile(string patientId)
    {
        var latest = Files.FindLatestFile(patientId, StepId, "hla_*.json");
        if (latest is null)
            return null;
        return Files.ReadJson<HlaProfile>(patientId, StepId, latest.Name);
    }

    public bool HasHlaProfile(string patientId) => GetHlaProfile(patientId) is not null;

    private static bool IsValidAlleleFormat(string allele) => AllelePattern.IsMatch(allele);

    private (bool valid, List<string> invalid) ValidateAlleles(IEnumerable<string> alleles)
    {
        var invalid = alleles.Where(a => !IsValidAlleleFormat(a)).ToList();
        return (invalid.Count == 0, invalid);
    }

    private StepResult WriteProfile(string patientId, HlaProfile profile, DateTime start, string message)
    {
        var fileName = $"hla_{PathResolver.Timestamp()}.json";
        Files.WriteJson(patientId, StepId, fileName, profile);
        var summary = new Dictionary<string, object> { ["alleleCount"] = profile.ClassIAlleles.Count, ["source"] = profile.Source };
        WriteSummary(patientId, summary);
        var outputFiles = Files.ListStepFiles(patientId, StepId, fileName);
        return StepResult.Ok(StepId, message, outputFiles, summary, DateTime.UtcNow - start);
    }
}
