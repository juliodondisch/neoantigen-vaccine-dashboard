# Deploying to a real server

This walks through setting up a fresh Linux server (e.g. an AWS EC2 instance) to run the
pipeline against real data, and accessing the frontend from your own machine. Read
[`docs/PROGRESS.md`](docs/PROGRESS.md) first — it lists exactly what's verified vs. still
unverified (most tool CLI invocations were written from documented usage and never run for
real, since no bioinformatics tools fit on the machine this was built on).

## 0. Before you start

- **Sizing**: reference genome + indexes want ~40-60GB; real WGS BAMs run 140-240GB *each* —
  budget storage accordingly (several hundred GB+ if you're pulling in tumor/normal/RNA BAMs).
  32GB RAM is workable but tight for full-genome bwa-mem2 indexing / STAR genome generation —
  expect those steps to be slower, not to fail outright.
- **Reference build match**: if you're bringing in externally-aligned BAMs (e.g. GIAB/NIST
  data), check what reference build and contig-naming convention they were aligned to.
  `setup_reference.py` downloads UCSC's `hg38.fa.gz` (`chr1`, `chr21`, ... naming). If your BAMs
  were aligned against a differently-named GRCh38 build (e.g. NCBI's no-alt analysis set,
  `1`/`2`/... naming), variant calling will misbehave — you'd need to either re-align against
  this reference or point `setup_reference.py`/`PathResolver` at a matching one instead.

## 1. Clone and install everything

```bash
git clone <this-repo> neoantigen-vaccine-dashboard
cd neoantigen-vaccine-dashboard

python3 setup_tools.py --dry-run   # see the plan first
python3 setup_tools.py             # installs conda (if missing), .NET SDK (if missing),
                                    # bwa-mem2/samtools/STAR/salmon/GATK4/OptiType/VEP/
                                    # pvactools/Node.js (via one conda env), mhcflurry
                                    # model weights, and the VEP cache
```

If conda or .NET were just installed, run `source ~/.bashrc` (or open a new shell) before
continuing. Everything else lives inside the conda env (`neoantigen` by default).

## 2. Point the app at the conda env's tools

The app finds tools via `App:ToolPaths` config and `App:PythonExecutable`, which default to
bare names resolved off `PATH`. Rather than editing the committed `appsettings.json` with a
path specific to your machine, set environment variables (ASP.NET Core maps `App__Key` /
`App__ToolPaths__subkey` onto the same config automatically):

```bash
ENV_NAME=neoantigen   # or whatever you passed to --env-name
ENV_BIN="$(conda info --base)/envs/$ENV_NAME/bin"

export App__PythonExecutable="$ENV_BIN/python3"
export App__ToolPaths__bwa-mem2="$ENV_BIN/bwa-mem2"
export App__ToolPaths__samtools="$ENV_BIN/samtools"
export App__ToolPaths__gatk="$ENV_BIN/gatk"
export App__ToolPaths__STAR="$ENV_BIN/STAR"
export App__ToolPaths__vep="$ENV_BIN/vep"
export App__ToolPaths__OptiType="$ENV_BIN/OptiTypePipeline.py"
export App__ToolPaths__pvacseq="$ENV_BIN/pvacseq"
export App__ToolPaths__pvacvector="$ENV_BIN/pvacvector"
```

Put these in `~/.bashrc` (or a systemd unit's `Environment=` lines, if you run it that way) so
they survive reboots/new shells. `mhcflurry-predict` resolves automatically once `python3`
above is the conda env's — no separate override needed.

## 3. Download the reference genome

```bash
python3 python/scripts/setup_reference.py \
    --genome GRCh38 --output-dir data/references/GRCh38 --include-rna true --dry-run true   # sanity check first

python3 python/scripts/setup_reference.py \
    --genome GRCh38 --output-dir data/references/GRCh38 --include-rna true
```

This also builds the bwa-mem2 index (slow — this is the long step) and, with `--include-rna`,
the Salmon transcriptome index for expression quantification. The app's alignment step will
also trigger this automatically on first run if it detects the reference is missing and there's
enough disk space — but running it explicitly first lets you watch it happen and catch problems
before kicking off a real patient run.

## 4. Start the app

```bash
# Backend — from backend/src/NeoantigenPipeline.Api
ASPNETCORE_ENVIRONMENT=Production dotnet run --urls http://0.0.0.0:5163

# Frontend — from frontend/, separate terminal
cp .env.local.example .env.local   # if not already present; edit if doing direct access (step 5b)
npm install
npm run build && npm run start     # production build; `npm run dev` also works for iteration
```

`appsettings.json` (tracked, no separate config file needed) ships with `EnableDevEndpoints:
false` and safe defaults — the relative paths (`../../../data`, etc.) only resolve correctly
when the backend is launched from `backend/src/NeoantigenPipeline.Api` as shown above.

## 5. Access the frontend from your Mac

### Option A — SSH tunnel (recommended: simplest, no config changes, nothing exposed publicly)

```bash
ssh -L 3000:localhost:3000 -L 5163:localhost:5163 <user>@<instance-address>
```

Leave that session open, then open `http://localhost:3000` in your Mac's browser as normal — it
behaves exactly like local development. `AllowedOrigins` in `appsettings.json` already defaults
to `http://localhost:3000`, which is what the browser sends regardless of the tunnel, so no CORS
changes are needed.

### Option B — direct access via the instance's public address

Needs three changes:

1. Open the port in your security group (3000 for the frontend; keep 5163 restricted to your IP
   if possible — no reason to expose the API publicly beyond what the frontend needs).
2. Tell the backend to accept that origin:
   ```bash
   export App__AllowedOrigins__0="http://<instance-public-ip-or-domain>:3000"
   ```
3. Tell the frontend where the backend actually is (edit `frontend/.env.local`):
   ```
   NEXT_PUBLIC_API_BASE_URL=http://<instance-public-ip-or-domain>:5163
   ```

Rebuild/restart the frontend after changing `.env.local` (`NEXT_PUBLIC_*` vars are baked in at
build time).

## 6. Verify before touching real data

Temporarily enable the dev/test endpoints to seed a fixture patient and confirm the whole chain
works end to end before downloading anything large:

```bash
export App__EnableDevEndpoints=true
# restart the backend, then:
curl -X POST http://localhost:5163/api/dev/tests/seed \
  -H "Content-Type: application/json" -d '{"seedThroughStepId":"09_filtering"}'
```

Use the returned patient ID to run step 10 (ranking) and step 11 (vaccine design) via the UI or
`POST /api/patients/{id}/steps/{stepId}/run` — these don't need any of the heavy tools and prove
the orchestration layer, disk-space checks, and file plumbing work on this machine. Once
satisfied, unset `App__EnableDevEndpoints` (or set it back to `false`) before moving to real
patient data.

## 7. Bring in real data

For large BAMs (140-240GB), don't use the browser upload zone — register the file path directly
so the app reads it in place rather than copying it:

```bash
# Small files: multipart upload works fine, e.g.
curl -X POST http://localhost:5163/api/patients/{patientId}/steps/02_alignment/files/upload \
  -F "files=@/path/to/small.bam" -F "fileKind=tumor_dna"

# Large files already on the server's disk — register in place instead (no copy, no size limit):
curl -X POST http://localhost:5163/api/patients/{patientId}/steps/02_alignment/files/register \
  -H "Content-Type: application/json" \
  -d '{"sourcePath": "/path/to/tumor.bam", "fileKind": "tumor_dna", "copy": false}'
```

BAMs uploaded/registered directly into `02_alignment` skip the alignment step (see
`AlignmentService.HasOwnBams`) and are automatically checked for a valid `@RG SM:` tag,
coordinate sort order, and an index — repaired where possible, or reported with a clear error if
not (`python/scripts/validate_bam.py`). See `docs/PROGRESS.md` for what's been tested vs. not.

## What to expect

The orchestration (disk checks, job tracking, patient logging to `data/patients/{id}/patient.log`,
graceful degradation when a tool/model is missing) is tested and solid. The actual bioinformatics
tool invocations are not — they're written from documented CLI usage and marked `TEMP-PATCH`
throughout the Python scripts. The first real run of each tool is likely to surface a flag
mismatch or two; `patient.log` and each step's error response will show the tool's actual stderr
verbatim, which is the fastest way to fix it.
