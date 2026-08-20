# Structural members and weldments

Aetheris X2 authors a weldment as a semantic structure, not as a Boolean union of intersecting solids:

```text
Structure → Member AIR → Joint resolution → Assembly Interfaces → finished members → AP242 Assembly + Cut List
```

The bounded X2 workflow supports explicit 3D nodes, straight paths, constant structural sections, member orientation, catalog material resolution, two-member butt and polygonal miter joints, fillet-weld requirements, and deterministic JSON cut lists. Curved members, tube coping, three-member miters, connection design, weld sizing, and structural FEA are future work.

## 1. Author the structure graph

Nodes are structurally meaningful locations. Paths retain stable identity, endpoints, direction, length, and source provenance.

```firmament
Structure Corner {
    Node A = [0mm, 0mm, 0mm];
    Node B = [600mm, 0mm, 0mm];
    Node C = [600mm, 400mm, 0mm];
    Path Horizontal: A -> B;
    Path Vertical: B -> C;
}
```

X2 paths are straight segments and every path has exactly one Member assignment.

## 2. Define sections and members

Section geometry and material identity are separate. X2 admits `Standard.Structural.SquareTube`, `RectangularTube`, `RoundTube`, `Angle`, `FlatBar`, and `RoundBar`.

Tube sections are genuinely hollow. Square/rectangular tube fields are `Width`, optional `Height`, and `Thickness`; round sections use `Diameter`; angles use `Width`, `Height`, and `Thickness`.

```firmament
Section RHS {
    Kind: Standard.Structural.SquareTube;
    Width: 40mm;
    Thickness: 3mm;
}

Member Horizontal {
    Path: Horizontal;
    Section: RHS;
    Material: Standard.Materials.Steel.ASTM_A36;
    Orientation: [0,0,1];
}
```

`Orientation` is a reference vector projected perpendicular to the member axis. It is mandatory for asymmetric `Angle` sections. `Auto` is deterministic for symmetric sections.

### Columnar graph tables

For frames with many edges, use the existing first-class `Record` + `Static Table` mechanism instead of repeating blocks. Structural table rows normalize to exactly the same Member AIR:

```firmament
Record StructuralMemberRow { Name: String Path: String Section: String Material: String Orientation: String }
Static Table Members: StructuralMemberRow Key: Name {
    Name: ["Top", "Bottom", "Left", "Right"]
    Path: ["TopPath", "BottomPath", "LeftPath", "RightPath"]
    Section: ["RHS", "RHS", "RHS", "RHS"]
    Material: ["Standard.Materials.Steel.ASTM_A36", "Standard.Materials.Steel.ASTM_A36", "Standard.Materials.Steel.ASTM_A36", "Standard.Materials.Steel.ASTM_A36"]
    Orientation: ["UpZ", "UpZ", "UpZ", "UpZ"]
}
```

`StructuralJointRow` columns are `Name`, `Type`, `FirstMember`, `FirstEnd`, `SecondMember`, `SecondEnd`, and `Primary`. End values are `Start` or `End`; `Type` is `Butt` or `Miter`. `Primary` names the through member for a butt joint. The column is still required for a miter row because Static Tables have one fixed schema, but its value is ignored. `StructuralWeldRow` columns are `Name`, `Joint`, `Type`, and `Size`; X2 weld type is `Fillet`.

```firmament
Record StructuralJointRow { Name: String Type: String FirstMember: String FirstEnd: String SecondMember: String SecondEnd: String Primary: String }
Static Table Joints: StructuralJointRow Key: Name {
    Name: ["TopLeft"]
    Type: ["Butt"]
    FirstMember: ["Top"]
    FirstEnd: ["Start"]
    SecondMember: ["Left"]
    SecondEnd: ["End"]
    Primary: ["Top"]
}

Record StructuralWeldRow { Name: String Joint: String Type: String Size: Length }
Static Table Welds: StructuralWeldRow Key: Name {
    Name: ["TopLeftWeld"]
    Joint: ["TopLeft"]
    Type: ["Fillet"]
    Size: [4mm]
}
```

Column cardinality is checked and source order does not control identity, joint ownership, grouping, or output ordering. Explicit `Member`/`Joint`/`Weld` blocks remain useful for small examples and may coexist with table rows when identities do not collide. Prefer identity names that describe location rather than treatment (`TopLeft`, not `TopLeftButt`) so a later joint-type change does not make the identity misleading.

Path dimensions describe member centerlines. Section material extends around those centerlines, so a 1000 mm by 600 mm rectangular path loop made from 40 mm tube has 1040 mm by 640 mm outside bounds. The cut list preserves both raw path length and finished length: a nominal 800 mm leg trimmed beneath a 40 mm top rail has a 780 mm finished cut.

## 3. Resolve joints, then weld requirements

A Joint changes member end geometry and lowers an explicit interface between two member occurrences. A Weld records a fabrication requirement against that interface; it does not create a bead solid.

```firmament
Joint CornerMiter { Members: [Horizontal.End, Vertical.Start]; Type: Miter; }
Weld CornerWeld { Joint: CornerMiter; Type: Fillet; Size: 4mm; }
```

`Miter` supports two non-collinear polygonal members. Joint pathing is selected through `JudgmentEngine`: the separating and reflex angle-bisector candidates publish admissibility, utility score, deterministic tie-breaking, and rejection reasons. The reflex candidate is rejected when both retained member rays would occupy the same half-space. Both independently materialized member end loops must coincide on the selected interface or compilation fails. A `Butt` joint identifies the through member explicitly:

For a closed rectangular picture frame, the two cuts on each rail have opposite handedness. Its top/plan outline is therefore an isosceles trapezoid, not a parallelogram. Export qualification checks that the cap loops lie on both the declared joint-interface planes and their bound B-rep `PlaneSurface` supports, then checks that this trapezoidal profile survives AP242 reimport. A support mismatch fails before export with `structural-member-cap-support-plane-mismatch:<member>:<end>`; the diagnostic names the corrective binding formula because STEP reconstructs vertices from incident support planes and can otherwise turn correct topology points into a parallelogram.

```firmament
Joint TeeButt {
    Members: [Through.Start, Terminating.End];
    Type: Butt;
    Primary: Through;
}
```

The terminating member is trimmed to the primary envelope. Unsupported round-section miters, collinear joints, disconnected references, and repeated geometry-owning end treatments fail explicitly.

## 4. Inspect fabrication output

```powershell
aetheris inspect fixtures/Canonical/Structural/welded-workbench.firmament --json
aetheris build fixtures/Canonical/Structural/welded-workbench.firmament --output artifacts/local/x2/x2-welded-workbench.step --json
```

Build emits an AP242 occurrence assembly and sibling `*.cutlist.json`. Members remain independent definitions and occurrences; the structural report exposes the assembly plus every joint interface. Miter interfaces include their shared plane, selected Judgment strategy and utility, rejected candidates, and a checked `matingSurfacesCoincident` result. Every admitted butt or miter contact also verifies that the complete member vertex sets occupy opposite retained half-spaces; `volumetricOverlapMm3` is reported as zero only when that proof succeeds. The report also contains nodes, paths, sections, materials, members, joints, welds, cut-list groups, mass, bounds, analytic surface families, reimport status, and stage timings. Grouping compares section, material, raw/finished length, and fabrication treatment—not length alone.

Copy [the welded workbench fixture](../../../fixtures/Canonical/Structural/welded-workbench.firmament) for a complete example. Smaller examples live beside it in `fixtures/Canonical/Structural/`.

### Template-generated graph tables

For policy-level authoring, invoke `Standard.Structural.WeldedWorkbench` through Forge Protocol v1. Its typed `WeldedWorkbenchPolicy` exposes width, depth, height, tube size, wall thickness, and material. The Template generates the skeleton plus columnar Member/Joint/Weld tables; downstream AIR and fabrication behavior are identical to explicit tables. Forge `list`, `describe`, and `invoke` require no structural-specific RPC and can return both `StepAp242` and `CutListJson`.
