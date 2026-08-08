namespace NeoantigenPipeline.Api.Common;

public enum PatientLogLevel { Info, Warning, Error }

/// <summary>
/// Appends a human-readable event log to <c>{patientDir}/patient.log</c> — every step
/// attempt, Python invocation/response, warning (e.g. an empty output file), success/
/// failure with duration, and every upload/download. Built so a patient's whole run
/// history can be read back (or pasted somewhere) without re-running anything, which
/// matters once this runs unattended on a remote server.
/// </summary>
public class PatientLogger
{
    private readonly PathResolver _paths;
    private readonly object _writeLock = new();

    public PatientLogger(PathResolver paths)
    {
        _paths = paths;
    }

    public void Info(string patientId, string category, string message) => Write(patientId, PatientLogLevel.Info, category, message);
    public void Warning(string patientId, string category, string message) => Write(patientId, PatientLogLevel.Warning, category, message);
    public void Error(string patientId, string category, string message) => Write(patientId, PatientLogLevel.Error, category, message);

    public void Write(string patientId, PatientLogLevel level, string category, string message)
    {
        var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}Z] {level.ToString().ToUpperInvariant(),-7} {category,-18} {message}";
        var dir = _paths.GetPatientDir(patientId);
        var path = Path.Combine(dir, "patient.log");

        // A simple in-process lock is enough here — log writes are small, infrequent
        // relative to step runtimes, and don't need to survive multi-process deployment.
        lock (_writeLock)
        {
            Directory.CreateDirectory(dir);
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }

    public string GetLogPath(string patientId) => Path.Combine(_paths.GetPatientDir(patientId), "patient.log");

    public string ReadTail(string patientId, int maxLines = 500)
    {
        var path = GetLogPath(patientId);
        if (!File.Exists(path))
            return "";
        var lines = File.ReadAllLines(path);
        return string.Join(Environment.NewLine, lines.Length > maxLines ? lines[^maxLines..] : lines);
    }
}
