# M4 intersection authority audit

The audit covered Kernel.Core, BRep Boolean routes, analytic cone/plane support, CLI line/surface sectioning, clipping and triangulation, Panel boundary and mate checks, Drawing HLR, SurfaceMeshIR, STEP import/recovery, and restricted-field trim prototypes.

| Area | Classification | Authority assessment | M4 action |
|---|---|---|---|
| `TransverseConePlaneIntersection` | bounded constructive, analytic | healthy bounded construction: caller intent fixes cone/world-Z section and hyperbola family | unchanged; regression tested |
| `BrepBoolean` and recognized family builders | topology-authoring, bounded recognized cases | contained legacy behavior; topology is explicitly requested by a Boolean and routed through recognized families/JudgmentEngine | unchanged; generic query has no dependency on it |
| STEP analyzer plane sections and ray hits | internal query-like; analytic plus tessellated fallback | contained analysis behavior; section output is explicitly requested and provenance identifies fallback | unchanged |
| STEP importer polygon crossings/recovery | internal topology recovery, approximate planar predicates | future architectural risk because imported loop-role recovery can affect topology; already bounded by tolerance, diagnostics, and JudgmentEngine | documented, not refactored in M4 |
| Firmament restricted-field/marching contours | approximate/sampled, internal-only | contained legacy prototype; records say numerical-only/not exportable and BRep topology not emitted | unchanged |
| Profile arrangement line/arc intersections | topology-authoring inside explicit profile construction | healthy bounded construction: authored profile intent requires splitting/reconstruction | unchanged |
| Planar triangulation, display-loop checks, feature-band clipping | internal-only planar predicates | healthy implementation detail for already-authored topology/display | unchanged |
| Drawing projection/HLR segment cuts | approximate display-only | no model authority | unchanged |
| SurfaceMeshIR and display tessellation | sampled/approximate visualization | no topology authoring; analytic BRep remains owner | unchanged |
| Panel boundary self-crossing and G0/G1/G2 mates | sampled validation evidence | healthy query-like validation; does not create topology | unchanged; Panel patches dogfood M4 |
| BRep interference and AABB overlap | generic query-like, bounded | observational verification/candidate pruning | unchanged |

No release-blocking path was found where an arbitrary M4 numerical witness directly becomes an edge, trim, face, or Boolean result. The principal future risk is STEP recovery, where approximate planar crossing facts participate in import normalization; that path is explicit, diagnosed, and utility-selected, so a broad M4 refactor would increase risk without strengthening the new public predicate.

