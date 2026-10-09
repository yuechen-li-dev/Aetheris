# Geometry, field and display-compiler boundaries

`Aetheris.Kernel.Core` owns native geometry and BRep operations and has no project
or package dependency. `Aetheris.Fields` owns the analytic field expression tree,
deterministic evaluator tape, capability analysis and Visual TypeScript source
lowering. It depends only on Core. Both are AGPL-3.0-only libraries.

`Aetheris.Continuum` owns broader analysis, discretization and solver coordination.
It consumes Fields and retains the existing public field namespace and assembly
type forwarding. `Aetheris.Kernel.Firmament` owns authoring and display-artifact
coordination; its shader compiler is the pinned Copeland WGSL backend.

The current backend pin is `0.1.0-preview.1-a3.16c0ce996358af80`. Its seven NuGet
packages are Copeland.Markdown, Copeland.Profile, Copeland.SpanAllocation,
Copeland.TS, Copeland.TS.Mir, Copeland.TS.Workspace and Copeland.TS.Backend.Wgsl.
They are package dependencies for this downstream display path, not embedded
Copeland source projects or a dependency of native field evaluation. Building
the complete application restores them; packaging Fields/Core does not.

Consumers that need fields should reference `Aetheris.Fields`, not Continuum or
Firmament. See [fields](fields.md) for the package build, numerical admission and
source provenance contracts. GPU compilation remains the consuming renderer's
responsibility; Fields emits VTS source without depending on a shader compiler.

## Licence composition

Aetheris components retain AGPL-3.0-only. Copeland packages retain their own GPL
grants; no extraction here changes those licences. [GPLv3 section 13](https://www.gnu.org/licenses/gpl-3.0.html#section13)
and [AGPLv3 section 13](https://www.gnu.org/licenses/agpl-3.0.html#section13) allow
their combination. Each part retains its licence; corresponding-source and
notice requirements apply to conveyed combined builds, and AGPL's network-source
clause applies to the combination where its conditions are met.

Analytic shader implementation emitted from the field evaluator is conservatively
treated as AGPL-derived. Its provenance is retained with the generated source.
Artist assets and authored geometry descriptions retain their own terms; this
does not assert that all CAD exports acquire AGPL. A package/shader bake is not
a mechanism for silently removing the evaluator's licence boundary.

Aurelian's optional package adapter lives in Copeland at
`src/Integrations/Aurelian.Lighting.Aetheris`. Its own adapter/tracing source is
GPL, and the Aetheris dependency/emitted evaluator is AGPL. Aurelian's core does
not acquire a direct CAD project dependency. Existing humanoid and full bake
development bridges have separate, explicit source-checkout dependencies.
