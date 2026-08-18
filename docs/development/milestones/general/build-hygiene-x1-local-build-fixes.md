# BUILD-HYGIENE-X1 — local build fixes after FTC-06 validation

## 1. Problem statement

STEP-AP242-HARDEN-X1 completed its intended FTC-06 `ADVANCED_FACE.same_sense` preservation work, but local validation still exposed two unrelated hygiene blockers in the everyday developer loop:

- `scripts/test-active.sh` failed under bash because the shell scripts were checked in with CRLF line endings.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore` could fail on Windows with locked files under `obj`, including `Aetheris.Server` static web asset cache files and Roslyn-produced intermediate DLLs in other projects.

These fixes are local build/test hygiene only.

## 2. CRLF shell script root cause

All tracked `scripts/*.sh` files were stored with CRLF line endings. Under bash this surfaced as:

```text
set: pipefail\r: invalid option name
```

## 3. Line-ending policy

The scripts were normalized to LF and the repo policy now explicitly protects shell scripts with:

```gitattributes
*.sh text eol=lf
```

This keeps Git Bash and other POSIX shells from reintroducing CRLF into shell entrypoints.

## 4. File-lock symptom

The full solution build could fail with errors such as:

- `CS2012` on `obj\Debug\net10.0\*.dll` files, often reported as locked by `VBCSCompiler`;
- `MSB3491` / `MSB3501` on `*.FileListAbsolute.txt`;
- `MSB4018` in `Aetheris.Server` static web asset cache files such as `rpswa.dswa.cache.json`.

## 5. File-lock root cause found

This was reproducible locally without any long-lived `Aetheris.Server` process running.

Observed diagnosis:

- `Get-Process Aetheris.Server` returned no live server process.
- Sysinternals `handle.exe` was not installed locally.
- `Get-CimInstance Win32_Process` showed active `VBCSCompiler.exe` plus many `dotnet ... MSBuild.dll /nodemode:1 /nodeReuse:true` worker processes during the failing build.
- The failing solution build logged several projects being built twice in the same invocation, including repeated output lines such as `Aetheris.Kernel.Core -> ...`, `Aetheris.Forge -> ...`, and `Aetheris.Server -> ...`.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore -m:1` succeeded, while the default parallel build reproduced the locks.

The local root cause is therefore solution-build parallelism against shared intermediate outputs in the current solution graph, not a CAD/STEP behavior regression and not a required server-process teardown change.

## 6. Fix applied or local remediation steps

Applied fixes:

- normalized all `scripts/*.sh` files to LF;
- added `.gitattributes` protection for `*.sh`;
- taught the bash helper scripts to resolve `dotnet` reliably on Windows Git Bash by honoring `DOTNET_BIN`, `dotnet`, `dotnet.exe`, or the standard `C:\Program Files\dotnet\dotnet.exe` location;
- aligned `scripts/test-active.sh` with the existing active-suite policy by passing `--filter "Category!=SlowCorpus"` so NIST slow-corpus snapshot audits do not leak back into the default active lane;
- added repo-local `Directory.Build.rsp` with:

```text
-maxCpuCount:1
```

`Directory.Build.rsp` is automatically consumed by command-line MSBuild / `dotnet build`, so the normal repo command:

```bash
dotnet build Aetheris.slnx -f net10.0 --no-restore
```

now runs with the same sequential scheduling that proved stable during diagnosis.

If someone intentionally wants to bypass repo defaults for an experiment, they can use `-noAutoResponse` and pass explicit MSBuild switches themselves.

## 7. Commands run

Commands used during diagnosis and validation:

```bash
file scripts/*.sh
grep -RIl $'\r' scripts || true
bash -n scripts/test-active.sh
bash -n scripts/test-legacy.sh
bash -n scripts/test-corpus.sh
bash -n scripts/test-diagnose-hang.sh
dotnet --info
dotnet restore Aetheris.slnx
dotnet clean Aetheris.slnx -f net10.0
dotnet build Aetheris.slnx -f net10.0 --no-restore
dotnet build Aetheris.slnx -f net10.0 --no-restore -m:1
Get-Process dotnet -ErrorAction SilentlyContinue | Select-Object Id,ProcessName,Path,StartTime
Get-Process Aetheris.Server -ErrorAction SilentlyContinue
Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'VBCSCompiler|dotnet' } | Select-Object ProcessId,Name,CommandLine
```

Post-fix validation also included the active script/test loop and focused FTC-06 coverage.

## 8. Remaining limitations

- The underlying duplicate-work scheduling shape of the current solution graph still merits future cleanup if faster parallel solution builds become a priority.
- The repo-local mitigation intentionally favors reliable local command-line builds over maximum build parallelism.
- The JavaScript SDK still reports existing npm audit warnings during build; those warnings were pre-existing and are outside BUILD-HYGIENE-X1 scope.

## 9. Non-goals

This milestone does not:

- change STEP import/export semantics;
- change CAD/kernel behavior;
- change BRep topology behavior;
- change Firmament V2 language semantics or lowering behavior;
- change AIR Region route policy;
- change CIR behavior;
- change Firmasm behavior.
