# Progress

## Fourth pass (2026-08-08) — AWS deployment: runtimes, config, network, docs

Before this, `setup_tools.py` only installed bioinformatics tools — .NET/Node.js/conda itself
weren't covered, `appsettings.json` shipped with an absolute `/data` path nobody had actually
run against, CORS was hardcoded to `localhost:3000` only, and there was no deploy doc.

- **`setup_tools.py`** now checks-then-installs conda itself (Miniforge, Linux only — the
  actual deploy target), .NET SDK (via Microsoft's official `dotnet-install.sh`), and Node.js
  (`conda-forge::nodejs`, added to the same env so architecture detection is conda's problem,
  not a raw-binary-URL guess). Fully idempotent — safe to re-run.
- **`appsettings.json` (tracked) is now the real, working config** — no more relying on the
  gitignored `appsettings.Development.json` to make paths resolve. Verified live with
  `dotnet run --no-launch-profile` (bypassing `launchSettings.json`'s dev-environment default):
  `Hosting environment: Production`, dev endpoints correctly 404, patient CRUD works, relative
  data/python paths resolve correctly from a clean environment.
- **CORS is now configurable** (`App:AllowedOrigins`, plumbed through `AppConfig` and read
  directly off `IConfiguration` in `Program.cs` since it's needed before DI is built). Default
  still `localhost:3000` (works transparently over an SSH tunnel — the recommended access path,
  documented in `DEPLOY.md`); direct public access is supported via an env var override.
- **Found and fixed a real bug while writing `DEPLOY.md`'s register-in-place example:**
  `RegisterExternalFileAsync`'s `copy: false` path (the one meant for 140-240GB BAMs too big to
  duplicate) kept the file's original name verbatim instead of the canonical `tumor_*`/`normal_*`
  naming the rest of the pipeline globs for — so a registered BAM not already named that way
  would be silently invisible to `AlignmentService.HasOwnBams` and variant calling. Fixed by
  symlinking into the step folder under the canonical name (still zero-copy) instead of
  recording the bare original path. Verified live with a file named
  `weird_original_name_123.bam` → correctly appears as `tumor_{ts}.bam`, resolves through the
  symlink to the real (un-copied) content.
- Also fixed: `.env.local.example` (frontend) was gitignored by the blanket `.env*` rule and
  never actually committed, despite being referenced as the setup template — README/DEPLOY
  instructions to `cp .env.local.example .env.local` would have failed on a fresh clone. Added
  a `.gitignore` negation and committed it (no secrets in it — just the three `NEXT_PUBLIC_*` vars).
- **New: `README.md`** (doc map, local quick start) **and `DEPLOY.md`** (full AWS sequence:
  install → configure tool paths via env vars → download reference → start both processes →
  access from another machine via SSH tunnel or direct → verify with fixture seeding before
  touching real data → register real BAMs).

## Third pass (2026-08-08) — deployment readiness for a real, offline server run

The user clarified the target server has **no outbound network access at runtime** — every
external fetch must happen once, ahead of time, during setup. Also asked for tool alternatives
that don't need DTU Health Tech's gated academic registration, and BAM validation since their
real workflow uploads externally-aligned BAMs directly (skipping step 2).

**New: `setup_tools.py`** (repo root) — conda/bioconda installer for bwa-mem2, samtools, STAR,
salmon, GATK4, OptiType, VEP, pvactools, run once on the server before anything else. Also
fetches mhcflurry model weights and (by default now, since VEP database mode needs network)
the VEP cache. `--include-bigmhc` best-effort clones BigMHC for step 8.

**New: RNA expression quantification is implemented** (`python/scripts/quantify_expression.py`,
Salmon quasi-mapping — doesn't need STAR/BAM, works directly off FASTQ). Previously this didn't
exist at all; `FilteringService` now runs it automatically when RNA-seq was uploaded and no
pre-made expression TSV exists. `setup_reference.py --include-rna` builds the Salmon index +
tx2gene mapping (parsed straight from Ensembl cDNA FASTA headers — no separate GTF needed).

**Steps 8 & 11 now attempt real (non-stub) predictors, with graceful fallback:**
- Step 8: BigMHC (github.com/KarchinLab/bigmhc, no registration) instead of PRIME/NetMHCpan.
  `predict_immunogenicity.py`'s `predict_bigmhc_im()` is genuinely unverified (BigMHC isn't
  installed anywhere I could test) — any failure falls back to stub automatically, so this is
  safe to ship but likely still silently stubs until debugged against a real install.
- Step 11: `pvacvector` now attempts a real call using MHCflurry as the prediction algorithm
  (also no registration) instead of unconditionally raising. Also unverified. Failure no longer
  blocks vaccine design — the construct still gets built and exported, just without the
  junctional-epitope check, and `junctionalEpitopeCheckRan` in the summary says honestly
  whether it worked.
- `VaccineDesignService.RequiredTools` relaxed to empty (construct assembly is native Python,
  doesn't actually need pvactools) — was incorrectly hard-blocking on a tool it doesn't require.

**New: BAM validation/repair** (`python/scripts/validate_bam.py` + `BamValidationService`) —
runs automatically on every BAM that enters the pipeline via a skip-alignment path (uploaded
directly to either 01_upload or 02_alignment). Checks `samtools quickcheck` integrity, `@RG SM:`
tag correctness (fixes via `samtools addreplacerg` if wrong/missing), coordinate sort order
(fixes via `samtools sort`), and index presence (builds if missing). Corrupt/unfixable BAMs now
fail the alignment step with a clear message instead of surfacing as a cryptic Mutect2 error
three steps later.

**Offline-first default flip:** `UseVepDatabaseMode` / `ProteinEffectsService`'s
`useDatabaseMode` now default to **false** (cache mode) everywhere — database mode queries
Ensembl live over the network, which the target server doesn't have.

**Bug fixed while auditing:** Mutect2 was always being passed `--panel-of-normals <path>` even
when that file didn't exist (this app never ships a PoN), which would've made every real
variant-calling run hard-fail on a missing file. Now only passed if actually present.

**Genuinely still not automatable** (flagged honestly, not worked around):
- NetMHCpan/NetMHCIIpan (DTU Health Tech gated registration) — avoided entirely by routing
  steps 8/11 through BigMHC/MHCflurry instead, per the above.
- BigMHC's pretrained weights still need manual download per its own README even with
  `--include-bigmhc` (the script clones the repo, not the weights — no confident stable URL).
- PRIME is not wired in (kept as an explicit "not installed" error, unlike BigMHC).

## Current state (2026-08-07)

Full stack scaffolded and wired end-to-end: Next.js frontend, ASP.NET Core backend (all 11
pipeline steps registered via `StepRegistry`), Python scripts for every step, C# unit tests
passing, backend smoke-tested live against the dev/tests fixture-seeding endpoints.

**What actually runs for real, locally (verified live via curl):**
- 01 upload (manifest), 06 candidates (real sliding-window logic, no stub), 07 presentation
  (stub ,  mhcflurry installed but its model weights aren't downloaded, see Temp patches),
  08 immunogenicity (stub), 09 filtering (real self-similarity + expression logic against
  `data/references/proteome/mini_proteome.fasta`), 10 ranking (real C# scoring + HLA-spread
  selection, no Python).

**What's correctly SKIPPED (tool missing), not failed:** 02 alignment (bwa-mem2/samtools),
03 variants (gatk), 04 protein effects (vep), 05 HLA typing (OptiType), 11 vaccine design
(pvacvector). All five have real Python implementations written from the spec, gated behind
`ValidateRequiredTools()`, and are exercised via `FixtureSeeder` for downstream testing
instead. Their exact CLI flag shapes are unverified against real installs ,  see TEMP-PATCH
comments in each script.

**Frontend:** full component tree built (types, Zustand stores, API client, all layout/patient/
step/common/dev components, all 11 step panels, both pages). `npm run build` and `npm run lint`
pass. Not yet exercised in an actual browser ,  no display available in this environment; only
verified via production build + the backend endpoints it calls.

## Second pass (2026-08-07, later) ,  verification without subagents

Went through every endpoint by hand with curl plus `npm run dev`/`npm run build`/`npm run lint`,
no subagents. Found and fixed two more real bugs:

- **`FixtureSeeder.SeedVariantsAsync` wrote `somatic_pass_{ts}.vcf.gz.txt`** (plain text with a
  `.txt` tail) while `ProteinEffectsService` looks for glob `somatic_pass_*.vcf.gz` ,  the
  fixture would never actually be found by step 4. Now writes real gzip content with the
  correct extension; verified with `gzip -dc` that it round-trips as a valid VCF.
- **`isUserUploaded` reverted to `false` on every `GET .../files` call** after an upload,
  because `FileSystemService.ToManagedFile` always hardcoded it ,  the flag was only ever
  correct in the immediate upload response. Now inferred from file kind + step (tumor/normal/
  rna files inside `01_upload`), so it survives re-listing.

Confirmed working live (backend on :5163, frontend on :3000, real HTTP calls, not just builds):
patient CRUD (create/list/update/delete), file upload/list/preview/download, `/api/tools` +
`/api/tools/disk`, steps 1/6/7/8/9/10 running for real end to end, steps 2/3/4/5/11 correctly
SKIPPED with a clear missing-tool message, `POST .../10_ranking/preview` (live slider endpoint),
CORS from `localhost:3000` → `localhost:5163`, and all three frontend pages (`/`,
`/patients/[id]`, `/dev/tests`) returning 200 with no server-side render errors.

**Still genuinely unverified** (would need either a real browser or the missing bio tools):
actual client-side rendering/interactivity in a browser (no display in this environment ,  only
confirmed the HTML shell renders and the API it depends on is reachable with correct CORS/JSON
shapes), and the five tool-gated steps' real CLI invocations (bwa-mem2/GATK/VEP/OptiType/
pvacvector ,  none installed here, marked TEMP-PATCH, deferred to server pass per CLAUDE.md).

`npm run lint`: 0 errors, 9 pre-existing warnings (React hooks exhaustive-deps / setState-in-
effect style suggestions in polling hooks) ,  not fixed, non-blocking, cosmetic.

## Backend/frontend contract fixes made during integration

- Enum JSON serialization (`StepStatus`, `JobStatus`) now uses `JsonStringEnumConverter` , 
  was serializing as raw ints, which didn't match the frontend's string-literal types.
- `GET /api/steps` added as a non-patient-scoped alias for step definitions (spec §11's
  `listStepDefinitions()` takes no patientId; spec §14's contract table nests it under
  `/api/patients/{pid}/steps`). Both routes now work.
- `predict_presentation.py` falls back to the stub predictor when mhcflurry raises (not just
  when the binary is missing from PATH) ,  mhcflurry is importable in `.venv` here but its
  downloaded model weights aren't present.

## Pending server verification

Deferred per CLAUDE.md unattended-mode ,  implemented against the spec, not run against real
tools:
- `align.py` (bwa-mem2/STAR flag shapes)
- `call_variants.py` (Mutect2/GATK flag shapes, matched-normal requirement)
- `annotate_effects.py` (VEP flag shapes, `--database` vs cache mode)
- `type_hla.py` (OptiType flag shapes, `_result.tsv` output parsing)
- `design_vaccine.py`'s `run_pvacvector()` (raises until verified; construct assembly itself , 
  linkers/UTRs/codon table ,  is real, tested logic, just never run past the pvacvector gate)

## Temporary patches

- `predict_presentation.py`: mhcflurry model weights not downloaded (would require a network
  fetch of unknown size on a disk-constrained machine) ,  falls back to `predict_stub()`.
  Real fix: `mhcflurry-downloads fetch models_class1_presentation` once disk/network allow.
- `python/tools/make_test_data.py`: generates a synthetic ~5kb "chromosome" instead of
  downloading real chr21, per CLAUDE.md's disk discipline (this machine had ~3.8GB free at
  session start, not the ~10GB assumed, and dropped to ~1GB system-wide mid-session from
  causes outside this repo). Swap in the real UCSC download once run with real disk headroom.
- `FixtureSeeder` lives in `backend/src/NeoantigenPipeline.Api/Testing/` rather than the Tests
  project (spec §7) because `DevTestsController` needs it via DI; the Tests project reaches it
  through its existing ProjectReference. See `docs/deviations.md`.

## Known gaps / not yet built

- Integration/E2E test classes from spec §7 (`StepIntegrationTestBase`,
  `AlignmentIntegrationTests`, etc.) ,  only the four required Tier-1 unit test classes exist
  (`SlidingWindowGeneratorTests`, `ScoreCalculatorTests`, `HlaSpreadSelectorTests`,
  `PathResolverTests`), all passing (25/25).
- Frontend not verified in an actual browser (no display in this environment).
- `data/references/GRCh38` and `vep_cache` intentionally absent ,  never download these here.
- Step 1 upload: server-side-path registration mode exists (`FilesController.RegisterPath`)
  but is untested against a genuinely huge file.

## Disk

System-wide free space dropped from ~3.8GB to ~1GB during this session from causes outside
this repo (this repo's own additions total a few MB of source text; `.venv` at 1.4GB and
`frontend/node_modules` at 446MB both predate this session). Recommend freeing disk space
before doing anything disk-heavy (installing mhcflurry's model weights, real chr21, etc.).
