# Contributing

## Development approach

- Work milestone-first: implement only the scoped behavior for the current milestone.
- Keep pull requests focused; avoid speculative feature branches folded into one PR.
- Prefer small, mergeable increments over broad framework design.

## Testing expectations

- Behavior changes require tests.
- Run .NET build/test checks locally before opening a PR.
- Prefer the automation-friendly repo entrypoint:

  ```bash
  export PATH="$HOME/.dotnet:$PATH"
  ./scripts/test-all.sh
  ```

- The default script currently runs the full Firmament and Server suites plus `Aetheris.Kernel.Core.Tests` with `Category!=SlowCorpus` in a deterministic order. Use direct `dotnet test ...csproj` commands when you need broader or more targeted coverage.

- If you need a narrower repro, run the individual test projects directly:

  ```bash
  export PATH="$HOME/.dotnet:$PATH"
  dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --logger "console;verbosity=minimal"
  dotnet test Aetheris.Server.Tests/Aetheris.Server.Tests.csproj --logger "console;verbosity=minimal"
  dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --logger "console;verbosity=minimal"
  ```

- Use `Aetheris.sln` for solution-oriented automation and editor compatibility with .NET 8; do not rely on `Aetheris.slnx` as the routine automation entrypoint.

## Kernel discipline

- Do not introduce ad hoc epsilon constants; use `ToleranceContext` + `ToleranceMath` and follow `docs/numerics-policy.md`.
- Favor diagnostic/result-oriented kernel operations over exceptions for expected operation outcomes (future-facing rule).
