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

    // No inline default on purpose — Validate() throws if this is unset. A hidden fallback
    // here is exactly what let "which reference genome" drift silently across five different
    // places in the codebase during the first real deployment (see docs/CORRECTION_PLAN.md §1).
    public string DefaultReferenceGenome { get; set; } = "";

    public bool UseVepDatabaseMode { get; set; }
    public string FixtureRoot { get; set; } = "";

    // Per-step timeout overrides, keyed by step ID (e.g. "05_hla_typing"). Falls back to
    // LongStepTimeoutSeconds when a step isn't listed.
    public Dictionary<string, int> StepTimeoutSeconds { get; set; } = new();

    public int GetStepTimeout(string stepId) =>
        StepTimeoutSeconds.TryGetValue(stepId, out var t) ? t : LongStepTimeoutSeconds;

    // Origins allowed to call the API via CORS — the frontend's own address as seen by the
    // browser. appsettings.json supplies "http://localhost:3000" as the real default (works
    // out of the box via an SSH tunnel to a remote server); no inline default here on purpose —
    // .NET's config binder appends config-sourced array values onto a non-empty field-initializer
    // default rather than replacing it, which silently duplicated every entry. For direct access
    // from another machine, add that frontend origin in config — see DEPLOY.md.
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

    public string GetToolPath(string toolName) =>
        ToolPaths.TryGetValue(toolName, out var path) ? path : toolName;

    private static readonly string[] RequiredToolKeys =
        { "bwa-mem2", "samtools", "gatk", "vep", "OptiType", "mhcflurry", "pvacseq", "pvacvector" };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DataRoot))
            throw new InvalidOperationException("App:DataRoot must be configured.");
        if (string.IsNullOrWhiteSpace(PythonScriptsRoot))
            throw new InvalidOperationException("App:PythonScriptsRoot must be configured.");
        if (string.IsNullOrWhiteSpace(DefaultReferenceGenome))
            throw new InvalidOperationException("App:DefaultReferenceGenome must be configured.");
        if (AllowedOrigins is null || AllowedOrigins.Length == 0)
            throw new InvalidOperationException("App:AllowedOrigins must be configured.");
        foreach (var required in RequiredToolKeys)
            if (!ToolPaths.ContainsKey(required))
                throw new InvalidOperationException($"App:ToolPaths must contain an entry for '{required}'.");
    }
}
