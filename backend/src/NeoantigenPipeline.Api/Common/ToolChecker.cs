using System.Collections.Concurrent;
using System.Diagnostics;
using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Common;

public class ToolChecker
{
    private readonly AppConfig _config;
    private readonly ILogger<ToolChecker> _logger;
    private readonly ConcurrentDictionary<string, ToolStatus> _cache = new();

    private static readonly Dictionary<string, string[]> UsedBy = new()
    {
        ["bwa-mem2"] = new[] { PipelineStepIds.Alignment },
        ["STAR"] = new[] { PipelineStepIds.Alignment },
        ["samtools"] = new[] { PipelineStepIds.Alignment, PipelineStepIds.Variants },
        ["gatk"] = new[] { PipelineStepIds.Variants },
        ["vep"] = new[] { PipelineStepIds.ProteinEffects },
        ["OptiType"] = new[] { PipelineStepIds.HlaTyping },
        ["mhcflurry"] = new[] { PipelineStepIds.Presentation },
        ["pvacseq"] = new[] { PipelineStepIds.Candidates },
        ["pvacvector"] = new[] { PipelineStepIds.VaccineDesign },
    };

    public ToolChecker(AppConfig config, ILogger<ToolChecker> logger)
    {
        _config = config;
        _logger = logger;
    }

    public ToolStatus Check(string toolName) => _cache.GetOrAdd(toolName, ProbeTool);

    public List<ToolStatus> CheckAll() => UsedBy.Keys.Select(Check).ToList();

    public bool IsAvailable(string toolName) => Check(toolName).IsAvailable;

    public List<string> GetMissingTools(string[] requiredTools) =>
        requiredTools.Where(t => !IsAvailable(t)).ToList();

    public void InvalidateCache() => _cache.Clear();

    private ToolStatus ProbeTool(string toolName)
    {
        var path = _config.GetToolPath(toolName);
        var versionCmd = GetVersionCommand(toolName);
        var status = new ToolStatus
        {
            ToolName = toolName,
            ResolvedPath = path,
            UsedBySteps = UsedBy.TryGetValue(toolName, out var steps) ? steps : Array.Empty<string>(),
        };

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add(versionCmd ?? "--version");

            using var process = Process.Start(psi);
            if (process is null)
            {
                status.IsAvailable = false;
                status.Error = "Failed to start process";
                return status;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            status.IsAvailable = true;
            status.Version = string.IsNullOrWhiteSpace(stdout) ? stderr.Trim() : stdout.Trim();
        }
        catch (Exception ex)
        {
            status.IsAvailable = false;
            status.Error = ex.Message;
        }

        return status;
    }

    private static string? GetVersionCommand(string toolName) => toolName switch
    {
        "gatk" => "--version",
        "vep" => "--help",
        "OptiType" => "--help",
        _ => "--version",
    };
}
