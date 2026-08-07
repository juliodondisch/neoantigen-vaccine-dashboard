# Neoantigen Pipeline ,  Technical Specification

> Companion to `PROJECT_PLAN.md`. This document defines every file, class, field, and function signature in the codebase.

**Stack:** Next.js 14+ (App Router) + React + TypeScript + Zustand + Tailwind · ASP.NET Core 8 (C#) · Python 3.11

---

## Table of Contents

1. [Repository Structure](#1-repository-structure)
2. [Data Directory Structure](#2-data-directory-structure)
3. [Backend ,  Models & Enums](#3-backend--models--enums)
4. [Backend ,  Common Infrastructure](#4-backend--common-infrastructure)
5. [Backend ,  Controllers](#5-backend--controllers)
6. [Backend ,  Step Services](#6-backend--step-services)
7. [Backend ,  Testing](#7-backend--testing)
8. [Python Scripts](#8-python-scripts)
9. [Frontend ,  Types](#9-frontend--types)
10. [Frontend ,  Zustand Stores](#10-frontend--zustand-stores)
11. [Frontend ,  API Client](#11-frontend--api-client)
12. [Frontend ,  Components](#12-frontend--components)
13. [Frontend ,  Pages](#13-frontend--pages)
14. [API Contract Reference](#14-api-contract-reference)
15. [Appendix C ,  Design System](#appendix-c--design-system)

---

## 1. Repository Structure

```
neoantigen-pipeline/
├── README.md
├── CLAUDE.md
├── docker-compose.yml
├── .env.example
│
├── backend/
│   ├── NeoantigenPipeline.sln
│   ├── src/
│   │   └── NeoantigenPipeline.Api/
│   │       ├── NeoantigenPipeline.Api.csproj
│   │       ├── Program.cs
│   │       ├── appsettings.json
│   │       ├── appsettings.Development.json
│   │       │
│   │       ├── Controllers/
│   │       │   ├── PatientsController.cs
│   │       │   ├── StepsController.cs
│   │       │   ├── FilesController.cs
│   │       │   ├── ToolsController.cs
│   │       │   └── DevTestsController.cs
│   │       │
│   │       ├── Models/
│   │       │   ├── Patient.cs
│   │       │   ├── PatientSummary.cs
│   │       │   ├── StepDefinition.cs
│   │       │   ├── StepState.cs
│   │       │   ├── ManagedFile.cs
│   │       │   ├── StepResult.cs
│   │       │   ├── ValidationResult.cs
│   │       │   ├── StepParameters.cs
│   │       │   ├── JobRecord.cs
│   │       │   ├── ToolStatus.cs
│   │       │   ├── PythonResponse.cs
│   │       │   └── Dto/
│   │       │       ├── CreatePatientRequest.cs
│   │       │       ├── UpdatePatientRequest.cs
│   │       │       ├── RunStepRequest.cs
│   │       │       ├── RunStepResponse.cs
│   │       │       ├── StepStatusResponse.cs
│   │       │       └── UploadResponse.cs
│   │       │
│   │       ├── Common/
│   │       │   ├── IPipelineStep.cs
│   │       │   ├── PipelineStepBase.cs
│   │       │   ├── StepRegistry.cs
│   │       │   ├── PathResolver.cs
│   │       │   ├── FileSystemService.cs
│   │       │   ├── PythonRunner.cs
│   │       │   ├── PatientRepository.cs
│   │       │   ├── JobManager.cs
│   │       │   ├── ToolChecker.cs
│   │       │   ├── AppConfig.cs
│   │       │   └── Exceptions/
│   │       │       ├── PipelineException.cs
│   │       │       ├── StepValidationException.cs
│   │       │       ├── PythonExecutionException.cs
│   │       │       └── PatientNotFoundException.cs
│   │       │
│   │       └── Services/
│   │           ├── 01_Upload/UploadService.cs
│   │           ├── 02_Alignment/AlignmentService.cs
│   │           ├── 03_VariantCalling/VariantCallingService.cs
│   │           ├── 04_ProteinEffects/ProteinEffectsService.cs
│   │           ├── 05_HlaTyping/HlaTypingService.cs
│   │           ├── 06_CandidateGeneration/
│   │           │   ├── CandidateGenerationService.cs
│   │           │   └── SlidingWindowGenerator.cs
│   │           ├── 07_Presentation/PresentationService.cs
│   │           ├── 08_Immunogenicity/ImmunogenicityService.cs
│   │           ├── 09_Filtering/FilteringService.cs
│   │           ├── 10_Ranking/
│   │           │   ├── RankingService.cs
│   │           │   ├── ScoreCalculator.cs
│   │           │   └── HlaSpreadSelector.cs
│   │           └── 11_VaccineDesign/VaccineDesignService.cs
│   │
│   └── tests/
│       └── NeoantigenPipeline.Tests/
│           ├── NeoantigenPipeline.Tests.csproj
│           ├── Unit/
│           │   ├── PathResolverTests.cs
│           │   ├── SlidingWindowGeneratorTests.cs
│           │   ├── ScoreCalculatorTests.cs
│           │   ├── HlaSpreadSelectorTests.cs
│           │   ├── PythonResponseParsingTests.cs
│           │   └── ValidationTests.cs
│           ├── Integration/
│           │   ├── StepIntegrationTestBase.cs
│           │   ├── AlignmentIntegrationTests.cs
│           │   ├── VariantCallingIntegrationTests.cs
│           │   ├── ProteinEffectsIntegrationTests.cs
│           │   ├── HlaTypingIntegrationTests.cs
│           │   ├── PresentationIntegrationTests.cs
│           │   └── VaccineDesignIntegrationTests.cs
│           ├── EndToEnd/
│           │   └── FullPipelineTests.cs
│           └── Fixtures/
│               ├── FixtureSeeder.cs
│               └── data/            (see §2.3)
│
├── python/
│   ├── requirements.txt
│   ├── common/
│   │   ├── __init__.py
│   │   ├── io_utils.py
│   │   ├── response.py
│   │   └── config.py
│   ├── scripts/
│   │   ├── align.py
│   │   ├── call_variants.py
│   │   ├── annotate_effects.py
│   │   ├── type_hla.py
│   │   ├── generate_candidates.py
│   │   ├── predict_presentation.py
│   │   ├── predict_immunogenicity.py
│   │   ├── filter_candidates.py
│   │   └── design_vaccine.py
│   └── tools/
│       ├── make_test_data.py
│       └── check_tools.py
│
└── frontend/
    ├── package.json
    ├── tsconfig.json
    ├── next.config.js
    ├── tailwind.config.ts
    ├── .env.local.example
    └── src/
        ├── app/
        │   ├── layout.tsx
        │   ├── page.tsx
        │   ├── globals.css
        │   ├── patients/[patientId]/page.tsx
        │   └── dev/tests/page.tsx
        │
        ├── components/
        │   ├── layout/
        │   │   ├── TopBar.tsx
        │   │   ├── StepSidebar.tsx
        │   │   └── StepSidebarItem.tsx
        │   ├── patients/
        │   │   ├── PatientGrid.tsx
        │   │   ├── PatientCard.tsx
        │   │   └── CreatePatientModal.tsx
        │   ├── steps/
        │   │   ├── StepPanel.tsx
        │   │   ├── StepExplanation.tsx
        │   │   ├── StepRunButton.tsx
        │   │   ├── FileUploadZone.tsx
        │   │   ├── FileTable.tsx
        │   │   ├── panels/
        │   │   │   ├── UploadPanel.tsx
        │   │   │   ├── AlignmentPanel.tsx
        │   │   │   ├── VariantPanel.tsx
        │   │   │   ├── ProteinEffectsPanel.tsx
        │   │   │   ├── HlaTypingPanel.tsx
        │   │   │   ├── CandidatePanel.tsx
        │   │   │   ├── PresentationPanel.tsx
        │   │   │   ├── ImmunogenicityPanel.tsx
        │   │   │   ├── FilteringPanel.tsx
        │   │   │   ├── RankingPanel.tsx
        │   │   │   └── VaccineDesignPanel.tsx
        │   │   └── widgets/
        │   │       ├── WeightSlider.tsx
        │   │       ├── CandidateTable.tsx
        │   │       ├── HlaAlleleList.tsx
        │   │       ├── ConsequenceChart.tsx
        │   │       ├── VafHistogram.tsx
        │   │       └── ConstructDiagram.tsx
        │   ├── common/
        │   │   ├── Toast.tsx
        │   │   ├── ToastContainer.tsx
        │   │   ├── Spinner.tsx
        │   │   ├── Modal.tsx
        │   │   ├── Button.tsx
        │   │   ├── DataTable.tsx
        │   │   └── StatusBadge.tsx
        │   └── dev/
        │       ├── TestHarness.tsx
        │       ├── TestResultRow.tsx
        │       └── ToolStatusPanel.tsx
        │
        ├── lib/
        │   ├── api/
        │   │   ├── client.ts
        │   │   ├── patients.ts
        │   │   ├── steps.ts
        │   │   ├── files.ts
        │   │   └── dev.ts
        │   ├── constants/
        │   │   ├── steps.ts
        │   │   └── config.ts
        │   └── utils/
        │       ├── format.ts
        │       ├── polling.ts
        │       └── cn.ts
        │
        ├── stores/
        │   ├── usePatientStore.ts
        │   ├── useStepStore.ts
        │   ├── useToastStore.ts
        │   ├── useRankingStore.ts
        │   └── useDevStore.ts
        │
        ├── hooks/
        │   ├── useStepPolling.ts
        │   ├── useFileUpload.ts
        │   └── useStepFiles.ts
        │
        └── types/
            ├── patient.ts
            ├── step.ts
            ├── file.ts
            ├── candidate.ts
            └── api.ts
```

---

## 2. Data Directory Structure

### 2.1 Root layout

```
/data
├── config/
│   └── tool-paths.json
├── references/
│   ├── GRCh38/
│   │   ├── GRCh38.fa
│   │   ├── GRCh38.fa.fai
│   │   ├── GRCh38.fa.bwt.2bit.64      (bwa-mem2 index)
│   │   └── panel_of_normals.vcf.gz
│   ├── chr21_test/                     (Tier-1 test reference, ~250MB)
│   │   ├── chr21.fa
│   │   ├── chr21.fa.fai
│   │   └── chr21.fa.bwt.2bit.64
│   ├── vep_cache/                      (optional; several GB)
│   ├── proteome/
│   │   ├── uniprot_human.fasta
│   │   └── mini_proteome.fasta         (50 proteins, test fixture)
│   └── hla/
│       └── optitype_reference/
└── patients/
    └── {patientId}/
        ├── patient.json
        ├── 01_upload/
        ├── 02_alignment/
        ├── 03_variants/
        ├── 04_protein_effects/
        ├── 05_hla_typing/
        ├── 06_candidates/
        ├── 07_presentation/
        ├── 08_immunogenicity/
        ├── 09_filtering/
        ├── 10_ranking/
        ├── 11_vaccine_design/
        └── _jobs/
            └── {jobId}.json
```

### 2.2 Per-step file contents

| Folder | Written by | Files produced |
|---|---|---|
| `01_upload` | user | `tumor_dna_*.fastq.gz`, `normal_dna_*.fastq.gz`, `tumor_rna_*.fastq.gz`, `*.bam`, `_manifest.json` |
| `02_alignment` | align.py | `tumor_{ts}.bam`, `tumor_{ts}.bam.bai`, `normal_{ts}.bam`, `normal_{ts}.bam.bai`, `rna_{ts}.bam`, `align_{ts}.log`, `align_{ts}.summary.json` |
| `03_variants` | call_variants.py | `somatic_{ts}.vcf.gz`, `somatic_{ts}.vcf.gz.tbi`, `somatic_pass_{ts}.vcf.gz`, `variants_{ts}.summary.json` |
| `04_protein_effects` | annotate_effects.py | `annotated_{ts}.vcf.gz`, `protein_altering_{ts}.tsv`, `effects_{ts}.summary.json` |
| `05_hla_typing` | type_hla.py | `hla_{ts}.json`, `optitype_{ts}.tsv`, `hla_{ts}.log` |
| `06_candidates` | generate_candidates.py | `candidates_{ts}.tsv`, `candidates_{ts}.summary.json` |
| `07_presentation` | predict_presentation.py | `presentation_{ts}.tsv`, `presentation_{ts}.summary.json` |
| `08_immunogenicity` | predict_immunogenicity.py | `immunogenicity_{ts}.tsv`, `immunogenicity_{ts}.summary.json` |
| `09_filtering` | filter_candidates.py | `filtered_{ts}.tsv`, `removed_{ts}.tsv`, `filtering_{ts}.summary.json` |
| `10_ranking` | RankingService (C#) | `ranked_{ts}.tsv`, `selected_{ts}.tsv`, `weights_{ts}.json` |
| `11_vaccine_design` | design_vaccine.py | `vaccine_{ts}.fasta`, `vaccine_{ts}.gb`, `construct_{ts}.json` |

`{ts}` = `yyyyMMdd_HHmmss`. Files are never overwritten or deleted.

### 2.3 Test fixture data

```
backend/tests/NeoantigenPipeline.Tests/Fixtures/data/
├── tiny/
│   ├── tumor_R1.fq.gz              (~20MB, chr21 simulated)
│   ├── tumor_R2.fq.gz
│   ├── normal_R1.fq.gz
│   ├── normal_R2.fq.gz
│   └── truth_variants.vcf          (wgsim ground truth)
├── vcf/
│   ├── golden_consequences.vcf     (one variant per consequence type)
│   ├── somatic_pass_20.vcf         (20 PASS variants)
│   └── no_normal.vcf
├── peptides/
│   ├── candidates_100.tsv
│   ├── positive_controls.tsv       (known HLA-A*02:01 epitopes)
│   └── self_peptides.tsv           (verbatim human protein fragments)
├── hla/
│   └── hla_reference.json          (HLA-A*02:01, A*01:01, B*07:02, B*08:01, C*07:01, C*07:02)
├── expression/
│   └── expression_mini.tsv         (gene → TPM, one gene at 0)
└── proteome/
    └── mini_proteome.fasta         (50 proteins)
```

---

## 3. Backend ,  Models & Enums

### `Models/Patient.cs`

```csharp
public class Patient
{
    public string Id { get; set; }                    // GUID string
    public string Name { get; set; }
    public string? Notes { get; set; }
    public string? CancerType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? ReferenceGenome { get; set; }      // "GRCh38" | "chr21_test"
}
```

### `Models/PatientSummary.cs`

```csharp
public class PatientSummary
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? CancerType { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CompletedSteps { get; set; }
    public int TotalSteps { get; set; }
    public string? FurthestStepId { get; set; }
    public long TotalDiskBytes { get; set; }
}
```

### `Models/StepDefinition.cs`

```csharp
public class StepDefinition
{
    public string Id { get; set; }                    // "03_variants"
    public int Order { get; set; }                    // 3
    public string DisplayName { get; set; }           // "Call Somatic Mutations"
    public string ShortDescription { get; set; }
    public string LongExplanation { get; set; }
    public string ToolName { get; set; }              // "Mutect2 (GATK)"
    public string[] RequiredInputStepIds { get; set; }
    public bool IsUploadStep { get; set; }
    public bool HasParameters { get; set; }
    public bool ProducesDownload { get; set; }
    public string[] RequiredTools { get; set; }
}
```

### `Models/StepState.cs`

```csharp
public enum StepStatus
{
    NotStarted,
    InputsMissing,
    Ready,
    Running,
    Completed,
    Failed
}

public class StepState
{
    public string StepId { get; set; }
    public StepStatus Status { get; set; }
    public DateTime? LastRunAt { get; set; }
    public string? LastError { get; set; }
    public int OutputFileCount { get; set; }
    public long OutputBytes { get; set; }
    public string? ActiveJobId { get; set; }
    public Dictionary<string, object>? LastSummary { get; set; }
}
```

### `Models/ManagedFile.cs`

```csharp
public class ManagedFile
{
    public string Name { get; set; }
    public string RelativePath { get; set; }          // "03_variants/somatic_20260806_101500.vcf.gz"
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string Extension { get; set; }
    public string? FileKind { get; set; }             // "tumor_dna" | "normal_dna" | "rna" | "output" | "log" | "summary"
    public bool IsUserUploaded { get; set; }
}
```

### `Models/StepResult.cs`

```csharp
public class StepResult
{
    public bool Success { get; set; }
    public string StepId { get; set; }
    public string? Message { get; set; }
    public string? ErrorDetail { get; set; }
    public List<ManagedFile> OutputFiles { get; set; } = new();
    public Dictionary<string, object> Summary { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public DateTime CompletedAt { get; set; }

    public static StepResult Ok(string stepId, string message, List<ManagedFile> files, Dictionary<string, object> summary, TimeSpan duration);
    public static StepResult Fail(string stepId, string message, string? detail = null);
}
```

### `Models/ValidationResult.cs`

```csharp
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> MissingTools { get; set; } = new();

    public static ValidationResult Valid();
    public static ValidationResult Invalid(params string[] errors);
    public void AddError(string error);
    public void AddWarning(string warning);
    public void AddMissingTool(string toolName);
}
```

### `Models/StepParameters.cs`

```csharp
public class StepParameters
{
    public Dictionary<string, object> Values { get; set; } = new();

    public T? Get<T>(string key, T? defaultValue = default);
    public double GetDouble(string key, double defaultValue = 0);
    public int GetInt(string key, int defaultValue = 0);
    public bool GetBool(string key, bool defaultValue = false);
    public string? GetString(string key, string? defaultValue = null);
    public bool Has(string key);
}
```

### `Models/JobRecord.cs`

```csharp
public enum JobStatus { Queued, Running, Succeeded, Failed, Cancelled }

public class JobRecord
{
    public string JobId { get; set; }
    public string PatientId { get; set; }
    public string StepId { get; set; }
    public JobStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public StepResult? Result { get; set; }
    public string? LogTail { get; set; }
    public int ProgressPercent { get; set; }
}
```

### `Models/ToolStatus.cs`

```csharp
public class ToolStatus
{
    public string ToolName { get; set; }
    public bool IsAvailable { get; set; }
    public string? Version { get; set; }
    public string? ResolvedPath { get; set; }
    public string? Error { get; set; }
    public string[] UsedBySteps { get; set; }
}
```

### `Models/PythonResponse.cs`

```csharp
public class PythonResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public List<string> OutputFiles { get; set; } = new();
    public Dictionary<string, object> Summary { get; set; } = new();

    public static PythonResponse Parse(string stdout);
    public static bool TryParse(string stdout, out PythonResponse? response);
}
```

### `Models/Dto/*.cs`

```csharp
public class CreatePatientRequest
{
    public string Name { get; set; }
    public string? Notes { get; set; }
    public string? CancerType { get; set; }
    public string? ReferenceGenome { get; set; }
}

public class UpdatePatientRequest
{
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public string? CancerType { get; set; }
}

public class RunStepRequest
{
    public Dictionary<string, object>? Parameters { get; set; }
    public bool Async { get; set; } = true;
}

public class RunStepResponse
{
    public string? JobId { get; set; }
    public bool Completed { get; set; }
    public StepResult? Result { get; set; }
}

public class StepStatusResponse
{
    public StepState State { get; set; }
    public JobRecord? ActiveJob { get; set; }
    public List<ManagedFile> InputFiles { get; set; } = new();
    public List<ManagedFile> OutputFiles { get; set; } = new();
}

public class UploadResponse
{
    public bool Success { get; set; }
    public List<ManagedFile> UploadedFiles { get; set; } = new();
    public string? Error { get; set; }
}
```

---

## 4. Backend ,  Common Infrastructure

### `Common/AppConfig.cs`

```csharp
public class AppConfig
{
    public string DataRoot { get; set; }
    public string ReferenceRoot { get; set; }
    public string PythonExecutable { get; set; }
    public string PythonScriptsRoot { get; set; }
    public int DefaultTimeoutSeconds { get; set; }
    public int LongStepTimeoutSeconds { get; set; }
    public bool EnableDevEndpoints { get; set; }
    public Dictionary<string, string> ToolPaths { get; set; } = new();
    public string DefaultReferenceGenome { get; set; }
    public bool UseVepDatabaseMode { get; set; }

    public string GetToolPath(string toolName);
    public void Validate();
}
```

### `Common/PathResolver.cs`

```csharp
public class PathResolver
{
    private readonly AppConfig _config;

    public PathResolver(AppConfig config);

    public string GetPatientsRoot();
    public string GetPatientDir(string patientId);
    public string GetPatientJsonPath(string patientId);
    public string GetStepDir(string patientId, string stepId);
    public string GetJobsDir(string patientId);
    public string GetJobPath(string patientId, string jobId);
    public string GetReferenceDir(string genomeName);
    public string GetReferenceFasta(string genomeName);
    public string GetPanelOfNormals(string genomeName);
    public string GetProteomeFasta(bool useMini = false);
    public string GetPythonScript(string scriptName);
    public string EnsureStepDir(string patientId, string stepId);
    public void EnsurePatientSkeleton(string patientId);
    public string BuildOutputPath(string patientId, string stepId, string baseName, string extension);
    public static string Timestamp();
    public bool IsPathWithinDataRoot(string path);
}
```

### `Common/FileSystemService.cs`

```csharp
public class FileSystemService
{
    private readonly PathResolver _paths;
    private readonly ILogger<FileSystemService> _logger;

    public FileSystemService(PathResolver paths, ILogger<FileSystemService> logger);

    public List<ManagedFile> ListStepFiles(string patientId, string stepId);
    public List<ManagedFile> ListStepFiles(string patientId, string stepId, string globPattern);
    public ManagedFile? FindLatestFile(string patientId, string stepId, string globPattern);
    public List<ManagedFile> FindFiles(string patientId, string stepId, params string[] globPatterns);
    public bool StepHasFiles(string patientId, string stepId);
    public bool StepHasFilesMatching(string patientId, string stepId, string globPattern);
    public long GetStepSizeBytes(string patientId, string stepId);
    public long GetPatientSizeBytes(string patientId);
    public Task<ManagedFile> SaveUploadAsync(string patientId, string stepId, IFormFile file, string? fileKind = null);
    public Task<ManagedFile> RegisterExternalFileAsync(string patientId, string stepId, string sourcePath, string? fileKind = null, bool copy = false);
    public Stream OpenRead(string patientId, string stepId, string fileName);
    public bool DeleteFile(string patientId, string stepId, string fileName);
    public void WriteJson<T>(string patientId, string stepId, string fileName, T content);
    public T? ReadJson<T>(string patientId, string stepId, string fileName);
    public string? ReadTextFile(string patientId, string stepId, string fileName, int maxBytes = 1_000_000);
    public long GetAvailableDiskBytes();
    private static ManagedFile ToManagedFile(FileInfo info, string stepId);
    private static string? InferFileKind(string fileName);
}
```

### `Common/PythonRunner.cs`

```csharp
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
    public string Stdout { get; set; }
    public string Stderr { get; set; }
    public TimeSpan Duration { get; set; }
    public bool TimedOut { get; set; }
    public bool Success => ExitCode == 0 && !TimedOut;
}

public class PythonRunner
{
    private readonly AppConfig _config;
    private readonly PathResolver _paths;
    private readonly ILogger<PythonRunner> _logger;

    public PythonRunner(AppConfig config, PathResolver paths, ILogger<PythonRunner> logger);

    public Task<PythonExecutionResult> RunAsync(string scriptName, Dictionary<string, string> args, PythonExecutionOptions? options = null);
    public Task<PythonResponse> RunAndParseAsync(string scriptName, Dictionary<string, string> args, PythonExecutionOptions? options = null);
    public Task<PythonExecutionResult> RunRawAsync(string[] commandParts, PythonExecutionOptions? options = null);
    private static string BuildArgumentString(string scriptPath, Dictionary<string, string> args);
    private static string ExtractJsonBlock(string stdout);
}
```

### `Common/IPipelineStep.cs`

```csharp
public interface IPipelineStep
{
    StepDefinition Definition { get; }
    ValidationResult ValidateInputs(string patientId);
    Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken cancellationToken = default);
    Task<StepState> GetStateAsync(string patientId);
    List<ManagedFile> GetInputFiles(string patientId);
    List<ManagedFile> GetOutputFiles(string patientId);
}
```

### `Common/PipelineStepBase.cs`

```csharp
public abstract class PipelineStepBase : IPipelineStep
{
    protected readonly PathResolver Paths;
    protected readonly FileSystemService Files;
    protected readonly PythonRunner Python;
    protected readonly ToolChecker Tools;
    protected readonly ILogger Logger;

    protected PipelineStepBase(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, ILogger logger);

    public abstract StepDefinition Definition { get; }
    public abstract Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken cancellationToken = default);

    public virtual ValidationResult ValidateInputs(string patientId);
    public virtual Task<StepState> GetStateAsync(string patientId);
    public virtual List<ManagedFile> GetInputFiles(string patientId);
    public virtual List<ManagedFile> GetOutputFiles(string patientId);

    protected ValidationResult ValidateRequiredSteps(string patientId);
    protected ValidationResult ValidateRequiredTools();
    protected StepResult BuildResult(string patientId, PythonResponse response, TimeSpan duration);
    protected string RequireLatestFile(string patientId, string stepId, string glob, string friendlyName);
    protected void WriteSummary(string patientId, Dictionary<string, object> summary);
    protected Dictionary<string, object>? ReadLatestSummary(string patientId);
}
```

### `Common/StepRegistry.cs`

```csharp
public class StepRegistry
{
    private readonly Dictionary<string, IPipelineStep> _steps;
    private readonly List<StepDefinition> _orderedDefinitions;

    public StepRegistry(IEnumerable<IPipelineStep> steps);

    public IReadOnlyList<StepDefinition> GetAllDefinitions();
    public IPipelineStep GetStep(string stepId);
    public bool TryGetStep(string stepId, out IPipelineStep? step);
    public IPipelineStep? GetPreviousStep(string stepId);
    public IPipelineStep? GetNextStep(string stepId);
    public IReadOnlyList<IPipelineStep> GetAllSteps();
    public int StepCount { get; }
}
```

### `Common/PatientRepository.cs`

```csharp
public class PatientRepository
{
    private readonly PathResolver _paths;
    private readonly FileSystemService _files;
    private readonly StepRegistry _registry;
    private readonly SemaphoreSlim _writeLock;

    public PatientRepository(PathResolver paths, FileSystemService files, StepRegistry registry);

    public Task<List<PatientSummary>> ListAsync();
    public Task<Patient?> GetAsync(string patientId);
    public Task<Patient> CreateAsync(CreatePatientRequest request);
    public Task<Patient> UpdateAsync(string patientId, UpdatePatientRequest request);
    public Task<bool> DeleteAsync(string patientId, bool deleteFiles = false);
    public Task<bool> ExistsAsync(string patientId);
    public Task<PatientSummary> BuildSummaryAsync(Patient patient);
    private Task SaveAsync(Patient patient);
}
```

### `Common/JobManager.cs`

```csharp
public class JobManager
{
    private readonly ConcurrentDictionary<string, JobRecord> _jobs;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations;
    private readonly PathResolver _paths;
    private readonly StepRegistry _registry;
    private readonly ILogger<JobManager> _logger;

    public JobManager(PathResolver paths, StepRegistry registry, ILogger<JobManager> logger);

    public string StartJob(string patientId, string stepId, StepParameters parameters);
    public JobRecord? GetJob(string patientId, string jobId);
    public JobRecord? GetActiveJobForStep(string patientId, string stepId);
    public List<JobRecord> ListJobs(string patientId);
    public bool CancelJob(string patientId, string jobId);
    public Task<StepResult> RunSynchronousAsync(string patientId, string stepId, StepParameters parameters, CancellationToken ct = default);
    private Task ExecuteJobAsync(JobRecord job, StepParameters parameters);
    private void PersistJob(JobRecord job);
    private JobRecord? LoadJob(string patientId, string jobId);
    private void UpdateProgress(string jobId, int percent, string? logLine);
}
```

### `Common/ToolChecker.cs`

```csharp
public class ToolChecker
{
    private readonly AppConfig _config;
    private readonly ILogger<ToolChecker> _logger;
    private readonly ConcurrentDictionary<string, ToolStatus> _cache;

    public ToolChecker(AppConfig config, ILogger<ToolChecker> logger);

    public ToolStatus Check(string toolName);
    public List<ToolStatus> CheckAll();
    public bool IsAvailable(string toolName);
    public List<string> GetMissingTools(string[] requiredTools);
    public void InvalidateCache();
    private ToolStatus ProbeTool(string toolName);
    private static string? GetVersionCommand(string toolName);
}
```

### `Common/Exceptions/*.cs`

```csharp
public class PipelineException : Exception
{
    public string? StepId { get; }
    public PipelineException(string message, string? stepId = null, Exception? inner = null);
}

public class StepValidationException : PipelineException
{
    public ValidationResult Validation { get; }
    public StepValidationException(ValidationResult validation, string stepId);
}

public class PythonExecutionException : PipelineException
{
    public int ExitCode { get; }
    public string Stderr { get; }
    public string ScriptName { get; }
    public PythonExecutionException(string scriptName, int exitCode, string stderr, string? stepId = null);
}

public class PatientNotFoundException : PipelineException
{
    public string PatientId { get; }
    public PatientNotFoundException(string patientId);
}
```

---

## 5. Backend ,  Controllers

### `Controllers/PatientsController.cs`

Route: `/api/patients`

```csharp
[ApiController]
[Route("api/patients")]
public class PatientsController : ControllerBase
{
    private readonly PatientRepository _repository;
    private readonly ILogger<PatientsController> _logger;

    public PatientsController(PatientRepository repository, ILogger<PatientsController> logger);

    [HttpGet]                 public Task<ActionResult<List<PatientSummary>>> List();
    [HttpGet("{patientId}")]  public Task<ActionResult<Patient>> Get(string patientId);
    [HttpPost]                public Task<ActionResult<Patient>> Create([FromBody] CreatePatientRequest request);
    [HttpPatch("{patientId}")] public Task<ActionResult<Patient>> Update(string patientId, [FromBody] UpdatePatientRequest request);
    [HttpDelete("{patientId}")] public Task<ActionResult> Delete(string patientId, [FromQuery] bool deleteFiles = false);
    [HttpGet("{patientId}/summary")] public Task<ActionResult<PatientSummary>> GetSummary(string patientId);
}
```

### `Controllers/StepsController.cs`

Route: `/api/patients/{patientId}/steps`

```csharp
[ApiController]
[Route("api/patients/{patientId}/steps")]
public class StepsController : ControllerBase
{
    private readonly StepRegistry _registry;
    private readonly JobManager _jobs;
    private readonly PatientRepository _patients;
    private readonly ILogger<StepsController> _logger;

    public StepsController(StepRegistry registry, JobManager jobs, PatientRepository patients, ILogger<StepsController> logger);

    [HttpGet]                              public ActionResult<List<StepDefinition>> ListDefinitions();
    [HttpGet("states")]                    public Task<ActionResult<List<StepState>>> GetAllStates(string patientId);
    [HttpGet("{stepId}")]                  public Task<ActionResult<StepStatusResponse>> GetStatus(string patientId, string stepId);
    [HttpGet("{stepId}/validate")]         public ActionResult<ValidationResult> Validate(string patientId, string stepId);
    [HttpPost("{stepId}/run")]             public Task<ActionResult<RunStepResponse>> Run(string patientId, string stepId, [FromBody] RunStepRequest request);
    [HttpGet("{stepId}/jobs/{jobId}")]     public ActionResult<JobRecord> GetJob(string patientId, string stepId, string jobId);
    [HttpPost("{stepId}/jobs/{jobId}/cancel")] public ActionResult Cancel(string patientId, string stepId, string jobId);
    [HttpGet("{stepId}/summary")]          public Task<ActionResult<Dictionary<string, object>>> GetSummary(string patientId, string stepId);
}
```

### `Controllers/FilesController.cs`

Route: `/api/patients/{patientId}/steps/{stepId}/files`

```csharp
[ApiController]
[Route("api/patients/{patientId}/steps/{stepId}/files")]
public class FilesController : ControllerBase
{
    private readonly FileSystemService _files;
    private readonly PathResolver _paths;
    private readonly ILogger<FilesController> _logger;

    public FilesController(FileSystemService files, PathResolver paths, ILogger<FilesController> logger);

    [HttpGet]                        public ActionResult<List<ManagedFile>> List(string patientId, string stepId);
    [HttpPost("upload")]             public Task<ActionResult<UploadResponse>> Upload(string patientId, string stepId, [FromForm] List<IFormFile> files, [FromForm] string? fileKind);
    [HttpPost("register")]           public Task<ActionResult<UploadResponse>> RegisterPath(string patientId, string stepId, [FromBody] RegisterFileRequest request);
    [HttpGet("{fileName}/download")] public ActionResult Download(string patientId, string stepId, string fileName);
    [HttpGet("{fileName}/preview")]  public ActionResult<string> Preview(string patientId, string stepId, string fileName, [FromQuery] int maxLines = 100);
    [HttpDelete("{fileName}")]       public ActionResult Delete(string patientId, string stepId, string fileName);
}

public class RegisterFileRequest
{
    public string SourcePath { get; set; }
    public string? FileKind { get; set; }
    public bool Copy { get; set; } = false;
}
```

### `Controllers/ToolsController.cs`

```csharp
[ApiController]
[Route("api/tools")]
public class ToolsController : ControllerBase
{
    private readonly ToolChecker _tools;
    private readonly FileSystemService _files;

    public ToolsController(ToolChecker tools, FileSystemService files);

    [HttpGet]                 public ActionResult<List<ToolStatus>> ListAll();
    [HttpGet("{toolName}")]   public ActionResult<ToolStatus> Get(string toolName);
    [HttpPost("refresh")]     public ActionResult<List<ToolStatus>> Refresh();
    [HttpGet("disk")]         public ActionResult<DiskStatus> GetDiskStatus();
}

public class DiskStatus
{
    public long AvailableBytes { get; set; }
    public long DataUsedBytes { get; set; }
}
```

### `Controllers/DevTestsController.cs`

Gated by `AppConfig.EnableDevEndpoints`.

```csharp
[ApiController]
[Route("api/dev/tests")]
public class DevTestsController : ControllerBase
{
    private readonly AppConfig _config;
    private readonly FixtureSeeder _seeder;
    private readonly StepRegistry _registry;
    private readonly ILogger<DevTestsController> _logger;

    public DevTestsController(AppConfig config, FixtureSeeder seeder, StepRegistry registry, ILogger<DevTestsController> logger);

    [HttpPost("seed")]         public Task<ActionResult<Patient>> SeedTestPatient([FromBody] SeedRequest request);
    [HttpPost("run")]          public Task<ActionResult<List<TestRunResult>>> RunTests([FromBody] RunTestsRequest request);
    [HttpGet("results")]       public ActionResult<List<TestRunResult>> GetLastResults();
    [HttpDelete("cleanup")]    public Task<ActionResult> CleanupTestPatients();
}

public class SeedRequest
{
    public string? PatientName { get; set; }
    public string SeedThroughStepId { get; set; }     // e.g. "07_presentation"
    public bool UseTinyFixtures { get; set; } = true;
}

public class RunTestsRequest
{
    public int Tier { get; set; } = 1;
    public string[]? StepIds { get; set; }
    public string? PatientId { get; set; }
}

public class TestRunResult
{
    public string StepId { get; set; }
    public string TestName { get; set; }
    public string Outcome { get; set; }               // "Passed" | "Failed" | "Skipped"
    public string? Message { get; set; }
    public string? SkipReason { get; set; }
    public double DurationSeconds { get; set; }
    public List<string> Assertions { get; set; } = new();
}
```

---

## 6. Backend ,  Step Services

Every service extends `PipelineStepBase`. Only step-specific members are listed below; inherited members are in §4.

### `Services/01_Upload/UploadService.cs`

```csharp
public class UploadService : PipelineStepBase
{
    public const string StepId = "01_upload";
    private static readonly string[] AllowedExtensions = { ".fastq", ".fq", ".fastq.gz", ".fq.gz", ".bam", ".cram" };
    private const long MaxBrowserUploadBytes = 2_147_483_648;   // 2GB

    public override StepDefinition Definition { get; }

    public UploadService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, ILogger<UploadService> logger);

    public override ValidationResult ValidateInputs(string patientId);
    public override Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default);

    public ValidationResult ValidateUpload(IFormFile file, string? fileKind);
    public bool HasTumorDna(string patientId);
    public bool HasNormalDna(string patientId);
    public bool HasRnaSeq(string patientId);
    public bool InputsAreBam(string patientId);
    public UploadManifest BuildManifest(string patientId);
    private static bool IsAllowedExtension(string fileName);
    private static string NormalizeExtension(string fileName);
}

public class UploadManifest
{
    public List<ManagedFile> TumorDna { get; set; } = new();
    public List<ManagedFile> NormalDna { get; set; } = new();
    public List<ManagedFile> TumorRna { get; set; } = new();
    public bool AlreadyAligned { get; set; }
    public long TotalBytes { get; set; }
}
```

**Note:** `RunAsync` for this step performs manifest generation only ,  there is no computation. It writes `_manifest.json`.

### `Services/02_Alignment/AlignmentService.cs`

```csharp
public class AlignmentService : PipelineStepBase
{
    public const string StepId = "02_alignment";
    private readonly UploadService _uploadService;

    public override StepDefinition Definition { get; }

    public AlignmentService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, UploadService uploadService, ILogger<AlignmentService> logger);

    public override ValidationResult ValidateInputs(string patientId);
    public override Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default);

    public bool CanSkip(string patientId);
    public Task<StepResult> PassThroughBamsAsync(string patientId);
    private Dictionary<string, string> BuildPythonArgs(string patientId, string sampleType, StepParameters parameters);
    private Task<PythonResponse> AlignSampleAsync(string patientId, string sampleType, bool isRna, StepParameters parameters, CancellationToken ct);
}
```

**Parameters:** `threads` (int, default 4), `referenceGenome` (string), `dryRun` (bool)

### `Services/03_VariantCalling/VariantCallingService.cs`

```csharp
public class VariantCallingService : PipelineStepBase
{
    public const string StepId = "03_variants";

    public override StepDefinition Definition { get; }

    public VariantCallingService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, ILogger<VariantCallingService> logger);

    public override ValidationResult ValidateInputs(string patientId);
    public override Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default);

    public VariantSummary? GetLatestVariantSummary(string patientId);
    private string ResolveTumorBam(string patientId);
    private string ResolveNormalBam(string patientId);
}

public class VariantSummary
{
    public int TotalVariants { get; set; }
    public int PassVariants { get; set; }
    public int FilteredVariants { get; set; }
    public Dictionary<string, int> FilterReasons { get; set; } = new();
    public List<double> VafDistribution { get; set; } = new();
    public double MedianVaf { get; set; }
}
```

**Parameters:** `minVaf` (double, default 0.05), `usePanelOfNormals` (bool, default true), `intervals` (string, optional ,  e.g. `chr21` for tests)

### `Services/04_ProteinEffects/ProteinEffectsService.cs`

```csharp
public class ProteinEffectsService : PipelineStepBase
{
    public const string StepId = "04_protein_effects";
    private static readonly string[] KeptConsequences = { "missense_variant", "stop_gained", "frameshift_variant", "inframe_insertion", "inframe_deletion", "start_lost" };

    public override StepDefinition Definition { get; }

    public ProteinEffectsService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, ILogger<ProteinEffectsService> logger);

    public override ValidationResult ValidateInputs(string patientId);
    public override Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default);

    public EffectsSummary? GetLatestEffectsSummary(string patientId);
    public List<ProteinAlteringVariant> ReadProteinAlteringVariants(string patientId);
}

public class EffectsSummary
{
    public int InputVariants { get; set; }
    public int ProteinAltering { get; set; }
    public int Discarded { get; set; }
    public Dictionary<string, int> ConsequenceCounts { get; set; } = new();
}

public class ProteinAlteringVariant
{
    public string Chromosome { get; set; }
    public int Position { get; set; }
    public string Ref { get; set; }
    public string Alt { get; set; }
    public string GeneSymbol { get; set; }
    public string GeneId { get; set; }
    public string TranscriptId { get; set; }
    public string Consequence { get; set; }
    public int ProteinPosition { get; set; }
    public string WildTypeAminoAcid { get; set; }
    public string MutantAminoAcid { get; set; }
    public double Vaf { get; set; }
    public string? WildTypeProteinSequence { get; set; }
    public string? MutantProteinSequence { get; set; }
}
```

**Parameters:** `useDatabaseMode` (bool), `keepConsequences` (string[])

### `Services/05_HlaTyping/HlaTypingService.cs`

```csharp
public class HlaTypingService : PipelineStepBase
{
    public const string StepId = "05_hla_typing";

    public override StepDefinition Definition { get; }

    public HlaTypingService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, ILogger<HlaTypingService> logger);

    public override ValidationResult ValidateInputs(string patientId);
    public override Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default);

    public HlaProfile? GetHlaProfile(string patientId);
    public bool HasHlaProfile(string patientId);
    private static bool IsValidAlleleFormat(string allele);
}

public class HlaProfile
{
    public List<string> ClassIAlleles { get; set; } = new();   // ["HLA-A*02:01", ...]
    public List<string> ClassIIAlleles { get; set; } = new();
    public Dictionary<string, double> Confidence { get; set; } = new();
    public DateTime TypedAt { get; set; }
    public string Source { get; set; }                         // "OptiType" | "manual"

    public List<string> GetAllAlleles();
    public bool IsComplete();
}
```

**Parameters:** `manualAlleles` (string[], optional override), `includeClassII` (bool, default false)

### `Services/06_CandidateGeneration/SlidingWindowGenerator.cs`

Pure logic, no external tools ,  fully unit-testable.

```csharp
public class SlidingWindowGenerator
{
    private readonly int _minLength;
    private readonly int _maxLength;

    public SlidingWindowGenerator(int minLength = 8, int maxLength = 11);

    public List<PeptidePair> GeneratePairs(ProteinAlteringVariant variant);
    public List<string> GenerateWindows(string proteinSequence, int mutationPosition, int windowLength);
    public List<PeptidePair> GenerateForAllLengths(string wildTypeSequence, string mutantSequence, int mutationPosition);
    public int ExpectedWindowCount(int proteinLength, int mutationPosition);
    private static bool IsValidPeptide(string peptide);
    private static (int start, int end) ClampWindow(int center, int length, int sequenceLength);
}

public class PeptidePair
{
    public string MutantPeptide { get; set; }
    public string WildTypePeptide { get; set; }
    public int Length { get; set; }
    public int MutationOffsetInPeptide { get; set; }
    public string GeneSymbol { get; set; }
    public string TranscriptId { get; set; }
    public int ProteinPosition { get; set; }
    public double Vaf { get; set; }
    public string SourceVariantId { get; set; }
}
```

### `Services/06_CandidateGeneration/CandidateGenerationService.cs`

```csharp
public class CandidateGenerationService : PipelineStepBase
{
    public const string StepId = "06_candidates";
    private readonly ProteinEffectsService _effectsService;
    private readonly HlaTypingService _hlaService;
    private readonly SlidingWindowGenerator _generator;

    public override StepDefinition Definition { get; }

    public CandidateGenerationService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, ProteinEffectsService effectsService, HlaTypingService hlaService, ILogger<CandidateGenerationService> logger);

    public override ValidationResult ValidateInputs(string patientId);
    public override Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default);

    public List<NeoantigenCandidate> ReadCandidates(string patientId);
    public int CountCandidates(string patientId);
    private List<NeoantigenCandidate> ExpandAcrossAlleles(List<PeptidePair> pairs, List<string> alleles);
    private void WriteCandidatesTsv(string patientId, List<NeoantigenCandidate> candidates, string outputPath);
}
```

### `Models/NeoantigenCandidate.cs`

The central data object flowing through steps 6–11.

```csharp
public class NeoantigenCandidate
{
    // Identity
    public string CandidateId { get; set; }
    public string MutantPeptide { get; set; }
    public string WildTypePeptide { get; set; }
    public string HlaAllele { get; set; }
    public int PeptideLength { get; set; }

    // Provenance
    public string GeneSymbol { get; set; }
    public string TranscriptId { get; set; }
    public string SourceVariantId { get; set; }
    public string Chromosome { get; set; }
    public int Position { get; set; }
    public string Consequence { get; set; }
    public int MutationOffsetInPeptide { get; set; }

    // Step 7
    public double? PresentationScore { get; set; }
    public double? PresentationPercentileRank { get; set; }
    public double? WildTypePresentationScore { get; set; }
    public string? PresentationPredictor { get; set; }

    // Step 8
    public double? ImmunogenicityScore { get; set; }
    public string? ImmunogenicityPredictor { get; set; }

    // Step 9
    public bool PassedSelfFilter { get; set; }
    public bool PassedExpressionFilter { get; set; }
    public string? RemovalReason { get; set; }
    public double? SelfSimilarityScore { get; set; }
    public double? ExpressionTpm { get; set; }

    // Step 3 carry-through
    public double Vaf { get; set; }

    // Step 10
    public double? Agretopicity { get; set; }
    public double? FinalScore { get; set; }
    public int? FinalRank { get; set; }
    public bool IsSelected { get; set; }

    public double ComputeAgretopicity();
    public bool IsComplete();
}
```

### `Services/07_Presentation/PresentationService.cs`

```csharp
public class PresentationService : PipelineStepBase
{
    public const string StepId = "07_presentation";
    private readonly CandidateGenerationService _candidateService;

    public override StepDefinition Definition { get; }

    public PresentationService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, CandidateGenerationService candidateService, ILogger<PresentationService> logger);

    public override ValidationResult ValidateInputs(string patientId);
    public override Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default);

    public List<NeoantigenCandidate> ReadScoredCandidates(string patientId);
    private static string ResolvePredictorName(StepParameters parameters);
}
```

**Parameters:** `predictor` (`"mhcflurry"` | `"bigmhc_el"` | `"both"`), `percentileThreshold` (double, default 2.0), `useStub` (bool)

### `Services/08_Immunogenicity/ImmunogenicityService.cs`

```csharp
public class ImmunogenicityService : PipelineStepBase
{
    public const string StepId = "08_immunogenicity";
    private readonly PresentationService _presentationService;

    public override StepDefinition Definition { get; }

    public ImmunogenicityService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, PresentationService presentationService, ILogger<ImmunogenicityService> logger);

    public override ValidationResult ValidateInputs(string patientId);
    public override Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default);

    public List<NeoantigenCandidate> ReadScoredCandidates(string patientId);
}
```

**Parameters:** `predictor` (`"bigmhc_im"` | `"prime"` | `"stub"`), `useGpu` (bool, default false)

### `Services/09_Filtering/FilteringService.cs`

```csharp
public class FilteringService : PipelineStepBase
{
    public const string StepId = "09_filtering";
    private readonly ImmunogenicityService _immunogenicityService;

    public override StepDefinition Definition { get; }

    public FilteringService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, ImmunogenicityService immunogenicityService, ILogger<FilteringService> logger);

    public override ValidationResult ValidateInputs(string patientId);
    public override Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default);

    public bool RnaSeqAvailable(string patientId);
    public List<NeoantigenCandidate> ReadFilteredCandidates(string patientId);
    public List<NeoantigenCandidate> ReadRemovedCandidates(string patientId);
    public FilteringSummary? GetLatestFilteringSummary(string patientId);
}

public class FilteringSummary
{
    public int InputCount { get; set; }
    public int RemovedBySelfSimilarity { get; set; }
    public int RemovedByExpression { get; set; }
    public int Survived { get; set; }
    public bool ExpressionFilterApplied { get; set; }
}
```

**Parameters:** `applyExpressionFilter` (bool), `minTpm` (double, default 1.0), `selfSimilarityThreshold` (double), `useMiniProteome` (bool)

### `Services/10_Ranking/ScoreCalculator.cs`

Pure math, no I/O ,  fully unit-testable.

```csharp
public class RankingWeights
{
    public double Presentation { get; set; } = 1.0;
    public double Immunogenicity { get; set; } = 1.0;
    public double Agretopicity { get; set; } = 0.5;
    public double Expression { get; set; } = 0.5;
    public double Clonality { get; set; } = 0.5;
    public double HlaSpread { get; set; } = 0.5;

    public bool AllZero();
    public RankingWeights Normalized();
    public static RankingWeights FromParameters(StepParameters parameters);
    public static RankingWeights Default();
}

public class ScoreCalculator
{
    private readonly RankingWeights _weights;

    public ScoreCalculator(RankingWeights weights);

    public double ComputeScore(NeoantigenCandidate candidate, NormalizationBounds bounds);
    public List<NeoantigenCandidate> ScoreAll(List<NeoantigenCandidate> candidates);
    public NormalizationBounds ComputeBounds(List<NeoantigenCandidate> candidates);
    private static double Normalize(double value, double min, double max);
    private static double SafeGet(double? value, double fallback = 0.0);
}

public class NormalizationBounds
{
    public double MinPresentation { get; set; }
    public double MaxPresentation { get; set; }
    public double MinImmunogenicity { get; set; }
    public double MaxImmunogenicity { get; set; }
    public double MinAgretopicity { get; set; }
    public double MaxAgretopicity { get; set; }
    public double MinExpression { get; set; }
    public double MaxExpression { get; set; }
    public double MinVaf { get; set; }
    public double MaxVaf { get; set; }
}
```

### `Services/10_Ranking/HlaSpreadSelector.cs`

Set-level diversity constraint ,  cannot be expressed as a per-candidate weight.

```csharp
public class HlaSpreadSelector
{
    private readonly double _spreadWeight;
    private readonly List<string> _availableAlleles;

    public HlaSpreadSelector(double spreadWeight, List<string> availableAlleles);

    public List<NeoantigenCandidate> Select(List<NeoantigenCandidate> scoredCandidates, int targetCount);
    public Dictionary<string, int> GetAlleleCoverage(List<NeoantigenCandidate> selected);
    public double ComputeDiversityPenalty(string allele, Dictionary<string, int> currentCoverage, int totalSelected);
    private static double GiniCoefficient(IEnumerable<int> counts);
}
```

**Algorithm:** greedy selection. At each pick, effective score = `FinalScore - (spreadWeight × diversityPenalty(allele))`, where the penalty rises with how many already-selected candidates share that allele.

### `Services/10_Ranking/RankingService.cs`

```csharp
public class RankingService : PipelineStepBase
{
    public const string StepId = "10_ranking";
    private readonly FilteringService _filteringService;
    private readonly HlaTypingService _hlaService;

    public override StepDefinition Definition { get; }

    public RankingService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, FilteringService filteringService, HlaTypingService hlaService, ILogger<RankingService> logger);

    public override ValidationResult ValidateInputs(string patientId);
    public override Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default);

    public List<NeoantigenCandidate> Preview(string patientId, RankingWeights weights, int targetCount);
    public List<NeoantigenCandidate> ReadRankedCandidates(string patientId);
    public List<NeoantigenCandidate> ReadSelectedCandidates(string patientId);
    public RankingWeights? GetLastUsedWeights(string patientId);
}
```

**Parameters:** `presentationWeight`, `immunogenicityWeight`, `agretopicityWeight`, `expressionWeight`, `clonalityWeight`, `hlaSpreadWeight` (all double 0–1), `targetCount` (int, default 30)

**Note:** This service runs entirely in C# ,  no Python. `Preview` is called by the frontend on slider change without writing files.

### `Services/11_VaccineDesign/VaccineDesignService.cs`

```csharp
public class VaccineDesignService : PipelineStepBase
{
    public const string StepId = "11_vaccine_design";
    private readonly RankingService _rankingService;

    public override StepDefinition Definition { get; }

    public VaccineDesignService(PathResolver paths, FileSystemService files, PythonRunner python, ToolChecker tools, RankingService rankingService, ILogger<VaccineDesignService> logger);

    public override ValidationResult ValidateInputs(string patientId);
    public override Task<StepResult> RunAsync(string patientId, StepParameters parameters, CancellationToken ct = default);

    public VaccineConstruct? GetLatestConstruct(string patientId);
    public Stream OpenFastaStream(string patientId, string? fileName = null);
    public Stream OpenGenBankStream(string patientId, string? fileName = null);
}

public class VaccineConstruct
{
    public string FullSequence { get; set; }
    public int TotalLengthBp { get; set; }
    public List<ConstructElement> Elements { get; set; } = new();
    public List<string> PeptideOrder { get; set; } = new();
    public int JunctionalEpitopesAvoided { get; set; }
    public string LinkerSequence { get; set; }
    public string FivePrimeUtr { get; set; }
    public string ThreePrimeUtr { get; set; }
    public int PolyATailLength { get; set; }
    public DateTime DesignedAt { get; set; }
}

public class ConstructElement
{
    public string Type { get; set; }        // "5utr" | "signal" | "neoantigen" | "linker" | "3utr" | "polyA"
    public string Sequence { get; set; }
    public int StartPosition { get; set; }
    public int EndPosition { get; set; }
    public string? Label { get; set; }
}
```

**Parameters:** `linkerType` (`"gs"` | `"aay"` | `"furin"`), `includeSignalPeptide` (bool), `codonOptimize` (bool), `exportFormat` (`"fasta"` | `"genbank"` | `"both"`)

---

## 7. Backend ,  Testing

### `tests/Fixtures/FixtureSeeder.cs`

The single most important testing component ,  decouples every step from every other.

```csharp
public class FixtureSeeder
{
    private readonly PathResolver _paths;
    private readonly FileSystemService _files;
    private readonly PatientRepository _patients;
    private readonly string _fixtureRoot;

    public FixtureSeeder(PathResolver paths, FileSystemService files, PatientRepository patients, string fixtureRoot);

    public Task<Patient> SeedPatientAsync(string name, string seedThroughStepId, bool useTinyFixtures = true);
    public Task SeedStepAsync(string patientId, string stepId, bool useTinyFixtures = true);
    public Task SeedUploadAsync(string patientId, bool tiny);
    public Task SeedAlignmentAsync(string patientId, bool tiny);
    public Task SeedVariantsAsync(string patientId, string vcfFixtureName = "somatic_pass_20.vcf");
    public Task SeedProteinEffectsAsync(string patientId);
    public Task SeedHlaTypingAsync(string patientId);
    public Task SeedCandidatesAsync(string patientId, int count = 100);
    public Task SeedPresentationAsync(string patientId);
    public Task SeedImmunogenicityAsync(string patientId);
    public Task SeedFilteringAsync(string patientId);
    public Task SeedRankingAsync(string patientId);
    public Task CleanupTestPatientsAsync();
    public List<NeoantigenCandidate> BuildSyntheticCandidates(int count, string[] alleles, Random? rng = null);
    private string FixturePath(params string[] parts);
}
```

### `tests/Integration/StepIntegrationTestBase.cs`

```csharp
public abstract class StepIntegrationTestBase : IAsyncLifetime
{
    protected string TestPatientId { get; private set; }
    protected ServiceProvider Services { get; private set; }
    protected FixtureSeeder Seeder { get; private set; }
    protected AppConfig Config { get; private set; }
    protected StepRegistry Registry { get; private set; }

    public virtual Task InitializeAsync();
    public virtual Task DisposeAsync();

    protected T GetService<T>() where T : notnull;
    protected Task SeedThrough(string stepId);
    protected void SkipIfToolMissing(string toolName);
    protected void AssertFileExists(string stepId, string globPattern);
    protected void AssertFileCount(string stepId, string globPattern, int expected);
    protected T ReadSummary<T>(string stepId);
}
```

### Unit test classes ,  key test methods

```csharp
public class SlidingWindowGeneratorTests
{
    [Fact] public void GeneratesCorrectWindowCountForCentralMutation();
    [Fact] public void EveryWindowContainsMutatedPosition();
    [Fact] public void WildTypeCounterpartMatchesLengthAndPosition();
    [Theory] public void HandlesMutationNearProteinStart(int position);
    [Theory] public void HandlesMutationNearProteinEnd(int position);
    [Fact] public void RejectsPeptidesWithInvalidAminoAcids();
    [Fact] public void ReturnsEmptyForProteinShorterThanMinWindow();
}

public class ScoreCalculatorTests
{
    [Fact] public void SingleWeightOfOneMatchesSortByThatCriterion();
    [Fact] public void AllZeroWeightsProducesStableOrder();
    [Fact] public void ChangingWeightReordersResults();
    [Fact] public void HandlesNullScoresWithoutThrowing();
    [Fact] public void NormalizationHandlesIdenticalValues();
}

public class HlaSpreadSelectorTests
{
    [Fact] public void SelectsExactlyTargetCount();
    [Fact] public void HighSpreadWeightIncludesMultipleAlleles();
    [Fact] public void ZeroSpreadWeightSelectsPurelyByScore();
    [Fact] public void HandlesFewerCandidatesThanTargetCount();
    [Fact] public void SingleAlleleCandidateSetDoesNotThrow();
}

public class PathResolverTests
{
    [Fact] public void RejectsPathTraversalInPatientId();
    [Fact] public void RejectsPathTraversalInFileName();
    [Fact] public void TimestampFormatIsSortable();
    [Fact] public void BuildOutputPathNeverCollides();
}
```

### Integration test methods (representative)

```csharp
public class VariantCallingIntegrationTests : StepIntegrationTestBase
{
    [Fact] public Task ProducesValidVcfFromTinyBams();
    [Fact] public Task RecallsPlantedVariantsAboveThreshold();       // vs truth_variants.vcf, expect >80%
    [Fact] public Task PassSubsetIsNonEmpty();
    [Fact] public Task VafFieldPopulatedOnAllRecords();
    [Fact] public Task FailsLoudlyWithoutMatchedNormal();
}

public class ProteinEffectsIntegrationTests : StepIntegrationTestBase
{
    [Fact] public Task SynonymousVariantIsFiltered();
    [Fact] public Task MissenseVariantSurvivesWithCorrectAminoAcidChange();
    [Fact] public Task NonsenseVariantFlaggedAsStopGained();
    [Fact] public Task FrameshiftVariantSurvives();
    [Fact] public Task IntergenicVariantIsFiltered();
}

public class PresentationIntegrationTests : StepIntegrationTestBase
{
    [Fact] public Task AllPeptidesReceiveScores();
    [Fact] public Task ScoresAreInValidRange();
    [Fact] public Task KnownEpitopesOutscoreRandomSequences();       // positive control
    [Fact] public Task CompletesUnderTenSeconds();
}
```

---

## 8. Python Scripts

### Shared conventions

Every script:
- Accepts `--arg value` style arguments
- Prints exactly one JSON object to **stdout** on success, wrapped in `###JSON_START###` / `###JSON_END###` markers
- Writes human-readable progress to **stderr**
- Exits `0` on success, non-zero on failure with the error message on stderr

### `python/common/response.py`

```python
class PythonResponse:
    def __init__(self, success: bool, message: str = "", error: str | None = None): ...

    success: bool
    message: str
    error: str | None
    output_files: list[str]
    summary: dict[str, Any]

    def add_file(self, path: str) -> None: ...
    def set_summary(self, key: str, value: Any) -> None: ...
    def update_summary(self, values: dict[str, Any]) -> None: ...
    def to_json(self) -> str: ...
    def emit(self) -> None: ...

def emit_success(message: str, files: list[str], summary: dict) -> None: ...
def emit_failure(error: str, exit_code: int = 1) -> NoReturn: ...
def log(message: str) -> None: ...
def log_progress(current: int, total: int, label: str = "") -> None: ...
```

### `python/common/io_utils.py`

```python
def read_vcf(path: str) -> Iterator[dict]: ...
def write_vcf(path: str, records: Iterable[dict], header: str) -> None: ...
def read_tsv(path: str) -> list[dict]: ...
def write_tsv(path: str, rows: list[dict], columns: list[str]) -> None: ...
def read_fasta(path: str) -> Iterator[tuple[str, str]]: ...
def write_fasta(path: str, records: list[tuple[str, str]], line_width: int = 60) -> None: ...
def ensure_dir(path: str) -> None: ...
def timestamped_name(base: str, extension: str) -> str: ...
def file_size_mb(path: str) -> float: ...
def check_file_exists(path: str, description: str) -> None: ...
def run_command(cmd: list[str], description: str, timeout: int | None = None) -> subprocess.CompletedProcess: ...
```

### `python/common/config.py`

```python
class ToolConfig:
    bwa_mem2: str
    samtools: str
    gatk: str
    star: str
    vep: str
    optitype: str
    pvactools: str

    @classmethod
    def from_env(cls) -> "ToolConfig": ...
    def check_available(self, tool_name: str) -> bool: ...
    def require(self, tool_name: str) -> str: ...

def get_reference_path(genome: str) -> str: ...
def get_data_root() -> str: ...
```

### `python/scripts/align.py`

```python
def main() -> None: ...
def parse_args() -> argparse.Namespace: ...
def align_dna(fastq_r1: str, fastq_r2: str, reference: str, output_bam: str, threads: int, sample_name: str) -> dict: ...
def align_rna(fastq_r1: str, fastq_r2: str, star_index: str, output_bam: str, threads: int) -> dict: ...
def sort_and_index(input_bam: str, output_bam: str, threads: int) -> None: ...
def compute_alignment_stats(bam_path: str) -> dict: ...   # mapped_reads, total_reads, mapping_rate, mean_coverage
def dry_run_stub(output_bam: str) -> dict: ...
```

**CLI:** `--fastq-r1 --fastq-r2 --reference --output-bam --threads --sample-name --rna --dry-run`

### `python/scripts/call_variants.py`

```python
def main() -> None: ...
def parse_args() -> argparse.Namespace: ...
def run_mutect2(tumor_bam: str, normal_bam: str, reference: str, output_vcf: str, pon: str | None, intervals: str | None) -> None: ...
def filter_calls(raw_vcf: str, reference: str, output_vcf: str) -> None: ...
def extract_pass_variants(vcf_path: str, output_path: str, min_vaf: float) -> int: ...
def compute_vaf_distribution(vcf_path: str) -> list[float]: ...
def summarize_filters(vcf_path: str) -> dict[str, int]: ...
def get_normal_sample_name(bam_path: str) -> str: ...
```

**CLI:** `--tumor-bam --normal-bam --reference --output-vcf --panel-of-normals --intervals --min-vaf`

### `python/scripts/annotate_effects.py`

```python
def main() -> None: ...
def parse_args() -> argparse.Namespace: ...
def run_vep(input_vcf: str, output_vcf: str, use_database: bool, cache_dir: str | None) -> None: ...
def parse_vep_consequences(vcf_path: str) -> list[dict]: ...
def filter_protein_altering(records: list[dict], kept_consequences: list[str]) -> list[dict]: ...
def extract_protein_sequences(records: list[dict], reference_proteome: str) -> list[dict]: ...
def build_mutant_sequence(wildtype_seq: str, protein_position: int, wt_aa: str, mut_aa: str, consequence: str) -> str: ...
def count_by_consequence(records: list[dict]) -> dict[str, int]: ...
```

**CLI:** `--input-vcf --output-vcf --output-tsv --use-database --cache-dir --keep-consequences`

### `python/scripts/type_hla.py`

```python
def main() -> None: ...
def parse_args() -> argparse.Namespace: ...
def run_optitype(input_file: str, output_dir: str, is_bam: bool, include_class_ii: bool) -> str: ...
def parse_optitype_output(tsv_path: str) -> dict: ...
def normalize_allele(raw: str) -> str: ...        # "A*02:01" -> "HLA-A*02:01"
def validate_alleles(alleles: list[str]) -> tuple[bool, list[str]]: ...
def extract_hla_reads(bam_path: str, hla_regions: str, output_fastq: str) -> None: ...
```

**CLI:** `--input --output-dir --output-json --is-bam --include-class-ii`

### `python/scripts/generate_candidates.py`

```python
def main() -> None: ...
def parse_args() -> argparse.Namespace: ...
def generate_windows(sequence: str, mutation_pos: int, min_len: int, max_len: int) -> list[tuple[str, int]]: ...
def build_peptide_pairs(variant: dict, min_len: int, max_len: int) -> list[dict]: ...
def expand_across_alleles(pairs: list[dict], alleles: list[str]) -> list[dict]: ...
def write_candidates(candidates: list[dict], output_path: str) -> None: ...
```

**CLI:** `--variants-tsv --hla-json --output-tsv --min-length --max-length`

> **Note:** duplicated deliberately in C# (`SlidingWindowGenerator`) and Python. The C# version is authoritative and unit-tested; the Python version exists for standalone pipeline use. Keep them behaviorally identical, or delete the Python one once the C# path is proven.

### `python/scripts/predict_presentation.py`

```python
def main() -> None: ...
def parse_args() -> argparse.Namespace: ...
def predict_mhcflurry(peptides: list[str], alleles: list[str]) -> dict[tuple[str, str], dict]: ...
def predict_bigmhc_el(peptides: list[str], alleles: list[str], use_gpu: bool) -> dict[tuple[str, str], dict]: ...
def predict_stub(peptides: list[str], alleles: list[str], seed: int = 42) -> dict[tuple[str, str], dict]: ...
def merge_predictions(candidates: list[dict], predictions: dict, predictor_name: str) -> list[dict]: ...
def score_wildtype_counterparts(candidates: list[dict], predictor: str) -> list[dict]: ...
def batch_iterator(items: list, batch_size: int) -> Iterator[list]: ...
```

**CLI:** `--candidates-tsv --output-tsv --predictor --batch-size --use-stub`

### `python/scripts/predict_immunogenicity.py`

```python
def main() -> None: ...
def parse_args() -> argparse.Namespace: ...
def predict_bigmhc_im(peptides: list[str], alleles: list[str], use_gpu: bool) -> dict: ...
def predict_prime(peptides: list[str], alleles: list[str]) -> dict: ...
def predict_stub(peptides: list[str], alleles: list[str], seed: int = 42) -> dict: ...
def merge_scores(candidates: list[dict], scores: dict, predictor_name: str) -> list[dict]: ...
```

**CLI:** `--candidates-tsv --output-tsv --predictor --use-gpu --use-stub`

### `python/scripts/filter_candidates.py`

```python
def main() -> None: ...
def parse_args() -> argparse.Namespace: ...
def load_proteome(fasta_path: str) -> set[str]: ...
def build_kmer_index(proteome: dict[str, str], k: int) -> set[str]: ...
def check_self_similarity(peptide: str, kmer_index: set[str], k: int) -> tuple[bool, float]: ...
def load_expression(tsv_path: str) -> dict[str, float]: ...
def apply_expression_filter(candidates: list[dict], expression: dict[str, float], min_tpm: float) -> tuple[list[dict], list[dict]]: ...
def apply_self_filter(candidates: list[dict], kmer_index: set[str], k: int) -> tuple[list[dict], list[dict]]: ...
```

**CLI:** `--candidates-tsv --proteome-fasta --expression-tsv --output-tsv --removed-tsv --min-tpm --kmer-size`

### `python/scripts/design_vaccine.py`

```python
def main() -> None: ...
def parse_args() -> argparse.Namespace: ...
def run_pvacvector(peptides: list[str], alleles: list[str], output_dir: str) -> list[str]: ...
def build_construct(ordered_peptides: list[str], linker: str, include_signal: bool, codon_optimize: bool) -> dict: ...
def reverse_translate(peptide: str, codon_optimize: bool) -> str: ...
def add_utrs(coding_sequence: str) -> tuple[str, dict]: ...
def check_junctional_epitopes(construct: str, alleles: list[str]) -> list[dict]: ...
def write_fasta_output(construct: dict, output_path: str, patient_name: str) -> None: ...
def write_genbank_output(construct: dict, output_path: str, patient_name: str) -> None: ...

LINKERS: dict[str, str] = {"gs": "GGGGS", "aay": "AAY", "furin": "RAKR"}
FIVE_PRIME_UTR: str
THREE_PRIME_UTR: str
SIGNAL_PEPTIDE: str
POLY_A_LENGTH: int
```

**CLI:** `--selected-tsv --hla-json --output-dir --linker-type --include-signal --codon-optimize --export-format`

### `python/tools/make_test_data.py`

```python
def main() -> None: ...
def download_chr21(output_dir: str) -> str: ...
def build_bwa_index(fasta_path: str) -> None: ...
def simulate_reads(reference: str, output_prefix: str, n_reads: int, mutation_rate: float, seed: int) -> tuple[str, str]: ...
def generate_truth_vcf(reference: str, planted_mutations: list[dict], output_path: str) -> None: ...
def build_mini_proteome(source_fasta: str, n_proteins: int, output_path: str) -> None: ...
def build_golden_consequence_vcf(output_path: str) -> None: ...
def build_synthetic_candidates(n: int, alleles: list[str], output_path: str) -> None: ...
```

### `python/tools/check_tools.py`

```python
def main() -> None: ...
def check_tool(name: str, version_cmd: list[str]) -> dict: ...
def check_all() -> list[dict]: ...
def check_python_packages() -> list[dict]: ...
def check_reference_files(reference_root: str) -> list[dict]: ...
```

---

## 9. Frontend ,  Types

### `types/patient.ts`

```typescript
export interface Patient {
  id: string;
  name: string;
  notes?: string;
  cancerType?: string;
  createdAt: string;
  updatedAt: string;
  referenceGenome?: string;
}

export interface PatientSummary {
  id: string;
  name: string;
  cancerType?: string;
  createdAt: string;
  completedSteps: number;
  totalSteps: number;
  furthestStepId?: string;
  totalDiskBytes: number;
}

export interface CreatePatientRequest {
  name: string;
  notes?: string;
  cancerType?: string;
  referenceGenome?: string;
}

export interface UpdatePatientRequest {
  name?: string;
  notes?: string;
  cancerType?: string;
}
```

### `types/step.ts`

```typescript
export type StepId =
  | '01_upload' | '02_alignment' | '03_variants' | '04_protein_effects'
  | '05_hla_typing' | '06_candidates' | '07_presentation'
  | '08_immunogenicity' | '09_filtering' | '10_ranking' | '11_vaccine_design';

export type StepStatus =
  | 'NotStarted' | 'InputsMissing' | 'Ready' | 'Running' | 'Completed' | 'Failed';

export type JobStatus = 'Queued' | 'Running' | 'Succeeded' | 'Failed' | 'Cancelled';

export interface StepDefinition {
  id: StepId;
  order: number;
  displayName: string;
  shortDescription: string;
  longExplanation: string;
  toolName: string;
  requiredInputStepIds: StepId[];
  isUploadStep: boolean;
  hasParameters: boolean;
  producesDownload: boolean;
  requiredTools: string[];
}

export interface StepState {
  stepId: StepId;
  status: StepStatus;
  lastRunAt?: string;
  lastError?: string;
  outputFileCount: number;
  outputBytes: number;
  activeJobId?: string;
  lastSummary?: Record<string, unknown>;
}

export interface StepResult {
  success: boolean;
  stepId: StepId;
  message?: string;
  errorDetail?: string;
  outputFiles: ManagedFile[];
  summary: Record<string, unknown>;
  duration: string;
  completedAt: string;
}

export interface ValidationResult {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  missingTools: string[];
}

export interface JobRecord {
  jobId: string;
  patientId: string;
  stepId: StepId;
  status: JobStatus;
  startedAt: string;
  completedAt?: string;
  errorMessage?: string;
  result?: StepResult;
  logTail?: string;
  progressPercent: number;
}

export interface StepStatusResponse {
  state: StepState;
  activeJob?: JobRecord;
  inputFiles: ManagedFile[];
  outputFiles: ManagedFile[];
}
```

### `types/file.ts`

```typescript
export type FileKind = 'tumor_dna' | 'normal_dna' | 'rna' | 'output' | 'log' | 'summary';

export interface ManagedFile {
  name: string;
  relativePath: string;
  sizeBytes: number;
  createdAt: string;
  modifiedAt: string;
  extension: string;
  fileKind?: FileKind;
  isUserUploaded: boolean;
}

export interface UploadResponse {
  success: boolean;
  uploadedFiles: ManagedFile[];
  error?: string;
}

export interface UploadProgress {
  fileName: string;
  loaded: number;
  total: number;
  percent: number;
  status: 'pending' | 'uploading' | 'complete' | 'error';
  error?: string;
}
```

### `types/candidate.ts`

```typescript
export interface NeoantigenCandidate {
  candidateId: string;
  mutantPeptide: string;
  wildTypePeptide: string;
  hlaAllele: string;
  peptideLength: number;

  geneSymbol: string;
  transcriptId: string;
  sourceVariantId: string;
  chromosome: string;
  position: number;
  consequence: string;
  mutationOffsetInPeptide: number;

  presentationScore?: number;
  presentationPercentileRank?: number;
  wildTypePresentationScore?: number;
  presentationPredictor?: string;

  immunogenicityScore?: number;
  immunogenicityPredictor?: string;

  passedSelfFilter: boolean;
  passedExpressionFilter: boolean;
  removalReason?: string;
  selfSimilarityScore?: number;
  expressionTpm?: number;

  vaf: number;

  agretopicity?: number;
  finalScore?: number;
  finalRank?: number;
  isSelected: boolean;
}

export interface RankingWeights {
  presentation: number;
  immunogenicity: number;
  agretopicity: number;
  expression: number;
  clonality: number;
  hlaSpread: number;
}

export interface HlaProfile {
  classIAlleles: string[];
  classIIAlleles: string[];
  confidence: Record<string, number>;
  typedAt: string;
  source: string;
}

export interface VaccineConstruct {
  fullSequence: string;
  totalLengthBp: number;
  elements: ConstructElement[];
  peptideOrder: string[];
  junctionalEpitopesAvoided: number;
  linkerSequence: string;
  fivePrimeUtr: string;
  threePrimeUtr: string;
  polyATailLength: number;
  designedAt: string;
}

export interface ConstructElement {
  type: '5utr' | 'signal' | 'neoantigen' | 'linker' | '3utr' | 'polyA';
  sequence: string;
  startPosition: number;
  endPosition: number;
  label?: string;
}
```

### `types/api.ts`

```typescript
export interface ApiError {
  status: number;
  message: string;
  detail?: string;
}

export interface ToolStatus {
  toolName: string;
  isAvailable: boolean;
  version?: string;
  resolvedPath?: string;
  error?: string;
  usedBySteps: string[];
}

export interface DiskStatus {
  availableBytes: number;
  dataUsedBytes: number;
}

export interface TestRunResult {
  stepId: string;
  testName: string;
  outcome: 'Passed' | 'Failed' | 'Skipped';
  message?: string;
  skipReason?: string;
  durationSeconds: number;
  assertions: string[];
}
```

---

## 10. Frontend ,  Zustand Stores

### `stores/usePatientStore.ts`

```typescript
interface PatientStore {
  // State
  patients: PatientSummary[];
  currentPatient: Patient | null;
  isLoading: boolean;
  error: string | null;

  // Actions
  fetchPatients: () => Promise<void>;
  fetchPatient: (patientId: string) => Promise<void>;
  createPatient: (request: CreatePatientRequest) => Promise<Patient>;
  updatePatient: (patientId: string, request: UpdatePatientRequest) => Promise<void>;
  deletePatient: (patientId: string, deleteFiles: boolean) => Promise<void>;
  setCurrentPatient: (patient: Patient | null) => void;
  clearError: () => void;
}

export const usePatientStore = create<PatientStore>()(...);
```

**Not persisted** ,  always fetched fresh from disk-backed API.

### `stores/useStepStore.ts`

```typescript
interface StepStore {
  // State
  definitions: StepDefinition[];
  states: Record<StepId, StepState>;
  selectedStepId: StepId | null;
  activeJobs: Record<StepId, JobRecord>;
  inputFiles: Record<StepId, ManagedFile[]>;
  outputFiles: Record<StepId, ManagedFile[]>;
  validations: Record<StepId, ValidationResult>;
  isLoadingStates: boolean;

  // Actions
  fetchDefinitions: () => Promise<void>;
  fetchAllStates: (patientId: string) => Promise<void>;
  fetchStepStatus: (patientId: string, stepId: StepId) => Promise<void>;
  validateStep: (patientId: string, stepId: StepId) => Promise<ValidationResult>;
  runStep: (patientId: string, stepId: StepId, parameters?: Record<string, unknown>) => Promise<string | null>;
  pollJob: (patientId: string, stepId: StepId, jobId: string) => Promise<void>;
  cancelJob: (patientId: string, stepId: StepId, jobId: string) => Promise<void>;
  selectStep: (stepId: StepId) => void;
  refreshFiles: (patientId: string, stepId: StepId) => Promise<void>;
  reset: () => void;

  // Selectors
  getStepState: (stepId: StepId) => StepState | undefined;
  isStepRunning: (stepId: StepId) => boolean;
  canRunStep: (stepId: StepId) => boolean;
}

export const useStepStore = create<StepStore>()(...);
```

**Persisted to sessionStorage:** `selectedStepId` only (so a refresh keeps you on the same step).

### `stores/useToastStore.ts`

```typescript
export type ToastVariant = 'success' | 'error' | 'info' | 'warning';

export interface Toast {
  id: string;
  variant: ToastVariant;
  title: string;
  message?: string;
  createdAt: number;
  durationMs: number;
  persistent: boolean;
}

interface ToastStore {
  toasts: Toast[];

  show: (toast: Omit<Toast, 'id' | 'createdAt'>) => string;
  success: (title: string, message?: string) => string;
  error: (title: string, message?: string) => string;
  info: (title: string, message?: string) => string;
  warning: (title: string, message?: string) => string;
  dismiss: (id: string) => void;
  dismissAll: () => void;
}

export const useToastStore = create<ToastStore>()(...);
```

**Defaults:** success/info auto-dismiss at 4000ms; errors are `persistent: true` (must be dismissed manually ,  error text is the most valuable thing on screen).

### `stores/useRankingStore.ts`

```typescript
interface RankingStore {
  // State
  weights: RankingWeights;
  targetCount: number;
  previewCandidates: NeoantigenCandidate[];
  isPreviewLoading: boolean;
  hasUnsavedChanges: boolean;
  lastCommittedWeights: RankingWeights | null;

  // Actions
  setWeight: (key: keyof RankingWeights, value: number) => void;
  setWeights: (weights: RankingWeights) => void;
  setTargetCount: (count: number) => void;
  fetchPreview: (patientId: string) => Promise<void>;
  commitRanking: (patientId: string) => Promise<void>;
  resetWeights: () => void;
  loadCommittedWeights: (patientId: string) => Promise<void>;
}

export const useRankingStore = create<RankingStore>()(...);
```

**Persisted to localStorage, keyed by patient ID:** `weights`, `targetCount`. Slider positions survive navigation.
**Debounce:** `fetchPreview` is debounced 300ms on slider change.

### `stores/useDevStore.ts`

```typescript
interface DevStore {
  toolStatuses: ToolStatus[];
  diskStatus: DiskStatus | null;
  testResults: TestRunResult[];
  isRunningTests: boolean;
  selectedTier: 1 | 2;

  fetchToolStatuses: () => Promise<void>;
  refreshTools: () => Promise<void>;
  fetchDiskStatus: () => Promise<void>;
  seedTestPatient: (seedThroughStepId: StepId) => Promise<Patient>;
  runTests: (tier: 1 | 2, stepIds?: StepId[]) => Promise<void>;
  cleanupTestPatients: () => Promise<void>;
  setTier: (tier: 1 | 2) => void;
}

export const useDevStore = create<DevStore>()(...);
```

**Not persisted.**

---

## 11. Frontend ,  API Client

### `lib/api/client.ts`

```typescript
export class ApiClient {
  private baseUrl: string;

  constructor(baseUrl: string);

  get<T>(path: string, params?: Record<string, string | number | boolean>): Promise<T>;
  post<T>(path: string, body?: unknown): Promise<T>;
  patch<T>(path: string, body?: unknown): Promise<T>;
  delete<T>(path: string, params?: Record<string, string>): Promise<T>;
  postFormData<T>(path: string, formData: FormData, onProgress?: (progress: number) => void): Promise<T>;
  getBlob(path: string): Promise<Blob>;
  private handleResponse<T>(response: Response): Promise<T>;
  private buildUrl(path: string, params?: Record<string, unknown>): string;
}

export const apiClient: ApiClient;
export function isApiError(error: unknown): error is ApiError;
```

### `lib/api/patients.ts`

```typescript
export async function listPatients(): Promise<PatientSummary[]>;
export async function getPatient(patientId: string): Promise<Patient>;
export async function createPatient(request: CreatePatientRequest): Promise<Patient>;
export async function updatePatient(patientId: string, request: UpdatePatientRequest): Promise<Patient>;
export async function deletePatient(patientId: string, deleteFiles?: boolean): Promise<void>;
export async function getPatientSummary(patientId: string): Promise<PatientSummary>;
```

### `lib/api/steps.ts`

```typescript
export async function listStepDefinitions(): Promise<StepDefinition[]>;
export async function getAllStepStates(patientId: string): Promise<StepState[]>;
export async function getStepStatus(patientId: string, stepId: StepId): Promise<StepStatusResponse>;
export async function validateStep(patientId: string, stepId: StepId): Promise<ValidationResult>;
export async function runStep(patientId: string, stepId: StepId, parameters?: Record<string, unknown>, async?: boolean): Promise<RunStepResponse>;
export async function getJob(patientId: string, stepId: StepId, jobId: string): Promise<JobRecord>;
export async function cancelJob(patientId: string, stepId: StepId, jobId: string): Promise<void>;
export async function getStepSummary(patientId: string, stepId: StepId): Promise<Record<string, unknown>>;
export async function previewRanking(patientId: string, weights: RankingWeights, targetCount: number): Promise<NeoantigenCandidate[]>;
```

### `lib/api/files.ts`

```typescript
export async function listFiles(patientId: string, stepId: StepId): Promise<ManagedFile[]>;
export async function uploadFiles(patientId: string, stepId: StepId, files: File[], fileKind?: FileKind, onProgress?: (p: number) => void): Promise<UploadResponse>;
export async function registerFilePath(patientId: string, stepId: StepId, sourcePath: string, fileKind?: FileKind, copy?: boolean): Promise<UploadResponse>;
export function getDownloadUrl(patientId: string, stepId: StepId, fileName: string): string;
export async function downloadFile(patientId: string, stepId: StepId, fileName: string): Promise<void>;
export async function previewFile(patientId: string, stepId: StepId, fileName: string, maxLines?: number): Promise<string>;
export async function deleteFile(patientId: string, stepId: StepId, fileName: string): Promise<void>;
```

### `lib/api/dev.ts`

```typescript
export async function listTools(): Promise<ToolStatus[]>;
export async function refreshTools(): Promise<ToolStatus[]>;
export async function getDiskStatus(): Promise<DiskStatus>;
export async function seedTestPatient(seedThroughStepId: StepId, useTinyFixtures?: boolean): Promise<Patient>;
export async function runTests(tier: 1 | 2, stepIds?: StepId[], patientId?: string): Promise<TestRunResult[]>;
export async function cleanupTestPatients(): Promise<void>;
```

### `lib/constants/steps.ts`

```typescript
export const STEP_IDS: StepId[];
export const STEP_ORDER: Record<StepId, number>;
export const STEP_DISPLAY_NAMES: Record<StepId, string>;
export const STEP_ICONS: Record<StepId, string>;
export function getStepIndex(stepId: StepId): number;
export function getPreviousStepId(stepId: StepId): StepId | null;
export function getNextStepId(stepId: StepId): StepId | null;
export function isUploadStep(stepId: StepId): boolean;
```

### `lib/utils/format.ts`

```typescript
export function formatBytes(bytes: number, decimals?: number): string;
export function formatDuration(seconds: number): string;
export function formatDate(iso: string): string;
export function formatRelativeTime(iso: string): string;
export function formatScore(score: number | undefined, decimals?: number): string;
export function formatPercent(value: number, decimals?: number): string;
export function truncatePeptide(peptide: string, maxLength?: number): string;
export function highlightMutation(mutant: string, wildType: string): { char: string; isMutated: boolean }[];
```

### `lib/utils/polling.ts`

```typescript
export interface PollOptions {
  intervalMs: number;
  maxAttempts?: number;
  timeoutMs?: number;
  onTick?: (attempt: number) => void;
}

export async function pollUntil<T>(fn: () => Promise<T>, predicate: (result: T) => boolean, options: PollOptions): Promise<T>;
export function createPoller<T>(fn: () => Promise<T>, predicate: (result: T) => boolean, options: PollOptions): { start: () => Promise<T>; stop: () => void };
```

---

## 12. Frontend ,  Components

### `components/layout/TopBar.tsx`

```typescript
interface TopBarProps {
  patientName?: string;
  showBackLink?: boolean;
  rightSlot?: React.ReactNode;
}
export function TopBar(props: TopBarProps): JSX.Element;
```

### `components/layout/StepSidebar.tsx`

```typescript
interface StepSidebarProps {
  patientId: string;
  definitions: StepDefinition[];
  states: Record<StepId, StepState>;
  selectedStepId: StepId | null;
  onSelectStep: (stepId: StepId) => void;
}
export function StepSidebar(props: StepSidebarProps): JSX.Element;
```

### `components/layout/StepSidebarItem.tsx`

```typescript
interface StepSidebarItemProps {
  definition: StepDefinition;
  state?: StepState;
  isSelected: boolean;
  isDisabled: boolean;
  onClick: () => void;
}
export function StepSidebarItem(props: StepSidebarItemProps): JSX.Element;
```

### `components/patients/PatientGrid.tsx`

```typescript
interface PatientGridProps {
  patients: PatientSummary[];
  isLoading: boolean;
  onSelectPatient: (patientId: string) => void;
  onCreateClick: () => void;
}
export function PatientGrid(props: PatientGridProps): JSX.Element;
```

### `components/patients/PatientCard.tsx`

```typescript
interface PatientCardProps {
  patient: PatientSummary;
  onClick: () => void;
  onDelete?: () => void;
}
export function PatientCard(props: PatientCardProps): JSX.Element;
```

### `components/patients/CreatePatientModal.tsx`

```typescript
interface CreatePatientModalProps {
  isOpen: boolean;
  onClose: () => void;
  onCreated: (patient: Patient) => void;
}
export function CreatePatientModal(props: CreatePatientModalProps): JSX.Element;
```

### `components/steps/StepPanel.tsx`

Dispatcher ,  renders the correct panel for the selected step.

```typescript
interface StepPanelProps {
  patientId: string;
  stepId: StepId;
  definition: StepDefinition;
  state?: StepState;
}
export function StepPanel(props: StepPanelProps): JSX.Element;
```

### `components/steps/StepExplanation.tsx`

```typescript
interface StepExplanationProps {
  definition: StepDefinition;
  collapsible?: boolean;
  defaultExpanded?: boolean;
}
export function StepExplanation(props: StepExplanationProps): JSX.Element;
```

### `components/steps/StepRunButton.tsx`

```typescript
interface StepRunButtonProps {
  patientId: string;
  stepId: StepId;
  label?: string;
  parameters?: Record<string, unknown>;
  disabled?: boolean;
  disabledReason?: string;
  onComplete?: (result: StepResult) => void;
}
export function StepRunButton(props: StepRunButtonProps): JSX.Element;
```

### `components/steps/FileUploadZone.tsx`

```typescript
interface FileUploadZoneProps {
  patientId: string;
  stepId: StepId;
  fileKind: FileKind;
  label: string;
  description?: string;
  acceptedExtensions: string[];
  required?: boolean;
  allowServerPath?: boolean;
  onUploaded: (files: ManagedFile[]) => void;
}
export function FileUploadZone(props: FileUploadZoneProps): JSX.Element;
```

### `components/steps/FileTable.tsx`

```typescript
interface FileTableProps {
  patientId: string;
  stepId: StepId;
  files: ManagedFile[];
  title?: string;
  showDownload?: boolean;
  showPreview?: boolean;
  showDelete?: boolean;
  emptyMessage?: string;
  onRefresh?: () => void;
}
export function FileTable(props: FileTableProps): JSX.Element;
```

### Panel components

All share the same prop shape:

```typescript
interface PanelProps {
  patientId: string;
  definition: StepDefinition;
  state?: StepState;
}

export function UploadPanel(props: PanelProps): JSX.Element;
export function AlignmentPanel(props: PanelProps): JSX.Element;
export function VariantPanel(props: PanelProps): JSX.Element;
export function ProteinEffectsPanel(props: PanelProps): JSX.Element;
export function HlaTypingPanel(props: PanelProps): JSX.Element;
export function CandidatePanel(props: PanelProps): JSX.Element;
export function PresentationPanel(props: PanelProps): JSX.Element;
export function ImmunogenicityPanel(props: PanelProps): JSX.Element;
export function FilteringPanel(props: PanelProps): JSX.Element;
export function RankingPanel(props: PanelProps): JSX.Element;
export function VaccineDesignPanel(props: PanelProps): JSX.Element;
```

**Panel-specific content:**

| Panel | Extra widgets |
|---|---|
| `UploadPanel` | 3× `FileUploadZone` (tumor/normal/RNA), manifest summary |
| `AlignmentPanel` | thread selector, "inputs already aligned" notice, mapping-rate stat |
| `VariantPanel` | `VafHistogram`, variant count, filter-reason breakdown |
| `ProteinEffectsPanel` | `ConsequenceChart`, kept/discarded counts |
| `HlaTypingPanel` | `HlaAlleleList`, manual override input |
| `CandidatePanel` | candidate count, `CandidateTable` preview (first 50) |
| `PresentationPanel` | predictor selector, `CandidateTable` sorted by score |
| `ImmunogenicityPanel` | predictor selector, accuracy caveat callout, `CandidateTable` |
| `FilteringPanel` | expression toggle (disabled w/o RNA), removed-candidates table |
| `RankingPanel` | 6× `WeightSlider`, live `CandidateTable`, allele-coverage chart |
| `VaccineDesignPanel` | linker selector, `ConstructDiagram`, download buttons |

### Widgets

```typescript
interface WeightSliderProps {
  label: string;
  description: string;
  value: number;
  min?: number;
  max?: number;
  step?: number;
  disabled?: boolean;
  disabledReason?: string;
  onChange: (value: number) => void;
}
export function WeightSlider(props: WeightSliderProps): JSX.Element;

interface CandidateTableProps {
  candidates: NeoantigenCandidate[];
  columns?: CandidateColumn[];
  maxRows?: number;
  highlightSelected?: boolean;
  sortBy?: keyof NeoantigenCandidate;
  isLoading?: boolean;
  emptyMessage?: string;
}
export type CandidateColumn = 'rank' | 'peptide' | 'wildType' | 'allele' | 'gene'
  | 'presentation' | 'immunogenicity' | 'agretopicity' | 'vaf' | 'expression' | 'finalScore';
export function CandidateTable(props: CandidateTableProps): JSX.Element;

interface HlaAlleleListProps {
  profile: HlaProfile | null;
  isLoading?: boolean;
  allowManualOverride?: boolean;
  onOverride?: (alleles: string[]) => void;
}
export function HlaAlleleList(props: HlaAlleleListProps): JSX.Element;

interface ConsequenceChartProps {
  counts: Record<string, number>;
  height?: number;
}
export function ConsequenceChart(props: ConsequenceChartProps): JSX.Element;

interface VafHistogramProps {
  vafValues: number[];
  binCount?: number;
  height?: number;
}
export function VafHistogram(props: VafHistogramProps): JSX.Element;

interface ConstructDiagramProps {
  construct: VaccineConstruct;
  showSequence?: boolean;
}
export function ConstructDiagram(props: ConstructDiagramProps): JSX.Element;
```

### Common components

```typescript
interface ToastProps { toast: Toast; onDismiss: (id: string) => void; }
export function Toast(props: ToastProps): JSX.Element;

export function ToastContainer(): JSX.Element;

interface SpinnerProps { size?: 'sm' | 'md' | 'lg'; className?: string; }
export function Spinner(props: SpinnerProps): JSX.Element;

interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
  size?: 'sm' | 'md' | 'lg';
}
export function Modal(props: ModalProps): JSX.Element;

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost';
  size?: 'sm' | 'md' | 'lg';
  isLoading?: boolean;
  leftIcon?: React.ReactNode;
}
export function Button(props: ButtonProps): JSX.Element;

interface DataTableProps<T> {
  data: T[];
  columns: DataTableColumn<T>[];
  keyExtractor: (row: T) => string;
  isLoading?: boolean;
  emptyMessage?: string;
  maxHeight?: string;
  onRowClick?: (row: T) => void;
}
export interface DataTableColumn<T> {
  key: string;
  header: string;
  render: (row: T) => React.ReactNode;
  width?: string;
  align?: 'left' | 'center' | 'right';
  sortable?: boolean;
}
export function DataTable<T>(props: DataTableProps<T>): JSX.Element;

interface StatusBadgeProps { status: StepStatus | JobStatus; size?: 'sm' | 'md'; }
export function StatusBadge(props: StatusBadgeProps): JSX.Element;
```

### Dev components

```typescript
export function TestHarness(): JSX.Element;

interface TestResultRowProps { result: TestRunResult; }
export function TestResultRow(props: TestResultRowProps): JSX.Element;

interface ToolStatusPanelProps { statuses: ToolStatus[]; diskStatus: DiskStatus | null; onRefresh: () => void; }
export function ToolStatusPanel(props: ToolStatusPanelProps): JSX.Element;
```

---

## 13. Frontend ,  Pages & Hooks

### `app/layout.tsx`

```typescript
export const metadata: Metadata;
export default function RootLayout({ children }: { children: React.ReactNode }): JSX.Element;
```
Renders `<ToastContainer />` globally.

### `app/page.tsx` ,  patient list

```typescript
export default function HomePage(): JSX.Element;
```
Uses `usePatientStore`. Renders `PatientGrid` + `CreatePatientModal`.

### `app/patients/[patientId]/page.tsx` ,  dashboard

```typescript
interface PageProps { params: { patientId: string } }
export default function PatientDashboardPage({ params }: PageProps): JSX.Element;
```
Layout: `TopBar` / `StepSidebar` / `StepPanel`. On mount: fetch patient, definitions, all step states.

### `app/dev/tests/page.tsx`

```typescript
export default function DevTestsPage(): JSX.Element;
```
Returns 404 unless `NEXT_PUBLIC_ENABLE_DEV_TOOLS === 'true'`.

### `hooks/useStepPolling.ts`

```typescript
interface UseStepPollingOptions {
  patientId: string;
  stepId: StepId;
  jobId: string | null;
  intervalMs?: number;
  onComplete?: (job: JobRecord) => void;
  onError?: (error: string) => void;
}
export function useStepPolling(options: UseStepPollingOptions): {
  job: JobRecord | null;
  isPolling: boolean;
  stop: () => void;
};
```

### `hooks/useFileUpload.ts`

```typescript
interface UseFileUploadOptions {
  patientId: string;
  stepId: StepId;
  fileKind?: FileKind;
  onSuccess?: (files: ManagedFile[]) => void;
  onError?: (error: string) => void;
}
export function useFileUpload(options: UseFileUploadOptions): {
  upload: (files: File[]) => Promise<void>;
  registerPath: (path: string) => Promise<void>;
  progress: UploadProgress[];
  isUploading: boolean;
  reset: () => void;
};
```

### `hooks/useStepFiles.ts`

```typescript
export function useStepFiles(patientId: string, stepId: StepId): {
  inputFiles: ManagedFile[];
  outputFiles: ManagedFile[];
  isLoading: boolean;
  refresh: () => Promise<void>;
};
```

---

## 14. API Contract Reference

| Method | Path | Request | Response |
|---|---|---|---|
| GET | `/api/patients` | ,  | `PatientSummary[]` |
| POST | `/api/patients` | `CreatePatientRequest` | `Patient` |
| GET | `/api/patients/{pid}` | ,  | `Patient` |
| PATCH | `/api/patients/{pid}` | `UpdatePatientRequest` | `Patient` |
| DELETE | `/api/patients/{pid}?deleteFiles=` | ,  | `204` |
| GET | `/api/patients/{pid}/summary` | ,  | `PatientSummary` |
| GET | `/api/patients/{pid}/steps` | ,  | `StepDefinition[]` |
| GET | `/api/patients/{pid}/steps/states` | ,  | `StepState[]` |
| GET | `/api/patients/{pid}/steps/{sid}` | ,  | `StepStatusResponse` |
| GET | `/api/patients/{pid}/steps/{sid}/validate` | ,  | `ValidationResult` |
| POST | `/api/patients/{pid}/steps/{sid}/run` | `RunStepRequest` | `RunStepResponse` |
| GET | `/api/patients/{pid}/steps/{sid}/jobs/{jid}` | ,  | `JobRecord` |
| POST | `/api/patients/{pid}/steps/{sid}/jobs/{jid}/cancel` | ,  | `204` |
| GET | `/api/patients/{pid}/steps/{sid}/summary` | ,  | `object` |
| POST | `/api/patients/{pid}/steps/10_ranking/preview` | `{weights, targetCount}` | `NeoantigenCandidate[]` |
| GET | `/api/patients/{pid}/steps/{sid}/files` | ,  | `ManagedFile[]` |
| POST | `/api/patients/{pid}/steps/{sid}/files/upload` | multipart | `UploadResponse` |
| POST | `/api/patients/{pid}/steps/{sid}/files/register` | `RegisterFileRequest` | `UploadResponse` |
| GET | `/api/patients/{pid}/steps/{sid}/files/{name}/download` | ,  | binary |
| GET | `/api/patients/{pid}/steps/{sid}/files/{name}/preview?maxLines=` | ,  | `string` |
| DELETE | `/api/patients/{pid}/steps/{sid}/files/{name}` | ,  | `204` |
| GET | `/api/tools` | ,  | `ToolStatus[]` |
| POST | `/api/tools/refresh` | ,  | `ToolStatus[]` |
| GET | `/api/tools/disk` | ,  | `DiskStatus` |
| POST | `/api/dev/tests/seed` | `SeedRequest` | `Patient` |
| POST | `/api/dev/tests/run` | `RunTestsRequest` | `TestRunResult[]` |
| DELETE | `/api/dev/tests/cleanup` | ,  | `204` |

### Error response shape

All non-2xx responses:

```json
{
  "status": 400,
  "message": "Step 03_variants cannot run",
  "detail": "Required input step 02_alignment has no output files"
}
```

Python stderr is surfaced verbatim in `detail` for `PythonExecutionException`. Do not genericize these ,  the real error text is the most useful thing the user can see.

---

## Appendix A ,  Dependency Injection Registration

`Program.cs` registration order:

```csharp
// Config
builder.Services.Configure<AppConfig>(builder.Configuration.GetSection("App"));
builder.Services.AddSingleton<AppConfig>(sp => sp.GetRequiredService<IOptions<AppConfig>>().Value);

// Infrastructure (singleton ,  stateless)
builder.Services.AddSingleton<PathResolver>();
builder.Services.AddSingleton<FileSystemService>();
builder.Services.AddSingleton<PythonRunner>();
builder.Services.AddSingleton<ToolChecker>();
builder.Services.AddSingleton<PatientRepository>();

// Steps (singleton ,  must register before StepRegistry)
builder.Services.AddSingleton<UploadService>();
builder.Services.AddSingleton<IPipelineStep>(sp => sp.GetRequiredService<UploadService>());
// ... repeat for all 11, registering both concrete type and IPipelineStep
// (concrete registration needed because some services take others as dependencies)

builder.Services.AddSingleton<StepRegistry>();
builder.Services.AddSingleton<JobManager>();
builder.Services.AddSingleton<FixtureSeeder>();   // dev only
```

**Ordering constraint:** services with inter-dependencies (`AlignmentService` → `UploadService`, `RankingService` → `FilteringService` + `HlaTypingService`) must have their concrete types registered, not just the interface.

## Appendix B ,  Configuration

`appsettings.json`:

```json
{
  "App": {
    "DataRoot": "/data",
    "ReferenceRoot": "/data/references",
    "PythonExecutable": "python3",
    "PythonScriptsRoot": "./python/scripts",
    "DefaultTimeoutSeconds": 3600,
    "LongStepTimeoutSeconds": 86400,
    "EnableDevEndpoints": false,
    "DefaultReferenceGenome": "GRCh38",
    "UseVepDatabaseMode": false,
    "ToolPaths": {
      "bwa-mem2": "bwa-mem2",
      "samtools": "samtools",
      "gatk": "gatk",
      "STAR": "STAR",
      "vep": "vep",
      "OptiType": "OptiTypePipeline.py",
      "mhcflurry": "mhcflurry-predict",
      "pvacseq": "pvacseq",
      "pvacvector": "pvacvector"
    }
  }
}
```

`frontend/.env.local`:

```
NEXT_PUBLIC_API_BASE_URL=http://localhost:5163
NEXT_PUBLIC_ENABLE_DEV_TOOLS=true
NEXT_PUBLIC_POLL_INTERVAL_MS=2000
```

---

## Appendix C ,  Design System

> **This appendix is deliberately malleable.** Unlike the class signatures and API contracts above ,  which are binding ,  the visual direction here is a starting point, not a contract. If a specific value doesn't work in practice (a color reads badly against real data, a spacing value crowds a dense table), adjust it and note the change. What should *not* drift are the underlying principles in C.1: they're what make the rest cohere.

### C.1 Principles

Four rules that govern every visual decision. Deviate from the tokens if needed; don't deviate from these.

1. **Chrome is quiet; color belongs to data.** Buttons, panels, and navigation stay neutral. Saturated color is reserved for variant consequences, score gradients, and state indicators. If the UI shell competes with the VAF histogram, the shell is wrong.
2. **Saturation encodes confidence.** This pipeline's predictions are genuinely uncertain (immunogenicity precision runs ~10–35%). A low-confidence score should *look* washed out. Never render an uncertain number with the same visual weight as a reliable one.
3. **Space comes from padding, not emptiness.** "Spacious" means generous internal padding and breathing room inside dense components ,  not large blank regions. A table with 48px rows and 32px panel padding feels spacious while showing plenty of data.
4. **Borders, not shadows.** Hairline rules define structure. Drop shadows read as modern SaaS; flat panels with crisp 1px borders read as a precision instrument, which is the target.

### C.2 Color tokens

The accent derives from **H&E staining** (hematoxylin and eosin ,  the stain that renders tumor tissue visible under a microscope). Hematoxylin's deep blue-violet is domain-true and avoids the generic medical blue every EHR reaches for.

```css
:root {
  /* Neutrals */
  --color-ink:        #12181B;  /* primary text ,  near-black, cool cast, never pure black */
  --color-slate:      #5A666B;  /* secondary text, labels, metadata */
  --color-paper:      #F6F7F6;  /* page background ,  cool grey-green */
  --color-surface:    #FFFFFF;  /* panels and cards above paper */
  --color-rule:       #DDE2E1;  /* hairlines, borders, table dividers */
  --color-rule-strong:#C3CBCA;  /* emphasis dividers, input borders */

  /* Accent */
  --color-accent:       #403A7E;  /* hematoxylin ,  primary buttons, active step, focus */
  --color-accent-hover: #4E4794;
  --color-accent-muted: #E8E7F2;  /* tinted backgrounds, selected rows */

  /* Semantic ,  step states */
  --color-state-idle:      #8C9599;  /* NotStarted */
  --color-state-blocked:   #B07D2B;  /* InputsMissing */
  --color-state-ready:     #403A7E;  /* Ready / Running */
  --color-state-complete:  #2F6B4F;  /* Completed */
  --color-state-failed:    #A33A3A;  /* Failed */
  --color-state-skipped:   #A8AFB2;  /* Skipped ,  tool missing */

  /* Semantic ,  feedback */
  --color-success-bg: #EDF4F0;
  --color-error-bg:   #F7EDED;
  --color-warning-bg: #F7F1E4;
  --color-info-bg:    #EDF0F2;
}
```

**Data palette (categorical).** For variant consequences, HLA alleles, and any other categorical encoding, use an Okabe–Ito-derived set ,  the colorblind-safe standard in scientific publishing, which is the correct instinct for this domain.

```css
:root {
  --data-1: #0072B2;  /* missense */
  --data-2: #D55E00;  /* stop_gained */
  --data-3: #009E73;  /* frameshift */
  --data-4: #CC79A7;  /* inframe indel */
  --data-5: #E69F00;  /* start_lost */
  --data-6: #56B4E9;  /* other */
}
```

**Score gradients (continuous).** Single-hue ramp, low→high. Use for presentation scores, immunogenicity scores, and final rank. The washed-out low end is the point ,  see principle 2.

```css
--score-0:  #DDE2E1;
--score-25: #B5B7CF;
--score-50: #8D8BB4;
--score-75: #66609A;
--score-100:#403A7E;
```

### C.3 Typography

**IBM Plex Sans** (UI, body, headings) + **IBM Plex Mono** (all sequence data, scores, IDs, file names). Both free via Google Fonts or `@fontsource`.

No third display face. Hierarchy comes from size, weight, and tracking. A decorative heading font on a data dashboard reads as a marketing page wearing a lab coat.

Mono for peptides is functional, not stylistic ,  character-level alignment is how you see which residue differs between mutant and wild-type.

```css
--font-sans: 'IBM Plex Sans', system-ui, sans-serif;
--font-mono: 'IBM Plex Mono', ui-monospace, monospace;

/* Type scale */
--text-display: 32px / 1.15, weight 600, tracking -0.02em;  /* page titles */
--text-h1:      24px / 1.25, weight 600, tracking -0.01em;  /* step titles */
--text-h2:      18px / 1.35, weight 600;                     /* section headers */
--text-body:    15px / 1.55, weight 400;                     /* explanations, prose */
--text-ui:      14px / 1.45, weight 450;                     /* labels, buttons, table cells */
--text-small:   13px / 1.4,  weight 400;                     /* metadata, captions */
--text-micro:   11px / 1.3,  weight 500, tracking 0.06em, uppercase;  /* eyebrows, column headers */
```

**Mono is mandatory for:** peptide sequences, DNA/RNA sequences, HLA alleles, all numeric scores, VAF values, file names, gene symbols, transcript IDs, chromosome positions, candidate IDs.

### C.4 Spacing and geometry

8px base grid.

```css
--space-1:  4px;   --space-2:  8px;   --space-3: 12px;
--space-4: 16px;   --space-6: 24px;   --space-8: 32px;
--space-12: 48px;  --space-16: 64px;
```

| Context | Value |
|---|---|
| Panel padding | 32px |
| Card padding | 24px |
| Between related elements | 16px |
| Between sections | 32px |
| Table row height | 48px |
| Table cell padding | 12px 16px |
| Sidebar width | 280px |
| Max content width | 1400px |

**Border radius ,  keep it minimal.** This is a firm preference, not a suggestion.

```css
--radius-sm: 2px;   /* badges, tags, inline chips */
--radius-md: 3px;   /* buttons, inputs, panels, cards ,  the default */
--radius-lg: 4px;   /* modals ,  the maximum anywhere in the app */
--radius-full: 9999px;  /* ONLY for the spinner and step-marker dots */
```

Nothing rectangular in this app should exceed 4px radius. No pill-shaped buttons, no rounded-corner cards, no `rounded-lg`/`rounded-xl`/`rounded-2xl` Tailwind classes on containers. Soft corners read as consumer SaaS; near-square corners read as an instrument. When in doubt, use less rounding ,  0px is an acceptable choice, 8px is not.

### C.5 Component rules

**Elevation.** No `box-shadow` on panels, cards, tables, or buttons. Structure comes from `1px solid var(--color-rule)`. The only permitted shadows are the modal overlay and toast stack, and those stay subtle (`0 2px 8px rgba(18,24,27,0.12)`).

**Buttons.**
- Primary: solid `--color-accent`, white text, 3px radius, 10px 20px padding.
- Secondary: transparent background, `1px solid --color-rule-strong`, `--color-ink` text.
- Danger: `1px solid --color-state-failed`, red text; solid fill only for confirmed destructive actions.
- Ghost: no border, `--color-slate` text, for tertiary actions in table rows.
- Never pill-shaped. Never gradient-filled.

**Tables.** No zebra striping. Hairline row dividers only. Sticky headers using `--text-micro` in `--color-slate`. Numerics right-aligned in mono. Hover state is `--color-paper`; selected row is `--color-accent-muted` with a 2px left border in `--color-accent`.

**Inputs.** `1px solid --color-rule-strong`, 3px radius, 10px 12px padding. Focus: 2px `--color-accent` ring with 1px offset. No inner shadows.

**Sliders (ranking panel).** Track `--color-rule`, fill and thumb `--color-accent`, square thumb with 2px radius. Live numeric value in mono to the right of the label. Disabled sliders (expression without RNA-seq) drop to 40% opacity with the reason shown inline.

**Toasts.** Bottom-right, 3px radius, `--color-surface` with a 3px left border in the semantic color. Success/info auto-dismiss at 4000ms; **errors persist until dismissed** ,  the Python stderr text is the most valuable thing on screen.

**Status indicators.** 8px circle in the semantic state color. `--color-state-ready` pulses while running. Never rely on color alone ,  pair with a label or icon.

### C.6 Signature element ,  the peptide diff

The one place to spend visual boldness. Mutant and wild-type peptides rendered character-aligned in mono, with the mutated residue picked out in `--color-accent` on `--color-accent-muted`:

```
MUTANT      K  L  V  F  F  A  E [D] V
WILD-TYPE   K  L  V  F  F  A  E [G] V
```

Implemented via `highlightMutation()` in `lib/utils/format.ts`. Appears in `CandidateTable`, the ranking preview, and `ConstructDiagram`.

This is the entire premise of the application made typographically vivid: a single amino acid difference is what makes a protein foreign to the immune system. Everything else in the interface stays disciplined so this reads as the memorable thing.

**Secondary structural device ,  the step spine.** The sidebar renders the 11 steps on a vertical connecting line, each with a state marker. Numbering is justified here because this genuinely *is* an ordered process where sequence carries information ,  it's structure encoding content, not decoration. The connecting line segment between two steps takes `--color-state-complete` once the earlier step has finished, so progress is legible at a glance.

### C.7 Quality floor

Non-negotiable regardless of how the tokens evolve:

- Visible keyboard focus on every interactive element (2px `--color-accent` ring)
- Text contrast ≥ 4.5:1 against its background
- `prefers-reduced-motion` respected ,  spinner degrades to a static indicator, pulse animations disabled
- Never encode meaning in color alone
- Dense tables remain readable at 1280px width; sidebar collapses below 1024px

### C.8 Tailwind configuration

```ts
// tailwind.config.ts
export default {
  theme: {
    extend: {
      colors: {
        ink: '#12181B',
        slate: { DEFAULT: '#5A666B' },
        paper: '#F6F7F6',
        surface: '#FFFFFF',
        rule: { DEFAULT: '#DDE2E1', strong: '#C3CBCA' },
        accent: { DEFAULT: '#403A7E', hover: '#4E4794', muted: '#E8E7F2' },
        state: {
          idle: '#8C9599', blocked: '#B07D2B', ready: '#403A7E',
          complete: '#2F6B4F', failed: '#A33A3A', skipped: '#A8AFB2',
        },
        data: {
          1: '#0072B2', 2: '#D55E00', 3: '#009E73',
          4: '#CC79A7', 5: '#E69F00', 6: '#56B4E9',
        },
      },
      fontFamily: {
        sans: ['IBM Plex Sans', 'system-ui', 'sans-serif'],
        mono: ['IBM Plex Mono', 'ui-monospace', 'monospace'],
      },
      borderRadius: {
        DEFAULT: '3px', sm: '2px', md: '3px', lg: '4px',
        // Deliberately omitted: xl, 2xl, 3xl. Do not re-add them.
      },
      boxShadow: {
        DEFAULT: 'none',
        overlay: '0 2px 8px rgba(18,24,27,0.12)',
      },
    },
  },
};
```
