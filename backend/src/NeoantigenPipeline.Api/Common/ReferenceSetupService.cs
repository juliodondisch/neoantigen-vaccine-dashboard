namespace NeoantigenPipeline.Api.Common;

/// <summary>
/// Detects whether a reference genome + bwa-mem2 index are present, checks disk space
/// before attempting anything, and invokes python/scripts/setup_reference.py to fetch
/// and build them when missing. Wired into AlignmentService so hitting "run alignment"
/// on a fresh server (no reference downloaded yet) triggers setup automatically instead
/// of failing with a missing-file error.
/// </summary>
public class ReferenceSetupService
{
    private readonly PathResolver _paths;
    private readonly PythonRunner _python;
    private readonly FileSystemService _files;
    private readonly ILogger<ReferenceSetupService> _logger;

    // Generous estimates matching python/scripts/setup_reference.py's own GENOME_SOURCES —
    // kept in sync manually since one is a pre-flight check and the other the actual worker.
    private static readonly Dictionary<string, long> RequiredBytesByGenome = new()
    {
        ["chr21_test"] = 600L * 1024 * 1024,
        ["GRCh38"] = 40L * 1024 * 1024 * 1024,
    };

    // Don't let a reference download eat into the last few GB of disk even if the
    // estimate above is a bit optimistic — mirrors CLAUDE.md's "stop below ~2GB free" rule,
    // with extra headroom since this is a much larger write than anything else in the app.
    private const long SafetyMarginBytes = 5L * 1024 * 1024 * 1024;

    public ReferenceSetupService(PathResolver paths, PythonRunner python, FileSystemService files, ILogger<ReferenceSetupService> logger)
    {
        _paths = paths;
        _python = python;
        _files = files;
        _logger = logger;
    }

    public bool IsReady(string genome)
    {
        var fasta = _paths.GetReferenceFasta(genome);
        var bwaIndexMarker = fasta + ".bwt.2bit.64";
        return File.Exists(fasta) && File.Exists(bwaIndexMarker);
    }

    public string GetSalmonIndexDir(string genome) => Path.Combine(_paths.GetReferenceDir(genome), "salmon_index");
    public string GetTx2GenePath(string genome) => Path.Combine(_paths.GetReferenceDir(genome), "tx2gene.tsv");

    /// <summary>Whether the Salmon transcriptome index + tx2gene mapping (step 9's expression
    /// quantification) are ready — independent of the DNA/bwa-mem2 side of the reference.</summary>
    public bool IsRnaReferenceReady(string genome) =>
        File.Exists(Path.Combine(GetSalmonIndexDir(genome), "info.json")) && File.Exists(GetTx2GenePath(genome));

    /// <summary>Per-asset readiness for a genome, so the dashboard can show what's missing
    /// before a run fails ten minutes in (docs/CORRECTION_PLAN.md §6, ToolsController).</summary>
    public ReferenceStatus GetStatus(string genome) => new()
    {
        Genome = genome,
        FastaReady = IsReady(genome),
        RnaReady = IsRnaReferenceReady(genome),
        IntervalsPresent = File.Exists(_paths.GetIntervalsPath(genome)),
        PanelOfNormalsPresent = File.Exists(_paths.GetPanelOfNormals(genome)),
    };

    public long EstimateRequiredBytes(string genome) =>
        RequiredBytesByGenome.TryGetValue(genome, out var bytes) ? bytes : RequiredBytesByGenome["GRCh38"];

    public bool HasEnoughDiskSpace(string genome) =>
        _files.GetAvailableDiskBytes() - EstimateRequiredBytes(genome) > SafetyMarginBytes;

    /// <summary>Human-readable reason a reference isn't ready and can't be auto-fetched,
    /// or null if it's either already ready or fetchable. Used by ValidateInputs to fail
    /// fast rather than starting a job doomed to run out of disk partway through.</summary>
    public string? DescribeBlocker(string genome)
    {
        if (IsReady(genome))
            return null;
        if (HasEnoughDiskSpace(genome))
            return null;

        var availableGb = _files.GetAvailableDiskBytes() / (1024.0 * 1024 * 1024);
        var neededGb = EstimateRequiredBytes(genome) / (1024.0 * 1024 * 1024);
        var marginGb = SafetyMarginBytes / (1024.0 * 1024 * 1024);
        return $"Reference genome '{genome}' is not present and there isn't enough free disk space to download it " +
               $"(need ~{neededGb:F0}GB plus a {marginGb:F0}GB safety margin, have {availableGb:F1}GB free).";
    }

    /// <summary>Downloads and indexes the reference if missing, checking disk space first.
    /// Long-running (a full GRCh38 fetch+index can take well over an hour) — callers should
    /// only invoke this from an async job, not a synchronous request.</summary>
    public async Task<(bool Ready, string? Error)> EnsureReferenceAsync(string genome, bool includeRna, string patientId, CancellationToken ct)
    {
        if (IsReady(genome) && (!includeRna || IsRnaReferenceReady(genome)))
            return (true, null);

        var blocker = DescribeBlocker(genome);
        if (blocker is not null)
            return (false, blocker);

        var args = new Dictionary<string, string>
        {
            ["genome"] = genome,
            ["output-dir"] = _paths.GetReferenceDir(genome),
            ["include-rna"] = includeRna ? "true" : "false",
        };

        try
        {
            var response = await _python.RunAndParseAsync(
                "setup_reference.py", args,
                new PythonExecutionOptions { TimeoutSeconds = 14400, CancellationToken = ct }, // up to 4h for a full genome
                patientId: patientId);
            return response.Success ? (true, null) : (false, response.Error);
        }
        catch (Exceptions.PythonExecutionException ex)
        {
            return (false, ex.Stderr);
        }
    }
}

public class ReferenceStatus
{
    public string Genome { get; set; } = "";
    public bool FastaReady { get; set; }
    public bool RnaReady { get; set; }
    public bool IntervalsPresent { get; set; }
    public bool PanelOfNormalsPresent { get; set; }
}
