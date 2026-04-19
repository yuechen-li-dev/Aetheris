# AGENTS.md

- Use Aetheris.CLI to inspect and validate parts/assemblies during development as ground-truth inspection tool. If `aetheris` is not available on PATH, build/run it via dotnet (for example `dotnet run --project Aetheris.CLI -- --help` and corresponding subcommand help) before making assumptions about current capabilities.

- Update documentation opportunistically during development and surface any inconsistencies or friction immediately; do not defer doc fixes, as the system is evolving rapidly and drift must be corrected in-place.

## Convergence rule

Every substantial task must end in exactly one of three states:

1. **Success**  
   The intended capability works in the real path and the real motivating case materially improves.

2. **Meaningful progression**  
   The capability is not complete, but one genuine blocker is removed and the next blocker is isolated with evidence.

3. **Honest stop**  
   Further work would require overbroad scope expansion, excessive debt, brittle patching, or tangled logic. Stop and report the reason with concrete evidence.

Do not continue producing patches once the work stops converging.

Do not confuse activity with progress.
A failed attempt is only acceptable if it leaves behind a narrower problem, stronger evidence, or a justified stop.

Any partial work must leave the codebase in a cleaner, more legible, and more diagnosable state than before.

## Test execution requirement

Do not skip .NET build/test validation just because `dotnet` is missing from the current environment.

If the required .NET SDK/runtime is not installed:
1. detect that explicitly,
2. install the required .NET version for this repo,
3. verify `dotnet` is available on PATH,
4. then run the full build/test commands.

Do not report .NET tests as “not run due to missing dotnet” unless installation is impossible in the current environment, and if so, explain exactly why.
