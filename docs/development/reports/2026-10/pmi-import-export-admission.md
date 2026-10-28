# Import-tolerant, export-strict PMI admission

Date: 2026-10-27

## Decision and implementation

A source-integrity pin identifies bytes; it does not select an editing policy.
Unchanged `InlineStep` annotation/re-export now uses the existing trusted
production exporter with enforced output preflight. It no longer requires the
importer's aggregate `GeometryStatus` to be `Qualified`.

| Boundary | Required evidence |
|---|---|
| Import/inspection | Retain geometry and report actual recovery/qualification; do not promote incomplete evidence |
| Exact Firmament part import | Explicit exact-BRep lane; no automatic tessellation/approximation fallback |
| Unchanged part plus PMI | Source receipt; unchanged topology, 3D supports/curves, edge trims, face bindings and vertices; proved PMI correspondence |
| Production export | Binding integrity, imported shell enclosure, canonical representation and enforced 3D boundary preflight |
| Optional UV traces | Independently validated set; otherwise omit it with `step-export-recovered-pcurves-omitted` |
| Trim-dependent editing | Existing operation-specific complete-trim/geometry guards remain in place |

Physical consistency allowance is capped at 0.1 mm on this imported-part route.
No recovery threshold was increased and manufacturing tolerances are not used as
healing allowances. Source qualification remains visible and is not relabeled as
complete after UV omission. Optional UV changes are excluded from the unchanged
PMI witness; its engineering topology, geometry and associations remain checked.

`TrustedProductionRoute` now defaults to `Enforce`. The low-level `LegacyRoute`
retains its compatibility/debug audit default; it is not the policy selected by
normal unchanged Firmament import/export. Explicit diagnostic overrides remain
available. Production normalization also uses physical boundary and shell
evidence instead of demanding complete UV recovery before or after normalization.

Representation policy remains analytic first, then admitted exact/bounded
non-rational geometry. Intentional polynomial B-splines remain supported. The
only rational product projection exception is a native ellipsoid's exact STEP
patches. Arbitrary rational geometry cannot bypass this policy through auxiliary
PMI dependencies. Geometry-changing normalization still cannot bypass the PMI
body witness.

## Actual geometry defects found by enforcing the boundary

The first strict run admitted thirteen exact NIST specimens and exposed three
existing representation defects. These were repaired before qualification:

- **FTC-07 and FTC-10:** an `EDGE_CURVE` can trim a B-spline through its vertices
  without a `TRIMMED_CURVE` wrapper. Import previously assigned the entire basis
  domain. Examples failed endpoint consistency by approximately 0.358/0.595 mm
  and 0.244 mm. Import now infers a bounded endpoint inverse, accepts a measured
  unique candidate within 0.000001 mm, and retains the original interval when it
  cannot resolve one. Explicit parameter trims are never overridden. The spline
  control net and topology vertices are unchanged.
- **STC-10:** the polynomial evaluator returned the last control point at every
  domain end. That is incorrect for non-clamped/periodic splines; the observed
  endpoint error was about 1.457 mm. Position and derivative evaluation now use
  the existing de Boor basis at the boundary. A separate uniform-basis test
  checks position, tangent, second derivative, rational unit-weight agreement
  and continuity at the domain end.

Strict output still rejects an inconsistent physical boundary. An invalid
optional UV trace can be omitted while preserving its source body's PMI. Tests
exercise both outcomes through the actual exporter.

Full qualification additionally exposed a historical canonical-box fixture whose
top cap listed reversed directed edges in forward order. Import now repairs only
a uniquely determined directed vertex cycle, preserving every edge, vertex,
coedge identity and edge sense. Ambiguous branches or disconnected components
are not guessed or stitched. Recovery is disclosed by
`step-import-loop-order-recovered`; the original STEP fixture is unchanged.

The historical ruled "production" probe is an open face with four boundary
edges. Its old success expectation is corrected to inspection success and solid
export refusal. A separate closed elliptical extrusion fixture qualifies the
positive workflow. `canon` now uses exact import, strict production export and
semantic PMI transfer in both metadata modes; deterministic headers are not an
audit bypass. Canonicalization warnings survive JSON/text presentation.

## Compatibility and baseline changes

The original NIST corpus still has sixteen exact successes and the explicitly
unsupported FTC-08 e1-tg input. Exactly three canonical serialization hashes were
updated after the endpoint repairs:

| Specimen | Previous SHA-256 | Corrected SHA-256 |
|---|---|---|
| FTC-07 e2 | `e2642cad595fb2ead2e3947f7a36fb7bba9b2deff1228ece9516c9fd73ad32ab` | `ea03b61c119b5b72c369a5b879cded916df4eece654a612fa3641845192060a7` |
| FTC-10 e2 | `0a8a7d4b2139e3c7974472a2db1b328ac5a8a75869dee7e869139d96dfb16d58` | `eff9d4990ed0509a2105389cfa785c9ba253a69ff0f7f1f2b8c2bfe4c07b73b5` |
| STC-10 e2 | `12f19eb86e1c02791a83fe1a75cb89920e5638f96ce09dce9816e1338d04ec8a` | `b3fefea3aa7068984fa1e2beb809b3bfe8ebb9176a913ac967b2e9101a57319b` |

All other fields in those frozen reports are unchanged: success status,
diagnostics, source sizes, and vertex/edge/face counts. The remaining fourteen
reports were not refreshed. These are explicit corrections to boundary
interpretation, not blanket baseline regeneration or an assertion that every
previously emitted byte is unchanged.

FTC-11's former invalid fixture is now the buildable
[`nist-ftc11-passthrough.firmament`](../../../../fixtures/Canonical/PMI/nist-ftc11-passthrough.firmament),
with retained source PMI and an added control. The old refusal expectation is
replaced by a real compiler test that preserves the source qualification and
requires strict output admission.

## Qualification

Persistent coverage includes all seventeen NIST inputs through source loading,
pin verification and the actual Firmament compiler; all sixteen exact inputs
through strict core PMI export; native ellipsoid rational projection; rational
auxiliary-PMI rejection; invalid physical boundaries; and invalid optional UV
data with unchanged semantic PMI. Original semantic round-trip and topology
correspondence tests remain in place.

Generated evidence and logs are in ignored
`artifacts/local/pmi-admission-audit/`. Public behavior is documented in
[STEP import](../../../public/firmament/step-import.md) and
[PMI](../../../public/firmament/pmi.md).

Final qualification against the frozen implementation:

- Complete solution Release build: zero errors, 38 warnings.
- Core fast lane: 1,141 passed.
- Complete solution regression: 6,234 passed, zero failed, two skipped across
  twenty test assemblies. The skips are optional external PDF rasterization
  tests requiring `AETHERIS_TEST_PDFTOPPM`.
- Rebuilt managed/WASM SDK; all nineteen JavaScript SDK tests passed.
- Fresh CLI corpus run: sixteen inputs passed `validate`, `build` and output
  analysis with semantic PMI retained. FTC-08 e1-tg remained an exact-geometry
  admission refusal, also reproduced with `geometryOnly`.
- Focused compatibility checks covered the repaired canonical box, explicit
  open-surface refusal, closed elliptical solid, and PMI preservation in both
  canonicalization metadata modes.
- `git diff --check` passed. No task-owned .NET runtime processes remained after
  qualification.

## Verdict and remaining limits

**Success for unchanged exact part import, PMI and strict re-export.** The
sixteen-part NIST capability is available through the normal compiler path,
without relabeling incomplete UV recovery as complete or weakening physical
boundary rejection. Unsupported FTC-08 is still explicit. Three serialization
changes repair documented source interpretation defects; accepted output is not
claimed byte-identical to the previous faulty boundary interpretation.

This does not qualify arbitrary imported-body edits or migration of PMI through
topology changes. Those retain their operation-specific guards. The preflight
reports its own bounded coverage; it is not a proof of every geometric property.
Legacy audit APIs remain available for compatibility and diagnosis. Product
Firmament imports and `canon` select the enforced production path explicitly.
