# PMI showcase

Builds a Firmament part with manufacturing PMI into AP242 STEP and an annotated GLB
that any glTF viewer opens. Every callout in the GLB is read back from the exported
STEP, not from the source.

```powershell
dotnet run --project demos/Aetheris.PmiShowcase -- fixtures/Canonical/PMI/showcase-bearing-mount.firmament
```

Output goes to `artifacts/local/demos/pmi-showcase/`: `<part>.step`, `<part>.glb` and
`<part>.pmi.json` (the semantic records read back from the STEP). The language is
described in [Semantic PMI and AP242](../../docs/public/firmament/pmi.md).
