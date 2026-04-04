# Aetheris.Firmament.FrictionLab

Aetheris.Firmament.FrictionLab is an **authoring friction lab** for Firmament, not a production feature project.

## Purpose

- Capture real `.firmament` part-authoring attempts in `Cases/`.
- Run each case through the **real** Aetheris Firmament compile/export pipeline.
- Emit STEP artifacts and per-run summary output for quick friction review.
- Record authoring friction explicitly in `review.toon` instead of forcing workarounds.

## FL2 milestone

FL2 expands the lab with a focused 10-case suite covering brackets, holes, patterns, pockets, support structures, shaft features, shelling, and intentionally missing advanced geometry (thread helix and loft transition).

The suite is designed for measurement, not pass-rate inflation:

- successes identify what is currently easy,
- partials expose awkward or fragile expressions,
- failures document genuinely missing shape capabilities.

## Anti-pretzel rule (mandatory)

If a case cannot be expressed cleanly:

- stop,
- do not build complex hacks,
- do not simulate missing features with unnatural constructions,
- report friction directly in `review.toon`.

Failures are expected and valuable in this phase.

## Run

```bash
dotnet run --project Aetheris.Firmament.FrictionLab/Aetheris.Firmament.FrictionLab.csproj
```

Outputs:
- `Reports/summary.json`
- `Reports/Artifacts/*.step` (only when export succeeds)
