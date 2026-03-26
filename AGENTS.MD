# AGENTS.md

## Test execution requirement

Do not skip .NET build/test validation just because `dotnet` is missing from the current environment.

If the required .NET SDK/runtime is not installed:
1. detect that explicitly,
2. install the required .NET version for this repo,
3. verify `dotnet` is available on PATH,
4. then run the full build/test commands.

Do not report .NET tests as “not run due to missing dotnet” unless installation is impossible in the current environment, and if so, explain exactly why.
