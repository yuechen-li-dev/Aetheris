# The editable V8

An Aetheris-authored, interface-mated four-stroke mechanism. The browser displays exported definition meshes and animates their stable occurrences using the exported mechanical specification.

From a checkout with .NET 10, Node/npm, Python 3 and PowerShell:

```powershell
./demos/Aetheris.EditableV8/Run.ps1
```

Open `http://127.0.0.1:8765/`. The command builds the staged assemblies, exports AP242 and meshes, validates the mechanism, installs the existing lockfile-pinned viewer dependency if needed, checks the actual JavaScript evaluator against C#, and serves the result in the foreground. Ctrl+C stops the server. Add `-NoServe` for generation and validation only.

The current authoring, export, variant and inspection contract is documented in [Editable mechanisms](../../docs/public/firmament/editable-mechanisms.md). See that guide before creating a variant. Outputs default to ignored `artifacts/local/demos/editable-v8/`; no generated meshes belong in source control.

Use the baseline/revised buttons, orbit/zoom, camera presets, cylinder selector, bank/cylinder isolation, glass/cutaway/solid views, and crank-angle scrubber. The revised engine adds 6 mm of stroke. Cycle color is illustrative; there is no pressure, torque, combustion, contact-dynamics or FEA model.
