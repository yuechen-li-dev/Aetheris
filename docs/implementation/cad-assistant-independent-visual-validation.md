# CAD Assistant independent visual validation

CAD Assistant is an independent OCCT-based STEP importer, tessellator, and viewer. Aetheris uses it as a manual visual smoke test after its own build, topology, and STEP reimport checks. This reduces the chance that the same Aetheris assumptions are repeated at every validation layer.

It is deliberately optional: CAD Assistant is not a compiler dependency, does not run in automated .NET tests, and is not a geometric oracle.

## Launcher

The Windows-first launcher defaults to searching `C:\Program Files\CAD Assistant` and uses a deterministic discovery rule: one `CADAssistant.exe` is accepted; otherwise it only accepts a single unambiguous CAD Assistant-named executable. It rejects missing paths and ambiguous candidates.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File tools/Open-In-CadAssistant.ps1 `
  -Step artifacts/template-m4b-standard.step
```

Specify the viewer explicitly when it is installed elsewhere. The precedence is `-CadAssistantPath`, then `AETHERIS_CAD_ASSISTANT_EXE`, then the default installation search.

```powershell
tools/Open-In-CadAssistant.ps1 -Step 'C:\work\part.step' `
  -CadAssistantPath 'D:\CAD Assistant\CADAssistant.exe' `
  -BuildReport 'C:\work\build-report.json' `
  -AnalyzeReport 'C:\work\analyze-report.json'

$env:AETHERIS_CAD_ASSISTANT_EXE = 'D:\CAD Assistant\CADAssistant.exe'
tools/Open-In-CadAssistant.ps1 -Step 'C:\work\part.step'
```

The launcher resolves and prints the STEP artifact path and SHA-256 before launch, as well as any supplied build/analyze report paths. `-NoLaunch` validates and prints the launch specification without opening a GUI; it is useful for tooling tests and review preparation.

## Evidence stack and boundaries

| Layer | Role |
| --- | --- |
| Aetheris build/tests | Authoritative compiler-stage correctness, Concept IR and Feature AIR provenance, BRepPlan parity, topology counts/roles, manifold checks, analytic dimensions, volume, semantic PMI, and Aetheris-emitted STEP evidence. |
| Aetheris STEP reimport/analyze | First-party round-trip verification. |
| CAD Assistant / OCCT | Independent manual importer, tessellator, and visual smoke test. |
| Cadmata | Future dedicated Aetheris visual environment. |

CAD Assistant can provide independent evidence that the STEP opens in OCCT, the expected body and silhouette are visible, holes and chamfers appear where expected, faces are not visibly missing or inverted, shell orientation looks sane, and unexpected seams or tessellation artifacts are noticed. Section/clipping tools can be used manually when interior geometry matters.

It does **not** prove original feature history, semantic provenance, exact PMI completeness, every topology identity, full AP242 semantics, or Aetheris compiler correctness. “CAD Assistant opened the file” is never sufficient proof; retain the Aetheris build and analyze evidence.

## Review packet convention

Keep visual evidence alongside the exact artifact, preferably under `artifacts/visual-validation/<case>/`:

```text
model.step
build-report.json
analyze-report.json
visual-validation.json
README.md
cad-assistant.png
```

`visual-validation.json` may record the STEP filename and hash, report filenames, the discovered viewer executable, `"validationRole": "Independent OCCT visual smoke test"`, and `"status": "PendingManualReview"`. Add `cad-assistant.png` only after a human or Codex has inspected the opened model. The launcher does not copy, transform, or rewrite the STEP file.

## Manual checklist

1. Confirm the expected number of visible bodies.
2. Spin the model and inspect all sides.
3. Use shaded-with-edges or equivalent mode.
4. Inspect chamfer transitions and hole openings.
5. Check for missing, inverted, or visually collapsed faces.
6. Use section/clipping tools when internal geometry matters.
7. Save a screenshot tied to the exact STEP hash.
8. Record pass/fail notes without replacing machine validation.

Cadmata should eventually consume or emit the same lightweight packet metadata and screenshots, while keeping CAD Assistant a separate, independent visual check rather than coupling compiler work to a desktop CAD installation.
