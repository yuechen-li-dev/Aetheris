# Cadmata semantic inspector M2

Cadmata’s fixture display contract remains compiler-authored: entities publish stable IDs, semantic kind/label, source span, hierarchy, topology descendants, material descendants, diagnostics, and metadata. The M2 extension treats `Datum` and `HoleDiameter` as inspectable entities rather than renderer details.

Selection is a single stable-ID state. The document-scoped semantic index builds correspondence once per artifact; semantic selections traverse only published relations and highlight BRep face/edge descendants. Geometry picking remains a BRep hit; imported STEP entity IDs are not currently in the fixture contract and are shown honestly as unavailable.

The viewport annotation layer is separate from mesh materials. It uses HTML screen-space labels plus Three leader lines, hides with standard HTML occlusion, and is controlled by the PMI toggle. Hole dimensions render `⌀nominal +plus/-minus`; datum labels render an associated leader. Annotation colors are viewport-theme tokens, independent of the warm shell theme.

The `profile-compose-l-bracket-counterbore-pmi` fixture publishes current supported Datum and HoleDiameter PMI with nominal, asymmetric tolerance, datum refs, target, and projection/manual metadata. Template instances are not yet published by the compiler fixture channel; the generic entity model can represent `TemplateInstance` once it supplies template name, instance name, parameters, and generated child IDs.

MachinaLayout is used for property tables and match-based inspector rendering; the tree/annotation DOM stays plain React because it is dynamic scene content rather than static layout.

Current limitation: AP242 imported PMI and raw STEP entity IDs are not published by the server display contract yet; M2 visualizes compiler-authored Firmament PMI only.

## Closeout

The projected `pmi-projected-hole-diameter` fixture exercises the full Static/Require chain. The server exports and reimports the real model, publishes every BRep face plus the hole-shaft ownership, and exposes `MountDiameterConstraint`, `Mount.Diameter`, nominal/tolerance, Datum A, expected value, expected provenance, and tolerance source. Viewport BRep picks resolve through the precomputed published-ID index; no browser-side geometric classification is performed.

Browser evidence is under `docs/preview2/evidence/cadmata-m2/`. Imported AP242 PMI, exhaustive STEP entity IDs, broad Pattern/EdgeFinish coverage, and TemplateInstance UI remain intentionally deferred.
