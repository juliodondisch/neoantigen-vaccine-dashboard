using System.Diagnostics;
using System.Text;
using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Common;

public class PythonExecutionOptions
{
    public int TimeoutSeconds { get; set; } = 3600;
    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string>? EnvironmentVariables { get; set; }
    public Action<string>? OnStdoutLine { get; set; }
    public Action<string>? OnStderrLine { get; set; }
    public CancellationToken CancellationToken { get; set; }
}

public class PythonExecutionResult
{
    public int ExitCode { get; set; }
    public string Stdout { get; set; } = "";
    public string Stderr { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public bool TimedOut { get; set; }
    public bool Success => ExitCode == 0 && !TimedOut;
}

public class PythonRunner
{
    private readonly AppConfig _config;
    private readonly PathResolver _paths;
    private readonly PatientLogger _patientLog;
    private readonly ILogger<PythonRunner> _logger;

    public PythonRunner(AppConfig config, PathResolver paths, PatientLogger patientLog, ILogger<PythonRunner> logger)
    {
        _config = config;
        _paths = paths;
        _patientLog = patientLog;
        _logger = logger;
    }

    public async Task<PythonExecutionResult> RunAsync(string scriptName, Dictionary<string, string> args, PythonExecutionOptions? options = null, string? patientId = null)
    {
        var scriptPath = _paths.GetPythonScript(scriptName);
        var commandParts = new List<string> { _config.PythonExecutable, scriptPath };
        foreach (var (key, value) in args)
        {
            // Omit the flag entirely when unset, rather than passing an empty value -
            // argparse options are typed (str/bool) and an empty positional breaks parsing.
            if (string.IsNullOrEmpty(value))
                continue;
            commandParts.Add($"--{key}");
            commandParts.Add(value);
        }

        if (patientId is not null)
            _patientLog.Info(patientId, scriptName, $"Invoking: {string.Join(' ', commandParts.Skip(1))}");

        var result = await RunRawAsync(commandParts.ToArray(), options);

        if (patientId is not null)
        {
            if (result.TimedOut)
                _patientLog.Error(patientId, scriptName, $"Timed out after {result.Duration.TotalSeconds:F1}s");
            else if (result.ExitCode != 0)
                _patientLog.Error(patientId, scriptName, $"Exited {result.ExitCode} after {result.Duration.TotalSeconds:F1}s: {Tail(result.Stderr)}");
            else
                _patientLog.Info(patientId, scriptName, $"Exited 0 after {result.Duration.TotalSeconds:F1}s");
        }

        return result;
    }

    public async Task<PythonResponse> RunAndParseAsync(string scriptName, Dictionary<string, string> args, PythonExecutionOptions? options = null, string? patientId = null)
    {
        var result = await RunAsync(scriptName, args, options, patientId);
        if (!result.Success)
        {
            throw new Exceptions.PythonExecutionException(scriptName, result.ExitCode, result.Stderr);
        }

        if (!PythonResponse.TryParse(result.Stdout, out var response) || response is null)
        {
            var detail = $"Script exited 0 but produced no parseable JSON block. Stdout tail: {Tail(result.Stdout)}";
            if (patientId is not null)
                _patientLog.Error(patientId, scriptName, detail);
            throw new Exceptions.PythonExecutionException(scriptName, result.ExitCode, detail);
        }

        if (patientId is not null)
        {
            if (response.Success)
                _patientLog.Info(patientId, scriptName, $"Response: {response.Message}");
            else
                _patientLog.Warning(patientId, scriptName, $"Response reported failure: {response.Error}");
        }

        return response;
    }

    public async Task<PythonExecutionResult> RunRawAsync(string[] commandParts, PythonExecutionOptions? options = null)
    {
        options ??= new PythonExecutionOptions();
        var stopwatch = Stopwatch.StartNew();

        var psi = new ProcessStartInfo
        {
            FileName = commandParts[0],
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = options.WorkingDirectory ?? Directory.GetCurrentDirectory(),
        };
        for (var i = 1; i < commandParts.Length; i++)
            psi.ArgumentList.Add(commandParts[i]);

        if (options.EnvironmentVariables is not null)
            foreach (var (k, v) in options.EnvironmentVariables)
                psi.Environment[k] = v;

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stdout.AppendLine(e.Data);
            options.OnStdoutLine?.Invoke(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stderr.AppendLine(e.Data);
            options.OnStderrLine?.Invoke(e.Data);
        };

        _logger.LogInformation("Running: {Command}", string.Join(' ', commandParts));

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new PythonExecutionResult
            {
                ExitCode = -1,
                Stderr = $"Failed to start process '{commandParts[0]}': {ex.Message}",
                Duration = stopwatch.Elapsed,
            };
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, options.CancellationToken);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = !options.CancellationToken.IsCancellationRequested;
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
        }

        stopwatch.Stop();

        return new PythonExecutionResult
        {
            ExitCode = timedOut ? -1 : process.ExitCode,
            Stdout = stdout.ToString(),
            Stderr = timedOut ? stderr + "\n[process killed: exceeded timeout]" : stderr.ToString(),
            Duration = stopwatch.Elapsed,
            TimedOut = timedOut,
        };
    }

    private static string Tail(string text, int maxChars = 500) =>
        text.Length <= maxChars ? text : text[^maxChars..];
}
