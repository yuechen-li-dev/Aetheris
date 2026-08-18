# Development documentation

This directory contains historical engineering reports, architecture work, milestones, experiments, implementation evidence, and scope contracts. It is not canonical user documentation. For current supported behavior, use [`docs/public`](../public/README.md).

- `architecture/`: system design, archaeology, and durable internal contracts.
- `milestones/`: chronological feature work and checked-in evidence.
- `implementation/`: implementation investigations and productionization reports.
- `scope-contracts/`: phase boundaries, readiness contracts, and closeouts.
- `history/`: superseded documentation retained for provenance, including old Firmament authoring references.
- `audits/`, `reports/`, and `tooling/`: bounded engineering reviews and tool work.

Historical source examples preserve the syntax they originally exercised. They do not override the Preview 3 public docs or the executable. Maintainer build/test policy is recorded in [`milestones/general/build-test-policy-net10-and-legacy-v1.md`](milestones/general/build-test-policy-net10-and-legacy-v1.md).

Generated development output is local by default. See the [generated-artifact policy](GENERATED-ARTIFACT-POLICY.md) before running evidence tools or promoting any generated result into this tree.
