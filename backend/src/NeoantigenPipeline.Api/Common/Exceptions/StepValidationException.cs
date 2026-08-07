using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Common.Exceptions;

public class StepValidationException : PipelineException
{
    public ValidationResult Validation { get; }

    public StepValidationException(ValidationResult validation, string stepId)
        : base(string.Join("; ", validation.Errors), stepId)
    {
        Validation = validation;
    }
}
