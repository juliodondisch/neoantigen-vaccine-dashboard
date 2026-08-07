namespace NeoantigenPipeline.Api.Common.Exceptions;

public class PatientNotFoundException : PipelineException
{
    public string PatientId { get; }

    public PatientNotFoundException(string patientId)
        : base($"Patient '{patientId}' was not found")
    {
        PatientId = patientId;
    }
}
