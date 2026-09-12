# LANG-SET-X1 — finite named sets and Pattern mapping

## Executive verdict

Accepted. Firmament can express a finite named engineering dataset as `Set<T>`, refer to its entries by semantic name, and map every entry deterministically into existing semantic construction without Enum, Match, a general loop, or a second Pattern executor.

## Before and after

The mounting-point source previously needed an anonymous Record wrapper and numeric identity:

```firmament
Record MountSpec { Center: Point2 }
Static Mounts: MountSpec[] = [
    MountSpec { Center: Point2(-16mm, -9mm) }
    MountSpec { Center: Point2(16mm, -9mm) }
]
Pattern MountPattern Over Mounts { M8Counterbore<Current> }
```

The named dataset now states its intent directly:

```firmament
Static MountPoints: Set<Point2> {
    LowerLeft => Point2(-16mm, -9mm)
    LowerRight => Point2(16mm, -9mm)
    UpperLeft => Point2(-16mm, 9mm)
    UpperRight => Point2(16mm, 9mm)
}
Pattern Mounts Over MountPoints {
    point => M8Counterbore(Center: point)
}
```

Likewise, four standalone `Point2 South/East/North/West` declarations can be one `Set<Point2>` when they are primarily one boundary dataset. `BoundaryPoints.South` and its peers bind as `Point2` values and can feed existing `Line2` definitions. Independently meaningful points should remain declarations; no mass migration is implied.

The particular South/East/North/West coordinates form a rhombus with unequal 90 mm and 64 mm diagonals, not a `Rect2`; changing it to today's axis-aligned `Rect2 { Center; Size }` would change the support boundary. The point-plus-four-lines friction is now represented by the bounded `Polygon2<Rhombus> MountBoundaryShape { Center; Diagonals }` scaffold and consumed as `MountBoundaryShape |> TraceLoop`. It lowers to the existing Point2/Line2/Profile route. `Rhombus` is the only admitted closed variant; this is not general generics, an arbitrary polygon API, or a second geometry engine.

## Audit and reused authority

Before X1, `Static T[]` meant a finite immutable compile-time, source-ordered sequence of typed Record values. Canonical Pattern already enumerated those arrays, substituted `Current` through the existing finite Template expansion, and created numeric identities such as `MountPattern[0]`. Arrays preserved order and values but could not preserve author-facing element names. That forced Point2 values into one-field Records or repeated standalone point declarations.

X1 reuses canonical static declaration erasure, Record field checking, Template instantiation, Feature expansion, concrete Feature AIR/material lowering, source-order handling, and the existing 1,024-instance Pattern bound. Set adds only named value evidence, named lookup, and association-aware Pattern expansion. No second executor or runtime collection exists.

## Type and identity model

| Construct | Meaning | Identity |
| --- | --- | --- |
| `T[]` | Anonymous homogeneous ordered values | Numeric position is compatibility bookkeeping. |
| `Set<T>` | All named homogeneous values in one finite dataset | Entry name plus source order and provenance. |
| `Record` | One structured value | Record type and named fields. |
| `Enum` | One value selected from closed alternatives | Selected variant. |

Every Set entry retains `Name`, typed `Value`, zero-based `SourceOrder`, and source-span `Provenance`. Pattern results use dotted semantic identities such as `Mounts.UpperRight`; material-safe declarations use the corresponding `Mounts_UpperRight` spelling. Pattern inspection retains source Set, source entry, source value, Pattern, and expansion ordinal.

Names must be unique within a Set. Equal values under different names are legal. Empty `Set<T>` is valid and maps to zero instances. X1 admits file-level Static sets of `Point2`, `Point3`, `Vector2`, scalar built-ins, and declared Records in the Mechanical schema. Nested Sets, executable feature values, arbitrary collection algebra, and feature-local Sets are deferred/rejected.

## Control-flow boundary

Set plus Pattern does not add arbitrary iteration. Expansion cardinality is fully known from the authored Set and every entry is mapped in source order. There is no filter, conditional member enablement, mutation, indexing API, sorting, `Map`, `Reduce`, `Fold`, `for`, or `foreach`. Authors who need fewer constructed members author the intended Set.

Inside Set, `Name => Value` means named association. Inside Pattern, `value => construction` means bounded mapping. Neither is Match or branch syntax. Existing arrays, Template, Enum, Match, Feature, Span, and `|>` semantics are unchanged.

## Flagship evidence

The canonical [`mounting-points.firmament`](../../fixtures/Canonical/Set/mounting-points.firmament) builds four counterbores through the ordinary Feature and profile-composition route. Each is checked independently against `Span<Plane> MountingArea`. Moving `UpperRight` outside produces `firmament-feature-footprint-outside-span:Mounts.UpperRight:MountingArea...`, retaining the Set entry identity rather than only an index.

The Set and prior Record-array mounting sources export byte-identical STEP for equivalent geometry, even though the Set flagship now authors its support boundary as `Polygon2<Rhombus>` and the comparison fixture retains the explicit four-point/four-line boundary. Repeated Set export is deterministic. Structured CLI inspection reports Set element type, count, ordered entries/provenance, Pattern associations, and the typed Polygon variant with generated guide identities.

## Compatibility and later burn-in

The legacy `Static T[] = [...]` and `Pattern ... { Template<Current> }` forms remain valid. Later LANG-BURN replay should consider the `finish-pattern`, `pattern-finish`, `hexbolt`, and profile-compose mounting-plate cases: they contain anonymous Record arrays whose entries may benefit from names. They were not migrated in X1 because identity intent must be assessed case by case.

## Validation

The implementation is covered by focused parser/binder/lowering tests, typed invalid fixtures, named access, empty cardinality, source-order determinism, Record payload and Template coexistence, Feature mapping, Span containment identity, array/Enum/Match compatibility, and byte-identical STEP parity.

The completed release gates are:

- Release solution build: succeeded with zero warnings and zero errors.
- Full Firmament suite: 1,378 passed.
- Full CLI suite: 418 passed.
- Canonical qualification: 160 fixtures passed, including all four Set fixtures and the `Polygon2<Rhombus>` flagship.
- Packaged CLI: locally packed, installed as a .NET tool, and used to inspect the flagship successfully.
- Public documentation links and repository layout guard: passed.
- `git diff --check`: passed; Git reported only the repository's normal LF-to-CRLF working-copy notices.
