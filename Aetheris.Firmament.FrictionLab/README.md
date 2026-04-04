# Aetheris.Firmament.FrictionLab

Aetheris.Firmament.FrictionLab is an **authoring friction lab** for Firmament, not a production feature project.

## Purpose

- Capture real `.firmament` part-authoring attempts in `Cases/`.
- Run each case through the **real** Aetheris Firmament compile/export pipeline.
- Emit STEP artifacts and per-run summary output for quick friction review.
- Record authoring friction explicitly in `review.toon` instead of forcing workarounds.

## Rules

- Do not author production kernel or language expansion code here.
- If a case cannot be expressed cleanly in current Firmament, **stop and record the friction**.
- Do not “pretzel” cases into unnatural constructs just to claim success.

## Run

```bash
dotnet run --project Aetheris.Firmament.FrictionLab/Aetheris.Firmament.FrictionLab.csproj
```

Outputs:
- `Reports/summary.json`
- `Reports/Artifacts/*.step` (for successful exports)
