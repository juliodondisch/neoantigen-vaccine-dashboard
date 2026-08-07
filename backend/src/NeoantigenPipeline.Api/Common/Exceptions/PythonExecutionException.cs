namespace NeoantigenPipeline.Api.Common.Exceptions;

public class PythonExecutionException : PipelineException
{
    public int ExitCode { get; }
    public string Stderr { get; }
    public string ScriptName { get; }

    public PythonExecutionException(string scriptName, int exitCode, string stderr, string? stepId = null)
        : base($"Python script '{scriptName}' exited with code {exitCode}", stepId)
    {
        ScriptName = scriptName;
        ExitCode = exitCode;
        Stderr = stderr;
    }
}
