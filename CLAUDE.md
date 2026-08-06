# CLAUDE.md — Neoantigen Pipeline

A web app that takes a cancer patient's tumor + normal DNA and works through the full computational pipeline to design a personalized mRNA vaccine sequence. Next.js frontend, C#/ASP.NET Core orchestration, Python for the actual bioinformatics tools.

Right now this is being built **on a Mac M1 with roughly 10GB of free disk**. Most bioinformatics tools are not installed and most real data is far too large to touch. Build against stubs and tiny fixtures; the heavy stuff runs on a server later. This constraint shapes everything below.

## Required reading (in this order, once, at session start)

1. `docs/PROJECT_PLAN.md` — what this is, why each step exists, the testing strategy, the build order. Read once for context; do not re-read repeatedly.
2. `docs/TECHNICAL_SPEC.md` — **the authoritative design.** Every file, class, field, and function signature. Directory structure and API contracts are binding. Do not invent structure beyond it. If reality forces a deviation (a library API differs, a signature doesn't compile), make the smallest possible deviation and note it in `docs/deviations.md` in one line.

The spec is detailed enough that you should rarely need to design anything. When you find yourself inventing a class or endpoint that isn't in the spec, stop and check whether you missed it.

## Build order — follow this, not the step numbering

Steps 1–4 are slow, need tools that aren't installed, and need data that doesn't fit on this disk. Do not start there.

1. **Skeleton.** Next.js shell, C# API, patient CRUD, folder creation, `StepRegistry`, one dummy step that only lists files. Prove the full loop (React → API → service → disk → back) works.
2. **Test harness + `FixtureSeeder`.** Build this early, not last. Seeding a patient with steps 1–7 pre-populated is what makes everything else developable without running alignment.
3. **Steps 5–8** (HLA typing → candidates → presentation → immunogenicity). Fast, cheap, testable. Use stub predictors where tools are missing.
4. **Steps 9–11** (filtering, ranking sliders, vaccine design). Step 10 is pure C# math — no Python, fully unit-testable, and the most interesting logic in the project.
5. **Steps 2–4** (alignment, variant calling, protein effects). Mostly thin wrappers around external tools. Write them, stub the tool calls, mark as pending server verification.
6. **Step 1 upload.** Browser upload won't handle 150GB files — the server-side-path registration mode is the real path. Solve it once the rest works.

## How to work

- Work autonomously through the build order. After each meaningful unit of work: build, run tests, commit.
- Test after every step you complete. Not at the end of a phase — after each step. `dotnet test` for C#, `npm test` for frontend if configured.
- A step is not done until its unit tests pass. Integration tests that need missing tools are **skipped, not failed** — see tool availability below.
- Secrets and machine-specific paths live in `.env` / `appsettings.Development.json` (both gitignored). Never print full key values.
- Bypass permissions is on. Still: no destructive or system-wide actions. Never `rm -rf` outside the repo, never modify system config, **never download reference genomes or real sequencing data** — that will fill the disk.

## Disk discipline (this machine has ~10GB free)

- Never download GRCh38, VEP cache, UniProt, or any real patient data.
- Test fixtures are generated, not downloaded, and the whole fixture set must stay under ~500MB. `python/tools/make_test_data.py` builds them.
- If a task seems to require multi-GB reference data, that's a signal it belongs in Tier 2 — stub it and defer.
- Before writing anything large, check available space. If a write would take free space below ~2GB, stop and flag it instead.

## Tool availability — expect most tools to be missing

bwa-mem2, GATK, VEP, OptiType, STAR are probably not installed. This is normal and planned for.

- Every step service implements `ValidateRequiredTools()`. Use it.
- Missing tool → the step reports **SKIPPED (tool missing)**, visually distinct from FAILED. Never let a missing tool surface as a crash or a red failing test.
- MHCflurry and pVACtools are pip-installable and small — those you can actually run locally.
- Stub predictors (`--use-stub`, `predict_stub()`) exist in the spec by design. Use them freely for local development. A stub is not a temp patch; it's a specified feature.

## Debugging protocol (important — do not loop)

When something fails (build error, failing test, unexpected runtime behavior):

1. State the observed problem in one or two sentences.
2. Form **one** most-likely cause. Briefly — no long chains of speculation.
3. Apply one targeted fix and re-run the failing check.
4. If it passes: continue. If the fix didn't solve it, or caused a new problem: **stop. Do not attempt a third theory.** Record it and move on per unattended mode below.

Hard limits: maximum **2 fix attempts per distinct problem**, never undo-and-redo the same change twice, never restructure the project to route around a bug. If an error you "fixed" earlier reappears, that counts as unsolved.

## Do not guess external interfaces

Bioinformatics CLI tools have unintuitive and version-dependent flags. GATK, VEP, OptiType, and bwa-mem2 argument shapes are unverified until run against real installs.

- Never invent or "correct" CLI flags from memory. Run `--help` on the installed tool and match the code to what's actually there.
- If the tool isn't installed, write the call from the spec, mark it `// TEMP-PATCH: unverified CLI, needs real tool run`, and defer verification to the server pass.
- Same for library APIs (pysam, MHCflurry's Python surface, Zustand middleware): check the installed package's real surface rather than trying variations. Two failed signature guesses on the same call is an unsolved problem — stop.

## Unattended mode

The user is not present. Do not block waiting for a reply.

- **Anything needing real tools or real data:** implement against the spec, verify what you can (argument construction, file paths, response parsing with a fake tool output), then record it in `docs/PROGRESS.md` under `Pending server verification` with exactly what needs running. Deferred, not skipped.
- **When blocked** (debugging limit hit, or something genuinely needs the user): apply a clearly-marked temporary patch that lets progress continue — a stub, a mock, a hardcoded value, or the feature disabled behind a config flag. Every patch must: (1) carry a `// TEMP-PATCH:` comment explaining what's fake and why, (2) be listed in PROGRESS.md under `Temporary patches` with what the real fix needs, and (3) never be treated as the real implementation. A gate passed via a patch is recorded as `passed (patched)`.
- Keep patches minimal. Do not build elaborate fake systems.
- Distinguish **stubs** (specified in the spec, permanent, fine) from **temp patches** (unplanned, marked, tracked). Don't file stubs as patches.

## Checkpoints and progress

- Git-commit after every step that builds and passes tests, and before any risky change.
- **Commit messages: short and lowercase.** `add path resolver`, `fix vcf parse`, `sliding window tests pass`, `wire up ranking sliders`, `stub optitype`. No conventional-commit prefixes, no ceremony, no multi-line bodies.
- If a fix attempt fails, prefer `git checkout` back to the last commit over hand-reverting edits.
- Maintain `docs/PROGRESS.md` — keep it short, overwritten as you go:
  - current step being built
  - last thing that passed
  - open issues
  - `Pending server verification` list
  - `Temporary patches` list
- On session start, read PROGRESS.md first. If it exists, resume from there instead of starting over.

## Token discipline

- Keep responses short: what you did, what passed/failed, what's next. No restating the plan, no summarizing the spec back, no progress essays.
- Do not re-read the plan and spec on every step — consult the specific section you need.
- Prefer one decisive command over several exploratory ones.
- Test output, build logs, and tool stderr go to files, not into the conversation. Quote only the failing lines.

## Commands

**Backend**
- Build: `dotnet build`
- Test: `dotnet test`
- Unit only: `dotnet test --filter Category=Unit`
- Run: `dotnet run --project backend/src/NeoantigenPipeline.Api`

**Frontend**
- Install: `npm install` (in `frontend/`)
- Dev: `npm run dev`
- Build: `npm run build`
- Lint: `npm run lint`

**Python**
- Install: `pip install -r python/requirements.txt`
- Tool check: `python python/tools/check_tools.py`
- Build fixtures: `python python/tools/make_test_data.py`

**Dev UI**: `/dev/tests` (requires `NEXT_PUBLIC_ENABLE_DEV_TOOLS=true`)

## Things that will bite you

- **DI registration.** Several step services depend on other step services (`RankingService` needs `FilteringService` and `HlaTypingService`). Each must be registered as both its concrete type *and* as `IPipelineStep`. See Appendix A of the spec. Getting this wrong produces confusing runtime resolution errors.
- **`SlidingWindowGenerator` exists in both C# and Python.** The C# version is authoritative and unit-tested. Keep them behaviorally identical or the pipeline will disagree with itself.
- **Step 10 runs in C#, not Python.** It needs a fast preview endpoint for live slider updates. Don't route it through a subprocess.
- **HLA spread is a set-level property**, not a per-candidate score. It's greedy selection with a diversity penalty (`HlaSpreadSelector`), not another term in the weighted sum. Don't collapse it into `ScoreCalculator`.
- **Files are never overwritten or deleted.** Every output gets a `{yyyyMMdd_HHmmss}` suffix. Old runs stay.
- **Error toasts are persistent**, success toasts auto-dismiss. Python stderr flows through to the user verbatim — do not genericize it into "Step failed".
- **Off-by-one in the sliding window.** Mutations near a protein's start or end produce fewer windows. Test the boundaries; that's where the bugs live.
- **Border radius stays tiny.** Appendix C of the spec caps radius at 4px anywhere in the app, and the Tailwind config deliberately omits `rounded-xl` and above. Do not re-add them. Soft corners make this read as consumer SaaS; near-square reads as an instrument. When unsure, use less.
- **Design tokens are malleable, class signatures are not.** Appendix C is a starting point — adjust a color or spacing value if it doesn't work against real data, and note the change in `docs/deviations.md`. The four principles at the top of C.1 are what shouldn't drift.

## Current state

Nothing exists yet. Start with the skeleton (build order item 1), then the test harness, then steps 5–8.
