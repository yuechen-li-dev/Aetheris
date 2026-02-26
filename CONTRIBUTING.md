# Contributing

## Development approach

- Work milestone-first: implement only the scoped behavior for the current milestone.
- Keep pull requests focused; avoid speculative feature branches folded into one PR.
- Prefer small, mergeable increments over broad framework design.

## Testing expectations

- Behavior changes require tests.
- Run .NET build/test checks locally before opening a PR.

## Kernel discipline

- Do not introduce ad hoc epsilon constants; follow `docs/numerics-policy.md`.
- Favor diagnostic/result-oriented kernel operations over exceptions for expected operation outcomes (future-facing rule).
