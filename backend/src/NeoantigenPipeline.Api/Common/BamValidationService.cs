using System.Text.Json;

namespace NeoantigenPipeline.Api.Common;

/// <summary>
/// Checks an uploaded/externally-aligned BAM has a correct @RG SM: tag, is coordinate-sorted,
/// and has an index — repairing what's safely fixable via python/scripts/validate_bam.py.
/// Externally-provided BAMs (skip-alignment path) are the input this app has the least
/// control over, so this runs whenever one enters the pipeline rather than letting a
/// missing read group surface as a cryptic GATK failure two steps downstream.
/// </summary>
public class BamValidationService
{
    private readonly PythonRunner _python;
    private readonly ILogger<BamValidationService> _logger;

    public BamValidationService(PythonRunner python, ILogger<BamValidationService> logger)
    {
        _python = python;
        _logger = logger;
    }

    public record ValidationOutcome(bool Success, string? RepairedPath, string? Error, string? Message);

    public async Task<ValidationOutcome> ValidateAndFixAsync(string bamPath, string expectedSampleName, string outputDir, string patientId, CancellationToken ct)
    {
        var outputPath = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(bamPath)}_validated_{PathResolver.Timestamp()}.bam");
        var args = new Dictionary<string, string>
        {
            ["bam"] = bamPath,
            ["expected-sample-name"] = expectedSampleName,
            ["output-bam"] = outputPath,
            ["fix"] = "true",
        };

        try
        {
            var response = await _python.RunAndParseAsync(
                "validate_bam.py", args,
                new PythonExecutionOptions { TimeoutSeconds = 1800, CancellationToken = ct },
                patientId: patientId);

            var repairedPath = GetStringField(response.Summary, "repairedPath");
            return new ValidationOutcome(true, repairedPath, null, response.Message);
        }
        catch (Exceptions.PythonExecutionException ex)
        {
            return new ValidationOutcome(false, null, ex.Stderr, null);
        }
    }

    private static string? GetStringField(Dictionary<string, object> summary, string key)
    {
        if (!summary.TryGetValue(key, out var value))
            return null;
        return value switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } je => je.GetString(),
            _ => null,
        };
    }
}
