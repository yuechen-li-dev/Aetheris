# THREAD-EXTERNAL-X1 — one Firmament Thread on an existing HexBolt

**Verdict: Accepted for the bounded HexBolt case.** The existing smooth `StandardPart Bolt { Family: HexBolt }` gains one `Thread` declaration and produces one modeled, closed, consistently oriented BRep and production STEP. No generic Boolean, arbitrary sweep, mesh union, or cosmetic thread route was added.

## Authored delta

`fixtures/Thread/hexbolt-smooth.firmament` and `fixtures/Thread/hexbolt-threaded.firmament` use the same 50 mm, 8 mm nominal HexBolt specification. The threaded file adds only:

```firmament
Thread MainThread {
    Surface: face(Bolt.Shank)
    MajorDiameter: 8mm
    Pitch: 1.25mm
    Length: 47.5mm
    StartOffset: 0.75mm
}
```

The existing `ThreadDesignation` and `ThreadLength` standard-part fields remain metadata. The new `Thread` feature owns the modeled thread. Static schema and LX completion expose its support, major diameter, pitch, length, optional start offset, right-hand policy, derived root diameter, turn count, and flank/crest roles. `Thread` carries stable source provenance; representative rib faces and edges map to its source span. Retained head and tip face identities are remapped into the threaded standard-part report.

## Bounded direct stitch

The HexBolt head, under-head blend, and tip come from the existing exact coaxial construction. `HexBoltThreadStitch` replaces only its two cylindrical shank skin faces. It copies the qualified `BrepHelicalRib` root skin, flanks, crest, and start/end caps into the same topology model, omitting the rib's temporary stock end caps. Two planar annular shoulders connect the original HexBolt half-circle boundary rings to the rib's root-stock full-circle rings. Every boundary edge has two opposed uses. The result is one shell, with no coincident cylindrical faces or overlapping volumes.

The rib is additive over the new root stock. The admitted 60-degree truncated profile uses root width `5P/6`, crest width `P/8`, radial depth `17√3 P/48`, and root diameter `D − 17√3 P/24`. For `D=8 mm` and `P=1.25 mm`, the root diameter is `6.466413347 mm`. Lead equals pitch; the 47.5 mm span yields 38 complete right-hand turns. The bolt shank axis is +X. The helical centerline runs from x=0.95 to 48.45 mm; its last flank reaches x≈48.9708 mm, leaving about 0.0917 mm of exposed root stock before the tip chamfer begins at x=49.0625 mm. The annular transition and the existing 0.9375 mm tip chamfer remain visible. They are a deterministic simple end treatment, not a standards-grade runout.

The same `Thread` syntax works on a standalone `Cylinder Shank` through `face(Shank.OuterWall)`. The compiler validates cylindrical support, stock/major-diameter equality, positive pitch and length, finite stock margins, profile depth, complete-turn admission, and handedness. Failure emits an explicit diagnostic and no smooth substitute.

## Supplied McMaster reference

The supplied 91280A546 smooth/threaded STEP pair is dimensional and visual evidence, not topology authority. The threaded source STEP contains weighted rational B-spline curves that the current Aetheris importer cannot qualify. The comparison remains bounded:

| Feature | Supplied reference evidence | Aetheris modeled bolt | Difference / qualification |
| --- | ---: | ---: | --- |
| Major diameter | 8 mm analytic radius-4 records | 8 mm crest envelope | Nominal match |
| Root/minor diameter | 6.37620 mm at one sampled end vertex | 6.466413347 mm derived root | Reference sample is not a certified minimum; exact deviation unqualified |
| Pitch | 1.25 mm inferred from repeated 0.625 mm phase intervals | 1.25 mm | Nominal match; reference pitch remains inferred |
| Thread span | Exact runout bounds unverified | 47.5 mm / 38 turns | No valid axial deviation claim |
| Shank length | 50 mm | 50 mm | Nominal match |
| Head across flats / height | Smooth-reference envelope consistent with 13 / 5.3 mm | 13 / 5.3 mm authored | No head marking |

## BRep, STEP, display, and performance

The threaded bolt has **176 faces, 355 edges, 184 vertices, and 37 deterministic seam splits**. `BrepBindingValidator`, `BrepExportPreflight`, and mass properties accept one enclosed, consistently oriented body. Its STEP exports and reimports through Aetheris as one enclosed-manifold body with the same topology counts and overall bounds x=−5.3 to 50 mm, y=±6.5 mm, z=±7.50555 mm. The STEP uses qualified B-spline flank/crest surfaces plus analytic cylinder, cone, torus, and plane surfaces. The smooth HexBolt's STEP reimport has two source-sense orientation disagreements; the threaded bolt retains that same baseline count, and the imported shell is resolved consistently. External CAD-reader parity was not separately checked.

The BRep display tessellator covers every face and provides a shaded isometric render at `artifacts/local/thread-x1/threaded-hexbolt.png`. The production STEP is `artifacts/local/thread-x1/hexbolt-threaded.step`. The opt-in artifact test records compile/build, tessellation, topology, hash, and file-size measurements in `artifacts/local/thread-x1/threaded-hexbolt.json`. On the measured Windows Release host, compile/build took 3.75 s, including 3.34 s of helical-rib construction and shell stitching; STEP export took 0.28 s. Tessellation took 10.65 s and produced 606,870 display triangles. The deterministic STEP hash is `1A3A80A35F601A1C140777CBBAB1FF57CC788D491AFA517F9D229BA1248F9682`; the production file is about 8.8 MB. CLI STEP reimport took about 11 s including process startup. These are representative measurements, not performance guarantees.

Focused tests cover the plain-cylinder thread, invalid parameters, edits, schema/LX, source mapping, STEP roundtrip, determinism, the HexBolt stitch, and retention of head/tip semantics. The smooth HexBolt fixture still builds. The Release solution builds with zero errors; the fast Core lane passes 1,000 tests. In the full Release lane, Core passes 1,133/1,133 and Firmament passes 1,664/1,664. The full lane remains red on the established unrelated baseline: two CLI tests (HexBolt OBJ polygon count 905 expected versus 908 actual, and SheetMetal validation) and five CTC03 SheetMetal tests. Details are in `artifacts/local/thread-x1/full-test.log`.

## Limits

X1 admits only external, constant-pitch, constant-diameter, single-start, right-hand, complete-turn threads on the supported cylinder/HexBolt shapes. The bolt's planar shoulder transitions and clipped rib ends are valid but simplified. No internal, tapered, left-hand, multi-start, arbitrary profile, standards database, runout, thread relief, or engraved `8.8` head marking is implemented. The supplied threaded STEP cannot yet be used for exact overlay because weighted rational source curves are not imported.
