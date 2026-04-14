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
