namespace NeoantigenPipeline.Api.Common;

public class AppConfig
{
    public string DataRoot { get; set; } = "";
    public string ReferenceRoot { get; set; } = "";
    public string PythonExecutable { get; set; } = "python3";
    public string PythonScriptsRoot { get; set; } = "";
    public int DefaultTimeoutSeconds { get; set; } = 3600;
    public int LongStepTimeoutSeconds { get; set; } = 86400;
    public bool EnableDevEndpoints { get; set; }
    public Dictionary<string, string> ToolPaths { get; set; } = new();
    public string DefaultReferenceGenome { get; set; } = "chr21_test";
    public bool UseVepDatabaseMode { get; set; }
    public string FixtureRoot { get; set; } = "";

    // Origins allowed to call the API via CORS — the frontend's own address as seen by the
    // browser. Defaults to localhost:3000 (works out of the box via an SSH tunnel to a remote
    // server). For direct access from another machine, add that frontend origin here — see
    // DEPLOY.md.
    public string[] AllowedOrigins { get; set; } = { "http://localhost:3000" };

    public string GetToolPath(string toolName) =>
        ToolPaths.TryGetValue(toolName, out var path) ? path : toolName;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DataRoot))
            throw new InvalidOperationException("App:DataRoot must be configured.");
        if (string.IsNullOrWhiteSpace(PythonScriptsRoot))
            throw new InvalidOperationException("App:PythonScriptsRoot must be configured.");
    }
}
