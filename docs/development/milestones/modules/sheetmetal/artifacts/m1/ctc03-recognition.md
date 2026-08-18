# CTC-03 recognition

Status: **Partial**. Aetheris believes CTC-03 is a 1.90754 mm (0.075 in) constant-thickness fabricated sheet with a dominant planar base, seven cylindrical bends, seven attached planar regions, and two long profile/slot openings. Partial is deliberate because the recognizer does not yet stitch every cap/cut/end face into exact global blank topology.

Thickness tolerance is 0.01 mm. `JudgmentEngine` selected the dominant candidate cluster from 15 admitted source pairs: eight plane pairs and seven coaxial cylinder pairs. Every admitted residual is below tolerance. The candidate inside radius is 6.35 mm (0.25 in) for all seven bends.

| Bend | Angle | Axis direction | Direction | Adjacent regions |
|---|---:|---|---|---|
| `bend-c-0005-0024` | 90° | +X | Down | p0061/0066 → p0088/0089 |
| `bend-c-0006-0009` | 90° | +X | Up | p0062/0063 → p0096/0097 |
| `bend-c-0046-0050` | 45° | +Y | Up | p0109/0111 → p0118/0119 |
| `bend-c-0057-0070` | 90° | +Y | Up | p0065/0069 → p0118/0119 |
| `bend-c-0058-0064` | 90° | +X | Up | p0062/0063 → p0065/0069 |
| `bend-c-0059-0068` | 90° | +Y | Up | p0065/0069 → p0102/0104 |
| `bend-c-0060-0067` | 90° | +X | Up | p0061/0066 → p0065/0069 |

Base selection is deterministic heuristic evidence: largest recovered planar area, then stable-ID tie-break. Bends, regions, and cuts retain ordered STEP face/edge bindings. No filename, product name, PMI label, or hardcoded CTC coordinate participates in recognition.

The recovery result is serialized in [`ctc03-recovered.firmament`](ctc03-recovered.firmament). This loss-aware form records analytic plane/cylinder frames, reference boundaries, bend adjacency and policy, feature ownership, source face bindings, and the `Partial` status. It does not copy or claim authority over the source STEP BRep. Parsing and lowering this file reproduces the CTC flat hash `2a78092499226525b3b12cdd403d52d1dfa40224a1e23cf296415fc8f153f7f2`.
