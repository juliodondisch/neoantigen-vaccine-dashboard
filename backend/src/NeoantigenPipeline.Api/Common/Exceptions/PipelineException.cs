namespace NeoantigenPipeline.Api.Common.Exceptions;

public class PipelineException : Exception
{
    public string? StepId { get; }

    public PipelineException(string message, string? stepId = null, Exception? inner = null)
        : base(message, inner)
    {
        StepId = stepId;
    }
}
