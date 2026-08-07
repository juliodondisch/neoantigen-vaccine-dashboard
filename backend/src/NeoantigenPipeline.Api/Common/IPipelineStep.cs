using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Common;

public interface IPipelineStep
{
    StepDefinition Definition { get; }

    ValidationResult ValidateInputs(string patientId);
    Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken cancellationToken = default);
    Task<StepState> GetStateAsync(string patientId);
    List<ManagedFile> GetInputFiles(string patientId);
    List<ManagedFile> GetOutputFiles(string patientId);
}
