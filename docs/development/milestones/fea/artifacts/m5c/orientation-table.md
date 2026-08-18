# M5C frozen-policy orientation table

The canonical table and interpretation are in `../../m5c-immersed-basis-robustness.md`. Each case directory contains AnalysisIR, native results, residual history, sparse metrics, boundary quadrature, conventional Abaqus deck, and the required `numerical-lowering-strategy-map.json`.

The 15/20/45 directory is final M5C output. `m5b-failure-reproduction` is the untouched pre-change control. Their maximum displacement changes from 2.985701e-3 m to 1.405287e-5 m and diagonal ratio from 2.862738e10 to 5.003115e1 on the same physical source and lattice policy.

