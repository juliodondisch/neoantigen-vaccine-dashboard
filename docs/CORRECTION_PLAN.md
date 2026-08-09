# Correction Plan — Post-Deployment Fixes

> Companion to `PROJECT_PLAN.md` and `TECHNICAL_SPEC.md`. Written after a full deployment attempt on AWS EC2 (Amazon Linux 2023, m5.2xlarge, 32GB RAM, 1.5TB EBS) against real GIAB HG008 data. Every item here comes from a real, observed failure — not speculation.
>
> **Working model:** all edits happen on the Mac (which has ~10GB free and cannot run the heavy steps), then get pushed to git and pulled on the server. Nothing in this plan requires local execution of bioinformatics tools.

---

## 0. What actually happened, in one paragraph

The pipeline design was sound. Steps 1–4 (upload, alignment, variant calling, VEP annotation) all completed successfully against real 148GB/134GB tumor-normal WGS data and produced correct, verified output. The failures were almost entirely in three categories: **(a)** configuration values that existed in more than one place and silently drifted apart, **(b)** environment/tooling assumptions that were correct on a Mac dev box and wrong on a fresh Linux server, and **(c)** one genuine domain-knowledge error in the HLA typing step — OptiType was handed a genome-aligned BAM when it needs raw FASTQ so it can perform its own mapping against its internal HLA allele reference.

---

## 1. The single biggest structural problem: configuration lives in too many places

This one caused more lost time than every other issue combined, and it recurred in four distinct forms. It deserves a dedicated fix rather than four separate patches.

### 1.1 The failure pattern

A single logical setting — "which reference genome are we using" — existed in **five** independent places:

1. `appsettings.json` (`App:DefaultReferenceGenome`)
2. `appsettings.Development.json` (same key, overlay)
3. `AppConfig.cs` (C# property default: `= "chr21_test"`)
4. Hardcoded `?? "chr21_test"` fallbacks in `AlignmentService.cs` (×2), `VariantCallingService.cs`, `FilteringService.cs`
5. Each patient's own `patient.json` (`ReferenceGenome`, baked in at creation time)

Changing any one of them did nothing, because a lower-priority one won. The same shape of problem hit tool paths (C# `ToolPaths` vs. Python `ToolConfig` env vars), CORS origins, and the API base URL.

### 1.2 Spec changes required

**`TECHNICAL_SPEC.md` §3 — `Models/StepParameters.cs`**

Add an explicit rule to the spec text: *service classes must never contain a literal fallback for a configured value.* If `parameters.GetString("x")` returns null, the fallback is `AppConfig`, never an inline string.

**`TECHNICAL_SPEC.md` §4 — `Common/AppConfig.cs`**

Change the class so every property that has a "sensible default" gets that default from exactly one place. Concretely:

```csharp
public class AppConfig
{
    // BEFORE: public string DefaultReferenceGenome { get; set; } = "chr21_test";
    // AFTER:  no inline default; Validate() throws if unset.
    public string DefaultReferenceGenome { get; set; } = "";

    public void Validate()
    {
        // add to existing checks:
        if (string.IsNullOrWhiteSpace(DefaultReferenceGenome))
            throw new InvalidOperationException("App:DefaultReferenceGenome must be configured.");
        if (AllowedOrigins is null || AllowedOrigins.Length == 0)
            throw new InvalidOperationException("App:AllowedOrigins must be configured.");
        foreach (var required in new[] { "bwa-mem2", "samtools", "gatk", "vep", "OptiType", "mhcflurry", "pvacseq", "pvacvector" })
            if (!ToolPaths.ContainsKey(required))
                throw new InvalidOperationException($"App:ToolPaths must contain an entry for '{required}'.");
    }
}
```

The point: **fail loudly at startup** rather than silently falling back to a dev fixture value. Had this been in place, the `chr21_test` problem would have surfaced in the first thirty seconds instead of after an hour of debugging.

**New method on `PathResolver`:**

```csharp
public string GetIntervalsPath(string genomeName);   // {ReferenceRoot}/{genome}/coding_regions.interval_list
```

### 1.3 Code changes required

| File | Change |
|---|---|
| `Common/AppConfig.cs` | Remove inline default on `DefaultReferenceGenome`; extend `Validate()` per above |
| `Common/PathResolver.cs` | Add `GetIntervalsPath(string genomeName)` |
| `Services/02_Alignment/AlignmentService.cs` | Remove `const string defaultGenome = "chr21_test"` (line ~53) and both `?? "chr21_test"` fallbacks (lines ~82, ~182) → `?? Config.DefaultReferenceGenome` |
| `Services/03_VariantCalling/VariantCallingService.cs` | Same fallback removal; also change `["intervals"] = parameters.GetString("intervals") ?? ""` → `?? Paths.GetIntervalsPath(reference)` |
| `Services/09_Filtering/FilteringService.cs` | Same fallback removal |
| `Common/PipelineStepBase.cs` | Add a protected `AppConfig Config` so services can reach the default without a new constructor param everywhere |

### 1.4 Patient-level config

`PatientRepository.CreateAsync` currently does `ReferenceGenome = request.ReferenceGenome ?? "chr21_test"`. Change to `?? _config.DefaultReferenceGenome`.

**Separately** — and this is a real design question, not just a bug: services currently read the reference genome from **step parameters**, never from the patient record. So a patient's stored `ReferenceGenome` is effectively decorative. Two options:

- **(a)** Make `PipelineStepBase` resolve reference genome as: step parameter → patient record → app default. This makes the patient field meaningful and per-patient genome selection actually work.
- **(b)** Drop `ReferenceGenome` from the `Patient` model entirely and make it app-level only.

Recommend **(a)** — the field exists in the spec for a reason, and mixed-genome datasets are a realistic future need. Add to `TECHNICAL_SPEC.md` §6 as an explicit resolution-order note under each step service.

---

## 2. Environment configuration: `ASPNETCORE_ENVIRONMENT` and the two-config-file trap

### 2.1 What happened

`appsettings.Development.json` was edited correctly, repeatedly, and had zero effect — because `ASPNETCORE_ENVIRONMENT` was unset, so ASP.NET loaded only the base `appsettings.json`, which still had every stale value. Confusingly, `dotnet run` printed `Hosting environment: Development` anyway (it reads `launchSettings.json`), so the startup log actively lied about which config was live.

### 2.2 Fixes

**Keep both files in sync for anything that isn't genuinely environment-specific.** The base `appsettings.json` should contain correct production-ish defaults, not stale dev fixtures. Specifically, `ToolPaths` should be identical in both.

**Add a startup log line that proves which config actually loaded.** In `Program.cs`, right after `app.Services.GetRequiredService<AppConfig>().Validate()`:

```csharp
var cfg = app.Services.GetRequiredService<AppConfig>();
app.Logger.LogInformation(
    "Config loaded: env={Env} refGenome={Genome} dataRoot={Data} origins={Origins}",
    app.Environment.EnvironmentName, cfg.DefaultReferenceGenome, cfg.DataRoot,
    string.Join(",", cfg.AllowedOrigins));
```

This one line would have made the problem obvious immediately.

**Add to `launchSettings.json`** an explicit `ASPNETCORE_ENVIRONMENT` in the environment variables block so `dotnet run` genuinely sets it rather than relying on ambient state.

**Spec change:** `TECHNICAL_SPEC.md` Appendix B gains a note that `appsettings.json` holds complete, valid defaults and `.Development.json` overrides only genuinely-local values (data paths, dev endpoints flag).

---

## 3. Tool path resolution: two independent systems that never talked

### 3.1 What happened

The C# side resolves tools via `AppConfig.ToolPaths` → `ToolChecker`. The Python side **independently** resolves them via `python/common/config.py`'s `ToolConfig.from_env()`, reading `TOOL_BWA_MEM2`, `TOOL_GATK`, `TOOL_OPTITYPE`, etc. from environment variables with their own hardcoded fallbacks. Fixing the C# config had no effect on what the Python scripts actually invoked — this is what caused the "OptiType not on PATH" error to persist long after the C# config was demonstrably correct.

### 3.2 Fix — make C# the single source of truth

`PythonRunner` should pass the resolved tool paths **into the subprocess environment** on every invocation. That way Python's `ToolConfig.from_env()` keeps working exactly as written, but the values it reads come from C# config rather than from whatever happens to be in the ambient shell.

**`Common/PythonRunner.cs`** — in `RunAsync`, before starting the process:

```csharp
// Project C# ToolPaths into the subprocess env so python/common/config.py's
// ToolConfig.from_env() resolves the same binaries the C# side validated.
private static readonly Dictionary<string, string> ToolEnvVarNames = new()
{
    ["bwa-mem2"] = "TOOL_BWA_MEM2",
    ["samtools"] = "TOOL_SAMTOOLS",
    ["gatk"]     = "TOOL_GATK",
    ["STAR"]     = "TOOL_STAR",
    ["vep"]      = "TOOL_VEP",
    ["OptiType"] = "TOOL_OPTITYPE",
    ["pvacseq"]  = "TOOL_PVACTOOLS",
    ["mhcflurry"]= "TOOL_MHCFLURRY",
};

foreach (var (toolKey, envVar) in ToolEnvVarNames)
    if (_config.ToolPaths.TryGetValue(toolKey, out var path))
        startInfo.EnvironmentVariables[envVar] = path;
```

**`python/common/config.py`** — add the missing `mhcflurry` field to `ToolConfig` (it's referenced in `ToolPaths` on the C# side and used by step 7, but absent from the Python dataclass entirely):

```python
mhcflurry: str = "mhcflurry-predict"
# and in from_env():
mhcflurry=os.environ.get("TOOL_MHCFLURRY", "mhcflurry-predict"),
```

**Spec change:** `TECHNICAL_SPEC.md` §4 `PythonRunner` gains this as documented behaviour, and §8 gains a note that `ToolConfig.from_env()` is populated by the caller, not by ambient shell state.

---

## 4. The HLA typing step: the one genuine domain error

### 4.1 What happened

`type_hla.py` handed OptiType a BAM file. OptiType, seeing a BAM, **skips its own mapping stage** (`if not bam_input:` in its `run_pipeline`) and uses the existing alignment as if it were alignment against its internal HLA allele reference. But the BAM was aligned against whole-genome GRCh38, so `binary.columns` — which should hold HLA allele IDs — held garbage, including a literal `'1'`, which then failed a lookup in OptiType's allele table.

Three separate problems were found and fixed along the way; only the last one is the root cause, but **all four fixes are needed**:

| Problem | Symptom | Fix |
|---|---|---|
| Whole 148GB BAM passed to OptiType | ~22GB RSS, no completion in 30+ min | Extract HLA region (`chr6:29000000-33000000`) first |
| 4.17M reads at 157x depth in-region | ~9 min just for dataframe construction | Downsample to ~30x target depth |
| `chr`-prefixed chromosome names | `Error: 'chr1'` (OptiType issue #64) | Moot once FASTQ is used — OptiType maps itself |
| **BAM input at all** | `Error: '1'` | **Convert to FASTQ before invoking OptiType** |

### 4.2 Rewrite of `python/scripts/type_hla.py`

New pipeline inside `run_optitype()`:

```
input BAM
  → extract_hla_reads()      samtools view -b <bam> chr6:29000000-33000000
  → count_reads()            samtools view -c        (log read count + est. depth)
  → downsample_bam()         samtools view -s SEED.FRAC   (only if est. depth > target * 1.15)
  → bam_to_fastq()           samtools fastq          ← NEW, replaces strip_chr_prefix()
  → optitype run -i <fastq> --dna -o <dir> --verbose
```

**Functions to add:**

```python
def bam_to_fastq(bam_path: str, output_fastq: str) -> None:
    """Convert a BAM to FASTQ so OptiType performs its own mapping.

    OptiType skips its internal razers3/yara mapping stage when given a BAM,
    and instead reads the existing alignment as if it were alignment against
    its own HLA allele reference. Our BAMs are aligned against whole-genome
    GRCh38, so that assumption is wrong and produces malformed allele IDs
    (observed: KeyError '1'). Handing it FASTQ forces the correct path.
    """
```

**Functions to remove:** `strip_chr_prefix()` — solves a problem that only exists on the BAM-input path.

**Functions to keep, with fixes:**

- `count_reads()` — must not pass `capture_output=` to `io_utils.run_command()`; that parameter does not exist in the real signature (`run_command(cmd, description, timeout=None) -> CompletedProcess`). Use `subprocess.run` directly, or add `capture_output` to `run_command` and update the spec.
- `run_optitype()` — CLI is the modern `click` interface: `optitype run -i <in> --dna -o <dir> --verbose`. The old `-i x -d -v -o y` form does not exist.

**Module constants to add:**

```python
HLA_REGION = "chr6:29000000-33000000"    # classical class I locus (HLA-A/B/C)
HLA_REGION_BP = 4_000_000
TARGET_DEPTH = 30                         # OptiType validated at typical 30-60x, not 150x+
ASSUMED_READ_LENGTH_BP = 150
```

**Spec change:** `TECHNICAL_SPEC.md` §6 Step 5 and §8 `type_hla.py` both need rewriting to describe the extract → downsample → FASTQ → OptiType flow, the `--verbose` flag, and the reasoning for each stage. The current spec text implies OptiType is handed the normal BAM directly.

### 4.3 Timeout

`HlaTypingService`'s `TimeoutSeconds = 600` is far too short — the real run takes ~10 minutes for a 30x input and longer at higher depth. Raise to `3600`. More generally, **step timeouts should come from `AppConfig`, not be hardcoded per service** — see §7.

---

## 5. Other real code bugs found

### 5.1 `FileSystemService.ReadTextFile` — OOM on large files

Previewing a 144GB BAM called `File.ReadAllText()` with no guard and crashed the backend with `OutOfMemoryException`.

**`Common/FileSystemService.cs`:**

```csharp
private static readonly string[] NonPreviewableExtensions =
    { ".bam", ".bai", ".cram", ".crai", ".gz", ".bz2", ".fastq", ".fq", ".pdf" };

public string? ReadTextFile(string patientId, string stepId, string fileName, int maxBytes = 1_000_000)
{
    var path = ...;
    var info = new FileInfo(path);
    if (!info.Exists) return null;

    if (NonPreviewableExtensions.Any(e => fileName.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
        return $"[Binary or compressed file ({FormatBytes(info.Length)}) — preview not supported.]";

    using var stream = File.OpenRead(path);
    var buffer = new byte[(int)Math.Min(maxBytes, info.Length)];
    var read = stream.Read(buffer, 0, buffer.Length);
    var text = Encoding.UTF8.GetString(buffer, 0, read);
    return info.Length > maxBytes ? text + $"\n\n[truncated — showing first {maxBytes:N0} of {info.Length:N0} bytes]" : text;
}
```

**Frontend:** `FileTable.tsx` should not render a preview button for extensions in that list. Belt and braces — the backend refusing is the real fix, but the button shouldn't be there.

### 5.2 Steps report `Completed` when they actually failed

Observed twice: `GET /steps/05_hla_typing` returned `"status":"Completed"` while the underlying job record held `"Status": 3` (Failed) and no output file existed. The step-level status is derived from folder contents rather than from the job outcome, so intermediate files (`hla_region_reads.bam`) made a failed step look finished.

**`Common/PipelineStepBase.GetStateAsync`** must consider the most recent job record, not just file presence:

```csharp
// Status resolution order:
//   1. active job running        -> Running
//   2. last job Failed           -> Failed  (regardless of files on disk)
//   3. required outputs present  -> Completed
//   4. inputs missing            -> InputsMissing
//   5. otherwise                 -> Ready / NotStarted
```

This requires each step to declare what its **real** output looks like, so intermediates don't count. Add to `IPipelineStep`:

```csharp
string[] PrimaryOutputPatterns { get; }   // e.g. ["hla_*.json"] for step 5
```

and use `Files.StepHasFilesMatching(patientId, StepId, pattern)` rather than `StepHasFiles`.

**Spec change:** `TECHNICAL_SPEC.md` §4 `IPipelineStep` and §3 `StepState` both need this. Currently the spec says `outputFileCount` drives status, which is exactly the bug.

### 5.3 Jobs stuck in `Running` forever after a backend restart

`JobManager` keeps jobs in a `ConcurrentDictionary` and persists to `_jobs/{id}.json`. When the backend dies mid-run, the persisted record stays `Running` with `completedAt: null` permanently, and nothing ever reconciles it.

**`Common/JobManager.cs`** — add a startup reconciliation pass:

```csharp
public void ReconcileOrphanedJobs(string patientId);
// On construction (or first access per patient): any persisted job with
// Status == Running and no live CancellationTokenSource is marked
// Failed with ErrorMessage = "Interrupted — backend restarted while job was running."
```

Call it from `PatientRepository.GetAsync` or a hosted startup service.

### 5.4 Variant calling ran whole-genome by default

`["intervals"] = parameters.GetString("intervals") ?? ""` meant every run scanned all 3.1Gb unless a caller explicitly passed intervals — and nothing ever did. Restricting to the exome coding regions took the run from an unmeasured multi-hour projection to ~25 minutes of steady progress at ~25 Mb/min.

Fixed by §1.3 (`?? Paths.GetIntervalsPath(reference)`), but the **reference data itself** must now be provisioned — see §6.

### 5.5 `FilterMutectCalls` is missing from the variant-calling step

`call_variants.py` runs `Mutect2` and produces `somatic_*.raw.vcf.gz`, but step 4 looks for `somatic_pass_*.vcf.gz` — which only exists after `gatk FilterMutectCalls` runs. That command was never in the script; it had to be run by hand.

**`python/scripts/call_variants.py`** — `filter_calls()` exists in the spec's function list but evidently isn't wired into `main()`. Ensure the flow is:

```
run_mutect2()        -> {prefix}.raw.vcf.gz
filter_calls()       -> {prefix}_pass.vcf.gz     (gatk FilterMutectCalls -R ref -V raw -O pass)
extract_pass_variants() / summarize_filters()
```

Note that `FilterMutectCalls` **annotates** the FILTER column rather than dropping rows — 25,072 raw calls produced a 6,268-line file where only some rows carry `PASS`. Any downstream count should filter on `$7 == "PASS"`, not on line count.

### 5.6 VEP output named `.gz` but written uncompressed

`annotate_effects.py` writes `annotated_*.vcf.gz` containing plain text. `zcat`/`gzip` correctly reject it; anything downstream that decompresses will break.

**`python/scripts/annotate_effects.py`** — add `--compress_output bgzip` to the VEP invocation, or pipe the output through `bgzip` before naming it `.gz`. Verify with `file <path>` reporting `BGZF`.

### 5.7 OptiType CLI form and `--verbose`

Already covered in §4.2, but worth calling out separately for the spec: the installed OptiType is a `click`-based CLI installed via `pip install -e .` from source, exposing `optitype run|check-deps|init-config|info`. It is **not** the flat `OptiTypePipeline.py` script the spec's `ToolPaths` assumed. `--verbose` is required to get any progress output at all.

---

## 6. Reference data provisioning

Currently nothing in the app or setup provisions reference data, and the spec is silent on how it gets there. Three assets are needed:

| Asset | Size | Source | Needed by |
|---|---|---|---|
| GRCh38 FASTA + `.fai` + `.dict` | ~3.1GB | NCBI `GCA_000001405.15_GRCh38_no_alt_analysis_set.fna.gz` | Steps 2, 3, 4 |
| Exome coding intervals | ~13MB | Broad `whole_exome_illumina_coding_v1...interval_list` | Step 3 |
| VEP cache (`homo_sapiens_vep_116_GRCh38`) | ~26GB download, ~26GB extracted | Ensembl FTP | Step 4 |

**New file: `python/tools/fetch_references.py`**

```python
def main() -> None: ...
def fetch_grch38(reference_root: str) -> None: ...       # download, gunzip, samtools faidx, gatk CreateSequenceDictionary
def fetch_coding_intervals(reference_root: str) -> None: ...
def fetch_vep_cache(reference_root: str) -> None: ...     # long; supports --skip-vep
def verify_references(reference_root: str) -> dict: ...   # returns per-asset present/missing
```

This is a **setup script the operator runs once per machine**, deliberately not something the app does on demand — genomics reference data is large, versioned, and worth an explicit human decision. Add to `TECHNICAL_SPEC.md` §8 alongside `make_test_data.py` and `check_tools.py`.

**`ToolsController`** gains `GET /api/tools/references` returning `verify_references()` output, so the dashboard can show what's missing before a run fails ten minutes in.

---

## 7. Step timeouts belong in config

`HlaTypingService` hardcodes `TimeoutSeconds = 600`; `VariantCallingService` hardcodes `7200`. Both were wrong for real data.

**`Common/AppConfig.cs`:**

```csharp
public Dictionary<string, int> StepTimeoutSeconds { get; set; } = new();
public int GetStepTimeout(string stepId) =>
    StepTimeoutSeconds.TryGetValue(stepId, out var t) ? t : LongStepTimeoutSeconds;
```

**`appsettings.json`:**

```json
"StepTimeoutSeconds": {
  "02_alignment": 86400,
  "03_variants": 86400,
  "04_protein_effects": 14400,
  "05_hla_typing": 3600,
  "07_presentation": 3600,
  "08_immunogenicity": 7200,
  "11_vaccine_design": 3600
}
```

Every service replaces its literal with `Config.GetStepTimeout(StepId)`.

---

## 8. Logging and progress visibility

### 8.1 The problem

`PythonRunner` captures subprocess stdout/stderr through pipes and surfaces them only after the process exits. For a ten-minute step this means ten minutes of total silence, and when the backend died mid-run the output was lost entirely. Diagnosing the OptiType failures required patching `python/common/response.py` to also append to a fixed file, purely so `tail -f` had something to read.

### 8.2 Fix — make streaming a first-class feature

**`Common/PythonRunner.cs`** — `PythonExecutionOptions` already declares `OnStdoutLine` / `OnStderrLine` callbacks in the spec. Actually use them: read the streams line-by-line as they arrive rather than buffering to completion, and have `PipelineStepBase` wire them to both (a) the `ILogger`, so lines appear in `backend.log` live, and (b) `JobManager.UpdateProgress`, so `JobRecord.LogTail` reflects recent output while the job is still running.

**`Models/JobRecord.cs`** — `LogTail` was always `null` in practice. It should hold the last ~50 lines of subprocess output, updated live.

**`python/common/response.py`** — keep `log()` writing to `sys.stderr` with `flush=True` (that part was always correct), and drop the file-append hack once C# streams properly.

**Frontend** — `StepRunButton` / step panels can poll `GET /steps/{id}/jobs/{jobId}` and render `logTail` in a small scrolling output box while a step runs. This is the difference between "is it working?" and "it's on chromosome 3 of 24."

---

## 9. Single `requirements.txt` and a real setup script

### 9.1 What went wrong

Everything below had to be discovered and fixed by hand during deployment. None of it is in any requirements file:

- `python3.11-tkinter` — `pvacseq` fails on `import turtle` without it
- `setuptools<81` — `mhcflurry` uses `pkg_resources`, removed from newer setuptools
- `samtools` — not in Amazon Linux 2023 repos; must build from source
- `htslib` (`bgzip`, `tabix`) — VEP's installer requires them
- `Bio::DB::HTS::Tabix` — Perl module VEP needs; `--NO_HTSLIB` does not cover it
- `bwa-mem2` — build from source
- `gatk` — zip release, plus needs a `python` (unversioned) symlink for its wrapper
- OptiType — `pip install -e .` from a git clone, not from PyPI
- `curl` conflicts with preinstalled `curl-minimal`
- `glpk-devel` does not exist under that name (it's `glpk` / `glpk-utils` depending on repo)

### 9.2 The deliverable: `requirements.txt` (single file, all Python deps)

```
# ── Core pipeline ────────────────────────────────────────────────
setuptools<81            # mhcflurry imports pkg_resources; removed in >=81
numpy>=1.24,<2
pandas>=2.0,<2.1         # pvactools pins <2.1
pysam>=0.22
biopython>=1.81

# ── Step 7 / 8: binding + immunogenicity prediction ──────────────
mhcflurry>=2.0.6
# NOTE: after install, run `mhcflurry-downloads fetch models_class1_presentation`

# ── Step 5: HLA typing ───────────────────────────────────────────
pyomo>=6.6               # OptiType ILP modelling layer
matplotlib>=3.5          # OptiType coverage plot
click>=8.0               # OptiType CLI
# OptiType itself is NOT on PyPI — installed from source by setup.sh

# ── Steps 6 / 9 / 11: candidates, filtering, vaccine design ──────
pvactools>=7.1
```

### 9.3 The deliverable: `setup.sh` (single script, everything non-pip)

A new top-level `setup.sh` that is idempotent (safe to re-run), checks before installing, and covers every item in §9.1. Structure:

```bash
#!/usr/bin/env bash
set -euo pipefail

# 0. detect distro (dnf vs apt) and set PKG accordingly
# 1. system packages: Development Tools, cmake, wget, unzip, tar, git,
#    java-17, perl + perl-DBI + perl-DBD-MySQL + perl-Archive-Zip,
#    python3.11 + python3.11-devel + python3.11-tkinter, glpk, glpk-utils
#    (NOTE: do NOT install `curl` — conflicts with preinstalled curl-minimal)
# 2. python: unversioned symlink if absent (gatk's wrapper needs `python`)
# 3. dotnet SDK — version must match global.json (see §10)
# 4. node 20
# 5. htslib from source  -> bgzip, tabix          [check: command -v bgzip]
# 6. samtools from source                          [check: command -v samtools]
# 7. bwa-mem2 from source                          [check: -x $HOME/bwa-mem2/bwa-mem2]
# 8. gatk zip release                              [check: command -v gatk]
# 9. Bio::DB::HTS (Perl) from Ensembl/Bio-HTS      [check: perl -MBio::DB::HTS::Tabix -e1]
# 10. ensembl-vep + `perl INSTALL.pl --AUTO ac --SPECIES homo_sapiens --ASSEMBLY GRCh38 --NO_TEST`
# 11. python venv + pip install -r requirements.txt
# 12. mhcflurry-downloads fetch models_class1_presentation
# 13. OptiType: git clone + pip install -e .
# 14. write $HOME/.neoantigen_env with all TOOL_* paths + ASPNETCORE_ENVIRONMENT
# 15. print a summary table of every tool: found / installed / FAILED
```

Two design points worth stating explicitly:

- **Every step guarded by a `command -v` / file-exists check**, so a re-run after a failure resumes rather than redoing everything. The deployment session lost real time to re-running things that were already done.
- **Ends with a verification table**, not silence. `python/tools/check_tools.py` already exists in the spec for this — call it as the final step.

**Env file rather than `.bashrc` edits**: `setup.sh` writes `$HOME/.neoantigen_env` containing every `TOOL_*` export plus `ASPNETCORE_ENVIRONMENT=Development`. The operator sources it (or systemd `EnvironmentFile=`s it). This is reproducible; appending to `.bashrc` from a script is not, and the deployment ended up with duplicate `OptiType` PATH entries from exactly that.

**Spec change:** `TECHNICAL_SPEC.md` §1 gains `setup.sh` and `requirements.txt` at repo root; §8 documents `fetch_references.py`.

---

## 10. Toolchain version pinning

`.slnx` (newer solution format) and `net10.0` were generated by the Mac's SDK and rejected by the server's .NET 8. This wasted an hour before the server SDK was upgraded.

**Add `global.json` at repo root:**

```json
{ "sdk": { "version": "10.0.100", "rollForward": "latestFeature" } }
```

Pick whichever major version the Mac actually has (`dotnet --list-sdks`) and make `setup.sh` install that exact major on the server. Pinning in one place beats discovering the mismatch at build time.

**Also:** either regenerate the solution as `.sln` (widely compatible) or keep `.slnx` and require the newer SDK via `global.json`. Don't leave it ambiguous.

---

## 11. Deployment ergonomics

These aren't code bugs, but each one cost real time and each has a small permanent fix.

| Problem | Fix |
|---|---|
| Public IP changes on every stop/start; `.env.local`, `AllowedOrigins`, `next.config.js` all need updating | Allocate an **Elastic IP**. Also add a `scripts/set-host.sh <ip>` that rewrites all three in one command. |
| `next.config.js` `allowedDevOrigins` hardcodes the IP | Read from `process.env.NEXT_PUBLIC_DEV_ORIGIN` |
| Backend port defaulted to 5163, docs said 5000 | Pin `applicationUrl` in `launchSettings.json` to 5000 and correct the docs, or standardise on 5163 everywhere. Either is fine; the inconsistency is the problem. |
| SSH drop kills foreground backend/frontend/long jobs | Document `tmux`/`screen` as the standard way to run them, or add systemd units. `nohup` worked but is easy to forget. |
| Zip transfer to server included `node_modules`, `.next`, `__MACOSX` | Use `git clone` on the server. If transferring directly, `rsync --exclude` per the excludes already in `.gitignore`. |
| Security group only had port 22 | Document 22 / 3000 / 5163 as the required inbound set in a new `DEPLOY.md` |

---

## 12. Carried-forward TODOs (from earlier in the project, still open)

1. **Male (XY) sample handling** — no PAR-masking of the reference, no sex detection. A male sample risks duplicate/incorrect calls in the X/Y pseudoautosomal regions. Needs: a PAR-masked reference variant, sex inference (from X/Y coverage ratio or metadata), and reference selection wired through `PathResolver`.
2. **MHC class II predictions** — pipeline is class I only. Affects `type_hla.py` (`--include-class-ii` is accepted but unused), step 7/8 predictors, and the candidate model.
3. **Server-side fixes → git** — all deployment fixes were made directly on the EC2 box via `nano`/`sed` and exist only there. Either port them into this correction plan's changes (preferred, since the plan supersedes them) or `git diff` on the server and cherry-pick.
4. **Single-pass setup** — addressed by §9.
5. **Demo video** — unblocked once the pipeline runs end to end.

---

## 13. Suggested order of work

Ordered so that each stage makes the next one debuggable, and so nothing requires local execution of heavy tools.

**Stage 1 — config consolidation (Mac, no testing needed)**
§1, §2, §7, §10. Pure C# edits plus config files. Removes the entire class of "changed a setting, nothing happened" failures. `dotnet build` is sufficient verification.

**Stage 2 — setup and provisioning (Mac, tested on server)**
§9, §6. Write `requirements.txt`, `setup.sh`, `fetch_references.py`. These can only be truly tested on a fresh server, but a fresh EC2 instance is the honest test — and a much faster one than the manual deployment was.

**Stage 3 — real code bugs (Mac, unit-testable)**
§3, §5.1, §5.2, §5.3. `ReadTextFile` guards, step status resolution, job reconciliation, tool env projection. All unit-testable per the existing Tier-1 strategy in `PROJECT_PLAN.md` §7 — no bioinformatics tools required.

**Stage 4 — Python script fixes (Mac, verified on server)**
§4 (`type_hla.py` FASTQ rewrite), §5.5 (`FilterMutectCalls`), §5.6 (VEP compression). Logic is straightforward; correctness needs real data, so verify on the server.

**Stage 5 — logging and progress (Mac)**
§8. Makes Stage 4's server verification dramatically less painful. Arguably worth pulling earlier.

**Stage 6 — deployment ergonomics**
§11, plus `DEPLOY.md`. Elastic IP, `set-host.sh`, tmux/systemd documentation.

**Stage 7 — carried-forward features**
§12 items 1 and 2 — genuine feature work, not corrections.

---

## Appendix — files touched, at a glance

**New files**
- `requirements.txt` (root)
- `setup.sh` (root)
- `global.json` (root)
- `DEPLOY.md` (root)
- `scripts/set-host.sh`
- `python/tools/fetch_references.py`

**Modified — C#**
- `Common/AppConfig.cs` — remove inline defaults, extend `Validate()`, add `StepTimeoutSeconds` + `GetStepTimeout`
- `Common/PathResolver.cs` — add `GetIntervalsPath`
- `Common/PythonRunner.cs` — project `TOOL_*` env vars; stream stdout/stderr live
- `Common/FileSystemService.cs` — `ReadTextFile` size + binary guards
- `Common/PipelineStepBase.cs` — expose `Config`; status resolution order; wire streaming callbacks
- `Common/JobManager.cs` — `ReconcileOrphanedJobs`; live `LogTail` updates
- `Common/IPipelineStep.cs` — add `PrimaryOutputPatterns`
- `Common/PatientRepository.cs` — reference genome default from config
- `Controllers/ToolsController.cs` — add `GET /api/tools/references`
- `Services/02_Alignment/AlignmentService.cs` — remove hardcoded genome fallbacks
- `Services/03_VariantCalling/VariantCallingService.cs` — genome fallback; intervals default
- `Services/05_HlaTyping/HlaTypingService.cs` — timeout from config
- `Services/09_Filtering/FilteringService.cs` — genome fallback
- `Program.cs` — config-provenance startup log
- `Properties/launchSettings.json` — explicit `ASPNETCORE_ENVIRONMENT`, pinned port
- `appsettings.json` / `appsettings.Development.json` — complete valid defaults; `StepTimeoutSeconds`

**Modified — Python**
- `python/common/config.py` — add `mhcflurry` to `ToolConfig`
- `python/common/response.py` — drop the file-append debug hack
- `python/common/io_utils.py` — add `capture_output` to `run_command` (or leave, and have callers use `subprocess` directly — pick one and make the spec match)
- `python/scripts/type_hla.py` — full rewrite: extract → downsample → FASTQ → OptiType
- `python/scripts/call_variants.py` — wire in `filter_calls()`
- `python/scripts/annotate_effects.py` — bgzip the output

**Modified — Frontend**
- `next.config.js` — `allowedDevOrigins` from env
- `components/steps/FileTable.tsx` — no preview button for binary extensions
- `components/steps/StepRunButton.tsx` (or panels) — render live `logTail`

**Modified — Spec**
- `TECHNICAL_SPEC.md` §1 (new root files), §3 (`StepState`), §4 (`AppConfig`, `PathResolver`, `PythonRunner`, `IPipelineStep`, `JobManager`), §6 (Step 3 intervals, Step 5 rewrite), §8 (`type_hla.py`, `fetch_references.py`), Appendix B (config layering)
- `PROJECT_PLAN.md` §7 (testing — add that reference provisioning is a Tier-2 prerequisite), §8 (cross-cutting — config provenance, timeouts)
