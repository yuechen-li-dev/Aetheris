# STEP import and inlineSTEP

Use `aetheris analyze part.step --json` to inspect STEP topology, analytic surface families, bounds, manifold assessment, and semantic PMI. Use `aetheris sheetmetal recognize` for bounded Sheet Metal recognition rather than treating generic topology analysis as manufacturing intent.

Firmament `inlineSTEP` consumes Aetheris-canonical AP242 on the bounded production path. Imported faces are selected by source entity identity, for example `body.face(#170)`, and recognized regions may supply stable semantic aliases. [`inline-step-cantilever.firmament`](../../../fixtures/Canonical/FEA/inline-step-cantilever.firmament) demonstrates FEA on a checked-in canonical STEP resource.

The importer does not promise arbitrary containment, multi-root assemblies, or every AP242 surface family as a single native body. Multi-root assembly-like STEP belongs to assembly import. Unsupported containment in FEA reports `firmament-analysis-inline-step-containment-unsupported`; missing files and unresolved face identities are fatal diagnostics.
