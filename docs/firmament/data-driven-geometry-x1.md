# Data-driven geometry X1

> A reconstruction model may be a typed database compiled into geometry.

> Compress repetition in source, not evidence in the compiler.

X1 adds a deliberately bounded front end for scaffold-backed prismatic Profiles:

```firmament
Record LobeSpec { Key: Symbol; Path: Point2[]; InnerCenter: Point2; OuterCenter: Point2; Radius: Length; InnerStart: Point2; InnerEnd: Point2; OuterStart: Point2; OuterEnd: Point2; Sweep: ArcSweep; Role: ConceptRole }
Let Lobes: LobeSpec[] = [ LobeSpec { Key: LeftTop; /* static typed values */ } ]
Template RoundedLobe<Spec: LobeSpec> { Concept Struct Layout { } Profile Shape { } }
Expand Lobes With RoundedLobe
Add Lobes { Profiles: [LeftTopEar, RightTopEar]; From: -100mm; To: 0mm; Role: StrongInferenceR50Lobe }
```

`LobeSpec[]` is finite and literal-only (maximum 1024 rows). Keys are required, symbolic, and unique. Required fields, units, path shape, sweep, and template argument type are checked before Profile or Feature AIR construction. There are no runtime loops, dynamic collections, macro execution, or array-valued BRep operations.

Expansion emits ordinary `Point2`, `Circle2`, `Line2`, and Profile segments. A grouped `Add` is then erased to individual same-interval/same-role Adds. Inspection retains the record key, template name, generated profile/member paths, and grouped-operation paths. In particular, template evidence uses paths such as `template:RoundedLobe[LeftTop].Layout.InnerArcGuide`, profiles use `profile:Lobes[LeftTop].Shape`, and group lowering uses `compose:Ctc01.Lobes[LeftTop]`.

This is intentionally not a general table language. Record declarations, static arrays, one named template argument, and finite expansion are the whole capability. General polygon generators, CSV/dataframe import, symmetry, recursive templates, and runtime loops remain deferred.

Default inspection consumes the expanded compiler result only; use `inspect-compose --materialize`, `build`, or `verify` for BRep and artifact evidence. See [inspect performance X1](../tooling/inspect-performance-x1.md).
