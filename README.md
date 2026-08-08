# Neoantigen Vaccine Design Pipeline

A web app that takes a cancer patient's tumor and normal DNA (plus optional RNA-seq) and
runs the full computational pipeline to design a personalized mRNA cancer vaccine sequence:
alignment → somatic variant calling → protein-effect annotation → HLA typing → candidate
neoantigen generation → HLA presentation & immunogenicity prediction → safety/expression
filtering → weighted ranking → vaccine construct assembly.

Next.js frontend, ASP.NET Core (C#) orchestration, Python for the actual bioinformatics.

## Documentation map

| Doc | What's in it |
|---|---|
| [`docs/PROJECT_PLAN.md`](docs/PROJECT_PLAN.md) | What this is, why each step exists, the testing strategy |
| [`docs/TECHNICAL_SPEC.md`](docs/TECHNICAL_SPEC.md) | The authoritative design — every file, class, field, API contract |
| [`docs/PROGRESS.md`](docs/PROGRESS.md) | Current build state: what's real, what's stubbed, what's unverified |
| [`docs/deviations.md`](docs/deviations.md) | One-line log of places the implementation deviates from the spec |
| [`DEPLOY.md`](DEPLOY.md) | How to set up a real server (tools, reference genome, network access) and run the pipeline against real data |
| [`CLAUDE.md`](CLAUDE.md) | Build conventions and constraints this project was developed under |

## Quick start (local development)

This repo was built on a disk-constrained laptop against stub predictors and tiny synthetic
fixtures — real bioinformatics tools were never installed locally. For that workflow:

```bash
# Backend
cd backend/src/NeoantigenPipeline.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run   # binds :5163, dev endpoints enabled

# Frontend (separate terminal)
cd frontend
npm install
npm run dev                                      # binds :3000
```

Open `http://localhost:3000`. The `/dev/tests` page (behind `NEXT_PUBLIC_ENABLE_DEV_TOOLS=true`
in `frontend/.env.local`) can seed a fixture patient pre-populated through any step, so you can
exercise the UI without running real tools.

`dotnet test` (in `backend/`) runs the C# unit suite — pure logic (sliding-window peptide
generation, ranking math, HLA-spread selection, path resolution), no external tools needed.

## Running against real data on a real server

That's what [`DEPLOY.md`](DEPLOY.md) is for — installing the actual bioinformatics tools,
downloading a real reference genome, and pointing the app at them. Most tool CLI invocations in
this codebase are marked `TEMP-PATCH` in comments because they were written from documented
usage and never run against a real install (no bioinformatics tools fit on the dev machine this
was built on) — expect some debugging the first time each one runs for real. `docs/PROGRESS.md`
has the full honest list of what's verified vs. not.
