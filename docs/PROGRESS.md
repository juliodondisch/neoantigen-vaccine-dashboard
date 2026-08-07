# Progress

## Current state (2026-08-07)

Full stack scaffolded and wired end-to-end: Next.js frontend, ASP.NET Core backend (all 11
pipeline steps registered via `StepRegistry`), Python scripts for every step, C# unit tests
passing, backend smoke-tested live against the dev/tests fixture-seeding endpoints.

**What actually runs for real, locally (verified live via curl):**
- 01 upload (manifest), 06 candidates (real sliding-window logic, no stub), 07 presentation
  (stub — mhcflurry installed but its model weights aren't downloaded, see Temp patches),
  08 immunogenicity (stub), 09 filtering (real self-similarity + expression logic against
  `data/references/proteome/mini_proteome.fasta`), 10 ranking (real C# scoring + HLA-spread
  selection, no Python).

**What's correctly SKIPPED (tool missing), not failed:** 02 alignment (bwa-mem2/samtools),
03 variants (gatk), 04 protein effects (vep), 05 HLA typing (OptiType), 11 vaccine design
(pvacvector). All five have real Python implementations written from the spec, gated behind
`ValidateRequiredTools()`, and are exercised via `FixtureSeeder` for downstream testing
instead. Their exact CLI flag shapes are unverified against real installs — see TEMP-PATCH
comments in each script.

**Frontend:** full component tree built (types, Zustand stores, API client, all layout/patient/
step/common/dev components, all 11 step panels, both pages). `npm run build` and `npm run lint`
pass. Not yet exercised in an actual browser — no display available in this environment; only
verified via production build + the backend endpoints it calls.

## Backend/frontend contract fixes made during integration

- Enum JSON serialization (`StepStatus`, `JobStatus`) now uses `JsonStringEnumConverter` —
  was serializing as raw ints, which didn't match the frontend's string-literal types.
- `GET /api/steps` added as a non-patient-scoped alias for step definitions (spec §11's
  `listStepDefinitions()` takes no patientId; spec §14's contract table nests it under
  `/api/patients/{pid}/steps`). Both routes now work.
- `predict_presentation.py` falls back to the stub predictor when mhcflurry raises (not just
  when the binary is missing from PATH) — mhcflurry is importable in `.venv` here but its
  downloaded model weights aren't present.

## Pending server verification

Deferred per CLAUDE.md unattended-mode — implemented against the spec, not run against real
tools:
- `align.py` (bwa-mem2/STAR flag shapes)
- `call_variants.py` (Mutect2/GATK flag shapes, matched-normal requirement)
- `annotate_effects.py` (VEP flag shapes, `--database` vs cache mode)
- `type_hla.py` (OptiType flag shapes, `_result.tsv` output parsing)
- `design_vaccine.py`'s `run_pvacvector()` (raises until verified; construct assembly itself —
  linkers/UTRs/codon table — is real, tested logic, just never run past the pvacvector gate)

## Temporary patches

- `predict_presentation.py`: mhcflurry model weights not downloaded (would require a network
  fetch of unknown size on a disk-constrained machine) — falls back to `predict_stub()`.
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
  `AlignmentIntegrationTests`, etc.) — only the four required Tier-1 unit test classes exist
  (`SlidingWindowGeneratorTests`, `ScoreCalculatorTests`, `HlaSpreadSelectorTests`,
  `PathResolverTests`), all passing (25/25).
- Frontend not verified in an actual browser (no display in this environment).
- `data/references/GRCh38` and `vep_cache` intentionally absent — never download these here.
- Step 1 upload: server-side-path registration mode exists (`FilesController.RegisterPath`)
  but is untested against a genuinely huge file.

## Disk

System-wide free space dropped from ~3.8GB to ~1GB during this session from causes outside
this repo (this repo's own additions total a few MB of source text; `.venv` at 1.4GB and
`frontend/node_modules` at 446MB both predate this session). Recommend freeing disk space
before doing anything disk-heavy (installing mhcflurry's model weights, real chr21, etc.).
