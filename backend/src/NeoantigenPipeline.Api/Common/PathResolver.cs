namespace NeoantigenPipeline.Api.Common;

public class PathResolver
{
    private readonly AppConfig _config;

    public PathResolver(AppConfig config)
    {
        _config = config;
    }

    public string GetPatientsRoot() => Path.Combine(_config.DataRoot, "patients");

    public string GetPatientDir(string patientId)
    {
        ValidateSegment(patientId, nameof(patientId));
        return Path.Combine(GetPatientsRoot(), patientId);
    }

    public string GetPatientJsonPath(string patientId) => Path.Combine(GetPatientDir(patientId), "patient.json");

    public string GetStepDir(string patientId, string stepId)
    {
        ValidateSegment(stepId, nameof(stepId));
        return Path.Combine(GetPatientDir(patientId), stepId);
    }

    public string GetJobsDir(string patientId) => Path.Combine(GetPatientDir(patientId), "_jobs");

    public string GetJobPath(string patientId, string jobId)
    {
        ValidateSegment(jobId, nameof(jobId));
        return Path.Combine(GetJobsDir(patientId), $"{jobId}.json");
    }

    public string GetReferenceDir(string genomeName)
    {
        ValidateSegment(genomeName, nameof(genomeName));
        return Path.Combine(_config.ReferenceRoot, genomeName);
    }

    public string GetReferenceFasta(string genomeName)
    {
        var fastaName = genomeName == "chr21_test" ? "chr21.fa" : $"{genomeName}.fa";
        return Path.Combine(GetReferenceDir(genomeName), fastaName);
    }

    public string GetPanelOfNormals(string genomeName) =>
        Path.Combine(GetReferenceDir(genomeName), "panel_of_normals.vcf.gz");

    public string GetProteomeFasta(bool useMini = false) =>
        Path.Combine(_config.ReferenceRoot, "proteome", useMini ? "mini_proteome.fasta" : "uniprot_human.fasta");

    public string GetPythonScript(string scriptName) => Path.Combine(_config.PythonScriptsRoot, scriptName);

    public string EnsureStepDir(string patientId, string stepId)
    {
        var dir = GetStepDir(patientId, stepId);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public void EnsurePatientSkeleton(string patientId)
    {
        Directory.CreateDirectory(GetPatientDir(patientId));
        Directory.CreateDirectory(GetJobsDir(patientId));
        foreach (var stepId in PipelineStepIds.All)
        {
            Directory.CreateDirectory(GetStepDir(patientId, stepId));
        }
    }

    public string BuildOutputPath(string patientId, string stepId, string baseName, string extension)
    {
        var dir = EnsureStepDir(patientId, stepId);
        var ext = extension.StartsWith('.') ? extension : $".{extension}";
        return Path.Combine(dir, $"{baseName}_{Timestamp()}{ext}");
    }

    public static string Timestamp() => DateTime.Now.ToString("yyyyMMdd_HHmmss");

    public bool IsPathWithinDataRoot(string path)
    {
        var fullDataRoot = Path.GetFullPath(_config.DataRoot);
        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(fullDataRoot, StringComparison.Ordinal);
    }

    private static void ValidateSegment(string segment, string paramName)
    {
        if (string.IsNullOrWhiteSpace(segment))
            throw new ArgumentException("Value cannot be empty.", paramName);
        if (segment.Contains("..") || segment.Contains('/') || segment.Contains('\\'))
            throw new ArgumentException($"Value contains illegal path characters: '{segment}'.", paramName);
    }
}

public static class PipelineStepIds
{
    public const string Upload = "01_upload";
    public const string Alignment = "02_alignment";
    public const string Variants = "03_variants";
    public const string ProteinEffects = "04_protein_effects";
    public const string HlaTyping = "05_hla_typing";
    public const string Candidates = "06_candidates";
    public const string Presentation = "07_presentation";
    public const string Immunogenicity = "08_immunogenicity";
    public const string Filtering = "09_filtering";
    public const string Ranking = "10_ranking";
    public const string VaccineDesign = "11_vaccine_design";

    public static readonly string[] All =
    {
        Upload, Alignment, Variants, ProteinEffects, HlaTyping,
        Candidates, Presentation, Immunogenicity, Filtering, Ranking, VaccineDesign,
    };
}
