using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Common;

public interface IPipelineStep
{
    StepDefinition Definition { get; }

    /// <summary>Glob patterns (matched against this step's own output folder) that identify a
    /// *real*, final output — as opposed to an intermediate file a partially-failed run might
    /// have left behind (e.g. HLA typing's "hla_region_reads.bam" scratch file). Empty means
    /// "any output file counts" (the old, looser behavior). Used by GetStateAsync so a step
    /// whose last job failed doesn't report Completed just because some file exists on disk.</summary>
    string[] PrimaryOutputPatterns => Array.Empty<string>();

    ValidationResult ValidateInputs(string patientId);
    Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken cancellationToken = default);
    Task<StepState> GetStateAsync(string patientId);
    List<ManagedFile> GetInputFiles(string patientId);
    List<ManagedFile> GetOutputFiles(string patientId);
}
