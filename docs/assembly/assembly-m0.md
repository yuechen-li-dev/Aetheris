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

The XML-like parser is deliberately bounded to `Assembly` and paired `Part` tags. It does not import Copeland syntax machinery. Assembly Template expansion is not in M0, but the normalized source model separates definition identity from occurrence identity and therefore does not block Template-generated trees. Table/Record provenance is retained as an ordered string/source provenance trail in dimensional relations; the dogfood uses `HousingTable`, `BearingTable`, and `SpacerTemplate` origins.

Inspect with:

```text
aetheris asm inspect fixtures/AssemblyM0/bearing-module.firmament --json
```

JSON exposes tree/BOM data, Interface definitions and Roles, Mate participants and semantic IDs, constraints, transforms/status, fit bands, dimensional graph, complete stackup chains, diagnostics, and phase timings. Text mode gives a compact product tree, Mate table, and stackup summary. This is the Cadmata seam for M0; transformed geometry display is deferred.

Legacy `.firmasm` remains accepted only by `asm exec`/`asm export`. It is flat JSON with authored transforms and cannot be losslessly migrated to typed Interfaces because it contains no semantic relationship intent. A bounded automatic importer would fabricate meaning, so M0 documents migration: rewrite the flat `parts`/`instances` list as a nested product tree, expose semantic endpoints on definitions, replace transforms with Interface/Mate declarations, and retain explicit transforms only as an escape hatch. Per-instance STEP packaging remains the current export seam; the exporter does not author AP242 product structure or mapped-item assembly relationships.
