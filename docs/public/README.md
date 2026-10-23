# Aetheris documentation

Aetheris is an exact, code-first CAD system. You describe a part, an assembly or
an analysis in **Firmament**, a small typed language, and the Aetheris compiler
turns it into exact STEP AP242 B-rep geometry, with its manufacturing intent and
PMI intact. When it cannot do exactly what you wrote, it stops and tells you why.

This folder is the user documentation and the authority on current behavior.
Everything else under `docs/` is development history, release records or legal
drafts; the [project history](../development/history/README.md) explains how the
system got here.

## Start here

1. [Getting started](getting-started.md): install the CLI and build a first part.
   With the Windows release ZIP, start from the [bundle walkthrough](release-bundle.md).
2. [Learn Firmament](learn/README.md): six short lessons, from a plate with a
   hole to a two-part assembly. Every example builds in CI.
3. [Language reference](reference/language/README.md): every construct, its
   fields and its limits.

**If you are an AI agent writing Firmament**, read the lessons and the language
reference first, then the [modeling strategy skill](skills/firmament-modeling-strategy/SKILL.md)
for how to plan a part. Trust `aetheris validate --json` over anything you
remember: the compiler is the source of truth, and it reports every request it
cannot honour.

## Guides

### Parts

- [Geometry and features](firmament/geometry.md): what each feature supports and
  what combines
- [Closed 2D boundaries](firmament/boundaries.md), [cubic profile guides](firmament/cubic-bezier-profiles.md),
  [equation curves](firmament/equation-curves.md), [profile boundary edits](firmament/profile-boundary-edits.md)
- [Fillets and chamfers](firmament/fillet-profiles.md), [G2 plateaus](firmament/plateaus.md),
  [material offsets](firmament/material-offsets.md)
- [Revolve](firmament/revolve.md), [sweep](firmament/sweep.md), [two-section loft](firmament/loft.md),
  [section chains](firmament/section-chains.md)
- [Threads](firmament/threads.md), [perforation](firmament/perforation.md), [gears](firmament/gears.md)
- [Spans](firmament/spans.md), [Concept points and functions](firmament/concept-points-and-functions.md),
  [derived Concept frames](firmament/concept-derivation.md)
- [Body states and sculpting](firmament/sculpting.md)

### Reuse and libraries

- [Templates](firmament/templates.md) and [product-family examples](firmament/template-examples.md)
- [Patterns](firmament/patterns.md)
- [Standard Library](standard-library/README.md): product families, invocation, authoring
- [Standard Paperclip](firmament/paperclip.md)
- [Materials](firmament/materials.md)

### Manufacturing domains

- [Sheet metal](firmament/sheet-metal.md)
- [Molded plastic shells](firmament/plastic-shell.md)
- [Formed wire](firmament/wire-form.md), [point wire routes](firmament/wire-point-routes.md),
  [mathematical knots](firmament/mathematical-knots.md)
- [Structural members and weldments](firmament/structural.md)
- [Piping and local autorouting](firmament/piping.md)

### Assemblies, scenes and motion

- [Assemblies](firmament/assemblies.md), [authoring foundation](firmament/assembly-authoring-foundation.md),
  [components and mounting sites](firmament/assembly-components-and-sites.md),
  [datum seating](firmament/datum-seating.md)
- [Editable mechanisms and prescribed motion](firmament/editable-mechanisms.md)
- [Inventory Records and modules](firmament/inventory.md),
  [project snapshots](firmament/project-snapshots.md),
  [incremental compilation](firmament/incremental-compilation.md)
- [Scenes](firmament/scenes.md), [OpenUSD export](firmament/openusd.md), [GLB export](firmament/glb.md)

### Requirements, analysis and drawings

- [PMI](firmament/pmi.md)
- [Linear-elastic FEA](firmament/fea.md)
- [Drawings: PDF, SVG and PowerPoint](drawings.md)
- [Reading engineering drawings with Drawing Notes](drawing-notes.md)

### Existing geometry

- [STEP import](firmament/step-import.md)
- [Imported through-hole revisions](firmament/imported-feature-recognition.md)
- [3DM geometry recovery](reference/3dm-recovery.md)

### Tools and integration

- Helios: [sketching](firmament/sketcher.md), [AI author](firmament/ai-author.md),
  [engineering references](firmament/engineering-assistance.md)
- Cadmata: [overview](cadmata/overview.md), [measurement](cadmata/measurement.md),
  [PMI](cadmata/pmi.md), [portable Windows build](cadmata/portable-windows.md)
- Display: [Three Telos](three-telos.md), [viewport themes](firmament/viewport-themes.md),
  [surface and trim inspection](firmament/surface-inspection.md)
- Web SDK: [getting started](web-sdk/getting-started.md), [build your own CAD front end](web-sdk/build-your-own-cad-frontend.md),
  [editor integration](web-sdk/editor-integration.md), [reference editor](web-sdk/reference-editor.md),
  [API reference](web-sdk/api-reference.md), [capability matrix](web-sdk/capability-matrix.md),
  [troubleshooting](web-sdk/troubleshooting.md)
- [Forge Host Protocol v1](forge/interop.md): invoke Templates from Python, Go, Rust or TypeScript
- [Aetheris.Fields](fields.md) and [library boundaries](project-boundaries.md)
- [Firmament language style](firmament/language-style.md) and the formatter

## Reference

- [Language reference](reference/language/README.md)
- [CLI](reference/cli.md)
- [Diagnostics](reference/diagnostics.md)
- [Targets and selectors](reference/targets.md)
- [Supported features](reference/supported-features.md) and [known issues](reference/known-issues.md)
- [Wireframe SVG previews](reference/wireframe.md)
- Release notes: [Preview 3](reference/release-notes.md), [Preview 4 Cadmata](cadmata/preview4-release-notes.md)

## Showcases

- [The Difference Engine](demos/difference-engine-showcase.md): hierarchical CAD
  and prescribed browser animation
- [Carved electric guitar](demos/guitar-surfacing-x0.md) and its
  [subassemblies](demos/guitar-subassemblies.md)
- [Industrial ATLAS](demos/industrial-atlas-modernization.md)
- [Factory scene](demos/factory-scene.md)
- [Code-native bracket](code-native-bracket.md)
- [Virtual sculpture](virtual-sculpture.md): exact geometry made just for fun

## Experimental

[Humanoid bodies, garments and exterior reconstruction](experimental/README.md)
exist in the repository but are not part of the supported product.
