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

## FL3 milestone — placement/reference ergonomics mini-lab

FL3 adds `PlacementLab/` as a candidate-syntax experiment to compare three placement authoring models before any implementation work:

- **anchor-based** placement (`on`, `axis`, `normal`, `offset`, `concentric_with` style intent),
- **frame-based** placement (explicit local frame/origin/axes),
- **tiny semantic sugar** for narrow, common mechanical placement tasks.

These FL3 files are **not production Firmament syntax** and are intentionally isolated from production parser/compiler fixtures.

FL3 keeps the anti-pretzel rule active: if a candidate style becomes awkward, verbose, or unnatural, record that friction directly instead of inventing helper scaffolding.

Goal: establish the smallest, clearest placement model to trial first, using friction evidence rather than overengineering.

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
