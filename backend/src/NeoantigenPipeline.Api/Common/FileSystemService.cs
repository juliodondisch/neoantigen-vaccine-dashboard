using System.Text.Json;
using System.Text.RegularExpressions;
using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Common;

public class FileSystemService
{
    private readonly PathResolver _paths;
    private readonly ILogger<FileSystemService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public FileSystemService(PathResolver paths, ILogger<FileSystemService> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public List<ManagedFile> ListStepFiles(string patientId, string stepId) =>
        ListStepFiles(patientId, stepId, "*");

    public List<ManagedFile> ListStepFiles(string patientId, string stepId, string globPattern)
    {
        var dir = _paths.GetStepDir(patientId, stepId);
        if (!Directory.Exists(dir))
            return new List<ManagedFile>();

        var regex = GlobToRegex(globPattern);

        return new DirectoryInfo(dir)
            .GetFiles()
            .Where(f => regex.IsMatch(f.Name))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(f => ToManagedFile(f, stepId))
            .ToList();
    }

    public ManagedFile? FindLatestFile(string patientId, string stepId, string globPattern) =>
        ListStepFiles(patientId, stepId, globPattern).OrderByDescending(f => f.CreatedAt).FirstOrDefault();

    public List<ManagedFile> FindFiles(string patientId, string stepId, params string[] globPatterns)
    {
        var results = new List<ManagedFile>();
        foreach (var pattern in globPatterns)
            results.AddRange(ListStepFiles(patientId, stepId, pattern));
        return results.DistinctBy(f => f.RelativePath).ToList();
    }

    public bool StepHasFiles(string patientId, string stepId) => ListStepFiles(patientId, stepId).Count > 0;

    public bool StepHasFilesMatching(string patientId, string stepId, string globPattern) =>
        ListStepFiles(patientId, stepId, globPattern).Count > 0;

    public long GetStepSizeBytes(string patientId, string stepId) =>
        ListStepFiles(patientId, stepId).Sum(f => f.SizeBytes);

    public long GetPatientSizeBytes(string patientId) =>
        PipelineStepIds.All.Sum(stepId => GetStepSizeBytes(patientId, stepId));

    public async Task<ManagedFile> SaveUploadAsync(string patientId, string stepId, IFormFile file, string? fileKind = null)
    {
        var dir = _paths.EnsureStepDir(patientId, stepId);
        var ext = Path.GetExtension(file.FileName);
        var baseName = Path.GetFileNameWithoutExtension(file.FileName);

        // BAMs are located downstream by glob (tumor_*.bam / normal_*.bam), not by
        // original filename ,  canonicalize the base name by fileKind so a BAM uploaded
        // as e.g. "sample1.bam" is still findable regardless of what the user named it.
        // This applies whether the BAM lands in 01_upload or is uploaded directly into
        // 02_alignment (skipping alignment entirely when the caller already has BAMs).
        if (ext.Equals(".bam", StringComparison.OrdinalIgnoreCase))
        {
            baseName = fileKind switch
            {
                "tumor_dna" => "tumor",
                "normal_dna" => "normal",
                "rna" => "rna",
                _ => baseName,
            };
        }

        var destName = $"{baseName}_{PathResolver.Timestamp()}{ext}";
        var destPath = Path.Combine(dir, destName);

        await using (var stream = new FileStream(destPath, FileMode.CreateNew))
        {
            await file.CopyToAsync(stream);
        }

        var info = new FileInfo(destPath);
        var managed = ToManagedFile(info, stepId);
        managed.FileKind = fileKind ?? InferFileKind(destName);
        managed.IsUserUploaded = true;
        return managed;
    }

    public Task<ManagedFile> RegisterExternalFileAsync(string patientId, string stepId, string sourcePath, string? fileKind = null, bool copy = false)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Source path does not exist: {sourcePath}", sourcePath);

        var dir = _paths.EnsureStepDir(patientId, stepId);
        ManagedFile managed;

        if (copy)
        {
            var ext = Path.GetExtension(sourcePath);
            var baseName = Path.GetFileNameWithoutExtension(sourcePath);
            var destName = $"{baseName}_{PathResolver.Timestamp()}{ext}";
            var destPath = Path.Combine(dir, destName);
            File.Copy(sourcePath, destPath);
            managed = ToManagedFile(new FileInfo(destPath), stepId);
        }
        else
        {
            // Registered in place: recorded via a pointer manifest rather than moved,
            // since source files may be far too large to copy (150GB+ WGS files).
            managed = ToManagedFile(new FileInfo(sourcePath), stepId);
            managed.RelativePath = sourcePath;
        }

        managed.FileKind = fileKind ?? InferFileKind(sourcePath);
        managed.IsUserUploaded = true;
        return Task.FromResult(managed);
    }

    public Stream OpenRead(string patientId, string stepId, string fileName)
    {
        var path = ResolveExistingFile(patientId, stepId, fileName);
        return new FileStream(path, FileMode.Open, FileAccess.Read);
    }

    public bool DeleteFile(string patientId, string stepId, string fileName)
    {
        var dir = _paths.GetStepDir(patientId, stepId);
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

    public void WriteJson<T>(string patientId, string stepId, string fileName, T content)
    {
        var dir = _paths.EnsureStepDir(patientId, stepId);
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(content, JsonOptions));
    }

    public T? ReadJson<T>(string patientId, string stepId, string fileName)
    {
        var dir = _paths.GetStepDir(patientId, stepId);
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
            return default;
        var text = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(text, JsonOptions);
    }

    public string? ReadTextFile(string patientId, string stepId, string fileName, int maxBytes = 1_000_000)
    {
        var path = ResolveExistingFile(patientId, stepId, fileName, throwIfMissing: false);
        if (path is null)
            return null;

        var info = new FileInfo(path);
        if (info.Length <= maxBytes)
            return File.ReadAllText(path);

        var buffer = new byte[maxBytes];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        var read = stream.Read(buffer, 0, maxBytes);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
    }

    public long GetAvailableDiskBytes()
    {
        var root = Directory.Exists(_paths.GetPatientsRoot())
            ? Path.GetPathRoot(Path.GetFullPath(_paths.GetPatientsRoot()))
            : Path.GetPathRoot(Directory.GetCurrentDirectory());
        if (root is null)
            return -1;
        var drive = new DriveInfo(root);
        return drive.AvailableFreeSpace;
    }

    private string ResolveExistingFile(string patientId, string stepId, string fileName, bool throwIfMissing = true)
    {
        var dir = _paths.GetStepDir(patientId, stepId);
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
        {
            if (throwIfMissing)
                throw new FileNotFoundException($"File not found: {fileName} in {stepId}", path);
            return null!;
        }
        return path;
    }

    private static Regex GlobToRegex(string glob)
    {
        var pattern = "^" + Regex.Escape(glob).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return new Regex(pattern, RegexOptions.IgnoreCase);
    }

    private static ManagedFile ToManagedFile(FileInfo info, string stepId) => new()
    {
        Name = info.Name,
        RelativePath = $"{stepId}/{info.Name}",
        SizeBytes = info.Length,
        CreatedAt = info.CreationTimeUtc,
        ModifiedAt = info.LastWriteTimeUtc,
        Extension = info.Extension,
        FileKind = InferFileKind(info.Name),
        // Not persisted metadata ,  approximated from file kind, since only the upload
        // step's tumor/normal/rna files are ever user-supplied rather than pipeline-generated.
        IsUserUploaded = InferFileKind(info.Name) is "tumor_dna" or "normal_dna" or "rna" && stepId == PipelineStepIds.Upload,
    };

    private static string? InferFileKind(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        if (lower.Contains("tumor_dna") || lower.Contains("tumor_r1") || lower.Contains("tumor_r2"))
            return "tumor_dna";
        if (lower.Contains("normal_dna") || lower.Contains("normal_r1") || lower.Contains("normal_r2"))
            return "normal_dna";
        if (lower.Contains("rna"))
            return "rna";
        if (lower.EndsWith(".log"))
            return "log";
        if (lower.Contains("summary"))
            return "summary";
        return "output";
    }
}
