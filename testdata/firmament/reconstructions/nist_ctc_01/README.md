# NIST CTC-01 reconstruction (attempt 1)

This folder captures checkpoint-oriented Firmament artifacts for the first bounded
reconstruction attempt of:

- `testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp`

## Checkpoints

- `ctc01_attempt1_cp0.firmament`: base stock envelope only.
- `ctc01_attempt1_cp1.firmament`: base + one major cylindrical subtract.
- `ctc01_attempt1_cp2.firmament`: attempts additional cylindrical families; currently expected to fail in bounded boolean composition.
- `ctc01_attempt1_cp3.firmament`: extends cp2 with additive ribs (pre-fillet topology target), currently blocked by cp2 failure.
- `ctc01_attempt1_cp4.firmament`: fillet-stage-A attempt, currently blocked by cp2 failure.

## Current bounded blocker

The current boolean bounded-family pipeline rejects continuation past the early
cylindrical subtract chain in cp2 (`BlindContinuationOutsideBoundedFamily`).

As a result, this attempt currently reaches meaningful progression through cp1,
with cp2+ retained as explicit next-step probes.

## CTC01-RECONSTRUCTION-A1

`ctc01_reconstruction_a1.firmament` is the separate Firmament V2 pressure-test
result. It does not extend the parser or geometry kernel and does not reuse the
legacy Boolean checkpoint chain. It compiles a Concept Struct scaffold, a
rectangular primary-plate subset, and two exact four-hole patterns through the
current production route. The full audit, comparison, M8 contradiction, and gap
ledger are in `docs/reconstruction/ctc01-reconstruction-a1.md` and
`artifacts/reconstruction/ctc01/`.

## Semantic prismatic reconstruction

- `ctc01_prismatic_blockout_x2.firmament`: initial exact Profile/Compose blockout.
- `ctc01_prismatic_blockout_x3.firmament`: shared Concept/template scaffold, exact lobe arcs, mid-level web, central hex, and stable semantic selections.
- `ctc01_prismatic_blockout_x4.firmament`: preserves X3 and adds the four reference-backed Ø35 mounting holes as semantic `Hole<Shaft>/ThroughAll` features inside the composition.

The X4 investigation, feature ranking, Cadmata evidence, verification, and LLM-authoring friction log are in `docs/reconstruction/ctc01-llm-pressure-x4.md`.
