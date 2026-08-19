# Sheet Metal M6: generic enclosure products

## Parser-level language audit

The implementation and tests—not older prose—establish this current map:

| Surface | Current syntax and lowering | M6 status |
|---|---|---|
| Part authoring | Normal Firmament V2 declarations such as `Record`, `Static`, `Template <...>`, `SheetMetal`, `Require`, `Concept`, specialization with `<Name: value>`, and downstream `Extend SheetMetal` | Existing M5 typed Template frontend and Sheet Metal lowering retained |
| Template AST | `TemplateDeclarationIr` with typed value/type parameters; record arguments bind to `BoundTemplateRecordIr`; specialization records deterministic `ConceptIrTemplateInstantiation` provenance | Reused; no Assembly-specific Template engine |
| Concept satisfaction | Part Templates claim a Concept after `:`; members and types are checked before domain lowering | Existing Body/Lid contracts retained; M6 adds the same structural claim form to Assembly Templates |
| Assembly profile | `Assembly Name { <Assembly Name> ... <Part P = Definition<...>></Part> ... </Assembly> ... }` | Canonical product syntax remains XML-shaped |
| Assembly Template | `Template < Spec: RecordType > Assembly Product: ProductConcept { ... }`; occurrence `<Assembly X = Product<Spec: StaticSpec>></Assembly>` | Existing generic Assembly specialization extended with nested Record projection (`Spec.Body`) and structural Concept validation |
| Interface | `Interface Name { Role ... requires ...; Lower ...; Fit ... inside ...; Allow ...; }` | Existing parser/IR/compiler reused |
| Role | Capability requirements such as `AxisCapable`, `PlaneCapable`, `PointCapable`, and `DimensionalCapable` | Checked before geometric placement |
| Mate | `Mate Name: Interface { Role: Product.Part.Semantic; }` | Existing `MateIr`, constraint lowering, placement solver, and validation reused |
| `Require` / `Assert` | `Require Name => static-bool-expression`; `Assert ToleranceStackup ...` | Existing Template/static and dimensional graph paths retained |
| `.firmasm` | Current Firmament V2 Assembly document profile requiring exactly one exported root; JSON-shaped input is legacy migration data | Canonical M6 fixture uses `.firmasm` |

`AssemblySource` lowers XML containment into `AssemblyMemberSource`; Interfaces and Mates remain sibling graph declarations. Binding produces `AssemblyInstanceIr`, `MateIr`, `PlacementConstraintIr`, placement results, dimensional relations, and reusable `AssemblyDefinitionIr`. M1 materializes each distinct part definition, validates exact geometry residuals and positive-volume interference, and M2 exports AP242 definitions/occurrences without fusing bodies.

The actual gap was cross-profile materialization: the generic Assembly executor only knew the ordinary solid-part compiler. M6 adds `AssemblyPartMaterializer`, a narrow domain hook. Sheet Metal resolves projected typed records through its normal Template frontend and returns its authoritative exact formed BRep. Assembly still owns hierarchy, Interfaces, Mates, placement, interference, and AP242.

Two stale statements were corrected in place: `README.md` called all `.firmasm` deprecated, and `docs/development/milestones/assembly/assembly-m0.md` still said Assembly Template expansion and AP242 product export were absent. Both described older milestones.

## Canonical product source

The complete passing source is `fixtures/Compatibility/Firmasm/SheetMetal/network-appliance-product-m6.firmasm`. Its essential shape is:

```firmament
Concept EnclosureProduct { Body: Part Lid: Part Closure: Mate Attachments: Mate }

Template < Spec: EnclosureProductSpec >
Assembly ElectronicsEnclosureProduct: EnclosureProduct {
  <Assembly ElectronicsEnclosureProduct>
    <Part Body = ElectronicsEnclosure<Spec: Spec.Body>> ... </Part>
    <Part Lid = RemovablePanLid<Spec: Spec.Lid>> ... </Part>
  </Assembly>
  Anchor: ElectronicsEnclosureProduct.Body.Closure;
  Mate Closure: LidClosure { Body: ElectronicsEnclosureProduct.Body.Closure; Lid: ElectronicsEnclosureProduct.Lid.Closure; }
  Mate Attachments: AlignedScrewPattern { Body: ElectronicsEnclosureProduct.Body.Attachments; Lid: ElectronicsEnclosureProduct.Lid.Attachments; }
  Expose { Semantic Body = Body.Closure; Semantic Lid = Lid.Closure; }
}

Assembly NetworkAppliance {
  <Assembly NetworkAppliance>
    <Assembly Product = ElectronicsEnclosureProduct<Spec: NetworkProductSpec>></Assembly>
  </Assembly>
  Anchor: NetworkAppliance.Product;
}
```

The XML-shaped tree distinguishes product containment from normal Sheet Metal construction. Graph declarations stay outside that tree. `Spec.Body` and `Spec.Lid` are typed nested Record projections and resolve to the same ordinary M5 part Templates.

## Contracts, interface, placement, and attachment

`ElectronicsEnclosure` continues to satisfy the `Enclosure` part Concept; `RemovablePanLid` satisfies `RemovableLid`; the Assembly Template satisfies `EnclosureProduct`. Removing the `Lid` member produces `assembly-concept-missing-member` before geometry. A Role capability mismatch produces `assembly-mate-capability-mismatch` before placement.

`LidClosure` requires axis, plane, and dimensional capabilities on Body and Lid. It lowers axis coincidence and seating-plane coincidence, admits only rotation about the closure axis, and declares `Fit Body.Width inside Lid.Width`. For the canonical 160 × 110 mm body and 162 × 112 mm lid, the Mate derives the lid world transform `[-1, -1, 36]` mm; no raw occurrence transform is authored.

The authored uniform side clearance is 1 mm. Derived lid dimensions are 162 × 112 mm. Product DFM reports a measured bounded side clearance of 1 mm against the 0.4..1.5 mm policy and a 9 mm required overlap. A 0.2 mm specialization fails `assembly-dfm-lid-clearance` at `Product.Closure`; changing it to 1 mm repairs the product.

`AlignedScrewPattern` is a bounded typed attachment foundation. Body and Lid roles require point capability, the Mate preserves semantic correspondence at `Product.Attachments.HoleA`, and the BOM records four nominal M3 placeholders. It intentionally does not claim a full fastener catalog or thread analysis.

## Product result and manufacturing

The real host call is:

```csharp
var product = EnclosureProductFamilies.MakeEnclosureProduct(
    new EnclosureProductSpec(160, 110, 36, 1.2, 1.2, 1.5, 1, 9),
    "NetworkAppliance");
var artifacts = product.Export("artifacts/m6/product");
```

This call emits typed Firmament specialization input, then invokes the same `ElectronicsEnclosureProduct` Assembly Template and the same `ElectronicsEnclosure` / `RemovablePanLid` part Templates. C# contains no alternate CAD construction.

The result exposes:

- hierarchy `NetworkAppliance → Product → Body, Lid`;
- stable product paths including `Product.Body.Front`, `Product.Body.Rear`, `Product.Body.FrontLip`, `Product.Lid.Top`, `Product.Lid.Front`, `Product.Closure`, and `Product.Attachments`;
- independent exact formed parts, exact line/arc flat contours, flat STEP, and SVG;
- AP242 Assembly STEP with two geometric definitions and distinct Body/Lid occurrences;
- deterministic specialization, geometry, flat-pattern, and product DFM ordering;
- categorized `Part`, `Interface`, and `Assembly` findings;
- a minimal Body ×1, Lid ×1, M3-placeholder ×4 BOM.

The network-appliance source keeps customization in the normal part profile: Body has two indicators, power/Ethernet cutouts, four vents, and a lid-mount hole; Lid has the corresponding mount hole. Its exact flats contain nine Body cut loops and one Lid cut loop. The canonical CLI inspection succeeds with two materialized definitions and two part occurrences. Body geometry has 81 faces / 194 edges / 118 vertices; Lid has 47 / 102 / 58. AP242 export/reimport, all four per-part formed/flat STEP reimports, exact blank validation, and manifold preflight are covered by `SheetMetalM6Tests`.

## DFM evidence and bounded seams

Part findings come from `SheetMetalDfm`. Interface findings cover clearance, overlap, and attachment correspondence. Assembly findings use the existing exact `BrepSolidInterference` evidence to reject positive-volume Body/Lid penetration and report the bounded +Z removal check. The general `IntersectionQuery`, `ContactQuery`, and `ClosestPointQuery` surface is curve/patch oriented today; pretending it performs exact body/body clearance would be dishonest. Extending those query APIs to body/body witnesses is the next geometry-evidence seam.

Grounding is left as an explicit future Interface metadata seam; no electrical simulation or paint-mask inference is claimed.

## LLM authoring friction

- Fix now: nested product Record projection, Assembly structural Concept diagnostics, Sheet Metal definition materialization, and CLI routing were missing and are implemented.
- Future abstraction: authoring the same closure dimensions in typed product Records and XML semantic datums is repetitive; generated/derived semantic datums should come from part Concepts.
- Inherent engineering choice: clearance range, overlap, attachment count/location, service direction, grounding, material/finish, and tolerance policy require product engineering decisions.

## Verdict

Yes: Aetheris now expresses a bounded complete two-part Sheet Metal enclosure as a user-defined generic Firmament Assembly Template composed from ordinary Sheet Metal part Templates. The XML-like Assembly profile is preserved. Body/Lid relationships are typed Interface/Role/Mate semantics, and Forge-facing C# invokes the same construction to obtain Body, Lid, Assembly, per-part flats, artifacts, BOM, and product DFM.

The largest remaining blocker for a commercial hardware pipeline is tolerance-backed exact body/body clearance/contact evidence across full formed assemblies, including coatings and manufacturing process variation. Current nominal relational DFM and exact positive-volume interference are useful, but they are not yet a production fit/tolerance certification system.
