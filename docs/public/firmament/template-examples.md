# Engineering product families with Templates

These examples map the useful Preview 3 envelope. Every example uses an existing compiler or domain path; none adds geometry or a general programming construct.

## Fastener-aware mounting interface

[`generic-mounting-plate.firmament`](../../../fixtures/Templates/Canonical/generic-mounting-plate.firmament) is a complete product Template parameterized by width, height, thickness, and shaft-hole diameter. It enforces positive dimensions before materialization and produces a deterministic mounting plate through the ordinary semantic Hole path. [`record-array-pattern-holes.firmament`](../../../fixtures/Canonical/valid/record-array-pattern-holes.firmament) extends the idea to a finite `MountSpec` set. Counterbore and countersink families use the same existing `Hole<...>` semantics when that variant is fixed by the family.

## Process-aware machined part

[`cnc-dfm-policy.firmament`](../../../fixtures/Templates/Canonical/cnc-dfm-policy.firmament) is the complete modern port: typed policy Record, immutable shop defaults, `with` specialization, named positive-limit `Require`, a `CncManufacturingPolicy` Concept Struct, and real minimum-tool-radius enforcement against a semantic Hole. [`boss-pocket-mounting-block.firmament`](../../../fixtures/Canonical/valid/boss-pocket-mounting-block.firmament) demonstrates the existing Boss/Pocket vocabulary; Pocket's explicit `MinimumFloorThickness` remains the local final authority.

The matching [`fdm-dfm-policy.firmament`](../../../fixtures/Templates/Canonical/fdm-dfm-policy.firmament) and [`sheet-metal-dfm-policy.firmament`](../../../fixtures/Templates/Canonical/sheet-metal-dfm-policy.firmament) ports preserve the historical nozzle/wall/overhang/layer and thickness/bend/K-factor data as typed family contracts. Persisted lowercase process templates still parse through the compatibility adapter, but new source should use these canonical data-first forms.

## Derived enclosure family

The embedded [`SheetMetalProductFamilies.firmament`](../../../Aetheris.SheetMetal/Firmament/SheetMetalProductFamilies.firmament) is the flagship product-family implementation. `ElectronicsEnclosure<Spec: EnclosureSpec>` changes width, depth, height, thickness, bend radius, K-factor, lip height, and relief policy while retaining one body definition. Desktop and rugged configurations can be derived from one immutable Record with `with` before specialization.

## Table-driven plate family

[`table-template-concept-path-compose.firmament`](../../../fixtures/Canonical/valid/table-template-concept-path-compose.firmament) selects a `PlateStandard` from a keyed finite Table, nests it in `PlateSpec`, derives a thicker configuration with `with`, and specializes one profile/Compose family. Its generic parameter is a Record; positive dimensions are the constraint; the concrete plate bounds and thickness change.

## Pattern-specialized features

[`docs-four-hole-pattern.firmament`](../../../fixtures/Canonical/valid/docs-four-hole-pattern.firmament) combines `Static` Record data, `Template<spec: MountSpec>`, and `Pattern ... Over`. The same feature Template expands any bounded mounting list into deterministic semantic holes without recursion or a loop language.

## Semantic type specialization

The compiler's canonical Template tests exercise `Template<type TBody satisfies PrismaticBody, Width: Length, ...>`. The type-like parameter is visibly different from values and must satisfy a language Concept. A finite enum `Match` selects an already-supported chamfer distance; the selected branch is erased before Feature AIR.

## Forge automation

`Standard.SheetMetal.ElectronicsEnclosure` exposes the enclosure family through Forge Host Protocol v1. Python, Go, Rust, or TypeScript callers send the stable ID and a JSON `Spec` object; the author-facing generic signature is descriptive metadata, not protocol identity. The same invocation can deterministically emit formed STEP, flat STEP, and SVG.

## Where to stop

Move the problem to C# when it needs database access, catalog search, joins, algorithms, external I/O, unbounded iteration, optimization, or large strategy search. Templates should make engineering families legible, not make the compiler clever.
