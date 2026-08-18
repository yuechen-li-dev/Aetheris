# Topology parity

The extraction preserves allocation order, edge/coedge flags, geometry creation, binding order, and public dispatch. Canonical fingerprints below are from the post-extraction real facade/builder path; existing pre-M3 behavior/count assertions remain green.

| Case | V | E | F | Curves | Surfaces | Edge/face bindings | Structural/manifold evidence | STEP/reimport | STEP SHA-256 |
|---|---:|---:|---:|---:|---:|---:|---|---|---|
| two-cell orthogonal union | 8 | 12 | 6 | 12 | 6 | 12 / 6 | canonical binding/graph validation; strict incidence deferred for merged-rectangle T-junction seam | yes / yes | `09e6b8d838d08748d60fe6f08f0bfcb09282eb983d26d6fc51956cf5e8828de2` |
| cylinder-root keyway | 8 | 12 | 6 | 12 | 6 | 12 / 6 | binding/graph valid; strict two-use shell incidence passes | yes / yes | `b91443673ab595a0449e91359aa697ae597a03efdb90d7394ac937107e25ae98` |
| polygonal through cut | 128 | 192 | 66 | 192 | 66 | 192 / 66 | binding/graph valid; strict two-use shell incidence passes | yes / yes | `8554faf173a41abeb15facbeb2bd3cceb4f2ea486d6aa1e1b11c0740b922fe7d` |

No semantic bindings or provenance are fabricated by Surgery. These recipes retain their existing `SafeBooleanComposition` handling above the layer.
