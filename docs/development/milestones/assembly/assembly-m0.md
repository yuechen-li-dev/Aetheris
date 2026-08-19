# Assembly M0 authoring and inspection

Product containment uses XML-like syntax because visual nesting materially improves assembly readability. Graph relations remain ordinary sibling declarations outside that nesting:

```firmament
Assembly BearingModule {
    <Assembly BearingModule>
        <Assembly Rotor>
            <Part Shaft = ShaftPart>
                Semantic Journal {
                    Axis Axis = [0,0,0] -> [0,0,1];
                    Dimension Diameter = 19.98mm tol +0.01mm -0.01mm;
                }
            </Part>
        </Assembly>
    </Assembly>

    Anchor: BearingModule.Rotor.Shaft.Journal;
    Mate ...
}
```

The XML-like product tree remains deliberately bounded to paired `Assembly`, `Part`, and `Panel` tags. Later Assembly milestones added ordinary `Template <...> Assembly ...` definitions and angle-bracket occurrences without changing that visual profile. Table/Record provenance is retained as an ordered string/source provenance trail in dimensional relations.

Inspect with:

```text
aetheris asm inspect fixtures/Canonical/Assembly/bearing-module.firmament --json
```

JSON exposes tree/BOM data, Interface definitions and Roles, Mate participants and semantic IDs, constraints, transforms/status, fit bands, dimensional graph, complete stackup chains, diagnostics, and phase timings. Text mode gives a compact product tree, Mate table, and stackup summary. This is the Cadmata seam for M0; transformed geometry display is deferred.

Current Assembly also admits generic fully registered semantic frames:

```firmament
Semantic Mount {
    DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1];
}
Interface RegisteredMount {
    Role Moving requires DatumFrameCapable;
    Role Fixed requires DatumFrameCapable;
    Lower FrameCoincident Moving Fixed SameDirection;
}
```

`FrameCoincident` resolves all six rigid-body degrees of freedom. `SameDirection`
and `OpposedDirection` are explicit. Text inspection prints stable datum paths and
the resolved DOF/status. Axis/plane/point constraints remain available when free
motion is intentional, with typed underconstraint diagnostics otherwise.

Historical JSON-shaped `.firmasm` remains accepted as legacy migration input. Current `.firmasm` is the single-root Firmament V2 Assembly profile, and current AssemblyIR exports native AP242 product structure. Migration deliberately preserves explicit transforms without inventing Interfaces or Mates.

AP242 occurrence export follows the OCCT/XDE convention used by the checked-in
`testdata/step242/OCCT/as1.step` reference: each `NEXT_ASSEMBLY_USAGE_OCCURRENCE`
is paired with a `CONTEXT_DEPENDENT_SHAPE_REPRESENTATION`; its complex
relationship names the child representation first and parent representation
second, and combines `REPRESENTATION_RELATIONSHIP`,
`REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION`, and
`SHAPE_REPRESENTATION_RELATIONSHIP`. The item-defined transform maps the child
placement into the parent frame. Keeping all three relationship facets is
required for OCCT-based consumers to preserve both hierarchy and placement.
