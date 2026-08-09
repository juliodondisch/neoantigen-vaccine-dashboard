using NeoantigenPipeline.Api.Common.Exceptions;
using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Models.Dto;

namespace NeoantigenPipeline.Api.Common;

public class PatientRepository
{
    private readonly PathResolver _paths;
    private readonly FileSystemService _files;
    private readonly StepRegistry _registry;
    private readonly PatientLogger _patientLog;
    private readonly AppConfig _config;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public PatientRepository(PathResolver paths, FileSystemService files, StepRegistry registry, PatientLogger patientLog, AppConfig config)
    {
        _paths = paths;
        _files = files;
        _registry = registry;
        _patientLog = patientLog;
        _config = config;
    }

    public Task<List<PatientSummary>> ListAsync()
    {
        var root = _paths.GetPatientsRoot();
        if (!Directory.Exists(root))
            return Task.FromResult(new List<PatientSummary>());

        var summaries = new List<PatientSummary>();
        foreach (var dir in Directory.GetDirectories(root))
        {
            var patientId = Path.GetFileName(dir);
            var jsonPath = _paths.GetPatientJsonPath(patientId);
            if (!File.Exists(jsonPath))
                continue;
            var patient = System.Text.Json.JsonSerializer.Deserialize<Patient>(File.ReadAllText(jsonPath));
            if (patient is null)
                continue;
            summaries.Add(BuildSummaryAsync(patient).GetAwaiter().GetResult());
        }
        return Task.FromResult(summaries.OrderByDescending(s => s.CreatedAt).ToList());
    }

    public Task<Patient?> GetAsync(string patientId)
    {
        var jsonPath = _paths.GetPatientJsonPath(patientId);
        if (!File.Exists(jsonPath))
            return Task.FromResult<Patient?>(null);
        var patient = System.Text.Json.JsonSerializer.Deserialize<Patient>(File.ReadAllText(jsonPath));
        return Task.FromResult(patient);
    }

    public async Task<Patient> CreateAsync(CreatePatientRequest request)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Notes = request.Notes,
            CancerType = request.CancerType,
            ReferenceGenome = request.ReferenceGenome ?? _config.DefaultReferenceGenome,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _paths.EnsurePatientSkeleton(patient.Id);
        await SaveAsync(patient);
        _patientLog.Info(patient.Id, "patient", $"Created patient '{patient.Name}' (cancerType={patient.CancerType ?? "unspecified"}, referenceGenome={patient.ReferenceGenome})");
        return patient;
    }

    public async Task<Patient> UpdateAsync(string patientId, UpdatePatientRequest request)
    {
        var patient = await GetAsync(patientId) ?? throw new PatientNotFoundException(patientId);

        if (request.Name is not null) patient.Name = request.Name;
        if (request.Notes is not null) patient.Notes = request.Notes;
        if (request.CancerType is not null) patient.CancerType = request.CancerType;
        patient.UpdatedAt = DateTime.UtcNow;

        await SaveAsync(patient);
        return patient;
    }

    public Task<bool> DeleteAsync(string patientId, bool deleteFiles = false)
    {
        var dir = _paths.GetPatientDir(patientId);
        if (!Directory.Exists(dir))
            return Task.FromResult(false);

        if (deleteFiles)
        {
            Directory.Delete(dir, recursive: true);
        }
        else
        {
            File.Delete(_paths.GetPatientJsonPath(patientId));
        }
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(string patientId) => Task.FromResult(File.Exists(_paths.GetPatientJsonPath(patientId)));

    public Task<PatientSummary> BuildSummaryAsync(Patient patient)
    {
        var definitions = _registry.GetAllDefinitions();
        var completed = 0;
        string? furthest = null;

        foreach (var def in definitions)
        {
            if (_files.StepHasFiles(patient.Id, def.Id))
            {
                completed++;
                furthest = def.Id;
            }
        }

        return Task.FromResult(new PatientSummary
        {
            Id = patient.Id,
            Name = patient.Name,
            CancerType = patient.CancerType,
            CreatedAt = patient.CreatedAt,
            CompletedSteps = completed,
            TotalSteps = definitions.Count,
            FurthestStepId = furthest,
            TotalDiskBytes = _files.GetPatientSizeBytes(patient.Id),
        });
    }

    private async Task SaveAsync(Patient patient)
    {
        await _writeLock.WaitAsync();
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(patient, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_paths.GetPatientJsonPath(patient.Id), json);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
