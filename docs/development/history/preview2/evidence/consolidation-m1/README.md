# P2-CONSOLIDATION-M1 evidence

- `limitation-inventory.json`: classified current limitations and historical
  seams using the A-F milestone taxonomy.
- `concept-path-support-matrix.csv`: positive/negative capability matrix.
- `workflow-evidence.json`: direct, native Table/Record/Template, InlineStep,
  Forge, FEA, SurfaceMeshIR, and deterministic-hash results.

The limitation search covered public docs/manifests, compiler diagnostics, and
test skip metadata. Historical experiment/research notes were not rewritten en
masse: where they preserve a past milestone, the current architecture document
or a superseded note is authoritative. No xUnit skipped Fact/Theory declarations
were found in the audited .NET projects.

Consistency findings:

1. Preview 1 release documents accurately recorded the old Compose restriction
   but looked current in repository search; they now carry historical notices.
2. The Preview 1 capability manifest and Preview 2 roadmap contained the stale
   active limitation; both now report the closed behavior.
3. The SurfaceMeshIR M1 document listed families subsequently delivered in
   M2-M7; it now labels that paragraph as M1-era history and summarizes current
   bounds.
4. The CIR/FRep authoritative-volume roadmap predated the settled authority
   split; it now carries a superseded-architecture notice without erasing useful
   historical work.
5. FEA docs already describe basic linear elasticity as implemented and clearly
   separate future curved/nonlinear/contact/dynamics work.
6. Forge docs already state the one-output/prism/sourcegen/import/discovery
   bounds precisely; they remain deliberate rather than stale.
