# Generated-artifact policy

Generated development output is local by default. Tools, probes, benchmarks, demos, and qualification scripts must write to `artifacts/local/` unless the caller supplies another explicit output path. That tree is ignored by Git and may contain arbitrarily large or noisy run data.

Do not commit per-run logs, traces, diagnostic dumps, meshes, package outputs, or expanded reports merely to preserve a run. A generated file may be promoted into source control only when it is a durable regression golden, a compact release record, or bounded development evidence that cannot be represented more clearly as a summary.

A promoted artifact must:

- live under the owning `fixtures/`, `testdata/**/golden/`, `docs/development/**/artifacts/`, or `docs/release/` area;
- be deterministic or document why byte stability is impossible;
- have provenance and a reproduction command in an adjacent README or report;
- contain no secrets, machine-specific paths, or unbounded event-by-event diagnostics;
- be reviewed for size and diff usefulness before it is staged.

The repository layout guard rejects generated output under retired/local roots and rejects new tracked diagnostic data over 20,000 lines. The narrowly scoped historical exceptions are listed in `scripts/tracked-large-artifact-allowlist.txt`; adding an entry is a deliberate policy change and requires explaining why a compact summary or external artifact is insufficient.

When a detailed result supports a report, commit the report and a compact summary, then keep the raw output under `artifacts/local/`. Never change a generator's default to point at a tracked documentation directory for convenience.
